using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgDisCity
{
	public bool IsInDisCity(uint Map)
	{
		return 2021 == Map || 2022 == Map || 2023 == Map || 2024 == Map;
	}

	public bool AllowJoin()
	{
		return Mode == ProcesType.Idle;
	}

	public void CreateMaps()
	{
		if (Map1 == null)
		{
			Map1 = LegacyNativePoolMaps.ServerMaps![2021u];
		}
		if (Map2 == null)
		{
			Map2 = LegacyNativePoolMaps.ServerMaps![2022u];
		}
		if (Map3 == null)
		{
			Map3 = LegacyNativePoolMaps.ServerMaps![2023u];
		}
		if (Map4 == null)
		{
			Map4 = LegacyNativePoolMaps.ServerMaps![2024u];
		}
	}

	public int KillsMap2Records(uint _class)
	{
		if ((_class >= 1000 && _class <= 1005) || (_class >= 5000 && _class <= 5005) || (_class >= 6000 && _class <= 6005))
		{
			return 800;
		}
		if (_class >= 2000 && _class <= 2005)
		{
			return 900;
		}
		if ((_class >= 4000 && _class <= 4005) || (_class >= 7000 && _class <= 7005))
		{
			return 1300;
		}
		if (_class <= 13005)
		{
			return 600;
		}
		return 1000;
	}
}
