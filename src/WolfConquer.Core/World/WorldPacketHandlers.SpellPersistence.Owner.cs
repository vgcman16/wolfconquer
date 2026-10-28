using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacySpellFileOwner
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Expected;
        internal readonly LegacyTradeRecoveryState Recovery;
        internal readonly LegacyNativeGameClientRuntime? Native;
        internal readonly LegacyNativePlayerRuntime? Player;
        internal readonly string UsersRoot, SpellsRoot;
        private readonly string usersLiteral, spellsLiteral;
        private readonly object identity;
        private readonly bool paid;

        private LegacySpellFileOwner(WorldPacketHandlerContext context, WorldSession expected, object identity, bool paid)
        {
            Context = context; Expected = expected; this.identity = identity; this.paid = paid;
            Recovery = context.TradeRecovery!; Native = context.Connection.NativeGameClient; Player = Native?.Player;
            usersLiteral = expected.UsersRoot!; spellsLiteral = context.PlayerSpellsRoot!;
            UsersRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(usersLiteral));
            SpellsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(spellsLiteral));
        }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession expected, bool paid,
            out LegacySpellFileOwner? owner, out string reason)
        {
            owner = null; reason = "spellMutationOwnerUnavailable";
            if (context.Sessions is not { } sessions || context.TradeRecovery is not { } recovery ||
                expected.Character is null || expected.PlayerSpells?.Spells is null ||
                expected.AccountId is <= 0 or > uint.MaxValue || expected.Character.Uid is <= 0 or > uint.MaxValue ||
                string.IsNullOrWhiteSpace(expected.UsersRoot) || string.IsNullOrWhiteSpace(context.PlayerSpellsRoot) ||
                !Path.IsPathFullyQualified(expected.UsersRoot) || !Path.IsPathFullyQualified(context.PlayerSpellsRoot) ||
                !context.Connection.HasSendRoute || !sessions.TryGet(context.Connection, out var current) ||
                !ReferenceEquals(current, expected) || !recovery.IsReadyForCharacterSpells(expected.UsersRoot, context.PlayerSpellsRoot) ||
                !recovery.TryRequireLiveJournalClear(out reason) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, expected.Character.Uid, out var identity)) return false;
            var captured = new LegacySpellFileOwner(context, expected, identity, paid);
            if (!captured.IsCurrent()) return false;
            owner = captured; reason = string.Empty; return true;
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(Context.TradeRecovery, Recovery) || Context.PlayerSpellsRoot != spellsLiteral ||
                Expected.UsersRoot != usersLiteral || !Recovery.IsReadyForCharacterSpells(UsersRoot, SpellsRoot)) return false;
            if (Context.Trades is { } trades && (!Monitor.IsEntered(trades.SyncRoot) || trades.HasConnectionEnded(Context.Connection))) return false;
            if (paid && (Context.Trades is not { } paidTrades || paidTrades.Contains(Context.Connection) ||
                Expected.LegacyTradeActive || !LegacySecondaryPasswordRuntime.IsAdmitted(Context.Connection, Expected))) return false;
            return DeliveryIsCurrent(Expected);
        }

        internal bool MatchesIdentity(object expectedIdentity) => ReferenceEquals(identity, expectedIdentity);

        internal bool DeliveryIsCurrent(WorldSession expected) => !Recovery.IsBlocked &&
            ReferenceEquals(Context.Connection.NativeGameClient, Native) && ReferenceEquals(expected.NativeGameClient, Native) &&
            (Native is null || Player is not null && ReferenceEquals(Native.Player, Player) &&
                ReferenceEquals(Player.Owner, Native) && Player.UID == Expected.Character.Uid &&
                (Native.OrdinarySpellBinding is not { } binding ||
                    ReferenceEquals(binding.Store, Context.Sessions) && ReferenceEquals(binding.Recovery, Recovery) &&
                    string.Equals(binding.UsersRoot, UsersRoot, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(binding.SpellsRoot, SpellsRoot, StringComparison.OrdinalIgnoreCase) &&
                    binding.Store.IsBoundSpellOwnerCurrent(binding))) &&
            LegacyNativeClientPublication.TryCurrent(Context.Sessions!, Context.Connection, Expected.Character.Uid, identity, out var current) &&
            ReferenceEquals(current, expected) && current.AccountId == Expected.AccountId && current.UsersRoot == usersLiteral;
    }
}
