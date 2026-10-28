namespace WolfConquer.Core.World;

internal sealed class LegacyNativeExistingPrefixBefore
{
    private readonly LegacyNativeGameClientRuntime client;
    private readonly LegacyNativePlayerRuntime player;
    private readonly LegacyNativePrefixValues values;
    private readonly uint job;
    private readonly ushort level;
    private readonly ushort transformation;

    internal LegacyNativeExistingPrefixBefore(LegacyNativeGameClientRuntime client)
    {
        this.client = client; player = client.Player;
        values = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);
        job = player.Class; level = player.Level; transformation = player.TransformationID;
    }

    internal bool IsCurrent() => ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) &&
        !client.FullLoading && !client.Fake && player.Class == job && player.Level == level && player.TransformationID == transformation &&
        LegacyNativePlayerRuntime.ExistingPrefixLoad.MatchesValuesPlain(player, values);
}
