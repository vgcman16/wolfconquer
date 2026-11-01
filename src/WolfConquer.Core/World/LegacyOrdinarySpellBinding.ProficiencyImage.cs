namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinarySpellBinding
{
    private LegacySavedProficiencyImage? savedProficiencies;
    private string? savedProficiencyRoot;
    internal LegacySavedProficiencyImage SavedProficiencies => Volatile.Read(ref savedProficiencies) ?? throw InvalidProficiencyOrigin();
    internal string ProficienciesRoot => savedProficiencyRoot ?? throw InvalidProficiencyOrigin();

    internal void InitializePreparedProficiencyImage(LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner,
        string? root, byte[]? image, WorldSession proposed)
    {
        if (savedProficiencies is not null || savedProficiencyRoot is not null || IsRetired ||
            !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !owner.BeforeOwnerIsCurrentPlain(Client, Player) || owner.PreparedUid != Uid ||
            Client.OrdinarySpellBinding is not null || Client.MyProfs is not null ||
            !ReferenceEquals(proposed.NativeGameClient, Client) || proposed.AccountId != AccountId || proposed.Character.Uid != Uid ||
            !ReferenceEquals(proposed.PlayerProficiencies, ProficiencySnapshot) ||
            string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) throw InvalidProficiencyOrigin();
        var prepared = new LegacySavedProficiencyImage(image);
        if (!prepared.MatchesSnapshot(proposed.PlayerProficiencies)) throw InvalidProficiencyOrigin();
        savedProficiencyRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        savedProficiencies = prepared; // Actual captured admission image; no later disk recapture.
    }

    internal void PublishSavedProficiencies(LegacySavedProficiencyImage prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (savedProficiencies is null || savedProficiencyRoot is null || IsRetired ||
            !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
            !ReferenceEquals(Client.OrdinarySpellBinding, this) || !ReferenceEquals(Connection.NativeGameClient, Client) ||
            !ReferenceEquals(Client.Player, Player) || !ReferenceEquals(Player.Owner, Client)) throw InvalidProficiencyOrigin();
        Volatile.Write(ref savedProficiencies, prepared);
    }

    private static InvalidOperationException InvalidProficiencyOrigin() => new("ordinaryProficiencySavedOriginUnavailable");
}
