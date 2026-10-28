using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task NativeTrainingPublishesSpellAndBothGenuineAdmittedProficienciesTogether()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; RequireCoherent(f);
            var profs = Assert.IsType<LegacyNativeProficiencyStorage>(f.Native.MyProfs);
            Assert.Equal(1000U, Assert.Single(before.PlayerProficiencies.Proficiencies, p => p.Id == 410).Experience);
            Assert.Equal(2000U, Assert.Single(before.PlayerProficiencies.Proficiencies, p => p.Id == 420).Experience);
            Assert.Equal(1000U, profs.ClientProf[410].Experience); Assert.Equal(2000U, profs.ClientProf[420].Experience);
            await NativePacketTestThread.Run(() =>
            {
                var oldEquipment = f.Native.Equipment; uint oldSpellRate = LegacyNativeServerConfig.ExpRateSpell;
                uint oldProfRate = LegacyNativeServerConfig.ExpRateProf;
                try
                {
                    var equipment = new LegacyNativeEquipItemStorage(f.Native) { RightWeapon = 410009, LeftWeapon = 420009 };
                    f.Native.Equipment = equipment; LegacyNativeServerConfig.ExpRateSpell = 1; LegacyNativeServerConfig.ExpRateProf = 1;
                    var metadata = Enumerable.Range(0, 6).ToDictionary(i => (ushort)i,
                        i => new LegacyNativeMagicType.Magic { ID = 6001, Level = (byte)i, Experience = 10000 });
                    var attack = new LegacyNativeInteractQuery { UID = f.Player.UID, OpponentUID = f.Player.UID, SpellID = 6001, AtkType = 24 };
                    using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                    LegacyNativeUpdateSpell.CheckUpdate(packet, f.Native, attack, 600, metadata);
                    var after = f.Current; Assert.NotSame(before, after);
                    Assert.Equal(1711, Assert.Single(after.PlayerSpells.Spells, s => s.Id == 6001).Experience);
                    Assert.Equal(1200U, Assert.Single(after.PlayerProficiencies.Proficiencies, p => p.Id == 410).Experience);
                    Assert.Equal(2200U, Assert.Single(after.PlayerProficiencies.Proficiencies, p => p.Id == 420).Experience);
                    Assert.Equal(1200U, profs.ClientProf[410].Experience); Assert.Equal(2200U, profs.ClientProf[420].Experience);
                    RequireCoherent(f);
                }
                finally { f.Native.Equipment = oldEquipment; LegacyNativeServerConfig.ExpRateSpell = oldSpellRate; LegacyNativeServerConfig.ExpRateProf = oldProfRate; }
            });
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false, training: true);
    }
}
