using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyLayTrap12990SpellId = 12_990;
    private const uint LegacyLayTrap12990FloorItemId = 1_530;
    private const byte LegacyLayTrap12990FloorColor = 14;
    private const int LegacyLayTrap12990FloorInvokeMilliseconds = 1_000;

    private readonly record struct LegacyLayTrap12990Target(
        uint Uid,
        ushort X,
        ushort Y,
        string Kind);

    private static async ValueTask<bool> HandleLegacyPlayerLayTrap12990MagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyLayTrap12990SpellId)
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
        var casterUid = CheckedUInt32(character.Uid, nameof(character.Uid));
        var casterX = CheckedUInt16(character.X, nameof(character.X));
        var casterY = CheckedUInt16(character.Y, nameof(character.Y));
        var floorEffectCount = 0;
        var storedFloorEffectCount = 0;
        var targetKind = "none";

        if (TryResolveLegacyLayTrap12990Target(context, resourceSession, query.OpponentUid, out var target))
        {
            targetKind = target.Kind;
            floorEffectCount = 1;

            var packet = new LegacyFloorItemPacket(
                NextLegacyFloorItemUid(),
                LegacyLayTrap12990FloorItemId,
                target.X,
                target.Y,
                MaxLife: 0,
                LegacyFloorItemDropType.Effect,
                Life: 0,
                LegacyLayTrap12990FloorColor,
                casterUid,
                ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
                FlowerType: 0,
                Timer: 0,
                casterX,
                casterY,
                Name: "");

            if (context.FloorItems is { } floorItems)
            {
                var now = DateTimeOffset.UtcNow;
                var effect = new FloorItemRuntimeEffect(
                    spell.Id,
                    casterX,
                    casterY,
                    now.AddMilliseconds(LegacyLayTrap12990FloorInvokeMilliseconds),
                    Distance: 0,
                    HitRadius: 0);
                floorItems.AddVisible(FloorItemRuntimeEntry.ForEffect(character.MapId, packet, effect), now);
                storedFloorEffectCount = 1;
            }
            else
            {
                await LogParityGapAsync(
                        context,
                        LegacyParityGapDiagnostics.RuntimeStateNotLoaded(
                            FloorItemRuntimeStateResource,
                            "lay-trap-12990-floor-effect-runtime",
                            "LayTrap 12990 floor effect was sent, but floor item runtime state is not loaded",
                            facts:
                            [
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                                LegacyParityGapDiagnostics.Fact("targetUid", target.Uid),
                                LegacyParityGapDiagnostics.Fact("floorUid", packet.Uid),
                            ],
                            dedupeFacts:
                            [
                                LegacyParityGapDiagnostics.Fact("spellId", spell.Id),
                            ]))
                    .ConfigureAwait(false);
            }

            await SendFloorItemVisibleAsync(context, character, packet, cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                context,
                resourceSession,
                playerSpell,
                spell,
                totalDamage: 10_000,
                cancellationToken)
            .ConfigureAwait(false);

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            casterX,
            casterY,
            spell.Id,
            SpellLevel: 0,
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            Targets: []);
        await SendPlayerSpellAnimationToVisibleAsync(context, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"targetUid={query.OpponentUid} targetKind={targetKind} animationX={casterX} animationY={casterY} " +
                $"floorEffectCount={floorEffectCount} storedFloorEffectCount={storedFloorEffectCount}; " +
                "source=LayTrap.cs case 12990.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryResolveLegacyLayTrap12990Target(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint opponentUid,
        out LegacyLayTrap12990Target target)
    {
        if (TryGetVisibleSessionByUid(context, session, opponentUid, out var playerTarget))
        {
            target = new LegacyLayTrap12990Target(
                opponentUid,
                CheckedUInt16(playerTarget.Session.Character.X, nameof(playerTarget.Session.Character.X)),
                CheckedUInt16(playerTarget.Session.Character.Y, nameof(playerTarget.Session.Character.Y)),
                "player");
            return true;
        }

        if (TryFindVisibleMonsterTargetByUid(context, session.Character, opponentUid, out var monsterTarget))
        {
            target = new LegacyLayTrap12990Target(
                opponentUid,
                monsterTarget.Monster.X,
                monsterTarget.Monster.Y,
                "monster");
            return true;
        }

        if (TryFindVisibleSobNpcByUid(context, session.Character, opponentUid, out var sobNpc))
        {
            target = new LegacyLayTrap12990Target(
                opponentUid,
                CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
                CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
                "sobNpc");
            return true;
        }

        target = default;
        return false;
    }
}
