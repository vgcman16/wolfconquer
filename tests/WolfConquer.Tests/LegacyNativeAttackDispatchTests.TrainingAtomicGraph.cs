using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public Task TrainingKeepsExistingRowsWhenSecondPassCreatesAMissingWeapon() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.NextLevel(1000); scope.Attack.AtkType = 2;
        scope.Client.Equipment!.RightWeapon = 410009; scope.Client.Equipment.LeftWeapon = 420009;
        var profs = scope.Client.MyProfs!;
        var priorMap = profs.ClientProf;
        var retained = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7,
            NeededExperience = 1600000, PreviouseLevel = 3 };
        priorMap[410] = retained;
        var spell = scope.ClientSpell;

        LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 600, scope.Rows);

        Assert.NotSame(priorMap, profs.ClientProf);
        Assert.Same(retained, profs.ClientProf[410]); Assert.Equal(207u, retained.Experience);
        Assert.Same(spell, scope.Native.Storage.ClientSpells[1195]); Assert.Equal(600, spell.Experience);
        Assert.Equal(2u, profs.ClientProf[420].Level);
        Assert.Equal(58800u, profs.ClientProf[420].Experience);
        Assert.Equal(new ushort[] { 2199, 2041, 2199, 2101, 2199, 2179, 2179, 2041, 2199 }, scope.Queue.Select(Opcode));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GenuineProficiencyComparerRawSpellReplacementRefusesAllPreparedTraining(bool resize) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.NextLevel(1000); scope.Client.Equipment!.RightWeapon = 410009;
        var originalSpell = scope.ClientSpell;
        var rawMap = scope.Native.Storage.ClientSpells;
        var replacement = new LegacyNativeMsgSpell { ID = 1195, Level = 3, Experience = 17 };
        bool triggered = false;
        var comparer = new DirectWriterProbeComparer<uint>(() =>
        {
            triggered = true;
            rawMap[1195] = replacement;
            if (resize)
                for (ushort id = 2000; id < 3000; id++) rawMap[id] = new() { ID = id };
        });
        var profs = scope.Client.MyProfs!;
        var rawProfs = new ConcurrentDictionary<uint, LegacyNativeMsgProficiency>(comparer);
        var proficiency = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        rawProfs[410] = proficiency; profs.ClientProf = rawProfs;
        var packet = new LegacyNativePacketMutationWitness(scope.Packet);
        comparer.Arm();

        LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 6, scope.Rows);

        Assert.True(triggered); Assert.Same(rawMap, scope.Native.Storage.ClientSpells);
        Assert.Same(replacement, rawMap[1195]); Assert.Equal(17, replacement.Experience);
        Assert.Equal(0, originalSpell.Experience); Assert.Equal((ushort)0, originalSpell.Level);
        Assert.Same(rawProfs, profs.ClientProf); Assert.Same(proficiency, rawProfs[410]);
        Assert.Equal(7u, proficiency.Experience); Assert.Equal(5u, proficiency.Level);
        packet.RequireUnchanged(); Assert.Empty(scope.Queue);
    });
}
