#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete DBActions.Write storage and methods. No host binding, path repair,
// directory creation, implicit flush, retry, or transaction wrapper is added.
internal class LegacyNativeDbWrite : IDisposable
{
    private string location = "";

    private List<string> Items;

    private StreamWriter SW;

    public LegacyNativeDbWrite(string loc)
    {
        Items = new List<string>();
        location = LegacyNativeServerConfig.DbLocation + loc;
    }

    public void ChangeLocation(string loc)
    {
        location = LegacyNativeServerConfig.DbLocation + loc;
    }

    public void Dispose()
    {
        Items = null;
        location = string.Empty;
        SW = null;
    }

    public LegacyNativeDbWrite Add(string data)
    {
        Items.Add(data);
        return this;
    }

    public LegacyNativeDbWrite Execute(LegacyNativeDbWriteMode mod)
    {
        bool flag = true;
        if (mod == LegacyNativeDbWriteMode.Open)
        {
            if (flag == File.Exists(location))
            {
                File.WriteAllText(location, string.Empty);
                using (SW = File.AppendText(location))
                {
                    SW.WriteLine("Count=" + Items.Count);
                    for (int i = 0; i < Items.Count; i++)
                    {
                        SW.WriteLine(Items[i]);
                    }
                    SW.Close();
                }
            }
            else
            {
                using (SW = File.AppendText(location))
                {
                    SW.WriteLine("Count=" + Items.Count);
                    for (int j = 0; j < Items.Count; j++)
                    {
                        SW.WriteLine(Items[j]);
                    }
                    SW.Close();
                }
            }
        }
        else
        {
            using (SW = File.AppendText(location))
            {
                SW.WriteLine("Count=" + Items.Count);
                for (int k = 0; k < Items.Count; k++)
                {
                    SW.WriteLine(Items[k]);
                }
                SW.Close();
            }
        }
        return this;
    }
}
