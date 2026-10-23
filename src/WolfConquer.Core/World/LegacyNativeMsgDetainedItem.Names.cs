using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeMsgDetainedItem
{
	public unsafe string Signature
	{
		get
		{
			fixed (sbyte* signature = _Signature)
			{
				return new string(signature);
			}
		}
		set
		{
			fixed (sbyte* signature = _Signature)
			{
				for (int i = 0; i < value.Length; i++)
				{
					signature[i] = (sbyte)value[i];
				}
			}
		}
	}

	public unsafe string PerfectionOwnerName
	{
		get
		{
			fixed (sbyte* ownerName = _OwnerName)
			{
				return new string(ownerName);
			}
		}
		set
		{
			fixed (sbyte* ownerName = _OwnerName)
			{
				for (int i = 0; i < value.Length; i++)
				{
					ownerName[i] = (sbyte)value[i];
				}
			}
		}
	}

	public unsafe string OwnerName
	{
		get
		{
			fixed (sbyte* value = szOwnerName)
			{
				return new string(value);
			}
		}
		set
		{
			fixed (sbyte* ptr = szOwnerName)
			{
				for (int i = 0; i < value.Length; i++)
				{
					ptr[i] = (sbyte)value[i];
				}
			}
		}
	}

	public unsafe string GainerName
	{
		get
		{
			fixed (sbyte* value = szGainerName)
			{
				return new string(value);
			}
		}
		set
		{
			fixed (sbyte* ptr = szGainerName)
			{
				for (int i = 0; i < value.Length; i++)
				{
					ptr[i] = (sbyte)value[i];
				}
			}
		}
	}

}
