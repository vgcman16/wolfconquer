using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgApprenticeInformation
{
    public LegacyNativePacket GetArray(LegacyNativePacket stream)
    {
        stream.InitWriter();
        stream.Write((uint)Mode);
        stream.Write(Mentor_ID);
        stream.Write(Apprentice_ID);
        stream.Write(Mesh);
        stream.Write(Shared_Battle_Power);
        stream.Write(_999999);
        stream.Write(Enrole_date);
        stream.Write(Level);
        stream.Write(Class);
        stream.Write(PkPoints);
        stream.ZeroFill(22);
        stream.Write(Online);
        stream.Write(UnKnow2);
        stream.Write(Apprentice_Experience);
        stream.Write(Apprentice_Blessing);
        stream.Write(Apprentice_Composing);
        stream.Write(MentorName!, ApprenticeName!, ApprenticeSpouse!, PersonalSignature!);
        stream.Finalize(PacketID);
        return stream;
    }
}
