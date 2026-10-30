using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupItemSpellRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemSpellRecoveryTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ARealNewRuneGrantCannotAcquireHistoricalVersion9AuthorityWithoutItsRecordedOperation(int boundary) =>
        WithFixture(async f =>
        {
            await f.StageAsync(boundary); await f.CloseAsync();
            Assert.DoesNotContain(PlayerSpellSnapshotLoader.Parse(f.Before[f.SpellsPath]!).Spells, row => row.Id == Fixture.RuneSpell);
            Assert.Contains(PlayerSpellSnapshotLoader.Parse(f.After[f.SpellsPath]).Spells, row => row.Id == Fixture.RuneSpell);
            var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject(); Assert.Equal(14, record["Version"]!.GetValue<int>());
            record["Version"] = 9; Assert.True(record.Remove("RuneOperation"));
            File.WriteAllText(f.PendingPath, record.ToJsonString());
            f.AssertRefused();
        }, mode: "rune-equip");

    [Theory]
    [InlineData("retiring-before-duplicate")] [InlineData("unknown-uid")] [InlineData("malformed-tail")]
    public Task V9KeepsThePhysicalNamespaceScanAndCannotHideUnrelatedRawUidEvidence(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        var uid = fault == "retiring-before-duplicate" ? Fixture.ItemUid : fault == "unknown-uid"
            ? LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1 : Fixture.ItemUid + 1_000;
        var raw = PlayerItemSnapshotLoader.ToLegacyBytes(new([NewItem(uid)], [], [], [], []));
        if (fault == "malformed-tail") raw = raw.Concat(new byte[] { 0x71 }).ToArray();
        File.WriteAllBytes(Path.Combine(f.Data.Items, (Fixture.Uid + 1).ToString(CultureInfo.InvariantCulture) + ".bin"), raw);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("foreign-character")] [InlineData("missing-items")]
    [InlineData("foreign-spells")] [InlineData("missing-existing-spells")]
    public Task CurrentCharacterItemsAndSpellsMustMatchTheirActualCapturedBeforeOrAfterImages(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        if (fault == "missing-items") File.Delete(f.ItemsPath);
        else if (fault == "missing-existing-spells") File.Delete(f.SpellsPath);
        else if (fault == "foreign-spells") File.WriteAllBytes(f.SpellsPath, new byte[sizeof(int)]);
        else
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out var encoding);
            Assert.Contains("Money=777\r\n", text, StringComparison.Ordinal);
            File.WriteAllBytes(f.CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(
                text.Replace("Money=777\r\n", "Money=776\r\n", StringComparison.Ordinal))).ToArray());
        }
        f.AssertRefused();
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task EveryKnownV9StageRequiresItsOwnActualCapturedAfterImagePrefix(int ordinal) => WithFixture(async f =>
    {
        await f.StopAfterStageFlushedAsync(3); await f.CloseAsync();
        var path = f.StagePath(f.OrderedTargets[ordinal]); var bytes = File.ReadAllBytes(path); bytes[0] ^= 0xff;
        File.WriteAllBytes(path, bytes); f.AssertRefused();
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public Task ForeignTransactionStagesRemainFencedInAllThreeConfiguredRoots(int ordinal) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync(); var target = f.OrderedTargets[ordinal];
        File.WriteAllBytes(target + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp", f.After[target]);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("domain")] [InlineData("evidence")] [InlineData("items-root")] [InlineData("spells-root")]
    public Task V9RecoveryCannotSubstituteAnotherConfiguredNamespaceIdentity(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(2); await f.CloseAsync(); var configured = f.Data.Options();
        var changed = fault switch
        {
            "domain" => configured with { ExpectedDomain = Guid.NewGuid() },
            "evidence" => configured with { HistoricalEvidence = configured.HistoricalEvidence + " changed" },
            "items-root" => configured with { ItemsRoot = Path.Combine(f.Data.Root, "ForeignItems") },
            "spells-root" => configured with { SpellsRoot = Path.Combine(f.Data.Root, "ForeignSpells") },
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        f.AssertRefused(changed);
    });

    [Theory]
    [InlineData("captured-spells-root")] [InlineData("spells-file-owner")]
    [InlineData("unexpected-kind")] [InlineData("unsupported-version")]
    public Task AChangedCapturedDomainOwnerKindOrVersionCannotAcquireV9RecoveryAuthority(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync(); var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
        switch (fault)
        {
            case "captured-spells-root": record["CharacterItemsSpellsDomain"]!["Spells"] = Path.Combine(f.Data.Root, "ForeignSpells"); break;
            case "spells-file-owner": Target(record, LegacyTradeJournalFileKind.Spells)["FileId"] = (long)Fixture.Uid + 1; break;
            case "unexpected-kind": Target(record, LegacyTradeJournalFileKind.Spells)["Kind"] = (int)LegacyTradeJournalFileKind.Proficiencies; break;
            case "unsupported-version": record["Version"] = 99; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString()); f.AssertRefused();
    });

    [Theory]
    [InlineData("leading-zero-file")] [InlineData("subdirectory")]
    public Task UnrelatedSpellsEvidenceCannotBorrowTheSupportedCapturedTargetExemption(string fault) => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync();
        if (fault == "subdirectory") Directory.CreateDirectory(Path.Combine(f.Data.Spells, "foreign"));
        else File.WriteAllBytes(Path.Combine(f.Data.Spells, "0" + Fixture.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"),
            f.Before[f.SpellsPath]!);
        f.AssertRefused();
    });

    [Fact]
    public Task CapturedExistingSpellsBeforeCannotAuthorizeRecreationAfterItsRootAndTargetDisappear() => WithFixture(async f =>
    {
        await f.StageAsync(0); await f.CloseAsync(); Assert.NotNull(f.Before[f.SpellsPath]); File.Delete(f.SpellsPath);
        Assert.Empty(Directory.GetFileSystemEntries(f.Data.Spells));
        Directory.Delete(f.Data.Spells); f.AssertRefused(); Assert.False(Directory.Exists(f.Data.Spells));
    });

    [Theory]
    [InlineData("root-file")] [InlineData("foreign-stage")] [InlineData("tampered-known-stage")]
    [InlineData("foreign-target")] [InlineData("known-stage-directory")]
    public Task MissingDirectoryAuthorityCannotAdmitForeignRootTargetOrStageEvidence(string fault) => WithFixture(async f =>
    {
        Assert.False(Directory.Exists(f.Data.Spells)); await f.StageAsync(0); await f.CloseAsync();
        Assert.False(Directory.Exists(f.Data.Spells)); Assert.Null(f.Before[f.SpellsPath]);
        if (fault == "root-file") File.WriteAllText(f.Data.Spells, "foreign root evidence");
        else
        {
            Directory.CreateDirectory(f.Data.Spells);
            if (fault == "foreign-target") File.WriteAllBytes(f.SpellsPath, new byte[sizeof(int)]);
            else if (fault == "known-stage-directory") Directory.CreateDirectory(f.StagePath(f.SpellsPath));
            else
            {
                var bytes = f.After[f.SpellsPath].ToArray();
                var path = f.StagePath(f.SpellsPath);
                if (fault == "tampered-known-stage") bytes[0] ^= 0xff;
                else path = f.SpellsPath + ".managed-trade-" + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(path, bytes);
            }
        }
        f.AssertRefused();
        if (fault == "known-stage-directory") Assert.True(Directory.Exists(f.StagePath(f.SpellsPath)));
    }, missingSpellsDirectory: true);

    [Fact]
    public Task ActualExclusiveLeaseIsRequiredBeforeAnyV9RecoveryWrite() => WithFixture(async f =>
    {
        await f.StageAsync(2); await f.CloseAsync();
        using (new FileStream(f.Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            f.AssertRefused(leaseAvailable: false);
        f.AssertLeaseAvailable(); f.Open(); f.AssertRecoveredImages();
    });

    [Fact]
    public Task GenuineRetainedProducerIntentIsNotAssumedCompletedByBookConsumption() => WithFixture(async f =>
    {
        var roots = f.Data.Options().Roots; var inputs = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Fixture.Uid);
        var hashes = inputs.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : SHA256.HashData(pair.Value));
        var recipe = new LegacyOrdinaryAdmissionRecipe(Fixture.Uid, roots, hashes,
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Character]!), SHA256.HashData(inputs[LegacyTradeJournalFileKind.Spells]!),
            SHA256.HashData(inputs[LegacyTradeJournalFileKind.Proficiencies]!), [new(Fixture.OrdinaryType, 3, 10, 10)]);
        var payload = recipe.Encode(); Assert.Equal(payload, LegacyOrdinaryAdmissionRecipe.Decode(payload, roots).Encode());
        var digest = SHA256.HashData(payload);
        var request = new LegacyItemUidReservationRequest(new Guid(digest.AsSpan(0, 16)), LegacyOrdinaryAdmissionUidOwner.Writer,
            Fixture.Uid, Fixture.Uid, Convert.ToHexString(digest).ToLowerInvariant(), 1);
        Assert.True(f.Runtime.Ordinary.Registry.TryPrepareProducerIntent(f.Data.ExpectedDomain, request, payload,
            out var prepared, out var reason), reason);
        Assert.Null(Assert.IsType<LegacyItemUidProducerIntentSnapshot>(prepared).Allocation);
        f.RegistryState = File.ReadAllBytes(f.Data.State);
        await f.StageAsync(0); await f.CloseAsync(); f.AssertRefused();
    });
}
