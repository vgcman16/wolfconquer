namespace WolfConquer.Core.Data;

internal partial class LegacyNativeQuestInfo
{
    public partial class DBQuest
    {
        public bool CanAcceptQuest(uint Class)
        {
            if (Class % 100 > 5)
            {
                Class = Class / 1000 * 1000 + 5;
            }
            if (Profession.Length == 0 || Profession.Length == 1)
            {
                return true;
            }
            for (int i = 0; i < Profession.Length; i++)
            {
                if (Class == Profession[i])
                {
                    return true;
                }
            }
            return false;
        }
    }
}
