using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record LegacyChristmasNpcProjection(
    uint Id,
    int MapId,
    ushort X,
    ushort Y,
    ushort Mesh,
    ushort Type,
    string? Name,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int SourceLine,
    string RawLine)
{
    public uint Uid => Id;

    public static bool TryCreate(ChristmasNpc npc, out LegacyChristmasNpcProjection projection)
    {
        ArgumentNullException.ThrowIfNull(npc);

        projection = default!;
        if (!TryToUInt32(npc.Id, out var uid) ||
            !TryToUInt16(npc.X, out var x) ||
            !TryToUInt16(npc.Y, out var y) ||
            !TryToUInt16(npc.Lookface, out var mesh) ||
            !TryToUInt16(npc.Kind, out var type))
        {
            return false;
        }

        projection = new LegacyChristmasNpcProjection(
            uid,
            npc.MapId,
            x,
            y,
            mesh,
            type,
            npc.Name,
            npc.Parameters,
            npc.SourceFile,
            npc.LineNumber,
            npc.RawLine);
        return true;
    }

    public bool MatchesRequestEntity(uint requestedUid)
    {
        return Uid == requestedUid;
    }

    public LegacyNpcSpawnPacket ToNpcSpawnPacket(ushort action = 0)
    {
        return new LegacyNpcSpawnPacket(
            Uid,
            Uid,
            Unknown: 0,
            X,
            Y,
            Mesh,
            Type,
            action);
    }

    private static bool TryToUInt32(int value, out uint result)
    {
        if (value < 0)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryToUInt16(int value, out ushort result)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
        return true;
    }
}
