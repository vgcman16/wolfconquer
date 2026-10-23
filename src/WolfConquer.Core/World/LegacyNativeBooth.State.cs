using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Genuine physical vendor reference and whole default birth/operator only.
// Full client constructor is held, never replaced by a silent vending success.
internal partial class LegacyNativeBooth
{
	public static LegacyNativeWrappingCounter BoothCounter = new LegacyNativeWrappingCounter(1u)
	{
		Finish = 10000u
	};

	public static Dictionary<uint, LegacyNativeBooth> Booths = new Dictionary<uint, LegacyNativeBooth>();

	public static object SyncRoot = new object();

	public LegacyNativeDbSafeDictionary<uint, LegacyNativeBoothItem> ItemList;

#pragma warning disable CS0649 // Assigned by the genuine whole client constructor, currently held.
	private LegacyNativeGameClientRuntime? Owner;
#pragma warning restore CS0649

	public LegacyNativeSobNpcRuntime? Base;

	public string Name => Owner!.Player.Name;


	public LegacyNativeBooth()
	{
		ItemList = new LegacyNativeDbSafeDictionary<uint, LegacyNativeBoothItem>(20);
	}

	public static implicit operator LegacyNativeSobNpcRuntime(LegacyNativeBooth booth)
	{
		return booth.Base!;
	}
}
