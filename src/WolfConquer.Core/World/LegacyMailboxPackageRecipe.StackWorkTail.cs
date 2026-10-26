namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageRecipe
{
    private static bool TryReadStackWorkTail(BinaryReader reader, int grants, out int newCount)
    {
        newCount = -1;
        var stream = reader.BaseStream;
        if (reader.ReadBytes(32).Length != 32) return false;
        var count = reader.ReadInt32();
        var changes = reader.ReadInt32();
        if (count is < 0 or > 40 || changes < grants || changes > grants + 40 ||
            stream.Length - stream.Position < changes * 19L + 4) return false;
        var seen = new HashSet<int>();
        var colors = new Dictionary<int, uint>();
        var granted = new HashSet<int>();
        var previousGrant = -1;
        for (var index = 0; index < changes; index++)
        {
            var grant = reader.ReadInt32();
            var uid = reader.ReadUInt32();
            var allocation = reader.ReadInt32();
            var quantity = reader.ReadUInt16();
            var color = reader.ReadUInt32();
            var isNewByte = reader.ReadByte();
            if (grant < previousGrant || grant < 0 || grant >= grants || color > 8 || isNewByte > 1) return false;
            previousGrant = grant;
            granted.Add(grant);
            if (uid != 0)
            {
                if (allocation != -1 || isNewByte != 0 || quantity == 0) return false;
            }
            else
            {
                if (allocation < 0 || allocation >= count) return false;
                if (isNewByte == 1)
                {
                    if (color < 3 || allocation != seen.Count || !seen.Add(allocation)) return false;
                    colors.Add(allocation, color);
                }
                else if (!seen.Contains(allocation) || quantity == 0 || colors[allocation] != color) return false;
            }
        }
        var attachments = reader.ReadInt32();
        if (attachments is < 0 or > 40 || stream.Length - stream.Position != attachments * 4L) return false;
        var moved = new HashSet<uint>();
        for (var index = 0; index < attachments; index++)
        { var uid = reader.ReadUInt32(); if (uid == 0 || !moved.Add(uid)) return false; }
        if (seen.Count != count || granted.Count != grants) return false;
        newCount = count;
        return true;
    }
}
