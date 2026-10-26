using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerFullPromotionTransactionTests
{
    private static Dictionary<string, byte[]> Files(Fixture fixture)
    {
        var paths = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories);
        Assert.Contains(fixture.UidLease, paths);
        Assert.Equal(0L, new FileInfo(fixture.UidLease).Length);
        // The one actual FileShare.None process lease has no content. Check its
        // exact membership/length above; every other file is byte-verified.
        return paths.Where(path => !string.Equals(path, fixture.UidLease, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(fixture.Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertFiles(Fixture fixture, Dictionary<string, byte[]> expected)
    {
        var actual = Files(fixture);
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static void AssertUnchanged(Fixture fixture, WorldSession before, Dictionary<string, byte[]> files)
    {
        Assert.Same(before, fixture.Current);
        AssertFiles(fixture, files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.DoesNotContain("WorldServer applied LegacyNpc AccountManager full promotion", fixture.Log.ToString());
    }

    private static void AssertCommitted(Fixture fixture, WorldSession before, Dictionary<string, byte[]> files,
        ulong points, long promoted, byte charge)
    {
        var after = fixture.Current;
        Assert.NotSame(before, after);
        Assert.Equal(promoted, after.Character.Class);
        var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = points.ToString(CultureInfo.InvariantCulture), ["Class"] = promoted.ToString(CultureInfo.InvariantCulture),
            ["ChargePromotion"] = charge.ToString(CultureInfo.InvariantCulture),
        };
        Assert.Equal(fields.OrderBy(row => row.Key, StringComparer.Ordinal), after.Character.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal));
        var empty = new Dictionary<string, string>();
        Assert.Equal(before.Character with { Class = promoted, Attributes = empty }, after.Character with { Attributes = empty });
        Assert.Same(before.PlayerItems, after.PlayerItems);
        Assert.Same(before.PlayerSpells, after.PlayerSpells);
        Assert.Same(before.PlayerProficiencies, after.PlayerProficiencies);
        Assert.Same(before.CombatGear, after.CombatGear);
        var expected = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase)
        {
            [Path.GetRelativePath(fixture.Root, fixture.UserPath)] = ExpectedImage(before, files[Path.GetRelativePath(fixture.Root, fixture.UserPath)], points, promoted, charge),
        };
        AssertFiles(fixture, expected);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static byte[] ExpectedImage(WorldSession before, byte[] image, ulong points, long promoted, byte charge)
    {
        var text = Encoding.UTF8.GetString(image);
        foreach (var (key, value) in new[] { ("DonatePoints", points.ToString(CultureInfo.InvariantCulture)),
                     ("Class", promoted.ToString(CultureInfo.InvariantCulture)), ("ChargePromotion", charge.ToString(CultureInfo.InvariantCulture)) })
        {
            if (before.Character.Attributes.TryGetValue(key, out var old))
                text = text.Replace(key + "=" + old + "\r\n", key + "=" + value + "\r\n", StringComparison.Ordinal);
            else text = text.Replace("[Unrelated]", key + "=" + value + "\r\n[Unrelated]", StringComparison.Ordinal);
        }
        return Encoding.UTF8.GetBytes(text);
    }
}
