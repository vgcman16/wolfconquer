using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native TutorInfo owner; no startup or managed mentor policy adapter.
internal class LegacyNativeTutorInfo
{
    public class TutorType
    {
        public int Index;
        public int MinLevel;
        public int MaxLevel;
        public int StudentNum;
        public int BattleLevShare;
    }

    public static List<TutorType> TutorTypes = new List<TutorType>();
    public static int[] BattleLimit = new int[400];

    public static void Load()
    {
        string[] array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "cq_tutor_type.txt");
        string[] array2 = array;
        foreach (string text in array2)
        {
            if (!text.Contains(';'))
            {
                string[] array3 = text.Split(',');
                TutorType tutorType = new TutorType();
                tutorType.Index = int.Parse(array3[0]);
                tutorType.MinLevel = int.Parse(array3[1]);
                tutorType.MaxLevel = int.Parse(array3[2]);
                tutorType.StudentNum = int.Parse(array3[3]);
                tutorType.BattleLevShare = int.Parse(array3[4]);
                TutorTypes.Add(tutorType);
            }
        }
        array = File.ReadAllLines(LegacyNativeServerConfig.DbLocation + "cq_tutor_battle_limit_type.txt");
        BattleLimit = new int[array.Length + 1];
        string[] array4 = array;
        foreach (string text2 in array4)
        {
            if (!text2.Contains(';'))
            {
                string[] array5 = text2.Split(',');
                BattleLimit[int.Parse(array5[0])] = int.Parse(array5[1]);
            }
        }
    }

    public static TutorType? GetTutorInfo(LegacyNativeGameClientRuntime user)
    {
        foreach (TutorType tutorType in TutorTypes)
        {
            if (user.Player.Level >= tutorType.MinLevel && Math.Min(140, (int)user.Player.Level) <= tutorType.MaxLevel)
                return tutorType;
        }
        return null;
    }

    public static int AddAppCount(LegacyNativeGameClientRuntime user)
    {
        return GetTutorInfo(user)?.StudentNum ?? 0;
    }

    public static int ShareBattle(LegacyNativeGameClientRuntime Mentor, int Student_battle)
    {
        if (Student_battle >= Mentor.Player.RealBattlePower) return 0;
        TutorType? tutorInfo = GetTutorInfo(Mentor);
        if (tutorInfo != null)
        {
            int battleLevShare = tutorInfo.BattleLevShare;
            int val = (Mentor.Player.RealBattlePower - Student_battle) * battleLevShare / 100;
            int val2 = BattleLimit[Math.Min(BattleLimit.Length - 1, Student_battle)];
            return Math.Min(val, val2);
        }
        return 0;
    }
}
