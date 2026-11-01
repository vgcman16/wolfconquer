using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterRandomCpsTests
{
    [Fact]
    public Task RealRandomRewardIsDurableBeforeOutputAndKillerXp() => RunAsync(async (f, monster, states) =>
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files(); string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        string initial = Encoding.UTF8.GetString(files[key]);
        string cpText = Replace(initial, "ConquerPoints=639778904", "ConquerPoints=639778906");
        var cpFiles = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase) { [key] = Encoding.UTF8.GetBytes(cpText) };
        int cpOutput = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (!IsCps(bytes, 639778906)) return;
            Assert.Equal(0, cpOutput++);
            Assert.Equal(639778906L, f.Current().Character.ConquerPoints);
            Assert.Equal(639778906U, binding.Player.ConquerPoints);
            Assert.Equal(139L, f.Current().Character.Level);
            f.Data.AssertFiles(cpFiles);
        };
        try { await HitAsync(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, cpOutput);
        string final = Replace(cpText, "Level=139", "Level=140");
        final = Replace(final, "Atributes=12", "Atributes=15");
        final = Replace(final, "MinHitPoints=5000", "MinHitPoints=13836");
        cpFiles[key] = Encoding.UTF8.GetBytes(final);
        f.Data.AssertFiles(cpFiles);
        Assert.Equal(140L, f.Current().Character.Level); Assert.Equal((ushort)140, binding.Player.Level);
        Assert.Equal(639778906L, f.Current().Character.ConquerPoints); Assert.Equal(639778906U, binding.Player.ConquerPoints);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        int cp = f.ActorFrames.FindIndex(bytes => IsCps(bytes, 639778906));
        int xp = f.ActorFrames.FindIndex(bytes => IsUpdate(bytes, LegacyMsgUpdateDataType.Experience, 0));
        Assert.True(cp >= 0 && xp > cp);
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
    });

    [Fact]
    public Task RealCharacterLeaseRefusesRewardWithoutWalletOrSuccessOutput() => RunAsync(async (f, monster, states) =>
    {
        var before = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files();
        using (new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            await HitAsync(f, monster);
        Assert.Same(before, f.Current()); Assert.Equal(639778904U, binding.Player.ConquerPoints);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); f.Data.AssertFiles(files);
        Assert.DoesNotContain(f.ActorFrames, bytes => IsCps(bytes, 639778906));
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
    });

    [Fact]
    public Task LostCpsOutputFencesAfterDurableRewardWithoutXpReplay() => RunAsync(async (f, monster, states) =>
    {
        var before = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files(); string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        files[key] = Encoding.UTF8.GetBytes(Replace(Encoding.UTF8.GetString(files[key]),
            "ConquerPoints=639778904", "ConquerPoints=639778906"));
        int interrupted = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (!IsCps(bytes, 639778906)) return;
            Assert.Equal(0, interrupted++); f.Data.AssertFiles(files);
            Assert.Equal(639778906U, binding.Player.ConquerPoints);
            throw new IOException("monster random CP output lost");
        };
        try { await Assert.ThrowsAnyAsync<Exception>(() => HitAsync(f, monster)); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, interrupted); Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        f.Data.AssertFiles(files);
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
    });

    private static Task RunAsync(Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, MonsterRuntimeStateStore, Task> body)
    {
        var family = new Monster(910, "RewardTarget", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "910", ["life"] = "100", ["Boss"] = "0", ["lookface"] = "910", ["level"] = "139",
            ["attack_range"] = "1", ["view_range"] = "30", ["attack_speed"] = "1000", ["move_speed"] = "500",
            ["attack_min"] = "5", ["attack_max"] = "6", ["attack_user"] = "3", ["settings"] = "53",
        }, []);
        var threshold = new LevelExperience(0, 139, 1, 0, 0, 1, "0 139 1 0 0", new[] { "0", "139", "1", "0", "0" });
        var catalog = LegacyItemWorldDataFixture.Create([], [], monsters: new[] { family }, levelExperience: new[] { threshold });
        var actor = OwnedEquipmentBehaviorFixture.Character(classCode: 1000, level: 139, reborn: 1,
            strength: 1000, mapId: 1011, x: 430, y: 380, minHitPoints: 5000) with
        {
            MinMana = 0, MaxMana = 0,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["DynamicID"] = "77", ["Action"] = "0", ["Stamina"] = "100", ["UserExpRate"] = "1",
              ["LegacyMonsterDeathConquerPointRatePercent"] = "100", ["LegacyMonsterDeathConquerPointRateRoll"] = "0",
              ["LegacyMonsterDeathConquerPointRoll"] = "2" },
        };
        return OwnedEquipmentBehaviorFixture.RunAsync(new(actor, PlayerItemSnapshot.Empty), catalog, async f =>
        {
            var states = Assert.IsType<MonsterRuntimeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_monsterStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            var spawn = new MonsterSpawn(910, 1011, family.Id, 431, 380, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1011, family.Name, "fixture://owned-random-cp/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            f.ActorFrames.Clear(); await body(f, monster, states);
        });
    }

    private static bool IsCps(byte[] bytes, ulong value) => IsUpdate(bytes, LegacyMsgUpdateDataType.ConquerPoints, value);
    private static bool IsUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == OwnedEquipmentBehaviorFixture.Actor && packet.Updates.Count == 1 && packet.Updates[0].Type == type && packet.Updates[0].Value == value;
    private static string Replace(string text, string before, string after)
    {
        string needle = "\r\n" + before + "\r\n"; Assert.Contains(needle, text);
        return text.Replace(needle, "\r\n" + after + "\r\n", StringComparison.Ordinal);
    }
    private static Task HitAsync(OwnedEquipmentBehaviorFixture f, LegacyMonsterInstance monster) =>
        f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgAttackPacket(new MsgAttackQuery(
            OwnedEquipmentBehaviorFixture.Actor, monster.Uid, monster.X, monster.Y, MsgAttackType.Physical,
            0, 0, 0, MsgAttackEffect.None, 0)).ToFrameBytes());
}
