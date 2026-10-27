#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public partial class MsgNinjaItem
    {
        public static void Create(LegacyNativeGameClientRuntime user, LegacyNativeNinja _NinjaSprint, TypeID Type, uint UID)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            MsgNinjaItem msgNinjaItem = new MsgNinjaItem();
            msgNinjaItem.Type = Type;
            msgNinjaItem.UID = UID;
            msgNinjaItem.Items = new Item[_NinjaSprint.Items.Count];
            int num = 0;
            foreach (LegacyNativeNinja.Item value in _NinjaSprint.Items.Values)
            {
                msgNinjaItem.Items[num] = new Item();
                msgNinjaItem.Items[num].ID = value.ItemID;
                msgNinjaItem.Items[num].Level = value.Level;
                msgNinjaItem.Items[num].Position = value.Position;
                msgNinjaItem.Items[num].Points = value.Exp;
                msgNinjaItem.Items[num].UK = value.UK;
                num++;
            }
            user.Send(stream.CreateNinjaItem(msgNinjaItem));
        }

        internal static MsgNinjaItem PrepareSingleItem(uint uid, LegacyNativeNinja.Item item, TypeID type) => new()
        {
            Type = type,
            UID = uid,
            // Preserve the actual single-item producer's required UK=0 omission.
            Items = [new Item { ID = item.ItemID, Level = item.Level, Position = item.Position, Points = item.Exp }],
        };
        public static void Create(LegacyNativeGameClientRuntime user, LegacyNativeNinja.Item Item, TypeID Type)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            MsgNinjaItem msgNinjaItem = PrepareSingleItem(user.Player.UID, Item, Type);
            user.Send(stream.CreateNinjaItem(msgNinjaItem));
        }
    }
}
