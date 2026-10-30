using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Captured only from this Frame's real admitted connection. Policy data,
    // numeric IDs and a caller-created native object cannot issue this actor.
    private sealed record GmCommandActor(ConquerTcpConnection Connection, WorldSession Expected,
        LegacyOrdinarySpellBinding Binding, LegacyOwnedNativeItemImage ItemImage, object Identity,
        LegacySecondaryPasswordState Pin, LegacyOwnedAdmissionOptions Routes, LegacyGmCapability? RequiredMailCapability);

    private GmCommandActor? CaptureGmActor(ConquerTcpConnection connection, LegacyGmCommandKind kind)
    {
        if (_gmPolicy is not { } policy || _ownedAdmissionRuntime is not { } owner ||
            !ReferenceEquals(owner.Recovery, _tradeRecovery) ||
            !owner.TryGetOperatorGearMailConfiguration(this, policy.ExpectedDomain, out var routes, out _) ||
            !_sessions.TryGet(connection, out var session) || session is null || session.AccountId is <= 0 or > uint.MaxValue ||
            session.Character.Uid is <= 0 or > uint.MaxValue ||
            !GmHasAnyCapability(session) || connection.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(session.NativeGameClient, binding.Client) ||
            !ReferenceEquals(binding.Store, _sessions) || !ReferenceEquals(binding.Connection, connection) ||
            !ReferenceEquals(binding.Recovery, _tradeRecovery) ||
            !LegacyNativeClientPublication.TryCaptureIdentity(_sessions, connection, session.Character.Uid, out var identity) ||
            !LegacySecondaryPasswordRuntime.TryGet(connection, session, out var pin, out var admitted) || !admitted)
            return null;
        LegacyGmCapability? mailCapability = kind switch
        {
            LegacyGmCommandKind.Kit => LegacyGmCapability.MailTestKitSelf,
            LegacyGmCommandKind.Item => LegacyGmCapability.MailCatalogItemSelf,
            _ => null
        };
        var actor = new GmCommandActor(connection, session, binding, binding.Items.Image, identity, pin, routes, mailCapability);
        return GmActorCurrent(actor, requireOriginal: true, out _) ? actor : null;
    }

    private bool GmHasAnyCapability(WorldSession session) => GmAllows(session, LegacyGmCapability.InspectSelf) ||
        GmAllows(session, LegacyGmCapability.MailTestKitSelf) || GmAllows(session, LegacyGmCapability.MailCatalogItemSelf);

    private bool GmAllows(WorldSession session, LegacyGmCapability capability) =>
        _gmPolicy is { } policy && session.AccountId is > 0 and <= uint.MaxValue &&
        session.Character.Uid is > 0 and <= uint.MaxValue && policy.Allows(policy.ExpectedDomain,
            checked((uint)session.AccountId), checked((uint)session.Character.Uid), capability);

    // The provisional ingress actor grants nothing until this check runs under
    // the actual exclusive world/trade owners. Recheck before each private send.
    private bool GmActorCurrent(GmCommandActor actor, bool requireOriginal, out WorldSession current)
    {
        current = null!;
        if (_gmPolicy is not { } policy || _ownedAdmissionRuntime is not { } runtime ||
            !ReferenceEquals(runtime.Recovery, _tradeRecovery) || _tradeRecovery.IsBlocked ||
            !runtime.TryGetOperatorGearMailConfiguration(this, policy.ExpectedDomain, out var routes, out _) ||
            routes != actor.Routes || !_tradeRecovery.TryRequireLiveJournalClear(out _) ||
            !GmResponseActorCurrent(actor, out var observed) ||
            requireOriginal && (!ReferenceEquals(observed, actor.Expected) ||
                !ReferenceEquals(actor.Binding.Items.Image, actor.ItemImage))) return false;
        current = observed;
        return true;
    }

    // Response-only witness: a recovery fence must still permit an explicit
    // bounded outcome on the SAME admitted actor. It never grants an action or
    // bypasses readiness before preparation/commit. A retired issuer is silent.
    private bool GmResponseActorCurrent(GmCommandActor actor, out WorldSession current)
    {
        current = null!;
        var binding = actor.Binding;
        if (_gmPolicy is not { } policy || policy.ExpectedDomain != actor.Routes.ExpectedDomain ||
            _ownedAdmissionRuntime is not { } runtime || !ReferenceEquals(runtime.Recovery, _tradeRecovery) ||
            _tradeStates.HasConnectionEnded(actor.Connection) || !actor.Connection.HasSendRoute ||
            !actor.Connection.HasDisconnectRoute ||
            !LegacyNativeClientPublication.TryCurrent(_sessions, actor.Connection,
                actor.Expected.Character.Uid, actor.Identity, out var observed) ||
            !ReferenceEquals(observed.NativeGameClient, binding.Client) ||
            !ReferenceEquals(actor.Connection.NativeGameClient, binding.Client) ||
            !ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) ||
            !ReferenceEquals(binding.Store, _sessions) || !ReferenceEquals(binding.Connection, actor.Connection) ||
            !ReferenceEquals(binding.Recovery, _tradeRecovery) ||
            !OperatorMailSameRoute(observed.UsersRoot, actor.Routes.UsersRoot) || !GmHasAnyCapability(observed) ||
            !LegacySecondaryPasswordRuntime.TryGet(actor.Connection, observed, out var pin, out var admitted) ||
            !admitted || pin != actor.Pin || !_sessions.IsBoundSpellOwnerCurrent(binding) ||
            !_sessions.IsRuntimeInspectionOwnerCurrent(binding)) return false;
        // Finish callback-capable reads before the final exact physical/Store
        // issuance witnesses. Never recapture an actor after a callback.
        if (!binding.PhysicalOwnerIsCurrentPlain() ||
            !_sessions.TryGet(actor.Connection, out var final) || !ReferenceEquals(final, observed) ||
            !ReferenceEquals(final.NativeGameClient, binding.Client) ||
            !ReferenceEquals(binding.Client.OrdinarySpellBinding, binding)) return false;
        current = final;
        return true;
    }

    private bool GmMailOwnerMatches(GmCommandActor actor, OperatorMailOwner owner) =>
        ReferenceEquals(actor.Connection, owner.Connection) && ReferenceEquals(actor.Expected, owner.Expected) &&
        ReferenceEquals(actor.Binding, owner.Binding) && ReferenceEquals(actor.Identity, owner.Identity) &&
        actor.Pin == owner.Pin && actor.Routes == owner.Routes &&
        (actor.RequiredMailCapability is LegacyGmCapability.MailTestKitSelf or LegacyGmCapability.MailCatalogItemSelf) &&
        GmAllows(owner.Expected, actor.RequiredMailCapability.Value) &&
        GmActorCurrent(actor, requireOriginal: true, out _);
}
