using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal struct LegacyNativeBoothItem
{
	public LegacyNativeGameItemRuntime Item;
	public long Cost;
	public LegacyNativeMsgItemView.ActionMode Cost_Type;
}
