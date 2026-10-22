using System.Linq;

namespace WolfConquer.Core.World;

// Genuine native Beasts state/constructor/method prefix. Packet Load is NOT installed.
// A GameClient field or MODEL assignment is not native LoadCharacter birth/adoption.
internal class LegacyNativeBeastsRuntime
{
	public enum HammerRunes : uint
	{
		None = 0u,
		YellowRuneSlot1 = 1u,
		YellowRuneSlot2 = 2u,
		YellowRuneSlot3 = 4u,
		YellowRuneSlot4 = 8u,
		YellowRuneSlot5 = 0x10u
	}

	public LegacyNativeGameClientRuntime Owner;

	public bool Activated;

	public uint Points;

	public uint flag;

	private byte _fixedlevel;

	private byte _level;

	public byte FixedLevel
	{
		get
		{
			return _fixedlevel;
		}
		set
		{
			_fixedlevel = value;
		}
	}

	public byte Level
	{
		get
		{
			return _level;
		}
		set
		{
			while (value > LegacyNativeBeastsAtrribute.Attributes.Count)
			{
				value--;
				FixedLevel++;
			}
			_level = value;
		}
	}

	public uint TotalPoints
	{
		get
		{
			uint num = Points;
			for (byte b = 1; b < Level; b++)
			{
				num += LegacyNativeBeastsAtrribute.Attributes[b].RequiredPoints;
			}
			return num;
		}
		set
		{
			if (Activated)
			{
				Level = 1;
			}
			for (int i = 0; i < LegacyNativeBeastsAtrribute.Attributes.Count && value >= LegacyNativeBeastsAtrribute.Attributes[(byte)(i + 1)].RequiredPoints; i++)
			{
				value -= LegacyNativeBeastsAtrribute.Attributes[(byte)(i + 1)].RequiredPoints;
				Level++;
			}
			Points += value;
			while (Points >= LegacyNativeBeastsAtrribute.Attributes.Values.LastOrDefault()!.RequiredPoints)
			{
				Points -= LegacyNativeBeastsAtrribute.Attributes.Values.LastOrDefault()!.RequiredPoints;
				FixedLevel++;
			}
		}
	}

	public byte Phase => (byte)(Activated ? ((byte)(1 + Level / 10)) : 0);

	public LegacyNativeBeastsRuntime(LegacyNativeGameClientRuntime client)
	{
		Owner = client;
		FixedLevel = 1;
	}

	public byte YellowRuneSlots()
	{
		byte b = 0;
		b += 5;
		if (LegacyNativeBeastsAtrribute.Attributes.ContainsKey(Level))
		{
			b += LegacyNativeBeastsAtrribute.Attributes[Level].YellowRuneSlots;
		}
		if ((flag & 1) == 1)
		{
			b++;
		}
		if ((flag & 2) == 2)
		{
			b++;
		}
		if ((flag & 4) == 4)
		{
			b++;
		}
		if ((flag & 8) == 8)
		{
			b++;
		}
		if ((flag & 0x10) == 16)
		{
			b++;
		}
		return b;
	}

	public void AddFlag(HammerRunes flag)
	{
		if (!ContainsFlag(flag))
		{
			this.flag |= (uint)flag;
		}
	}

	public bool ContainsFlag(HammerRunes flag)
	{
		ulong num = this.flag;
		num &= (ulong)(~flag);
		return num != this.flag;
	}
}
