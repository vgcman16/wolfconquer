namespace WolfConquer.Core.World;

// Whole native MsgApprenticeInformation storage/Create/Fill/WriteString owner.
internal partial class LegacyNativeMsgApprenticeInformation
{
    public enum Action : uint { Mentor = 1u, Apprentice }

    public ushort Length;
    public ushort PacketID;
    public Action Mode;
    public uint Mentor_ID;
    public uint Apprentice_ID;
    public uint Mesh;
    public uint Shared_Battle_Power;
    public uint _999999;
    public uint Enrole_date;
    public uint Level;
    public uint Class;
    public ushort PkPoints;
    public uint Online;
    public uint UnKnow2;
    public ulong Apprentice_Experience;
    public ushort Apprentice_Blessing;
    public ushort Apprentice_Composing;
    public string? MentorName;
    public string? ApprenticeSpouse;
    public string? ApprenticeName;
    public string? PersonalSignature;

    public void Fill(LegacyNativeGameClientRuntime client)
    {
        Level = (byte)client.Player.Level;
        Class = client.Player.Class;
        PkPoints = client.Player.PKPoints;
        Mesh = client.Player.Mesh;
        Online = 1u;
    }

    public void WriteString(string? _MentorName, string? _ApprenticeSpouse, string? _ApprenticeName, string? _PersonalSignature)
    {
        MentorName = _MentorName;
        ApprenticeSpouse = _ApprenticeSpouse;
        ApprenticeName = _ApprenticeName;
        PersonalSignature = _PersonalSignature;
    }

    public static LegacyNativeMsgApprenticeInformation Create()
    {
        LegacyNativeMsgApprenticeInformation msgApprenticeInformation = new LegacyNativeMsgApprenticeInformation();
        msgApprenticeInformation.Length = 150;
        msgApprenticeInformation.PacketID = 2200;
        msgApprenticeInformation._999999 = 999999u;
        return msgApprenticeInformation;
    }
}
