using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    public void SetKingDomQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
    {
        ApplyObjectiveMutation(stream, UID, Intentions, ObjectiveMutation.KingdomSet);
    }

    public void SetQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
    {
        ApplyObjectiveMutation(stream, UID, Intentions, ObjectiveMutation.Set);
    }

    public void IncreaseQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
    {
        ApplyObjectiveMutation(stream, UID, Intentions, ObjectiveMutation.Increase);
    }
}
