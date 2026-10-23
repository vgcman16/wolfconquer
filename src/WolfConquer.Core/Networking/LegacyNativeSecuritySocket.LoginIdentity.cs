using System.Net;
using System.Net.NetworkInformation;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeSecuritySocket
{
    // Exact native SecuritySocket38 field; no connection/host/worker initialization.
    public bool OverrideTiming = false;


	public string RemoteIp
	{
		get
		{
			try
			{
				if (Connection == null)
				{
					return "NONE";
				}
				return (Connection.RemoteEndPoint as IPEndPoint)!.Address.ToString();
			}
			catch
			{
				return "NONE";
			}
		}
	}


	public string GetMACAddress()
	{
		NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
		string text = string.Empty;
		NetworkInterface[] array = allNetworkInterfaces;
		foreach (NetworkInterface networkInterface in array)
		{
			if (text == string.Empty)
			{
				IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
				text = networkInterface.GetPhysicalAddress().ToString();
			}
		}
		return text;
	}

}
