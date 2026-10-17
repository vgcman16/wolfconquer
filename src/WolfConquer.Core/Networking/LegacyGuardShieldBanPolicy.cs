namespace WolfConquer.Core.Networking;

public sealed record LegacyGuardShieldBanPolicy(
    uint UseSpeedHours,
    uint OpenProgramHours,
    uint AutoClickerHours)
{
    public static LegacyGuardShieldBanPolicy Empty { get; } = new(0, 0, 0);

    public LegacyGuardShieldBanDecision CreateDecision(string kind)
    {
        return kind switch
        {
            "Clicker" => new(kind, AutoClickerHours, AutoClickerHours, "Use Auto CLicker"),
            "Program" => new(kind, OpenProgramHours, AutoClickerHours, "Use Auto Program"),
            "Speed" => new(kind, UseSpeedHours, AutoClickerHours, "Use Speed"),
            _ => new(kind, 0, 0, string.Empty),
        };
    }

    public string ToStatusText()
    {
        return $"UseSpeed={UseSpeedHours}h OpenProgram={OpenProgramHours}h AutoClicker={AutoClickerHours}h";
    }

    public string ToEffectiveStatusText()
    {
        return $"Clicker={AutoClickerHours}h Program={AutoClickerHours}h Speed={AutoClickerHours}h";
    }
}

public sealed record LegacyGuardShieldBanDecision(
    string Kind,
    uint ConfiguredHours,
    uint EffectiveHours,
    string Reason)
{
    public bool WouldBan => EffectiveHours > 0 && Reason.Length > 0;
}
