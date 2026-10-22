using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeItemExtra
{
    public LegacyNativePacket CreateArray(LegacyNativePacket stream, bool tointer = false)
    {
        stream.InitWriter();
        stream.Write((uint)(Refinerys.Count + Purifications.Count));
        foreach (Purification purification in Purifications)
        {
            stream.Write(purification.ItemUID);
            stream.Write((uint)purification.Typ);
            stream.Write(purification.PurificationItemID);
            stream.Write(purification.PurificationLevel);
            stream.Write(purification.EffectPercent2);
            stream.Write(purification.PurificationDuration);
            stream.Write(purification.SecoundsLeft);
            stream.Write(0u);
        }
        foreach (Refinery refinery in Refinerys)
        {
            stream.Write(refinery.ItemUID);
            stream.Write((uint)refinery.Typ);
            if (tointer)
            {
                stream.Write(refinery.EffectID);
            }
            else if ((refinery.EffectID >= 724440 && refinery.EffectID <= 724444) || refinery.EffectID == 3004140 || (refinery.EffectID >= 3006165 && refinery.EffectID <= 3006170))
            {
                stream.Write(301u);
            }
            else
            {
                stream.Write(refinery.EffectID);
            }
            stream.Write(refinery.EffectLevel);
            stream.Write(refinery.EffectPercent);
            stream.Write(refinery.EffectPercent2);
            stream.Write(refinery.SecoundsLeft);
        }
        stream.Finalize(2134);
        return stream;
    }
}
