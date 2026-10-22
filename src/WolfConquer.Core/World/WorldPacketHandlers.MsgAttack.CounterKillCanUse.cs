using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Capture BEFORE MsgAttack's first awaited Focused/Intensify prefix, not after it.
    // A Store with a missing/rebound owner is never an unregistered/no-Store fallback.
    private sealed record LegacyCounterKillCanUseOwner(
        ConquerTcpConnection Connection, long Uid, WorldSessionStore? Store, object? NativeClientIdentity,
        LegacyNativeGameClientRuntime? PhysicalClient);

    private static bool TryCaptureLegacyCounterKillCanUseOwner(
        WorldPacketHandlerContext context, out LegacyCounterKillCanUseOwner? owner)
    {
        owner = null;
        if (context.Session is not { } session) return false;
        object? identity = null;
        if (context.Sessions is { } world &&
            !LegacyNativeClientPublication.TryCaptureIdentity(
                world, context.Connection, session.Character.Uid, out identity))
            return false;
        owner = new(context.Connection, session.Character.Uid, context.Sessions, identity,
            context.Connection.NativeGameClient); // Original physical CLIENT, not pre-prefix Player.
        return true;
    }

    private static bool IsLegacyCounterKillCanUseOwnerCurrent(
        WorldPacketHandlerContext context, LegacyCounterKillCanUseOwner owner)
    {
        if (!ReferenceEquals(owner.Connection, context.Connection) ||
            !ReferenceEquals(owner.Store, context.Sessions))
            return false;
        if (owner.Store is { } world)
            return owner.NativeClientIdentity is { } identity &&
                LegacyNativeClientPublication.TryCurrent(
                    world, owner.Connection, owner.Uid, identity, out _);
        return context.Session is { } local && local.Character.Uid == owner.Uid;
    }

    private static async ValueTask ClearLegacyCounterKillCanUseFocusedAsync(
        WorldPacketHandlerContext context, LegacyCounterKillCanUseOwner owner,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyCounterKillCanUseOwnerCurrent(context, owner) ||
            context.Session is not { } session ||
            !TryRefreshLegacyPlayerMagicResourceSession(
                context, owner.Uid, ref session, owner.NativeClientIdentity) ||
            !ReadAttributeBool(session.Character.Attributes, "doFocus", "DoFocus"))
            return;
        if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                current => current with
                {
                    Character = WithoutCharacterAttribute(
                        WithoutCharacterAttribute(current.Character, "doFocus"), "DoFocus"),
                }, owner.NativeClientIdentity))
            return;
        await context.Log.WriteLineAsync(
                "WorldServer cleared legacy Focused attack marker " +
                $"{FormatSession(context with { Session = session })} source=MsgAttackPacket.cs doFocus=false.")
            .ConfigureAwait(false);
        // Terminal log only. Cleanup must never reacquire a replacement generation.
    }

    private static async ValueTask<bool> HandleLegacyCounterKillSwitchAsync(
        WorldPacketHandlerContext context, MsgAttackPacket packet,
        LegacyCounterKillCanUseOwner? owner, CancellationToken cancellationToken)
    {
        if (packet.Query.AttackType != MsgAttackType.CounterKillSwitch ||
            context.Session is not { } session)
            return false;
        if (owner is null || !ReferenceEquals(owner.Connection, context.Connection) ||
            !ReferenceEquals(owner.Store, context.Sessions) ||
            session.Character.Uid != owner.Uid ||
            (owner.Store is not null && owner.NativeClientIdentity is null))
            return true;

        // Pinned generation with Store; explicit no-Store keeps the existing local-session model.
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(
            context, owner.Uid, ref session, owner.NativeClientIdentity);
        if (!Refresh()) return true;
        if (await TryRejectLegacyCounterKillProcessAdmissionAsync(
                context with { Session = session }, session, owner, cancellationToken).ConfigureAwait(false))
            return true;
        if (!Refresh()) return true; // The admission helper may await its terminal map denial.
        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CounterKill switch " +
                    $"{FormatSession(context with { Session = session })} spellId=6003; world catalog was not available after Process admission.")
                .ConfigureAwait(false);
            return true; // Terminal diagnostic/handled denial AFTER reached Process, no fallback.
        }
        if (!LegacyMagicRuntime.TryResolveLearnedSpell(
                session.PlayerSpells, worldData, LegacyCounterKillSpellId,
                out var playerSpell, out var spell) || !IsLegacyCounterKillSpell(spell))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy CounterKill switch " +
                    $"{FormatSession(context with { Session = session })} spellId=6003; learned CounterKill spell was not available.")
                .ConfigureAwait(false);
            // Terminal diagnostic only: never continue on a UID-only refresh or a new client.
            return true;
        }

        // Native6003 is NOT in MagicType's weapon-token whitelist and sort29 is not Attack/Direct.
        // This exact book/DB level is captured once; later resource-send callbacks do not reselect it.
        // Reuse ONLY the native-shaped cost/cold primitive, not generic Magic preparation/cadence.
        // It has no Alive/Grace/manual item-effects gate and publishes no ColdTime packet.
        var prepared = await TryPrepareLegacyRootFirstFamilyCanUseAsync(
                context with { Session = session }, session, spell, cancellationToken,
                owner.NativeClientIdentity).ConfigureAwait(false);
        if (prepared is null) return true;
        session = prepared;
        if (!Refresh()) return true;

        // TryCommit reads and toggles the CURRENT record inside the same original Store gate.
        // Never derive a stale pre-await bit then publish it with an unguarded final Sessions.Set.
        if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                current => current with
                {
                    Character = WithCharacterAttribute(current.Character,
                        RuntimeLegacyCounterKillActiveAttribute,
                        IsLegacyCounterKillActive(current.Character) ? "0" : "1"),
                }, owner.NativeClientIdentity))
            return true;
        if (!Refresh()) return true;
        var active = IsLegacyCounterKillActive(session.Character);
        var response = new MsgAttackPacket(packet.Query with
        {
            SpellId = playerSpell.Id,
            SpellLevel = playerSpell.Level,
            Damage = active ? 1 : 0,
        });
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        if (!Refresh()) return true;
        await context.Log.WriteLineAsync(
                "WorldServer handled legacy CounterKill switch " +
                $"{FormatSession(context with { Session = session })} active={active}.")
            .ConfigureAwait(false);
        return true; // Final log has no subsequent gameplay publication/commit.

        // Global OnSoul holder/publication and Jiang2309 are deliberately NOT implemented here.
    }
}
