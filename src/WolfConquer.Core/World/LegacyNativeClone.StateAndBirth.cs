using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Genuine complete physical clone state and whole constructor fault order.
internal partial class LegacyNativeClone
{
	public LegacyNativeBitVector32 BitVector;

	public uint UID = 0u;

	public LegacyNativeGameClientRuntime Owner;

	public static LegacyNativeCounter CounterUID = new LegacyNativeCounter(700100u);

	public int cloneflag = 0;

	public string clonename = "";


	public LegacyNativeClone(LegacyNativePlayerRuntime role, string CloneName, int flag, LegacyNativePacket stream)
	{
		BitVector = new LegacyNativeBitVector32(224);
		Owner = role.Owner;
		cloneflag = flag;
		clonename = CloneName;
		UID = CounterUID.Next;
		SendView(role.Owner, stream);
		AddFlag(LegacyNativeMsgUpdate.Flags.Invisibility);
	}
}
