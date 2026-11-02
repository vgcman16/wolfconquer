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
    public Task OwnedSobBirthCannotBeReplacedThroughPublishedMembershipAndRepeatUsesRealTarget() => Run(true, async f =>
    {
        await f.Attack();
        Assert.Equal(InitialHp - Damage, f.Hp());
        var catalog = f.Input.World;
        var original = Assert.Single(catalog.SobNpcs);
        Assert.Same(f.Input.Sob, original);
        Assert.False(catalog.SobNpcs is IList<SobNpc>);
        Assert.False(catalog.SobNpcsById[original.Id] is IList<SobNpc>);
        var index = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<SobNpc>>>(catalog.SobNpcsById);
        Assert.Throws<NotSupportedException>(() => index[original.Id] = new[] { original with { } });
        Assert.Same(original, catalog.SobNpcsById[original.Id][0]);
        var firstStamp = f.Binding.Player.AttackStamp;
        var start = f.Owner.ActorFrames.Count;
        await f.Repeat();
        Assert.Equal(InitialHp - 2 * Damage, f.Hp());
        Assert.NotEqual(firstStamp, f.Binding.Player.AttackStamp);
        AssertOneOutput(f, start, sob: true);
        Assert.False(f.Owner.Recovery.IsBlocked);
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
