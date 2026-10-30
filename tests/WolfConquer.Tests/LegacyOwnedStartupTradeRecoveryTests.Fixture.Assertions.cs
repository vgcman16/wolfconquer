using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupTradeRecoveryFixture
{
    internal LegacyOwnedAdmissionRuntime Open()
    {
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.True(success, reason);
        var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        Assert.True(runtime.Ordinary.IsReconciliationComplete);
        Assert.True(runtime.Recovery.IsReadyForOrdinaryAdmission(Data.Options().Roots));
        return runtime;
    }

    internal void AssertRecoveredImages()
    {
        Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        foreach (var (path, bytes) in After) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor,
            JsonNode.Parse(RegistryState)!["HighWater"]!.GetValue<uint>());
        var expected = ExpectedWallets();
        AssertWallet(LeftUid, expected.LeftMoney, expected.LeftCp, hasClaim: false);
        AssertWallet(RightUid, expected.RightMoney, expected.RightCp, hasClaim: false);
        var left = new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(LeftUid);
        var right = new PlayerItemSnapshotLoader(Data.Items).LoadByCharacterUid(RightUid);
        uint[] expectedLeft = !complete ? [LeftItemUid] : reciprocalItems ? [RightItemUid] : [];
        uint[] expectedRight = !complete ? [RightItemUid] : reciprocalItems ? [LeftItemUid] : [LeftItemUid, RightItemUid];
        Assert.Equal(expectedLeft, left.InventoryItems.Select(item => item.Uid).Order().ToArray());
        Assert.Equal(expectedRight, right.InventoryItems.Select(item => item.Uid).Order().ToArray());
        foreach (var row in left.WarehouseItems.Concat(right.WarehouseItems))
        { Assert.Equal((byte)1, row.Bound); Assert.Equal((byte)1, row.Locked); }
        uint[] expectedUids = lockedMode is "deposit" or "root-and-deposit"
            ? [LeftItemUid, RightItemUid, LeftItemUid + 100, RightItemUid + 100, LeftItemUid + 200]
            : [LeftItemUid, RightItemUid, LeftItemUid + 100, RightItemUid + 100];
        Assert.Equal(expectedUids, new[] { ItemsPath(LeftUid), ItemsPath(RightUid) }.SelectMany(path =>
            LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(path))).Order().ToArray());
    }

    internal void AssertRefused(LegacyOwnedAdmissionOptions? options = null, bool leaseAvailable = true)
    {
        var before = Data.Files();
        var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, options ?? Data.Options(), out var opened, out var reason);
        if (opened is not null) runtimes.Add(opened);
        Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
        Data.AssertFiles(before);
        Assert.True(LegacyTradeJournal.HasPending(Data.Users));
        if (leaseAvailable) AssertLeaseAvailable();
    }

    internal void AssertLeaseAvailable()
    {
        using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(0L, lease.Length);
    }

    private Dictionary<string, byte[]> TargetFiles() => new[] { LeftUid, RightUid }
        .SelectMany(uid => new[] { CharacterPath(uid), ItemsPath(uid) })
        .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    private (long LeftMoney, long LeftCp, long RightMoney, long RightCp) ExpectedWallets() =>
        !complete || currency == "none" ? (777, 222, 2_000, 60) :
        currency == "left-only" ? (477, 217, 2_300, 65) : (1_177, 227, 1_600, 55);
    private void AssertWallet(uint uid, long money, long cp, bool hasClaim)
    {
        var image = File.ReadAllBytes(CharacterPath(uid));
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        Assert.Equal(money, saved.Money); Assert.Equal(cp, saved.ConquerPoints);
        Assert.Equal(hasClaim, ReadClaim(uid) is not null);
    }
    private LegacyTradeEscrowClaim? ReadClaim(uint uid)
    {
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath(uid)), out _));
        Assert.True(LegacyTradeEscrowClaims.TryRead(saved.Attributes, out var claim, out var reason), reason);
        return claim;
    }
    private static void AssertWalletImage(uint uid, byte[] image, long money, long cp)
    {
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        Assert.Equal(uid, saved.Uid); Assert.Equal(money, saved.Money); Assert.Equal(cp, saved.ConquerPoints);
        Assert.True(LegacyTradeEscrowClaims.TryRead(saved.Attributes, out var claim, out var reason), reason);
        Assert.Null(claim);
    }
}
