using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerDaggerStormLayTrapMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (spell.Id != LegacyLayTrap.DaggerStormSpellId)
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
        var floorPacket = new LegacyFloorItemPacket(
            NextLegacyFloorItemUid(),
            LegacyLayTrap.GetDaggerStormFloorItemId(playerSpell.UseSpellSoul),
            query.X,
            query.Y,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacyLayTrap.DaggerStormFloorColor,
            casterUid,
            ReadAttributeUInt32(character.Attributes, "GuildID", "GuildId"),
            FlowerType: LegacyLayTrap.DaggerStormFlowerType,
            Timer: 0,
            OwnerX: 0,
            OwnerY: 0,
            LegacyLayTrap.DaggerStormFloorName);

        var storedFloorEffectCount = 0;
        if (context.FloorItems is { } floorItems)
        {
            var now = DateTimeOffset.UtcNow;
            var effect = new FloorItemRuntimeEffect(
                spell.Id,
                query.X,
                query.Y,
                now.AddMilliseconds(LegacyLayTrap.DaggerStormFloorLifetimeMilliseconds),
                Distance: 0,
                HitRadius: 0);
            floorItems.AddVisible(
                FloorItemRuntimeEntry.ForEffect(character.MapId, floorPacket, effect),
                now);
            storedFloorEffectCount = 1;
        }

        await SendFloorItemVisibleAsync(context, character, floorPacket, cancellationToken)
            .ConfigureAwait(false);

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
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = updatedSession },
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy DaggerStorm LayTrap magic " +
                $"{FormatSession(context with { Session = updatedSession })} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"spellLevel={playerSpell.Level} soulLevel={playerSpell.UseSpellSoul} targetX={query.X} targetY={query.Y} " +
                $"floorItemId={floorPacket.ItemId} storedFloorEffectCount={storedFloorEffectCount}; " +
                "source=LayTrap.cs case 11600.")
            .ConfigureAwait(false);
        return true;
    }
}
