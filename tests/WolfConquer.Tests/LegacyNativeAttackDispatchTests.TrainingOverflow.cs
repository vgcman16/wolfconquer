using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Theory]
    [InlineData(0, uint.MaxValue, 1u)]
    [InlineData(0, 2_147_483_648u, 2u)]
    [InlineData(int.MaxValue, 1u, 1u)]
    [InlineData(int.MaxValue - 1, 2u, 1u)]
    [InlineData(-1, 1u, 1u)]
    [InlineData(int.MinValue, 1u, 1u)]
    public Task TrainingRejectsUnrepresentableOrNegativeSpellExperienceBeforeAnyLocalCredit(
        int experience, uint damage, uint rate) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(uint.MaxValue);
        s.ClientSpell.Experience = experience; LegacyNativeServerConfig.ExpRateSpell = rate;
        var (right, left) = PrepareOverflowTrainingProficiencies(s);
        var before = CaptureOverflowTrainingPacket(s);
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, damage, s.Rows);
        AssertOverflowTrainingRefused(s, right, left, experience, before);
    });

    [Theory]
    [InlineData(0, 2_147_483_647u, 1u, int.MaxValue)]
    [InlineData(int.MaxValue - 1, 1u, 1u, int.MaxValue)]
    [InlineData(33, uint.MaxValue, 0u, 33)]
    public Task TrainingAcceptsRepresentableSignedExperienceBoundaryAndZeroRate(
        int experience, uint damage, uint rate, int expected) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(uint.MaxValue); s.Attack.AtkType = 0;
        s.ClientSpell.Experience = experience; LegacyNativeServerConfig.ExpRateSpell = rate;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, damage, s.Rows);
        Assert.Equal(expected, s.ClientSpell.Experience); Assert.Equal((ushort)0, s.ClientSpell.Level);
        var sent = NativeSpellTestFixture.ReadSpell(Assert.Single(s.Queue));
        Assert.Equal(expected, sent.Experience); Assert.Equal((ushort)0, sent.Level);
    });

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 141)]
    public Task IneligibleSpellTrainingDoesNotBlockTheIndependentPhysicalProficiencyPasses(
        bool rune, ushort needLevel) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); s.NextLevel(uint.MaxValue);
        s.Row.isRuneSkill = rune; s.Row.NeedLevel = needLevel;
        s.ClientSpell.Experience = int.MaxValue; LegacyNativeServerConfig.ExpRateSpell = uint.MaxValue;
        var equipment = Assert.IsType<LegacyNativeEquipItemStorage>(s.Client.Equipment);
        var profs = Assert.IsType<LegacyNativeProficiencyStorage>(s.Client.MyProfs);
        equipment.RightWeapon = 410009;
        var right = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        profs.ClientProf[410] = right;
        LegacyNativeUpdateSpell.CheckUpdate(s.Packet, s.Client, s.Attack, 600, s.Rows);
        Assert.Equal(207u, right.Experience); Assert.Equal(5u, right.Level);
        Assert.Equal(int.MaxValue, s.ClientSpell.Experience); Assert.Equal((ushort)0, s.ClientSpell.Level);
        Assert.Equal(new ushort[] { 2199, 2199 }, s.Queue.Select(Opcode));
    });

    private static (LegacyNativeMsgProficiency Right, LegacyNativeMsgProficiency Left)
        PrepareOverflowTrainingProficiencies(Scope s)
    {
        var equipment = Assert.IsType<LegacyNativeEquipItemStorage>(s.Client.Equipment);
        var profs = Assert.IsType<LegacyNativeProficiencyStorage>(s.Client.MyProfs);
        equipment.RightWeapon = 410009; equipment.LeftWeapon = 420009;
        var right = new LegacyNativeMsgProficiency { ID = 410, Level = 5, Experience = 7 };
        var left = new LegacyNativeMsgProficiency { ID = 420, Level = 5, Experience = 11 };
        profs.ClientProf[410] = right; profs.ClientProf[420] = left;
        s.ClientSpell.PreviousLevel = 7;
        return (right, left);
    }

    private static (int Size, int Position, byte[] Bytes) CaptureOverflowTrainingPacket(Scope s) =>
        (s.Packet.Size, s.Packet.Position, NativeSpellTestFixture.Bytes(s.Packet));

    private static void AssertOverflowTrainingRefused(Scope s,
        LegacyNativeMsgProficiency right, LegacyNativeMsgProficiency left, int experience,
        (int Size, int Position, byte[] Bytes) before)
    {
        var profs = Assert.IsType<LegacyNativeProficiencyStorage>(s.Client.MyProfs);
        Assert.Same(right, profs.ClientProf[410]); Assert.Same(left, profs.ClientProf[420]);
        Assert.Equal(7u, right.Experience); Assert.Equal(11u, left.Experience);
        Assert.Equal(5u, right.Level); Assert.Equal(5u, left.Level);
        Assert.Equal(experience, s.ClientSpell.Experience); Assert.Equal((ushort)0, s.ClientSpell.Level);
        Assert.Equal((byte)7, s.ClientSpell.PreviousLevel);
        Assert.Equal(before.Size, s.Packet.Size); Assert.Equal(before.Position, s.Packet.Position);
        Assert.Equal(before.Bytes, NativeSpellTestFixture.Bytes(s.Packet)); Assert.Empty(s.Queue);
    }
}
