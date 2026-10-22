namespace WolfConquer.Core.Data;

internal partial class LegacyNativeQuestInfo
{
    public partial class DBQuest
    {
        public byte TypeId = 0;

        public byte CompleteFlag = 0;

        public uint ActivityType = 0u;

        public ushort MissionId = 0;

        public string Name = "";

        public ushort Lv_min;

        public ushort Lv_max;

        public ushort First;

        public ushort Map;

        public uint[] Profession = null!;

        public ulong Exp = 0uL;

        public ulong Gold = 0uL;

        public byte Sex;

        public uint FinishTime;

        public uint ActivityBeginTime;

        public uint ActivityEndTime;

        public NpcInfo BeginNpcId = null!;

        public NpcInfo FinishNpcId = null!;

        public uint Intentions;

        public uint[] Prize = null!;

        public ushort[] Prequest = null!;
    }
}
