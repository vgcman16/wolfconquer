using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime : IEnumerable<LegacyNativeChiRuntime.ChiPower>, IEnumerable
{
	public static ConcurrentDictionary<uint, LegacyNativeChiRuntime> ChiPool = new ConcurrentDictionary<uint, LegacyNativeChiRuntime>();

	public int ChiPoints = 0;

	public string Name = "";

	public uint UID;

	public long DragonTime = 0L;

	public long PhoenixTime = 0L;

	public long TurtleTime = 0L;

	public long TigerTime = 0L;

	public ChiAttribute[]? DragonPowers = null;

	public ChiAttribute[]? PhoenixPowers = null;

	public ChiAttribute[]? TurtlePowers = null;

	public ChiAttribute[]? TigerPowers = null;

	public ChiPower Dragon;

	public ChiPower Phoenix;

	public ChiPower Turtle;

	public ChiPower Tiger;

	private uint test = 0u;

	public static object SynRoot = new object();

	public uint MaxChiPoints
	{
		get
		{
			uint num = 4000u;
			using (IEnumerator<ChiPower> enumerator = GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					ChiPower current = enumerator.Current;
					if (current != null && current.UnLocked && current.ChiAttributes != null)
					{
						num += current.ChiAttributes.MaxChiPoints;
					}
				}
			}
			return num;
		}
	}

	public uint CriticalStrike
	{
		get
		{
			return test;
		}
		set
		{
			test = value;
		}
	}

	public uint SkillCriticalStrike { get; set; }

	public uint Immunity { get; set; }

	public uint Breakthrough { get; set; }

	public uint Counteraction { get; set; }

	public uint MaxLife { get; set; }

	public uint AddAttack { get; set; }

	public uint AddMagicAttack { get; set; }

	public uint AddMagicDefense { get; set; }

	public uint FinalAttack { get; set; }

	public uint FinalMagicAttack { get; set; }

	public uint FinalDefense { get; set; }

	public uint FinalMagicDefense { get; set; }
}
