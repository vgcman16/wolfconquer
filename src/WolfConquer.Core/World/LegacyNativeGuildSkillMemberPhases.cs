namespace WolfConquer.Core.World;

internal static class LegacyNativeGuildSkillMemberPhases
{
    // Native GameClient constructor1383: after OnInterServer and before Lottery.
    // Explicit and unwired; this does not reconstruct the omitted full constructor.
    internal static void AssignGuildSkillAtNativeConstructorSlot(LegacyNativeGameClientRuntime client)
    {
        client.GuildSkill = new LegacyNativeGuildSkillRuntime();
    }
}
