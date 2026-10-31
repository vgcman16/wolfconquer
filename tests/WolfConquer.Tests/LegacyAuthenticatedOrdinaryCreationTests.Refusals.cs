using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("hp")] [InlineData("durability")] [InlineData("rune-zero")]
    public Task RealAuthenticatedCreationRejectsMalformedRequiredMetadataWithoutAdmittingUidWork(string fault) => Run(async f =>
    {
        if (fault == "rune-zero") Assert.IsType<string[]>(f.Catalog.RuneStorageAttributes[0].Fields)[3] = "invalid";
        else
        {
            var starter = f.Catalog.Items.First(row => row.Id == 132009);
            Assert.IsType<string[]>(starter.Fields)[fault == "hp" ? 18 : 21] = "invalid";
        }
        var files = f.Data.Files(); var native = f.Native; var player = native.Player;
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await f.CreateAsync(); f.Data.AssertFiles(files); Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _));
        Assert.Same(native, f.Native); Assert.Same(player, native.Player); Assert.Equal(0U, player.UID);
        Assert.Null(native.Inventory); Assert.Null(native.Equipment); Assert.Null(native.Rune);
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection)); Assert.False(File.Exists(f.PendingPath));
        Assert.False(f.Runtime.Recovery.IsBlocked);
    });

    [Theory]
    [InlineData("payload")] [InlineData("allocation")] [InlineData("unknown")] [InlineData("foreign-stage")]
    public Task CorruptingGenuineCapturedCreationEvidenceCannotAuthorizeStartupOrDiscardItsBirthHistory(string fault) => Run(async f =>
    {
        _ = await CaptureCreation(f, 0); await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        var operation = record["OrdinaryCreation"]!.AsObject();
        switch (fault)
        {
            case "payload":
            {
                var bytes = Convert.FromBase64String(operation["Payload"]!.GetValue<string>());
                var marker = Encoding.UTF8.GetBytes("creation-owner"); int offset = bytes.AsSpan().IndexOf(marker);
                Assert.True(offset >= 0); bytes[offset] = (byte)'C'; // Valid canonical text, different actual issued recipe/request.
                operation["Payload"] = Convert.ToBase64String(bytes); break;
            }
            case "allocation": operation["Allocation"]!["Generation"] = operation["Allocation"]!["Generation"]!.GetValue<ulong>() + 1; break;
            case "unknown": operation["Authenticated"] = true; break; // A caller flag is not creation authority.
            case "foreign-stage":
                File.WriteAllBytes(f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", new byte[] { 1 }); break;
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString(), new UTF8Encoding(false));
        var files = f.Data.Files();
        Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
        Assert.Null(opened); f.Data.AssertFiles(files);
        using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });
}
