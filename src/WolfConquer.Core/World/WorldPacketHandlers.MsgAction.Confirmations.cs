using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleHotkeysActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer echoed source-backed MsgAction Hotkeys " +
                $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; legacy MsgDataPacket.Hotkeys [Data(75)] sends msg.ActionCreate(data).")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleConfirmAssociatesAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer echoed source-backed MsgAction ConfirmAssociates " +
                $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; legacy MsgDataPacket.ConfirmAssociates [Data(76)] sends msg.ActionCreate(data).")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleConfirmGuildAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer echoed source-backed MsgAction ConfirmGuild " +
                $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; legacy MsgDataPacket.ConfirmGuild [Data(97)] sends msg.ActionCreate(data) after optional guild-state refresh.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleAwayActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction Away; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var currentAway = ReadAttributeUInt64(session.Character.Attributes, "Away") != 0;
        var toggledAway = packet.ObjId == CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid))
            ? !currentAway
            : false;
        var updatedCharacter = WithCharacterAttribute(session.Character, "Away", toggledAway ? 1UL : 0UL);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await SendPlayerSpawnToVisiblePeersAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction Away " +
                $"{FormatSession(updatedContext)} away={(toggledAway ? 1 : 0)} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; legacy MsgDataPacket.[Data(161)] toggles Player.Away and refreshes visible role spawn.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleGhostActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction Ghost; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        if (character.MinHitPoints > 0 && !HasLegacyStatusFlag(character, LegacyDeadFlag))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction Ghost " +
                    $"{FormatSession(context)} hitpoints={character.MinHitPoints}; " +
                    "source MsgDataPacket.Ghost [Data(145)] only responds when Player.Alive is false.")
                .ConfigureAwait(false);
            return;
        }

        var uid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var effectFrame = LegacyStringPacket.PlayerEffect(uid, "ghost").ToFrameBytes();
        var actionFrame = (packet with
        {
            ObjId = uid,
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        }).ToFrameBytes();

        await SendFrameToVisiblePlayersAsync(context, session, effectFrame, cancellationToken)
            .ConfigureAwait(false);
        await SendFrameToVisiblePlayersAsync(context, session, actionFrame, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction Ghost " +
                $"{FormatSession(context)} x={character.X} y={character.Y}; " +
                "source=MsgDataPacket.Ghost [Data(145)] sends effect 'ghost' and echoes ActionCreate(data) to view when dead.")
            .ConfigureAwait(false);
    }



    private static async ValueTask HandleQueryEquipmentActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer source-backed no-op MsgAction QueryEquipment " +
                    $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                    $"dwParam={packet.DwParam}; legacy ActionType.QueryEquipment=408 is defined, " +
                    "but no active world session is available and source has no [Data(408)] no-session side effect.")
                .ConfigureAwait(false);
            return;
        }

        await SendPlayerEquipmentAppearanceRefreshAsync(context, session, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled source-backed MsgAction QueryEquipment " +
                $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; sent LegacyShowEquipment plus player-spawn appearance refresh " +
                "using the local QueryEquipment-equivalent path shared by old source equipment/status refresh seams.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyUnregistered476ActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await context.Log.WriteLineAsync(
                "WorldServer source-backed no-op MsgAction LegacyUnregistered476 " +
                $"{FormatSession(context)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam}; legacy MsgDataPacket.Process only invokes registered [Data] handlers, " +
                "ActionType.cs defines no 476 constant, and source has no [Data(476)].")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleChangeDirectionActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction ChangeDirection {FormatSession(context)}; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithCharacterAttribute(session.Character, "Angle", packet.Facing);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var updatedContext = context with { Session = updatedSession };
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction ChangeDirection " +
                $"{FormatSession(updatedContext)} angle={packet.Facing}; " +
                "source=MsgDataPacket.ChangeDirection [Data(79)] updates Player.Angle.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleChangeStanceAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction ChangeStance {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        var playerUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var stance = unchecked((ushort)packet.DwParam);
        var removedActivePick = HasActivePick(character);
        var updatedCharacter = WithCharacterAttribute(character, "Action", stance);
        if (removedActivePick)
        {
            updatedCharacter = WithoutCharacterAttribute(updatedCharacter, "ActivePick");
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var skinType = LegacyDragonSkinState.FromSnapshot(updatedCharacter).EquippedSkin;
        var animaSkinFrame = new LegacyDragonSkinPacket(
            LegacyDragonSkinType.Effect,
            playerUid,
            skinType,
            OK: false).ToFrameBytes();
        var action = packet with
        {
            ObjId = playerUid,
            DwParam = stance,
        };
        var actionFrame = action.ToFrameBytes();

        await context.Connection.SendAsync(animaSkinFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(actionFrame, cancellationToken).ConfigureAwait(false);

        var visibleSessions = new List<WorldActiveSession>();
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == updatedCharacter.Uid ||
                    !CanSeePlayer(active.Session.Character, updatedCharacter))
                {
                    continue;
                }

                await active.Connection.SendAsync(animaSkinFrame, cancellationToken).ConfigureAwait(false);
                await active.Connection.SendAsync(actionFrame, cancellationToken).ConfigureAwait(false);
                visibleSessions.Add(active);
            }
        }

        var castPrayEchoes = 0;
        if (HasLegacyStatusFlag(updatedCharacter, LegacyCastPrayFlag))
        {
            foreach (var target in visibleSessions)
            {
                if (LegacyGetDistance(
                        updatedCharacter.X,
                        updatedCharacter.Y,
                        target.Session.Character.X,
                        target.Session.Character.Y) > 4)
                {
                    continue;
                }

                var castPrayFrame = (action with
                {
                    Timestamp = CheckedUInt32(target.Session.Character.Uid, nameof(target.Session.Character.Uid)),
                }).ToFrameBytes();
                await context.Connection.SendAsync(castPrayFrame, cancellationToken).ConfigureAwait(false);
                foreach (var active in visibleSessions)
                {
                    await active.Connection.SendAsync(castPrayFrame, cancellationToken).ConfigureAwait(false);
                }

                castPrayEchoes++;
            }
        }

        if (removedActivePick)
        {
            await SendChangeStanceActivePickRemovalAsync(context, session, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction ChangeStance " +
                $"{FormatSession(context)} action={stance} skinType={skinType} " +
                $"visiblePlayerCount={visibleSessions.Count} castPrayEchoes={castPrayEchoes} " +
                $"activePickRemoved={removedActivePick}; source=MsgDataPacket.ChangeStance [Data(81)].")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendChangeStanceActivePickRemovalAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            DwParam = LegacyActionPickDwParam,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.ActionPick,
            Facing = 2,
            RemainTime = 0,
            Strings = ["Interrupted"],
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent MsgAction ChangeStance ActivePick interruption " +
                $"{FormatSession(context)}.")
            .ConfigureAwait(false);
    }

    private static CharacterSnapshot WithoutLegacyStatusFlag(CharacterSnapshot character, int flag)
    {
        if (flag == 212) character = LegacyFineRain212Invocation.Forget(character);
        if (flag == LegacyLightningShieldActivationLifetime.StatusFlag)
            character = LegacyLightningShieldActivationLifetime.Forget(character);
        if (flag == LegacyLightningShieldBaseLifetime.StatusFlag)
            character = LegacyLightningShieldBaseLifetime.Forget(character);
        character = LegacyArrowBladesFlagRegistration.Forget(character, flag);
        character = LegacyStatusFlagDeathRegistration.Forget(character, flag);
        if (flag < 0 ||
            !TryGetLegacyStatusFlagsAttribute(character.Attributes, out var key, out var raw))
        {
            return character;
        }

        var wordIndex = flag / 32;
        var bitIndex = flag % 32;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (wordIndex >= parts.Length)
        {
            return character;
        }

        var words = new ulong[Math.Max(10, parts.Length)];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] &= ~(1UL << bitIndex);
        return WithCharacterAttribute(character, key, string.Join('/', words) + "/");
    }

    private static CharacterSnapshot WithoutLegacyStatusFlags(CharacterSnapshot character, IReadOnlyCollection<int> flags)
    {
        foreach (var flag in flags)
        {
            character = WithoutLegacyStatusFlag(character, flag);
        }

        return character;
    }

    private static uint BuildLegacyCharacterMesh(CharacterSnapshot character)
    {
        var transformation = checked((long)ReadAttributeUInt64(
            character.Attributes,
            "TransformationID",
            "TransformationId"));
        var mesh = checked((transformation * 10_000_000) + (character.Face * 10_000) + character.Body);
        return CheckedUInt32(mesh, "Mesh");
    }

    private static async ValueTask HandleConfirmProficienciesAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped MsgAction confirm-proficiencies {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var count = 0;
        foreach (var proficiency in session.PlayerProficiencies.Proficiencies)
        {
            await context.Connection.SendAsync(
                    LegacyProficiencyPacket.FromSnapshot(proficiency).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            count++;
        }

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyProficiency confirm " +
                $"{FormatSession(context)} count={count}.")
            .ConfigureAwait(false);
    }

}
