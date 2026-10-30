using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClanPacket
{
	public static LegacyNativePacket ClanMembersCreate(this LegacyNativePacket stream, LegacyNativeClanMember[] Elements)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(Elements);
		var members = (LegacyNativeClanMember[])Elements.Clone();
		var rows = new MemberWireRow[members.Length];
		for (int index = 0; index < members.Length; index++)
		{
			var member = members[index];
			if (member is null)
				throw new ArgumentException("Clan member array contains a missing row.", nameof(Elements));
			var name = member.Name;
			if (name is null)
				throw new ArgumentException("Clan member row has no name.", nameof(Elements));
			rows[index] = new MemberWireRow(name, member.Level, (ushort)member.Rank,
				(ushort)(member.Online ? 1 : 0), member.Class, member.Donation);
		}
		stream.PrepareFrame(2049, prepared =>
		{
			prepared.Write(4u);
			prepared.Write(0u);
			prepared.Write(0u);
			prepared.Write((uint)rows.Length);
			foreach (var row in rows)
			{
				prepared.Write(row.Name, 32);
				prepared.Write(row.Level);
				prepared.Write(row.Rank);
				prepared.Write(row.Online);
				prepared.Write(row.Class);
				prepared.Write(1u);
				prepared.Write(row.Donation);
			}
		});
		return stream;
	}

	public static LegacyNativePacket ClanBulletinCreate(this LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativeClanRuntime clan)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(clan);
		var player = client.Player;
		ArgumentNullException.ThrowIfNull(player);
		if (!ReferenceEquals(player.Owner, client))
			throw new InvalidOperationException("Clan bulletin player belongs to a different client.");
		uint clanUid = player.ClanUID;
		string? bulletin = clan.ClanBuletin;
		stream.PrepareFrame(2049, prepared =>
		{
			prepared.Write(25u);
			prepared.Write(clanUid);
			prepared.Write(0u);
			prepared.Write(new[] { bulletin! });
		});
		return stream;
	}

	public static LegacyNativePacket ClanRelationCreate(this LegacyNativePacket stream, uint ClanID, string ClanName, string ClanLeader, LegacyNativeMsgClan.Info aTyp)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(ClanName);
		ArgumentNullException.ThrowIfNull(ClanLeader);
		string[] names = [ClanName, ClanLeader];
		stream.PrepareFrame(2049, prepared =>
		{
			prepared.Write((uint)aTyp);
			prepared.Write(ClanID);
			prepared.Write(0u);
			prepared.Write(names);
		});
		return stream;
	}

	public static LegacyNativePacket ClanAppendSingleClientCreate(this LegacyNativePacket stream, uint ClanID, string Name, LegacyNativeMsgClan.Info aTyp)
	{
		ArgumentNullException.ThrowIfNull(stream);
		ArgumentNullException.ThrowIfNull(Name);
		string[] names = [Name];
		stream.PrepareFrame(2049, prepared =>
		{
			prepared.Write((uint)aTyp);
			prepared.Write(ClanID);
			prepared.Write(0u);
			prepared.Write(names);
		});
		return stream;
	}

	public static void GetClan(this LegacyNativePacket stream, out LegacyNativeMsgClan.Info Mode, out uint UID, out uint dwparam, out string[]? list)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var result = stream.ReadPrepared(prepared =>
		{
			var mode = (LegacyNativeMsgClan.Info)prepared.ReadUInt32();
			uint uid = prepared.ReadUInt32();
			uint parameter = prepared.ReadUInt32();
			string[] values = prepared.ReadStringList();
			return (Mode: mode, UID: uid, Parameter: parameter, List: values);
		});
		Mode = result.Mode;
		UID = result.UID;
		dwparam = result.Parameter;
		list = result.List;
	}

	public static LegacyNativePacket ClanCallBackCreate(this LegacyNativePacket stream, LegacyNativeMsgClan.Info Mode, uint UID, uint dwparam, string[]? list)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var values = list is null ? null : (string[])list.Clone();
		stream.PrepareFrame(2049, prepared =>
		{
			prepared.Write((uint)Mode);
			prepared.Write(UID);
			prepared.Write(dwparam);
			if (values is not null) prepared.Write(values);
		});
		return stream;
	}

	private readonly record struct MemberWireRow(string Name, uint Level, ushort Rank,
		ushort Online, uint Class, uint Donation);
}
