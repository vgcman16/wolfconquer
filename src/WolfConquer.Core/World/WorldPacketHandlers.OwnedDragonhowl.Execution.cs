using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class OwnedDragonhowlPreparation
    {
        private int executionStarted, completedOutputPhases;
        internal bool HasCompletedActualOutput => Volatile.Read(ref executionStarted) == 1 &&
            Volatile.Read(ref completedOutputPhases) == Phases.Count;
        internal async ValueTask<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref executionStarted, 1, 0) != 0) return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < Phases.Count; index++)
                {
                    if (!Store.TryCommitDragonhowlPhase(this, index, out var published) || published is null)
                        throw new InvalidOperationException("ownedDragonhowlPreparedPhaseRefused");
                    var phase = Phases[index];
                    if (phase.Death is not null && !await Store.PublishDragonhowlDeathAsync(this, index, cancellationToken).ConfigureAwait(false))
                        throw new InvalidOperationException("ownedDragonhowlDeathOutputOwnerChanged");
                    if (phase.NonPlayer is { } nonPlayer)
                    {
                        bool complete = phase.MonsterOutput is { } monster
                            ? await monster.PublishAsync(this, nonPlayer, cancellationToken).ConfigureAwait(false)
                            : phase.SobEvent is not null
                                ? await Store.PublishDragonhowlSobEventAsync(this, nonPlayer, cancellationToken).ConfigureAwait(false)
                                : phase.SobOutput is { } sob && await sob.PublishAsync(this, nonPlayer, cancellationToken).ConfigureAwait(false);
                        if (!complete) throw new InvalidOperationException("ownedDragonhowlNonPlayerOutputOwnerChanged");
                    }
                    foreach (var frame in phase.Frames)
                        await SendPhaseAsync(phase.Connection, published, phase.Audience, frame, cancellationToken).ConfigureAwait(false);
                    Volatile.Write(ref completedOutputPhases, index + 1);
                }
                if (!Store.TryReadDragonhowlCurrent(this, Context.Connection, out var actor))
                    throw new InvalidOperationException("ownedDragonhowlAnimationOwnerLost");
                if (Selection.SpellId == 17_650)
                {
                    // Ironbone's first pass excludes self; its second includes
                    // self followed by the same source-visible peers.
                    foreach (var frame in animation.ToFrames())
                        await SendVisibleAsync(Context.Connection, actor, frame, includeSelf: false, cancellationToken).ConfigureAwait(false);
                    foreach (var frame in animation.ToFrames())
                        await SendVisibleAsync(Context.Connection, actor, frame, includeSelf: true, cancellationToken).ConfigureAwait(false);
                }
                else if (animation.Targets.Count != 0)
                    foreach (var frame in animation.ToFrames())
                        await SendVisibleAsync(Context.Connection, actor, frame, includeSelf: true, cancellationToken).ConfigureAwait(false);
                if (!Store.CompleteDragonhowl(this)) throw new InvalidOperationException("ownedDragonhowlCompletionChanged");
                return true;
            }
            catch (Exception error) { Store.ThrowDragonhowlFailure(this, error); throw; }
            finally { Store.EndDragonhowl(this); }
        }

        private async ValueTask SendPhaseAsync(ConquerTcpConnection connection, WorldSession source,
            DragonhowlAudience audience, byte[] frame, CancellationToken token)
        {
            if (audience == DragonhowlAudience.Owner)
            { await SendCheckedAsync(connection, frame, token).ConfigureAwait(false); return; }
            await SendVisibleAsync(connection, source, frame, includeSelf: true, token).ConfigureAwait(false);
        }

        private async ValueTask SendVisibleAsync(ConquerTcpConnection connection, WorldSession source,
            byte[] frame, bool includeSelf, CancellationToken token)
        {
            if (includeSelf) await SendCheckedAsync(connection, frame, token).ConfigureAwait(false);
            foreach (var candidate in Store.Snapshot())
            {
                if (ReferenceEquals(candidate.Connection, connection) || candidate.Session.Character.Uid == source.Character.Uid ||
                    !CanSeePlayer(source.Character, candidate.Session.Character) ||
                    ReadCharacterDynamicId(source.Character) != ReadCharacterDynamicId(candidate.Session.Character)) continue;
                // Observers receive output but do not become action participants.
                if (!Store.TryGet(candidate.Connection, out var current) || !ReferenceEquals(current, candidate.Session)) continue;
                await SendCheckedAsync(candidate.Connection, frame, token).ConfigureAwait(false);
            }
        }

        private async ValueTask SendCheckedAsync(ConquerTcpConnection recipient, byte[] frame, CancellationToken token)
        {
            if (!Store.TryReadDragonhowlCurrent(this, Context.Connection, out _))
                throw new InvalidOperationException("ownedDragonhowlOutputOwnerChanged");
            await recipient.SendAsync(frame.ToArray(), token).ConfigureAwait(false);
            if (!Store.TryReadDragonhowlCurrent(this, Context.Connection, out _))
                throw new InvalidOperationException("ownedDragonhowlOutputOwnerChanged");
        }
    }
}
