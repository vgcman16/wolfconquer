using System;
using ProtoBuf;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    // Complete native contract and diagnostic printer. Process remains wholly absent
    // until its genuine equipment, inventory, player-flag and currency dependencies close.
    [ProtoContract]
    public partial class MsgNinjaInfo
    {
        [Flags]
        public enum TypeID
        {
            Equip = 0,
            Unequip = 1,
            UpLevel = 2,
            BoostAptitude = 4,
            SaveDiscard = 5,
            Hat = 6,
            AddPoints = 7,
            AddMastery = 8,
            InstantUpgrade = 9,
            UnlockMastery = 0xB
        }

        [ProtoMember(1, IsRequired = true)]
        public TypeID Type = TypeID.Equip;

        [ProtoMember(2, IsRequired = true)]
        public uint ID = 0u;

        [ProtoMember(3, IsRequired = true)]
        public uint Points = 0u;

        [ProtoMember(4, IsRequired = true)]
        public uint Position = 0u;

        public static void PrintPacket(byte[] packet)
        {
            foreach (byte b in packet)
            {
                Console.Write(Convert.ToString(b, 16).PadLeft(2, '0').ToUpper() + " ");
            }
            Console.Write("\n\n");
        }
    }
}
