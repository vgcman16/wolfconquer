using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal class LegacyNativeClientProficiency
{
    // Native implicit sequential packing: ID0, Level4, PreviouseLevel8,
    // padding9..11, Experience12, NeededExperience16; expected size20, not Pack=1.
    public struct DBProf
    {
        public uint ID;

        public uint Level;

        public byte PreviouseLevel;

        public uint Experience;

        public uint NeededExperience;

        public DBProf GetDBSpell(LegacyNativeMsgProficiency prof)
        {
            ID = prof.ID;
            Level = prof.Level;
            PreviouseLevel = prof.PreviouseLevel;
            Experience = prof.Experience;
            NeededExperience = prof.NeededExperience;
            return this;
        }

        public LegacyNativeMsgProficiency GetClientProf()
        {
            LegacyNativeMsgProficiency msgProficiency = new LegacyNativeMsgProficiency();
            msgProficiency.ID = ID;
            msgProficiency.Level = Level;
            msgProficiency.PreviouseLevel = PreviouseLevel;
            msgProficiency.Experience = Experience;
            msgProficiency.NeededExperience = NeededExperience;
            return msgProficiency;
        }
    }
}
