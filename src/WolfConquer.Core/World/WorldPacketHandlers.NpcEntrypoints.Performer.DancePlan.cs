using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class LegacyPerformerDanceOwner
    {
        internal readonly WorldPacketHandlerContext Context;
        internal readonly WorldSession Expected;
        internal readonly LegacyNativeGameClientRuntime? Native;
        internal readonly LegacyNativePlayerRuntime? Player;
        private readonly object identity;

        private LegacyPerformerDanceOwner(WorldPacketHandlerContext context, WorldSession expected, object identity)
        {
            Context = context; Expected = expected; this.identity = identity;
            Native = context.Connection.NativeGameClient; Player = Native?.Player;
        }

        internal static bool TryCapture(WorldPacketHandlerContext context, WorldSession expected,
            out LegacyPerformerDanceOwner? owner)
        {
            owner = null;
            if (expected.Character.Uid is <= 0 or > uint.MaxValue || expected.PlayerSpells?.Spells is null ||
                context.Sessions is not { } sessions ||
                !LegacyNativeClientPublication.TryCaptureIdentity(sessions, context.Connection, expected.Character.Uid, out var identity))
                return false;
            var captured = new LegacyPerformerDanceOwner(context, expected, identity);
            if (!captured.IsCurrent(expected)) return false;
            owner = captured;
            return true;
        }

        internal bool IsCurrent(WorldSession expected) =>
            ReferenceEquals(Context.Connection.NativeGameClient, Native) && ReferenceEquals(expected.NativeGameClient, Native) &&
            (Native is null || Player is not null && ReferenceEquals(Native.Player, Player) &&
                ReferenceEquals(Player.Owner, Native) && Player.UID == expected.Character.Uid) &&
            LegacyNativeClientPublication.TryCurrent(Context.Sessions!, Context.Connection, Expected.Character.Uid, identity, out var current) &&
            ReferenceEquals(current, expected) && current.AccountId == Expected.AccountId &&
            current.Character.Uid == Expected.Character.Uid;
    }

    private sealed class LegacyPreparedPerformerDance
    {
        private readonly LegacyPerformerDanceOwner owner;
        private readonly LegacyPerformerSpellSource source;
        private readonly string subProfInfo, path;
        private readonly byte[] beforeImage, completeImage;
        private readonly byte[][] frames;
        internal WorldSession Updated { get; }

        internal LegacyPreparedPerformerDance(LegacyPerformerDanceOwner owner, LegacyPerformerSpellSource source, string root,
            LegacyPerformerDanceDefinition dance, byte phrase)
        {
            this.owner = owner; this.source = source;
            if (source.Rows.Length == ushort.MaxValue || source.Rows.Any(row => row.Id == dance.SpellId) ||
                !owner.Expected.Character.Attributes.TryGetValue("SubProfInfo", out var rawState) ||
                !LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var state) ||
                (GetLegacySubclassEntry(state, LegacySubclassMasterPerformerId)?.Phrase ?? 0) != phrase || phrase < dance.RequiredPhase)
                throw InvalidPerformerDanceState();
            subProfInfo = rawState;

            var before = new PlayerSpellSnapshot(new LegacyPerformerSpellRows(source.Rows));
            var learned = new PlayerSpellSnapshotEntry(dance.SpellId, 0, 0, 0, 0, 0);
            var added = LegacySkillBookUse.AddOrUpdateSpell(before, learned);
            var after = new PlayerSpellSnapshot(new LegacyPerformerSpellRows(added.Spells.ToArray()));
            beforeImage = PlayerSpellSnapshotLoader.ToLegacyBytes(before);
            completeImage = PlayerSpellSnapshotLoader.ToLegacyBytes(after);
            path = Path.Combine(root, owner.Expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            Updated = owner.Expected with { PlayerSpells = after };
            frames =
            [
                LegacySpellPacket.FromSnapshot(learned).ToFrameBytes(),
                LegacyStringPacket.PlayerEffect(checked((uint)Updated.Character.Uid), "zf2-e300").ToFrameBytes(),
                LegacyMsgMessagePacket.SystemTalk(dance.SystemMessage).ToFrameBytes(),
                LegacyNpcReplyPacket.ForDialog(dance.SuccessDialogText).ToFrameBytes(),
                LegacyNpcReplyPacket.ForOption(dance.SuccessOptionText, LegacyNpcClosedOption).ToFrameBytes(),
                LegacyNpcReplyPacket.ForAvatar(LegacyPerformerAvatarId).ToFrameBytes(),
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
            ];
            if (!InputsCurrent()) throw InvalidPerformerDanceState();
        }

        private bool InputsCurrent()
        {
            // The actual known backing is compared AFTER the last caller
            // attribute/comparer read, without invoking its indexer/enumerator.
            return owner.Expected.Character.Attributes.TryGetValue("SubProfInfo", out var current) &&
                current == subProfInfo && source.IsCurrent() && owner.IsCurrent(owner.Expected);
        }

        internal bool TryCommit(CancellationToken cancellationToken, out string reason)
        {
            var failure = "sessionChanged";
            var committed = owner.Context.Sessions!.TryCommitSession(owner.Context.Connection, owner.Expected, Updated, () =>
            {
                if (owner.Native is { } native)
                {
                    lock (native.SpellAdmissionSyncRoot)
                    lock (owner.Player!.SpellAdmissionSyncRoot) return Persist();
                }
                return Persist();
            });
            reason = failure;
            return committed;

            bool Persist()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!InputsCurrent()) return false;
                return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                {
                    if (!TryPreparePerformerDanceImage(path, beforeImage, completeImage, out var image))
                    { failure = "savedSpellsChanged"; return false; }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!InputsCurrent()) return false;
                    SnapshotFilePersistence.WriteAllBytesAtomically(path, image!, ".player-spell-file-");
                    return true;
                });
            }
        }

        internal async ValueTask<bool> PublishAsync(WorldPacketHandlerContext context, CancellationToken cancellationToken)
        {
            foreach (var frame in frames)
            {
                if (!owner.IsCurrent(Updated)) return false;
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!owner.IsCurrent(Updated)) return false;
            }
            return true;
        }
    }

    private static InvalidOperationException InvalidPerformerDanceState() =>
        new("Performer learning requires complete stable current spell and subclass state.");
}
