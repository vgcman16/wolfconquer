using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeStatue
{
	public static LegacyNativeCounter CounterUID = new LegacyNativeCounter(105175u);

	public static LegacyNativeStatue? StaticStatue = null;

	public static LegacyNativeSobNpcRuntime? StaticSobNpc = null;

	public int Action = 0;

	public ushort Action2;

	public uint UID;

	public int HitPotion = 0;

	public LegacyNativeGameClientRuntime? user;

	public byte[]? StatuePacket;

	public bool Static = false;


}
