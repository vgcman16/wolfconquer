namespace WolfConquer.Core.World;

internal enum LegacyPairMovementKind { Walk = 1, Jump = 2 }

// Recorded intent only. Construction does not grant a live pair, write or receipt.
internal sealed record LegacyPairMovementOperation
{
    public LegacyPairMovementKind Kind { get; }
    public long ActorAccountId { get; }
    public uint ActorUid { get; }
    public long PartnerAccountId { get; }
    public uint PartnerUid { get; }
    public uint MapId { get; }
    public uint DynamicId { get; }
    public ushort FromX { get; }
    public ushort FromY { get; }
    public ushort ToX { get; }
    public ushort ToY { get; }
    public ushort DirectionOrFacing { get; }

    internal LegacyPairMovementOperation(LegacyPairMovementKind kind, long actorAccountId, uint actorUid,
        long partnerAccountId, uint partnerUid, uint mapId, uint dynamicId,
        ushort fromX, ushort fromY, ushort toX, ushort toY, ushort directionOrFacing)
    {
        Kind = kind; ActorAccountId = actorAccountId; ActorUid = actorUid;
        PartnerAccountId = partnerAccountId; PartnerUid = partnerUid;
        MapId = mapId; DynamicId = dynamicId; FromX = fromX; FromY = fromY;
        ToX = toX; ToY = toY; DirectionOrFacing = directionOrFacing;
        Validate();
    }

    internal void Validate()
    {
        if (ActorAccountId <= 0 || PartnerAccountId <= 0 || ActorAccountId == PartnerAccountId ||
            ActorUid == 0 || PartnerUid == 0 || ActorUid == PartnerUid || MapId == 0)
            throw new InvalidDataException("pairMovementIdentityInvalid");
        if (Kind == LegacyPairMovementKind.Jump)
        {
            if (ToX == 0 && ToY == 0 || Math.Abs((int)ToX - FromX) >= 17 || Math.Abs((int)ToY - FromY) >= 17)
                throw new InvalidDataException("pairMovementJumpGeometryInvalid");
            return;
        }
        if (Kind != LegacyPairMovementKind.Walk || DirectionOrFacing > 7)
            throw new InvalidDataException("pairMovementKindOrDirectionInvalid");
        (int x, int y) = DirectionOrFacing switch
        {
            0 => (0, 1), 1 => (-1, 1), 2 => (-1, 0), 3 => (-1, -1),
            4 => (0, -1), 5 => (1, -1), 6 => (1, 0), 7 => (1, 1),
            _ => throw new InvalidDataException("pairMovementDirectionInvalid"),
        };
        if ((int)FromX + x != ToX || (int)FromY + y != ToY)
            throw new InvalidDataException("pairMovementWalkGeometryInvalid");
    }

    internal bool Contains(uint uid) => uid == ActorUid || uid == PartnerUid;
    internal uint UidFor(long accountId) => accountId == ActorAccountId ? ActorUid :
        accountId == PartnerAccountId ? PartnerUid : throw new InvalidDataException("pairMovementAccountInvalid");
}
