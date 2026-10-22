namespace WolfConquer.Core.World;

// Native mutable ActionQuery [ProtoContract] shape. The concrete serializer projects
// source ProtoMember1..24 (required1/9/12/13/14/15), without generic registration.
internal class LegacyNativeActionQuery
{
    public uint ObjId;
    public uint TargetUID;
    public uint dwParam;
    public int dwParam2;
    public long dwParam3;
    public bool SucDone;
    public uint TargetPositionX;
    public uint TargetPositionY;
    public uint Timestamp;
    public int NpcID;
    public uint CheatLogData1;
    public ushort Type;
    public ushort Fascing;
    public uint PositionX;
    public uint PositionY;
    public uint CheatLogData2;
    public uint MapID;
    public uint CheatLogData3;
    public int ScheduleTime;
    public int LinkIndex;
    public uint CheatLogData4;
    public int RemainTime;
    public bool MountCharge;
    public string[]? Strings;
}
