using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgClan
{
public enum Info : uint
	{
		Info = 1u,
		Members = 4u,
		Recruit = 9u,
		AcceptRecruit = 10u,
		Join = 11u,
		AcceptJoinRequest = 12u,
		AddEnemy = 14u,
		DeleteEnemy = 15u,
		AddAlly = 19u,
		RequestAlly = 17u,
		AcceptAlly = 18u,
		DeleteAlly = 20u,
		TransferLeader = 21u,
		KickMember = 22u,
		Quit = 23u,
		Announce = 24u,
		SetAnnouncement = 25u,
		Dedicate = 26u,
		MyClan = 29u
	}
}
