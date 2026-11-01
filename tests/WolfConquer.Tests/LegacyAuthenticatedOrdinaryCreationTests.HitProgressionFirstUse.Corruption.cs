using System.Text.Json.Nodes;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("foreign-empty-profs")]
    [InlineData("reverse-created-profs")]
    [InlineData("foreign-stage")]
    [InlineData("substituted-spells-before")]
    [InlineData("changed-profs-after")]
    public Task ActualFirstUseIntentRefusesForeignOrCorruptMissingOriginWithoutMutation(string corruption) =>
        RunFirstUseHitAsync(true, true, true, async f =>
        {
            var capture = await CaptureFirstUseRecoveryAsync(f, 0, flushed: false);
            await f.Owner.CloseAsync();
            var record = capture.Record; bool replaceRecord = false;
            switch (corruption)
            {
                case "foreign-empty-profs": File.WriteAllBytes(f.ProfsPath, new byte[4]); break;
                case "reverse-created-profs": File.WriteAllBytes(f.ProfsPath, FirstUseProfsGolden()); break;
                case "foreign-stage": File.WriteAllBytes(f.ProfsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [1]); break;
                case "substituted-spells-before":
                    record["Changes"]![1]!["Before"] = Convert.ToBase64String(new byte[4]); replaceRecord = true; break;
                case "changed-profs-after":
                {
                    var bytes = FirstUseProfsGolden(); bytes[16] = 1;
                    record["Changes"]!.AsArray()[^1]!["After"] = Convert.ToBase64String(bytes); replaceRecord = true; break;
                }
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            if (replaceRecord) File.WriteAllText(f.Owner.PendingPath, record.ToJsonString());
            var evidence = f.Owner.Data.Files();
            var output = f.Owner.Sent.Count + f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count;
            for (var restart = 0; restart < 2; restart++)
            {
                Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(), out var opened, out var reason), reason);
                Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason)); f.Owner.Data.AssertFiles(evidence);
                AssertHistoricalFiles(capture.History, HistoryFiles(f.Owner)); Assert.Equal(capture.Ledger, File.ReadAllBytes(f.Owner.Data.State));
                Assert.Equal(output, f.Owner.Sent.Count + f.Owner.Frames(CreationFixture.RuneActor).Count + f.Owner.Frames(DeathTargetAccount).Count);
                using var lease = new FileStream(f.Owner.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        });
}
