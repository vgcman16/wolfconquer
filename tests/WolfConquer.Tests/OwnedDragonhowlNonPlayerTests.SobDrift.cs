using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlNonPlayerTests
{
    [Fact]
    public Task ARealSobResetBetweenCompletedCastsCanSupplyTheSameBirthRepeatSuccessor() => Run(true, async f =>
    {
        await f.Attack();
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.True(f.Binding.Client.OnAutoAttack);
        f.Sobs.ResetHitpointsToMax(f.Input.Sob!);
        Assert.Equal(InitialHp, f.Hp());
        var firstStamp = f.Binding.Player.AttackStamp;
        var start = f.Owner.ActorFrames.Count;
        await f.Repeat();
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.NotEqual(firstStamp, f.Binding.Player.AttackStamp);
        AssertOneOutput(f, start, sob: true);
        Assert.True(f.Binding.Client.OnAutoAttack);
        Assert.False(f.Owner.Recovery.IsBlocked);
    });

    [Fact]
    public Task SameUidCatalogBirthReplacementCannotBeAdoptedByAnEarlierSobRepeat() => Run(true, async f =>
    {
        await f.Attack();
        Assert.Equal(InitialHp - Damage, f.Hp());
        var actor = f.Current; var binding = f.Binding;
        var stamp = binding.Player.AttackStamp;
        var rows = Assert.IsType<SobNpc[]>(f.Input.World.SobNpcs);
        var original = Assert.Single(rows);
        try
        {
            rows[0] = original with { }; // Same values/UID, different adopted catalog birth.
            Assert.NotSame(original, rows[0]);
            var outputCount = f.Owner.ActorFrames.Count;
            var files = f.Owner.Data.Files();
            await f.Repeat();
            Assert.Equal(InitialHp - Damage, f.Hp());
            Assert.Same(actor, f.Current);
            Assert.Equal(stamp, binding.Player.AttackStamp);
            Assert.False(binding.Client.OnAutoAttack);
            Assert.Equal(outputCount, f.Owner.ActorFrames.Count);
            Assert.False(f.Owner.Recovery.IsBlocked); f.Owner.Data.AssertFiles(files);
        }
        finally { rows[0] = original; }
    });

    [Fact]
    public Task SameValueSobResetAfterActualCostOutputInvalidatesTheCapturedPhaseAndFences() => Run(true, async f =>
    {
        var binding = f.Binding;
        var reset = false;
        Dictionary<string, byte[]>? filesAtCost = null;
        f.Owner.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Actor || reset || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
                packet.Uid != Actor || !packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Mana)) return;
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.Equal(499, f.Current.Character.MinMana);
            Assert.Equal(499, (int)binding.Player.Mana);
            Assert.Equal(InitialHp, f.Hp());
            filesAtCost = f.Owner.Data.Files();
            // Real registered target API advances its mutation version even
            // though resetting a full-HP SOB leaves every visible value equal.
            f.Sobs.ResetHitpointsToMax(f.Input.Sob!);
            Assert.Equal(InitialHp, f.Hp()); reset = true;
        };
        await f.Attack();
        f.Owner.BeforeSend = null;
        Assert.True(reset, f.Owner.Log.ToString());
        Assert.Equal(InitialHp, f.Hp());
        Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.True(binding.IsRetired);
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        Assert.Equal(499, (int)binding.Player.Mana);
        Assert.False(binding.Client.OnAutoAttack);
        Assert.DoesNotContain(f.Owner.ActorFrames, bytes => LegacySpellAnimationPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == 17_490);
        f.Owner.Data.AssertFiles(Assert.IsType<Dictionary<string, byte[]>>(filesAtCost));
    }, manaCost: 1);
}
