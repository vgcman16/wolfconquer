namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeWs2_32
{
	internal struct TimeValue
	{
		public int Seconds;

		public int Microseconds;

		public TimeValue(long microSeconds)
		{
			Seconds = (int)(microSeconds / 1000000);
			Microseconds = (int)(microSeconds % 1000000);
		}
	}

	public enum ShutDownFlags
	{
		SD_RECEIVE,
		SD_SEND,
		SD_BOTH
	}
}
