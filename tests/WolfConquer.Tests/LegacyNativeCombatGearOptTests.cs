using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearOptTests
{
    [Fact]
    public void UnloadedGatePrecedesAllNullArgumentsAndWireConversion()
    {
        using var s = new Scope();
        Table.m_Load = false;
        Table.Info = null!;
        Assert.Null(Opt.Process(null!, null!, uint.MaxValue, uint.MaxValue));
        Assert.Null((byte[])new Opt());
        Assert.Null((byte[])(Opt)null!);
        Assert.Null(Table.Info);
        Assert.Null(Gear.GetRandom);
        Assert.Empty(s.Output.ToString());
    }

    [Fact]
    public void NativeDefaultOperationsOnlyLogTheNarrowedTypeWithoutReadingUserOrMetadata()
    {
        using var s = new Scope();
        Table.Info = null!;
        foreach (int type in new[] { 0, 2, 5, 6, 7, 257, -1 })
        {
            var info = Request((Opt.Types)type);
            s.Output.GetStringBuilder().Clear();
            Assert.Empty(Opt.Process(info, null!, 17, 2000));
            Assert.Equal("MsgCombatGearOpt Type->" + unchecked((byte)type) + s.Output.NewLine, s.Output.ToString());
            Assert.Equal((Opt.Types)type, info.Type);
            Assert.Equal(77u, info.UID);
            Assert.Equal(13u, info.Exp);
            Assert.Equal(7u, info.Transform);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenEmitsAnImmutableInitialAckThenMutatesTheSameInfoToEquippedView(bool equipped)
    {
        using var s = new Scope();
        var weapon = s.Weapon(50, 23, id: 202);
        weapon.Equip = equipped ? 1u : 2u;
        weapon.Transform = 31;
        Table.Info = null!; // OPEN does not read DBItem.
        var info = Request(Opt.Types.OPEN);
        var material = new Opt.Items { UID = 9, Size = 10 };
        var materials = new[] { material };
        var values = new uint[] { 5 };
        info.SelectMaterial = materials;
        info.U = values;

        var frames = Opt.Process(info, s.User, 17, 2000);

        const string extra = "30053A040809100A";
        Assert.Equal(equipped ? 2 : 1, frames.Count);
        Operation(frames[0], Opt.Types.OPEN, extra: extra);
        if (equipped) Operation(frames[1], Opt.Types.VIEW, uid: 17, transform: 31, exp: 23, extra: extra);
        Assert.Equal(equipped ? Opt.Types.VIEW : Opt.Types.OPEN, info.Type);
        Assert.Equal(equipped ? 17u : 1u, info.UID);
        Assert.Equal(equipped ? 23u : 13u, info.Exp);
        Assert.Equal(equipped ? 31u : 7u, info.Transform);
        Assert.Same(values, info.U);
        Assert.Same(materials, info.SelectMaterial);
        Assert.Same(material, Assert.Single(info.SelectMaterial));
        Assert.Same(weapon, Assert.Single(s.User.Items).Value);
        var saved = frames.Select(frame => frame.ToArray()).ToArray();
        info.Exp = 999;
        material.Size = 999;
        for (int i = 0; i < frames.Count; i++) Assert.Equal(saved[i], frames[i]);
        Operation(frames[0], Opt.Types.OPEN, extra: extra);
    }

    [Fact]
    public void OpenNullUserFaultHappensAfterMutatingRequestUidWithoutRepair()
    {
        using var s = new Scope();
        var info = Request(Opt.Types.OPEN);
        Assert.Throws<NullReferenceException>(() => Opt.Process(info, null!, 17, 2000));
        Assert.Equal(1u, info.UID);
        Assert.Equal(Opt.Types.OPEN, info.Type);
        Assert.Equal(13u, info.Exp);
        Assert.Equal(7u, info.Transform);
    }

    [Theory]
    [InlineData(3, 7u)]
    [InlineData(4, 0u)]
    public void TransformAndCancelOnlyChangeExactLevelFiftyAndDoNotReadMetadata(int operation, uint expected)
    {
        var type = (Opt.Types)operation;
        using var s = new Scope();
        var weapon = s.Weapon(50, 23, id: 202);
        weapon.Transform = 31;
        Table.Info = null!;
        var info = Request(type);
        var frames = Opt.Process(info, s.User, 17, 7000); // Not restricted to caller's class.
        Assert.Equal(2, frames.Count);
        Updated(frames[0], 50, exp: 23, transform: expected, id: 202);
        Operation(frames[1], type);
        Assert.Equal(expected, weapon.Transform);
        Assert.Equal(7u, info.Transform); // CANCEL does not clear the input scalar.
        Assert.Equal(1u, info.UID);
        Assert.Same(weapon, Assert.Single(s.User.Items).Value);
    }

    [Fact]
    public void TransformAndCancelDenyOtherLevelsWithoutTouchingRequestOrWeapon()
    {
        using var s = new Scope();
        Table.Info = null!;
        foreach (var type in new[] { Opt.Types.TRANSFORM, Opt.Types.CANCEL })
        foreach (uint level in new uint[] { 0, 49, 51, uint.MaxValue })
        {
            var weapon = s.Weapon(level, 23);
            weapon.Transform = 31;
            var info = Request(type);
            Assert.Empty(Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(31u, weapon.Transform);
            Assert.Equal(level, weapon.Level);
            Assert.Equal(77u, info.UID);
        }
    }

    [Fact]
    public void TransformBuilderFaultLeavesTransformAndUidMutated()
    {
        using var s = new Scope();
        var weapon = s.Weapon(50);
        weapon.BlackJades[5] = null!;
        var info = Request(Opt.Types.TRANSFORM);
        Assert.Throws<NullReferenceException>(() => Opt.Process(info, s.User, 17, 2000));
        Assert.Equal(7u, weapon.Transform);
        Assert.Equal(1u, info.UID);
        Assert.Null(weapon.BlackJades[5]);
    }

    [Fact]
    public void AllMutationBranchesReturnEarlyForMissingRequestedKeyWithoutReadingNullMetadata()
    {
        using var s = new Scope();
        Table.Info = null!;
        foreach (var type in new[] { Opt.Types.TRANSFORM, Opt.Types.CANCEL, Opt.Types.UPGRADE_TAOIST,
            Opt.Types.MASTERY_SCORE_TAOIST, Opt.Types.UPGRADE_TAOIST_ALL, Opt.Types.UPGRADE_PIRATE,
            Opt.Types.MASTERY_SCORE_PIRATE })
        {
            var info = Request(type);
            Assert.Empty(Opt.Process(info, s.User, 17, 2000));
            Assert.Equal(77u, info.UID);
            Assert.Equal(type, info.Type);
            Assert.Equal(13u, info.Exp);
            Assert.Equal(7u, info.Transform);
        }
        Assert.Empty(s.User.Items);
    }
}
