using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

// Native MyConsole handle/type-initializer/Write paths and separate exception-file path.
// Unrelated console/UI methods remain absent.
internal partial class LegacyNativeConsole
{
    private static IntPtr _InputHandle;

    private static IntPtr _OutputHandle;

    private static LegacyNativeCounter ExceptionsCounter;

    public const int STD_INPUT_HANDLE = -10;

    public const int STD_OUTPUT_HANDLE = -11;

    public const int STD_ERROR_HANDLE = -12;

    private static bool AllowSourcelOGIN;

    public static IntPtr InputHandle
    {
        get => _InputHandle;
        set
        {
            if (!LegacyNativeConsoleKernel32.SetStdHandle(-10, value))
            {
                throw new Exception("Unable to set the new Input handle");
            }
            _InputHandle = value;
        }
    }

    public static IntPtr OutputHandle
    {
        get => _OutputHandle;
        set
        {
            if (!LegacyNativeConsoleKernel32.SetStdHandle(-11, value))
            {
                throw new Exception("Unable to set the new Output handle");
            }
            _OutputHandle = value;
        }
    }

    static LegacyNativeConsole()
    {
        ExceptionsCounter = new LegacyNativeCounter(1u);
        AllowSourcelOGIN = true;
        InputHandle = LegacyNativeConsoleKernel32.GetStdHandle(-10);
        OutputHandle = LegacyNativeConsoleKernel32.GetStdHandle(-11);
        if (InputHandle.ToInt32() == -1 || OutputHandle.ToInt32() == -1)
        {
            throw new Exception("Unable to get the Console Handle");
        }
    }

    public static void Write(string value)
    {
        value = DateTime.Now.ToString("[hh:mm:ss ]: ") + value;
        uint lpNumberOfCharsWritten = 0u;
        LegacyNativeConsoleKernel32.WriteConsole(
            OutputHandle, value, (uint)value.Length, out lpNumberOfCharsWritten, IntPtr.Zero);
    }

    public static bool AllowSource()
    {
        return AllowSourcelOGIN;
    }

    public static void WriteLine(string value, ConsoleColor color = ConsoleColor.White)
    {
        Random random = new Random();
        color = (ConsoleColor)(ushort)random.Next(1, 14);
        if (value.ToString() == "" || value.ToString() == " ")
        {
            Console.WriteLine();
            return;
        }
        Console.ForegroundColor = AllowSourcelOGIN ? color : ConsoleColor.White;
        Write(value + "\r\n");
    }
}
