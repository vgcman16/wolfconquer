using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyVictoryChestTransactionTests
{
    private sealed partial class Fixture
    {
        private readonly List<FileStream> faultLeases = [];

        internal void PrepareFault(string fault)
        {
            Assert.True(recovery.IsReadyFor(Users, Items));
            Assert.False(recovery.IsBlocked);
            Assert.True(Current.Character.PlayerQuests.TryGetQuest(QuestId, out var quest));
            Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
            Assert.Equal(new uint[] { 100 }, quest.Intentions);
            if (fault == "questsAuthorityMissing") questAuthority = false;
            else if (fault == "questsDurableFinished")
            {
                var finished = Current.Character.PlayerQuests.WithQuest(quest with { Status = PlayerQuestStatus.Finished });
                new PlayerQuestSnapshotLoader(Quests).SaveByCharacterUid(CharacterUid, finished);
                Assert.Equal(PlayerQuestSnapshotLoader.Serialize(finished), File.ReadAllBytes(QuestPath));
            }
            else if (fault is not ("itemsLocked" or "questsLocked")) throw new ArgumentException("Unknown owned fault.", nameof(fault));
            // The live Accepted owner is deliberately not reset/replaced by a disk fault.
            Assert.Equal(questsBefore, PlayerQuestSnapshotLoader.Serialize(Current.Character.PlayerQuests));
        }

        internal async Task ReplyWithFaultAsync(string fault)
        {
            if (fault is not ("itemsLocked" or "questsLocked")) { await ReplyAsync(); return; }
            var lease = new FileStream(fault == "itemsLocked" ? ItemPath : QuestPath,
                FileMode.Open, FileAccess.Read, FileShare.None);
            faultLeases.Add(lease);
            Exception? primary = null;
            try { await ReplyAsync(); }
            catch (Exception error) { primary = error; }
            // An observation timeout is not completion. Keep this real leaf
            // lease and fixture alive while the actual handler remains active.
            if (calls.Any(call => !call.IsCompleted))
            {
                if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
                throw new InvalidOperationException("Owned Victory handler completion is unproven.");
            }
            Exception? cleanup = null;
            try { lease.Dispose(); faultLeases.Remove(lease); }
            catch (Exception error) { cleanup = error; }
            if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
            if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        }

        internal void RepairFault(string fault)
        {
            Assert.Empty(faultLeases);
            if (fault == "questsAuthorityMissing") questAuthority = true;
            else if (fault == "questsDurableFinished") File.WriteAllBytes(QuestPath, questsBefore);
            else if (fault is not ("itemsLocked" or "questsLocked")) throw new ArgumentException("Unknown owned fault.", nameof(fault));
            Assert.Equal(characterBefore, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemsBefore, File.ReadAllBytes(ItemPath));
            Assert.Equal(questsBefore, File.ReadAllBytes(QuestPath));
            Assert.True(recovery.IsReadyFor(Users, Items));
            Assert.False(recovery.IsBlocked);
            Sent.Clear(); Log.GetStringBuilder().Clear();
        }
    }
}
