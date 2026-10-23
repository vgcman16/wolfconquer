using System;

namespace WolfConquer.Core.World;

[Flags]
internal enum LegacyNativeMobStatus : uint
{
	Idle = 0u,
	SearchTarget = 1u,
	Attacking = 2u,
	Respawning = 3u
}
