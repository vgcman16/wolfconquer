using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    public LegacyNativePacket ItemCreate(LegacyNativePacket stream, LegacyNativeGameItemRuntime item)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var image = new LegacyNativeItemPacketSnapshot(item);
        uint remaining = RemainingTime;
        uint minutes = TimeLeftInMinutes;
        return WriteCapturedItem(stream, image, remaining, minutes);
    }

    internal static LegacyNativePacket WriteCapturedItem(LegacyNativePacket stream,
        LegacyNativeItemPacketSnapshot image, uint remaining, uint minutes,
        Flags.ItemMode? mode = null, byte? locked = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!image.IsCurrent()) throw new InvalidOperationException("Item changed before frame preparation.");
        stream.PrepareFrame(2116, prepared =>
        {
            prepared.Write(image.Uid);
            prepared.Write(image.ITEM_ID);
            prepared.Write(image.Durability);
            prepared.Write(image.MaximDurability);
            prepared.Write((ushort)(mode ?? image.Mode));
            prepared.Write((byte)image.Position);
            prepared.Write(image.SocketProgress);
            prepared.Write((byte)image.SocketOne);
            prepared.Write((byte)image.SocketTwo);
            prepared.Write(image.SpellID);
            prepared.Write((byte)image.Effect);
            prepared.Write(image.Plus);
            prepared.Write(image.Bless);
            prepared.Write(image.Bound);
            prepared.Write(image.Enchant);
            prepared.Write(image.ProgresGreen);
            prepared.Write(image.Suspicious);
            prepared.Write((byte)0);
            prepared.Write(locked ?? image.Locked);
            prepared.Write((byte)0);
            prepared.Write(image.PlusProgress);
            prepared.Write(remaining);
            prepared.Write(minutes);
            prepared.Write(image.StackSize);
            prepared.Write(image.PerfectionLevel);
            prepared.Write(image.PerfectionProgress);
            prepared.Write(image.OwnerUID);
            prepared.Write(image.OwnerName, 32);
            prepared.Write(image.Signature, 64);
            prepared.ZeroFill(2);
            prepared.Write(image.RuneEXP);
            for (int i = 0; i < image.RelicCount; i++)
            {
                prepared.Write(image.RelicWord(i));
            }
            prepared.Write(image.AnimaItemID);
            prepared.Write(0);
            prepared.Write(image.MythSoul);
            prepared.Write(image.MythSoulPoints);
        });
        return stream;
    }
}
