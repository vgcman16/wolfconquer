namespace WolfConquer.Core.World;

// Genuine Associate member storage. Live BattlePower/IsOnline and record writer remain separate.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class Member
    {
        public uint UID = 0u;
        public uint Mesh;
        public uint Level;
        public uint NobilityRank;
        public uint Class;
        public string Signature = "";
        public ulong Timer = 0uL;
        public uint ExpBalls = 0u;
        public uint Stone = 0u;
        public uint KillMap = 0u;
        public uint Blessing = 0u;
        public string Name = "";
        public ushort KillsCount = 0;

        public int GetTimerLeft()
        {
            if (Timer == 0) return 0;
            int num = NativeTimerMinutesToInt32(new TimeSpan((long)Timer).TotalMinutes - new TimeSpan(DateTime.Now.Ticks).TotalMinutes);
            if (num <= 0)
            {
                Timer = 0uL;
                return 0;
            }
            return num;
        }

        // Same native conv.i4 primitive independently probed by root for Rate(double):
        // .artifacts/chi-native-conversion-probe. Keep this one timer cast scoped;
        // modern saturation would retain extreme timers native immediately expires.
        private static int NativeTimerMinutesToInt32(double value)
        {
            return value >= int.MinValue && value < 2147483648.0
                ? unchecked((int)value)
                : int.MinValue;
        }
    }
}
