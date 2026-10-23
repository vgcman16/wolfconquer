using WolfConquer.Core.Networking;
namespace WolfConquer.Core.World;


internal static class LegacyNativeMsgRaceRecord
{
	public enum RaceRecordTypes
	{
		BestTime,
		EndTime,
		AddRecord,
		CloseRecords
	}

	public static LegacyNativePacket RaceRecordCreate(this LegacyNativePacket stream, RaceRecordTypes typ, int rank, string name, int dwparam, int prize)
	{
		stream.InitWriter();
		stream.Write((uint)typ);
		stream.Write(rank);
		stream.Write(name, 32);
		stream.Write(dwparam);
		stream.Write(prize);
		stream.Finalize(1071);
		return stream;
	}

	public static LegacyNativePacket RaceRecordCreate(this LegacyNativePacket stream, RaceRecordTypes typ, int rank, int dwparam1, int dwparam2, int time, int prize)
	{
		stream.InitWriter();
		stream.Write((uint)typ);
		stream.Write(rank);
		stream.Write(dwparam1);
		stream.Write(dwparam2);
		stream.ZeroFill(8);
		stream.Write(time);
		stream.Write(prize);
		stream.Finalize(1071);
		return stream;
	}
}
