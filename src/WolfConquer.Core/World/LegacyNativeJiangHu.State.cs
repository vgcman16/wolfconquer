using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	public static ConcurrentDictionary<uint, LegacyNativeJiangHu> Poll = new ConcurrentDictionary<uint, LegacyNativeJiangHu>();

	// Framework Random() was explicitly seeded from TickCount; keep that algorithm on net9.
	private Random Rand = new Random(Environment.TickCount);

	public Stage[] ArrayStages;

	public byte Talent = 3;

	public uint FreeCourse = 30000u;

	public uint FreeTimeToday = 0u;

	public uint UID;

	public DateTime StartCountDwon;

	public DateTime CountDownEnd;

	public DateTime CountDownMode;

	public DateTime ThreadStamp = default(DateTime);

	public string Name = null!;

	public string CustomizedName = null!;

	public uint RoundBuyPoints;

	public uint Inner_Strength;

	public byte Level;

	public byte Rank;

	public bool OnJiangMode = false;

	private bool CheckMode = false;

	public BackUpStar? Buckupstar = null;

	public LegacyNativeMsgStatus statusclient;

	// All DateTime-derived seconds fit Int64. Preserve Framework/x86 low UInt32 bits,
	// rather than net9's saturating floating-point-to-UInt32 conversion.
	public uint Time => unchecked((uint)(long)(CountDownEnd - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds);

	public LegacyNativeJiangHu(uint ID)
	{
		StartCountDwon = default(DateTime);
		CountDownEnd = default(DateTime);
		UID = ID;
		ArrayStages = new Stage[9];
		for (int i = 0; i < ArrayStages.Length; i++)
		{
			ArrayStages[i] = new Stage(i);
		}
		if (!Poll.ContainsKey(ID))
		{
			Poll.TryAdd(ID, this);
		}
		else
		{
			LegacyNativeConsole.WriteLine(ID + " ===> Un Putted Jiang ID [POP].", ConsoleColor.DarkRed);
		}
		statusclient = new LegacyNativeMsgStatus();
	}
}
