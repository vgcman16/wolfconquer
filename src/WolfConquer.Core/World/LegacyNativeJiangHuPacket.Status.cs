using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native MsgBuilder codecs; no modern frame/router or host adoption is installed.
internal static partial class LegacyNativeJiangHuPacket
{
	public static LegacyNativePacket JiangHuStatusCreate(this LegacyNativePacket stream, string Name, byte Stage = 0, byte Talent = 0, uint Timer = 0u, ulong StudyPoints = 0uL, uint FreeTimeToday = 0u, byte FreeTimeTodeyUsed = 0, uint RoundBuyPoints = 0u, ICollection<LegacyNativeJiangHu.Stage>? array = null)
	{
		stream.InitWriter();
		stream.Write(Name, 32);
		stream.Write((byte)((Stage == 0) ? 1 : Stage));
		stream.Write(Talent);
		stream.Write(Timer);
		stream.Write((byte)0);
		stream.Write(StudyPoints);
		stream.Write(FreeTimeToday);
		stream.Write(9999999);
		stream.Write(FreeTimeTodeyUsed);
		stream.Write(RoundBuyPoints);
		if (array != null)
		{
			foreach (LegacyNativeJiangHu.Stage item in array)
			{
				if (item.Activate)
				{
					LegacyNativeJiangHu.Stage.Star[] arrayStars = item.ArrayStars;
					foreach (LegacyNativeJiangHu.Stage.Star star in arrayStars)
					{
						stream.Write(star.UID);
					}
				}
			}
		}
		stream.Finalize(2085);
		return stream;
	}
}
