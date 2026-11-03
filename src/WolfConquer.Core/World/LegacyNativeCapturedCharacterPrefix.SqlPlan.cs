using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeCapturedCharacterPrefix
{
    // Fresh caller-owned DATA on every exposure; neither this graph nor the
    // immutable Source grants native, account, Store or SQL revision custody.
    internal SqlOrdinaryAdmissionData? PreparedData => sqlPlan?.ReadPreparedData();

    internal static bool TryPrepareFromSqlPlan(ConquerTcpConnection connection, SqlOrdinaryAdmissionImagePlan? plan,
        out LegacyNativeCapturedCharacterPrefix? prepared, out string reason)
    {
        prepared = null;
        reason = "capturedNativePrefixPlanMissing";
        if (plan is null) return false;
        try
        {
            var data = plan.ReadPreparedData();
            var captured = plan.PreparedPrefix;
            if (!ReferenceEquals(data.Source, plan.After) || !ReferenceEquals(data.Source.Core, captured.Image) ||
                data.Source.AccountId <= 0 || captured.Image.AccountId != data.Source.AccountId ||
                data.Source.CharacterUid != data.Character.Uid || captured.Image.CharacterUid != data.Source.CharacterUid ||
                captured.SavedPlayerValues.Uid != data.Source.CharacterUid)
            { reason = "capturedNativePrefixPlanIdentityMismatch"; return false; }
            return TryPrepareCore(connection, captured, data.Character, plan, out prepared, out reason);
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException or KeyNotFoundException or IndexOutOfRangeException)
        { reason = "capturedNativePrefixInvalidData"; return false; }
    }
}
