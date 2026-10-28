using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// A binding is consumed only while the issuing Store still owns this exact
// object. Its saved UID, by itself, never authorizes a publication.
internal sealed class LegacyOrdinarySpellBinding
{
    internal readonly WorldSessionStore Store;
    internal readonly ConquerTcpConnection Connection;
    internal readonly object NativeIdentity;
    internal readonly LegacyNativeGameClientRuntime Client;
    internal readonly LegacyNativePlayerRuntime Player;
    internal readonly LegacyNativeSpellStorage Storage;
    internal readonly LegacyNativeProficiencyStorage Proficiencies;
    internal readonly LegacyTradeRecoveryState Recovery;
    internal readonly string UsersRoot, SpellsRoot;
    internal readonly string SessionUsersRoot;
    internal readonly long AccountId, Uid;
    private int retired;
    private PlayerSpellSnapshot snapshot;
    private PlayerProficiencySnapshot proficiencySnapshot;
    private LegacySavedSpellImage savedSpells;
    internal PlayerSpellSnapshot Snapshot => Volatile.Read(ref snapshot);
    internal PlayerProficiencySnapshot ProficiencySnapshot => Volatile.Read(ref proficiencySnapshot);
    internal LegacySavedSpellImage SavedSpells => Volatile.Read(ref savedSpells);
    internal bool IsRetired => Volatile.Read(ref retired) != 0;

    internal LegacyOrdinarySpellBinding(WorldSessionStore store, ConquerTcpConnection connection,
        object identity, WorldSession session, LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeSpellStorage storage, LegacyNativeProficiencyStorage proficiencies,
        LegacyTradeRecoveryState recovery, string spellsRoot, byte[]? savedSpellImage)
    {
        Store = store; Connection = connection; NativeIdentity = identity;
        Client = client; Player = player; Storage = storage; Proficiencies = proficiencies;
        Recovery = recovery; SpellsRoot = spellsRoot;
        SessionUsersRoot = session.UsersRoot!;
        UsersRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(SessionUsersRoot));
        AccountId = session.AccountId; Uid = session.Character.Uid; snapshot = session.PlayerSpells;
        proficiencySnapshot = session.PlayerProficiencies;
        savedSpells = new(savedSpellImage);
    }

    internal WorldSessionStore.NativeSpellMutationLease EnterMutation() => Store.EnterBoundSpellMutation(this);
    internal void PublishSnapshots(PlayerSpellSnapshot spells, PlayerProficiencySnapshot proficiencies)
    {
        Volatile.Write(ref snapshot, spells);
        Volatile.Write(ref proficiencySnapshot, proficiencies);
    }
    internal void PublishSavedSpells(LegacySavedSpellImage image) => Volatile.Write(ref savedSpells, image);
    internal void Retire() => Volatile.Write(ref retired, 1);

    internal bool PhysicalOwnerIsCurrentPlain() => !IsRetired && !Recovery.IsBlocked &&
        ReferenceEquals(Connection.NativeGameClient, Client) && ReferenceEquals(Client.Player, Player) &&
        ReferenceEquals(Player.Owner, Client) && Player.UID == Uid &&
        ReferenceEquals(Client.OrdinarySpellBinding, this) && ReferenceEquals(Client.MySpells, Storage) &&
        ReferenceEquals(Storage.OrdinaryBinding, this) && Storage.IsOwnedBy(Client) &&
        ReferenceEquals(Client.MyProfs, Proficiencies) && Proficiencies.IsOwnedBy(Client);
}

internal sealed class LegacyCommittedSpellPublicationException : InvalidOperationException
{
    internal LegacyCommittedSpellPublicationException()
        : base("ordinarySpellRecoveryRequired:durableCommitPublicationUnproven") { }
}
