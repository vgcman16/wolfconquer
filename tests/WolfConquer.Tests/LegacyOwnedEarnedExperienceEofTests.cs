using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedEarnedExperienceEofTests
{
    [Fact]
    public Task ActualEofDuringFirstEarnedQueryReconnectsWithoutRecredit()
    {
        var family = new Monster(910, "EarnedEof", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "138",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        static LevelExperience Threshold(int level) => new(0, level, 1, 0, 0, 1,
            $"0 {level} 1 0 0", new[] { "0", level.ToString(System.Globalization.CultureInfo.InvariantCulture), "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family },
            levelExperience: new[] { Threshold(138), Threshold(139) });
        OwnedEquipmentBehaviorInput Player(uint uid, string name) => new(
            OwnedEquipmentBehaviorFixture.Character(uid, name, classCode: 1000, level: 138, reborn: 1,
                strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
            {
                MinMana = 0, MaxMana = 0,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
            }, PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(Player(OwnedEquipmentBehaviorFixture.Actor, "EofActor"), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://earned-eof/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var before = f.Current(); var peer = f.Current(OwnedEquipmentBehaviorFixture.Peer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var peerBinding = Assert.IsType<LegacyOrdinarySpellBinding>(peer.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files(); var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            Dictionary<string, byte[]>? atEof = null; Task? close = null; f.ActorFrames.Clear();
            f.Frames(OwnedEquipmentBehaviorFixture.Peer).Clear();
            f.BeforeSend = (_, bytes) =>
            {
                if (binding.Player.Level != 139 || close is not null ||
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
                atEof = f.Data.Files();
                close = f.Connection(OwnedEquipmentBehaviorFixture.Actor).CompleteTransportCloseAsync();
                Assert.False(close.IsCompleted); // Actual cleanup waits for this in-flight exclusive Frame.
            };
            try
            {
                var hit = new MsgAttackPacket(new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, monster.Uid,
                    monster.X, monster.Y, MsgAttackType.Physical, 0, 0, 0, MsgAttackEffect.None, 0));
                await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, hit.ToFrameBytes());
            }
            finally { f.BeforeSend = null; }
            Assert.NotNull(close); await close; Assert.NotNull(atEof);
            f.Data.AssertFiles(atEof);
            Assert.Equal(files.Keys.OrderBy(x => x), atEof.Keys.OrderBy(x => x));
            foreach (var row in files.Where(row => !string.Equals(row.Key, characterKey, StringComparison.OrdinalIgnoreCase)))
                Assert.Equal(row.Value, atEof[row.Key]);
            var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(atEof[characterKey], before.AccountId,
                checked((uint)before.Character.Uid));
            Assert.NotNull(pending); Assert.Equal((ushort)139, pending.CurrentLevel);
            Assert.Equal((ushort)1, pending.StepIndex); Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
            var attacks = f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                .Select(frame => MsgAttackPacket.TryParse(frame, out var packet) ? packet : (MsgAttackPacket?)null)
                .Where(packet => packet is { } value && value.Query.AttackType == MsgAttackType.Physical && value.Query.OpponentUid == monster.Uid)
                .Select(packet => packet!.Value).ToArray();
            var attack = Assert.Single(attacks); Assert.True(attack.Query.Damage > 0);
            Assert.Equal(checked((ulong)Math.Max(10, attack.Query.Damage)), pending.CreditedExperience);
            Assert.Equal(pending.InitialExperience + pending.CreditedExperience,
                pending.RemainingExperience + pending.ConsumedExperience + pending.CappedExperience);
            Assert.False(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.Equal((ushort)139, binding.Player.Level); Assert.Equal(pending.RemainingExperience, binding.Player.Experience);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.False(binding.Client.OnAutoAttack); Assert.Null(binding.Client.AutoAttack);
            Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer)); Assert.False(peerBinding.IsRetired);
            Assert.Empty(f.Frames(OwnedEquipmentBehaviorFixture.Peer));
            Assert.DoesNotContain(f.ActorFrames, row => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
            int outputs = f.ActorFrames.Count;
            await f.Handler.SendDueBasicPhysicalAttacksAsync(CancellationToken.None);
            Assert.Equal(outputs, f.ActorFrames.Count); f.Data.AssertFiles(atEof);
            Assert.False(binding.Client.OnAutoAttack);

            var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var text = Encoding.UTF8.GetString(expected[characterKey]);
            static string Replace(string value, string from, string to)
            {
                string needle = "\r\n" + from + "\r\n";
                Assert.Contains(needle, value);
                return value.Replace(needle, "\r\n" + to + "\r\n", StringComparison.Ordinal);
            }
            text = Replace(text, "Level=138", "Level=140");
            text = Replace(text, "Atributes=12", "Atributes=18");
            text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
            expected[characterKey] = Encoding.UTF8.GetBytes(text);
            await f.ReconnectContinuationAfterAwaitedCloseAsync();
            var resumed = f.Current();
            var resumedBinding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
            Assert.NotSame(binding, resumedBinding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(resumedBinding));
            Assert.Equal(140, resumed.Character.Level); Assert.Equal(0, resumed.Character.Experience);
            Assert.Equal(18, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
            Assert.Equal((ushort)140, resumedBinding.Player.Level); Assert.Equal(0UL, resumedBinding.Player.Experience);
            Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
                resumed.AccountId, checked((uint)resumed.Character.Uid)));
            f.Data.AssertFiles(expected); Assert.Same(peer, f.Current(OwnedEquipmentBehaviorFixture.Peer));
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(peerBinding));
            Assert.DoesNotContain(f.ActorFrames, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical);
            Assert.Equal(new LegacyMsgUpdatePacket(OwnedEquipmentBehaviorFixture.Actor,
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Experience, 0)]).ToFrameBytes(), f.ActorFrames[^1]);
        }, Player(OwnedEquipmentBehaviorFixture.Peer, "EofPeer"));
    }
}
