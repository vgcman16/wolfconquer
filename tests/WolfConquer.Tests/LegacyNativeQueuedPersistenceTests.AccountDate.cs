using System.Globalization;
using System.Text.RegularExpressions;
using WolfConquer.Core.World;
using Leaves = WolfConquer.Core.Data.LegacyNativeServerDatabasePersistence;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQueuedPersistenceTests
{
    [WindowsNativeIniFact]
    public void AllowCreateMissingFileReturnsTrueWithoutCreatingAnything()
    {
        using var files = new Files();
        string path = files.User(17);
        Assert.True(Leaves.AllowCreate(17));
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFileSystemEntries(files.Users));
    }

    [WindowsNativeIniTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AllowCreateRejectsAnyExistingFileRegardlessOfContents(bool empty)
    {
        using var files = new Files();
        string path = files.User(uint.MaxValue);
        File.WriteAllBytes(path, empty ? [] : [0x00, 0xFF, 0x71]);
        byte[] original = File.ReadAllBytes(path);
        Assert.False(Leaves.AllowCreate(uint.MaxValue));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [WindowsNativeIniFact]
    public void AllowCreateDirectoryWithIniNameRetainsFileExistsFalseSemantics()
    {
        using var files = new Files();
        string path = files.User(0);
        files.CreateDirectory(path);
        Assert.True(Leaves.AllowCreate(0));
        Assert.True(Directory.Exists(path));
        Assert.Empty(Directory.GetFileSystemEntries(path));
    }

    [WindowsNativeIniFact]
    public void AllowCreateReadsCurrentRawDatabaseLocationRatherThanCachedIniState()
    {
        using var first = new Files();
        first.Seed(first.User(17));
        Assert.False(Leaves.AllowCreate(17));
        using (var second = new Files())
        {
            Assert.True(Leaves.AllowCreate(17));
            second.Seed(second.User(17));
            Assert.False(Leaves.AllowCreate(17));
        }
        Assert.Equal(first.Root, LegacyNativeServerConfig.DbLocation);
        Assert.False(Leaves.AllowCreate(17));
        Assert.Equal(UserRows, File.ReadAllText(first.User(17)));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-SA")]
    public void GenerateDateUsesOneLocalGregorianDateWithExactUnpaddedNativeSeparators(string culture)
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        CultureInfo priorUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            DateTime earliest = DateTime.Now;
            string value = Leaves.GenerateDate();
            DateTime latest = DateTime.Now;
            Match match = Regex.Match(value, "^([0-9]+)and([0-9]+)and([0-9]+) and ([0-9]+) and ([0-9]+) and ([0-9]+)$");
            Assert.True(match.Success, value);
            int[] parts = match.Groups.Cast<Group>().Skip(1)
                .Select(group => int.Parse(group.Value, CultureInfo.InvariantCulture)).ToArray();
            var observed = new DateTime(parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
            Assert.InRange(observed, earliest.AddTicks(-(earliest.Ticks % TimeSpan.TicksPerSecond)), latest);
            Assert.Equal(parts[0] + "and" + parts[1] + "and" + parts[2] + " and " +
                parts[3] + " and " + parts[4] + " and " + parts[5], value);
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
            CultureInfo.CurrentUICulture = priorUi;
        }
    }
}
