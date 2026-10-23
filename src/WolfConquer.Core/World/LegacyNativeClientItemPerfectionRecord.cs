using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Native 80-byte ClientItems.Perfection layout. Bounded fixed32 low-byte fields;
// no UTF8 substitution, overrun, unterminated scan or silent truncation.
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
                int length = 0;
                while (length < 32 && ownerName[length] != 0) length++;
                return Marshal.PtrToStringAnsi((IntPtr)ownerName, length) ?? string.Empty;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 32) throw new ArgumentOutOfRangeException(nameof(value), "Fixed item text cannot exceed 32 characters.");
            fixed (sbyte* ownerName = _OwnerName)
            {
                for (int i = 0; i < 32; i++)
                    ownerName[i] = i < value.Length ? unchecked((sbyte)value[i]) : (sbyte)0;
            }
        }
    }

    public unsafe string SpecialText
    {
        get
        {
            fixed (sbyte* specialText = _SpecialText)
            {
                int length = 0;
                while (length < 32 && specialText[length] != 0) length++;
                return Marshal.PtrToStringAnsi((IntPtr)specialText, length) ?? string.Empty;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > 32) throw new ArgumentOutOfRangeException(nameof(value), "Fixed item text cannot exceed 32 characters.");
            fixed (sbyte* specialText = _SpecialText)
            {
                for (int i = 0; i < 32; i++)
                    specialText[i] = i < value.Length ? unchecked((sbyte)value[i]) : (sbyte)0;
            }
        }
    }

    // Extracted DBItem.GetPerfectionInfo body; native receiver state is unused.
    internal static LegacyNativeClientItemPerfectionRecord GetPerfectionInfo(LegacyNativeGameItemRuntime dataItem)
    {
        ArgumentNullException.ThrowIfNull(dataItem);
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
        ArgumentNullException.ThrowIfNull(item);
        item.PerfectionLevel = Level;
        item.OwnerUID = OwnerUID;
        item.OwnerName = OwnerName;
        item.PerfectionProgress = Progres;
        item.Signature = SpecialText;
    }
}
