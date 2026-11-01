using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedCombatExperienceTests
{
    [Fact]
    public Task ActualOutputFailureAfterCommittedLevelFencesTheHolder() =>
        RunHitAsync(async (f, monster, family, states) =>
        {
            var before = f.Current();
            var native = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding).Player;
            f.BeforeSend = (_, _) =>
            {
                if (native.Level == 140) throw new IOException("owned XP output interruption");
            };
            try { await Assert.ThrowsAnyAsync<Exception>(() => HitAsync(f, monster)); }
            finally { f.BeforeSend = null; }
            Assert.True(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            var disk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(before.AccountId);
            Assert.NotNull(disk);
            Assert.Equal(140, disk.Level);
            Assert.Equal(0, disk.Experience);
            Assert.Equal(15, disk.AttributePoints);
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
        });

    [Fact]
    public Task ActualCharacterFileLeaseRefusesExperienceWithoutUndoingTheMonsterHit() =>
        RunHitAsync(async (f, monster, family, states) =>
        {
            var before = f.Current();
            var beforeFile = File.ReadAllBytes(f.CharacterPath());
            var allFiles = f.Data.Files();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            f.ActorFrames.Clear();
            using (new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
                await HitAsync(f, monster);
            Assert.Same(before, f.Current());
            Assert.Equal(beforeFile, File.ReadAllBytes(f.CharacterPath()));
            f.Data.AssertFiles(allFiles);
            Assert.Equal((ushort)139, binding.Player.Level);
            Assert.Equal(0UL, binding.Player.Experience);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString());
            Assert.Contains("WorldServer refused combat experience reason=combatExperienceCharacterReplace:capturedImageUnchanged:", f.Log.ToString());
        });

    [Fact]
    public Task ActualMonsterHitLevelsAdmittedPlayerAndPersistsItsNativeExperienceAndStats() =>
        RunHitAsync(async (f, monster, family, states) =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal)
                { ["Other"] = "one", ["other"] = "two" };
            f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before with
                { Character = before.Character with { Attributes = fields } });
            var files = f.Data.Files();
            var characterPath = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var expectedText = Encoding.UTF8.GetString(files[characterPath]);
            expectedText = ReplaceLine(expectedText, "Level=139", "Level=140");
            expectedText = ReplaceLine(expectedText, "Atributes=12", "Atributes=15");
            expectedText = ReplaceLine(expectedText, "MinHitPoints=5000", "MinHitPoints=11136");
            expectedText = ReplaceLine(expectedText, "MinMana=5000", "MinMana=0");
            // This non-magic class has no gear mana: the canonical writer
            // persists both the calculated maximum and current mana as zero.
            expectedText = ReplaceLine(expectedText, "MaxMana=5000", "MaxMana=0");
            files[characterPath] = Encoding.UTF8.GetBytes(expectedText);
            f.ActorFrames.Clear();
            int? xpStart = null;
            f.BeforeSend = (_, _) => { if (binding.Player.Level == 140) xpStart ??= f.ActorFrames.Count; };
            try { await HitAsync(f, monster); }
            finally { f.BeforeSend = null; }
            var after = f.Current();
            Assert.Equal(140, after.Character.Level);
            Assert.Equal(0, after.Character.Experience);
            Assert.Equal(15, after.Character.AttributePoints);
            Assert.Equal("one", after.Character.Attributes["Other"]);
            Assert.Equal("two", after.Character.Attributes["other"]);
            Assert.Equal((ushort)140, binding.Player.Level);
            Assert.Equal(0UL, binding.Player.Experience);
            Assert.Equal((ushort)15, binding.Player.Atributes);
            Assert.Equal(11136, after.Character.MinHitPoints);
            Assert.Equal(0, after.Character.MinMana);
            Assert.Equal(0, after.Character.MaxMana);
            Assert.Equal(11136, binding.Player.HitPoints);
            Assert.Equal(0u, binding.Player.Mana);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            var disk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(after.AccountId);
            Assert.NotNull(disk);
            Assert.Equal(after.Character.Level, disk.Level);
            Assert.Equal(after.Character.Experience, disk.Experience);
            Assert.Equal(after.Character.AttributePoints, disk.AttributePoints);
            f.Data.AssertFiles(files);
            Assert.NotNull(xpStart);
            Assert.True(xpStart.Value > 0); // The actual hit response precedes XP.
            var frames = f.ActorFrames.Skip(xpStart.Value).ToArray();
            var uid = OwnedEquipmentBehaviorFixture.Actor;
            var expectedFrames = new[]
            {
                Update(uid, (LegacyMsgUpdateDataType.Level, 140)),
                Update(uid, (LegacyMsgUpdateDataType.Atributes, 15)),
                Update(uid, (LegacyMsgUpdateDataType.MaxHitpoints, 11136)),
                Update(uid, (LegacyMsgUpdateDataType.MaxHitpoints, 11136), (LegacyMsgUpdateDataType.Hitpoints, 11136)),
                Update(uid, (LegacyMsgUpdateDataType.MaxMana, 0)),
                Update(uid, (LegacyMsgUpdateDataType.MaxMana, 0), (LegacyMsgUpdateDataType.Mana, 0)),
                Update(uid, (LegacyMsgUpdateDataType.Experience, 0)),
            };
            Assert.Equal(expectedFrames.Length, frames.Length);
            for (int i = 0; i < frames.Length; i++) Assert.Equal(expectedFrames[i], frames[i]);
        });

    private static byte[] Update(uint uid, params (LegacyMsgUpdateDataType Type, ulong Value)[] entries) =>
        new LegacyMsgUpdatePacket(uid, entries.Select(entry => new LegacyMsgUpdateEntry(entry.Type, entry.Value)).ToArray()).ToFrameBytes();

    private static string ReplaceLine(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n";
        Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }

    private static Task RunHitAsync(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, Monster,
        MonsterRuntimeStateStore, Task> body)
    {
        var family = new Monster(910, "OwnedExperience", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-xp/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            await body(f, monster, family, states);
        });
    }

    private static Task HitAsync(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster) =>
        f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
            Uid: OwnedEquipmentBehaviorFixture.Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y,
            AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());
}
