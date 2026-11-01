using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public Task GenuineHitProgressionPrefixesRecoverOnceWithDistinctTargetAccountAndPlayer(bool lethal, int boundary) =>
        RunHitRecoveryAsync(lethal, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryAsync(f, target, lethal, boundary);
            await AssertHitRecoveryRestartsAsync(capture);
        });

    [Theory]
    [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public Task GenuineHitProgressionFlushedStagesRecoverWithoutEarlierPublication(bool lethal, int stageCount) =>
        RunHitRecoveryAsync(lethal, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryAsync(f, target, lethal, stageCount, flushed: true);
            for (int index = 0; index < capture.Targets.Length; index++)
            {
                string path = capture.Targets[index];
                string key = Path.GetRelativePath(f.Data.Root, path);
                Assert.Equal(capture.Before[key], File.ReadAllBytes(path));
                string stage = path + ".managed-trade-" + capture.Transaction + ".tmp";
                if (index < stageCount) Assert.Equal(capture.After[key], File.ReadAllBytes(stage));
                else Assert.False(File.Exists(stage));
            }
            await AssertHitRecoveryRestartsAsync(capture);
        });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task GenuineEquipDeliveryDeathWithChangedSpellsAndProficiencyRecoversEachPrefix(int boundary) =>
        RunHitRecoveryAsync(true, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryDuringEquipAsync(f, target, boundary);
            string key = Path.GetRelativePath(f.Data.Root, capture.Targets[1]);
            Assert.NotEqual(capture.Before[key], capture.After[key]);
            await AssertHitRecoveryRestartsAsync(capture);
        }, changedDeath: true);

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task GenuineEquipDeliveryDeathWithChangedSpellsRecoversEachFlushedStage(int stageCount) =>
        RunHitRecoveryAsync(true, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryDuringEquipAsync(f, target, stageCount, flushed: true);
            string spellKey = Path.GetRelativePath(f.Data.Root, capture.Targets[1]);
            Assert.NotEqual(capture.Before[spellKey], capture.After[spellKey]);
            for (int index = 0; index < capture.Targets.Length; index++)
            {
                string path = capture.Targets[index]; string key = Path.GetRelativePath(f.Data.Root, path);
                Assert.Equal(capture.Before[key], File.ReadAllBytes(path));
                string stage = path + ".managed-trade-" + capture.Transaction + ".tmp";
                if (index < stageCount) Assert.Equal(capture.After[key], File.ReadAllBytes(stage));
                else Assert.False(File.Exists(stage));
            }
            await AssertHitRecoveryRestartsAsync(capture);
        }, changedDeath: true);

    [Fact]
    public Task GenuineChangedSpellIntentRefusesSpellAfterBeforeCharacterPrefixWithoutMutation() =>
        RunHitRecoveryAsync(true, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryDuringEquipAsync(f, target, 0);
            await f.CloseAsync();
            string spell = capture.Targets[1]; string key = Path.GetRelativePath(f.Data.Root, spell);
            Assert.NotEqual(capture.Before[key], capture.After[key]);
            File.WriteAllBytes(spell, capture.After[key]); // Declared negative non-prefix, after actual issuer closed.
            var files = f.Data.Files();
            int output = f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count;
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
                Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason)); f.Data.AssertFiles(files);
                AssertHistoricalFiles(capture.History, HistoryFiles(f)); Assert.Equal(capture.Ledger, File.ReadAllBytes(f.Data.State));
                Assert.Equal(output, f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count);
                using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        }, changedDeath: true);

    private static async Task AssertHitRecoveryRestartsAsync(HitRecoveryCapture capture, bool alreadyClosed = false)
    {
        var f = capture.Owner; int output = f.Sent.Count + f.Frames(capture.ActorAccount).Count + f.Frames(DeathTargetAccount).Count;
        if (!alreadyClosed) await f.CloseAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            using var owner = f.Open();
            Assert.True(owner.Recovery.IsReadyForCharacterItemsSpells(f.Data.Users, f.Data.Items, f.Data.Spells));
            f.Data.AssertFiles(capture.After); AssertHistoricalFiles(capture.History, HistoryFiles(f));
            Assert.Equal(capture.Ledger, File.ReadAllBytes(f.Data.State));
            Assert.Equal(capture.CreationPayload, CompletedIntent(f).CopyPayload());
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.All(capture.Targets, path => Assert.False(File.Exists(path + ".managed-trade-" + capture.Transaction + ".tmp")));
            Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim, out var observation, out var reason), reason);
            Assert.True(observation.Maximum <= System.Text.Json.Nodes.JsonNode.Parse(capture.Ledger)!["HighWater"]!.GetValue<uint>());
            Assert.Equal(output, f.Sent.Count + f.Frames(capture.ActorAccount).Count + f.Frames(DeathTargetAccount).Count);
        }
    }

    [Theory]
    [InlineData(false, "unknown-operation")]
    [InlineData(false, "raw-string")]
    [InlineData(false, "target-account")]
    [InlineData(false, "proficiency-uid")]
    [InlineData(false, "wrong-root")]
    [InlineData(false, "bad-after")]
    [InlineData(false, "actor-items-drift")]
    [InlineData(false, "reverse-prefix")]
    [InlineData(false, "foreign-stage")]
    [InlineData(false, "duplicate-player")]
    [InlineData(true, "spell-uid")]
    [InlineData(true, "reverse-prefix")]
    public Task ActualHitProgressionIntentRefusesCorruptRecordsAndForeignInputsWithoutMutation(bool lethal, string corruption) =>
        RunHitRecoveryAsync(lethal, async (f, target) =>
        {
            var capture = await CaptureHitRecoveryAsync(f, target, lethal, 0);
            await f.CloseAsync(); var record = capture.Record; var operation = record["PhysicalHitProgression"]!.AsObject();
            bool changeRecord = true;
            switch (corruption)
            {
                case "unknown-operation": operation["Accepted"] = true; break;
                case "raw-string": operation["RawExperience"] = "300"; break;
                case "target-account": record["Changes"]![0]!["FileId"] = DeathTargetUid; break;
                case "proficiency-uid": record["Changes"]!.AsArray()[^1]!["FileId"] = DeathTargetUid; break;
                case "spell-uid": record["Changes"]![1]!["FileId"] = DeathTargetAccount; break;
                case "wrong-root": record["HitProgressionDomain"]!["Proficiencies"] = f.Data.Items; break;
                case "bad-after": record["Changes"]![0]!["After"] = record["Changes"]![0]!["Before"]!.GetValue<string>(); break;
                case "actor-items-drift":
                {
                    string path = Path.Combine(f.Data.Items, CreationFixture.RuneActor.ToString(CultureInfo.InvariantCulture) + ".bin");
                    var bytes = File.ReadAllBytes(path); Assert.NotEmpty(bytes); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes);
                    changeRecord = false; break;
                }
                case "reverse-prefix":
                {
                    string path = capture.Targets[^1]; string key = Path.GetRelativePath(f.Data.Root, path);
                    File.WriteAllBytes(path, capture.After[key]); changeRecord = false; break;
                }
                case "foreign-stage":
                    File.WriteAllBytes(capture.Targets[^1] + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", [1]);
                    changeRecord = false; break;
                case "duplicate-player":
                    File.WriteAllBytes(Path.Combine(f.Data.Users, (DeathTargetAccount + 1).ToString(CultureInfo.InvariantCulture) + ".ini"),
                        File.ReadAllBytes(capture.Targets[0])); changeRecord = false; break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            if (changeRecord) File.WriteAllText(f.PendingPath, record.ToJsonString(), new UTF8Encoding(false));
            var files = f.Data.Files(); int output = f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count;
            for (int restart = 0; restart < 2; restart++)
            {
                Assert.False(LegacyOwnedAdmissionRuntime.TryOpen(f.Data.Loader, f.Data.Options(), out var opened, out var reason), reason);
                Assert.Null(opened); Assert.False(string.IsNullOrWhiteSpace(reason)); f.Data.AssertFiles(files);
                AssertHistoricalFiles(capture.History, HistoryFiles(f)); Assert.Equal(capture.Ledger, File.ReadAllBytes(f.Data.State));
                Assert.Equal(output, f.Sent.Count + f.Frames(CreationFixture.RuneActor).Count + f.Frames(DeathTargetAccount).Count);
                using var lease = new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
        });
}
