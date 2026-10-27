using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public Task TrainingRejectsForeignPhysicalEquipmentBeforeAnyCreditOrOutput() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var foreign = new NativeSpellTestFixture();
        scope.NextLevel(1000);
        foreign.Owner.Equipment = new(foreign.Owner) { RightWeapon = 410009 };
        scope.Client.Equipment = foreign.Owner.Equipment;
        var proficiency = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        scope.Client.MyProfs!.ClientProf[410] = proficiency;
        var spell = new LegacyNativeSpellMutationWitness(scope.Native.Storage);
        var packet = new LegacyNativePacketMutationWitness(scope.Packet);

        var error = Record.Exception(() =>
            LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 6, scope.Rows));

        spell.RequireUnchanged();
        packet.RequireUnchanged();
        Assert.Equal(7u, proficiency.Experience);
        Assert.Equal(5u, proficiency.Level);
        Assert.Empty(scope.Queue);
        Assert.Empty(foreign.Queue);
        Assert.IsType<InvalidOperationException>(error);
    });

    [Fact]
    public Task TrainingRejectsForeignProficiencyOwnerWhenARealWeaponPassUsesIt() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var foreign = new NativeSpellTestFixture();
        scope.NextLevel(1000);
        scope.Client.Equipment!.RightWeapon = 410009;
        var proficiency = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        foreign.Owner.MyProfs = new(foreign.Owner);
        foreign.Owner.MyProfs.ClientProf[410] = proficiency;
        scope.Client.MyProfs = foreign.Owner.MyProfs;
        var spell = new LegacyNativeSpellMutationWitness(scope.Native.Storage);
        var packet = new LegacyNativePacketMutationWitness(scope.Packet);

        var error = Record.Exception(() =>
            LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 6, scope.Rows));

        spell.RequireUnchanged();
        packet.RequireUnchanged();
        Assert.Same(proficiency, foreign.Owner.MyProfs.ClientProf[410]);
        Assert.Equal(7u, proficiency.Experience);
        Assert.Equal(5u, proficiency.Level);
        Assert.Empty(scope.Queue);
        Assert.Empty(foreign.Queue);
        Assert.IsType<InvalidOperationException>(error);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NoWeaponPassDoesNotRequireAnAbsentOrForeignUnusedProficiencyOwner(bool foreignOwner) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var foreign = new NativeSpellTestFixture();
        scope.NextLevel(1000);
        scope.Client.Equipment!.RightWeapon = 0;
        scope.Client.Equipment.LeftWeapon = 0;
        foreign.Owner.MyProfs = new(foreign.Owner);
        var proficiency = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        foreign.Owner.MyProfs.ClientProf[410] = proficiency;
        scope.Client.MyProfs = foreignOwner ? foreign.Owner.MyProfs : null;

        LegacyNativeUpdateSpell.CheckUpdate(scope.Packet, scope.Client, scope.Attack, 6, scope.Rows);

        Assert.Equal(6, scope.ClientSpell.Experience);
        Assert.Equal((ushort)0, scope.ClientSpell.Level);
        Assert.Equal((ushort)2101, Opcode(Assert.Single(scope.Queue)));
        Assert.Equal(7u, proficiency.Experience);
        Assert.Equal(5u, proficiency.Level);
        Assert.Empty(foreign.Queue);
    });
}
