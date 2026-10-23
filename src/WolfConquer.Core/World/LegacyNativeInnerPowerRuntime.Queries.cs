using System.Collections.Generic;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Complete native InnerPower305..382. Queries retain physical array order, not ID sorting.
    public bool isUnlockedNeiGong(byte stage)
    {
        if (stage > 1)
        {
            Stage? _stage = null;
            Stage.NeiGong? _gong = null;
            if (TryGetStageAndGong((byte)(stage - 1), out _stage, out _gong))
            {
                return _gong!.Unlocked;
            }
        }
        return stage == 1;
    }

    public void AddPotency(LegacyNativePacket stream, LegacyNativeGameClientRuntime user, int _Potency)
    {
        Potency += _Potency;
        user.Player.SendUpdate(stream, Potency, LegacyNativeMsgUpdate.DataType.InnerPowerPotency);
    }

    public bool TryGetStageAndGong(byte ID, out Stage? _stage, out Stage.NeiGong? _gong)
    {
        Stage[] stages = Stages;
        foreach (Stage stage in stages)
        {
            Stage.NeiGong[] neiGongs = stage.NeiGongs;
            foreach (Stage.NeiGong neiGong in neiGongs)
            {
                if (ID == neiGong.ID)
                {
                    _stage = stage;
                    _gong = neiGong;
                    return true;
                }
            }
        }
        _stage = null;
        _gong = null;
        return false;
    }

    public Stage.NeiGong[] GetNeiGongs()
    {
        List<Stage.NeiGong> list = new List<Stage.NeiGong>();
        Stage[] stages = Stages;
        foreach (Stage stage in stages)
        {
            if (!stage.UnLocked)
            {
                continue;
            }
            Stage.NeiGong[] neiGongs = stage.NeiGongs;
            foreach (Stage.NeiGong neiGong in neiGongs)
            {
                if (neiGong.Unlocked)
                {
                    list.Add(neiGong);
                }
            }
        }
        return list.ToArray();
    }

    public bool GetLastStage(out Stage? stage)
    {
        stage = null;
        for (int i = 0; i < Stages.Length; i++)
        {
            Stage stage2 = Stages[i];
            if (stage2.UnLocked)
            {
                stage = stage2;
            }
        }
        return stage != null;
    }
}
