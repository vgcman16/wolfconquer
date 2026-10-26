namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record AccountPromotionOwner(WorldSession Expected, object Identity,
        Dictionary<string, string> Attributes, KeyValuePair<string, string>[] Fields,
        LegacyAccountManagerFullPromotionPersistence.Values Values);

    private static bool TryCaptureAccountPromotionOwner(WorldPacketHandlerContext context, WorldSession expected,
        out AccountPromotionOwner? owner, out string reason)
    {
        owner = null; reason = "accountManagerFullPromotionOwnerUnavailable";
        if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
            expected is null || expected.Character is null || expected.AccountId is <= 0 or > uint.MaxValue ||
            expected.Character.Uid is <= 0 or > uint.MaxValue || trades.HasConnectionEnded(context.Connection) ||
            context.TradeRecovery is not { } recovery || !recovery.IsReadyForUsers(expected.UsersRoot) ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, expected.Character.Uid, out var identity)) return false;
        if (expected.Character.Attributes is not Dictionary<string, string> attributes ||
            attributes.GetType() != typeof(Dictionary<string, string>))
        { reason = "accountManagerFullPromotionAttributesUnavailable"; return false; }
        var fields = attributes.ToArray();
        var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields)
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !captured.TryAdd(pair.Key, pair.Value))
            { reason = "accountManagerFullPromotionAttributesAmbiguous"; return false; }
        if (!LegacyAccountManagerFullPromotionPersistence.TryReadValues(captured, out var values) || values.Class != expected.Character.Class)
        { reason = "accountManagerFullPromotionValuesInvalid"; return false; }
        owner = new(expected, identity, attributes, fields, values);
        if (IsCurrentAccountPromotionOwner(context, owner, expected, fields)) { reason = string.Empty; return true; }
        owner = null; reason = "accountManagerFullPromotionOwnerChanged"; return false;
    }

    private static bool IsCurrentAccountPromotionOwner(WorldPacketHandlerContext context, AccountPromotionOwner owner,
        WorldSession expected, KeyValuePair<string, string>[] fields)
    {
        if (context.TradeRecovery?.IsReadyForUsers(owner.Expected.UsersRoot) != true ||
            expected.Character.Attributes is not Dictionary<string, string> attributes ||
            attributes.GetType() != typeof(Dictionary<string, string>) || attributes.Count != fields.Length ||
            context.Sessions is not { } sessions || !LegacyNativeClientPublication.TryCurrent(sessions,
                context.Connection, owner.Expected.Character.Uid, owner.Identity, out var current) ||
            !ReferenceEquals(current, expected)) return false;
        var index = 0;
        foreach (var pair in attributes)
            if (index == fields.Length || pair.Key != fields[index].Key || pair.Value != fields[index++].Value) return false;
        // Complete all collection/recovery reads before the final actual Store
        // and ended-connection checks. No native financial fields are invented:
        // this caller's authority is its managed Character snapshot.
        return index == fields.Length && sessions.TryGet(context.Connection, out current) &&
            ReferenceEquals(current, expected) && context.Trades is { } trades && !trades.HasConnectionEnded(context.Connection);
    }
}
