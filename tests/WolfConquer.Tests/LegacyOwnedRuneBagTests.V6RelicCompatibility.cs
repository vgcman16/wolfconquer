using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData("relic-kind")]
    [InlineData("unknown-tail")]
    public Task ActualV6RuneBagNamespaceRejectsForeignKindOrMalformedFollowingRow(string corruption) => Run(async f =>
    {
        f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.AfterReservation;
        try { await f.DispatchAsync(); }
        finally { f.Runtime.RuneBagNamespaceInterruption = LegacyRuneBagNamespaceInterruption.None; }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Empty(f.Sent); await f.CloseAsync();
        Assert.True(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var opened, out var reason), reason);
        using (var registry = Assert.IsType<LegacyItemUidRegistry>(opened))
        {
            var request = new LegacyItemUidReservationRequest(Guid.NewGuid(), "old-real-tail", BagFixture.Uid,
                BagFixture.Uid, new string('c', 64), 1);
            Assert.Equal(LegacyItemUidReservationOutcome.Reserved,
                registry.ReserveForOperation(f.Data.ExpectedDomain, request, out var token, out _, out reason));
            Assert.NotNull(token); Assert.True(token.TryTake(out _));
        }
        var state = JsonNode.Parse(File.ReadAllBytes(f.Data.State))!.AsObject();
        Assert.Equal(6, state["Version"]!.GetValue<int>());
        var rows = state["Operations"]!.AsArray(); Assert.Equal(2, rows.Count);
        Assert.Equal("RuneBagReturn", rows[0]!["NamespaceReservation"]!["Kind"]!.GetValue<string>());
        if (corruption == "relic-kind") rows[0]!["NamespaceReservation"]!["Kind"] = "RelicFuse";
        else rows[1]!["unexpected"] = true;
        File.WriteAllText(f.Data.State, state.ToJsonString()); var files = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var refused, out _));
        Assert.Null(refused); f.Data.AssertFiles(files);
        Assert.False(LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out opened, out _));
        Assert.Null(opened); f.Data.AssertFiles(files);
    }, "out-rich");
}
