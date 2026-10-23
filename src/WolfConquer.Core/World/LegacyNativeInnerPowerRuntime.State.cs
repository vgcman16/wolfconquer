using System.Collections.Concurrent;
using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Complete native InnerPower122..160 and183..203. Nullable defaults remain physical nulls.
    public static ConcurrentDictionary<uint, LegacyNativeInnerPowerRuntime> InnerPowerPolle = new();
    public int Potency = 0;
    public uint UID;
    public string Name = "";
    public Stage[] Stages = new Stage[LegacyNativeInnerPowerTable.Count];
    public uint TotalScore => (uint)Stages.Sum((Stage p) => p.Score);
    public uint CriticalStrike { get; private set; }
    public uint SkillCriticalStrike { get; private set; }
    public uint Immunity { get; private set; }
    public uint Breakthrough { get; private set; }
    public uint Counteraction { get; private set; }
    public uint MaxLife { get; private set; }
    public uint AddAttack { get; private set; }
    public uint AddMagicAttack { get; private set; }
    public uint AddMagicDefense { get; private set; }
    public uint FinalAttack { get; private set; }
    public uint FinalMagicAttack { get; private set; }
    public uint FinalDefense { get; private set; }
    public uint FinalMagicDefense { get; private set; }
    public uint Defence { get; private set; }

    public LegacyNativeInnerPowerRuntime(string _Name, uint _uid)
    {
        Name = _Name;
        UID = _uid;
        for (int i = 0; i < LegacyNativeInnerPowerTable.Count; i++)
        {
            LegacyNativeInnerPowerTable.Stage stage = LegacyNativeInnerPowerTable.Stages[i];
            Stage stage2 = new Stage
            {
                ID = stage.ID,
                NeiGongs = new Stage.NeiGong[stage.NeiGongNum],
            };
            for (int j = 0; j < stage.NeiGongNum; j++)
            {
                stage2.NeiGongs[j] = new Stage.NeiGong();
                stage2.NeiGongs[j].ID = stage.NeiGongAtributes[j].ID;
            }
            Stages[i] = stage2;
        }
        InnerPowerPolle.TryAdd(_uid, this);
    }
}
