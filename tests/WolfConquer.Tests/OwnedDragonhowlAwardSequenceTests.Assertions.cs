using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlAwardSequenceTests
{
    // Independent D13 protobuf2176 bytes; no expected bytes are captured from
    // the operation. Peer1293601 uses A1 FA4E; 540=9C04 and 810=AA06.
    private static readonly byte[] ExperienceTen = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 10];
    private static readonly byte[] ExperienceNine = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 9];
    private static readonly byte[] LevelEleven = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 12, 16, 11];
    private static readonly byte[] LevelTwelve = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 12, 16, 12];
    private static readonly byte[] Restored540 = [20, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 156, 4, 18, 3, 16, 156, 4];
    private static readonly byte[] Restored810 = [20, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 170, 6, 18, 3, 16, 170, 6];
    private static readonly byte[] Dead30 = [16, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 1, 16, 30, 18, 0];
    private static readonly byte[] Dead810 = [17, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 170, 6, 18, 0];

    private static Dictionary<string, byte[]> ExpectedFiles(Sequence s, int awards, bool dead)
    {
        var f = s.Fixture;
        var files = f.Data.Files();
        void Set(uint uid, params (string Before, string After)[] replacements)
        {
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid));
            string text = Encoding.UTF8.GetString(files[key]);
            foreach (var (before, after) in replacements)
            {
                string line = "\r\n" + before + "\r\n";
                Assert.Contains(line, text);
                text = text.Replace(line, "\r\n" + after + "\r\n", StringComparison.Ordinal);
            }
            files[key] = Encoding.UTF8.GetBytes(text);
        }
        Set(Actor, ("MinMana=500", "MinMana=493"),
            ("ConquerPoints=639778904", "ConquerPoints=" + (639_778_904L + awards * 5)));
        if (s.Multiple)
        {
            int value = awards == 1 ? 20 : 30;
            Set(Peer, ("Level=10", "Level=" + (10 + awards)), ("Experience=0", "Experience=9"),
                ("Vitaliti=0", "Vitaliti=" + value), ("Spirit=10", "Spirit=" + value),
                ("MinHitPoints=30", "MinHitPoints=" + (dead ? 0 : awards == 1 ? 540 : 810)));
        }
        else Set(Peer, ("Experience=0", "Experience=10"), ("MinHitPoints=30", "MinHitPoints=" + (dead ? 0 : 30)));
        if (s.MissingSpells && dead)
            files.Add(Path.GetRelativePath(f.Data.Root, s.SpellPath), new byte[] { 0, 0, 0, 0 });
        return files;
    }

    private static void AssertPeer(Sequence s, LegacyOrdinarySpellBinding binding, int awards, bool dead)
    {
        var current = s.Fixture.Current(Peer);
        long level = s.Multiple ? 10 + awards : 10, xp = s.Multiple ? 9 : 10;
        int vitality = s.Multiple ? awards == 1 ? 20 : 30 : 0;
        int spirit = s.Multiple ? vitality : 10;
        uint maximum = s.Multiple ? awards == 1 ? 540U : 810U : 30U;
        uint mana = s.Multiple ? awards == 1 ? 600U : 900U : 300U;
        int hp = dead ? 0 : (int)maximum;
        Assert.Same(binding, current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(s.Fixture.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(level, current.Character.Level); Assert.Equal(xp, current.Character.Experience);
        Assert.Equal((long)vitality, current.Character.Vitality); Assert.Equal((long)spirit, current.Character.Spirit);
        Assert.Equal(12L, current.Character.AttributePoints); Assert.Equal((long)hp, current.Character.MinHitPoints);
        Assert.Equal((ushort)level, binding.Player.Level); Assert.Equal((ulong)xp, binding.Player.Experience);
        Assert.Equal((ushort)vitality, binding.Player.Vitality); Assert.Equal((ushort)spirit, binding.Player.Spirit);
        Assert.Equal((ushort)12, binding.Player.Atributes); Assert.Equal(hp, binding.Player.HitPoints);
        Assert.Equal(20L, current.Character.MinMana); Assert.Equal(450L, current.Character.MaxMana);
        Assert.Equal(20U, binding.Player.Mana);
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current, out var actualMaximum));
        Assert.Equal(maximum, actualMaximum); Assert.Equal(mana, LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana);
        Assert.Equal(maximum, binding.Client.Status.MaxHitpoints); Assert.Equal(mana, binding.Client.Status.MaxMana);
        Assert.True(LegacyNativeMagicQueryClient.TryRead(s.Fixture.Store, s.Fixture.Connection(Peer), Peer,
            out var magic, binding.NativeIdentity));
        Assert.Equal((uint)(spirit + 1), magic.MagicAttack);
        Assert.Equal("peer-seed", current.Character.Attributes["OpaqueOverlap"]);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
        var image = File.ReadAllBytes(s.Fixture.CharacterPath(Peer));
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        Assert.Equal(level, disk.Level); Assert.Equal(xp, disk.Experience); Assert.Equal((long)hp, disk.MinHitPoints);
        Assert.Equal((long)vitality, disk.Vitality); Assert.Equal((long)spirit, disk.Spirit);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(image, Peer, Peer));
    }

    private static bool IsUpdate(byte[] bytes, uint uid, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
        packet.Uid == uid && packet.Updates.Any(row => row.Type == type && row.Value == value);
    private static bool IsAnimation(byte[] bytes, ushort spellId) =>
        LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == spellId;
    private static string Log(Sequence s)
    {
        string value = s.Fixture.Log.ToString();
        return value.Length <= 12_000 ? value : value[^12_000..];
    }
}
