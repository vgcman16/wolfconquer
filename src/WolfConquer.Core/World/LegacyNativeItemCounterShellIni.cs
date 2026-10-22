using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Native INI family only; no host/config startup or loading route is installed.
public class LegacyNativeItemCounterShellIni : ILegacyNativeItemCounterShellIni
{
    public string? FileName;

    public const int Int32_Size = 15;

    public const int Int16_Size = 9;

    public const int Int8_Size = 6;

    public const int Bool_Size = 6;

    public const int Double_Size = 20;

    public const int Int64_Size = 22;

    public const int Float_Size = 10;

    public static Func<string, int> ToInt32 = int.Parse;

    public static Func<string, uint> ToUInt32 = uint.Parse;

    public static Func<string, short> ToInt16 = short.Parse;

    public static Func<string, ushort> ToUInt16 = ushort.Parse;

    public static Func<string, sbyte> ToInt8 = sbyte.Parse;

    public static Func<string, byte> ToUInt8 = byte.Parse;

    public static Func<string, bool> ToBool = bool.Parse;

    public static Func<string, double> ToDouble = double.Parse;

    public static Func<string, long> ToInt64 = long.Parse;

    public static Func<string, ulong> ToUInt64 = ulong.Parse;

    public static Func<string, float> ToFloat = float.Parse;

    public LegacyNativeItemCounterShellIni(string? fileName, bool overridePath = false)
    {
        if (!overridePath)
        {
            FileName = LegacyNativeServerConfig.DbLocation + fileName;
        }
        else
        {
            FileName = fileName;
        }
    }

    // Compatibility-only callback constructor; not a substitute for live ServerConfig.
    public LegacyNativeItemCounterShellIni(
        string? fileName, Func<string?> databaseLocation, bool overridePath = false)
    {
        FileName = overridePath ? fileName : databaseLocation() + fileName;
    }

    public LegacyNativeItemCounterShellIni()
    {
        FileName = null;
    }

    public unsafe string ReadString(string section, string key, string defaultValue, int size)
    {
        char* ptr = stackalloc char[size];
        GetPrivateProfileStringW(section, key, defaultValue, ptr, size, FileName);
        return new string(ptr).Trim(default(char));
    }

    public string ReadString(string section, string key, string defaultValue)
    {
        return ReadString(section, key, defaultValue, 255);
    }

    public string ReadBigString(string section, string key, string defaultValue)
    {
        return ReadString(section, key, defaultValue, 1020);
    }

    public string ReadBigString(string section, string key, string defaultValue, int size)
    {
        return ReadString(section, key, defaultValue, size);
    }

    public unsafe void ReadString(string section, string key, void* defaultValue, void* buffer, int size)
    {
        GetPrivateProfileStringA(section, key, defaultValue, (sbyte*)buffer, size, FileName);
    }

    public T ReadValue<T>(string section, string key, T defaultValue, Func<string, T> callback)
    {
        try
        {
            return callback(ReadString(section, key, defaultValue!.ToString()!));
        }
        catch
        {
            return defaultValue;
        }
    }

    public T ReadValue<T>(string section, string key, T defaultValue, Func<string, T> callback, int bufferSize)
    {
        try
        {
            return callback(ReadString(section, key, defaultValue!.ToString()!, bufferSize));
        }
        catch
        {
            return defaultValue;
        }
    }

    public int ReadInt32(string section, string key, int defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToInt32, 15);
    }

    public ulong ReadUInt64(string section, string key, ulong defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToUInt64, 22);
    }

    public long ReadInt64(string section, string key, long defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToInt64, 22);
    }

    public double ReadDouble(string section, string key, double defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToDouble, 20);
    }

    // Exact two-argument interface remains available; genuine native default path is below.
    public uint ReadUInt32(string section, string key) => ReadUInt32(section, key, 0u);

    public uint ReadUInt32(string section, string key, uint defaultValue = 0u)
    {
        return ReadValue(section, key, defaultValue, ToUInt32, 15);
    }

    public short ReadInt16(string section, string key, short defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToInt16, 9);
    }

    public ushort ReadUInt16(string section, string key, ushort defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToUInt16, 9);
    }

    public sbyte ReadSByte(string section, string key, sbyte defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToInt8, 6);
    }

    public byte ReadByte(string section, string key, byte defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToUInt8, 6);
    }

    public bool ReadBool(string section, string key, bool defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToBool, 6);
    }

    public float ReadFloat(string section, string key, float defaultValue)
    {
        return ReadValue(section, key, defaultValue, ToFloat, 10);
    }

    public void WriteString(string section, string key, string? value)
    {
        if (value == null)
        {
            value = "0";
        }
        WritePrivateProfileString(section, key, value, FileName);
    }

    public void Write<T>(string section, string key, T value)
    {
        WritePrivateProfileString(section, key, value!.ToString()!, FileName);
    }

    public unsafe void WriteStruct(string section, string key, void* lpStruct, int size)
    {
        WritePrivateProfileStructW(section, key, lpStruct, size, FileName);
    }

    public unsafe string[] GetSectionNames(int bufferSize)
    {
        char* ptr = stackalloc char[bufferSize];
        int privateProfileSectionNamesW = GetPrivateProfileSectionNamesW(ptr, bufferSize, FileName);
        if (privateProfileSectionNamesW == 0)
        {
            return new string[0];
        }
        return new string(ptr, 0, privateProfileSectionNamesW - 1).Split(default(char));
    }

    public unsafe string[] GetSection(string section, int bufferSize)
    {
        char* ptr = stackalloc char[bufferSize];
        int privateProfileSectionW = GetPrivateProfileSectionW(section, ptr, bufferSize, FileName);
        if (privateProfileSectionW == 0)
        {
            return new string[0];
        }
        return new string(ptr, 0, privateProfileSectionW - 1).Split(default(char));
    }

    public string[] GetSectionNames()
    {
        return GetSectionNames(4096);
    }

    public string[] GetSection(string section)
    {
        return GetSection(section, 4096);
    }

    public unsafe bool SectionExists(string section)
    {
        char* ptr = stackalloc char[section.Length + 1];
        int privateProfileSectionW = GetPrivateProfileSectionW(section, ptr, section.Length + 1, FileName);
        return *ptr != '\0';
    }

    public unsafe bool KeyExists(string section, string key)
    {
        char* ptr = stackalloc char[2];
        uint privateProfileStringW = GetPrivateProfileStringW(section, key, "\007F", ptr, 2, FileName);
        return (privateProfileStringW == 0 && *ptr == '\0') || (privateProfileStringW != 0 && *ptr != '\u007f');
    }

    public void WriteUInt32(string section, string key, uint value) => Write(section, key, value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private unsafe static extern uint GetPrivateProfileStringW(string section, string key, string defaultValue, char* returnedString, int size, string? fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private unsafe static extern uint GetPrivateProfileStringA(string section, string key, void* defaultValue, sbyte* returnedString, int size, string? fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int WritePrivateProfileString(string section, string key, string value, string? fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private unsafe static extern bool WritePrivateProfileStructW(string section, string key, void* lpStruct, int size, string? fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private unsafe static extern int GetPrivateProfileSectionNamesW(char* returnBuffer, int size, string? fileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private unsafe static extern int GetPrivateProfileSectionW(string section, char* returnBuffer, int size, string? fileName);
}
