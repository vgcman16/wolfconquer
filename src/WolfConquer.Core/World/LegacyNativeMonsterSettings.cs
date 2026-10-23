using System;

namespace WolfConquer.Core.World;

[Flags]
internal enum LegacyNativeMonsterSettings : uint
{
	None = 0u,
	Moves = 1u,
	RevivesSurroundings = 2u,
	Aggressive = 4u,
	HasPlayerOwner = 8u,
	DropItemsOnDeath = 0x10u,
	Respawns = 0x20u,
	Invincible = 0x40u,
	KillFlashers = 0x80u,
	King = 0x135u,
	Aide = 0x235u,
	Messenger = 0x435u,
	Lottus = 0x800u,
	Standard = 0x35u,
	Reviver = 0x62u,
	Guard = 0xA4u
}
