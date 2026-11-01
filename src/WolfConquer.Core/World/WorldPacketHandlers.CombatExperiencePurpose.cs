using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record CombatExperienceSource(WorldPacketHandlerContext Context,
        ConquerTcpConnection Recipient, WorldSession Expected, ulong Gain,
        TeamSessionStateStore.CombatExperienceRoster? Roster);
    private static readonly ConditionalWeakTable<OwnedCombatExperience, CombatExperienceSource> CombatExperienceSources = new();

    // Only the actual private combat-experience producer registers instances.
    internal sealed class OwnedCombatExperience
    {
        internal TeamSessionStateStore.CombatExperienceRoster? Roster =>
            CombatExperienceSources.TryGetValue(this, out var source) ? source.Roster : null;
        private LegacyOrdinarySpellBinding? binding;
        private WorldSession? proposed;
        internal void Bind(LegacyOrdinarySpellBinding owner, WorldSession candidate)
        {
            if (binding is not null || proposed is not null || !CombatExperienceSources.TryGetValue(this, out var source) ||
                !ReferenceEquals(source.Expected.NativeGameClient, owner.Client) || !ReferenceEquals(source.Recipient, owner.Connection))
                throw new InvalidOperationException("combatExperiencePurposeInvalid");
            binding = owner; proposed = candidate;
        }
        internal bool CoversAttributes(LegacyOrdinarySpellBinding owner, WorldSession expected, WorldSession candidate) =>
            CombatExperienceSources.TryGetValue(this, out var source) && ReferenceEquals(source.Expected, expected) &&
            ReferenceEquals(binding, owner) && ReferenceEquals(proposed, candidate) &&
            owner.Store.IsBoundSpellOwnerCurrent(owner) && owner.Store.TryGet(owner.Connection, out var current) &&
            ReferenceEquals(current, expected);
        internal bool IsCurrentPlain() => CombatExperienceSources.TryGetValue(this, out var source) &&
            binding is { } owner && proposed is { } candidate && CoversAttributes(owner, source.Expected, candidate);
        internal bool CoversRebuild(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding owner,
            WorldSession expected, WorldSession candidate) =>
            CombatExperienceSources.TryGetValue(this, out var source) && CoversAttributes(owner, expected, candidate) &&
            ReferenceEquals(context.Connection, source.Recipient) && ReferenceEquals(context.Sessions, source.Context.Sessions) &&
            ReferenceEquals(context.WorldData, source.Context.WorldData) &&
            ReferenceEquals(context.TradeOperations, source.Context.TradeOperations) &&
            ReferenceEquals(context.FrameOperationLease, source.Context.FrameOperationLease) &&
            context.TradeOperations?.OwnsActiveExclusiveLease(context.FrameOperationLease) == true;
        internal bool Covers(WorldSessionStore store, WorldPacketHandlerContext context,
            ConquerTcpConnection recipient, WorldSession expected, ulong gain) =>
            CombatExperienceSources.TryGetValue(this, out var source) &&
            ReferenceEquals(source.Context, context) && ReferenceEquals(source.Context.Sessions, store) && ReferenceEquals(context.Sessions, store) &&
            ReferenceEquals(source.Context.WorldData, context.WorldData) &&
            ReferenceEquals(source.Recipient, recipient) && ReferenceEquals(source.Expected, expected) && source.Gain == gain;
    }

    private static OwnedCombatExperience IssueCombatExperience(WorldPacketHandlerContext context,
        ConquerTcpConnection recipient, WorldSession expected, ulong gain,
        TeamSessionStateStore.CombatExperienceRoster? roster)
    {
        var token = new OwnedCombatExperience();
        CombatExperienceSources.Add(token, new(context, recipient, expected, gain, roster));
        return token;
    }

    internal static byte[][] PrepareCombatExperienceFrames(LegacyCombatExperienceProjection projection)
    {
        var character = projection.After;
        var uid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var frames = new List<byte[]>();
        if (projection.LevelChanged)
        {
            frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)character.Level)).ToFrameBytes());
            if (projection.StatProgression.AppliedSourceStats)
                frames.AddRange(LegacyMsgUpdatePacket.CreateLegacyRuntimeSourceStatUpdates(uid, character).Select(row => row.ToFrameBytes()));
            if (projection.StatProgression.AddedAttributePoints)
                frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeAttributePointsUpdate(uid,
                    CheckedUInt64(character.AttributePoints, nameof(character.AttributePoints))).ToFrameBytes());
        }
        frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(uid, checked((ulong)character.Experience)).ToFrameBytes());
        return frames.ToArray();
    }

    private static async ValueTask SendCommittedCombatExperienceAsync(ConquerTcpConnection connection,
        WorldSession session, WorldSessionStore.ActionPublication receipt, byte[][] frames, CancellationToken cancellationToken)
    {
        bool Current() => receipt.TryCurrent(out var current) && ReferenceEquals(current, session) &&
            connection.NativeGameClient is { } client && ReferenceEquals(client, current.NativeGameClient) &&
            client.Player.Level == current.Character.Level && current.Character.Experience >= 0 &&
            client.Player.Experience == (ulong)current.Character.Experience;
        foreach (var bytes in frames)
        {
            if (!Current()) receipt.FenceCommittedAttributeFailure(new InvalidOperationException("combatExperienceOutputOwnerChanged"));
            try { await connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) { receipt.FenceCommittedAttributeFailure(error); throw; }
            if (!Current()) receipt.FenceCommittedAttributeFailure(new InvalidOperationException("combatExperienceOutputOwnerChanged"));
        }
    }

    private static async ValueTask<WorldSession> SendCommittedLevelExperienceAsync(ConquerTcpConnection connection,
        WorldSession committed, WorldSessionStore.ActionPublication receipt, PreparedLevelUpRebuild rebuild,
        byte[][] frames, CancellationToken cancellationToken)
    {
        try
        {
            if (frames.Length < 2) throw new InvalidOperationException("combatLevelExperienceFramesInvalid");
            for (int i = 0; i < frames.Length - 1; i++)
            {
                if (!receipt.TryCurrent(out var current) || !ReferenceEquals(current, committed))
                    throw new InvalidOperationException("combatLevelPrefixOwnerChanged");
                await connection.SendAsync(frames[i], cancellationToken).ConfigureAwait(false);
                if (!receipt.TryCurrent(out current) || !ReferenceEquals(current, committed))
                    throw new InvalidOperationException("combatLevelPrefixOwnerChanged");
            }
            var completed = await rebuild.RebuildAndRestoreAsync(receipt, cancellationToken).ConfigureAwait(false);
            if (completed is null || !completed.TryCurrent(out var result) || !ReferenceEquals(result, completed.Session) ||
                result.Character.Level != committed.Character.Level || result.Character.Experience != committed.Character.Experience)
                throw new InvalidOperationException("combatLevelRebuildUnproven");
            await connection.SendAsync(frames[^1], cancellationToken).ConfigureAwait(false);
            if (!completed.TryCurrent(out result) || !ReferenceEquals(result, completed.Session) ||
                result.Character.Level != committed.Character.Level || result.Character.Experience != committed.Character.Experience)
                throw new InvalidOperationException("combatLevelExperienceOwnerChanged");
            return result;
        }
        catch (Exception error)
        {
            receipt.FenceCommittedLevelUpFailure(error);
            throw;
        }
    }
}
