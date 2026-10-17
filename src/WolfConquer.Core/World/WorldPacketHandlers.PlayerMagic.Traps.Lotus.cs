using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private readonly record struct LegacyLotusLayTrapDefinition(
        ushort SpellId,
        uint FloorItemId,
        string FloorName,
        int FullPowerStatusFlag,
        uint AppendIconFlag,
        bool BlocksNoRevMaps,
        string RuntimeGapSeam);

    private static async ValueTask<bool> HandleLegacyPlayerLotusLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyLotusLayTrapDefinition(spell.Id, out var lotus))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var character = resourceSession.Character;
        var taoistPower = ReadAttributeUInt32(character.Attributes, "TaoistPower", "TaoPower");
        if (taoistPower < 9 ||
            lotus.BlocksNoRevMaps && LegacyLayTrap.IsSpaceLeapBlockedMap(character.MapId))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer returned legacy Lotus LayTrap magic after preparation " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                    $"taoistPower={taoistPower} map={character.MapId} noRevBlocked={lotus.BlocksNoRevMaps && LegacyLayTrap.IsSpaceLeapBlockedMap(character.MapId)}; " +
                    "source=LayTrap.cs cases 12370/12380 gate breaks without floor effect.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var selfInteraction = new MsgAttackPacket(new MsgAttackQuery(
            casterUid,
            casterUid,
            query.X,
            query.Y,
            MsgAttackType.None,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0));
        await context.Connection.SendAsync(selfInteraction.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var timer = LegacyLayTrap.PackLegacyTqTimer(DateTime.Now.AddSeconds(7));
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            lotus.FloorItemId,
            query.X,
            query.Y,
            LegacyLayTrap.LotusFloorLife,
            LegacyFloorItemDropType.Effect,
            Life: LegacyLayTrap.LotusFloorLife,
            LegacyLayTrap.LotusFloorColor,
            casterUid,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: LegacyLayTrap.LotusFlowerType,
            Timer: timer,
            OwnerX: 0,
            OwnerY: 0,
            lotus.FloorName);

        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                query.X,
                query.Y,
                now.AddMilliseconds(LegacyLayTrap.LotusFloorLifetimeMilliseconds),
                Distance: 0,
                HitRadius: 0);
            floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect), now);
            storedFloorEffectCount = 1;
        }
        else
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                        FloorItemRuntimeStateResource,
                        lotus.RuntimeGapSeam,
                        $"{lotus.FloorName} floor effect was sent, but floor item runtime state is not loaded",
                        facts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            LegacyParityGapDiagnostics.Fact("floorUid", floorPacket.Uid),
                        ],
                        dedupeFacts:
                        [
                            LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                        ]))
                .ConfigureAwait(false);
        }

        await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = resourceSession },
                resourceSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 10_000,
                cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await ApplyLegacyLotusTaoPowerResetAsync(
                context,
                updatedSession,
                lotus,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Lotus LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} targetX={query.X} targetY={query.Y} " +
                $"floorItemId={floorPacket.ItemId} storedFloorEffectCount={storedFloorEffectCount} " +
                $"appendIconFlag={lotus.AppendIconFlag}; source=LayTrap.cs cases 12370/12380.")
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask<WorldSession> ApplyLegacyLotusTaoPowerResetAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyLotusLayTrapDefinition lotus,
        CancellationToken cancellationToken)
    {
        var resetCharacter = WithCharacterAttribute(
            WithCharacterAttribute(session.Character, "TaoistPower", 0UL),
            "TaoPower",
            0UL);
        var resetSession = session with { Character = resetCharacter };
        context.Sessions?.Set(context.Connection, resetSession);

        var uid = CheckedUInt32(resetSession.Character.Uid, nameof(resetSession.Character.Uid));
        var appendIcon = new LegacyMsgUpdatePacket(
            uid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.AppendIcon,
                    Value: 0,
                    Flag: lotus.AppendIconFlag,
                    Time: LegacyLayTrap.LotusAppendIconTime,
                    Damage: LegacyLayTrap.LotusAppendIconDamageAfterReset,
                    SpellLevel: LegacyLayTrap.LotusAppendIconSpellLevel),
            ]);
        await context.Connection.SendAsync(appendIcon.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        if (!HasLegacyStatusFlag(resetSession.Character, lotus.FullPowerStatusFlag))
        {
            return resetSession;
        }

        resetSession = resetSession with
        {
            Character = WithoutLegacyStatusFlag(resetSession.Character, lotus.FullPowerStatusFlag),
        };
        context.Sessions?.Set(context.Connection, resetSession);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context with { Session = resetSession },
                resetSession,
                cancellationToken)
            .ConfigureAwait(false);
        return resetSession;
    }

    private static bool TryGetLegacyLotusLayTrapDefinition(
        ushort spellId,
        out LegacyLotusLayTrapDefinition definition)
    {
        definition = spellId switch
        {
            LegacyLayTrap.AuroraLotusSpellId => new LegacyLotusLayTrapDefinition(
                LegacyLayTrap.AuroraLotusSpellId,
                LegacyLayTrap.AuroraLotusFloorItemId,
                LegacyLayTrap.AuroraLotusFloorName,
                LegacyLayTrap.FullPowerWaterStatusFlag,
                LegacyLayTrap.AuroraLotusAppendIconFlag,
                BlocksNoRevMaps: true,
                RuntimeGapSeam: "auroralotus-floor-effect-runtime"),
            LegacyLayTrap.FlameLotusSpellId => new LegacyLotusLayTrapDefinition(
                LegacyLayTrap.FlameLotusSpellId,
                LegacyLayTrap.FlameLotusFloorItemId,
                LegacyLayTrap.FlameLotusFloorName,
                LegacyLayTrap.FullPowerFireStatusFlag,
                LegacyLayTrap.FlameLotusAppendIconFlag,
                BlocksNoRevMaps: false,
                RuntimeGapSeam: "flamelotus-floor-effect-runtime"),
            _ => default,
        };

        return definition.SpellId != 0;
    }
}
