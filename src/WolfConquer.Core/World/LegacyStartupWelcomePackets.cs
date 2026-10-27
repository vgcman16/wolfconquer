using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyStartupWelcomePackets
{
    public const string PrivilegedBroadcastText = "GM server is open in Twin City.";

    public static IReadOnlyList<byte[]> CreateEnglishStartup(string serverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        var frames = new List<byte[]>();
        foreach (var message in CreateTalkMessages(serverName))
        {
            frames.Add(LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes());
        }

        foreach (var (mode, text) in CreateStaticGuiMessages(serverName))
        {
            frames.Add(LegacyNpcReplyPacket.ForStaticGui(mode, text).ToFrameBytes());
        }

        return frames;
    }

    public static bool TryCreatePrivilegedBroadcast(CharacterSnapshot snapshot, out byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!IsPrivilegedAccount(snapshot))
        {
            frame = [];
            return false;
        }

        frame = LegacyMsgMessagePacket.SystemBroadcast(PrivilegedBroadcastText).ToFrameBytes();
        return true;
    }

    private static IEnumerable<string> CreateTalkMessages(string serverName)
    {
        yield return $"Welcome to {serverName}.";
        yield return "Guild War runs Friday from 21:00 to 22:00.";
        yield return "Drops range from 100 to 5000 CPs; VIP users receive random bonuses.";
        yield return "We have many maps for you to hunt.";
        yield return $"Official website: {serverName} ";
        yield return $"Enjoy {serverName}.";
    }

    private static IEnumerable<(LegacyStaticGuiMode Mode, string Text)> CreateStaticGuiMessages(string serverName)
    {
        yield return (LegacyStaticGuiMode.Body, $"Welcome to {serverName}.");
        yield return (LegacyStaticGuiMode.Body, "- Please avoid cheats and report reproducible bugs.");
        yield return (LegacyStaticGuiMode.Body, "- Never share your account information with anyone.");
        yield return (LegacyStaticGuiMode.Body, "- Development build: gameplay and English are incomplete.");
        yield return (LegacyStaticGuiMode.Footer, $"Thank you for testing {serverName}.");
    }

    private static bool IsPrivilegedAccount(CharacterSnapshot snapshot)
    {
        if (snapshot.Name.Contains("[PM]", StringComparison.OrdinalIgnoreCase) ||
            snapshot.Name.Contains("[GM]", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return snapshot.Attributes.TryGetValue("ProjectManager", out var projectManager) &&
            (projectManager == "1" || bool.TryParse(projectManager, out var parsed) && parsed);
    }
}
