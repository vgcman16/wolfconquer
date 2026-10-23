namespace WolfConquer.Core.World;

// Actual native Quests fields and whole constructor13..24, NOT a fake quest DTO.
// Other complete methods live in partials; award/message-box callers remain held.
internal partial class LegacyNativeQuestsRuntime
{
    public LegacyNativePlayerRuntime Player;

    public Dictionary<uint, LegacyNativeMsgQuestList.QuestListItem> src;

    public Dictionary<uint, LegacyNativeMsgQuestList.QuestListItem> AcceptedQuests;

    // Constructor identity and cooperating persistence gate, not raw-writer safety.
    internal readonly LegacyNativePlayerRuntime PersistenceOwner;
    internal readonly object PersistenceSyncRoot = new();

    public LegacyNativeQuestsRuntime(LegacyNativePlayerRuntime _owner)
    {
        Player = _owner;
        PersistenceOwner = _owner;
        AcceptedQuests = new Dictionary<uint, LegacyNativeMsgQuestList.QuestListItem>();
        src = new Dictionary<uint, LegacyNativeMsgQuestList.QuestListItem>();
    }
}
