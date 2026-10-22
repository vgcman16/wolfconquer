namespace WolfConquer.Core.World;

// Complete native MrRayzo.Role.Instance.Nobility owner and nested ranking.
// Reuses the actual NobilityRank enum and live packet/client/member owners.
// SaveClient, LoadCharacter and table/startup callers stay absent;
// no modern donation runtime or login/persistence adoption is installed here.
internal partial class LegacyNativeNobility
{
    public uint UID;

    public string Name;

    public int Position = -1;

    public ulong Donation;

    public uint Mesh;

    public byte Gender;

    public LegacyNativeNobility(uint _uid, string _name, ulong _donation, uint _mesh, byte _gender)
    {
        UID = _uid;
        Name = _name;
        Donation = _donation;
        Mesh = _mesh;
        Gender = _gender;
    }

    public LegacyNativeNobility(LegacyNativeGameClientRuntime user)
    {
        UID = user.Player.UID;
        Name = user.Player.Name;
        Mesh = user.Player.Mesh;
        Gender = user.Player.GetGender;
    }
}
