using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeRoleView
{
	public void Clear(LegacyNativePacket stream)
	{
		Owner.Player.Px = 0;
		Owner.Player.Py = 0;
		LegacyNativeActionQuery pQuery = new LegacyNativeActionQuery
		{
			ObjId = Owner.Player.UID,
			Type = 135
		};
		SendView(stream.ActionCreate(pQuery), me: false);
		LegacyNativeClone[] array = Owner.Player.MyClones.ToArray();
		LegacyNativeClone[] array2 = array;
		foreach (LegacyNativeClone clone in array2)
		{
			clone.RemoveThat(Owner);
		}
		Owner.Player.MyClones.Clear();
	}
}
