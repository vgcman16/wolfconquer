using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    private static readonly byte[] Exp10 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 10];
    private static readonly byte[] Exp9 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 9];
    private static readonly byte[] Level11 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 12, 16, 11];
    private static readonly byte[] Restore540 = [20, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 156, 4, 18, 3, 16, 156, 4];

    private static Dictionary<string, byte[]> ExpectedAwardFiles(Case c)
    {
        var f = c.Fixture; var result = f.Data.Files();
        void Set(uint uid, params (string Before, string After)[] edits)
        {
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
            string text = Encoding.UTF8.GetString(result[key]);
            foreach (var (before, after) in edits)
            {
                string line = "\r\n" + before + "\r\n"; Assert.Contains(line, text);
                text = text.Replace(line, "\r\n" + after + "\r\n", StringComparison.Ordinal);
            }
            result[key] = Encoding.UTF8.GetBytes(text);
        }
        Set(Actor, ("MinMana=500", "MinMana=493"), ("ConquerPoints=639778904", "ConquerPoints=639778909"));
        if (c.Threshold) Set(Peer, ("Level=10", "Level=11"), ("Experience=0", "Experience=9"),
            ("Vitaliti=0", "Vitaliti=20"), ("Spirit=10", "Spirit=20"), ("MinHitPoints=30", "MinHitPoints=540"));
        else Set(Peer, ("Experience=0", "Experience=10"));
        return result;
    }

    private static void AssertAward(Case c, LegacyOrdinarySpellBinding binding)
    {
        var f = c.Fixture; var current = f.Current(Peer);
        Assert.Same(binding, current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(c.Threshold ? 11L : 10L, current.Character.Level);
        Assert.Equal(c.Threshold ? 9L : 10L, current.Character.Experience);
        Assert.Equal(c.Threshold ? 540L : 30L, current.Character.MinHitPoints);
        Assert.Equal(c.Threshold ? 20L : 0L, current.Character.Vitality);
        Assert.Equal(c.Threshold ? 20L : 10L, current.Character.Spirit);
        Assert.Equal(12L, current.Character.AttributePoints); Assert.Equal(20L, current.Character.MinMana);
        Assert.Equal(450L, current.Character.MaxMana); Assert.Equal(830L, current.Character.X); Assert.Equal(594L, current.Character.Y);
        Assert.Equal((ushort)(c.Threshold ? 11 : 10), binding.Player.Level);
        Assert.Equal(c.Threshold ? 9UL : 10UL, binding.Player.Experience);
        Assert.Equal(c.Threshold ? 540 : 30, binding.Player.HitPoints);
        Assert.Equal((ushort)(c.Threshold ? 20 : 0), binding.Player.Vitality);
        Assert.Equal((ushort)(c.Threshold ? 20 : 10), binding.Player.Spirit);
        Assert.Equal((ushort)12, binding.Player.Atributes); Assert.Equal(20U, binding.Player.Mana);
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current, out var hp));
        Assert.Equal(c.Threshold ? 540U : 30U, hp);
        Assert.Equal(c.Threshold ? 600U : 300U, LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana);
        Assert.Equal(hp, binding.Client.Status.MaxHitpoints);
        Assert.Equal(c.Threshold ? 600U : 300U, binding.Client.Status.MaxMana);
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Store, f.Connection(Peer), Peer, out var magic, binding.NativeIdentity));
        Assert.Equal(c.Threshold ? 21U : 11U, magic.MagicAttack);
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
        var bytes = File.ReadAllBytes(f.CharacterPath(Peer));
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
        Assert.Equal(current.Character.Level, disk.Level); Assert.Equal(current.Character.Experience, disk.Experience);
        Assert.Equal(current.Character.MinHitPoints, disk.MinHitPoints);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(bytes, Peer, Peer));
        Assert.False(File.Exists(c.SpellPath));
    }

    private static object Native(LegacyOrdinarySpellBinding binding) => new
    {
        Stamp = Unsafe.BitCast<DateTime, long>(binding.Player.AttackStamp), binding.Player.HitPoints,
        binding.Player.Mana, binding.Player.Stamina, binding.Player.ConquerPoints, binding.Player.Level,
        binding.Player.Experience, binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality,
        binding.Player.Spirit, binding.Player.Atributes, binding.Player.Action, binding.Player.Angle,
        binding.Client.OnAutoAttack, binding.Client.AutoAttack,
    };
    private static object QueryIdentity(LegacyOrdinarySpellBinding binding)
    {
        object Read(object registry, object key)
        {
            object?[] args = [key, null];
            Assert.True((bool)registry.GetType().GetMethod("TryGetValue")!.Invoke(registry, args)!);
            return Assert.IsAssignableFrom<object>(args[1]);
        }
        // Read-only observation, never a constructed or borrowed query owner.
        var type = typeof(LegacyNativeMagicQueryClient);
        var entries = type.GetField("Entries", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var completed = type.GetField("CurrentMaximumCompletions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        return Read(completed, Read(entries, binding.NativeIdentity));
    }
    private static bool IsAnimation(byte[] bytes, ushort spell) =>
        LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == spell;
    private static bool IsCp(byte[] bytes) => LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == Actor && packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.ConquerPoints && row.Value == 639_778_909);
}
