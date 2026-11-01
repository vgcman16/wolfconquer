using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native statue producer and map query; no lazy-map or persistence shortcut.
internal partial class LegacyNativeStatue
{
	public static bool ContainStatue(LegacyNativeGameMap map, ushort x, ushort y)
	{
		foreach (ILegacyNativeMapObj item in map.View!.Roles(LegacyNativeMapObjectType.SobNpc, x, y))
		{
			if (item.X == x && item.Y == y)
			{
				return true;
			}
		}
		return false;
	}

	public static void CreateStatue(LegacyNativeGameClientRuntime client, ushort x, ushort y, int Action, int action2, bool Static = false)
	{
		// Static creation writes the player's stance. An ordinary attachment
		// cannot enter this unjoined executor, even after its holder retires.
		var player = client.Player;
		if (Static && player.HasOwnedActionAttachment) return;
		if (!Static) { CreateStatueCore(client, x, y, Action, action2, Static); return; }
		if (Monitor.IsEntered(player.SpellAdmissionSyncRoot) && !Monitor.IsEntered(client.SpellAdmissionSyncRoot)) return;
		lock (client.SpellAdmissionSyncRoot)
		lock (player.SpellAdmissionSyncRoot)
		{
			if (!ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) || player.HasOwnedActionAttachment) return;
			CreateStatueCore(client, x, y, Action, action2, Static);
		}
	}

	private static void CreateStatueCore(LegacyNativeGameClientRuntime client, ushort x, ushort y, int Action, int action2, bool Static)
	{
		try
		{
			LegacyNativeStatue statue = new LegacyNativeStatue();
			statue.user = client;
			statue.UID = CounterUID.Next;
			statue.HitPotion = client.Player.HitPoints / 100;
			statue.Action = Action;
			statue.Static = Static;
			if (statue.Static)
			{
				statue.user!.Player.Action = LegacyNativePlayerMapFlags.ConquerAction.Sit;
				statue.user!.Player.Angle = LegacyNativePlayerMapFlags.ConquerAngle.South;
				StaticStatue = statue;
			}
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			LegacyNativeSobNpcRuntime sobNpc = new LegacyNativeSobNpcRuntime(statue);
			sobNpc.ObjType = LegacyNativeMapObjectType.SobNpc;
			sobNpc.UID = statue.UID;
			sobNpc.X = x;
			sobNpc.Y = y;
			sobNpc.Map = client.Player.Map;
			sobNpc.MaxHitPoints = (int)(client.Status.MaxHitpoints * 10);
			sobNpc.HitPoints = client.Player.HitPoints * 10;
			client.Player.View.SendView(sobNpc.GetArray(stream, view: false, st: false), me: true);
			client.Map!.View!.EnterMap((ILegacyNativeMapObj)sobNpc);
			if (Static)
			{
				StaticSobNpc = sobNpc;
			}
		}
		catch (Exception ex)
		{
			LegacyNativeConsole.WriteLine(ex.ToString());
		}
	}
}
