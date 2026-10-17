using WolfConquer.Core.Client;
using WolfConquer.Core.Data;

internal static partial class Program
{
    static void ProbeQuestInfo(string path, ImportProbeResult result)
    {
        try
        {
            var questInfo = ReadQuestInfo(path);
            Console.WriteLine($"questTaskTypes: {questInfo.TaskTypes.Count:N0}");
            Console.WriteLine($"questMissions: {questInfo.Missions.Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"questInfo: {ex.Message}");
        }
    }

    static void ProbeQuizQuestions(string path, ImportProbeResult result)
    {
        try
        {
            Console.WriteLine($"quizQuestions: {QuizQuestionParser.ParseFile(path).Count:N0}");
        }
        catch (Exception ex)
        {
            result.RecordFailure($"quizQuestions: {ex.Message}");
        }
    }

    static QuestInfoCatalog ReadQuestInfo(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.Utf8);
        return QuestInfoParser.Parse(encoding.GetString(File.ReadAllBytes(path)));
    }
}
