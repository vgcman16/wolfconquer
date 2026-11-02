using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937RelicAttributeConsumerTests
{
    [Fact]
    public void AbsentOptInLeavesExistingStartupCatalogUnchanged()
    {
        var world = World();
        Assert.True(WorldDataStartup.TryBindConfiguredChinese7937Attributes(world, out var reason, _ => null));
        Assert.Empty(reason);
        Assert.Null(world.Selected7937XuanbaoAttributes);
    }

    [Theory]
    [InlineData(null, "00")]
    [InlineData("C:/synthetic-only.lua", null)]
    [InlineData("", "")]
    [InlineData("C:/synthetic-only.lua", "not-a-pin")]
    public void IncompleteConfigurationNeverPublishesOrReadsAnImplicitFile(string? path, string? pin)
    {
        var world = World();
        Assert.False(WorldDataStartup.TryBindConfiguredChinese7937Attributes(world, out var reason,
            key => key == WorldDataStartup.Chinese7937XuanbaoInfoPathEnvironmentVariable ? path : pin));
        Assert.Equal("selected7937AttributePathAndFullPinRequired", reason);
        Assert.Null(world.Selected7937XuanbaoAttributes);
    }

    [Fact]
    public void ConfiguredPinnedPlainFileBindsTheActualWorldAndAChangedFileRefusesWithoutReplacingIt()
    {
        WithOwnedDirectory(root =>
        {
            var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module(goldRows: GoldRows(true)));
            var file = Path.Combine(root, "synthetic.lua");
            File.WriteAllBytes(file, bytes);
            var pin = Convert.ToHexString(SHA256.HashData(bytes));
            string? Read(string key) => key == WorldDataStartup.Chinese7937XuanbaoInfoPathEnvironmentVariable ? file : pin;
            var world = World();
            Assert.True(WorldDataStartup.TryBindConfiguredChinese7937Attributes(world, out var reason, Read), reason);
            var selected = world.Selected7937XuanbaoAttributes;
            Assert.NotNull(selected);
            Assert.Same(selected, LegacyBossSpecialDeathDropCatalog.FromWorldData(world).Selected7937Attributes);
            File.AppendAllText(file, "\n-- changed owned fixture\n");
            Assert.False(WorldDataStartup.TryBindConfiguredChinese7937Attributes(world, out reason, Read));
            Assert.Equal("selected7937AttributeFileRefused:PinMismatch", reason);
            Assert.Same(selected, world.Selected7937XuanbaoAttributes);
            Assert.False(File.ReadAllBytes(file).AsSpan().SequenceEqual(bytes));
        });
    }

    private static void WithOwnedDirectory(Action<string> action)
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        var root = Path.Combine(temp, "wolfconquer-7937-relic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Exception? primary = null;
        try { action(root); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try
            {
                var owned = Path.GetFullPath(root);
                if (!owned.StartsWith(Path.TrimEndingDirectorySeparator(temp) + Path.DirectorySeparatorChar,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    !Path.GetFileName(owned).StartsWith("wolfconquer-7937-relic-", StringComparison.Ordinal))
                    throw new IOException("Owned test directory boundary changed.");
                Directory.Delete(owned, recursive: true);
            }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    }
}
