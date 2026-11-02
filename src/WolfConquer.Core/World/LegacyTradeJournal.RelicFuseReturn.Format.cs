using System.Text.Json;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static LegacyRelicFuseReturnJournalOperation ReadRelicFuseReturn(JsonElement element)
    {
        var fields = GetProperties(element, "AccountId", "CharacterUid", "SelectedUid");
        var account = ReadRuneDecomposeUInt(fields["AccountId"]);
        var character = ReadRuneDecomposeUInt(fields["CharacterUid"]);
        var selected = ReadRuneDecomposeUInt(fields["SelectedUid"]);
        if (account == 0 || character == 0 || selected == 0)
            throw new JsonException("Invalid relic return identity.");
        return new(account, character, selected);
    }
}
