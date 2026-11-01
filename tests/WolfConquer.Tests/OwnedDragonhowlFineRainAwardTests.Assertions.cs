using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFineRainAwardTests
{
    private static readonly byte[] Exp10 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 10];
    private static readonly byte[] Exp9 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 5, 16, 9];
    private static readonly byte[] Level11 = [14, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 12, 16, 11];
    private static readonly byte[] Restore640 = [20, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 128, 5, 18, 3, 16, 128, 5];
    private static readonly byte[] Dead30 = [16, 0, 128, 8, 8, 161, 250, 78, 18, 4, 8, 1, 16, 30, 18, 0];
    private static readonly byte[] Dead540 = [17, 0, 128, 8, 8, 161, 250, 78, 18, 5, 8, 1, 16, 156, 4, 18, 0];

    private static void AssertFineRain(WorldSession current, int stamp)
    {
        foreach (int flag in new[] { 212, 213 })
        {
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, flag));
            Assert.True(LegacyStatusFlagDeathRegistration.TryRead(current.Character, flag, out var remove)); Assert.True(remove);
            Assert.Equal("true", current.Character.Attributes[$"LegacyFineRain{flag - 211}RemoveOnDead"]);
        }
        Assert.Equal("100", current.Character.Attributes["FineRainPower"]);
        Assert.Equal("100", current.Character.Attributes["defFineRainPower"]);
        var admission = LegacyFineRainEquipmentAdmission.ForOwner(current);
        Assert.Equal((long)Peer, admission.OwnerUid); Assert.Equal(stamp, admission.StampMilliseconds);
        AssertOpaque(current);
    }

    private static void AssertOpaque(WorldSession current) => AssertOpaque(current.Character);
    private static void AssertOpaque(CharacterSnapshot character)
    {
        var key = Assert.Single(character.Attributes.Keys, value => value.Equals("OpaqueFineRain", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("OpaqueFineRain", key); Assert.Equal(Opaque, character.Attributes[key]);
        Assert.Equal("peer-seed", character.Attributes["OpaqueOverlap"]);
    }

    private static Dictionary<string, byte[]> ExpectedFiles(RainCase c, bool dead)
    {
        var f = c.Owner; var files = f.Data.Files();
        void Set(uint uid, params (string Before, string After)[] edits)
        {
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath(uid)); string text = Encoding.UTF8.GetString(files[key]);
            foreach (var (before, after) in edits)
            {
                string line = "\r\n" + before + "\r\n"; Assert.Contains(line, text);
                text = text.Replace(line, "\r\n" + after + "\r\n", StringComparison.Ordinal);
            }
            files[key] = Encoding.UTF8.GetBytes(text);
        }
        Set(Actor, ("MinMana=500", "MinMana=493"), ("ConquerPoints=639778904", "ConquerPoints=639778909"));
        Set(Peer, ("Experience=0", c.Threshold ? "Experience=9" : "Experience=10"),
            ("MinHitPoints=130", "MinHitPoints=" + (dead ? 0 : c.BeforeDeathHp)));
        if (c.Threshold) Set(Peer, ("Level=10", "Level=11"), ("Vitaliti=0", "Vitaliti=20"), ("Spirit=10", "Spirit=20"));
        return files;
    }

    private static void AssertAward(RainCase c, LegacyOrdinarySpellBinding peer)
    {
        var current = c.Owner.Current(Peer);
        AssertFineRain(current, c.Stamp);
        Assert.Same(peer, current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(c.Owner.Store.IsBoundSpellOwnerCurrent(peer));
        Assert.Equal(c.Threshold ? 11L : 10L, current.Character.Level); Assert.Equal(c.Threshold ? 9L : 10L, current.Character.Experience);
        Assert.Equal((long)c.BeforeDeathHp, current.Character.MinHitPoints); Assert.Equal(c.BeforeDeathHp, peer.Player.HitPoints);
        Assert.Equal((uint)c.BeforeDeathHp, peer.Client.Status.MaxHitpoints);
        Assert.Equal(c.Threshold ? 600U : 300U, peer.Client.Status.MaxMana);
        Assert.Equal((ushort)(c.Threshold ? 11 : 10), peer.Player.Level); Assert.Equal(c.Threshold ? 9UL : 10UL, peer.Player.Experience);
        Assert.Equal(20U, peer.Player.Mana); Assert.Equal(20L, current.Character.MinMana);
        Assert.False(LegacyEquipmentRebuildAdmission.ApplyCurrent(current, LegacyEquipmentRebuildReason.PlayerDeath, c.LegacyClock).Admitted);
        Assert.False(c.Monsters.TryGetVisibleInstance(c.Monster, c.Clock.GetUtcNow(), out _));
        Assert.Equal(639_778_909L, c.Owner.Current().Character.ConquerPoints);
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(c.Owner.CharacterPath(Peer)), out _));
        Assert.Equal(current.Character.Level, disk.Level); Assert.Equal(current.Character.Experience, disk.Experience);
        Assert.Equal((long)c.BeforeDeathHp, disk.MinHitPoints); AssertOpaque(disk);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacter(current.Character, current.AccountId));
    }

    private static void AssertDead(RainCase c, LegacyOrdinarySpellBinding peer)
    {
        var current = c.Owner.Current(Peer);
        Assert.Same(peer, current.NativeGameClient!.OrdinarySpellBinding); Assert.True(c.Owner.Store.IsBoundSpellOwnerCurrent(peer));
        var epoch = ReadEpoch(peer);
        string rawFields = string.Join(";", current.Character.Attributes.Where(pair =>
                new[] { "StatusFlags", "BitVector", "MaxHitpoints", "MaxHitPoints", "MaxLife" }
                    .Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .Select(pair => pair.Key + "=" + pair.Value[..Math.Min(pair.Value.Length, 224)]));
        string diagnostic = $"FineRain post-death: threshold={c.Threshold};spell={c.Spell};" +
            $"hp={current.Character.MinHitPoints};managedMax={current.EquipmentHitpointsStatus?.MaximumHitpoints};" +
            $"nativeMax={peer.Client.Status.MaxHitpoints};epoch={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(epoch):x};" +
            $"sameInitialEpoch={ReferenceEquals(c.InitialQuery.Epoch, epoch)};{rawFields}";
        // Diagnose the actual clear/admission boundary before testing arithmetic.
        // All existing expectations remain: no retained212/213 policy/deadline.
        foreach (int flag in new[] { 212, 213 })
        {
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, flag), diagnostic);
            Assert.False(LegacyStatusFlagDeathRegistration.TryRead(current.Character, flag, out _), diagnostic);
            Assert.DoesNotContain($"LegacyFineRain{flag - 211}ExpiresAtUtc", current.Character.Attributes.Keys);
            Assert.DoesNotContain($"LegacyFineRain{flag - 211}RemoveOnDead", current.Character.Attributes.Keys);
        }
        Assert.Equal(0L, current.Character.MinHitPoints); Assert.Equal(0, peer.Player.HitPoints);
        Assert.True(c.AfterDeathMaximum == current.EquipmentHitpointsStatus!.MaximumHitpoints, diagnostic);
        Assert.Equal(c.AfterDeathMaximum, peer.Client.Status.MaxHitpoints);
        Assert.Equal(c.Threshold ? 600U : 300U, peer.Client.Status.MaxMana);
        Assert.True(LegacyNativeMagicQueryClient.TryRead(c.Owner.Store, c.Owner.Connection(Peer), Peer, out var fields, peer.NativeIdentity));
        Assert.Equal(c.Threshold ? 21U : 11U, fields.MagicAttack);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal("100", current.Character.Attributes["FineRainPower"]); Assert.Equal("100", current.Character.Attributes["defFineRainPower"]);
        Assert.Equal(c.Stamp, LegacyFineRainEquipmentAdmission.ForOwner(current).StampMilliseconds);
        AssertOpaque(current);
    }

    private static byte[] VectorGolden(bool dead)
    {
        // Independent2176 framing for Peer1293601, type25, eighteen32-bit
        // words encoded as repeated uint64 varints. Dead is word0 bit5.
        var bytes = new byte[48];
        byte[] prefix = [48, 0, 128, 8, 8, 161, 250, 78, 18, 38, 8, 25];
        prefix.CopyTo(bytes, 0);
        for (int i = 0; i < 18; i++) bytes[12 + i * 2] = 24;
        if (dead) bytes[13] = 32;
        return bytes;
    }

    private static bool Animation(byte[] bytes, ushort spell) => LegacySpellAnimationPacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == spell;
    private static bool StatusVector(byte[] bytes, out IReadOnlyList<ulong> words)
    {
        words = [];
        if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) || packet.Uid != Peer ||
            packet.Updates.Count != 1 || packet.Updates[0].Type != LegacyMsgUpdateDataType.StatusFlag || packet.Updates[0].Values is not { } value) return false;
        words = value; return true;
    }
    private static bool Flag(IReadOnlyList<ulong> words, int flag) => words.Count > flag / 32 && (words[flag / 32] & (1UL << (flag % 32))) != 0;
}
