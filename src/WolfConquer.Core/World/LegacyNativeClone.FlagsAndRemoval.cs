using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClone
{
	public void RemoveThat(LegacyNativeGameClientRuntime _Owner)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = UID,
			Type = 135
		};
		Owner.Player.View.SendView(stream.ActionCreate(pQuery), me: true);
	}

	public void AddFlag(LegacyNativeMsgUpdate.Flags Flag)
	{
		if (!BitVector.Contain((int)Flag))
		{
			BitVector.Add((int)Flag);
			UpdateSpawnPacket();
		}
	}

	public void RemoveFlag(LegacyNativeMsgUpdate.Flags Flag)
	{
		if (BitVector.Contain((int)Flag))
		{
			BitVector.Remove((int)Flag);
			UpdateSpawnPacket();
		}
	}

	public void UpdateSpawnPacket()
	{
		SendUpdate(BitVector.bits, LegacyNativeMsgUpdate.DataType.StatusFlag);
	}

	public void SendUpdate(uint[] Value, LegacyNativeMsgUpdate.DataType datatype)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, datatype, Value);
		stream = msgUpdate.GetArray(stream);
		Owner.Send(stream);
	}
}
