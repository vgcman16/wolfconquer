using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static bool TryReadCompleteMailboxGear(byte[] image, out CombatGearSnapshot? snapshot)
    {
        snapshot = null;
        try
        {
            ValidateMailboxGearImage(image);
            snapshot = CombatGearSnapshotLoader.Parse(new UTF8Encoding(false, true).GetString(image));
            return true;
        }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }
}
