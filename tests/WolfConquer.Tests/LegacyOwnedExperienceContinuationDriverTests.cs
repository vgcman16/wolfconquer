using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Fact]
    public Task ActualHitCrossesBothThresholdsAndClearsItsAwardOnce() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(files[key]);
        text = Replace(text, "Level=138", "Level=140");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        files[key] = Encoding.UTF8.GetBytes(text);
        f.ActorFrames.Clear();

        await Hit(f, monster);

        Assert.True(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _), Diagnostic(f));
        var after = f.Current();
        Assert.Equal(140, after.Character.Level); Assert.Equal(0, after.Character.Experience);
        Assert.Equal(18, after.Character.AttributePoints); Assert.Equal(11136, after.Character.MinHitPoints);
        Assert.Equal((ushort)140, binding.Player.Level); Assert.Equal(0UL, binding.Player.Experience);
        Assert.Equal((ushort)18, binding.Player.Atributes); Assert.Equal(11136, binding.Player.HitPoints);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            after.AccountId, checked((uint)after.Character.Uid)));
        f.Data.AssertFiles(files);
        var frames = f.ActorFrames;
        var uid = OwnedEquipmentBehaviorFixture.Actor;
        int level139 = frames.FindIndex(row => row.AsSpan().SequenceEqual(Update(uid, LegacyMsgUpdateDataType.Level, 139)));
        int level140 = frames.FindIndex(row => row.AsSpan().SequenceEqual(Update(uid, LegacyMsgUpdateDataType.Level, 140)));
        Assert.True(level139 >= 0 && level140 > level139);
        Assert.Equal(Update(uid, LegacyMsgUpdateDataType.Experience, 0), frames[^1]);
    });

    [Fact]
    public Task ActualFirstLevelQueryFailureColdReopenCompletesWithoutRecredit() => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expectedFiles = f.Data.Files();
        var characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var expectedText = Encoding.UTF8.GetString(expectedFiles[characterKey]);
        expectedText = Replace(expectedText, "Level=137", "Level=139");
        expectedText = Replace(expectedText, "Atributes=12", "Atributes=18");
        expectedText = Replace(expectedText, "MinHitPoints=5000", "MinHitPoints=11136");
        expectedFiles[characterKey] = Encoding.UTF8.GetBytes(expectedText);
        int failures = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (binding.Player.Level != 138 || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            failures++;
            throw new IOException("owned continuation first query interruption");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, failures);
        Assert.False(f.Recovery.IsBlocked);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(binding.IsRetired);
        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            before.AccountId, checked((uint)before.Character.Uid));
        Assert.NotNull(pending);
        Assert.Equal((ushort)138, pending.CurrentLevel);
        Assert.Equal((ushort)1, pending.StepIndex);
        Assert.Equal(LegacyExperienceContinuationPhase.QueryPending, pending.Phase);
        Assert.True(pending.CreditedExperience >= 2);
        AssertEarnedFromHit(f.ActorFrames, monster, pending);
        Assert.Contains(f.ActorFrames, row => row.AsSpan().SequenceEqual(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138)));
        Assert.Equal(pending.InitialExperience + pending.CreditedExperience,
            pending.RemainingExperience + pending.ConsumedExperience + pending.CappedExperience);
        var finalExperience = pending.RemainingExperience - 1;
        Assert.True(finalExperience < 1_000_000_000);
        expectedText = Replace(expectedText, "Experience=0", "Experience=" + finalExperience.ToString(System.Globalization.CultureInfo.InvariantCulture));
        expectedFiles[characterKey] = Encoding.UTF8.GetBytes(expectedText);
        await f.ReopenContinuationAsync(catalog);
        var resumed = f.Current();
        var newBinding = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
        Assert.Equal(139, resumed.Character.Level); Assert.Equal(checked((long)finalExperience), resumed.Character.Experience);
        Assert.Equal(18, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
        Assert.Equal((ushort)139, newBinding.Player.Level); Assert.Equal(finalExperience, newBinding.Player.Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(newBinding));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            resumed.AccountId, checked((uint)resumed.Character.Uid)));
        f.Data.AssertFiles(expectedFiles);
        AssertResumeWire(f.ActorFrames);
        Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Experience, finalExperience), f.ActorFrames[^1]);
    }, initialLevel: 137);

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, WorldDataCatalog, Task> body,
        int initialLevel = 138, bool excessVigor = false, bool omitOptionalScalars = false)
    {
        var family = new Monster(910, "OwnedContinuation", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "2500000", ["Boss"] = "0", ["lookface"] = "910", ["level"] = initialLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        static LevelExperience Threshold(int level, int amount) => new(0, level, amount, 0, 0, 1,
            $"0 {level} {amount} 0 0", new[] { "0", level.ToString(System.Globalization.CultureInfo.InvariantCulture),
                amount.ToString(System.Globalization.CultureInfo.InvariantCulture), "0", "0" });
        var rows = initialLevel == 137
            ? new[] { Threshold(137, 1), Threshold(138, 1), Threshold(139, 1_000_000_000) }
            : new[] { Threshold(138, 1), Threshold(139, 1) };
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: rows);
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: initialLevel, reborn: 1,
            strength: 100, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1" },
        };
        if (excessVigor)
        {
            var attributes = new Dictionary<string, string>(actor.Attributes, StringComparer.OrdinalIgnoreCase) { ["Vigor"] = "9999" };
            actor = actor with { Attributes = attributes };
        }
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty,
            OmitOptionalCharacterScalars: omitOptionalScalars), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-continuation/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            await body(f, monster, catalog);
        });
    }

    private static Task Hit(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster) =>
        f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
            Uid: OwnedEquipmentBehaviorFixture.Actor, OpponentUid: monster.Uid, X: monster.X, Y: monster.Y,
            AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0, ResponseDamage: 0,
            Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());

    private static async Task ExpectHitFailure(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster)
    {
        try { await Assert.ThrowsAnyAsync<Exception>(() => Hit(f, monster)); }
        catch (Xunit.Sdk.XunitException error)
        { throw new Xunit.Sdk.XunitException(error.Message + "\n" + Diagnostic(f)); }
    }

    private static string Diagnostic(OwnedEquipmentBehaviorFixture f)
    {
        var log = f.Log.ToString();
        if (log.Length > 16000) log = log[^16000..];
        var lines = File.ReadAllLines(f.CharacterPath()).Where(line =>
            line.StartsWith("Level=", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Experience=", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("ExperienceContinuation=", StringComparison.OrdinalIgnoreCase));
        return $"Continuation diagnostic: recoveryBlocked={f.Recovery.IsBlocked}; " +
            $"holderPresent={f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _)}; " +
            $"saved={string.Join(";", lines)}\n{log}";
    }

    private static byte[] Update(uint uid, LegacyMsgUpdateDataType type, ulong value) =>
        new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(type, value)]).ToFrameBytes();

    private static void AssertResumeWire(List<byte[]> frames)
    {
        int secondLevel = frames.FindIndex(row => row.AsSpan().SequenceEqual(
            Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 139)));
        Assert.True(secondLevel > 0);
        Assert.True(frames.Take(secondLevel).Count(row =>
            OwnedEquipmentBehaviorFixture.ReadFrame(row).Header.Opcode == LegacyPlayerStatusPacket.Opcode) >= 2);
        Assert.Contains(frames.Skip(secondLevel + 1), row =>
            OwnedEquipmentBehaviorFixture.ReadFrame(row).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
    }

    private static void AssertEarnedFromHit(List<byte[]> frames, LegacyMonsterInstance monster,
        LegacyExperienceContinuation pending)
    {
        var attacks = frames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Select(frame => MsgAttackPacket.TryParse(frame, out var packet) ? packet : (MsgAttackPacket?)null)
            .Where(packet => packet is { } value && value.Query.AttackType == MsgAttackType.Physical && value.Query.OpponentUid == monster.Uid)
            .Select(packet => packet!.Value).ToArray();
        var attack = Assert.Single(attacks);
        Assert.True(attack.Query.Damage > 0);
        // Same-level monster, rate one, no gear/gems or XP modifiers; source
        // applies its minimum ten to this actual physical response damage.
        Assert.Equal(checked((ulong)Math.Max(10, attack.Query.Damage)), pending.CreditedExperience);
    }

    private static string Replace(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n";
        Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }
}
