using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeClanRuntime
{
public override string ToString()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(ID).Add(Name!).Add(LeaderName!)
			.Add(Level)
			.Add(Donation)
			.Add(ClanBuletin!)
			.Add(BP)
			.Add(0)
			.Add(0)
			.Add(0);
		return writeLine.Close();
	}

public void Load(string? line)
	{
		if (!(line == "") && line != null)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
			ID = readLine.Read(0u);
			Name = readLine.Read("None");
			LeaderName = readLine.Read("None");
			Level = readLine.Read((byte)0);
			Donation = readLine.Read(0u);
			ClanBuletin = readLine.Read("None");
			BP = readLine.Read((byte)0);
		}
	}

public string SaveAlly()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(Ally.Count);
		foreach (LegacyNativeClanRuntime value in Ally.Values)
		{
			writeLine.Add(value.ID);
		}
		return writeLine.Close();
	}

public string SaveEnemy()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(Enemy.Count);
		foreach (LegacyNativeClanRuntime value in Enemy.Values)
		{
			writeLine.Add(value.ID);
		}
		return writeLine.Close();
	}
}
