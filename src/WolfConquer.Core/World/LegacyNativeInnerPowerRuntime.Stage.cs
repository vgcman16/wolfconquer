using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Whole native InnerPower.Stage67..120, including misspelled public source members.
    public class Stage
    {
        public class NeiGong
        {
            public byte ID;
            public byte Score;
            public bool Unlocked = false;
            public byte level;
            public bool Complete = false;
        }

        public ushort ID;
        public bool UnLocked = false;
        public NeiGong[] NeiGongs = null!;
        public bool Complete => NeiGongs.Length == NeiGongs.Where((NeiGong p) => p.Complete).Count();
        public uint Score => (uint)NeiGongs.Sum((NeiGong p) => p.Score);

        public ushort GetNoumberAtributes(NeiGong[] gongs)
        {
            LegacyNativeInnerPowerTable.Stage stage = LegacyNativeInnerPowerTable.Stages[ID - 1];
            ushort num = 0;
            for (int i = 0; i < gongs.Length; i++)
            {
                num += (ushort)stage.NeiGongAtributes[i].AtributesValues.Length;
            }
            return num;
        }

        public ushort GetAtributesCount(ushort gong_noumber)
        {
            return (ushort)LegacyNativeInnerPowerTable.Stages[ID - 1].NeiGongAtributes[gong_noumber].AtributesValues.Length;
        }

        public bool GetLastNeiGang(out NeiGong? _gong)
        {
            _gong = null;
            for (int i = 0; i < NeiGongs.Length; i++)
            {
                if (NeiGongs[i].Unlocked)
                {
                    _gong = NeiGongs[i];
                }
            }
            return _gong != null;
        }
    }
}
