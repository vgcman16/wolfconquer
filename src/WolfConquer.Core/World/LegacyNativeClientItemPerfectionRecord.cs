namespace WolfConquer.Core.World;

// Native ClientItems.Perfection storage and literal string accessors.
// No cap, terminator insertion, clearing, UTF8 replacement or null repair.
// Malformed/overlong buffers retain the native unsafe boundary; not a DTO.
internal struct LegacyNativeClientItemPerfectionRecord
{
    public uint ItemUID;
    public uint Level;
    public uint Progres;
    public uint OwnerUID;
    public unsafe fixed sbyte _OwnerName[32];
    public unsafe fixed sbyte _SpecialText[32];

    public unsafe string OwnerName
    {
        get
        {
            fixed (sbyte* ownerName = _OwnerName)
            {
                return new string(ownerName);
            }
        }
        set
        {
            fixed (sbyte* ownerName = _OwnerName)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    ownerName[i] = (sbyte)value[i];
                }
            }
        }
    }

    public unsafe string SpecialText
    {
        get
        {
            fixed (sbyte* specialText = _SpecialText)
            {
                return new string(specialText);
            }
        }
        set
        {
            fixed (sbyte* specialText = _SpecialText)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    specialText[i] = (sbyte)value[i];
                }
            }
        }
    }

    // Extracted DBItem.GetPerfectionInfo body; native receiver state is unused.
    internal static LegacyNativeClientItemPerfectionRecord GetPerfectionInfo(LegacyNativeGameItemRuntime dataItem)
    {
        return new LegacyNativeClientItemPerfectionRecord
        {
            ItemUID = dataItem.Uid,
            Level = dataItem.PerfectionLevel,
            Progres = dataItem.PerfectionProgress,
            OwnerUID = dataItem.OwnerUID,
            OwnerName = dataItem.OwnerName,
            SpecialText = dataItem.Signature
        };
    }

    // Exact five-store body AFTER the native postlude's real lookup succeeds.
    // Lookup priority/containers/ReadFile are absent, not simulated here.
    internal void ApplyTo(LegacyNativeGameItemRuntime item)
    {
        item.PerfectionLevel = Level;
        item.OwnerUID = OwnerUID;
        item.OwnerName = OwnerName;
        item.PerfectionProgress = Progres;
        item.Signature = SpecialText;
    }
}
