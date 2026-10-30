#nullable disable
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCMsgMailList
{
    public static void Loading(LegacyNativeGameClientRuntime client)
    {
        var mail = RequireMailMap(client);
        lock (mail.SyncRoot)
        {
            if (mail.Count == 0) return;
            var prepared = PreparedMailPages.Create(client, mail, sortById: false);
            prepared.RequireCurrent(client);
            // The real pool dequeues/rewinds on Take and cannot reserve or roll
            // back a rental. Touch it only after every actual page prepares.
            using var rental = new LegacyNativeRecycledPacket();
            var target = rental.GetStream();
            var witness = new MailOutputTarget(target);
            PublishMailPages(client, target, prepared, witness);
        }
    }

    // Whole native Process body only: no Packet attribute or dispatcher registration.
    private static void Process(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        var mail = RequireMailMap(user);
        lock (mail.SyncRoot)
        {
            if (mail.Count == 0) return;
            ArgumentNullException.ThrowIfNull(stream);
            var witness = new MailOutputTarget(stream);
            var prepared = PreparedMailPages.Create(user, mail, sortById: true);
            PublishMailPages(user, stream, prepared, witness);
        }
    }

    private static LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList> RequireMailMap(
        LegacyNativeGameClientRuntime client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.MailList ?? throw new ArgumentException("Client mail map is unavailable.", nameof(client));
    }
}
