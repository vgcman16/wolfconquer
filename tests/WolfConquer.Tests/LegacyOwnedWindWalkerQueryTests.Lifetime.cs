using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Fact]
    public Task QueryMintedFanSurvivesActualOrdinaryDeathPublicationAndTheConfiguredDelayedFrameRevive() => Run(async f =>
    {
        AssertJoined(f, true); var row = f.Row!; var timer = row.Timer; var deadline = timer.AddSeconds(row.Secounds);
        var files = f.Owner.Data.Files(); var character = File.ReadAllBytes(f.Owner.CharacterPath(Uid));
        var itemImage = File.ReadAllBytes(f.Owner.ItemPath(Uid)); int first = f.Sent.Count;
        var deathAt = f.Clock.GetUtcNow();
        // The existing ordinary-death PUBLICATION phase runs against the real
        // admitted Frame/Store/native receiver. No offensive spell, successful
        // combat kill or kill authorization is claimed by this direct phase call.
        await ExecuteActualWindDeathPublication(f);
        Assert.Equal(0L, f.Current.Character.MinHitPoints); Assert.Equal(0, f.Native.Player!.HitPoints);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(deathAt, DateTimeOffset.Parse(f.Current.Character.Attributes[LegacyPlayerDeathState.DeadStampAttribute],
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        AssertWindLifetimeRow(f, row, timer, deadline);
        var deathOutput = f.Sent.Skip(first).ToArray();
        var deathVectors = deathOutput.Where(bytes => IsVector(bytes, out _)).ToArray(); Assert.Equal(2, deathVectors.Length);
        Assert.Equal(VectorGolden(Words(true)), deathVectors[0]);
        var deadWords = Words(true); deadWords[0] = 1UL << LegacyPlayerDeathState.DeadStatusFlag;
        Assert.Equal(VectorGolden(deadWords), deathVectors[1]);
        var status = Assert.Single(deathOutput.Select(bytes => LegacyPlayerStatusPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var parsed) ? parsed : null).OfType<LegacyPlayerStatusPacket>());
        Assert.True(status.MaxHP > 0);
        AssertWindLifetimeFiles(f, files, ReplaceWindLifetimeHp(character, 1, 0));
        Assert.Equal(itemImage, File.ReadAllBytes(f.Owner.ItemPath(Uid)));

        // Both eligibility checks use the real Frame's configured clock. A
        // wall-clock coincidence cannot make this boundary pass.
        f.Clock.Advance(LegacyPlayerDeathState.DefaultReviveDelay - TimeSpan.FromTicks(1));
        var deadFiles = f.Owner.Data.Files(); first = f.Sent.Count; int peerFirst = f.Owner.Frames(Peer).Count;
        var revive = new MsgActionPacket { ObjId = Uid, Timestamp = 511_043_495, Type = MsgActionType.Revive }.ToFrameBytes();
        await f.Owner.SendAsync(Uid, revive);
        Assert.Equal(first, f.Sent.Count); Assert.Equal(peerFirst, f.Owner.Frames(Peer).Count); f.Owner.Data.AssertFiles(deadFiles);
        Assert.Equal(0L, f.Current.Character.MinHitPoints); AssertWindLifetimeRow(f, row, timer, deadline);
        f.Clock.Advance(TimeSpan.FromTicks(1)); await f.Owner.SendAsync(Uid, revive);
        // The established source policy is strictly AFTER allowedAt, not >=.
        Assert.Equal(first, f.Sent.Count); Assert.Equal(peerFirst, f.Owner.Frames(Peer).Count); f.Owner.Data.AssertFiles(deadFiles);
        Assert.Equal(0L, f.Current.Character.MinHitPoints); AssertWindLifetimeRow(f, row, timer, deadline);
        f.Clock.Advance(TimeSpan.FromTicks(1)); await f.Owner.SendAsync(Uid, revive);

        Assert.Equal(first + 6, f.Sent.Count); var output = f.Sent.Skip(first).ToArray();
        AssertWindLifetimeHpPair(output, status.MaxHP);
        Assert.True(LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output[2]), out var stamina));
        Assert.Equal(LegacyMsgUpdateDataType.Stamina, Assert.Single(stamina.Updates).Type);
        Assert.Equal(100UL, stamina.Updates[0].Value); Assert.Equal(VectorGolden(Words(true)), output[3]);
        Assert.True(LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output[4]), out var mesh));
        Assert.Equal(LegacyMsgUpdateDataType.Mesh, Assert.Single(mesh.Updates).Type);
        Assert.True(LegacyMapStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output[5]), out _));
        // The viewer spawn follows the canonical Store publication, not the
        // pre-projection revive DTO whose status aliases were cleared.
        var peerFrame = Assert.Single(f.Owner.Frames(Peer).Skip(peerFirst));
        Assert.True(LegacyPlayerSpawnPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(peerFrame), out var spawn));
        Assert.Equal((ulong)Uid, spawn.GetNumericField(2));
        var spawnWords = new ulong[10]; spawnWords[6] = 4; Assert.Equal(spawnWords, spawn.StatusFlags);
        Assert.Equal((long)status.MaxHP, f.Current.Character.MinHitPoints);
        Assert.Equal((long)status.MaxHP, (long)f.Native.Player.HitPoints);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, LegacyPlayerDeathState.GhostStatusFlag));
        AssertWindLifetimeRow(f, row, timer, deadline);
        AssertWindLifetimeFiles(f, files, ReplaceWindLifetimeHp(character, 1, status.MaxHP));
        Assert.Contains("WorldServer handled MsgAction revive", f.Owner.Log.ToString());
        foreach (uint recipient in new[] { Uid, Peer })
            Assert.All(f.Order.Where(entry => entry.Recipient == recipient && IsVector(entry.Bytes, out _)),
                entry => { Assert.True(IsVector(entry.Bytes, out var words)); Assert.Equal(4UL, words[6] & 4); });
    }, mode: "pair", peer: true);
}
