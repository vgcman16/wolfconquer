using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Uses genuine saved/native/Frame issuer, the real shared registry and its
// exclusive file lease. JSON mutation below is negative corruption only.
public sealed partial class LegacyOwnedRelicFuseTests
{
    [Fact]
    public Task RealLookalikeOldReceiptsCannotBecomeRelicAuthorityAndSurviveOwnedV7Reopen() => Run(async f =>
    {
        await f.CloseAsync();
        var lookalikes = new List<(LegacyItemUidReservationRequest Request, LegacyItemUidReservationReceipt Receipt)>();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var opened, out var reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            foreach (var writer in new[] { LegacyItemUidRegistry.RuneDecomposeWriter, LegacyItemUidRegistry.RuneBagWriter, LegacyItemUidRegistry.RelicFuseWriter })
            {
                var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), writer, FuseFixture.Uid,
                    FuseFixture.Uid, new string('a', 64), 1);
                Assert.Equal(LegacyItemUidReservationOutcome.Reserved,
                    registry.ReserveForOperation(f.Data.ExpectedDomain, request, out var token, out var receipt, out reason));
                Assert.NotNull(token); Assert.NotNull(receipt);
                Assert.True(token.TryTake(out var ids)); Assert.Single(ids);
                lookalikes.Add((request, receipt));
            }
        }
        await f.ReopenFrameAsync(); f.Sent.Clear();
        f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.AfterReservation;
        try { await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes()); }
        finally { f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.None; }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent);
        var highWater = f.HighWater; var beforeItems = File.ReadAllBytes(f.ItemsPath);
        await f.CloseAsync();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out opened, out reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            foreach (var (request, receipt) in lookalikes)
            {
                Assert.Throws<InvalidDataException>(() => registry.RequireRelicFuseAllocation(request, receipt));
                Assert.Equal(LegacyItemUidReservationOutcome.AlreadyRecorded,
                    registry.ReserveForOperation(f.Data.ExpectedDomain, request, out var duplicate, out var found, out reason));
                Assert.Null(duplicate); Assert.Equal(receipt, found);
            }
        }
        f.OpenRuntime(); Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.Equal(highWater, f.HighWater); Assert.Equal(beforeItems, File.ReadAllBytes(f.ItemsPath));
        await f.CloseAsync();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out opened, out reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            foreach (var (request, receipt) in lookalikes)
            {
                Assert.Equal(LegacyItemUidReservationOutcome.AlreadyRecorded,
                    registry.ReserveForOperation(f.Data.ExpectedDomain, request, out var duplicate, out var found, out reason));
                Assert.Null(duplicate); Assert.Equal(receipt, found);
            }
            Assert.True(registry.TryReserve(1, out var next, out reason), reason);
            Assert.NotNull(next); Assert.True(next.TryTake(out var ids)); Assert.Equal(new[] { highWater + 1 }, ids);
        }
    });

    [Theory]
    [InlineData("unknown-field")]
    [InlineData("unknown-kind")]
    [InlineData("overlap")]
    [InlineData("duplicate-property")]
    public Task ActualRelicNamespaceCannotHideMalformedTail(string corruption) => Run(async f =>
    {
        f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.AfterReservation;
        try { await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes()); }
        finally { f.Runtime.RelicFuseNamespaceInterruption = LegacyRelicFuseNamespaceInterruption.None; }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent); await f.CloseAsync();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out var opened, out var reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), "real-tail", FuseFixture.Uid,
                FuseFixture.Uid, new string('b', 64), 1);
            Assert.Equal(LegacyItemUidReservationOutcome.Reserved,
                registry.ReserveForOperation(f.Data.ExpectedDomain, request, out var token, out _, out reason));
            Assert.NotNull(token); Assert.True(token.TryTake(out _));
        }
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(7, state["Version"]!.GetValue<int>());
        var rows = state["Operations"]!.AsArray(); Assert.Equal(2, rows.Count);
        Assert.Equal("RelicFuse", rows[0]!["NamespaceReservation"]!["Kind"]!.GetValue<string>());
        var tail = rows[1]!.AsObject();
        switch (corruption)
        {
            case "unknown-field": tail["Unknown"] = 1; break;
            case "unknown-kind": tail["NamespaceReservation"] = new JsonObject { ["Version"] = 1, ["Kind"] = "Unknown" }; break;
            case "overlap": tail["First"] = rows[0]!["First"]!.GetValue<uint>(); break;
            case "duplicate-property": break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        var text = state.ToJsonString();
        if (corruption == "duplicate-property")
        {
            var original = text;
            text = text.Replace("\"Writer\":\"real-tail\"", "\"Writer\":\"real-tail\",\"Writer\":\"real-tail\"", StringComparison.Ordinal);
            Assert.NotEqual(original, text);
        }
        File.WriteAllText(f.Data.State, text); var files = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var rejected, out _));
        Assert.Null(rejected); f.Data.AssertFiles(files);
        Assert.False(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out opened, out _));
        Assert.Null(opened); f.Data.AssertFiles(files);
    });
}
