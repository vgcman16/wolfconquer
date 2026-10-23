using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	public enum AttackFlag : uint
	{
		None = 0u,
		NotHitFriends = 1u,
		NotHitClanMembers = 2u,
		NotHitGuildMembers = 4u,
		NotHitAlliedGuild = 8u,
		NoHitAlliesClan = 0x10u
	}

	public class Stage
	{
		public class Star
		{
			private int Count;

			public AtributesType Typ;

			public byte Level;

			public ushort UID;

			public bool Activate = false;

			public Star(int noumber)
			{
				Count = noumber;
			}
		}

		[Flags]
		public enum AtributesType
		{
			None = 0,
			MaxLife = 1,
			PAttack = 2,
			MAttack = 3,
			PDefense = 4,
			Mdefense = 5,
			FinalAttack = 6,
			FinalMagicAttack = 7,
			FinalDefense = 8,
			FinalMagicDefense = 9,
			CriticalStrike = 0xA,
			SkillCriticalStrike = 0xB,
			Immunity = 0xC,
			Breakthrough = 0xD,
			Counteraction = 0xE,
			MaxMana = 0xF
		}

		private int Count;

		public bool Activate = false;

		public Star[] ArrayStars;

		public Stage(int noumber)
		{
			Count = noumber;
			ArrayStars = new Star[9];
			for (int i = 0; i < ArrayStars.Length; i++)
			{
				ArrayStars[i] = new Star(i);
			}
		}
	}

	public class BackUpStar
	{
		public byte Stage;

		public byte PositionStar;

		public Stage.Star Star = null!;
	}
}
