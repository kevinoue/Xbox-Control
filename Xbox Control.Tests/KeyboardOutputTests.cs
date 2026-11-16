namespace Xbox_Control.Tests
{
    public class KeyboardOutputTests
    {
        private readonly List<KeyEvent> sent = new();
        private readonly KeyboardOutput output;

        public KeyboardOutputTests() => output = new KeyboardOutput(events => sent.AddRange(events));

        private static KeyPlan Plan(Modifiers mods, params Key[] keys) =>
            new(mods, new HashSet<Key>(keys), Array.Empty<Key>());

        private static KeyEvent Down(Key k) => new(k, true);
        private static KeyEvent Up(Key k) => new(k, false);

        [Fact]
        public void SameModifiers_SendsOnlyTheDifference()
        {
            output.Apply(Plan(Modifiers.None, Key.W));
            sent.Clear();
            output.Apply(Plan(Modifiers.None, Key.W, Key.D));
            Assert.Equal(new[] { Down(Key.D) }, sent);

            sent.Clear();
            output.Apply(Plan(Modifiers.None, Key.D));
            Assert.Equal(new[] { Up(Key.W) }, sent);
        }

        [Fact]
        public void StoppingOneAction_DoesNotReleaseAKeyAnotherStillNeeds()
        {
            // Turn right (Shift+D) + rise (Shift+Up); then the turn stops.
            output.Apply(Plan(Modifiers.Shift, Key.D, Key.Up));
            sent.Clear();
            output.Apply(Plan(Modifiers.Shift, Key.Up));
            Assert.Equal(new[] { Up(Key.D) }, sent); // Shift stays down, so Up still means "rise"
        }

        [Fact]
        public void ModifierChange_ReleasesAndRepressesCleanly()
        {
            output.Apply(Plan(Modifiers.Shift, Key.D)); // turning
            sent.Clear();
            output.Apply(Plan(Modifiers.None, Key.D));  // now strafing
            Assert.Equal(new[] { Up(Key.D), Up(Key.Shift), Down(Key.D) }, sent);
        }

        [Fact]
        public void Tap_LiftsModifiersFirst_ThenRestoresChord()
        {
            output.Apply(Plan(Modifiers.Shift, Key.A));
            sent.Clear();
            output.Apply(new KeyPlan(Modifiers.Shift, new HashSet<Key> { Key.A }, new[] { Key.Equals }));
            Assert.Equal(new[]
            {
                Up(Key.A), Up(Key.Shift),
                Down(Key.Equals), Up(Key.Equals),
                Down(Key.Shift), Down(Key.A),
            }, sent);
        }

        [Fact]
        public void ReleaseAll_ReleasesKeysThenModifiers()
        {
            output.Apply(Plan(Modifiers.Alt, Key.Up));
            sent.Clear();
            output.ReleaseAll();
            Assert.Equal(new[] { Up(Key.Up), Up(Key.Alt) }, sent);
            Assert.Equal("(none)", output.Held.ToString());

            sent.Clear();
            output.ReleaseAll();
            Assert.Empty(sent); // nothing held, nothing sent
        }

        [Fact]
        public void ReleaseEverything_SendsKeyUpForEveryKnownKey()
        {
            output.ReleaseEverything();
            Assert.All(sent, e => Assert.False(e.Down));
            Assert.Equal(Enum.GetValues<Key>().Length, sent.Count);
        }
    }

    public class DeadzoneTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(3000, -3000)]   // drift inside the round deadzone
        public void SmallDrift_IsZero(short x, short y)
        {
            var (ox, oy) = XInputGamepad.RadialDeadzone(x, y);
            Assert.Equal(0f, ox);
            Assert.Equal(0f, oy);
        }

        [Fact]
        public void FullDeflection_IsOne_AndDirectionIsKept()
        {
            var (x, y) = XInputGamepad.RadialDeadzone(32767, 0);
            Assert.Equal(1f, x, 3);
            Assert.Equal(0f, y, 3);

            var (dx, dy) = XInputGamepad.RadialDeadzone(-23170, 23170); // 45 degrees, full
            Assert.Equal(-0.707f, dx, 2);
            Assert.Equal(0.707f, dy, 2);
        }

        [Fact]
        public void MinValue_DoesNotExceedOne()
        {
            var (x, _) = XInputGamepad.RadialDeadzone(short.MinValue, 0);
            Assert.Equal(-1f, x, 3);
        }
    }
}
