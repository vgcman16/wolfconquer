using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStaticRoleRuntime
{
	public bool AddFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0)
	{
		if (!BitVector.ContainFlag((int)Flag))
		{
			BitVector.TryAdd((int)Flag, Secounds, RemoveOnDead, StampSecounds);
			UpdateFlagOffset();
			return true;
		}
		return false;
	}

	public bool RemoveFlag(LegacyNativeMsgUpdate.Flags Flag, LegacyNativeGameMap map)
	{
		if (BitVector.ContainFlag((int)Flag))
		{
			BitVector.TryRemove((int)Flag);
			UpdateFlagOffset();
			return true;
		}
		return false;
	}

	private void UpdateFlagOffset(bool SendScreem = true)
	{
		if (SendScreem)
		{
			SendUpdate(BitVector.bits, LegacyNativeMsgUpdate.DataType.StatusFlag);
		}
	}

	public void SendUpdate(uint[] Value, LegacyNativeMsgUpdate.DataType datatype)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, datatype, Value);
		stream = msgUpdate.GetArray(stream);
		Send(stream);
	}

	public bool UpdateFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool SetNewTimer, int MaxTime)
	{
		return BitVector.UpdateFlag((int)Flag, Secounds, SetNewTimer, MaxTime);
	}
}
