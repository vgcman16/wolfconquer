using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact missing physical source slots711,765/767/769,813,853; no alternate reward context.
    public uint QuestMultiple = 0u;
    public uint DragonFurnace;
    public uint DragonFurnaceForge;
    public uint DragonFurnaceForgeCount;
    public uint KillersDisCity = 0u;
    public uint BattleFieldPoints = 0u;

    // Whole native Player1345 predicate: actual poisoned flag ownership, not a cached pill boolean.
    public bool OnFerentPill => ContainFlag(LegacyNativeMsgUpdate.Flags.Poisoned);

    // Whole native Player4800..4808; target internal only because Packet is internal.
	internal void AddMapEffect(LegacyNativePacket stream, ushort x, ushort y, params string[] effect)
	{
		LegacyNativeMsgStringPacket msgStringPacket = new LegacyNativeMsgStringPacket();
		msgStringPacket.ID = LegacyNativeMsgStringPacket.StringID.LocationEffect;
		msgStringPacket.X = x;
		msgStringPacket.Y = y;
		msgStringPacket.Strings = effect;
		View.SendView(stream.StringPacketCreate(msgStringPacket), me: true);
	}
}
