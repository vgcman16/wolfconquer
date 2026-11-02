using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoRuntimeRulesTests
{
    [Fact]
    public void NoConfiguredPackedRulesPreserveExistingServerMetadata()
    {
        var world = World();
        Assert.True(WorldDataStartup.TryBindConfiguredChinese7937Rules(world, out var reason, _ => null));
        Assert.Empty(reason);
        Assert.Null(world.Selected7937XuanbaoRules);
        Assert.Same(world.XuanbaoRandomAttributesByItemTypeId, world.EffectiveXuanbaoRandomAttributesByItemTypeId);
    }

    [Theory]
    [InlineData(WorldDataStartup.Chinese7937ComposePathEnvironmentVariable)]
    [InlineData(WorldDataStartup.Chinese7937ComposeSha256EnvironmentVariable)]
    [InlineData(WorldDataStartup.Chinese7937RandomPathEnvironmentVariable)]
    [InlineData(WorldDataStartup.Chinese7937RandomSha256EnvironmentVariable)]
    public void APartialSettingCannotSelectAnImplicitFileOrSeedFallback(string loneSetting)
    {
        var world = World();
        Assert.False(WorldDataStartup.TryBindConfiguredChinese7937Rules(world, out var reason,
            name => name == loneSetting ? "incomplete" : null));
        Assert.Equal("selected7937ComposeAndRandomPathsAndPinsRequired", reason);
        Assert.Null(world.Selected7937XuanbaoRules);
    }

    [Fact]
    public void ConfiguredPinnedPackedFilesBindOneRealRuntimeAndRefuseChangedInput()
    {
        WithRuleDirectory(root =>
        {
            var world = World();
            var packed = Packed(world);
            var compose = Path.Combine(root, "compose.dat");
            var random = Path.Combine(root, "random.dat");
            File.WriteAllBytes(compose, packed.Compose); File.WriteAllBytes(random, packed.Random);
            var settings = new Dictionary<string, string>
            {
                [WorldDataStartup.Chinese7937ComposePathEnvironmentVariable] = compose,
                [WorldDataStartup.Chinese7937ComposeSha256EnvironmentVariable] = Convert.ToHexString(SHA256.HashData(packed.Compose)),
                [WorldDataStartup.Chinese7937RandomPathEnvironmentVariable] = random,
                [WorldDataStartup.Chinese7937RandomSha256EnvironmentVariable] = Convert.ToHexString(SHA256.HashData(packed.Random)),
            };
            string? Read(string name) => settings.GetValueOrDefault(name);
            Assert.True(WorldDataStartup.TryBindConfiguredChinese7937Rules(world, out var reason, Read), reason);
            var retained = world.Selected7937XuanbaoRules;
            Assert.NotNull(retained);
            using (var changed = new FileStream(random, FileMode.Append, FileAccess.Write, FileShare.None)) changed.WriteByte(1);
            Assert.False(WorldDataStartup.TryBindConfiguredChinese7937Rules(world, out reason, Read));
            Assert.Equal("selected7937PackedRuleFilesRefused:PinMismatch", reason);
            Assert.Same(retained, world.Selected7937XuanbaoRules);
            Assert.Equal(packed.Compose, File.ReadAllBytes(compose));
        });
    }

    private static void WithRuleDirectory(Action<string> action)
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        var root = Path.Combine(temp, "wolfconquer-7937-rules-runtime-" + Guid.NewGuid().ToString("N"));
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
                    !Path.GetFileName(owned).StartsWith("wolfconquer-7937-rules-runtime-", StringComparison.Ordinal) ||
                    (File.GetAttributes(owned) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Owned rule-runtime fixture boundary changed.");
                Directory.Delete(owned, recursive: true);
            }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    }
}
