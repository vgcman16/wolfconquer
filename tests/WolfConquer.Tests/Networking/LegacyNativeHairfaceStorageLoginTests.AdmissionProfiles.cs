using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("before")]
    [InlineData("mixed")]
    [InlineData("after")]
    public async Task UnsafePrototypeV6CanOnlyRetireExactAlreadyAfterFactsWithoutRollingForward(string state)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        await InterruptRealAdmission(input, uids, LegacyOrdinaryAdmissionInterruption.AfterPending);
        uids.Dispose();
        var pending = Path.Combine(input.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(7, record["Version"]!.GetValue<int>());
        record["Version"] = 6;
        record.AsObject().Remove("AdmissionPreparedAt");
        var character = record["Changes"]!.AsArray().Single(row => row!["Kind"]!.GetValue<int>() == (int)LegacyTradeJournalFileKind.Character)!;
        var unsafeAfter = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Convert.FromBase64String(character["After"]!.GetValue<string>()))
            .Replace("Money=1000\r\n", "Money=999\r\n", StringComparison.Ordinal));
        character["After"] = Convert.ToBase64String(unsafeAfter);
        File.WriteAllText(pending, record.ToJsonString());
        var bytes = File.ReadAllBytes(pending);
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        foreach (var row in record["Changes"]!.AsArray())
        {
            var kind = (LegacyTradeJournalFileKind)row!["Kind"]!.GetValue<int>();
            if (state != "after" && (state != "mixed" || kind != LegacyTradeJournalFileKind.Character)) continue;
            File.WriteAllBytes(roots.PathFor(kind, Uid), Convert.FromBase64String(row["After"]!.GetValue<string>()));
        }
        var files = CaptureAdmissionFiles(input);
        var uidBytes = uids.StateBytes();

        var recovered = FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason);

        Assert.Equal(state == "after", recovered);
        if (state == "after") Assert.False(File.Exists(pending));
        else
        {
            Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
            Assert.Equal(bytes, File.ReadAllBytes(pending));
        }
        AssertAdmissionFiles(files);
        Assert.Equal(uidBytes, uids.StateBytes());
    }

    [Theory]
    [InlineData("wallet")]
    [InlineData("stats")]
    [InlineData("item")]
    [InlineData("spell")]
    [InlineData("time-overflow")]
    [InlineData("missing-time")]
    public async Task PreservationProfileRefusesForgedBusinessImagesOrInvalidTimeBeforeAnyRecoveryWrites(string mutation)
    {
        using var input = new LoginInput();
        SeedOwnedOrdinaryParticipants(input);
        using var uids = new AdmissionUidFixture(input);
        await InterruptRealAdmission(input, uids, LegacyOrdinaryAdmissionInterruption.AfterPending);
        uids.Dispose();
        var pending = Path.Combine(input.Users, ".managed-trade-journal", "pending.json");
        var record = JsonNode.Parse(File.ReadAllBytes(pending))!;
        Assert.Equal(7, record["Version"]!.GetValue<int>());
        if (mutation == "time-overflow") record["AdmissionPreparedAt"] = long.MaxValue;
        else if (mutation == "missing-time") record.AsObject().Remove("AdmissionPreparedAt");
        else
        {
            var kind = mutation == "item" ? LegacyTradeJournalFileKind.Items : mutation == "spell" ? LegacyTradeJournalFileKind.Spells : LegacyTradeJournalFileKind.Character;
            var row = record["Changes"]!.AsArray().Single(value => value!["Kind"]!.GetValue<int>() == (int)kind)!;
            var image = Convert.FromBase64String(row["After"]!.GetValue<string>());
            if (mutation is "wallet" or "stats") image = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(image)
                .Replace(mutation == "wallet" ? "Money=1000\r\n" : "Strength=0\r\n",
                    mutation == "wallet" ? "Money=1\r\n" : "Strength=1\r\n", StringComparison.Ordinal));
            else if (mutation == "spell") image = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(1110, 0, 0, 0, 0, 0)]));
            else
            {
                var items = PlayerItemSnapshotLoader.Parse(image);
                image = PlayerItemSnapshotLoader.ToLegacyBytes(items with { EquipmentItems = [] });
            }
            row["After"] = Convert.ToBase64String(image);
        }
        File.WriteAllText(pending, record.ToJsonString());
        var bytes = File.ReadAllBytes(pending);
        var files = CaptureAdmissionFiles(input);
        var uidBytes = uids.StateBytes();

        Assert.False(FreshAdmissionRecovery(input).TryEnsureInitialized(out var reason));

        Assert.Contains(LegacyTradeJournal.RecoveryRequired, reason);
        Assert.Equal(bytes, File.ReadAllBytes(pending));
        AssertAdmissionFiles(files);
        Assert.Equal(uidBytes, uids.StateBytes());
    }
}
