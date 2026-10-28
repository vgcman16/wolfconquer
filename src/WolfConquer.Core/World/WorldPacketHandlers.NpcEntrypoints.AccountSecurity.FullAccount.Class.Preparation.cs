using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacyFullAccountClassOwner
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Expected;
        internal readonly LegacyTradeRecoveryState Recovery;
        internal readonly LegacyFullAccountClassInputs Inputs;
        internal readonly LegacyNativeGameClientRuntime? Native;
        internal readonly LegacyNativePlayerRuntime? Player;
        internal readonly string UsersRoot, SpellsRoot;
        private readonly string usersLiteral, spellsLiteral;
        private readonly object identity;
        internal object Identity => identity;

        private LegacyFullAccountClassOwner(WorldPacketHandlerContext context, WorldSession expected, object identity)
        {
            Context = context; Expected = expected; this.identity = identity;
            Recovery = context.TradeRecovery!; Native = context.Connection.NativeGameClient; Player = Native?.Player;
            usersLiteral = expected.UsersRoot!; spellsLiteral = context.PlayerSpellsRoot!;
            UsersRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(usersLiteral));
            SpellsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(spellsLiteral));
            Inputs = new(expected);
        }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession expected,
            out LegacyFullAccountClassOwner? owner, out string reason)
        {
            owner = null; reason = "fullAccountClassOwnerChanged";
            if (string.IsNullOrWhiteSpace(expected.UsersRoot)) { reason = "users-root-missing"; return false; }
            if (string.IsNullOrWhiteSpace(context.PlayerSpellsRoot)) { reason = "player-spells-root-missing"; return false; }
            if (!Path.IsPathFullyQualified(expected.UsersRoot) || !Path.IsPathFullyQualified(context.PlayerSpellsRoot))
            { reason = "fullAccountClassRoutesNotAbsolute"; return false; }
            if (!context.Connection.HasSendRoute || !context.Connection.HasDisconnectRoute)
            { reason = "fullAccountClassSenderUnavailable"; return false; }
            if (context.Sessions is not { } sessions || context.Trades is not { } trades ||
                context.TradeRecovery is not { } recovery || expected.AccountId is <= 0 or > uint.MaxValue ||
                expected.Character.Uid is <= 0 or > uint.MaxValue ||
                !Monitor.IsEntered(trades.SyncRoot) || trades.HasConnectionEnded(context.Connection) || trades.Contains(context.Connection) ||
                expected.LegacyTradeActive || !sessions.TryGet(context.Connection, out var current) || !ReferenceEquals(current, expected) ||
                !recovery.IsReadyForCharacterSpells(expected.UsersRoot, context.PlayerSpellsRoot) ||
                !recovery.TryRequireLiveJournalClear(out reason) ||
                !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, expected.Character.Uid, out var identity)) return false;
            var captured = new LegacyFullAccountClassOwner(context, expected, identity);
            if (!LegacyTradeEscrowClaims.TryRead(captured.Inputs.Stable.Character.Attributes, out var claim, out reason) || claim is not null)
            { reason = "fullAccountClassEscrowUnresolved"; return false; }
            if (!captured.IsCurrent(expected, checkInputs: true)) { reason = "fullAccountClassOwnerChanged"; return false; }
            owner = captured; reason = string.Empty; return true;
        }

        internal bool IsCurrent(WorldSession expected, bool checkInputs)
        {
            if (!ReferenceEquals(Context.TradeRecovery, Recovery) ||
                !Recovery.IsReadyForCharacterSpells(UsersRoot, SpellsRoot) ||
                Context.PlayerSpellsRoot != spellsLiteral || Expected.UsersRoot != usersLiteral ||
                Context.Trades is not { } trades || !Monitor.IsEntered(trades.SyncRoot) ||
                trades.HasConnectionEnded(Context.Connection) || trades.Contains(Context.Connection) ||
                !LegacySecondaryPasswordRuntime.IsAdmitted(Context.Connection, Expected)) return false;
            if (checkInputs && !Inputs.Matches()) return false;
            return IsPlainCurrent(expected);
        }

        internal bool IsPlainCurrent(WorldSession expected) =>
            ReferenceEquals(Context.Connection.NativeGameClient, Native) && ReferenceEquals(expected.NativeGameClient, Native) &&
            (Native is null || Player is not null && ReferenceEquals(Native.Player, Player) &&
                ReferenceEquals(Player.Owner, Native) && Player.UID == Expected.Character.Uid) &&
            LegacyNativeClientPublication.TryCurrent(Context.Sessions!, Context.Connection, Expected.Character.Uid, identity, out var current) &&
            ReferenceEquals(current, expected) && current.AccountId == Expected.AccountId && current.UsersRoot == usersLiteral &&
            Context.Trades is { } trades && !trades.HasConnectionEnded(Context.Connection);

        internal bool PeerCurrent(ConquerTcpConnection connection, long uid, object peerIdentity, out WorldSession current) =>
            LegacyNativeClientPublication.TryCurrent(Context.Sessions!, connection, uid, peerIdentity, out current);
    }

    private sealed partial class LegacyFullAccountClassPrepared
    {
        internal readonly LegacyFullAccountClassOwner Owner;
        private readonly LegacyFullAccountClassGrant grant;
        internal LegacyFullAccountClassProjection Projection { get; }
        internal WorldSession Updated { get; set; }
        private readonly FullAccountClassFrame[] frames;
        private readonly FullAccountClassPeer[] peers;
        private readonly LegacyNativeMagicQueryFields nativeMagic;
        private readonly LegacyTradeJournalChange[] changes;
        private readonly LegacyPreparedSpellFileWrite spellWrite;
        private readonly string characterPath, spellsPath;
        internal bool DurableCommitted { get; private set; }

        internal LegacyFullAccountClassPrepared(LegacyFullAccountClassOwner owner, LegacyFullAccountClassGrant grant)
        {
            Owner = owner; this.grant = grant;
            if (owner.Expected.Character.Level != LegacyFullAccountClassTargetLevel && owner.Context.WorldData is null)
                throw new FullAccountClassRefusal("world-data-missing-for-update-level");
            if (!TryCreateLegacyFullAccountClassProjection(owner.Context, owner.Inputs.Stable, grant, out var projection, out var reason))
                throw new FullAccountClassRefusal(reason);
            var spells = new PlayerSpellSnapshot(new LegacyPerformerSpellRows(projection.UpdatedSpells.Spells.ToArray()));
            Projection = projection with { UpdatedSpells = spells, RemovedSpellIds = projection.RemovedSpellIds.ToArray(),
                AddedSpellPackets = projection.AddedSpellPackets.ToArray() };
            Updated = owner.Expected with { Character = projection.UpdatedCharacter, PlayerSpells = spells };
            frames = BuildFullAccountClassFrames(owner, Projection, grant);
            nativeMagic = Projection.LevelChanged
                ? LegacyNativeMagicQueryClient.Read(owner.Context.Sessions, owner.Context.Connection,
                    owner.Expected.Character.Uid, owner.Identity) : default;
            peers = CaptureFullAccountClassPeers(owner, Projection);
            spellWrite = owner.Context.Sessions!.PrepareSpellFileWrite(owner.Context.Connection, owner.Expected,
                owner.Recovery, owner.UsersRoot, owner.SpellsRoot);
            spellWrite.PrepareImage(Updated.PlayerSpells);
            (characterPath, spellsPath, changes) = PrepareFullAccountClassImages(owner, Updated, spellWrite);
            if (!owner.IsCurrent(owner.Expected, checkInputs: true)) throw InvalidFullAccountClassState();
        }

        private bool MetadataCurrent()
        {
            if (!TryCreateLegacyFullAccountClassProjection(Owner.Context, Owner.Inputs.Stable, grant, out var current, out _)) return false;
            var actual = BuildFullAccountClassFrames(Owner, current, grant);
            if (actual.Length != frames.Length || !FullAccountCharacterScalarsEqual(current.UpdatedCharacter, Projection.UpdatedCharacter) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(current.UpdatedSpells).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(Projection.UpdatedSpells))) return false;
            for (var index = 0; index < frames.Length; index++)
                if (actual[index].Visible != frames[index].Visible || !actual[index].Bytes.AsSpan().SequenceEqual(frames[index].Bytes)) return false;
            return true;
        }

        // Inputs.Matches may acquire a ReadOnlyDictionary's real underlying
        // enumerator. All such callouts must precede this actual cache read.
        private bool NativeMagicCurrent() => !Projection.LevelChanged ||
            LegacyNativeMagicQueryClient.Read(Owner.Context.Sessions, Owner.Context.Connection,
                Owner.Expected.Character.Uid, Owner.Identity) == nativeMagic;
    }

    private sealed class FullAccountClassRefusal(string reason) : InvalidOperationException(reason) { }
}
