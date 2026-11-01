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
		RequireUnboundActorScalarMutation();
		ushort first;
		lock (Owner.SpellAdmissionSyncRoot)
		lock (SpellAdmissionSyncRoot)
		{
			RequireUnboundActorScalarMutation();
			ExtraAtributes += value;
			Atributes += value;
			first = Atributes;
		}
		// The real socket may acquire its own gate and invoke a cipher; never
		// hold native actor gates across that external send path.
		SendUpdate(stream, first, LegacyNativeMsgUpdate.DataType.Atributes);
		if (ExtraAtributes > 901)
		{
			ushort second;
			lock (Owner.SpellAdmissionSyncRoot)
			lock (SpellAdmissionSyncRoot)
			{
				RequireUnboundActorScalarMutation();
				if (ExtraAtributes <= 901) return;
				value = (ushort)(ExtraAtributes - 300);
				ExtraAtributes += value;
				Atributes += value;
				second = Atributes;
			}
			SendUpdate(stream, second, LegacyNativeMsgUpdate.DataType.Atributes);
		}
	}
}
