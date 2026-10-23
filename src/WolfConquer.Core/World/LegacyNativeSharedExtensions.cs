namespace WolfConquer.Core.World;

// Literal signed SharedExtensions time helpers, without a replacement clock or epoch.
public static class LegacyNativeSharedExtensions
{
    public static string RemoveIllegalCharacters(this string str, bool path, bool file)
    {
        string text = str;
        if (file)
        {
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            foreach (char oldChar in invalidFileNameChars)
            {
                text = text.Replace(oldChar, '_');
            }
        }
        if (path)
        {
            // Windows Framework4 set: modern .NET omits quote and angle brackets.
            // Retain the native per-call array, replacement order and null fault.
            char[] invalidPathChars = new char[]
            {
                '"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003',
                '\u0004', '\u0005', '\u0006', '\u0007', '\u0008', '\u0009', '\u000a', '\u000b',
                '\u000c', '\u000d', '\u000e', '\u000f', '\u0010', '\u0011', '\u0012', '\u0013',
                '\u0014', '\u0015', '\u0016', '\u0017', '\u0018', '\u0019', '\u001a', '\u001b',
                '\u001c', '\u001d', '\u001e', '\u001f'
            };
            foreach (char oldChar2 in invalidPathChars)
            {
                text = text.Replace(oldChar2, '_');
            }
        }
        return text;
    }

    public static int AllMilliseconds(this DateTime date)
    {
        return Environment.TickCount + (int)((date.Ticks - DateTime.Now.Ticks) / 10000);
    }

    public static int AllSeconds(this DateTime date)
    {
        return date.AllMilliseconds() / 1000;
    }

    public static int Value(this DateTime date)
    {
        return date.AllMilliseconds();
    }

    public static int GetHashCode(this DateTime date)
    {
        return date.Value();
    }

    public static void Iterate<T>(this T[] collection, Action<T> action)
    {
        foreach (T obj in collection)
        {
            action(obj);
        }
    }

    public static void Iteratefun<T>(this T[] collection, Action<T> action, Func<T, bool>? applied = null)
    {
        foreach (T val in collection)
        {
            if (applied != null)
            {
                if (applied(val))
                {
                    action(val);
                }
            }
            else
            {
                action(val);
            }
        }
    }

    public static void Iteratefunbool<T>(this T[] collection, Action<T> action, Func<T, bool>? applied = null)
    {
        foreach (T val in collection)
        {
            if (applied != null)
            {
                if (applied(val))
                {
                    action(val);
                }
            }
            else
            {
                action(val);
            }
        }
    }
}
