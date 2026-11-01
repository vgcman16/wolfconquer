using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData("recorded-after")]
    [InlineData("current-target")]
    [InlineData("known-stage")]
    [InlineData("foreign-stage")]
    [InlineData("foreign-journal")]
    [InlineData("unknown-field")]
    [InlineData("missing-field")]
    [InlineData("uint32-overflow")]
    [InlineData("ushort-overflow")]
    [InlineData("participant-uid")]
    [InlineData("participant-account")]
    [InlineData("map")]
    [InlineData("jump-geometry")]
    [InlineData("walk-geometry")]
    [InlineData("reversed-changes")]
    public Task GenuinePairedMovementPendingRefusesForeignRecordedOrPhysicalEvidenceWithoutWritesOrLeaseLeak(string corruption) => Run(async f =>
    {
        var capture = await CapturePair(f, jump: corruption != "walk-geometry", boundary: corruption == "known-stage" ? 2 : 0,
            flushed: corruption == "known-stage");
        await f.CloseAsync();
        // DECLARED NEGATIVE edits only after the genuine Frame produced21 and
        // its real pending/stages. None is a positive journal/owner substitute.
        if (corruption is not ("current-target" or "known-stage" or "foreign-stage" or "foreign-journal"))
        {
            var record = JsonNode.Parse(File.ReadAllBytes(capture.PendingPath))!;
            var operation = record["PairMovement"]!.AsObject();
            switch (corruption)
            {
                case "recorded-after":
                    var change = record["Changes"]![0]!;
                    byte[] bytes = Convert.FromBase64String(change["After"]!.GetValue<string>());
                    string text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
                    text = ReplaceLine(text, "X", 432, 433);
                    change["After"] = Convert.ToBase64String(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
                    break;
                case "unknown-field": operation["Foreign"] = 1; break;
                case "missing-field": Assert.True(operation.Remove("DirectionOrFacing")); break;
                // This is an invalid UInt32, not a claim that changing one
                // valid runtime DynamicId can be detected from saved bytes.
                case "uint32-overflow": operation["DynamicId"] = 4_294_967_296L; break;
                case "ushort-overflow": operation["ToY"] = 65_536; break;
                case "participant-uid": operation["ActorUid"] = Uid + 10; break;
                case "participant-account": operation["PartnerAccountId"] = (long)Peer + 10; break;
                case "map": operation["MapId"] = 1003; break;
                case "jump-geometry": operation["ToX"] = 449; break;
                case "walk-geometry": operation["ToX"] = 432; break;
                case "reversed-changes":
                    var changes = record["Changes"]!.AsArray();
                    var first = changes[0]!.DeepClone(); var second = changes[1]!.DeepClone();
                    changes[0] = second; changes[1] = first; break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            File.WriteAllText(capture.PendingPath, record.ToJsonString());
        }
        else if (corruption == "current-target")
        {
            var path = f.CharacterPath(Peer); var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1;
            File.WriteAllBytes(path, bytes);
        }
        else if (corruption == "known-stage")
        {
            string path = PairStage(capture, capture.Targets[0]); var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1;
            File.WriteAllBytes(path, bytes);
        }
        else if (corruption == "foreign-stage")
            File.WriteAllBytes(f.CharacterPath(Peer) + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [0x55]);
        else File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(capture.PendingPath)!, "foreign.json"), [0x55]);
        var corruptFiles = f.Data.Files(); int outputs = f.Order.Count;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool opened = LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var refused, out var reason);
            if (refused is not null) await refused.DisposeAsync();
            Assert.False(opened);
            Assert.Null(refused); Assert.NotEmpty(reason); f.Data.AssertFiles(corruptFiles); Assert.Equal(outputs, f.Order.Count);
            using var released = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Equal(0, released.Length);
        }
    });
}
