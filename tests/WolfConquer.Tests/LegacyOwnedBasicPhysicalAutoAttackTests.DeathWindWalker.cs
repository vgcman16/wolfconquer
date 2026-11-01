using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task RealLethalReceiveKeepsTheActualFanRegistrationAndItsOriginalDeadline() => RunFanDeath(async f =>
    {
        var vector = f.Binding(Target).Player.BitVector;
        Assert.True(vector.ArrayFlags.TryGetValue(194, out var original));
        Assert.NotNull(original); Assert.False(original.RemoveOnDead);
        var timer = original.Timer; var deadline = timer.AddSeconds(original.Secounds);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 194));
        var before = f.Files(); var after = HpImage(f, before, 200, 0);
        f.BeforeSend = (recipientUid, bytes) =>
        {
            if (!IsPhysical(bytes) && !IsHpPair(bytes, Target) && !IsDeathVector(bytes, out _)) return;
            f.AssertFiles(after); f.AssertCoherent(0);
            Assert.Same(original, vector.ArrayFlags[194]);
            Assert.Equal(timer, original.Timer); Assert.Equal(deadline, original.Timer.AddSeconds(original.Secounds));
            Assert.True((vector.bits[6] & 4) != 0);
        };
        try { await f.AttackAsync(); }
        finally { f.BeforeSend = null; }
        f.AssertFiles(after); f.AssertCoherent(0);
        Assert.Same(original, vector.ArrayFlags[194]);
        Assert.Equal(timer, original.Timer); Assert.Equal(deadline, original.Timer.AddSeconds(original.Secounds));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 194));
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        foreach (uint recipient in new[] { Actor, Target })
        {
            var outputs = f.Order.Where(row => row.Recipient == recipient && IsDeathVector(row.Bytes, out _)).ToArray();
            Assert.Equal(2, outputs.Length);
            Assert.Equal(DeathVectorGolden(false), outputs[0].Bytes);
            Assert.Equal(DeathVectorGolden(true), outputs[1].Bytes);
        }
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Owner.Store, f.Owner.Connection(Target), Target, out var fields,
            f.Binding(Target).NativeIdentity));
        Assert.Equal(626_003U, fields.RightWeaponItemId); Assert.Equal(626_009U, fields.LeftWeaponItemId);
        Assert.False(f.Binding().Client.OnAutoAttack);
        int count = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        Assert.Equal(count, f.Order.Count); f.AssertFiles(after); f.AssertCoherent(0);
    });

    private static Task RunFanDeath(Func<AttackFixture, Task> body)
    {
        // Actual retained PrideFan rows, matching the already qualified real
        // WindWalker query fixture. Their fields are not invented test stats.
        string[] raw = [
            "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
            "626009@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@14@@9@@0@@63@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
        ];
        var rows = raw.Select(line => line.Split("@@", StringSplitOptions.None))
            .Select(fields => new ItemType(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1], fields)).ToArray();
        var target = Saved(Target, "FanTarget", 831, 200, 0, false) with { Class = 16_051 };
        var items = new PlayerItemSnapshot([], [], [],
            [DeathFanItem(1_083_100, 626_003, 4), DeathFanItem(1_083_101, 626_009, 5)], []);
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(
            new(Saved(Actor, "FanKiller", 830, 5000, 10_000, false), PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty),
            NativeCallerWorldDataFixture.Create(rows), options, async owner =>
            {
                var fixture = new AttackFixture(owner, clock);
                fixture.AssertCoherent(200); fixture.Clear(); await body(fixture);
            }, new OwnedEquipmentBehaviorInput(target, items, PlayerSpellSnapshot.Empty));
    }

    private static PlayerItemSnapshotItem DeathFanItem(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 10, MaximumDurability = 10, StackSize = 0, Color = 5 };

    private static bool IsDeathVector(byte[] bytes, out IReadOnlyList<ulong> words)
    {
        words = [];
        if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
            packet.Uid != Target || packet.Updates.Count != 1 || packet.Updates[0].Type != LegacyMsgUpdateDataType.StatusFlag ||
            packet.Updates[0].Values is not { } values) return false;
        words = values; return true;
    }

    private static byte[] DeathVectorGolden(bool dead)
    {
        var words = new ulong[18]; words[6] = 4;
        if (dead) words[0] = 1UL << LegacyPlayerDeathState.DeadStatusFlag;
        var update = new List<byte> { 8, 25 };
        foreach (ulong word in words) { update.Add(24); Varint(update, word); }
        var body = new List<byte> { 8 }; Varint(body, Target); body.Add(18);
        Varint(body, checked((ulong)update.Count)); body.AddRange(update);
        return Frame(2176, body);
    }
}
