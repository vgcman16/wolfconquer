using System;
using System.Threading;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeThreadPool
{
	public static int MaxOnline;

	public static LegacyNativeTimerRule<LegacyNativeSecuritySocket>? ConnectionReceive;

	public static LegacyNativeTimerRule<LegacyNativeSecuritySocket>? ConnectionSend;

	public static LegacyNativeTimerRule<LegacyNativeSecuritySocket>? ConnectionReview;

	public static LegacyNativeStaticPool? GenericThreadPool;

	public static LegacyNativeTimerRule<LegacyNativeGameClientRuntime>? Characters;

	public static LegacyNativeTimerRule<LegacyNativeGameClientRuntime>? AutoAttack;

	public static LegacyNativeTimerRule<LegacyNativeGameClientRuntime>? Monsters;

	public static LegacyNativeTimerRule<LegacyNativeGameClientRuntime>? Buffers;

	public static LegacyNativeTimerRule<LegacyNativeGameClientRuntime>? Floor;

	public static LegacyNativeStaticPool? ReceivePool;

	public static LegacyNativeStaticPool? SendPool;

	#pragma warning disable CS0169 // Exact original unused stream declaration.
	private static LegacyNativePacket? stream;
#pragma warning restore CS0169

	public static int Online => LegacyNativePoolClients.GamePoll!.Count;
}
