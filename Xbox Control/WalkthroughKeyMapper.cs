namespace Xbox_Control
{
    /// <summary>
    /// Turns controller state into the keys for the SOLIDWORKS Walk-through, per the official
    /// key list (2023/2024 help, "Walk-through Control Panel"):
    ///   W / S            move forward / backward        A / D        move left / right
    ///   Shift+A / Shift+D  turn left / right            Shift+Up/Down  move up / down
    ///   Alt+Up / Alt+Down  look up / down               = / -        speed up / down
    ///   Shift+Z          zoom in                        Z            zoom out
    ///
    /// The keyboard has a single Shift and a single Alt, so actions that need different modifiers
    /// cannot be held at the same time without changing each other's meaning (e.g. rising with
    /// Shift+Up while strafing with D turns D into Shift+D = turn). This mapper picks one modifier
    /// group per tick and drops the keys whose meaning would change, so every key that is sent
    /// means what the stick asked for. Moving several things at once fully needs the direct
    /// SolidWorks API path; this is the safe version of the keystroke approach.
    /// </summary>
    public sealed class WalkthroughKeyMapper
    {
        // A direction turns on above PressThreshold and stays on until it falls below
        // ReleaseThreshold, so a stick resting near the edge doesn't rapidly toggle the key.
        public const float PressThreshold = 0.35f;
        public const float ReleaseThreshold = 0.25f;

        // When a Shift action and an Alt action are both requested, the group already active keeps
        // control unless the other one is stronger by this much (stops flip-flopping on diagonals).
        public const float TakeoverMargin = 0.35f;

        // Shift+W / Shift+S aren't listed in the help. Assumed to still walk; verify in SolidWorks.
        public static bool WalkWhileShiftHeld { get; set; } = true;

        private enum Group { None, Shift, Alt }

        private bool fwd, back, left, right, turnL, turnR, lookUp, lookDown, rise, fall;
        private bool prevDPadUp = true, prevDPadDown = true; // true: a button already held at start isn't a press
        private Group current = Group.None;

        public void Reset()
        {
            fwd = back = left = right = turnL = turnR = lookUp = lookDown = rise = fall = false;
            prevDPadUp = prevDPadDown = true;
            current = Group.None;
        }

        public KeyPlan Map(GamepadSnapshot s)
        {
            Latch(ref fwd, s.LeftY);
            Latch(ref back, -s.LeftY);
            Latch(ref right, s.LeftX);
            Latch(ref left, -s.LeftX);
            Latch(ref turnR, s.RightX);
            Latch(ref turnL, -s.RightX);
            Latch(ref lookUp, s.RightY);
            Latch(ref lookDown, -s.RightY);
            Latch(ref rise, s.RightTrigger);
            Latch(ref fall, s.LeftTrigger);

            bool zoomIn = s.IsDown(GamepadButtons.RightShoulder);
            bool zoomOut = s.IsDown(GamepadButtons.LeftShoulder);

            // Opposite requests cancel rather than letting whichever key was sent last win.
            bool doRise = rise && !fall, doFall = fall && !rise;
            bool doZoomIn = zoomIn && !zoomOut, doZoomOut = zoomOut && !zoomIn;

            float shiftStrength = Math.Max(
                (turnL || turnR) ? Math.Abs(s.RightX) : 0f,
                Math.Max(doRise ? s.RightTrigger : doFall ? s.LeftTrigger : 0f, doZoomIn ? 1f : 0f));
            float altStrength = (lookUp || lookDown) ? Math.Abs(s.RightY) : 0f;

            current = ChooseGroup(shiftStrength > 0f, altStrength > 0f, shiftStrength, altStrength);

            var keys = new HashSet<Key>();
            Modifiers mods = Modifiers.None;

            switch (current)
            {
                case Group.None:
                    if (fwd) keys.Add(Key.W);
                    if (back) keys.Add(Key.S);
                    if (left) keys.Add(Key.A);
                    if (right) keys.Add(Key.D);
                    if (doZoomOut) keys.Add(Key.Z);
                    break;

                case Group.Shift:
                    mods = Modifiers.Shift;
                    if (turnL) keys.Add(Key.A);          // Shift+A = turn left
                    if (turnR) keys.Add(Key.D);          // Shift+D = turn right
                    if (doRise) keys.Add(Key.Up);        // Shift+Up = move up
                    if (doFall) keys.Add(Key.Down);      // Shift+Down = move down
                    if (doZoomIn) keys.Add(Key.Z);       // Shift+Z = zoom in
                    if (WalkWhileShiftHeld)
                    {
                        if (fwd) keys.Add(Key.W);
                        if (back) keys.Add(Key.S);
                    }
                    // Not sent under Shift: strafe (A/D would turn) and zoom out (Z would zoom in).
                    break;

                case Group.Alt:
                    mods = Modifiers.Alt;
                    if (lookUp) keys.Add(Key.Up);        // Alt+Up = look up
                    if (lookDown) keys.Add(Key.Down);    // Alt+Down = look down
                    // Not sent under Alt: letters (Alt+letter opens menus) and zoom.
                    break;
            }

            var taps = new List<Key>();
            bool dUp = s.IsDown(GamepadButtons.DPadUp), dDown = s.IsDown(GamepadButtons.DPadDown);
            if (dUp && !prevDPadUp) taps.Add(Key.Equals);   // speed +
            if (dDown && !prevDPadDown) taps.Add(Key.Minus); // speed -
            prevDPadUp = dUp;
            prevDPadDown = dDown;

            return new KeyPlan(mods, keys, taps);
        }

        private Group ChooseGroup(bool wantShift, bool wantAlt, float shiftStrength, float altStrength)
        {
            if (!wantShift && !wantAlt) return Group.None;
            if (wantShift && !wantAlt) return Group.Shift;
            if (wantAlt && !wantShift) return Group.Alt;

            // Both requested (e.g. right stick pushed diagonally, or trigger held while looking).
            return current switch
            {
                Group.Shift => altStrength > shiftStrength + TakeoverMargin ? Group.Alt : Group.Shift,
                Group.Alt => shiftStrength > altStrength + TakeoverMargin ? Group.Shift : Group.Alt,
                _ => altStrength > shiftStrength ? Group.Alt : Group.Shift,
            };
        }

        private static void Latch(ref bool on, float value) =>
            on = on ? value > ReleaseThreshold : value > PressThreshold;
    }
}
