using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public LegacyNativePacket ItemCreate(LegacyNativePacket stream, LegacyNativeGameItemRuntime item)
    {
        stream.InitWriter();
        stream.Write(item.Uid);
        stream.Write(item.ITEM_ID);
        stream.Write(item.Durability);
        stream.Write(item.MaximDurability);
        stream.Write((ushort)item.Mode);
        stream.Write((byte)item.Position);
        stream.Write(item.SocketProgress);
        stream.Write((byte)item.SocketOne);
        stream.Write((byte)item.SocketTwo);
        stream.Write(item.SpellID);
        stream.Write((byte)item.Effect);
        stream.Write(item.Plus);
        stream.Write(item.Bless);
        stream.Write(item.Bound);
        stream.Write(item.Enchant);
        stream.Write(item.ProgresGreen);
        stream.Write(item.Suspicious);
        stream.Write((byte)0);
        stream.Write(item.Locked);
        stream.Write((byte)0);
        stream.Write(item.PlusProgress);
        stream.Write(RemainingTime);
        stream.Write(TimeLeftInMinutes);
        stream.Write(item.StackSize);
        stream.Write(item.PerfectionLevel);
        stream.Write(item.PerfectionProgress);
        stream.Write(item.OwnerUID);
        stream.Write(item.OwnerName, 32);
        stream.Write(item.Signature, 64);
        stream.ZeroFill(2);
        stream.Write(item.RuneEXP);
        for (int i = 0; i < item.RelicAttributes.Length; i++)
        {
            stream.Write(item.RelicAttributes[i]);
        }
        stream.Write(item.AnimaItemID);
        stream.Write(0);
        stream.Write(item.MythSoul);
        stream.Write(item.MythSoulPoints);
        stream.Finalize(2116);
        return stream;
    }
}
