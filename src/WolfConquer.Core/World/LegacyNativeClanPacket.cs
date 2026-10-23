using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClanPacket
{
	public static LegacyNativePacket ClanMembersCreate(this LegacyNativePacket stream, LegacyNativeClanMember[] Elements)
	{
		stream.InitWriter();
		stream.Write(4u);
		stream.Write(0u);
		stream.Write(0u);
		stream.Write((uint)Elements.Length);
		foreach (LegacyNativeClanMember member in Elements)
		{
			stream.Write(member.Name, 32);
			stream.Write((uint)member.Level);
			stream.Write((ushort)member.Rank);
			stream.Write((ushort)(member.Online ? 1u : 0u));
			stream.Write(member.Class);
			stream.Write(1u);
			stream.Write(member.Donation);
		}
		stream.Finalize(2049);
		return stream;
	}

	public static LegacyNativePacket ClanBulletinCreate(this LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativeClanRuntime clan)
	{
		stream.InitWriter();
		stream.Write(25u);
		stream.Write(client.Player.ClanUID);
		stream.Write(0u);
		stream.Write(clan.ClanBuletin!);
		stream.Finalize(2049);
		return stream;
	}

	public static LegacyNativePacket ClanRelationCreate(this LegacyNativePacket stream, uint ClanID, string ClanName, string ClanLeader, LegacyNativeMsgClan.Info aTyp)
	{
		stream.InitWriter();
		stream.Write((uint)aTyp);
		stream.Write(ClanID);
		stream.Write(0u);
		stream.Write(ClanName, ClanLeader);
		stream.Finalize(2049);
		return stream;
	}

	public static LegacyNativePacket ClanAppendSingleClientCreate(this LegacyNativePacket stream, uint ClanID, string Name, LegacyNativeMsgClan.Info aTyp)
	{
		stream.InitWriter();
		stream.Write((uint)aTyp);
		stream.Write(ClanID);
		stream.Write(0u);
		stream.Write(Name);
		stream.Finalize(2049);
		return stream;
	}

	public static void GetClan(this LegacyNativePacket stream, out LegacyNativeMsgClan.Info Mode, out uint UID, out uint dwparam, out string[]? list)
	{
		Mode = (LegacyNativeMsgClan.Info)stream.ReadUInt32();
		UID = stream.ReadUInt32();
		dwparam = stream.ReadUInt32();
		list = stream.ReadStringList();
	}

	public static LegacyNativePacket ClanCallBackCreate(this LegacyNativePacket stream, LegacyNativeMsgClan.Info Mode, uint UID, uint dwparam, string[]? list)
	{
		stream.InitWriter();
		stream.Write((uint)Mode);
		stream.Write(UID);
		stream.Write(dwparam);
		if (list != null)
		{
			stream.Write(list);
		}
		stream.Finalize(2049);
		return stream;
	}
}
