namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    // Whole native MsgGameItem.GetPerfectionPosition110..135; no rank publication.
	public int GetPerfectionPosition
	{
		get
		{
			ushort num = LegacyNativeItemPosition.ItemPosition(ITEM_ID);
			if (num <= 5)
			{
				return num;
			}
			if (LegacyNativeItemTypeRuneMetadata.IsShield(ITEM_ID))
			{
				return 4;
			}
			return num switch
			{
				19 => 5,
				6 => 6,
				18 => 7,
				8 => 8,
				12 => 9,
				10 => 10,
				11 => 11,
				_ => -1,
			};
		}
	}
}
