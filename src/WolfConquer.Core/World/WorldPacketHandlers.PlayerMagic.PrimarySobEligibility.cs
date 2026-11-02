using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record SobPrimaryAdmissionData(WorldPacketHandlerContext Context, WorldSession Actor,
        object? Identity, MsgAttackQuery Query, LegacyMagicRuntimeSpell Spell,
        SobNpcRuntimeStateStore.MagicPrimaryTarget Target,
        LegacyGuildWarRuntimeStateStore.PermissionObservation? GuildPermission)
    {
        internal bool InputsMatch(WorldSessionStore? store, ConquerTcpConnection connection,
            WorldSession expected, object? identity) =>
            ReferenceEquals(Context.Sessions, store) && ReferenceEquals(Context.Connection, connection) &&
            ReferenceEquals(Actor, expected) && ReferenceEquals(Identity, identity) &&
            Context.WorldData is { } data && data.SobNpcsById.TryGetValue(Target.Source.Id, out var rows) && rows is not null &&
            rows.Any(row => ReferenceEquals(row, Target.Source)) &&
            CanSeeSobNpc(expected.Character, Target.Visible) &&
            PassesLegacyPlayerSobNpcMagicPreResourceRangeGate(expected, Target.Visible, Query, Spell, out _);

        internal bool TryAttachGuildPermission(SobNpcRuntimeStateStore.MagicPrimaryLease targetLease)
        {
            if (GuildPermission is null) return true;
            if (!GuildPermission.TryEnter(out var guildLease) || guildLease is null) return false;
            try { targetLease.AttachGuildPermission(guildLease); return true; }
            catch { guildLease.Dispose(); throw; }
        }
        internal bool IsEligibleHeld(out string reason)
        {
            reason = "sobPrimaryTargetChanged";
            if (!Target.IsCurrentHeld() || !InputsMatch(Context.Sessions, Context.Connection, Actor, Identity)) return false;
            if (!CanLegacySobNpcMagicDamageTarget(Context, Actor.Character, Target.Visible, out reason)) return false;
            return Target.IsCurrentHeld();
        }
    }

    // Only the genuine captured admission factory associates this opaque key.
    // The SOB observation alone cannot authorize an actor cost publication.
    internal sealed class LegacySobPrimaryAdmission
    {
        internal bool TryReadTarget(WorldSessionStore? store, ConquerTcpConnection connection,
            WorldSession expected, object? identity, out SobNpcRuntimeStateStore.MagicPrimaryTarget? target)
        {
            target = null;
            if (!sobPrimaryAdmissions.TryGetValue(this, out var data) ||
                !data.InputsMatch(store, connection, expected, identity)) return false;
            target = data.Target;
            return true;
        }

        internal bool TryEnterForCost(WorldSessionStore? store, ConquerTcpConnection connection,
            WorldSession? expected, out SobNpcRuntimeStateStore.MagicPrimaryLease? lease, out string reason)
        {
            lease = null; reason = "sobPrimaryTargetChangedOrBusy";
            if (expected is null || !sobPrimaryAdmissions.TryGetValue(this, out var data) ||
                !data.InputsMatch(store, connection, expected, data.Identity) || !data.Target.TryEnter(out lease)) return false;
            try
            {
                if (data.TryAttachGuildPermission(lease!) && data.IsEligibleHeld(out reason) && lease!.IsCurrentPlain()) return true;
                lease!.Dispose(); lease = null;
                return false;
            }
            catch { lease!.Dispose(); lease = null; throw; }
        }
    }

    private static readonly ConditionalWeakTable<LegacySobPrimaryAdmission, SobPrimaryAdmissionData> sobPrimaryAdmissions = new();

    private static bool TryCaptureLegacySobPrimaryAdmission(WorldPacketHandlerContext context,
        LegacyRootAdmissionOwner owner, MsgAttackQuery query, LegacyMagicRuntimeSpell spell,
        bool selectedSobExecutor, out string reason)
    {
        reason = string.Empty;
        if (spell.Sort is not (LegacyMagicSort.Attack or LegacyMagicSort.DirectAttack)) return true;
        SobNpc? source;
        if (selectedSobExecutor)
        {
            if (!TryFindVisibleSobNpcByUid(context, owner.Expected.Character, query.OpponentUid, out source))
            { reason = "mandatory SOB primary is not visible"; return false; }
        }
        else
        {
            // Match Root's raw-map Monster -> Player -> SOB resolution.
            if (FindLegacyHellVortexSelectedMonster(context, owner.Expected, query.OpponentUid) is not null ||
                context.Sessions is { } world && FindLegacyMapBlockPlayer(world, owner.Expected, query.OpponentUid) is not null) return true;
            source = FindLegacyRootRawSobPrimary(context, owner.Expected, query.OpponentUid);
            if (source is null) return true;
        }
        LegacyGuildWarRuntimeStateStore.PermissionObservation? guildPermission = null;
        if (LegacySobNpcEventRules.IsNewGuildWarAuthorizationSob(source))
        {
            if (context.GuildWarStates is { } manager)
            {
                if (!manager.TryCapturePermission(out guildPermission) || guildPermission is null)
                { reason = "guildWarPermissionBusy"; return false; }
            }
            else if (owner.Store is not null && owner.Expected.NativeGameClient?.OrdinarySpellBinding is not null)
            {
                // Persisted character attributes cannot replace a required live
                // event owner on the genuine admitted/native server route.
                reason = "guildWarPermissionOwnerRequired";
                return false;
            }
        }
        if (context.SobNpcStates is not { } states)
        { reason = "SOB runtime state is not loaded"; return false; }
        if (!states.TryCaptureMagicPrimaryTarget(source, out var initial) || initial is null)
        { reason = "SOB primary observation is busy or has changed"; return false; }
        if (!CanSeeSobNpc(owner.Expected.Character, initial.Visible) ||
            !PassesLegacyPlayerSobNpcMagicPreResourceRangeGate(owner.Expected, initial.Visible, query, spell, out reason)) return false;
        if (!CanLegacySobNpcMagicDamageTarget(context, owner.Expected.Character, initial.Visible, out reason)) return false;

        // Permission reads may legitimately initialize an event's runtime cache.
        // Recapture the actual producer afterward; never adopt equal-looking data.
        if (!states.TryCaptureMagicPrimaryTarget(source, out var target) || target is null || !target.TryEnter(out var lease))
        { reason = "SOB primary changed during permission preparation"; return false; }
        using (lease)
        {
            var data = new SobPrimaryAdmissionData(context, owner.Expected, owner.Identity, query, spell, target, guildPermission);
            if (!data.TryAttachGuildPermission(lease!) || !data.InputsMatch(owner.Store, owner.Connection, owner.Expected, owner.Identity) ||
                !data.IsEligibleHeld(out reason) || !lease!.IsCurrentPlain())
            { reason = "SOB primary changed during permission preparation"; return false; }
            var receipt = new LegacySobPrimaryAdmission();
            sobPrimaryAdmissions.Add(receipt, data);
            guildPermission?.RecordPreparedHeld();
            owner.SobPrimary = receipt;
            return true;
        }
    }

    private static SobNpc? FindLegacyRootRawSobPrimary(WorldPacketHandlerContext context,
        WorldSession actor, uint uid)
    {
        if (uid > int.MaxValue || context.WorldData is not { } data || !data.SobNpcsById.TryGetValue((int)uid, out var rows)) return null;
        if (rows is null) throw new InvalidDataException("SOB primary catalog group is null.");
        foreach (var row in rows)
        {
            if (row is null) throw new InvalidDataException("SOB primary catalog row is null.");
            if (row.MapId == actor.Character.MapId && Math.Abs(row.X / 18 - actor.Character.X / 18) <= 1 &&
                Math.Abs(row.Y / 18 - actor.Character.Y / 18) <= 1) return row;
        }
        return null;
    }
}
