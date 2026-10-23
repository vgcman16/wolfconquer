using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
public uint Hair
	{
		get
		{
			return _hair;
		}
		set
		{
			_hair = value;
			if (!Owner.FullLoading)
			{
				return;
			}
			using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
			{
				LegacyNativePacket stream = recycledPacket.GetStream();
				SendUpdate(stream, _hair, LegacyNativeMsgUpdate.DataType.HairStyle);
			}
			if (Owner.HairfaceStorage != null)
			{
				Owner.HairfaceStorage.Add(LegacyNativeHairfaceStorageTable.Hairfaces.Where((LegacyNativeHairfaceStorageTable.Hairface i) => i.ID == value % 1000 && i.Type == LegacyHairFaceStorageType.Hairstyle).FirstOrDefault(), Send: false);
			}
		}
	}
}
