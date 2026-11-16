namespace Xbox_Control.Tests
{
    public class WalkthroughKeyMapperTests
    {
        private static GamepadSnapshot Pad(
            float lx = 0, float ly = 0, float rx = 0, float ry = 0,
            float lt = 0, float rt = 0, GamepadButtons buttons = GamepadButtons.None)
            => new(lx, ly, rx, ry, lt, rt, buttons);

        private static void AssertPlan(KeyPlan plan, Modifiers mods, params Key[] keys)
        {
            Assert.Equal(mods, plan.Modifiers);
            Assert.Equal(keys.OrderBy(k => k), plan.Keys.OrderBy(k => k));
        }

        [Fact]
        public void RightBumper_ZoomsIn_WithShiftZ()
        {
            // The old code sent plain Z here, which is Zoom OUT in the Walk-through.
            var plan = new WalkthroughKeyMapper().Map(Pad(buttons: GamepadButtons.RightShoulder));
            AssertPlan(plan, Modifiers.Shift, Key.Z);
        }

        [Fact]
        public void LeftBumper_ZoomsOut_WithPlainZ()
        {
            var plan = new WalkthroughKeyMapper().Map(Pad(buttons: GamepadButtons.LeftShoulder));
            AssertPlan(plan, Modifiers.None, Key.Z);
        }

        [Fact]
        public void LeftStick_Walks_And_Strafes_WithoutModifiers()
        {
            var m = new WalkthroughKeyMapper();
            AssertPlan(m.Map(Pad(ly: 1)), Modifiers.None, Key.W);
            AssertPlan(m.Map(Pad(ly: -1)), Modifiers.None, Key.S);
            AssertPlan(m.Map(Pad(lx: -1)), Modifiers.None, Key.A);
            AssertPlan(m.Map(Pad(lx: 1)), Modifiers.None, Key.D);
        }

        [Fact]
        public void RightStick_Turns_And_Looks()
        {
            var m = new WalkthroughKeyMapper();
            AssertPlan(m.Map(Pad(rx: 1)), Modifiers.Shift, Key.D);
            m.Reset();
            AssertPlan(m.Map(Pad(rx: -1)), Modifiers.Shift, Key.A);
            m.Reset();
            AssertPlan(m.Map(Pad(ry: 1)), Modifiers.Alt, Key.Up);
            m.Reset();
            AssertPlan(m.Map(Pad(ry: -1)), Modifiers.Alt, Key.Down);
        }

        [Fact]
        public void Triggers_MoveUpAndDown()
        {
            var m = new WalkthroughKeyMapper();
            AssertPlan(m.Map(Pad(rt: 1)), Modifiers.Shift, Key.Up);
            m.Reset();
            AssertPlan(m.Map(Pad(lt: 1)), Modifiers.Shift, Key.Down);
            m.Reset();
            AssertPlan(m.Map(Pad(lt: 1, rt: 1)), Modifiers.None); // both = cancel, not "last one wins"
        }

        [Fact]
        public void Rising_WhileStrafing_DoesNotTurn()
        {
            // Old behavior: Shift (rise) + D (strafe) = Shift+D = turn right.
            var plan = new WalkthroughKeyMapper().Map(Pad(lx: 1, rt: 1));
            AssertPlan(plan, Modifiers.Shift, Key.Up);
        }

        [Fact]
        public void Strafing_WhileTurning_SendsOnlyTheTurn()
        {
            // Old behavior: D (strafe right) + Shift+A (turn left) = turn left AND turn right.
            var plan = new WalkthroughKeyMapper().Map(Pad(lx: 1, rx: -1));
            AssertPlan(plan, Modifiers.Shift, Key.A);
        }

        [Fact]
        public void Walking_WhileTurning_KeepsWalking()
        {
            var plan = new WalkthroughKeyMapper().Map(Pad(ly: 1, rx: 1));
            AssertPlan(plan, Modifiers.Shift, Key.W, Key.D);
        }

        [Fact]
        public void Looking_WhileWalking_NeverSendsAltLetter()
        {
            // Alt+W would open the Window menu.
            var plan = new WalkthroughKeyMapper().Map(Pad(ly: 1, ry: 1));
            AssertPlan(plan, Modifiers.Alt, Key.Up);
        }

        [Fact]
        public void Rising_ThenLooking_DoesNotSendBothModifiers()
        {
            var m = new WalkthroughKeyMapper();
            m.Map(Pad(rt: 1));
            var plan = m.Map(Pad(rt: 1, ry: 1));
            Assert.NotEqual(Modifiers.Shift | Modifiers.Alt, plan.Modifiers);
            AssertPlan(plan, Modifiers.Shift, Key.Up); // the action already running keeps control
        }

        [Fact]
        public void DiagonalRightStick_StaysWithTheAxisThatStartedIt()
        {
            var m = new WalkthroughKeyMapper();
            AssertPlan(m.Map(Pad(rx: 0.9f)), Modifiers.Shift, Key.D);
            // Drifts up a bit while turning: still turning, not flipping to look.
            AssertPlan(m.Map(Pad(rx: 0.7f, ry: 0.6f)), Modifiers.Shift, Key.D);
            // Clearly pushed up instead: look takes over.
            AssertPlan(m.Map(Pad(rx: 0.3f, ry: 0.95f)), Modifiers.Alt, Key.Up);
        }

        [Fact]
        public void Threshold_HasHysteresis()
        {
            var m = new WalkthroughKeyMapper();
            AssertPlan(m.Map(Pad(ly: 0.30f)), Modifiers.None);          // below press threshold
            AssertPlan(m.Map(Pad(ly: 0.40f)), Modifiers.None, Key.W);   // pressed
            AssertPlan(m.Map(Pad(ly: 0.30f)), Modifiers.None, Key.W);   // still held above release
            AssertPlan(m.Map(Pad(ly: 0.20f)), Modifiers.None);          // released
        }

        [Fact]
        public void DPad_TapsSpeedOncePerPress()
        {
            var m = new WalkthroughKeyMapper();
            m.Map(Pad()); // neutral first frame
            Assert.Equal(new[] { Key.Equals }, m.Map(Pad(buttons: GamepadButtons.DPadUp)).Taps);
            Assert.Empty(m.Map(Pad(buttons: GamepadButtons.DPadUp)).Taps); // held: no repeat
            Assert.Empty(m.Map(Pad()).Taps);
            Assert.Equal(new[] { Key.Minus }, m.Map(Pad(buttons: GamepadButtons.DPadDown)).Taps);
        }

        [Fact]
        public void DPad_HeldAtStartup_IsNotAPress()
        {
            var m = new WalkthroughKeyMapper();
            Assert.Empty(m.Map(Pad(buttons: GamepadButtons.DPadUp)).Taps);
        }
    }
}
