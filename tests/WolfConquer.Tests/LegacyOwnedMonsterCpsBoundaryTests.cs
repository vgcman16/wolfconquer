using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedMonsterCpsBoundaryTests
{
    [Fact]
    public Task ExactTwoBillionRewardCommitsBeforeOutputAndXp() => RunAsync(1_999_999_998, async (f, monster, states) =>
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        var files = f.Data.Files(); string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        string initial = Encoding.UTF8.GetString(files[key]);
        string reward = Replace(initial, "ConquerPoints=1999999998", "ConquerPoints=2000000000");
        var expected = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase) { [key] = Encoding.UTF8.GetBytes(reward) };
        int seen = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (!CpsFrame(bytes, out var value)) return;
            Assert.Equal(2_000_000_000UL, value); Assert.Equal(0, seen++);
            Assert.Equal(2_000_000_000L, f.Current().Character.ConquerPoints);
            Assert.Equal(2_000_000_000U, binding.Player.ConquerPoints);
            Assert.Equal(139L, f.Current().Character.Level); f.Data.AssertFiles(expected);
        };
        try { await HitAsync(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, seen);
        expected[key] = Encoding.UTF8.GetBytes(LevelImage(reward)); f.Data.AssertFiles(expected);
        Assert.Equal(2_000_000_000L, f.Current().Character.ConquerPoints);
        Assert.Equal(2_000_000_000U, binding.Player.ConquerPoints);
        Assert.Equal(140L, f.Current().Character.Level); Assert.Equal((ushort)140, binding.Player.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        int cp = f.ActorFrames.FindIndex(bytes => CpsFrame(bytes, out var value) && value == 2_000_000_000UL);
        int xp = f.ActorFrames.FindIndex(bytes => ExperienceFrame(bytes)); Assert.True(cp >= 0 && xp > cp);
    });

    [Fact]
    public Task AboveTwoBillionRefusesEntireRewardButAllowsLegitimateXp() => RunAsync(1_999_999_999, async (f, monster, states) =>
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files(); string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        expected[key] = Encoding.UTF8.GetBytes(LevelImage(Encoding.UTF8.GetString(expected[key])));
        await HitAsync(f, monster);
        Assert.Equal(1_999_999_999L, f.Current().Character.ConquerPoints);
        Assert.Equal(1_999_999_999U, binding.Player.ConquerPoints);
        Assert.Equal(140L, f.Current().Character.Level); Assert.Equal((ushort)140, binding.Player.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); f.Data.AssertFiles(expected);
        Assert.DoesNotContain(f.ActorFrames, bytes => CpsFrame(bytes, out _));
        Assert.Contains(f.ActorFrames, ExperienceFrame);
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
    });

    [Fact]
    public Task SavedWalletChangedAfterAdmissionRefusesRewardAndLaterXp() => RunAsync(639_778_904, async (f, monster, states) =>
    {
        var before = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var bytes = File.ReadAllBytes(f.CharacterPath());
        var changed = Replace(Encoding.UTF8.GetString(bytes), "ConquerPoints=639778904", "ConquerPoints=639778905");
        File.WriteAllBytes(f.CharacterPath(), Encoding.UTF8.GetBytes(changed));
        var expected = f.Data.Files();
        await HitAsync(f, monster);
        Assert.Same(before, f.Current()); Assert.Equal(639_778_904L, f.Current().Character.ConquerPoints);
        Assert.Equal(639_778_904U, binding.Player.ConquerPoints); Assert.Equal((ushort)139, binding.Player.Level);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); f.Data.AssertFiles(expected);
        Assert.DoesNotContain(f.ActorFrames, bytes => CpsFrame(bytes, out _) || ExperienceFrame(bytes));
        Assert.Contains("refused owned monster-death random CP", f.Log.ToString());
        Assert.Contains("WorldServer refused combat experience continuation reason=experienceCreditCharacterImageInvalid.", f.Log.ToString());
        Assert.False(states.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
    });

    private static Task RunAsync(long cps, Func<OwnedEquipmentBehaviorFixture, LegacyMonsterInstance, MonsterRuntimeStateStore, Task> body)
    {
        var family = new Monster(910, "WalletBoundary", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            ConquerPoints = cps, MinMana = 0, MaxMana = 0,
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
                1011, family.Name, "fixture://wallet-boundary/910.ini", new Dictionary<string, string> { ["DynamicID"] = "77" });
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, family, states.NextLegacyDynamicMonsterUid()));
            Assert.Same(monster, states.RegisterDynamicMonster(monster, family));
            Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, family, out _));
            f.ActorFrames.Clear(); await body(f, monster, states);
        });
    }

    private static bool CpsFrame(byte[] bytes, out ulong value)
    {
        value = 0;
        if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
            packet.Uid != OwnedEquipmentBehaviorFixture.Actor || packet.Updates.Count != 1 ||
            packet.Updates[0].Type != LegacyMsgUpdateDataType.ConquerPoints) return false;
        value = packet.Updates[0].Value; return true;
    }
    private static bool ExperienceFrame(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == OwnedEquipmentBehaviorFixture.Actor && packet.Updates.Count == 1 &&
        packet.Updates[0].Type == LegacyMsgUpdateDataType.Experience && packet.Updates[0].Value == 0;
    private static string LevelImage(string text)
    {
        text = Replace(text, "Level=139", "Level=140"); text = Replace(text, "Atributes=12", "Atributes=15");
        return Replace(text, "MinHitPoints=5000", "MinHitPoints=13836");
    }
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
