using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private static Dictionary<string, byte[]> TeleportLocationImage(AttackFixture f, TeleportBefore before)
    {
        var expected = before.Files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string key = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Actor));
        var text = CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
        foreach (var (field, oldValue, newValue) in new (string, long, long)[]
        {
            ("Map", 1000, 1002), ("X", 830, 410), ("Y", 584, 354),
            ("PMap", 1000, 1000), ("PMapX", 500, 830), ("PMapY", 650, 584),
        }) text = ReplaceTeleportLine(text, field, oldValue, newValue);
        expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        return expected;
    }

    private static string ReplaceTeleportLine(string text, string field, long before, long after)
    {
        string old = "\r\n" + field + "=" + before.ToString(CultureInfo.InvariantCulture) + "\r\n";
        Assert.Equal(1, text.Split(old, StringSplitOptions.None).Length - 1);
        return text.Replace(old, "\r\n" + field + "=" + after.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
    }

    private static void AssertTeleportCommitted(AttackFixture f, TeleportBefore before, Dictionary<string, byte[]> files,
        LegacyTeleportDestination destination, long targetHp)
    {
        f.AssertFiles(files); f.AssertCoherent(targetHp);
        var current = f.Current(); var native = before.ActorBinding.Player;
        Assert.Same(before.TargetSession, f.Current(Target));
        var fields = new Dictionary<string, string>(before.ActorSession.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        bool crossed = destination.MapId != before.ActorSession.Character.MapId;
        if (crossed) { fields["DynamicID"] = "0"; fields["PDinamycID"] = "0"; }
        var expected = before.ActorSession.Character with
        {
            MapId = destination.MapId, X = destination.X, Y = destination.Y,
            PreviousMapId = crossed ? 1000 : before.ActorSession.Character.PreviousMapId,
            PreviousMapX = crossed ? 830 : before.ActorSession.Character.PreviousMapX,
            PreviousMapY = crossed ? 584 : before.ActorSession.Character.PreviousMapY,
            Attributes = current.Character.Attributes, PaidNobility = current.Character.PaidNobility,
        };
        fields["Map"] = expected.MapId.ToString(CultureInfo.InvariantCulture);
        fields["X"] = expected.X.ToString(CultureInfo.InvariantCulture);
        fields["Y"] = expected.Y.ToString(CultureInfo.InvariantCulture);
        fields["PMap"] = expected.PreviousMapId.ToString(CultureInfo.InvariantCulture);
        fields["PMapX"] = expected.PreviousMapX.ToString(CultureInfo.InvariantCulture);
        fields["PMapY"] = expected.PreviousMapY.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(expected, current.Character);
        Assert.Equal(fields.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase),
            current.Character.Attributes.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(before.ActorSession.Character.PaidNobility, current.Character.PaidNobility);
        Assert.Equal(destination.MapId, (long)native.Map); Assert.Equal(destination.DynamicId, (ulong)native.DynamicID);
        Assert.Equal(destination.X, (long)native.X); Assert.Equal(destination.Y, (long)native.Y);
        Assert.Equal((ushort)before.ActorSession.Character.X, native.Px); Assert.Equal((ushort)before.ActorSession.Character.Y, native.Py);
        Assert.False(before.ActorBinding.Client.OnAutoAttack);
        Assert.Same(before.Descriptor, before.ActorBinding.Client.AutoAttack);
        Assert.Equal(before.DescriptorValues, ReadTeleportDescriptor(before.ActorBinding.Client.AutoAttack!));
        Assert.Equal(before.StampBits, Unsafe.BitCast<DateTime, long>(native.AttackStamp));
        Assert.True(f.Owner.Handler.TryGetBasicPhysicalRetirementSnapshot(before.ActorBinding, out var after));
        Assert.NotNull(after); Assert.NotSame(before.Native, after); Assert.False(after!.OnAutoAttack);
        Assert.Equal(before.Native.Request, after.Request); Assert.Equal(before.Native.AttackStamp, after.AttackStamp);
        AssertTeleportGraphsUnchanged(f, before);
    }

    private static void AssertTeleportGraphsUnchanged(AttackFixture f, TeleportBefore before)
    {
        foreach (var (uid, original, binding, items, spells, profs) in new[]
        {
            (Actor, before.ActorSession, before.ActorBinding, before.ActorItems, before.ActorSpells, before.ActorProfs),
            (Target, before.TargetSession, before.TargetBinding, before.TargetItems, before.TargetSpells, before.TargetProfs),
        })
        {
            var current = f.Current(uid);
            Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(items, binding.Items.Image);
            Assert.Same(spells, binding.Storage.ClientSpells); Assert.Same(profs, binding.Proficiencies.ClientProf);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(PlayerSpellSnapshotLoader.ToLegacyBytes(original.PlayerSpells), PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
            Assert.Equal(PlayerProficiencySnapshotLoader.ToLegacyBytes(original.PlayerProficiencies),
                PlayerProficiencySnapshotLoader.ToLegacyBytes(current.PlayerProficiencies));
            Assert.Equal(original with { Character = current.Character, PlayerItems = current.PlayerItems,
                PlayerSpells = current.PlayerSpells, PlayerProficiencies = current.PlayerProficiencies }, current);
        }
        Assert.False(f.Owner.Recovery.IsBlocked);
    }

    private static byte[] TeleportActionGolden(uint map, uint x, uint y, ushort type, uint parameter = 0, bool mapParameter = false)
    {
        var fields = new List<(int Field, ulong Value)> { (1, Actor) };
        if (parameter != 0) fields.Add((3, parameter));
        if (mapParameter) fields.Add((5, map));
        fields.AddRange(new (int, ulong)[] { (9, TeleportTimestamp), (12, type), (13, 0), (14, x), (15, y) });
        return Frame(2046, fields.ToArray());
    }

    private static void AssertTeleportCoreWire(AttackFixture f, uint map, uint x, uint y, bool entireOwnerOutput)
    {
        Assert.NotEmpty(f.Order); Assert.Equal(Target, f.Order[0].Recipient);
        Assert.Equal(Frame(2046, [(1, Actor), (9, 0), (12, 135), (13, 0), (14, 0), (15, 0)]), f.Order[0].Bytes);
        var expected = new[]
        {
            TeleportActionGolden(map, x, y, 86, map, mapParameter: true),
            TeleportActionGolden(map, x, y, 157, 2, mapParameter: true),
            TeleportActionGolden(map, x, y, 114, map, mapParameter: true),
            TeleportActionGolden(map, x, y, 104), Frame(2213, [(1, map), (2, map), (3, 0)]),
        };
        var actual = f.Owner.Frames(Actor);
        Assert.True(actual.Count >= expected.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
        if (entireOwnerOutput) { Assert.Equal(expected.Length, actual.Count); Assert.Single(f.Owner.Frames(Target)); }
    }
}
