using System.Collections.Generic;

namespace WolfConquer.Core.Networking;

internal class LegacyNativeLoginController
{
	private static Dictionary<string, byte> LoginControllerContainer = new Dictionary<string, byte>();

	public static bool CanLogin(string IP)
	{
		byte value = 0;
		if (LoginControllerContainer.TryGetValue(IP, out value))
		{
			return value < 3;
		}
		LoginControllerContainer.Add(IP, 0);
		return true;
	}

	public static void LoggedIn(string IP)
	{
		try
		{
			if (LoginControllerContainer.ContainsKey(IP))
			{
				byte b = LoginControllerContainer[IP];
				LoginControllerContainer.Remove(IP);
				b++;
				LoginControllerContainer.Add(IP, b);
			}
		}
		catch
		{
		}
	}

	public static void Disconnect(string IP)
	{
		try
		{
			if (LoginControllerContainer.ContainsKey(IP))
			{
				byte b = LoginControllerContainer[IP];
				LoginControllerContainer.Remove(IP);
				if (b > 0)
				{
					b--;
				}
				LoginControllerContainer.Add(IP, b);
			}
		}
		catch
		{
		}
	}
}
