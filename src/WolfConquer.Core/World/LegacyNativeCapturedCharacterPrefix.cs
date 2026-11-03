using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Preparation readiness and immutable DATA only, never authenticated/native
// custody, Store lifetime, a SQL revision witness or a publication capability.
internal sealed partial class LegacyNativeCapturedCharacterPrefix
{
    private readonly ConquerTcpConnection connection;
    private readonly LegacyNativeGameClientRuntime client;
    private readonly LegacyNativePlayerRuntime player;
    private readonly LegacyCapturedCharacterPrefix captured;
    private readonly CharacterSnapshot preparedSnapshot;
    private readonly SqlOrdinaryAdmissionImagePlan? sqlPlan;
    private readonly LegacyNativePlayerRuntime.ExistingPrefixLoad playerLoad;
    private readonly LegacyNativeBeastsRuntime.PrefixLoad beasts;
    private readonly LegacyNativeHairfaceStorageRuntime.PrefixLoad hairface;
    private readonly LegacyNativeExistingPrefixBefore playerBefore;
    private readonly LegacyNativeBeastsRuntime.PrefixBefore beastsBefore;
    private readonly LegacyNativeHairfaceStorageRuntime.PrefixBefore hairfaceBefore;

    private LegacyNativeCapturedCharacterPrefix(ConquerTcpConnection connection, LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyCapturedCharacterPrefix captured, CharacterSnapshot preparedSnapshot,
        LegacyNativePlayerRuntime.ExistingPrefixLoad playerLoad, LegacyNativeBeastsRuntime.PrefixLoad beasts,
        LegacyNativeHairfaceStorageRuntime.PrefixLoad hairface, LegacyNativeExistingPrefixBefore playerBefore,
        LegacyNativeBeastsRuntime.PrefixBefore beastsBefore, LegacyNativeHairfaceStorageRuntime.PrefixBefore hairfaceBefore,
        SqlOrdinaryAdmissionImagePlan? sqlPlan)
    {
        this.connection = connection; this.client = client; this.player = player; this.captured = captured;
        this.preparedSnapshot = preparedSnapshot; this.playerLoad = playerLoad; this.beasts = beasts; this.hairface = hairface;
        this.playerBefore = playerBefore; this.beastsBefore = beastsBefore; this.hairfaceBefore = hairfaceBefore; this.sqlPlan = sqlPlan;
    }

    internal SqlWorldStateImage Image => captured.Image;
    internal CharacterSnapshot PreparedSnapshot => preparedSnapshot with
    {
        Attributes = new Dictionary<string, string>(preparedSnapshot.Attributes, StringComparer.OrdinalIgnoreCase),
        PaidNobility = new Dictionary<string, string>(preparedSnapshot.PaidNobility, StringComparer.OrdinalIgnoreCase),
        PlayerQuests = new PlayerQuestSnapshot(preparedSnapshot.PlayerQuests.Quests.Select(quest =>
            new PlayerQuestSnapshotEntry(quest.Uid, quest.Status, quest.Time, quest.Intentions.ToArray())).ToArray()),
    };
    internal LegacyNativePrefixValues PreparedPlayerValues => playerLoad.PreparedPlayerValues;
    internal byte[] CopyOnlineCharacterBytes() => captured.CopyOnlineCharacterBytes();

    private bool NativeIdentityCurrent() => !connection.HasObservedTransportClose && ReferenceEquals(connection.NativeGameClient, client) &&
        ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && !client.FullLoading && !client.Fake;

    internal bool IsCurrent()
    {
        try
        {
            return NativeIdentityCurrent() && playerBefore.IsCurrent() && beastsBefore.IsCurrent() && hairfaceBefore.IsCurrent() &&
                playerLoad.IsCurrentPlain() && beasts.IsCurrent() && hairface.IsCurrent() &&
                beastsBefore.IsCurrent() && hairfaceBefore.IsCurrent() && playerBefore.IsCurrent() &&
                beasts.IsOwnerCurrent() && hairface.IsOwnerCurrent() && playerLoad.IsCurrentPlain() && NativeIdentityCurrent();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or IndexOutOfRangeException or OverflowException)
        { return false; }
    }
}
