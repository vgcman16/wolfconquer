using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task ActualNativeSoulHandlerCommitsSelectedSoulToTheAdmittedSession()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var storage = RequireCoherent(f);
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                packet.SpellCreate(new LegacyNativeMsgSpell { ID = 6001, Level = 99, Experience = 7,
                    UseSoul = LegacyNativeMsgSpell.UseSpellSoulTyp.Three });
                LegacyNativeMsgSpell.HandlerSpell(f.Native, packet);
                Assert.NotSame(before, f.Current);
                var current = Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001);
                Assert.Equal(3, current.UseSpellSoul); Assert.Equal(4, current.Level); Assert.Equal(1111, current.Experience);
                var native = Assert.Single(RequireCoherent(f).ClientSpells.ToArray(), p => p.Key == 6001).Value;
                Assert.Equal(LegacyNativeMsgSpell.UseSpellSoulTyp.Three, native.UseSoul);
                Assert.Same(storage, f.Native.MySpells);
            });
        });
    }
}
