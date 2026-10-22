using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    private sealed class SkillRandom : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly LegacyNativeMagicType previousMagic = LegacyNativePoolMagic.Magic;
        internal LegacyNativeSafeRandom Control { get; }
        internal LegacyNativeMagicType Magic { get; } = new();

        internal SkillRandom(int seed = 9177)
        {
            Assert.NotEqual(0, seed); // Genuine native SetSeed ignores its argument; do not use it as a seed setter.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(seed);
            Control = new LegacyNativeSafeRandom(seed);
            LegacyNativePoolMagic.Magic = Magic;
        }

        internal void SameNext() => Assert.Equal(Control.Next(), LegacyNativePoolRandom.GetRandom.Next());

        public void Dispose()
        {
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolMagic.Magic = previousMagic;
        }
    }

    private static readonly ushort[] SelectableSkills =
        [16320, 16340, 16410, 16430, 16450, 16470, 16490, 16510, 16530, 16550];
}
