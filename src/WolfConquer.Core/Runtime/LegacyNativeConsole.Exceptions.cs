using WolfConquer.Core.World;
using Application = WolfConquer.Core.Runtime.LegacyNativeApplication;

namespace WolfConquer.Core.Runtime;

internal partial class LegacyNativeConsole
{
    public static void WriteException(Exception e)
    {
        try
        {
            WriteLine(e.ToString());
            SaveException(e);
        }
        catch (Exception ex)
        {
            WriteLine(ex.ToString());
        }
    }

    public static void SaveException(Exception e)
    {
        DateTime now = DateTime.Now;
        string text = now.Month + "-" + now.Day + "//";
        if (!Directory.Exists((Application.StartupPath + "Exceptions\\").RemoveIllegalCharacters(path: true, file: false)))
        {
            Directory.CreateDirectory((Application.StartupPath + "\\Exceptions\\").RemoveIllegalCharacters(path: true, file: false));
        }
        if (!Directory.Exists((Application.StartupPath + "\\Exceptions\\" + text).RemoveIllegalCharacters(path: true, file: false)))
        {
            Directory.CreateDirectory((Application.StartupPath + "\\Exceptions\\" + text).RemoveIllegalCharacters(path: true, file: false));
        }
        if (!Directory.Exists((Application.StartupPath + "\\Exceptions\\" + text + e.TargetSite!.Name).RemoveIllegalCharacters(path: true, file: false)))
        {
            Directory.CreateDirectory((Application.StartupPath + "\\Exceptions\\" + text + e.TargetSite!.Name).RemoveIllegalCharacters(path: true, file: false));
        }
        string text2 = (Application.StartupPath + "\\Exceptions\\" + text + e.TargetSite!.Name + "\\").RemoveIllegalCharacters(path: true, file: false);
        string text3 = (now.DayOfYear + "-" + now.Hour + "-" + now.Minute + "-" + now.Second + "E" + ExceptionsCounter.Next).RemoveIllegalCharacters(path: false, file: true);
        List<string> list = new List<string>();
        list.Add("----Exception message----");
        list.Add(e.Message);
        list.Add("----End of exception message----\r\n");
        list.Add("----Stack trace----");
        list.Add(e.StackTrace!);
        list.Add("----End of stack trace----\r\n");
        File.WriteAllLines(text2 + text3 + ".txt", list.ToArray());
        WriteLine(e.ToString());
    }
}
