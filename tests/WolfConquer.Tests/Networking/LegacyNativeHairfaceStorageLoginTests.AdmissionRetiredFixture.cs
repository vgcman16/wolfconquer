using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    // Historical owned data only. The actual registry prepares/reserves the old
    // literal writer, but the ordinary Frame is never given a migration switch.
    private static LegacyItemUidProducerIntentSnapshot SeedRetiredOrdinaryWork(LoginInput input,
        AdmissionUidFixture uids, bool allocated, out Dictionary<LegacyTradeJournalFileKind, byte[]?> after)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        var before = LegacyTradeJournal.CaptureOrdinaryAdmissionInputs(roots, Uid);
        var old = Encoding.UTF8.GetString(before[LegacyTradeJournalFileKind.Character]!);
        var character = Encoding.UTF8.GetBytes(old.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)
            .Replace("Strength=0\r\n", "Strength=5\r\n", StringComparison.Ordinal)
            .Replace("Agility=0\r\n", "Agility=2\r\n", StringComparison.Ordinal)
            .Replace("Vitaliti=0\r\n", "Vitaliti=3\r\n", StringComparison.Ordinal)
            .Replace("MinHitPoints=1\r\n", "MinHitPoints=93\r\n", StringComparison.Ordinal)
            .Replace("Money=1000\r\n", "Money=100000\r\n", StringComparison.Ordinal));
        var spells = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(1046, 0, 0, 0, 0, 0), new(1110, 0, 0, 0, 0, 0)]));
        var profs = new byte[4];
        var templates = new LegacyOrdinaryStarterTemplate[] { new(132009, 3, 321, 321), new(420301, 4, 321, 321),
            new(420301, 5, 321, 321), new(150003, 6, 321, 321) };
        var recipe = new LegacyOrdinaryAdmissionRecipe(Uid, roots,
            Enum.GetValues<LegacyTradeJournalFileKind>().ToDictionary(kind => kind,
                kind => before.GetValueOrDefault(kind) is { } bytes ? SHA256.HashData(bytes) : null),
            SHA256.HashData(character), SHA256.HashData(spells), SHA256.HashData(profs), templates).Encode();
        var hash = SHA256.HashData(recipe);
        var request = new LegacyItemUidReservationRequest(new Guid(hash.AsSpan(0, 16)), LegacyOrdinaryAdmissionUidOwner.Writer,
            Uid, Uid, Convert.ToHexString(hash).ToLowerInvariant(), 4);
        Assert.True(uids.Registry.TryPrepareProducerIntent(uids.Domain, request, recipe, out var prepared, out var reason), reason);
        var result = Assert.IsType<LegacyItemUidProducerIntentSnapshot>(prepared);
        if (allocated)
        {
            Assert.True(uids.Registry.TryReserveProducerIntent(uids.Domain, request, recipe, out var reserved, out reason), reason);
            result = Assert.IsType<LegacyItemUidProducerIntentSnapshot>(reserved);
        }
        after = before.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray());
        after[LegacyTradeJournalFileKind.Character] = character;
        after[LegacyTradeJournalFileKind.Spells] = spells;
        after[LegacyTradeJournalFileKind.Proficiencies] = profs;
        if (result.Allocation is { } receipt)
        {
            var colors = new uint[] { 3, 8, 3, 6 };
            var rows = templates.Select((row, index) => new PlayerItemSnapshotItem { Uid = receipt.First + (uint)index,
                ItemId = row.ItemId, Position = row.Position, Durability = 321, MaximumDurability = 321, Color = colors[index] }).ToArray();
            after[LegacyTradeJournalFileKind.Items] = PlayerItemSnapshotLoader.ToLegacyBytes(new([], [], [], rows, []));
        }
        return result;
    }

    private static void InstallRetiredAfterFacts(LoginInput input, IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> after)
    {
        var roots = LegacyOrdinaryAdmissionRoots.FromLoader(new CharacterSnapshotLoader(input.Users));
        foreach (var pair in after)
            if (pair.Value is not null) File.WriteAllBytes(roots.PathFor(pair.Key, Uid), pair.Value);
    }
}
