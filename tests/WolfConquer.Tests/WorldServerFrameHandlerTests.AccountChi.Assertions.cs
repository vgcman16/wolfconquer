using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static Dictionary<string, byte[]> ChiFiles(ChiPurchaseFixture fixture)
    {
        var paths = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories);
        Assert.Contains(fixture.UidLease, paths);
        Assert.Equal(0L, new FileInfo(fixture.UidLease).Length);
        return paths.Where(path => !string.Equals(path, fixture.UidLease, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(path => Path.GetRelativePath(fixture.Root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertChiFiles(ChiPurchaseFixture fixture, Dictionary<string, byte[]> expected)
    {
        var actual = ChiFiles(fixture);
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static void AssertChiUnchanged(ChiPurchaseFixture fixture, WorldSession before, Dictionary<string, byte[]> files)
    {
        Assert.Same(before, fixture.Current);
        AssertChiFiles(fixture, files);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.Empty(fixture.Sent);
    }

    private static void AssertChiCommitted(ChiPurchaseFixture fixture, WorldSession before,
        Dictionary<string, byte[]> files, ulong donation)
    {
        var after = fixture.Current;
        Assert.NotSame(before, after);
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = donation.ToString(CultureInfo.InvariantCulture), ["ChargeChi"] = "1",
        };
        Assert.Equal(attributes.OrderBy(row => row.Key, StringComparer.Ordinal), after.Character.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal));
        var empty = new Dictionary<string, string>();
        Assert.Equal(before.Character with { Attributes = empty }, after.Character with { Attributes = empty });
        Assert.Equal(before with { Character = after.Character }, after);
        Assert.Same(before.PlayerItems, after.PlayerItems);
        Assert.Same(before.PlayerSpells, after.PlayerSpells);
        Assert.Same(before.PlayerProficiencies, after.PlayerProficiencies);
        Assert.Same(before.CombatGear, after.CombatGear);
        var relative = Path.GetRelativePath(fixture.Root, fixture.UserPath);
        var image = Encoding.UTF8.GetString(files[relative]);
        var oldDonation = "DonatePoints=" + before.Character.Attributes["DonatePoints"] + "\r\n";
        Assert.Equal(1, image.Split(oldDonation, StringSplitOptions.None).Length - 1);
        image = image.Replace(oldDonation, "DonatePoints=" + donation.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        if (before.Character.Attributes.TryGetValue("ChargeChi", out var flag))
        {
            var oldFlag = "ChargeChi=" + flag + "\r\n";
            Assert.Equal(1, image.Split(oldFlag, StringSplitOptions.None).Length - 1);
            image = image.Replace(oldFlag, "ChargeChi=1\r\n", StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(1, image.Split("[PaidNobility]", StringSplitOptions.None).Length - 1);
            image = image.Replace("[PaidNobility]", "ChargeChi=1\r\n[PaidNobility]", StringComparison.Ordinal);
        }
        var expected = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase) { [relative] = Encoding.UTF8.GetBytes(image) };
        AssertChiFiles(fixture, expected);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
        Assert.Empty(fixture.Sent); // Source has no successful purchase frame.
    }

    private static void ChiSetOwnedField(ChiPurchaseFixture fixture, string key, string raw)
    {
        var before = fixture.Current;
        var text = File.ReadAllText(fixture.UserPath);
        var old = key + "=" + before.Character.Attributes[key] + "\r\n";
        Assert.Equal(1, text.Split(old, StringSplitOptions.None).Length - 1);
        File.WriteAllText(fixture.UserPath, text.Replace(old, key + "=" + raw + "\r\n", StringComparison.Ordinal), new UTF8Encoding(true));
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { [key] = raw };
        Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, before, before with
        { Character = before.Character with { Attributes = attributes } }));
        Assert.Equal(raw, fixture.Current.Character.Attributes[key]);
        Assert.Contains(key + "=" + raw + "\r\n", File.ReadAllText(fixture.UserPath), StringComparison.Ordinal);
    }

    private static int ChiAppliedCount(ChiPurchaseFixture fixture) => fixture.Log.ToString().Split('\n').Count(line =>
        line.StartsWith("WorldServer applied LegacyNpc AccountManager attribute flag purchase", StringComparison.Ordinal));
}
