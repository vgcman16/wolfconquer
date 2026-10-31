using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneBagTests
{
    [Theory]
    [InlineData("missing-request")] [InlineData("overlapping-allocation")] [InlineData("changed-cap")]
    [InlineData("changed-spell-byte11")] [InlineData("misbound-item-owner")] [InlineData("tampered-real-stage")]
    public Task GenuineCapturedV20CannotAcquireAuthorityFromChangedInputsOrStage(string fault) => Run(async f =>
    {
        await Capture(f, fault == "tampered-real-stage" ? 3 : 0, flushed: fault == "tampered-real-stage");
        await f.CloseAsync();
        if (fault == "tampered-real-stage")
        {
            var stage = StagePath(f, f.ItemsPath); Assert.True(File.Exists(stage));
            var bytes = File.ReadAllBytes(stage); bytes[^1] ^= 0x40; File.WriteAllBytes(stage, bytes);
        }
        else
        {
            // NEGATIVE edits of an actual captured Frame/Store WAL. No
            // positive operation, allocation, owner or completion is fabricated.
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
            var operation = record["RuneBag"]!;
            switch (fault)
            {
                case "missing-request": operation["Request"] = null; break;
                case "overlapping-allocation": operation["Allocation"]!["First"] = BagFixture.Target; break;
                case "changed-cap":
                    operation["Metadata"]!["RawMaximumStackSize"] = ushort.MaxValue; break;
                case "misbound-item-owner": record["Changes"]![1]!["FileId"] = BagFixture.Uid + 1; break;
                case "changed-spell-byte11":
                    var change = record["Changes"]![2]!;
                    var bytes = Convert.FromBase64String(change["After"]!.GetValue<string>());
                    Assert.True(bytes.Length >= 16); bytes[15] ^= 0x40;
                    change["After"] = Convert.ToBase64String(bytes); break;
                default: throw new InvalidDataException("Unknown negative input.");
            }
            File.WriteAllBytes(f.PendingPath, JsonSerializer.SerializeToUtf8Bytes(record));
        }
        var files = f.Data.Files();
        bool success = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var runtime, out var reason);
        if (runtime is not null) await runtime.DisposeAsync();
        Assert.False(success, reason); Assert.Null(runtime); f.Data.AssertFiles(files);
        using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }, "out-rich");
}
