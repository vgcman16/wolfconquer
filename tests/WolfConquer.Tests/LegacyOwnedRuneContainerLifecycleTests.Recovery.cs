using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneContainerLifecycleTests
{
    [Theory]
    [InlineData("store", 0)] [InlineData("store", 1)] [InlineData("store", 2)] [InlineData("store", 3)]
    [InlineData("bag", 0)] [InlineData("bag", 1)] [InlineData("bag", 2)] [InlineData("bag", 3)]
    [InlineData("drop", 0)] [InlineData("drop", 1)] [InlineData("drop", 2)] [InlineData("drop", 3)]
    public Task GenuineV15RecoversEveryReplacementPrefixWithoutNewUidsOrSecondReplay(string kind, int prefix) => Run(async f =>
    {
        var capture = await Stage(f, kind, prefix); var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        for (var index = 0; index < targets.Length; index++)
            Assert.Equal(index < prefix ? capture.After[targets[index]] : capture.Before[targets[index]], File.ReadAllBytes(targets[index]));
        await f.CloseAsync(); await f.ReopenFrameAsync(); capture.AssertRecovered(f, kind); f.AssertMarker("");
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); capture.AssertRecovered(f, kind); f.Data.AssertFiles(recovered);
    });

    [Theory]
    [InlineData("store", 1)] [InlineData("store", 2)] [InlineData("store", 3)]
    [InlineData("bag", 1)] [InlineData("bag", 2)] [InlineData("bag", 3)]
    [InlineData("drop", 1)] [InlineData("drop", 2)] [InlineData("drop", 3)]
    public Task GenuineFlushedContainerStagesAreExactBeforeOwnedRecoveryRemovesThem(string kind, int ordinal) => Run(async f =>
    {
        var capture = await Stage(f, kind, ordinal, flushed: true); var targets = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        var stages = targets.Select(path => StagePath(f, path)).ToArray();
        for (var index = 0; index < targets.Length; index++)
        {
            Assert.Equal(capture.Before[targets[index]], File.ReadAllBytes(targets[index]));
            if (index < ordinal) Assert.Equal(capture.After[targets[index]], File.ReadAllBytes(stages[index]));
            else Assert.False(File.Exists(stages[index]));
        }
        await f.CloseAsync(); await f.ReopenFrameAsync(); capture.AssertRecovered(f, kind);
        Assert.All(stages, path => Assert.False(File.Exists(path)));
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); capture.AssertRecovered(f, kind); f.Data.AssertFiles(recovered);
    });

    [Theory]
    [InlineData("unknown-kind")] [InlineData("missing-evidence")] [InlineData("misbound-owner")]
    [InlineData("changed-selected")] [InlineData("changed-marker")] [InlineData("changed-wallet")]
    [InlineData("foreign-stage")] [InlineData("tampered-stage")] [InlineData("stale-target")]
    public Task CapturedRealDropCannotAuthorizeMalformedIntentImagesOrUnrelatedStageWork(string fault) => Run(async f =>
    {
        var capture = await Stage(f, "drop", 3, flushed: fault == "tampered-stage"); await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject(); var operation = record["RuneContainer"]!.AsObject();
        if (fault == "unknown-kind") operation["Kind"] = 99;
        else if (fault == "missing-evidence") record.Remove("RuneContainer");
        else if (fault == "changed-selected") operation["SelectedUid"] = F.Shared;
        else if (fault == "misbound-owner") record["Changes"]!.AsArray().Single(row => row!["Kind"]!.GetValue<int>() ==
            (int)LegacyTradeJournalFileKind.Items)!["FileId"] = F.Uid + 1;
        else if (fault is "changed-marker" or "changed-wallet")
        {
            var change = record["Changes"]!.AsArray().Single(row => row!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Character)!;
            var text = CharacterSnapshotFileEncoding.Decode(capture.After[f.CharacterPath], out var encoding);
            text = fault == "changed-marker" ? text.Replace("ManagedRuneSpellOrigins=\r\n", "ManagedRuneSpellOrigins=14160:16\r\n", StringComparison.Ordinal)
                : text.Replace("Money=777\r\n", "Money=778\r\n", StringComparison.Ordinal);
            var changed = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            Assert.NotEqual(capture.After[f.CharacterPath], changed); change["After"] = Convert.ToBase64String(changed);
            File.WriteAllBytes(f.CharacterPath, changed);
        }
        else if (fault == "stale-target") File.WriteAllBytes(f.ItemsPath, capture.Before[f.ItemsPath]!);
        else
        {
            var path = fault == "foreign-stage" ? f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp" : StagePath(f, f.SpellsPath);
            var image = capture.After[f.SpellsPath].ToArray(); if (fault == "tampered-stage") image[0] ^= 0xff;
            File.WriteAllBytes(path, image);
        }
        if (fault == "stale-target")
        {
            // A captured BEFORE image is an allowed partial target; use a real
            // different closed image to challenge current-image identity.
            var wrong = capture.Before[f.ItemsPath]!.ToArray(); wrong[4 + 4] ^= 1; File.WriteAllBytes(f.ItemsPath, wrong);
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString()); f.AssertStartupRefused();
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RetiredDropUidCannotReappearAsUnrelatedPhysicalOwnershipEvenAtAllAfter(bool alias) => Run(async f =>
    {
        await Stage(f, "drop", 3); await f.CloseAsync();
        var target = alias ? "0" + F.Uid : (F.Uid + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        File.WriteAllBytes(Path.Combine(f.Data.Items, target + ".bin"),
            PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], [], [F.Item(F.First, F.FirstType)])));
        f.AssertStartupRefused();
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RetiredPhysicalUidMayRemainAnUnownedMailReferenceWithinThePinnedHighWater(bool beyondFloor) => Run(async f =>
    {
        var capture = await Stage(f, "drop", 3); await f.CloseAsync();
        var mailbox = new LegacyMailboxStore(f.Data.Root); const uint owner = F.Uid + 1;
        mailbox.Add(owner, new LegacyMailboxEntry { Id = 1, Title = "retired Rune reference",
            ItemId = beyondFloor ? LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 : F.First,
            Timer = DateTime.Now.AddDays(1).Ticks });
        mailbox.Save(owner); var path = mailbox.GetMailboxPath(owner); var image = File.ReadAllBytes(path);
        if (beyondFloor) f.AssertStartupRefused();
        else
        {
            f.Open(); capture.AssertRecovered(f, "drop"); Assert.Equal(image, File.ReadAllBytes(path));
            await f.CloseAsync(); var recovered = f.Data.Files(); f.Open(); capture.AssertRecovered(f, "drop"); f.Data.AssertFiles(recovered);
        }
    });
}
