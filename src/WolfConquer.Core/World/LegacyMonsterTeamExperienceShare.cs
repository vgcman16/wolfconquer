namespace WolfConquer.Core.World;

public readonly record struct LegacyMonsterTeamExperienceShareTarget(
    int FamilyMaxHealth,
    int Level,
    int MapId,
    ushort X,
    ushort Y);

public readonly record struct LegacyMonsterTeamExperienceShareMember(
    uint Uid,
    int Level,
    int MapId,
    ushort X,
    ushort Y,
    bool Alive);

public static class LegacyMonsterTeamExperienceShare
{
    public const int ShareDistance = 18;

    public static int CalculateBaseShare(int targetFamilyMaxHealth)
    {
        if (targetFamilyMaxHealth < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFamilyMaxHealth),
                targetFamilyMaxHealth,
                "Target family max health cannot be negative.");
        }

        return targetFamilyMaxHealth / 50;
    }

    public static int CalculateComputedShare(
        LegacyMonsterTeamExperienceShareTarget target,
        IEnumerable<LegacyMonsterTeamExperienceShareMember> teamMembers)
    {
        var share = CalculateBaseShare(target.FamilyMaxHealth);
        return HasNewbieNearTarget(target, teamMembers) ? share * 2 : share;
    }

    public static bool HasNewbieNearTarget(
        LegacyMonsterTeamExperienceShareTarget target,
        IEnumerable<LegacyMonsterTeamExperienceShareMember> teamMembers)
    {
        ArgumentNullException.ThrowIfNull(teamMembers);

        foreach (var member in teamMembers)
        {
            if (member.Alive &&
                member.MapId == target.MapId &&
                GetLegacyDistance(member.X, member.Y, target.X, target.Y) <= ShareDistance &&
                member.Level + 20 < target.Level)
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsRecipientEligible(
        LegacyMonsterTeamExperienceShareMember recipient,
        LegacyMonsterTeamExperienceShareMember killer)
    {
        return recipient.Uid != killer.Uid &&
            recipient.Alive &&
            recipient.MapId == killer.MapId &&
            GetLegacyDistance(recipient.X, recipient.Y, killer.X, killer.Y) <= ShareDistance;
    }

    public static int CalculateRecipientAmount(int computedShare, bool isSpouseRecipient)
    {
        if (computedShare < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(computedShare),
                computedShare,
                "Computed team experience share cannot be negative.");
        }

        return isSpouseRecipient ? computedShare * 2 : computedShare;
    }

    public static int CalculateRecipientAmount(
        int computedShare,
        LegacyMonsterTeamExperienceShareMember recipient,
        LegacyMonsterTeamExperienceShareMember killer,
        uint killerSpouseUid)
    {
        return IsRecipientEligible(recipient, killer)
            ? CalculateRecipientAmount(computedShare, recipient.Uid == killerSpouseUid)
            : 0;
    }

    private static long GetLegacyDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}
