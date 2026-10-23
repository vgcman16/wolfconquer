namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player869. The real deletion queue/lifecycle is not installed here.
    public bool Delete = false;

    // Native Player1081 only. Full Player.CompleteLogin owns future assignment;
    // ordinary physical Player birth does not allocate a substitute quest owner.
    internal LegacyNativeQuestsRuntime? QuestGUI;
}
