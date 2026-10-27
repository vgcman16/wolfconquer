using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask LogLegacySessionSidecarStatusAsync(WorldSession session)
    {
        try
        {
            var playerItems = session.PlayerItems;
            var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
            var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
            var hitpoints = session.Character.MinHitPoints <= 0
                ? 1U
                : CheckedUInt32(session.Character.MinHitPoints, nameof(session.Character.MinHitPoints));
            var maxHitpoints = ComputeLegacyStartupMaxHitpoints(
                session.Character,
                hitpoints,
                playerItems,
                combatGear,
                _worldData);
            var refineLevel = LegacyUserTotalRefineLevPacket
                .FromEquipmentPerfection(uid, playerItems)
                .Level;
            var status = LegacyPlayerStatusCalculator.Create(
                session.Character,
                playerItems,
                _worldData,
                maxHitpoints,
                refineLevel,
                combatGear,
                playerSpells: session.PlayerSpells);

            await _log.WriteLineAsync(
                    "WorldServer loaded legacy character sidecars " +
                    $"{FormatSession(session)} " +
                    $"usersRoot={session.UsersRoot ?? "none"} " +
                    $"playerItemsRoot={_snapshotLoader?.PlayerItemsRoot ?? "none"} " +
                    $"playersCombatGearRoot={_snapshotLoader?.PlayersCombatGearRoot ?? "none"} " +
                    $"inventoryItems={playerItems.InventoryItems.Count} " +
                    $"equipmentItems={playerItems.EquipmentItems.Count} " +
                    $"equipment={FormatEquipmentSummary(playerItems)} " +
                    $"runeItems={playerItems.RuneItems.Count} " +
                    $"wardrobeItems={playerItems.WardrobeItems.Count} " +
                    $"warehouseItems={playerItems.WarehouseItems.Count} " +
                    $"combatGearItems={combatGear.Items.Count} " +
                    $"combatGearEquipped={combatGear.Items.Count(item => item.Equip != 0)} " +
                    $"combatGear={FormatCombatGearSummary(combatGear)} " +
                    $"statusMaxHp={status.MaxHP} " +
                    $"statusDefence={status.Defence} " +
                    $"statusMagicDefence={status.MagicDefence} " +
                    $"statusDamage={status.Damage} " +
                    $"statusItemBless={status.ItemBless} " +
                    $"statusDodge={status.Dodge} " +
                    $"statusPhysicalDamageDecrease={status.PhysicalDamageDecrease} " +
                    $"statusMagicDamageDecrease={status.MagicDamageDecrease}.")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            await _log.WriteLineAsync(
                    "PARITY-BUG world sidecar status diagnostic failed " +
                    $"{FormatSession(session)} exception={ex.GetType().Name}: {ex.Message}")
                .ConfigureAwait(false);
        }
    }

    private static string FormatEquipmentSummary(PlayerItemSnapshot playerItems)
    {
        if (playerItems.EquipmentItems.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ',',
            playerItems.EquipmentItems
                .OrderBy(item => item.Position)
                .ThenBy(item => item.ItemId)
                .ThenBy(item => item.Uid)
                .Select(item =>
                    item.ItemId.ToString(CultureInfo.InvariantCulture) +
                    ":" +
                    item.Position.ToString(CultureInfo.InvariantCulture)));
    }

    private static string FormatCombatGearSummary(CombatGearSnapshot combatGear)
    {
        var equippedItems = combatGear.Items
            .Where(item => item.Equip != 0)
            .OrderBy(item => item.Equip)
            .ThenBy(item => item.ItemID)
            .ToArray();

        if (equippedItems.Length == 0)
        {
            return "none";
        }

        return string.Join(
            ',',
            equippedItems.Select(item =>
                item.ItemID.ToString(CultureInfo.InvariantCulture) +
                ":" +
                item.Equip.ToString(CultureInfo.InvariantCulture)));
    }

    private ValueTask LogHandlerExceptionAsync(WorldSession? session, PacketFrame frame, Exception exception)
    {
        return LogRuntimeExceptionAsync(
            session,
            frame,
            exception,
            sourceSeam: "WorldPacketHandlers.Registry",
            surface: "world-packet-handler");
    }

    private ValueTask LogFrameExceptionAsync(
        ConquerTcpConnection connection,
        PacketFrame frame,
        Exception exception,
        string sourceSeam,
        string surface)
    {
        _sessions.TryGet(connection, out var session);
        return LogRuntimeExceptionAsync(session, frame, exception, sourceSeam, surface);
    }

    private async ValueTask LogRuntimeExceptionAsync(
        WorldSession? session,
        PacketFrame frame,
        Exception exception,
        string sourceSeam,
        string surface)
    {
        var diagnostic = LegacyParityGapDiagnostics.RuntimeException(
            surface,
            frame.Header.Opcode,
            (int)frame.Payload.Length,
            exception.GetType().Name,
            sourceSeam,
            nextSeam: "inspect-runtime-exception",
            reason: "runtime exception was caught without terminating the live server; inspect the error log and decompiled source dispatch for parity triage");
        await _log.WriteLineAsync(diagnostic.FormatLogMessage()).ConfigureAwait(false);
        var exceptionDetail = frame.Header.Opcode == WorldPacketOpcodes.LegacySecondaryPassword
            ? exception.GetType().Name + ": [secondary password details redacted]"
            : exception.GetType().Name + ": " + exception.Message;
        await _log.WriteLineAsync(
                "PARITY-BUG world runtime exception " +
                $"{FormatSession(session)} {FormatFrameDiagnostic(frame)} " +
                $"sourceSeam={sourceSeam} exception={exceptionDetail}")
            .ConfigureAwait(false);
        Console.Error.WriteLine(
            "PARITY-BUG world runtime exception " +
            $"{DateTimeOffset.UtcNow:O} {FormatSession(session)} {FormatFrameDiagnostic(frame)} " +
            $"sourceSeam={sourceSeam}{Environment.NewLine}" +
            (frame.Header.Opcode == WorldPacketOpcodes.LegacySecondaryPassword ? exceptionDetail : exception.ToString()));
    }

    private static string FormatSession(WorldSession? session)
    {
        if (session is null)
        {
            return "session=none";
        }

        return "session=" +
            $"accountId={session.AccountId} " +
            $"username={session.Username} " +
            $"characterUid={session.Character.Uid} " +
            $"characterName={session.Character.Name} " +
            $"map={session.Character.MapId} " +
            $"x={session.Character.X} " +
            $"y={session.Character.Y} " +
            $"hp={session.Character.MinHitPoints} " +
            $"money={session.Character.Money} " +
            $"cp={session.Character.ConquerPoints}";
    }

    private static string FormatFrameDiagnostic(PacketFrame frame)
    {
        return "opcode=" + frame.Header.Opcode +
            " payloadLength=" + frame.Payload.Length.ToString(CultureInfo.InvariantCulture);
    }
}
