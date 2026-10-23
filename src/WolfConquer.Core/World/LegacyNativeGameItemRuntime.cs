using System.Collections.Concurrent;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;
using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Core.World;

// Complete native MsgGameItem construction storage/defaults and ITEM_ID/Rune setter.
// The SAME concrete item reference remains in Inventory; no DTO or identity wrapper.
// Native has no explicit constructor. Declaration-order initializers are retained.
// Focused partials own native ItemCreate/ToLog/lock/extra sends. Full Send,
// inventory updates, equipment classification and raw loading remain separate.
internal sealed partial class LegacyNativeGameItemRuntime
{
	public static ushort MythsoulBag = 217;

	public uint MythSoul;

	public uint MythSoulPoints;

	public MsgItemExtra.Purification Purification;

	public MsgItemExtra.Refinery Refinary;

	public ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Deposite = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();

	public uint ItemPoints;

	public ushort Leng;

	public ushort PacketID;

	internal uint Uid;

	private uint _id;

	public ushort Durability;

	public ushort MaximDurability;

	public Flags.ItemMode Mode;

	public ushort Position;

	public uint SocketProgress;

	private uint _TimeLeftInMinutes = 0u;

	public Flags.Gem SocketOne;

	public Flags.Gem SocketTwo;

	public uint SpellID;

	public Flags.ItemEffect Effect;

	public byte Plus;

	public byte Bless;

	public byte Bound;

	public byte Enchant;

	public uint ProgresGreen;

	public byte Suspicious;

	public byte Locked;

	public Flags.Color Color;

	public uint PlusProgress;

	public ushort Inscribed;

	public uint Activate;

	public ushort StackSize;

	public ushort UnKnow;

	public uint WH_ID;

	public uint OwnerUID = 0u;

	public uint PerfectionLevel = 0u;

	public ushort PerfectionRank = 0;

	public uint PerfectionProgress = 0u;

	public string OwnerName = "";

	public string Signature = "";

	public uint RuneEXP;

	public LegacyNativeRelicAttribute[] RelicAttributes = new LegacyNativeRelicAttribute[5];

	public uint AnimaItemID;

	public int UnLockTimer;

	public DateTime EndDate = DateTime.FromBinary(0L);

	public uint PerfectionStage => PerfectionLevel / 9;

	public uint PerfectionStageStars => PerfectionLevel % 9;

	public uint GetPrestigeScore => ItemPoints;

	public uint ITEM_ID
	{
		get
		{
			return _id;
		}
		set
		{
			_id = value;
			if (LegacyNativeItemTypeRuneMetadata.IsRune(value))
			{
				if (LegacyNativePoolItems.ItemsBase![value].RuneSpellID == value)
				{
					SpellID = LegacyNativePoolItems.ItemsBase![value].RuneSpellID - LegacyNativePoolItems.ItemsBase![value].RuneSpellID % 100 + 1;
					Effect = Flags.ItemEffect.RuneEffect;
				}
				else
				{
					SpellID = LegacyNativePoolItems.ItemsBase![value].RuneSpellID;
					Effect = Flags.ItemEffect.None;
				}
			}
		}
	}

	public uint RemainingTime
	{
		get
		{
			if (Activate == 1)
			{
				// Framework TimeSpan.TotalSeconds multiplies ticks by 1E-07;
				// modern division and saturating UInt32 narrowing change native wire data.
				return unchecked((uint)(long)((EndDate - DateTime.Now).Ticks * 1E-07));
			}
			return 0u;
		}
	}

	public uint TimeLeftInMinutes
	{
		get
		{
			if (Activate == 0)
			{
				return _TimeLeftInMinutes;
			}
			return 0u;
		}
		set
		{
			_TimeLeftInMinutes = value;
		}
	}

}
