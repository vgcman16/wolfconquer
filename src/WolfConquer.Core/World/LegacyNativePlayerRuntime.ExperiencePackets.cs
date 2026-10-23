using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
internal void CreateExpProtection(LegacyNativePacket stream, uint Time, bool uppdate = true)
	{
		if (uppdate)
		{
			ExpProtection = Time;
		}
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.ExpProtection, new uint[5] { 0u, ExpProtection, 0u, 0u, 0u });
		stream = msgUpdate.GetArray(stream);
		Owner.Send(stream);
	}

internal void CreateExtraExpPacket(LegacyNativePacket stream)
	{
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.DoubleExpTimer, new uint[5]
		{
			0u,
			DExpTime,
			0u,
			RateExp * 100,
			0u
		});
		stream = msgUpdate.GetArray(stream);
		Owner.Send(stream);
	}
}
