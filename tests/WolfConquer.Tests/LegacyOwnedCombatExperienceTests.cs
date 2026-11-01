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
    public Task EquipmentStatusOutputFailureAfterLevelCommitFencesWithoutRestoringHitpoints() =>
        RunHitAsync(async (f, monster, family, states) =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var files = f.Data.Files();
            var path = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var text = Encoding.UTF8.GetString(files[path]);
            text = ReplaceLine(text, "Level=139", "Level=140");
            text = ReplaceLine(text, "Atributes=12", "Atributes=15");
            files[path] = Encoding.UTF8.GetBytes(text);
            int interrupted = 0;
            Dictionary<string, byte[]>? atFailure = null;
            long observedHp = -1;
            f.BeforeSend = (_, bytes) =>
            {
                if (binding.Player.Level != 140 ||
                    OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
                interrupted++;
                observedHp = f.Current().Character.MinHitPoints;
                atFailure = f.Data.Files();
                throw new IOException("owned XP equipment status interruption");
            };
            Exception? outputError = null;
            try { outputError = await Assert.ThrowsAnyAsync<Exception>(() => HitAsync(f, monster)); }
            finally { f.BeforeSend = null; }
            Assert.IsType<IOException>(outputError);
            Assert.Equal(1, interrupted);
            Assert.Equal(5000, observedHp); Assert.Equal(5000, binding.Player.HitPoints);
            Assert.Equal((ushort)140, binding.Player.Level); Assert.Equal(0UL, binding.Player.Experience);
            Assert.Equal((ushort)15, binding.Player.Atributes);
            Assert.NotNull(atFailure);
            var pending = AssertPendingCheckpoint(f, before, monster, files, atFailure);
            Assert.False(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.True(binding.IsRetired);
            Assert.False(binding.Client.OnAutoAttack);
            f.Data.AssertFiles(atFailure);
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            await AssertResumesOnceAsync(f, before, pending, files, path);
        });

    [Fact]
    public Task ActualOutputFailureAfterCommittedLevelFencesTheHolder() =>
        RunHitAsync(async (f, monster, family, states) =>
        {
            var before = f.Current();
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
            var native = binding.Player;
            var files = f.Data.Files();
            var path = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var text = Encoding.UTF8.GetString(files[path]);
            text = ReplaceLine(text, "Level=139", "Level=140");
            text = ReplaceLine(text, "Atributes=12", "Atributes=15");
            files[path] = Encoding.UTF8.GetBytes(text);
            Dictionary<string, byte[]>? atFailure = null;
            int interrupted = 0;
            f.BeforeSend = (_, _) =>
            {
                if (native.Level != 140) return;
                interrupted++;
                atFailure = f.Data.Files();
                throw new IOException("owned XP output interruption");
            };
            Exception? outputError = null;
            try { outputError = await Assert.ThrowsAnyAsync<Exception>(() => HitAsync(f, monster)); }
            finally { f.BeforeSend = null; }
            Assert.IsType<IOException>(outputError);
            Assert.Equal(1, interrupted); Assert.NotNull(atFailure);
            var pending = AssertPendingCheckpoint(f, before, monster, files, atFailure);
            Assert.False(f.Recovery.IsBlocked);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.True(binding.IsRetired); Assert.False(binding.Client.OnAutoAttack);
            Assert.Equal((ushort)140, native.Level); Assert.Equal(0UL, native.Experience);
            Assert.Equal((ushort)15, native.Atributes); Assert.Equal(5000, native.HitPoints);
            f.Data.AssertFiles(atFailure);
            var disk = new CharacterSnapshotLoader(f.Data.Users).LoadByAccountId(before.AccountId);
            Assert.NotNull(disk);
            Assert.Equal(140, disk.Level);
            Assert.Equal(0, disk.Experience);
            Assert.Equal(15, disk.AttributePoints);
            Assert.True(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damaged));
            Assert.True(damaged.Hitpoints < monster.Hitpoints);
            await AssertResumesOnceAsync(f, before, pending, files, path);
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
            Assert.Contains("WorldServer refused combat experience continuation reason=experienceCreditReplace:capturedImageUnchanged:", f.Log.ToString());
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
            var accepted = f.Current();
            Assert.Equal("one", accepted.Character.Attributes["Other"]);
            Assert.Equal("two", accepted.Character.Attributes["other"]);
            var files = f.Data.Files();
            var characterPath = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var expectedText = Encoding.UTF8.GetString(files[characterPath]);
            expectedText = ReplaceLine(expectedText, "Level=139", "Level=140");
            expectedText = ReplaceLine(expectedText, "Atributes=12", "Atributes=15");
            expectedText = ReplaceLine(expectedText, "MinHitPoints=5000", "MinHitPoints=11136");
            files[characterPath] = Encoding.UTF8.GetBytes(expectedText);
            f.ActorFrames.Clear();
            int? xpStart = null;
            f.BeforeSend = (_, _) => { if (binding.Player.Level == 140) xpStart ??= f.ActorFrames.Count; };
            try { await HitAsync(f, monster); }
            finally { f.BeforeSend = null; }
            var after = f.Current();
            Assert.True(after.Character.Level == 140,
                $"Expected real level-up; current level={after.Character.Level}, XP={after.Character.Experience}, " +
                $"recoveryBlocked={f.Recovery.IsBlocked}; Frame log:\n{f.Log}");
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
            int level = Array.FindIndex(frames, bytes => bytes.AsSpan().SequenceEqual(Update(uid, (LegacyMsgUpdateDataType.Level, 140))));
            int points = Array.FindIndex(frames, bytes => bytes.AsSpan().SequenceEqual(Update(uid, (LegacyMsgUpdateDataType.Atributes, 15))));
            int status = Array.FindIndex(frames, bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            int refine = Array.FindIndex(frames, bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == LegacyUserTotalRefineLevPacket.Opcode);
            int restored = Array.FindIndex(frames, bytes => bytes.AsSpan().SequenceEqual(Update(uid,
                (LegacyMsgUpdateDataType.MaxHitpoints, 11136), (LegacyMsgUpdateDataType.Hitpoints, 11136))));
            Assert.True(level >= 0 && level < points && points < status && status < refine && refine < restored);
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frames[status]), out var queryStatus));
            Assert.Equal(11136U, queryStatus.MaxHP); Assert.Equal(0U, queryStatus.MaxMana);
            Assert.Contains(frames.Skip(status + 1).Take(refine - status - 1), bytes => bytes.AsSpan().SequenceEqual(Update(uid,
                (LegacyMsgUpdateDataType.MaxHitpoints, 11136), (LegacyMsgUpdateDataType.Hitpoints, 5000))));
            Assert.Equal(Update(uid, (LegacyMsgUpdateDataType.Experience, 0)), frames[^1]);
        });

    private static LegacyExperienceContinuation AssertPendingCheckpoint(OwnedEquipmentBehaviorFixture f, WorldSession before,
        LegacyMonsterInstance monster, Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        var path = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (name, bytes) in expected)
        {
            if (!string.Equals(name, path, StringComparison.OrdinalIgnoreCase)) { Assert.Equal(bytes, actual[name]); continue; }
            var actualText = Encoding.UTF8.GetString(actual[name]);
            var lines = actualText.Split("\r\n", StringSplitOptions.None)
                .Where(line => line.StartsWith("ExperienceContinuation=", StringComparison.Ordinal)).ToArray();
            var marker = Assert.Single(lines);
            Assert.Equal(Encoding.UTF8.GetString(bytes), actualText.Replace(marker + "\r\n", "", StringComparison.Ordinal));
        }
        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(actual[path], before.AccountId, checked((uint)before.Character.Uid));
        Assert.NotNull(pending);
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
        Assert.Equal((ushort)139, pending.InitialLevel); Assert.Equal((ushort)140, pending.CurrentLevel);
        Assert.Equal((ushort)1, pending.StepIndex); Assert.Equal(1UL, pending.ConsumedExperience);
        Assert.Equal(0UL, pending.InitialExperience); Assert.Equal(0UL, pending.RemainingExperience);
        var attacks = f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Select(frame => MsgAttackPacket.TryParse(frame, out var packet) ? packet : (MsgAttackPacket?)null)
            .Where(packet => packet is { } hit && hit.Query.AttackType == MsgAttackType.Physical && hit.Query.OpponentUid == monster.Uid)
            .Select(packet => packet!.Value).ToArray();
        var response = Assert.Single(attacks);
        var earned = checked((ulong)Math.Max(10, response.Query.Damage));
        Assert.Equal(earned, pending.CreditedExperience); Assert.Equal(earned - 1, pending.CappedExperience);
        return pending;
    }

    private static async Task AssertResumesOnceAsync(OwnedEquipmentBehaviorFixture f, WorldSession before,
        LegacyExperienceContinuation pending, Dictionary<string, byte[]> expected, string path)
    {
        await f.ReconnectContinuationAsync();
        var resumed = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
        Assert.NotSame(before.NativeGameClient, resumed.NativeGameClient);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.False(binding.Client.OnAutoAttack);
        Assert.Equal(140, resumed.Character.Level); Assert.Equal(0, resumed.Character.Experience);
        Assert.Equal(15, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
        Assert.Equal((ushort)140, binding.Player.Level); Assert.Equal(0UL, binding.Player.Experience);
        Assert.Equal((ushort)15, binding.Player.Atributes); Assert.Equal(11136, binding.Player.HitPoints);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid)));
        var text = Encoding.UTF8.GetString(expected[path]);
        text = ReplaceLine(text, "MinHitPoints=5000", "MinHitPoints=11136");
        expected[path] = Encoding.UTF8.GetBytes(text);
        f.Data.AssertFiles(expected);
        Assert.Equal(1UL, pending.ConsumedExperience);
        Assert.DoesNotContain(f.ActorFrames, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Physical);
        Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, (LegacyMsgUpdateDataType.Experience, 0)), f.ActorFrames[^1]);
    }

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
            MinMana = 0, MaxMana = 0,
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
