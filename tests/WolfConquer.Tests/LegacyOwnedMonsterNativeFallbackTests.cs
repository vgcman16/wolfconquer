using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterNativeFallbackTests
{
    [Theory]
    [InlineData("missing-session-client")]
    [InlineData("foreign-session-client")]
    [InlineData("missing-store")]
    [InlineData("retired-connection")]
    public Task ActualAttachedConnectionCannotFallBackToUnboundMonsterDamage(string invalid)
    {
        var family = new Monster(910, "NativeFallback", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family });
        OwnedEquipmentBehaviorInput Player(uint uid, string name) => new(
            OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 1000, level: 139, reborn: 1,
                strength: 100, agility: 0, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
            {
                MinMana = 0, MaxMana = 0,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100" },
            }, PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(Player(OwnedEquipmentBehaviorFixture.Actor, "FallbackActor"), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://native-fallback/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
            var original = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(original.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(binding.Player.HasOwnedVitalsAttachment);
            Assert.True(binding.Player.HasOwnedActionAttachment);
            var peer = f.Current(OwnedEquipmentBehaviorFixture.Peer);
            var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            var context = f.Handler.CreateMonsterRuntimeContext(new(connection, original));
            if (invalid == "missing-session-client") context = context with { Session = original with { NativeGameClient = null } };
            else if (invalid == "foreign-session-client") context = context with { Session = original with { NativeGameClient = peer.NativeGameClient } };
            else if (invalid == "missing-store") context = context with { Sessions = null };
            else if (invalid == "retired-connection")
            {
                Assert.True(f.Store.TryRemove(connection, out var removed)); Assert.Same(original, removed);
                Assert.True(binding.IsRetired); Assert.Same(binding, connection.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(binding.Player.HasOwnedVitalsAttachment);
                Assert.True(binding.Player.HasOwnedActionAttachment);
                context = context with { Session = original with { NativeGameClient = null } };
            }
            else throw new ArgumentOutOfRangeException(nameof(invalid));
            var files = f.Data.Files(); var stamp = binding.Player.AttackStamp;
            f.ActorFrames.Clear(); f.Frames(OwnedEquipmentBehaviorFixture.Peer).Clear();
            var request = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes();
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            await registry.HandleAsync(context, OwnedEquipmentBehaviorFixture.ReadFrame(request));
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visible));
            Assert.Equal(monster.Hitpoints, visible.Hitpoints);
            Assert.Equal(stamp, binding.Player.AttackStamp); Assert.False(binding.Client.OnAutoAttack);
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(OwnedEquipmentBehaviorFixture.Peer)); f.Data.AssertFiles(files);
            Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
            if (invalid == "retired-connection") Assert.False(f.Store.TryGet(connection, out _));
            else { Assert.Same(original, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); }
        }, Player(OwnedEquipmentBehaviorFixture.Peer, "FallbackPeer"));
    }
}
