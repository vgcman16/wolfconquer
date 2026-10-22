using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Cache = WolfConquer.Core.World.LegacyNativeBeastsAtrribute;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class WorldDataLoaderTests
{
    [Fact]
    public async Task StartupAdmitsOrderedBeastRowsIntoNativeCacheBeforeReturningCatalog()
    {
        using var lease = new StartupBeastLease(_rootPath);
        WriteStartupBeastRows(StartupBeastRow(3), StartupBeastRow(1, int.MaxValue, 255),
            StartupBeastRow(255, 0, 0));
        using var log = new StringWriter();

        var catalog = Assert.IsType<WorldDataCatalog>(await WorldDataStartup.TryLoadAsync(log));

        Assert.Equal(new[] { 3, 1, 255 }, catalog.BeastAttributes.Select(row => row.Level));
        Assert.Equal(new byte[] { 3, 1, 255 }, Cache.Attributes.Keys);
        AssertStartupNativeRow(Cache.Attributes[3], 3, 101, 4);
        AssertStartupNativeRow(Cache.Attributes[1], 1, int.MaxValue, 255);
        AssertStartupNativeRow(Cache.Attributes[255], 255, 0, 0);
        Assert.True(log.ToString().Contains("World data loaded:", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupDuplicateLaterBeastRowPreservesExistingReferencesAndAcceptedPrefix()
    {
        using var lease = new StartupBeastLease(_rootPath);
        var existing = new Cache.Attribute { Level = 9, RequiredPoints = 901 };
        Cache.Attributes.Add(9, existing);
        WriteStartupBeastRows(StartupBeastRow(3), StartupBeastRow(1),
            StartupBeastRow(3, 999, 9), StartupBeastRow(2));
        using var log = new StringWriter();

        await Assert.ThrowsAsync<ArgumentException>(() => WorldDataStartup.TryLoadAsync(log));

        Assert.Equal(new byte[] { 9, 3, 1 }, Cache.Attributes.Keys);
        Assert.Same(existing, Cache.Attributes[9]);
        AssertStartupNativeRow(Cache.Attributes[3], 3, 101, 4);
        AssertStartupNativeRow(Cache.Attributes[1], 1, 101, 4);
        Assert.False(Cache.Attributes.ContainsKey(2));
        Assert.False(log.ToString().Contains("World data loaded:", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupRepeatedLoadPreservesOldNativeRowAndAdmitsOnlyNewPrefixBeforeDuplicate()
    {
        using var lease = new StartupBeastLease(_rootPath);
        WriteStartupBeastRows(StartupBeastRow(2));
        using var firstLog = new StringWriter();
        Assert.NotNull(await WorldDataStartup.TryLoadAsync(firstLog));
        Assert.Equal(new byte[] { 2 }, Cache.Attributes.Keys);
        var originalRow = Cache.Attributes[2];
        WriteStartupBeastRows(StartupBeastRow(3), StartupBeastRow(2, 999, 9), StartupBeastRow(1));
        using var secondLog = new StringWriter();

        await Assert.ThrowsAsync<ArgumentException>(() => WorldDataStartup.TryLoadAsync(secondLog));

        Assert.Equal(new byte[] { 2, 3 }, Cache.Attributes.Keys);
        Assert.Same(originalRow, Cache.Attributes[2]);
        AssertStartupNativeRow(originalRow, 2, 101, 4);
        AssertStartupNativeRow(Cache.Attributes[3], 3, 101, 4);
        Assert.False(Cache.Attributes.ContainsKey(1));
        Assert.False(secondLog.ToString().Contains("World data loaded:", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupRejectsBeastLevelMismatchAfterAlreadyAdmittedPrefix()
    {
        await AssertStartupBeastFailureAfterPrefixAsync<FormatException>(
            StartupBeastRow(2) with { Level = 6 });
    }

    [Fact]
    public async Task StartupRejectsUnusedBeastParameterMismatchAfterAlreadyAdmittedPrefix()
    {
        var row = StartupBeastRow(2);
        var parameters = row.Parameters.ToArray();
        parameters[16]++;
        await AssertStartupBeastFailureAfterPrefixAsync<FormatException>(row with { Parameters = parameters });
    }

    [Fact]
    public async Task StartupRejectsUnusedBeastFieldSpellingMismatchAfterAlreadyAdmittedPrefix()
    {
        var row = StartupBeastRow(2);
        var fields = row.Fields.ToArray();
        fields[17] = "0117";
        await AssertStartupBeastFailureAfterPrefixAsync<FormatException>(row with { Fields = fields });
    }

    [Fact]
    public async Task StartupRejectsBeastRawLineMismatchAfterAlreadyAdmittedPrefix()
    {
        var row = StartupBeastRow(2);
        var fields = row.Fields.ToArray();
        fields[1] = "999";
        await AssertStartupBeastFailureAfterPrefixAsync<FormatException>(
            row with { RawLine = string.Join("@@", fields) });
    }

    [Fact]
    public async Task StartupRejectsNativeBeastLevelByteOverflowWithoutPublishingFaultedRow()
    {
        await AssertStartupBeastFailureAfterPrefixAsync<OverflowException>(StartupBeastRow(256));
    }

    [Fact]
    public async Task StartupRejectsNativeBeastSlotByteOverflowWithoutPublishingFaultedRow()
    {
        await AssertStartupBeastFailureAfterPrefixAsync<OverflowException>(StartupBeastRow(2, slots: 256));
    }

    [Fact]
    public async Task StartupRejectsNativeBeastUnsignedNegativeWithoutPublishingFaultedRow()
    {
        await AssertStartupBeastFailureAfterPrefixAsync<OverflowException>(StartupBeastRow(2, required: -1));
    }

    [Fact]
    public async Task StartupRejectsUnsignedValueOutsideTypedJsonIntDomainWithoutPublishingFaultedRow()
    {
        var row = StartupBeastRow(2);
        var fields = row.Fields.ToArray();
        fields[1] = "2147483648";
        await AssertStartupBeastFailureAfterPrefixAsync<FormatException>(
            row with { RawLine = string.Join("@@", fields), Fields = fields });
    }

    [Fact]
    public async Task StartupMissingManifestLeavesNativeCacheReferencesAndDbLocationUnchanged()
    {
        using var lease = new StartupBeastLease(_rootPath);
        var existing = new Cache.Attribute { Level = 9, RequiredPoints = 901 };
        Cache.Attributes.Add(9, existing);
        WriteStartupBeastRows(StartupBeastRow(2));
        File.Delete(Path.Combine(_rootPath, "manifest.json"));
        using var log = new StringWriter();

        Assert.Null(await WorldDataStartup.TryLoadAsync(log));

        Assert.Same(existing, Assert.Single(Cache.Attributes).Value);
        Assert.True(log.ToString().Contains("manifest.json is missing", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupMissingRequiredJsonKeepsExistingLoadNullAndNativeCacheUntouched()
    {
        using var lease = new StartupBeastLease(_rootPath);
        var existing = new Cache.Attribute { Level = 9, RequiredPoints = 901 };
        Cache.Attributes.Add(9, existing);
        WriteStartupBeastRows(StartupBeastRow(2));
        File.Delete(Path.Combine(_rootPath, "items.json"));
        using var log = new StringWriter();

        Assert.Null(await WorldDataStartup.TryLoadAsync(log));

        Assert.Same(existing, Assert.Single(Cache.Attributes).Value);
        Assert.False(log.ToString().Contains("World data loaded:", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupEmptyBeastCatalogDoesNotClearOrReplaceExistingNativeCache()
    {
        using var lease = new StartupBeastLease(_rootPath);
        var existing = new Cache.Attribute { Level = 9, RequiredPoints = 901 };
        Cache.Attributes.Add(9, existing);
        WriteStartupBeastRows();
        using var log = new StringWriter();

        var catalog = Assert.IsType<WorldDataCatalog>(await WorldDataStartup.TryLoadAsync(log));

        Assert.Empty(catalog.BeastAttributes);
        Assert.Same(existing, Assert.Single(Cache.Attributes).Value);
        AssertStartupLeaseUnchanged(lease);
    }

    [Fact]
    public async Task StartupBeastBridgeUsesCallingCurrentCultureForBothTypedAndNativeParsers()
    {
        using var lease = new StartupBeastLease(_rootPath);
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = custom;
        var row = StartupBeastRow(2);
        var fields = row.Fields.Select(value => value.Length == 0 ? value : "p" + value).ToArray();
        WriteStartupBeastRows(row with { RawLine = string.Join("@@", fields), Fields = fields });
        using var log = new StringWriter();

        var catalog = Assert.IsType<WorldDataCatalog>(await WorldDataStartup.TryLoadAsync(log));

        Assert.Equal("p2", Assert.Single(catalog.BeastAttributes).Fields[0]);
        AssertStartupNativeRow(Assert.Single(Cache.Attributes).Value, 2, 101, 4);
        AssertStartupLeaseUnchanged(lease);
    }

    private void WriteStartupBeastRows(params BeastAttribute[] rows)
    {
        WriteFixture(countOverrides: new Dictionary<string, int> { ["beastAttributes"] = rows.Length });
        WriteJson("beastAttributes.json", rows);
    }

    private static BeastAttribute StartupBeastRow(int level, int required = 101, int slots = 4)
    {
        var parameters = Enumerable.Range(101, 17).ToArray();
        parameters[0] = required;
        parameters[15] = slots;
        string[] fields =
        [
            level.ToString(CultureInfo.CurrentCulture),
            .. parameters.Select(value => value.ToString(CultureInfo.CurrentCulture)),
            "",
        ];
        return new BeastAttribute(level, parameters, "owned-beasts_attr.txt", 1,
            string.Join("@@", fields), fields);
    }

    private async Task AssertStartupBeastFailureAfterPrefixAsync<TException>(BeastAttribute faulted)
        where TException : Exception
    {
        using var lease = new StartupBeastLease(_rootPath);
        var existing = new Cache.Attribute { Level = 9, RequiredPoints = 901 };
        Cache.Attributes.Add(9, existing);
        WriteStartupBeastRows(StartupBeastRow(3), faulted, StartupBeastRow(4));
        using var log = new StringWriter();

        await Assert.ThrowsAsync<TException>(() => WorldDataStartup.TryLoadAsync(log));

        Assert.Equal(new byte[] { 9, 3 }, Cache.Attributes.Keys);
        Assert.Same(existing, Cache.Attributes[9]);
        AssertStartupNativeRow(Cache.Attributes[3], 3, 101, 4);
        Assert.False(Cache.Attributes.ContainsKey(4));
        Assert.False(log.ToString().Contains("World data loaded:", StringComparison.Ordinal));
        AssertStartupLeaseUnchanged(lease);
    }

    private static void AssertStartupNativeRow(Cache.Attribute row, byte level, uint required, byte slots)
    {
        Assert.Equal(level, row.Level);
        Assert.Equal(required, row.RequiredPoints);
        Assert.Equal(new uint[] { 102, 103, 104, 105, 106, 107, 108, 109, 110 },
            new uint[] { row.HPAdd, row.PAttack, row.MAttack, row.PDefense, row.MDefense,
                row.FinalPDmgDealt, row.FinalMDmgDealt, row.FinalPDmgReceived, row.FinalMDmgReceived });
        Assert.Equal(slots, row.YellowRuneSlots);
    }

    private static void AssertStartupLeaseUnchanged(StartupBeastLease lease)
    {
        Assert.Same(lease.Declared, Cache.Attributes);
        Assert.Equal(lease.NativeDbLocation, LegacyNativeServerConfig.DbLocation);
    }

    // Explicit test-only cache/environment/culture substitution, restored even on a failed assertion.
    private sealed class StartupBeastLease : IDisposable
    {
        private readonly Dictionary<byte, Cache.Attribute> _previousCache = Cache.Attributes;
        private readonly string? _previousSeedRoot =
            Environment.GetEnvironmentVariable(WorldDataOptions.SeedJsonEnvironmentVariable);
        private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

        public Dictionary<byte, Cache.Attribute> Declared { get; } = new();
        public string NativeDbLocation { get; } = LegacyNativeServerConfig.DbLocation;

        public StartupBeastLease(string ownedRoot)
        {
            Cache.Attributes = Declared;
            try
            {
                Environment.SetEnvironmentVariable(WorldDataOptions.SeedJsonEnvironmentVariable, ownedRoot);
            }
            catch
            {
                Cache.Attributes = _previousCache;
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                Cache.Attributes = _previousCache;
            }
            finally
            {
                try
                {
                    Environment.SetEnvironmentVariable(WorldDataOptions.SeedJsonEnvironmentVariable, _previousSeedRoot);
                }
                finally
                {
                    CultureInfo.CurrentCulture = _previousCulture;
                }
            }
        }
    }
}
