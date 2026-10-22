using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native primitive proficiency codecs, not a managed DTO or host route.
internal static class LegacyNativeProficiencyPacket
{
    public static void GetProficiency(this LegacyNativePacket stream, out LegacyNativeMsgProficiency prof)
    {
        prof = new LegacyNativeMsgProficiency();
        prof.ID = stream.ReadUInt32();
        prof.Level = stream.ReadUInt32();
        prof.Experience = stream.ReadUInt32();
        prof.NeededExperience = stream.ReadUInt32();
    }

    public static LegacyNativePacket ProficiencyCreate(this LegacyNativePacket stream,
        uint ID, uint Level, uint Experience, uint NeededExperience)
    {
        NeededExperience = LegacyNativeBaseFunc.ProficiencyLevelExperience(unchecked((byte)Level));
        stream.InitWriter();
        stream.Write(ID);
        stream.Write(Level);
        stream.Write(Experience);
        stream.Write(NeededExperience);
        stream.Finalize(2041);
        return stream;
    }

    public static LegacyNativePacket UpdateProfExperienceCreate(this LegacyNativePacket stream,
        uint Experience, uint NeededExperience, uint ID)
    {
        stream.InitWriter();
        stream.Write(Experience);
        stream.Write(NeededExperience);
        stream.Write(ID);
        stream.ZeroFill(8);
        stream.Finalize(2199);
        return stream;
    }
}
