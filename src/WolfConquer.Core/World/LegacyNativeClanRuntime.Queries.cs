using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClanRuntime
{
public static int MaxPlayersInClan(byte level)
	{
		return 11;
	}

public static void RegisterChangeName(uint clanid, string? name)
	{
		if (!ChangeNameRecords.ContainsKey(clanid))
		{
			ChangeNameRecords.TryAdd(clanid, name);
		}
		else
		{
			ChangeNameRecords[clanid] = name;
		}
	}

public static void ProcessChangeNames()
	{
		foreach (KeyValuePair<uint, string?> changeNameRecord in ChangeNameRecords)
		{
			if (Clans.TryGetValue(changeNameRecord.Key, out var value))
			{
				value.Name = changeNameRecord.Value;
			}
		}
	}

public bool TryGetClan(string? Name, out LegacyNativeClanRuntime? cln)
	{
		LegacyNativeClanRuntime[] array = Clans.Values.ToArray();
		LegacyNativeClanRuntime[] array2 = array;
		foreach (LegacyNativeClanRuntime clan in array2)
		{
			if (clan.Name == Name)
			{
				cln = clan;
				return true;
			}
		}
		cln = null;
		return false;
	}

public static bool AllowCreateClan(string? Name)
	{
		if (!LegacyNativeBaseFunc.NameStrCheck(Name))
		{
			return false;
		}
		foreach (string? value in ChangeNameRecords.Values)
		{
			if (value == Name)
			{
				return false;
			}
		}
		foreach (LegacyNativeClanRuntime value2 in Clans.Values)
		{
			if (value2.Name == Name)
			{
				return false;
			}
		}
		return true;
	}

public uint ProcentClanBp(uint Bp)
	{
		return Bp switch
		{
			1u => 40u,
			2u => 50u,
			3u => 60u,
			4u => 70u,
			_ => 30u,
		};
	}

public bool TryGetMember(string? MemberName, out LegacyNativeClanMember? membe)
	{
		foreach (LegacyNativeClanMember value in Members.Values)
		{
			if (value.Name == MemberName)
			{
				membe = value;
				return true;
			}
		}
		membe = null;
		return false;
	}

public bool IsEnemy(string name)
	{
		foreach (LegacyNativeClanRuntime value in Enemy.Values)
		{
			if (value.Name == name)
			{
				return true;
			}
		}
		return false;
	}

public bool IsAlly(string name)
	{
		foreach (LegacyNativeClanRuntime value in Ally.Values)
		{
			if (value.Name == name)
			{
				return true;
			}
		}
		return false;
	}

public uint GetClanAlly(string a_name)
	{
		foreach (LegacyNativeClanRuntime value in Ally.Values)
		{
			if (a_name == value.Name)
			{
				return value.ID;
			}
		}
		return 0u;
	}

public uint GetClanEnemy(string a_name)
	{
		foreach (LegacyNativeClanRuntime value in Enemy.Values)
		{
			if (a_name == value.Name)
			{
				return value.ID;
			}
		}
		return 0u;
	}
}
