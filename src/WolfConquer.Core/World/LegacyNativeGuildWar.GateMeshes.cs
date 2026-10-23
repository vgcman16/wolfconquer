using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native GateClose, GateDemolition and GateOpen families, source anomalies retained.
internal partial class LegacyNativeGuildWar
{
	internal StaticMesh GateClose(uint Level, bool Gate)
	{
		if (Gate)
		{
			switch (Level)
			{
			default:
				return StaticMesh.RightGate_Close_4;
			case 15u:
			case 16u:
			case 17u:
			case 18u:
			case 19u:
				return StaticMesh.RightGate_Close_3;
			case 10u:
			case 11u:
			case 12u:
			case 13u:
			case 14u:
				return StaticMesh.RightGate_Close_4;
			case 0u:
			case 1u:
			case 2u:
			case 3u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 9u:
				return StaticMesh.RightGate_Close_1;
			}
		}
		switch (Level)
		{
		default:
			return StaticMesh.LeftGate_Close_4;
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
			return StaticMesh.LeftGate_Close_3;
		case 10u:
		case 11u:
		case 12u:
		case 13u:
		case 14u:
			return StaticMesh.LeftGate_Close_2;
		case 0u:
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
		case 7u:
		case 8u:
		case 9u:
			return StaticMesh.LeftGate_Close_1;
		}
	}

	internal StaticMesh GateDemolition(uint Level, bool Gate)
	{
		if (Gate)
		{
			switch (Level)
			{
			default:
				return StaticMesh.RightGate_Demolition_4;
			case 15u:
			case 16u:
			case 17u:
			case 18u:
			case 19u:
				return StaticMesh.RightGate_Demolition_3;
			case 10u:
			case 11u:
			case 12u:
			case 13u:
			case 14u:
				return StaticMesh.RightGate_Demolition_2;
			case 0u:
			case 1u:
			case 2u:
			case 3u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 9u:
				return StaticMesh.RightGate_Demolition_1;
			}
		}
		switch (Level)
		{
		default:
			return StaticMesh.LeftGate_Demolition_4;
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
			return StaticMesh.LeftGate_Demolition_3;
		case 10u:
		case 11u:
		case 12u:
		case 13u:
		case 14u:
			return StaticMesh.LeftGate_Demolition_2;
		case 0u:
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
		case 7u:
		case 8u:
		case 9u:
			return StaticMesh.LeftGate_Demolition_1;
		}
	}

	internal StaticMesh GateOpen(uint Level, bool Gate)
	{
		if (Gate)
		{
			switch (Level)
			{
			default:
				return StaticMesh.RightGate_Open_4;
			case 15u:
			case 16u:
			case 17u:
			case 18u:
			case 19u:
				return StaticMesh.RightGate_Open_3;
			case 10u:
			case 11u:
			case 12u:
			case 13u:
			case 14u:
				return StaticMesh.RightGate_Open_2;
			case 0u:
			case 1u:
			case 2u:
			case 3u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 9u:
				return StaticMesh.RightGate_Open_1;
			}
		}
		switch (Level)
		{
		default:
			return StaticMesh.LeftGate_Open_4;
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
			return StaticMesh.LeftGate_Open_3;
		case 10u:
		case 11u:
		case 12u:
		case 13u:
		case 14u:
			return StaticMesh.LeftGate_Open_2;
		case 0u:
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
		case 7u:
		case 8u:
		case 9u:
			return StaticMesh.LeftGate_Open_1;
		}
	}
}
