using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Whole native GameClient475..516. Explicit SQL-capable owner only; no login registration.
    // Never execute against original accounts. Real SELECT precedes every state decision.
    public void Banned12(uint ID, string? name, bool unbanned = false)
    {
        using LegacyNativeMySqlCommand command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT).Select("accounts").Where("ID", ID);
        using LegacyNativeMySqlReader mySqlReader = new LegacyNativeMySqlReader(command);
        if (mySqlReader.Read())
        {
            int num = mySqlReader.ReadInt32("State");
            if (unbanned)
            {
                if (num == 0)
                {
                    CreateBoxDialog("Acount it Really UnBanned.");
                    return;
                }
                using (LegacyNativeMySqlCommand mySqlCommand = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE))
                {
                    mySqlCommand.Update("accounts").Set("State", 0L).Where("ID", ID)
                        .Execute();
                }
                Console.WriteLine("Done UnBanned {0} {1}", name, ID);
                CreateBoxDialog("Done UNBanned.");
            }
            else if (num == 1)
            {
                CreateBoxDialog("Acount it Really banned.");
            }
            else
            {
                using (LegacyNativeMySqlCommand mySqlCommand2 = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE))
                {
                    mySqlCommand2.Update("accounts").Set("State", 1L).Where("ID", ID)
                        .Execute();
                }
                Console.WriteLine("Done Banned {0} {1}", name, ID);
                CreateBoxDialog("Done Banned.");
            }
        }
        else
        {
            Console.WriteLine("This {0} {1} DoesntExist", ID, name);
        }
    }
}
