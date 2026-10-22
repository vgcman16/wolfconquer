#nullable disable

using System;
using System.IO;
using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyNativeGroupServerList
{
	public class Server
	{
		public uint ID;

		public uint MapID;

		public uint X;

		public uint Y;

		public uint Group;

		public string Name;

		public byte TransferType = 0;

		public string IPAddress = "";

		public ushort Port = 0;
	}

	public static Server MyServerInfo = null;

	public static Server InterServer = null;

	public static LegacyNativeDbSafeDictionary<uint, Server> GroupServers = new LegacyNativeDbSafeDictionary<uint, Server>();

	public static Server[] CanTransferServers()
	{
		return GroupServers.Values.Where((Server p) => p.ID != MyServerInfo.ID && MyServerInfo.TransferType == p.TransferType).ToArray();
	}

	public static Server GetServer(uint ID)
	{
		return GroupServers.Values.Where((Server p) => p.ID == ID).FirstOrDefault();
	}

	public static Server GetServer(string IpAddres, ushort Port)
	{
		Server result = null;
		foreach (Server value in GroupServers.Values)
		{
			if (value.IPAddress == IpAddres && Port == value.Port)
			{
				result = value;
				break;
			}
		}
		return result;
	}

	public static void Load()
	{
		string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "client_config.ini");
		string[] array2 = array;
		foreach (string line in array2)
		{
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, ' ');
			Server server = new Server();
			server.ID = readLine.Read(0u);
			server.Name = readLine.Read("");
			server.MapID = readLine.Read(0u);
			server.X = readLine.Read(0u);
			server.Y = readLine.Read(0u);
			server.TransferType = readLine.Read((byte)0);
			server.Group = readLine.Read(0u);
			readLine.Read(0u);
			readLine.Read(0u);
			server.IPAddress = readLine.Read("");
			server.Port = readLine.Read((ushort)0);
			GroupServers.Add(server.ID, server);
			if (server.Name == LegacyNativeServerConfig.ServerName)
			{
				MyServerInfo = server;
			}
			if (server.Name.Contains("Realm"))
			{
				InterServer = server;
			}
		}
	}
}
