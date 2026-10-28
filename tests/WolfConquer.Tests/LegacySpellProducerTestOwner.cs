using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Genuine UNBOUND component setup. No native owner/binding, accepting saver,
// queue or ordinary UID grant is fabricated. Call before deliberate fault setup.
internal static class LegacySpellProducerTestOwner
{
    internal static WorldPacketHandlerContext PrepareContext(WorldPacketHandlerContext context, ref WorldSession session)
    {
        if (context.Sessions is not { } sessions || context.PlayerSpellsRoot is not { } spells ||
            !Path.IsPathFullyQualified(spells) || context.Connection.NativeGameClient is not null || session.NativeGameClient is not null ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, session))
            throw new InvalidOperationException("Explicit current unbound spell fixture is required.");
        var users = session.UsersRoot ?? Path.Combine(Path.GetDirectoryName(spells)!, Path.GetFileName(spells) + "-Users");
        Directory.CreateDirectory(users);
        var path = Path.Combine(users, session.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(path))
        {
            var values = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(session.Character)) values[pair.Key] = pair.Value;
            values["Name"] = session.Character.Name;
            var text = "[Character]\n" + string.Join("\n", values.Select(pair => pair.Key + "=" + pair.Value)) + "\n";
            if (session.Character.PaidNobility.Count != 0)
                text += "[PaidNobility]\n" + string.Join("\n", session.Character.PaidNobility.Select(pair => pair.Key + "=" + pair.Value)) + "\n";
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        var disk = CharacterSnapshotParser.ParseText(File.ReadAllText(path));
        var attributes = new Dictionary<string, string>(disk.Attributes, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in session.Character.Attributes) attributes[pair.Key] = pair.Value;
        session = session with { UsersRoot = users, Character = session.Character with { Attributes = attributes } };
        if (!File.Exists(spells))
        {
            Directory.CreateDirectory(spells);
            var spellPath = Path.Combine(spells, session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            if (!File.Exists(spellPath)) new PlayerSpellSnapshotLoader(spells).SaveByCharacterUid(session.Character.Uid, session.PlayerSpells);
        }
        var items = context.PlayerItemsRoot;
        if (items is not null && !File.Exists(items))
        {
            Directory.CreateDirectory(items);
            var itemPath = Path.Combine(items, session.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            if (!File.Exists(itemPath)) new PlayerItemSnapshotLoader(items).SaveByCharacterUid(session.Character.Uid, session.PlayerItems);
        }
        var recovery = new LegacyTradeRecoveryState(users, items, null, null, null, spells, null);
        Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
        sessions.Set(context.Connection, session);
        return context with { Session = session, TradeRecovery = recovery, Trades = context.Trades ?? new LegacyTradeStateStore() };
    }
}
