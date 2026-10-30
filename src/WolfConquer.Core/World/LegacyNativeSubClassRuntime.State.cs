using System.Collections.Concurrent;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Genuine native subclass storage and constructor. SetPhrase and SetLevelss are
// wholly absent: their real Equipment.QueryEquipment dependency is not complete.
// No client/Player birth, DataCore, host or packet-handler caller is installed.
internal partial class LegacyNativeSubClassRuntime
{
    public LegacyNativeDbLevExp.Sort ActiveSubclass;

    private readonly object studyGate = new();
    private ushort studyPoints;
    private ulong studyRevision;

    public ushort StudyPoints
    {
        get { lock (studyGate) return studyPoints; }
        set
        {
            lock (studyGate)
            {
                ulong next = checked(studyRevision + 1);
                studyPoints = value;
                studyRevision = next;
            }
        }
    }

    public ConcurrentDictionary<LegacyNativeDbLevExp.Sort, LegacyNativeMsgSubClass.SubClases> src;

    public LegacyNativeSubClassRuntime()
    {
        src = new ConcurrentDictionary<LegacyNativeDbLevExp.Sort, LegacyNativeMsgSubClass.SubClases>();
    }
}
