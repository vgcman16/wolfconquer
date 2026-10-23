using System.Linq;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeInnerPowerRuntime
{
    // Complete native InnerPower162..181, slash terminator and locked-stage filtering retained.
    public override string ToString()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(Name).Add(UID).Add(Potency);
        Stage[] array = Stages.Where((Stage p) => p.UnLocked).ToArray();
        writeLine.Add(array.Length);
        Stage[] array2 = array;
        foreach (Stage stage in array2)
        {
            writeLine.Add(stage.ID).Add((byte)(stage.UnLocked ? 1u : 0u)).Add(stage.NeiGongs.Length);
            Stage.NeiGong[] neiGongs = stage.NeiGongs;
            foreach (Stage.NeiGong neiGong in neiGongs)
            {
                writeLine.Add(neiGong.ID).Add(neiGong.Score).Add((byte)(neiGong.Unlocked ? 1u : 0u))
                    .Add(neiGong.level).Add((byte)(neiGong.Complete ? 1u : 0u));
            }
        }
        return writeLine.Close();
    }
}
