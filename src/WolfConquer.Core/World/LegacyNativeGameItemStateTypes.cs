namespace WolfConquer.Core.World;

// Native construction state types with exact underlying enum widths/zero states.
// Purification/Refinery retain their value-type mutable availability semantics.
internal static class LegacyNativeGameItemFlags
{
	[Flags]
	public enum ItemMode : ushort
	{
		None = 0,
		AddItem = 1,
		Trade = 2,
		Update = 3,
		View = 4,
		Active = 5,
		AddItemReturned = 8,
		ChatItem = 9,
		ItemInbox = 0xB,
		Auction = 0xC
	}

	public enum ItemEffect : uint
	{
		None = 0u,
		RuneEffect = 1u,
		Poison = 200u,
		Stigma = 201u,
		MP = 202u,
		Shield = 203u,
		Horse = 100u
	}

	public enum Color : uint
	{
		Black = 2u,
		Orange,
		LightBlue,
		Red,
		Blue,
		Yellow,
		Purple,
		White
	}

	public enum Gem : byte
	{
		NormalPhoenixGem = 1,
		RefinedPhoenixGem = 2,
		SuperPhoenixGem = 3,
		NormalDragonGem = 11,
		RefinedDragonGem = 12,
		SuperDragonGem = 13,
		NormalFuryGem = 21,
		RefinedFuryGem = 22,
		SuperFuryGem = 23,
		NormalRainbowGem = 31,
		RefinedRainbowGem = 32,
		SuperRainbowGem = 33,
		NormalKylinGem = 41,
		RefinedKylinGem = 42,
		SuperKylinGem = 43,
		NormalVioletGem = 51,
		RefinedVioletGem = 52,
		SuperVioletGem = 53,
		NormalMoonGem = 61,
		RefinedMoonGem = 62,
		SuperMoonGem = 63,
		NormalTortoiseGem = 71,
		RefinedTortoiseGem = 72,
		SuperTortoiseGem = 73,
		NormalThunderGem = 101,
		RefinedThunderGem = 102,
		SuperThunderGem = 103,
		NormalGloryGem = 121,
		RefinedGloryGem = 122,
		SuperGloryGem = 123,
		NormalInfinityGem = 131,
		RefinedInfinityGem = 132,
		SuperInfinityGem = 133,
		NoSocket = 0,
		EmptySocket = byte.MaxValue
	}
}

internal partial class LegacyNativeItemExtra
{
	public enum Typing : uint
	{
		RefinaryAdding = 2u,
		PermanentRefinery,
		StabilizationEffectRefined,
		PurificationEffect,
		PurificationAdding,
		ExpireTime,
		Stabilization,
		StabilizationEffectPurification
	}

	public struct Purification
	{
		public uint ItemUID;

		public Typing Typ;

		public uint PurificationItemID;

		public uint PurificationLevel;

		public uint EffectPercent2;

		public uint PurificationDuration;

		public DateTime AddedOn;

		private bool Available;

		public int SecoundsLeft
		{
			get
			{
				if (PurificationDuration != 0)
				{
					TimeSpan timeSpan = new TimeSpan(AddedOn.AddSeconds(PurificationDuration).Ticks);
					TimeSpan timeSpan2 = new TimeSpan(DateTime.Now.Ticks);
					int num = (int)(timeSpan.TotalSeconds - timeSpan2.TotalSeconds);
					if (num > 0)
					{
						Available = true;
						return num;
					}
					Available = false;
				}
				else if (PurificationItemID > 1000)
				{
					Available = true;
				}
				return 0;
			}
		}

		public bool InLife
		{
			get
			{
				int secoundsLeft = SecoundsLeft;
				return Available;
			}
		}

		public static Purification ShallowCopy(Purification item)
		{
			return (Purification)item.MemberwiseClone();
		}
	}

	public struct Refinery
	{
		public uint ItemUID;

		public Typing Typ;

		public uint EffectID;

		public uint EffectLevel;

		public uint EffectPercent;

		public uint EffectPercent2;

		public uint EffectDuration;

		public DateTime AddedOn;

		public bool Available;

		public int SecoundsLeft
		{
			get
			{
				if (EffectDuration != 0)
				{
					TimeSpan timeSpan = new TimeSpan(AddedOn.AddSeconds(EffectDuration).Ticks);
					TimeSpan timeSpan2 = new TimeSpan(DateTime.Now.Ticks);
					int num = (int)(timeSpan.TotalSeconds - timeSpan2.TotalSeconds);
					if (num > 0)
					{
						Available = true;
						return num;
					}
					Available = false;
				}
				else if (EffectID > 1000)
				{
					Available = true;
				}
				return 0;
			}
		}

		public bool InLife
		{
			get
			{
				int secoundsLeft = SecoundsLeft;
				return Available;
			}
		}

		public static Refinery ShallowCopy(Refinery item)
		{
			return (Refinery)item.MemberwiseClone();
		}
	}
}

	internal enum LegacyNativeChiAttribute
	{
		None,
		CriticalStrike,
		SkillCriticalStrike,
		Immunity,
		Breakthrough,
		Counteraction,
		HPAdd,
		AddAttack,
		AddMagicAttack,
		AddMagicDefense,
		PhysicalDamageIncrease,
		MagicDamageIncrease,
		PhysicalDamageDecrease,
		MagicDamageDecrease,
		LukyStrike
	}

// Storage and deterministic encoding only; native random Relic selection is NOT wired.
internal struct LegacyNativeRelicAttribute
{
	public LegacyNativeChiAttribute Type;

	public ushort Value;

	public bool Epic;

	public LegacyNativeRelicAttribute(LegacyNativeChiAttribute type, ushort value, bool epic)
	{
		Type = type;
		Value = value;
		Epic = epic;
	}

	public LegacyNativeRelicAttribute(uint value)
	{
		Type = (LegacyNativeChiAttribute)(value % 100);
		Value = (ushort)(value / 1000);
		Epic = value % 1000 - value % 100 == 100;
	}

	public static implicit operator uint(LegacyNativeRelicAttribute attribute)
	{
		return (uint)(attribute.Value * 1000 + (byte)attribute.Type + (attribute.Epic ? 100 : 0));
	}
}
