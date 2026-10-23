using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyVictoryChestPersistence
{
    internal sealed record Binding(WorldPacketHandlerContext Context, WorldSession Expected, object Identity,
        LegacyNativeGameClientRuntime? Client, LegacyNativePlayerRuntime? Player,
        Dictionary<string, string> Attributes, KeyValuePair<string, string>[] Fields, bool EscrowFree);
    internal sealed record Paths(string Users, string ItemsRoot, string QuestsRoot,
        string Character, string Items, string Quests);

    internal static bool TryCaptureBinding(WorldPacketHandlerContext context, WorldSession expected,
        out Binding binding, out string reason)
    {
        binding = null!;
        reason = Prefix + "CurrentOwnerUnavailable";
        if (expected is null || expected.Character is null || expected.AccountId is <= 0 or > uint.MaxValue ||
            expected.Character.Uid is <= 0 or > uint.MaxValue || context.Sessions is not { } sessions ||
            !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection,
                expected.Character.Uid, out var identity)) return false;
        var client = context.Connection.NativeGameClient;
        if (expected.Character.Attributes is not Dictionary<string, string> attributes ||
            attributes.GetType() != typeof(Dictionary<string, string>)) return false;
        var fields = attributes.ToArray();
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields)
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !unique.TryAdd(pair.Key, pair.Value))
            { reason = Prefix + "AttributesAmbiguous"; return false; }
        // Parse only a privately copied ordinary-comparer map. A foreign source
        // dictionary comparer is not invoked as last-moment grant authority.
        var escrowFree = LegacyTradeEscrowClaims.TryRead(unique, out var escrow, out _) && escrow is null;
        var captured = new Binding(context, expected, identity, client, client?.Player, attributes, fields, escrowFree);
        if (!IsCurrent(captured, expected)) return false;
        binding = captured;
        reason = string.Empty;
        return true;
    }

    // No party-dictionary lookup or monitor acquisition from a send continuation.
    internal static bool IsCurrent(Binding owner, WorldSession expected) =>
        (owner.Context.Trades is not { } trades || !trades.HasConnectionEnded(owner.Context.Connection)) &&
        owner.Context.Sessions is { } sessions &&
        LegacyNativeClientPublication.TryCurrent(sessions, owner.Context.Connection,
            owner.Expected.Character.Uid, owner.Identity, out var current) && ReferenceEquals(current, expected) &&
        ReferenceEquals(expected.NativeGameClient, owner.Client) &&
        ReferenceEquals(owner.Context.Connection.NativeGameClient, owner.Client) &&
        (owner.Client is null || owner.Player is not null && ReferenceEquals(owner.Client.Player, owner.Player) &&
            ReferenceEquals(owner.Player.Owner, owner.Client) && owner.Player.UID == owner.Expected.Character.Uid);

    internal static bool GrantOwnersCurrent(Binding owner)
    {
        var context = owner.Context;
        if (context.Trades is not { } trades || !Monitor.IsEntered(trades.SyncRoot) ||
            owner.Expected.LegacyTradeActive || trades.HasConnectionEnded(context.Connection) ||
            trades.TryGet(context.Connection, out var party) && (party.WindowOpen || party.Other is null ||
                party.Other.WindowOpen || party.Items.Count != 0 || party.Money != 0 || party.ConquerPoints != 0) ||
            context.TradeRecovery is not { } recovery ||
            !recovery.IsReadyFor(owner.Expected.UsersRoot, context.PlayerItemsRoot, context.PlayerQuestsRoot)) return false;
        if (!owner.EscrowFree || !AttributesCurrent(owner)) return false;
        // After readiness/root work, finish on the exact Store/native binding.
        return IsCurrent(owner, owner.Expected);
    }

    internal static bool AttributesCurrent(Binding owner)
    {
        if (!ReferenceEquals(owner.Expected.Character.Attributes, owner.Attributes) ||
            owner.Attributes.Count != owner.Fields.Length) return false;
        var index = 0;
        foreach (var pair in owner.Attributes)
        {
            if (index == owner.Fields.Length || !string.Equals(pair.Key, owner.Fields[index].Key, StringComparison.Ordinal) ||
                !string.Equals(pair.Value, owner.Fields[index].Value, StringComparison.Ordinal)) return false;
            index++;
        }
        return index == owner.Fields.Length;
    }

    private static bool TryPaths(Binding owner, out Paths paths, out string reason)
    {
        paths = null!;
        reason = Prefix + "StorageBindingInvalid";
        var expected = owner.Expected;
        var context = owner.Context;
        if (string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(context.PlayerItemsRoot) ||
            string.IsNullOrWhiteSpace(context.PlayerQuestsRoot) || !Path.IsPathFullyQualified(expected.UsersRoot) ||
            !Path.IsPathFullyQualified(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerQuestsRoot) ||
            !LegacyTradeJournal.IsQuestDomainConfigured(expected.UsersRoot, context.PlayerItemsRoot, context.PlayerQuestsRoot)) return false;
        var users = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected.UsersRoot));
        var items = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.PlayerItemsRoot));
        var quests = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.PlayerQuestsRoot));
        var name = expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        var character = Path.Combine(users, name);
        var source = expected.Character.SourceFile;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(source, name, comparison) && (string.IsNullOrWhiteSpace(source) ||
            !Path.IsPathFullyQualified(source) || !string.Equals(Path.GetFullPath(source), character, comparison))) return false;
        var leaf = expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin";
        paths = new(users, items, quests, character, Path.Combine(items, leaf), Path.Combine(quests, leaf));
        return true;
    }

    private static bool TryReadCharacter(Paths paths, WorldSession expected, out byte[] bytes, out string reason)
    {
        bytes = [];
        if (!CharacterSnapshotFileBaseline.TryRead(paths.Character, Prefix, out var raw, out reason)) return false;
        var text = CharacterSnapshotFileEncoding.Decode(raw!.Bytes, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(raw.Bytes))
        { reason = Prefix + "EncodingUnproven"; return false; }
        var parsed = CharacterSnapshotParser.ParseText(text, expected.Character.SourceFile);
        if (parsed.Uid != expected.Character.Uid || parsed.Money != expected.Character.Money ||
            parsed.ConquerPoints != expected.Character.ConquerPoints ||
            !LegacyTradeEscrowClaims.TryRead(parsed.Attributes, out var escrow, out _) || escrow is not null)
        { reason = Prefix + "CharacterBaselineStaleOrEscrow"; return false; }
        bytes = raw.Bytes; // Byte-identical no-op authority target, never a rewritten INI.
        reason = string.Empty;
        return true;
    }

    private static bool TryReadBaselines(Paths paths, WorldSession expected,
        LegacyDragonSkinInventory inventory, LegacyVictoryQuestCapture quests,
        out byte[] character, out byte[] items, out byte[] questBytes, out string reason)
    {
        character = []; items = []; questBytes = [];
        if (!TryReadCharacter(paths, expected, out character, out reason) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, Prefix, out items, out reason) ||
            !CharacterSnapshotFileBaseline.TryReadBytes(paths.Quests, Prefix, out questBytes, out reason)) return false;
        // Required quest files have no UID header. Their configured character leaf,
        // strict raw image and exact live graph are all necessary, not inferred ownership.
        if (!LegacyVictoryQuestCapture.TryParseImage(questBytes, out _, out reason) ||
            !quests.MatchesImage(questBytes) || !quests.IsCurrent(expected.Character.PlayerQuests))
        { if (reason.Length == 0) reason = Prefix + "QuestBaselineStale"; return false; }
        if (!items.AsSpan().SequenceEqual(inventory.Bytes) || !inventory.IsCurrent(expected.PlayerItems))
        { reason = Prefix + "ItemBaselineStale"; return false; }
        return true;
    }

    private static bool TryProveFiles(Paths paths, byte[] character, byte[] items, byte[] quests) =>
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Character, Prefix, out var c, out _) && c.AsSpan().SequenceEqual(character) &&
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Items, Prefix, out var i, out _) && i.AsSpan().SequenceEqual(items) &&
        CharacterSnapshotFileBaseline.TryReadBytes(paths.Quests, Prefix, out var q, out _) && q.AsSpan().SequenceEqual(quests);
}
