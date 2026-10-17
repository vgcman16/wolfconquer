namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPhysicalNoMercyMinimumDamage = 22_000;
    private const uint LegacyPhysicalAssassinAutoHuntDamage = 38_000;

    private readonly record struct LegacyPlayerPlayerPhysicalPostIronShieldTailResult(
        WorldSession Attacker,
        uint Damage);

    private static async ValueTask<LegacyPlayerPlayerPhysicalPostIronShieldTailResult>
        ApplyLegacyPlayerPlayerPhysicalPostIronShieldTailAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        uint damage,
        CancellationToken cancellationToken)
    {
        if (HasLegacyStatusFlag(attacker.Character, LegacySlayerFlag))
        {
            damage = MulDivToUInt32(
                damage,
                ReadAttributeUInt32(
                    attacker.Character.Attributes,
                    "SlayerPercent",
                    "SlayerDamagePercent"),
                100);
            attacker = await TryConsumeLegacySlayerAsync(
                    context with { Session = attacker },
                    attacker,
                    shouldConsume: true,
                    cancellationToken)
                .ConfigureAwait(false);
            context = context with { Session = attacker };
        }

        if (context.WorldData is not { } worldData)
        {
            return new LegacyPlayerPlayerPhysicalPostIronShieldTailResult(attacker, damage);
        }

        if (TryGetLegacyEligibleEquippedRuneLevel(attacker, worldData, "NoMercy", out _) &&
            damage < LegacyPhysicalNoMercyMinimumDamage)
        {
            damage = LegacyPhysicalNoMercyMinimumDamage;
        }

        if (TryGetLegacyEligibleEquippedRuneLevel(attacker, worldData, "Assassin", out _) &&
            ReadAttributeBool(attacker.Character.Attributes, RuntimeAutoHuntAttribute))
        {
            damage = AddLegacySaturating(damage, LegacyPhysicalAssassinAutoHuntDamage);
        }

        return new LegacyPlayerPlayerPhysicalPostIronShieldTailResult(attacker, damage);
    }
}
