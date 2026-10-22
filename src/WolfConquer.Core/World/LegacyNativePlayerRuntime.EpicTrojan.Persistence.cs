using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native Player2892..2942 '/'-record leaf methods. No SaveClient or
// login caller is installed. Native array accesses and partial faults are literal.
public sealed partial class LegacyNativePlayerRuntime
{
    public string SaveEpicTrojan()
    {
        LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
        writeLine.Add(EpicTrojanItemChange[0]).Add(EpicTrojanItemChange[1]).Add(StageEpicTrojanQuest)
            .Add(ChangeEpicTrojan)
            .Add(CanChangeEpicMaterial)
            .Add(CanChangeArrayEpicMaterial)
            .Add(ChangeArrayEpicTrojan)
            .Add(EpicTrojanArrayItemChange[0])
            .Add(EpicTrojanArrayItemChange[1])
            .Add(EpicTrojanEvilArrayPoints)
            .Add(EpicTrojanAbysalStage)
            .Add(EpicTrojanMr_MirrorItemChange[0])
            .Add(EpicTrojanMr_MirrorItemChange[1])
            .Add(ChangeMr_MirrorEpicTrojan)
            .Add(CanChangeMr_MirrorEpicMaterial)
            .Add(EpicTrojanGeneralPakItemChange[0])
            .Add(EpicTrojanGeneralPakItemChange[1])
            .Add(ChangeGeneralPakEpicTrojan)
            .Add(CanChangGeneralPakMaterial)
            .Add(EpicTrojanMrMirrorPrograss);
        return writeLine.Close();
    }

    public void LoadEpicTrojan(string? line)
    {
        if (line != null)
        {
            LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
            EpicTrojanItemChange[0] = readLine.Read(0u);
            EpicTrojanItemChange[1] = readLine.Read(0u);
            StageEpicTrojanQuest = readLine.Read((byte)0);
            ChangeEpicTrojan = readLine.Read(0u);
            CanChangeEpicMaterial = readLine.Read((byte)0);
            CanChangeArrayEpicMaterial = readLine.Read((byte)0);
            ChangeArrayEpicTrojan = readLine.Read(0u);
            EpicTrojanArrayItemChange[0] = readLine.Read(0u);
            EpicTrojanArrayItemChange[1] = readLine.Read(0u);
            EpicTrojanEvilArrayPoints = readLine.Read(0u);
            EpicTrojanAbysalStage = readLine.Read(0u);
            EpicTrojanMr_MirrorItemChange[0] = readLine.Read(0u);
            EpicTrojanMr_MirrorItemChange[1] = readLine.Read(0u);
            ChangeMr_MirrorEpicTrojan = readLine.Read(0u);
            CanChangeMr_MirrorEpicMaterial = readLine.Read((byte)0);
            EpicTrojanGeneralPakItemChange[0] = readLine.Read(0u);
            EpicTrojanGeneralPakItemChange[1] = readLine.Read(0u);
            ChangeGeneralPakEpicTrojan = readLine.Read(0u);
            CanChangGeneralPakMaterial = readLine.Read((byte)0);
            EpicTrojanMrMirrorPrograss = readLine.Read((byte)0);
        }
    }
}
