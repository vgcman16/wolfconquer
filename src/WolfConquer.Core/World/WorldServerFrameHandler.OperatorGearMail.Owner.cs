using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed record OperatorMailOwner(LegacyOwnedAdmissionOptions Routes, ConquerTcpConnection Connection,
        WorldSession Expected, object Identity, LegacyOrdinarySpellBinding Binding,
        LegacySecondaryPasswordState Pin, KeyValuePair<string, string>[] Fields)
    {
        internal string CharacterPath => Path.Combine(Routes.UsersRoot, Expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string ItemsPath => Path.Combine(Routes.ItemsRoot, Expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string MailPath => Path.Combine(Routes.MailboxRoot, Expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string[] Paths => [CharacterPath, ItemsPath, MailPath];
    }

    private bool TryCaptureOperatorMailOwner(LegacyOwnedAdmissionRuntime runtime, OperatorMailRequest request,
        out OperatorMailOwner? owner, out string reason)
    {
        owner = null;
        reason = "operatorMailOnlineOwnerUnavailable";
        if (!runtime.TryGetOperatorGearMailConfiguration(this, request.Request.ExpectedDomain, out var routes, out reason) ||
            !OperatorMailSameRoute(_mailboxes!.ClaimRoot, routes.MailboxRoot) || !ReferenceEquals(_tradeRecovery, runtime.Recovery)) return false;
        var candidates = _sessions.Snapshot().Where(active => active.Session.AccountId == request.Request.AccountId &&
            active.Session.Character.Uid == request.Request.CharacterUid).ToArray();
        if (candidates.Length != 1) return false;
        var active = candidates[0];
        var current = active.Session;
        var client = active.Connection.NativeGameClient;
        if (client?.OrdinarySpellBinding is not { } binding || !ReferenceEquals(current.NativeGameClient, client) ||
            !ReferenceEquals(binding.Recovery, _tradeRecovery) || !ReferenceEquals(binding.Store, _sessions) ||
            !ReferenceEquals(binding.Connection, active.Connection) || !active.Connection.HasSendRoute ||
            !active.Connection.HasDisconnectRoute ||
            !LegacyNativeClientPublication.TryCaptureIdentity(_sessions, active.Connection, current.Character.Uid, out var identity) ||
            !LegacySecondaryPasswordRuntime.TryGet(active.Connection, current, out var pin, out var admitted) || !admitted)
            return false;
        var name = request.Request.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini";
        if (!OperatorMailSameRoute(current.UsersRoot, routes.UsersRoot) ||
            !(string.Equals(current.Character.SourceFile, name, StringComparison.OrdinalIgnoreCase) ||
                Path.IsPathFullyQualified(current.Character.SourceFile) &&
                string.Equals(Path.GetFullPath(current.Character.SourceFile), Path.Combine(routes.UsersRoot, name), StringComparison.OrdinalIgnoreCase)))
            return false;
        var fields = current.Character.Attributes.ToArray();
        var unique = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields)
            if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !unique.TryAdd(pair.Key, pair.Value)) return false;
        owner = new(routes, active.Connection, current, identity, binding, pin, fields);
        if (!OperatorMailOwnerCurrent(runtime, owner, current)) { owner = null; return false; }
        reason = string.Empty;
        return true;
    }

    private bool OperatorMailOwnerCurrent(LegacyOwnedAdmissionRuntime runtime, OperatorMailOwner owner, WorldSession expected)
    {
        if (!runtime.TryGetOperatorGearMailConfiguration(this, owner.Routes.ExpectedDomain, out var routes, out _) ||
            routes != owner.Routes || !ReferenceEquals(runtime, _ownedAdmissionRuntime) ||
            !expected.Character.Attributes.SequenceEqual(owner.Fields) || expected.LegacyTradeActive ||
            owner.Binding.Client.MyTrade is not null ||
            _tradeStates.Contains(owner.Connection) || _tradeStates.HasConnectionEnded(owner.Connection) ||
            expected.Character.Money < 0 || expected.Character.ConquerPoints is < 0 or > uint.MaxValue ||
            !LegacyTradeEscrowClaims.TryRead(expected.Character.Attributes, out var escrow, out _) || escrow is not null ||
            !LegacySecondaryPasswordRuntime.TryGet(owner.Connection, expected, out var pin, out var admitted) || !admitted || pin != owner.Pin)
            return false;
        // Finish callback-capable reads before the exact retained physical pin and
        // original Store issuance. Never capture a new issuance after a callback.
        return ReferenceEquals(expected.NativeGameClient, owner.Binding.Client) &&
            owner.Binding.PhysicalOwnerIsCurrentPlain() && _sessions.IsBoundSpellOwnerCurrent(owner.Binding) &&
            LegacyNativeClientPublication.TryCurrent(_sessions, owner.Connection, owner.Expected.Character.Uid,
                owner.Identity, out var current) && ReferenceEquals(current, expected);
    }

    private static bool OperatorMailSameRoute(string? first, string second) => !string.IsNullOrWhiteSpace(first) &&
        Path.IsPathFullyQualified(first) && string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
