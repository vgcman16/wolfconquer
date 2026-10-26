using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    private static Dictionary<string, byte[]> Files(Fixture fixture)
    {
        var paths = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories);
        Assert.Contains(fixture.UidLease, paths);
        Assert.Equal(0L, new FileInfo(fixture.UidLease).Length);
        // Only the actual unreadable process lease is checked by membership/size.
        // Every readable Character, snapshot, journal and registry byte is kept.
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
    }

    private static void AssertNoApplied(Fixture fixture) => Assert.DoesNotContain(
        "WorldServer applied LegacyNpc AccountManager conquer point purchase", fixture.Log.ToString());

    private static void AssertCommitted(Fixture fixture, WorldSession before, Dictionary<string, byte[]> files,
        ulong donation, uint points, long? bank = null)
    {
        var after = fixture.Current;
        Assert.NotSame(before, after);
        Assert.Equal((long)points, after.Character.ConquerPoints);
        var expectedBank = bank ?? before.Character.BankConquerPoints;
        Assert.Equal(expectedBank, after.Character.BankConquerPoints);
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = donation.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = points.ToString(CultureInfo.InvariantCulture),
        };
        if (expectedBank != before.Character.BankConquerPoints)
            attributes["BankCps"] = expectedBank.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(attributes.OrderBy(row => row.Key, StringComparer.Ordinal),
            after.Character.Attributes.OrderBy(row => row.Key, StringComparer.Ordinal));
        var empty = new Dictionary<string, string>();
        Assert.Equal(before.Character with { ConquerPoints = points, BankConquerPoints = expectedBank, Attributes = empty },
            after.Character with { Attributes = empty });
        Assert.Equal(before with { Character = after.Character }, after);
        Assert.Same(before.PlayerItems, after.PlayerItems);
        Assert.Same(before.PlayerSpells, after.PlayerSpells);
        Assert.Same(before.PlayerProficiencies, after.PlayerProficiencies);
        Assert.Same(before.CombatGear, after.CombatGear);
        var path = Path.GetRelativePath(fixture.Root, fixture.UserPath);
        var expected = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase)
        {
            [path] = ExpectedImage(before, files[path], donation, points, expectedBank),
        };
        AssertFiles(fixture, expected);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static byte[] ExpectedImage(WorldSession before, byte[] image, ulong donation, uint points, long bank)
    {
        var text = Encoding.UTF8.GetString(image);
        var values = new List<(string Key, string Value)> { ("DonatePoints", donation.ToString(CultureInfo.InvariantCulture)),
            ("ConquerPoints", points.ToString(CultureInfo.InvariantCulture)) };
        if (bank != before.Character.BankConquerPoints) values.Add(("BankCps", bank.ToString(CultureInfo.InvariantCulture)));
        foreach (var (key, value) in values)
        {
            if (before.Character.Attributes.TryGetValue(key, out var previous))
            {
                var old = key + "=" + previous + "\r\n";
                Assert.Equal(1, text.Split(old, StringSplitOptions.None).Length - 1);
                text = text.Replace(old, key + "=" + value + "\r\n", StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(1, text.Split("[Unrelated]", StringSplitOptions.None).Length - 1);
                text = text.Replace("[Unrelated]", key + "=" + value + "\r\n[Unrelated]", StringComparison.Ordinal);
            }
        }
        return Encoding.UTF8.GetBytes(text);
    }

    private static void AssertWalletFrame(byte[] frame, uint points)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update));
        Assert.Equal(Uid, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, entry.Type);
        Assert.Equal((ulong)points, entry.Value);
        Assert.Equal(0u, entry.Flag);
        Assert.Equal(0u, entry.Time);
        Assert.Equal(0u, entry.Damage);
        Assert.Equal(0u, entry.SpellLevel);
        Assert.Null(entry.Values);
    }

    private static void RewriteOwnedField(Fixture fixture, string key, string raw)
    {
        var before = fixture.Current;
        var old = key + "=" + before.Character.Attributes[key] + "\r\n";
        var text = File.ReadAllText(fixture.UserPath);
        Assert.Equal(1, text.Split(old, StringSplitOptions.None).Length - 1);
        File.WriteAllText(fixture.UserPath, text.Replace(old, key + "=" + raw + "\r\n", StringComparison.Ordinal), new UTF8Encoding(true));
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { [key] = raw };
        var points = before.Character.ConquerPoints;
        if (key == "ConquerPoints" && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) points = parsed;
        var bank = before.Character.BankConquerPoints;
        if (key == "BankCps" && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBank)) bank = parsedBank;
        var after = before with { Character = before.Character with { ConquerPoints = points, BankConquerPoints = bank, Attributes = attributes } };
        Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, before, after));
        // This changes a real already-admitted holder's owned test state. It
        // supplies no replacement identity, acceptance callback or saver.
    }

    private static void RemoveOwnedWalletField(Fixture fixture, string key)
    {
        var before = fixture.Current;
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        Assert.True(attributes.Remove(key));
        var after = before with { Character = before.Character with
        {
            ConquerPoints = key == "ConquerPoints" ? 0 : before.Character.ConquerPoints,
            BankConquerPoints = key == "BankCps" ? 0 : before.Character.BankConquerPoints,
            Attributes = attributes,
        } };
        Assert.True(fixture.Sessions.TryCompareAndSet(fixture.Connection, before, after));
        // Real CAS can legitimately scalar-autosave the changed typed wallet.
        // Remove its actual resulting row afterwards; no saver is bypassed.
        var value = key == "ConquerPoints" ? after.Character.ConquerPoints : after.Character.BankConquerPoints;
        var line = key + "=" + value.ToString(CultureInfo.InvariantCulture) + "\r\n";
        var text = File.ReadAllText(fixture.UserPath);
        Assert.Equal(1, text.Split(line, StringSplitOptions.None).Length - 1);
        text = text.Replace(line, string.Empty, StringComparison.Ordinal);
        File.WriteAllText(fixture.UserPath, text, new UTF8Encoding(true));
        Assert.False(text.Split("\r\n", StringSplitOptions.None).Any(line => line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)));
        Assert.False(fixture.Current.Character.Attributes.ContainsKey(key));
    }

    private static void AssertPurchaseFrames(Fixture fixture, uint points, uint spill)
    {
        var frames = fixture.Sent.ToArray();
        Assert.Equal(spill == 0 ? 1 : 2, frames.Length);
        if (spill != 0)
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frames[0]), out var box));
            Assert.Equal(LegacyNpcReplyInteractType.MessageBox, box.InteractType);
            Assert.Equal(spill.ToString(CultureInfo.InvariantCulture) +
                " Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.", box.Text);
            Assert.Equal(ushort.MaxValue, box.InputMaxLength);
            Assert.Equal((byte)0, box.OptionId);
        }
        AssertWalletFrame(frames[^1], points);
    }
}
