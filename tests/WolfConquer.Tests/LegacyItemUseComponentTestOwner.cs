using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual unbound component ownership, not ordinary/native admission. Call only
// during fixture setup, before deliberate stale-owner/PIN/failure inputs.
internal static class LegacyItemUseComponentTestOwner
{
    internal static WorldPacketHandlerContext Prepare(WorldPacketHandlerContext context,
        string root, ref WorldSession session)
    {
        if (!Path.IsPathFullyQualified(root) || context.Sessions is not { } store ||
            context.Connection.NativeGameClient is not null || session.NativeGameClient is not null ||
            !store.TryGet(context.Connection, out var current) || !ReferenceEquals(current, session))
            throw new InvalidOperationException("Current unbound item-use component fixture required.");
        var users = session.UsersRoot ?? Path.Combine(root, "Users");
        var items = context.PlayerItemsRoot ?? Path.Combine(root, "PlayersItems");
        Directory.CreateDirectory(users);
        Directory.CreateDirectory(items);
        var characterPath = Path.Combine(users, session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(characterPath))
        {
            var values = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var value in CharacterSnapshotScalarPersistence.BuildPersistedValues(session.Character))
                values[value.Key] = value.Value;
            values["Name"] = session.Character.Name;
            var text = "[Character]\n" + string.Join("\n", values.Select(value => value.Key + "=" + value.Value)) + "\n";
            if (session.Character.PaidNobility.Count != 0)
                text += "[PaidNobility]\n" + string.Join("\n", session.Character.PaidNobility.Select(value => value.Key + "=" + value.Value)) + "\n";
            File.WriteAllText(characterPath, text, new UTF8Encoding(false));
        }
        var itemPath = Path.Combine(items, session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        if (!File.Exists(itemPath)) new PlayerItemSnapshotLoader(items).SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
        session = session with { UsersRoot = users };
        store.Set(context.Connection, session);
        var recovery = new LegacyTradeRecoveryState(users, items);
        Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        Assert.True(recovery.IsReadyFor(users, items));
        return context with { Session = session, PlayerItemsRoot = items, TradeRecovery = recovery,
            TradeOperations = new WorldOperationBarrier(), Trades = new LegacyTradeStateStore() };
    }
}
