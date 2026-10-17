namespace WolfConquer.Core.World;

public readonly record struct LegacySkyTowerChallenge(
    uint ChallengeId,
    int Floor,
    uint MonsterFamilyId,
    int MonsterCount,
    int ClientMapId,
    ushort ClientX,
    ushort ClientY);

public static class LegacySkyTowerCatalog
{
    private const uint FirstChallengeId = 476;
    private const uint FirstExtendedChallengeId = 764;
    private const int FirstExtendedFloor = 136;
    private const int LastFloor = 160;
    private const int ClientMapId = 1_068;
    private const ushort ClientX = 50;
    private const ushort ClientY = 50;

    // D13 ini/instancemgr.lua, tower type 2, in ascending TowerLevel order.
    private static readonly byte[] MonsterCounts =
    [
        1, 2, 3, 4, 5, 2, 4, 1, 3, 5, 3, 1, 4, 2, 5, 3, 1, 4, 2, 5,
        3, 1, 4, 2, 5, 3, 1, 4, 2, 5, 4, 2, 1, 5, 3, 2, 1, 4, 2, 5,
        3, 1, 4, 2, 5, 3, 1, 4, 2, 5, 3, 1, 4, 2, 5, 3, 1, 4, 2, 5,
        3, 1, 4, 2, 5, 3, 1, 4, 2, 5, 3, 1, 4, 2, 5, 3, 1, 4, 2, 5,
        2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 2, 4, 1, 3, 5,
        2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 2, 4, 1, 3, 5,
        2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 2, 4, 1, 3, 5, 5, 2, 4, 1, 3,
        1, 1, 2, 2, 3, 4, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 20, 25,
    ];

    public const uint RootChallengeId = FirstChallengeId;

    public static bool TryResolve(uint challengeId, out LegacySkyTowerChallenge challenge)
    {
        var floor = ResolveFloor(challengeId);
        if (floor == 0)
        {
            challenge = default;
            return false;
        }

        var monsterFamilyId = challengeId <= 610
            ? checked(challengeId + 5_596)
            : challengeId <= 776
                ? checked(challengeId + 5_515)
                : 6_291U;

        challenge = new LegacySkyTowerChallenge(
            challengeId,
            floor,
            monsterFamilyId,
            MonsterCounts[floor - 1],
            ClientMapId,
            ClientX,
            ClientY);
        return true;
    }

    public static bool TryResolveByFloor(int floor, out LegacySkyTowerChallenge challenge)
    {
        if (floor is < 1 or > LastFloor)
        {
            challenge = default;
            return false;
        }

        var challengeId = floor < FirstExtendedFloor
            ? checked(FirstChallengeId + (uint)(floor - 1))
            : checked(FirstExtendedChallengeId + (uint)(floor - FirstExtendedFloor));
        return TryResolve(challengeId, out challenge);
    }

    private static int ResolveFloor(uint challengeId)
    {
        if (challengeId is >= FirstChallengeId and <= 610)
        {
            return checked((int)(challengeId - FirstChallengeId) + 1);
        }

        if (challengeId is >= FirstExtendedChallengeId and <= 788)
        {
            return checked((int)(challengeId - FirstExtendedChallengeId) + FirstExtendedFloor);
        }

        return 0;
    }
}
