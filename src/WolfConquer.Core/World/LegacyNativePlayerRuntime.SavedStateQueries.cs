namespace WolfConquer.Core.World;
public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player1343 /1369..1383 /2752.
    public bool IsStillBanned => (BannedChatStamp >= DateTime.Now && IsBannedChat) || PermenantBannedChat;
public uint DailyDays
	{
		get
		{
			uint num = 0u;
			for (byte b = 0; b < 31; b++)
			{
				if ((DailySignUpDays & (ulong)(1L << (int)b)) == (ulong)(1L << (int)b))
				{
					num++;
				}
			}
			return num;
		}
	}
    public uint PDinamycID { get; set; }
}
