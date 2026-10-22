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

        public static void Create(LegacyNativeGameClientRuntime user, LegacyNativeNinja.Item Item, TypeID Type)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            MsgNinjaItem msgNinjaItem = new MsgNinjaItem();
            msgNinjaItem.Type = Type;
            msgNinjaItem.UID = user.Player.UID;
            msgNinjaItem.Items = new Item[1];
            msgNinjaItem.Items[0] = new Item();
            msgNinjaItem.Items[0].ID = Item.ItemID;
            msgNinjaItem.Items[0].Level = Item.Level;
            msgNinjaItem.Items[0].Position = Item.Position;
            msgNinjaItem.Items[0].Points = Item.Exp;
            user.Send(stream.CreateNinjaItem(msgNinjaItem));
        }
    }
}
