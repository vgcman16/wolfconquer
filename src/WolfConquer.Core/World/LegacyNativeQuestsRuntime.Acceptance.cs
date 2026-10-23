using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    public bool Accept(LegacyNativeQuestInfo.DBQuest quest, uint time) =>
        AcceptPreparedQuest(quest, time, kingdomOnly: false, out _);

    public bool AcceptKingDomMission(LegacyNativeQuestInfo.DBQuest quest, uint time, out Row _quest) =>
        AcceptPreparedQuest(quest, time, kingdomOnly: true, out _quest);

    private bool AcceptPreparedQuest(LegacyNativeQuestInfo.DBQuest quest, uint time,
        bool kingdomOnly, out Row acceptedRow)
    {
        acceptedRow = null!;
        var player = PersistenceOwner;
        if (player is null || player.Owner is not { } client)
            throw new InvalidDataException("Quest acceptance has no genuine constructor owner.");
        QuestAcceptancePublication? publication = null;
        QuestAcceptanceGraph? graph = null;
        QuestAcceptanceMaps? maps = null;
        try
        {
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (PersistenceSyncRoot)
            {
                try
                {
                    if (quest is null) throw new InvalidDataException("Quest acceptance metadata is unavailable.");
                    var definition = new QuestAcceptanceDefinition(quest, quest.MissionId, quest.Intentions);
                    graph = new QuestAcceptanceGraph(this, client, player);
                    if (!definition.IsCurrent()) throw new InvalidDataException("Quest acceptance metadata changed.");
                    if (kingdomOnly && !graph.IsKingdom(definition.Uid)) return false;
                    if (graph.TryGetExisting(definition.Uid, out var existing))
                    {
                        if (!kingdomOnly) return false;
                        acceptedRow = existing;
                        return existing.Status != Row.QuestStatus.Finished;
                    }

                    uint count = kingdomOnly ? definition.Count : definition.Uid switch
                    {
                        1469 => 4u,
                        409 => 3u,
                        _ => definition.Count
                    };
                    graph.ValidateAppendCount(count);
                    var created = new Row
                    {
                        UID = definition.Uid, Status = Row.QuestStatus.Accepted,
                        Time = time, Intentions = new uint[checked((int)count)]
                    };
                    publication = new QuestAcceptancePublication(
                        LegacyNativeQuestPacket.PrepareQuestAcceptanceFrame(created.UID, created.Time, LegacyNativePacket.SealString));
                    maps = graph.Stage(created, kingdomOnly);
                    // Stage can invoke genuine custom comparers. Everything after
                    // that work is physical, callback-free recapture/validation.
                    if (!graph.IsCurrent() || !graph.MatchesPrepared(maps) ||
                        !definition.IsCurrent() || !publication.IsPrepared())
                        throw new InvalidDataException("Quest acceptance preparation is stale.");
                    src = maps.Source;
                    AcceptedQuests = maps.Accepted;
                    acceptedRow = created;
                }
                catch (Exception error) when (error is OverflowException or ArgumentException or InvalidOperationException)
                {
                    throw new InvalidDataException("Quest acceptance preparation was rejected.", error);
                }
            }

            // The graph was accepted already. A dropped/failed delivery is not
            // rejection and cannot authorize a fabricated map/row rollback.
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            lock (PersistenceSyncRoot)
                if (graph!.IsCommitted(maps!) && publication!.IsPrepared())
                {
                    try { client.Send(publication.Packet); }
                    catch (Exception)
                    {
                        // The accepted maps and out row are already committed.
                        // Even the native sender's own diagnostics may fail; do
                        // not log through that path again or invite a retry.
                    }
                }
            return true;
        }
        finally
        {
            publication?.Dispose();
        }
    }

    private sealed record QuestAcceptanceDefinition(LegacyNativeQuestInfo.DBQuest Metadata, ushort Uid, uint Count)
    {
        internal bool IsCurrent() => Metadata.MissionId == Uid && Metadata.Intentions == Count;
    }
}
