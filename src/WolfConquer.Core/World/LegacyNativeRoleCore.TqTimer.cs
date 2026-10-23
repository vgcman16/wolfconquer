using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeRoleCore
{
	internal static ulong TqTimer(DateTime timer)
	{
		ulong num = (ulong)(10000000000000L * (timer.Year - 1900));
		ulong num2 = (ulong)(100000000000L * (timer.Month - 1));
		ulong num3 = 100000000uL * (ulong)(timer.DayOfYear - 1);
		ulong num4 = (ulong)(timer.Day * 1000000);
		ulong num5 = (ulong)(timer.Hour * 10000);
		ulong num6 = (ulong)(timer.Minute * 100);
		ulong num7 = (ulong)timer.Second;
		return num + num2 + num3 + num4 + num5 + num6 + num7;
	}
}
