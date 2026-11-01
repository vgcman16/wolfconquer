using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedPeriodicCombatExperienceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealManiacDueFrameUsesLiveStatusAndRequiresSavedWallet(bool corruptSavedWallet)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = fields[1] = "12700"; fields[2] = "83"; fields[3] = "ManiacDance";
        fields[10] = "30100"; fields[13] = "1"; fields[14] = "1"; fields[15] = "5";
        fields[29] = "9"; fields[33] = "1000";
        var spell = new MagicType(12700, 12700, "ManiacDance", fields);
        var family = new Monster(910, "PeriodicExperience", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
            ["defense2"] = "10000",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        // Use actual saved weapon metadata and a complete family multiplier.
        // Physical spell damage correctly returns zero when defense2 is absent;
        // that malformed family cannot exercise a successful due-tick XP award.
        var sword = OwnedEquipmentBehaviorFixture.Item(410_439, fields: new Dictionary<int, string>
            { [13] = "200", [14] = "200" });
        var items = new PlayerItemSnapshot([], [], [], new PlayerItemSnapshotItem[]
            { new() { Uid = 99_101, ItemId = 410_439, Position = 4, Durability = 100, MaximumDurability = 100, StackSize = 1 } }, []);
        var catalog = LegacyItemWorldDataFixture.Create(new[] { sword }, [], monsters: new[] { family },
            magic: new[] { spell }, levelExperience: new[] { threshold });
        var flags = new ulong[16]; flags[0] = 1UL << LegacyRuntimeUpdateTicker.XpListFlag;
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1",
              ["StatusFlags"] = string.Join('/', flags), ["XPCount"] = "100" },
        };
        var learned = new PlayerSpellSnapshot([new PlayerSpellSnapshotEntry(12700, 0, 0, 0, 0, 0)]);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, items, learned), catalog, async f =>
        {
            var expectedFiles = f.Data.Files();
            string characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://periodic-owned/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
                Uid: OwnedEquipmentBehaviorFixture.Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y,
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
                Effect: MsgAttackEffect.None, Unknown: 0, SpellId: 12700)).ToFrameBytes());
            Assert.True(f.Current().Character.Attributes.TryGetValue(WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute, out var stamp));
            var due = DateTimeOffset.Parse(stamp!, CultureInfo.InvariantCulture).AddMilliseconds(1);
            // The cast opens the real timed skill; this later Frame phase,
            // rather than the initial cast, must cause the level transition.
            Assert.Equal(139L, f.Current().Character.Level);
            Assert.Equal(0L, f.Current().Character.Experience);
            Assert.Equal("91", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal((ushort)91, binding.Player.Stamina);
            f.Data.AssertFiles(expectedFiles);
            string expectedText = Encoding.UTF8.GetString(expectedFiles[characterKey]);
            if (corruptSavedWallet)
            {
                expectedText = ReplaceLine(expectedText, "ConquerPoints=639778904", "ConquerPoints=639778905");
                expectedFiles[characterKey] = Encoding.UTF8.GetBytes(expectedText);
                File.WriteAllBytes(f.CharacterPath(), expectedFiles[characterKey]);
            }
            Assert.True(states.TryGetVisibleInstance(monster, due, out var beforeTick));
            Assert.Equal(monster.Hitpoints, beforeTick.Hitpoints);
            f.ActorFrames.Clear();
            var entry = typeof(WorldServerFrameHandler).GetMethod("SendDueLegacyManiacDanceAttacksAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await Assert.IsType<ValueTask>(entry.Invoke(f.Handler, [due, CancellationToken.None]));
            var after = f.Current();
            bool visible = states.TryGetVisibleInstance(monster, due, out var damaged);
            after.Character.Attributes.TryGetValue(WorldPacketHandlers.RuntimeLegacyManiacDanceNextAttackAtUtcAttribute, out var afterStamp);
            var observedDisk = Assert.IsType<CharacterSnapshot>(new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(after.AccountId));
            var differingKeys = observedDisk.Attributes.Where(pair =>
                !after.Character.Attributes.TryGetValue(pair.Key, out var value) || value != pair.Value).Select(pair => pair.Key);
            if (corruptSavedWallet)
            {
                Assert.Equal(139L, after.Character.Level); Assert.Equal(0L, after.Character.Experience);
                Assert.Equal(639778904L, after.Character.ConquerPoints);
                Assert.Equal(639778904U, binding.Player.ConquerPoints);
                Assert.Equal((ushort)139, binding.Player.Level); Assert.Equal(5000, binding.Player.HitPoints);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.True(visible); Assert.NotNull(damaged); Assert.True(damaged.Hitpoints < beforeTick.Hitpoints);
                Assert.Contains("WorldServer refused combat experience continuation reason=experienceCreditCharacterImageInvalid.", f.Log.ToString());
                Assert.DoesNotContain(f.ActorFrames, bytes => LegacyMsgUpdatePacket.TryParse(
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                    update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Experience));
                f.Data.AssertFiles(expectedFiles);
                return;
            }
            Assert.True(after.Character.Level == 140,
                $"Due ManiacDance did not level: level={after.Character.Level}, xp={after.Character.Experience}, " +
                $"nativeLevel={binding.Player.Level}, due={due:O}, beforeStamp={stamp}, afterStamp={afterStamp}, " +
                $"monsterVisible={visible}, beforeHP={beforeTick.Hitpoints}, afterHP={damaged?.Hitpoints}, " +
                $"frames={f.ActorFrames.Count}, ownerCurrent={f.Store.IsBoundSpellOwnerCurrent(binding)}, " +
                $"savedScalarMismatch={CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(observedDisk, after.Character)}, " +
                $"savedAttributeDifferences={string.Join(',', differingKeys)}; log:\n{f.Log}");
            Assert.Equal((ushort)140, binding.Player.Level);
            Assert.Equal(15L, after.Character.AttributePoints);
            Assert.True(visible); Assert.NotNull(damaged);
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(after, out var max));
            Assert.Equal(11136U, max); Assert.Equal(11136L, after.Character.MinHitPoints);
            Assert.Equal(11136, binding.Player.HitPoints);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var disk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(after.AccountId);
            Assert.NotNull(disk); Assert.Equal(140L, disk.Level); Assert.Equal(15L, disk.AttributePoints);
            Assert.Equal(11136L, disk.MinHitPoints);
            expectedText = ReplaceLine(expectedText, "Level=139", "Level=140");
            expectedText = ReplaceLine(expectedText, "Atributes=12", "Atributes=15");
            expectedText = ReplaceLine(expectedText, "MinHitPoints=5000", "MinHitPoints=11136");
            expectedFiles[characterKey] = Encoding.UTF8.GetBytes(expectedText);
            f.Data.AssertFiles(expectedFiles);
            Assert.Contains(f.ActorFrames, bytes => LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var status) && status.MaxHP == 11136U);
            Assert.Contains(f.ActorFrames, bytes => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                update.Uid == OwnedEquipmentBehaviorFixture.Actor && update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Experience && row.Value == 0));
        });
    }

    private static string ReplaceLine(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n";
        Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }

    [Fact]
    public Task PeriodicContextRejectsSharedDisposedAndForeignOperationsBeforeEffects() =>
        OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(), PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), async f =>
            {
                var session = f.Current();
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
                var active = new WorldActiveSession(f.Connection(OwnedEquipmentBehaviorFixture.Actor), session);
                var barrier = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
                    .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
                var method = typeof(WorldServerFrameHandler).GetMethod("CreateMonsterRuntimeOperationContext",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                var files = f.Data.Files();
                var frames = f.ActorFrames.Select(frame => frame.ToArray()).ToArray();
                var disposed = await barrier.EnterExclusiveAsync();
                disposed.Dispose();
                using var shared = await barrier.EnterSharedAsync();
                var foreign = new WorldOperationBarrier();
                using var foreignExclusive = await foreign.EnterExclusiveAsync();
                foreach (var lease in new[] { shared, foreignExclusive, disposed })
                {
                    var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(f.Handler, [active, lease]));
                    Assert.IsType<InvalidOperationException>(error.InnerException);
                    Assert.Equal("periodicCombatOperationRequiresExclusiveLease", error.InnerException!.Message);
                    Assert.Same(session, f.Current());
                    Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                    Assert.Equal(frames, f.ActorFrames);
                    f.Data.AssertFiles(files);
                }
            });
}
