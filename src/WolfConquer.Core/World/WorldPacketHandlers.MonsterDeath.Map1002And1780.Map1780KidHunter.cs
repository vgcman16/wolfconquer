using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryApplyLegacyMap1780Family12314463BezoarDropAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_780 ||
            monsterFamily.Id != 12_314_463)
        {
            return;
        }

        const uint itemId = 721_714;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1780 && Family.ID == 12314463 Bezoar drop";
        await context.Connection.SendAsync(
                LegacyStringPacket
                    .PlayerEffect(CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)), "relive1")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var sentCount = await SendLegacyMapSpecialMonsterDeathDropBundleAsync(
                context,
                session,
                monster,
                monsterFamily,
                itemId,
                count: 1,
                scatterRange: 3,
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1780 KidSiezer Bezoar drop " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"itemId={itemId} sent={sentCount} effect=relive1 branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyMap1780Family12314462KidSiezerSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_780 ||
            monsterFamily.Id != 12_314_462)
        {
            return;
        }

        const int spawnedMonsterId = 12_314_463;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1780 && Family.ID == 12314462 KidSiezer spawn";
        var dynamicId = ReadCharacterDynamicId(session.Character);
        if (HasVisibleLegacyMonsterInScreen(
                context,
                session.Character,
                checked((uint)spawnedMonsterId),
                "KidSiezer"))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1780 KidSiezer spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"spawnedMonsterId={spawnedMonsterId} dynamicId={dynamicId}; visible KidSiezer already in screen " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        var spawned = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                context,
                session,
                monster,
                monsterFamily,
                spawnedMonsterId,
                mapId: 1_780,
                x: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                dynamicId: checked((long)dynamicId),
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "MsgItemPacket.ItemPacketCreate",
                    "map1780-kidsiezer-spawn-item-packet",
                    reason: "source sends a MsgItemPacket.Create frame with UID=2 and player X/Y after spawning KidSiezer; modern runtime models the spawned monster and dialog, but this client frame shape is not decoded yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        LegacyParityGapDiagnostics.Fact("x", session.Character.X),
                        LegacyParityGapDiagnostics.Fact("y", session.Character.Y),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                    ]))
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox("The~Kid~Siezer~appeared.~Go~kill~it.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1780 KidSiezer spawn branch " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"spawnedMonsterId={spawnedMonsterId} spawned={spawned} dynamicId={dynamicId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryApplyLegacyMap1780KidHunterSpawnAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        if (monster.MapId != 1_780 ||
            monsterFamily.Id is 12_314_463 or 12_314_462)
        {
            return;
        }

        const int spawnedMonsterId = 12_314_462;
        const uint bezoarItemId = 721_714;
        const string sourceBranch =
            "MonsterRole.cs killer.Player.Map == 1780 && Core.Rate(10) KidHunter spawn";
        if (!CheckLegacyMap1780KidHunterSpawnRate(session.Character))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1780 KidHunter spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"spawnedMonsterId={spawnedMonsterId} reason=\"Core.Rate(10)=false\" branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        var dynamicId = ReadCharacterDynamicId(session.Character);
        if (HasVisibleLegacyMonsterInScreen(
                context,
                session.Character,
                checked((uint)spawnedMonsterId),
                "KidHunter"))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1780 KidHunter spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"spawnedMonsterId={spawnedMonsterId} dynamicId={dynamicId}; visible KidHunter already in screen " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        if (CountLegacyInventoryItem(session.PlayerItems, bezoarItemId) >= 1)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped legacy map 1780 KidHunter spawn " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                    $"spawnedMonsterId={spawnedMonsterId} itemId={bezoarItemId} reason=\"Bezoar already in inventory\" " +
                    $"branch=\"{sourceBranch}\".")
                .ConfigureAwait(false);
            return;
        }

        var spawned = await TrySendLegacyMonsterDeathDynamicSpawnAsync(
                context,
                session,
                monster,
                monsterFamily,
                spawnedMonsterId,
                mapId: 1_780,
                x: CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                y: CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                dynamicId: checked((long)dynamicId),
                sourceBranch,
                cancellationToken)
            .ConfigureAwait(false);
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                    "MsgItemPacket.ItemPacketCreate",
                    "map1780-kidhunter-spawn-item-packet",
                    reason: "source sends a MsgItemPacket.Create frame with UID=2 and player X/Y after spawning KidHunter; modern runtime models the spawned monster and dialog, but this client frame shape is not decoded yet",
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("map", monster.MapId),
                        LegacyParityGapDiagnostics.Fact("monsterId", monster.MonsterId),
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                        LegacyParityGapDiagnostics.Fact("x", session.Character.X),
                        LegacyParityGapDiagnostics.Fact("y", session.Character.Y),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("familyId", monsterFamily.Id),
                        LegacyParityGapDiagnostics.Fact("spawnedMonsterId", spawnedMonsterId),
                    ]))
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket
                    .ForMessageBox("The~Kid~Hunter~appeared.~Go~kill~it.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy map 1780 KidHunter spawn branch " +
                $"{FormatSession(context)} monsterUid={monster.Uid} familyId={monsterFamily.Id} " +
                $"spawnedMonsterId={spawnedMonsterId} spawned={spawned} dynamicId={dynamicId} branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
    }

    private static bool CheckLegacyMap1780KidHunterSpawnRate(CharacterSnapshot character)
    {
        return TryReadAttributeBool(
                character.Attributes,
                out var forced,
                RuntimeLegacyMap1780KidHunterSpawnRateAttribute)
            ? forced
            : Random.Shared.Next(0, 100) < 10;
    }

    private static bool HasVisibleLegacyMonsterInScreen(
        WorldPacketHandlerContext context,
        CharacterSnapshot character,
        uint monsterId,
        string monsterName)
    {
        foreach (var target in EnumerateVisibleMonsterTargets(context, character))
        {
            if (target.Monster.MonsterId == monsterId ||
                string.Equals(target.Monster.Name, monsterName, StringComparison.Ordinal) ||
                string.Equals(target.MonsterFamily.Name, monsterName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
