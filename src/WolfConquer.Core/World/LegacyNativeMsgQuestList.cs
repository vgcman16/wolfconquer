using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Complete native physical declarations only. The whole Process remains absent pending
// genuine Quests gameplay/experience/message-box graph; no route is installed.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgQuestList
{
    public class QuestListItem
    {
        public enum QuestStatus : uint
        {
            Accepted,
            Finished,
            Available
        }

        public uint UID;
        public QuestStatus Status;
        public uint Time;
        public uint[] Intentions = new uint[1];
    }

    public enum QuestMode : ushort
    {
        AcceptQuest = 1,
        QuitQuest,
        Review,
        FinishQuest
    }
}
