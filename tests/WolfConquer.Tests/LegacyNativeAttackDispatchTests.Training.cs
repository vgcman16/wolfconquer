using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public void ZeroTrainingDamageReturnsBeforeAnyOwnerAttackDictionaryOrStreamRead() =>
        LegacyNativeUpdateSpell.CheckUpdate(null!, null!, null!, 0, null!);

    [Theory]
    [InlineData(2)] [InlineData(24)] [InlineData(28)]
    public Task WholeTrainingRetainsSourceDoubleWeaponProficiencyUpdateWhenSpellRecordExists(int attackType) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Attack.AtkType = (ushort)attackType;
        s.Client.Equipment!.RightWeapon = 410009; s.Client.Equipment.LeftWeapon = 420009;
        var right = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 1000 };
        var left = new LegacyNativeMsgProficiency { ID = 420, Level = 5, Experience = 2000 };
        s.Client.MyProfs!.ClientProf[410] = right; s.Client.MyProfs.ClientProf[420] = left;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, s.Rows);
        Assert.Same(right, s.Client.MyProfs.ClientProf[410]); Assert.Same(left, s.Client.MyProfs.ClientProf[420]);
        Assert.Equal(1200u, right.Experience); Assert.Equal(2200u, left.Experience);
        Assert.Equal(new ushort[] { 2199, 2199, 2199, 2199 }, s.Queue.Select(Opcode));
        Assert.Equal(new uint[] { 410, 420, 410, 420 }, s.Queue.Select(bytes => BitConverter.ToUInt32(bytes, 12)));
    });

    [Theory]
    [InlineData(0, false, 0u)] [InlineData(0, true, 100u)]
    [InlineData(2, false, 100u)] [InlineData(2, true, 200u)]
    public Task TrainingDictionaryAndAttackTypeControlIndependentWholeProficiencyPasses(int attackType, bool dictionary, uint gain) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Attack.AtkType = (ushort)attackType; s.Client.Equipment!.RightWeapon = 410009;
        var prof = new LegacyNativeMsgProficiency { ID = 410, Level = 5 }; s.Client.MyProfs!.ClientProf[410] = prof;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, dictionary ? s.Rows : null!);
        Assert.Equal(gain, prof.Experience); Assert.Equal((int)(gain / 100), s.Queue.Count);
    });

    [Theory]
    [InlineData(1000u, 0, 1000)] [InlineData(1001u, 1, 0)]
    public Task TrainingRequiresStrictGreaterExperienceForSingleLevelAndResetsRatherThanCarryingRemainder(
        uint damage, ushort level, int experience) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(1000);
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, damage, s.Rows);
        Assert.Equal(level, s.ClientSpell.Level); Assert.Equal(experience, s.ClientSpell.Experience);
        byte[][] frames = s.Queue.ToArray(); Assert.Equal(level == 0 ? new ushort[] { 2101 } : new ushort[] { 2179, 2101 }, frames.Select(Opcode));
        var sent = NativeSpellTestFixture.ReadSpell(frames.Last()); Assert.Equal(level, sent.Level); Assert.Equal(experience, sent.Experience);
    });

    [Fact]
    public Task TrainingRestoresPreviousLevelWithoutTableReLookupOrGrantingOrdinaryExperience() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(1000); s.ClientSpell.PreviousLevel = 9;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 1, s.Rows);
        Assert.Equal((ushort)9, s.ClientSpell.Level); Assert.Equal(1, s.ClientSpell.Experience);
        Assert.False(s.Rows.ContainsKey(9)); Assert.Equal(new ushort[] { 2179, 2101 }, s.Queue.Select(Opcode));
    });

    [Theory]
    [InlineData(true, 0)] [InlineData(false, 141)]
    public Task RuneAndNeedLevelGatesSuppressSpellChangesButDoNotInventOwnerRepair(bool rune, ushort needLevel) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(); s.Row.isRuneSkill = rune; s.Row.NeedLevel = needLevel; s.ClientSpell.Experience = 33;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 1000, s.Rows);
        Assert.Equal((ushort)0, s.ClientSpell.Level); Assert.Equal(33, s.ClientSpell.Experience); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task TrainingRejectsWrappedRateProductBeforeEitherProficiencyPass() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(uint.MaxValue); s.ClientSpell.Experience = int.MaxValue;
        LegacyNativeServerConfig.ExpRateSpell = uint.MaxValue;
        var (right, left) = PrepareOverflowTrainingProficiencies(s);
        var before = CaptureOverflowTrainingPacket(s);
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 2, s.Rows);
        AssertOverflowTrainingRefused(s, right, left, int.MaxValue, before);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingOrNullRequiredRowRefusesBeforeProficiencyOrSpellMutation(bool nullRow) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Skill(1195, 1); s.Rows.Clear(); s.Rows[0] = s.Row; s.Rows[2] = s.Row; s.Rows[3] = s.Row;
        if (nullRow) s.Rows[1] = null!;
        s.Client.Equipment!.RightWeapon = 410009; var prof = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        s.Client.MyProfs!.ClientProf[410] = prof;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, s.Rows);
        Assert.Equal(7u, prof.Experience); Assert.Equal((ushort)1, s.ClientSpell.Level); Assert.Equal(0, s.ClientSpell.Experience); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MissingProficiencyOwnerRefusesBeforeSpellCredit() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(); s.Client.Equipment!.RightWeapon = 410009; s.Client.MyProfs = null;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, s.Rows);
        Assert.Equal(0, s.ClientSpell.Experience); Assert.Equal((ushort)0, s.ClientSpell.Level); Assert.Empty(s.Queue);
    });

    [Fact]
    public Task MissingSpellOwnerRefusesBeforePhysicalProficiencyCredit() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Client.Equipment!.RightWeapon = 410009;
        var prof = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 }; s.Client.MyProfs!.ClientProf[410] = prof;
        s.Client.MySpells = null;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, s.Rows);
        Assert.Equal(7u, prof.Experience); Assert.Empty(s.Queue);
    });
    [Fact]
    public Task PhysicalTrainingWithNullDatabaseDoesNotRequireSpellOwner() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Attack.AtkType = 2; s.Client.Equipment!.RightWeapon = 410009;
        var prof = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        s.Client.MyProfs!.ClientProf[410] = prof; s.Client.MySpells = null;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, null!);
        Assert.Equal(107u, prof.Experience); Assert.Equal((ushort)2199, Opcode(Assert.Single(s.Queue)));
    });

    [Fact]
    public Task NoActiveTrainingPathDoesNotRequireUnrelatedOwnersOrPacket() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.Attack.AtkType = 0; s.Client.MySpells = null;
        s.Client.MyProfs = null; s.Client.Equipment = null;
        LegacyNativeUpdateSpell.CheckUpdate(null!, s.Client, s.Attack, 600, null!);
        Assert.Empty(s.Queue);
    });

}
