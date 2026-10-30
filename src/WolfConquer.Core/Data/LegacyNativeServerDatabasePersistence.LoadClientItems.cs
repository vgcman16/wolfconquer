using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    // Complete input, genuine detached items and all actual-owner preparation
    // precede publication. This component does not authorize ordinary gameplay.
    public static void LoadClientItems(LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var player = client.Player ?? throw new InvalidDataException("Native item loading requires its real player.");
        uint uid;
        string database;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            uid = player.UID;
            database = LegacyNativeServerConfig.DbLocation;
            RequireItemLoadIdentity(client, player, uid, database);
        }

        string path = Path.Combine(database, "PlayersItems", uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }

        // File.ReadAllBytes closes its owned handle on both success and failure.
        // A corrupt present file is never interpreted as an absent/empty file.
        var image = LegacyNativeItemLoadImage.Parse(bytes);
        var required = LegacyNativeItemLoadMaterialization.RequiredDestinations(image);
        if (required == LegacyNativeItemLoadOwners.Destination.None)
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
                RequireItemLoadIdentity(client, player, uid, database);
            return; // Complete empty/legacy-filtered image needs no item owner.
        }

        using var owners = LegacyNativeItemLoadOwners.Capture(client, player, required);
        RequireItemLoadIdentity(client, player, uid, database);
        var plan = LegacyNativeItemLoadMaterialization.Prepare(image, client, player);
        owners.Prepare(plan.Placements, plan.QuestUid);
        // Metadata access can invoke real catalog comparers; it must precede the
        // final pure owner validation and immediate nonthrowing publication.
        plan.RequireCurrent();
        RequireItemLoadIdentity(client, player, uid, database);
        owners.RequireCurrent();
        owners.PublishPrepared();
    }

    private static void RequireItemLoadIdentity(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, uint uid, string database)
    {
        if (uid == 0 || client.Fake || client.FullLoading || !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) ||
            player.UID != uid || string.IsNullOrWhiteSpace(database) ||
            !string.Equals(LegacyNativeServerConfig.DbLocation, database, StringComparison.Ordinal))
            throw new InvalidDataException("Native item-load player or database identity changed.");
    }
}
