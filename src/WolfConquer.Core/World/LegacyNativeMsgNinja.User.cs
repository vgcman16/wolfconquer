using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgNinjaUser contract/Create; MsgNinjaItem is in separate partials.
// MsgNinjaInfo, packet registration and host callers remain absent.
internal static partial class LegacyNativeMsgNinja
{
    [ProtoContract]
    public class MsgNinjaUser
    {
        public enum TypeID
        {
            Loading,
            Flag,
            Points
        }

        [ProtoMember(1, IsRequired = true)]
        public uint Type = 0u;

        [ProtoMember(2, IsRequired = true)]
        public uint UID = 0u;

        [ProtoMember(3, IsRequired = true)]
        public ulong Flag = 0uL;

        [ProtoMember(4, IsRequired = true)]
        public uint Points = 0u;

        [ProtoMember(5, IsRequired = true)]
        public uint Fire = 0u;

        [ProtoMember(6, IsRequired = true)]
        public uint Water = 0u;

        [ProtoMember(7, IsRequired = true)]
        public uint Earth = 0u;

        [ProtoMember(8, IsRequired = true)]
        public uint Wind = 0u;

        [ProtoMember(9, IsRequired = true)]
        public uint Lightning = 0u;

        [ProtoMember(10, IsRequired = true)]
        public uint FireNew = 0u;

        [ProtoMember(11, IsRequired = true)]
        public uint WaterNew = 0u;

        [ProtoMember(12, IsRequired = true)]
        public uint EarthNew = 0u;

        [ProtoMember(13, IsRequired = true)]
        public uint WindNew = 0u;

        [ProtoMember(14, IsRequired = true)]
        public uint LightningNew = 0u;

        [ProtoMember(15, IsRequired = true)]
        public uint Score = 0u;

        [ProtoMember(16, IsRequired = true)]
        public uint FireMastery = 0u;

        [ProtoMember(17, IsRequired = true)]
        public uint WaterMastery = 0u;

        [ProtoMember(18, IsRequired = true)]
        public uint EarthMastery = 0u;

        [ProtoMember(19, IsRequired = true)]
        public uint WindMastery = 0u;

        [ProtoMember(20, IsRequired = true)]
        public uint LightningMastery = 0u;

        [ProtoMember(21, IsRequired = true)]
        public uint UnlockMastery = 0u;

        public static void Create(LegacyNativeGameClientRuntime user, LegacyNativeNinja Ninja, uint UID)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            MsgNinjaUser msgNinjaUser = new MsgNinjaUser();
            msgNinjaUser.Type = 0u;
            msgNinjaUser.UID = UID;
            msgNinjaUser.Points = Ninja.Exp;
            msgNinjaUser.Flag = Ninja.Flag;
            msgNinjaUser.Fire = Ninja.Fire;
            msgNinjaUser.Water = Ninja.Water;
            msgNinjaUser.Earth = Ninja.Earth;
            msgNinjaUser.Wind = Ninja.Wind;
            msgNinjaUser.Lightning = Ninja.Lightning;
            msgNinjaUser.FireNew = Ninja.tmp_Fire;
            msgNinjaUser.WaterNew = Ninja.tmp_Water;
            msgNinjaUser.EarthNew = Ninja.tmp_Earth;
            msgNinjaUser.WindNew = Ninja.tmp_Wind;
            msgNinjaUser.LightningNew = Ninja.tmp_Lightning;
            msgNinjaUser.Score = Ninja.Score;
            msgNinjaUser.FireMastery = Ninja.FireMastery;
            msgNinjaUser.WaterMastery = Ninja.WaterMastery;
            msgNinjaUser.EarthMastery = Ninja.EarthMastery;
            msgNinjaUser.WindMastery = Ninja.WindMastery;
            msgNinjaUser.LightningMastery = Ninja.LightningMastery;
            msgNinjaUser.UnlockMastery = Ninja.UnlockMastery;
            user.Send(stream.CreateNinjaUser(msgNinjaUser));
        }
    }
}
