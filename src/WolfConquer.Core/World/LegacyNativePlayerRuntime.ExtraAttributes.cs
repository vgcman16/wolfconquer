using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native613 backing and whole1347..1363 property/2960..2972 writer.
    private ushort extraAtr;
public ushort ExtraAtributes
	{
		get
		{
			return extraAtr;
		}
		set
		{
			if (value > 901)
			{
				value = 900;
			}
			extraAtr = value;
		}
	}

internal void AddExtraAtributes(LegacyNativePacket stream, ushort value)
	{
		ExtraAtributes += value;
		Atributes += value;
		SendUpdate(stream, Atributes, LegacyNativeMsgUpdate.DataType.Atributes);
		if (ExtraAtributes > 901)
		{
			value = (ushort)(ExtraAtributes - 300);
			ExtraAtributes += value;
			Atributes += value;
			SendUpdate(stream, Atributes, LegacyNativeMsgUpdate.DataType.Atributes);
		}
	}
}
