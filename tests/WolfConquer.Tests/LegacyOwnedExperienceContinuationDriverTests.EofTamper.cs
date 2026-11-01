using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualEofWithChangedEarnedCheckpointFencesDomainAndKeepsPeer(bool corruptMarker) => RunTamperedEof(async (f, monster) =>
    {
        var actor = f.Current();
        var actorBinding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
        var peer = f.Current(OwnedEquipmentBehaviorFixture.Peer);
        var peerNative = peer.NativeGameClient;
        var uid = checked((uint)actor.Character.Uid);
        byte[]? earnedImage = null;
        Dictionary<string, byte[]>? expected = null;
        Task? close = null;
        int observed = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != OwnedEquipmentBehaviorFixture.Actor || actorBinding.Player.Level != 138 ||
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            observed++;
            earnedImage = File.ReadAllBytes(f.CharacterPath());
            var original = Encoding.UTF8.GetString(earnedImage);
            const string marker = "ExperienceContinuation=";
            if (!original.Contains(marker, StringComparison.Ordinal)) throw new InvalidDataException("earnedMarkerMissingAtActualSend");
            var changed = Encoding.UTF8.GetBytes(corruptMarker
                ? original.Replace(marker, marker + "x", StringComparison.Ordinal)
                : original + "; externally changed after earned checkpoint\r\n");
            File.WriteAllBytes(f.CharacterPath(), changed);
            expected = f.Data.Files();
            // Publish the real transport close without awaiting its cleanup
            // while the actual frame still owns the operation lease.
            close = f.Connection(OwnedEquipmentBehaviorFixture.Actor).CompleteTransportCloseAsync();
            throw new IOException("owned transport ended after checkpoint image changed");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, observed); Assert.NotNull(earnedImage); Assert.NotNull(expected); Assert.NotNull(close);
        await close;
        var earned = LegacyExperienceContinuationCodec.ReadCharacterImage(earnedImage, actor.AccountId, uid);
        Assert.NotNull(earned); Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, earned.Phase);
        Assert.Equal((ushort)1, earned.StepIndex); Assert.True(earned.CreditedExperience >= 2);
        AssertEarnedFromHit(f.ActorFrames, monster, earned);
        Assert.True(f.Recovery.IsBlocked);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(actorBinding.IsRetired);
        Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer));
        Assert.Same(peerNative, f.Connection(OwnedEquipmentBehaviorFixture.Peer).NativeGameClient);
        f.Data.AssertFiles(expected);
        Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString(), StringComparison.Ordinal);
    });

    private static Task RunTamperedEof(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, Task> body)
    {
        var family = new Monster(910, "OwnedContinuationEof", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "137",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var rows = new[]
        {
            new LevelExperience(0, 137, 1, 0, 0, 1, "0 137 1 0 0", new[] { "0", "137", "1", "0", "0" }),
            new LevelExperience(0, 138, 1, 0, 0, 1, "0 138 1 0 0", new[] { "0", "138", "1", "0", "0" }),
        };
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: rows);
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 137, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        var peer = OwnedEquipmentBehaviorFixture.Character(uid: OwnedEquipmentBehaviorFixture.Peer, name: "Peer",
            classCode: 1000, level: 137, reborn: 1, mapId: 1011, x: 432, y: 380, minHitPoints: 5000);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-continuation-eof/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            await body(f, monster);
        }, new OwnedEquipmentBehaviorInput(peer, PlayerItemSnapshot.Empty));
    }
}
