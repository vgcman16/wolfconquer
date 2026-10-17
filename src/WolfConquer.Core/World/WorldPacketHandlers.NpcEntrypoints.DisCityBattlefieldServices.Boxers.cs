namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyDisCityBattlefieldServicesTcBoxerNpcId = 101_611;
    private const uint LegacyDisCityBattlefieldServicesApeBoxerNpcId = 101_623;
    private const uint LegacyDisCityBattlefieldServicesDesertBoxerNpcId = 101_625;
    private const uint LegacyDisCityBattlefieldServicesPheonixBoxerNpcId = 101_627;
    private const uint LegacyDisCityBattlefieldServicesBirdBoxerNpcId = 101_629;

    private const long LegacyDisCityBattlefieldServicesTrainingGroundFee = 1_000;
    private const ushort LegacyDisCityBattlefieldServicesBoxerAvatarId = 8;

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesBoxerRootOptions =
    [
        new("Please teleport me there.", 1),
        new("Just passing by.", LegacyNpcClosedOption),
    ];

    private static readonly IReadOnlyDictionary<uint, LegacyDisCityBattlefieldServicesSource> LegacyDisCityBattlefieldServicesBoxerSources =
        new Dictionary<uint, LegacyDisCityBattlefieldServicesSource>
        {
            [LegacyDisCityBattlefieldServicesTcBoxerNpcId] =
                new("NpcHandler.TcBoxer", "NpcHandler.cs:28214-28262"),
            [LegacyDisCityBattlefieldServicesBirdBoxerNpcId] =
                new("NpcHandler.BirdBoxer", "NpcHandler.cs:28263-28311"),
            [LegacyDisCityBattlefieldServicesPheonixBoxerNpcId] =
                new("NpcHandler.PheonixBoxer", "NpcHandler.cs:28312-28360"),
            [LegacyDisCityBattlefieldServicesApeBoxerNpcId] =
                new("NpcHandler.ApeBoxer", "NpcHandler.cs:28361-28409"),
            [LegacyDisCityBattlefieldServicesDesertBoxerNpcId] =
                new("NpcHandler.DesertBoxer", "NpcHandler.cs:28410-28458"),
        };

    private static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleBoxerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        [
                            "If you are level 20 or above, you can level at the training ground.",
                            "It does not consume durability, life, or mana. Do you want me to teleport you there?",
                            "The fee is 1,000 silver.",
                        ],
                        LegacyDisCityBattlefieldServicesBoxerRootOptions,
                        LegacyDisCityBattlefieldServicesBoxerAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                if (session.Character.Money > LegacyDisCityBattlefieldServicesTrainingGroundFee)
                {
                    var chargedSession = await LegacyDisCityBattlefieldServicesSendMoneyUpdateAsync(
                            context,
                            session,
                            npcId,
                            optionId,
                            source,
                            session.Character.Money - LegacyDisCityBattlefieldServicesTrainingGroundFee,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await SendLegacyNpcTeleportAsync(
                            context,
                            chargedSession,
                            npcId,
                            optionId,
                            new LegacyNpcTeleportDestination(1_039, 226, 218, source.SourceMethod),
                            cancellationToken)
                        .ConfigureAwait(false);
                    await SendLegacyNpcDialogAsync(
                            context,
                            source.SourceMethod,
                            ["At the training ground, attack only dummies or stakes with a lower level than yours."],
                            [new LegacyNpcDialogOption("I see.", LegacyNpcClosedOption)],
                            LegacyDisCityBattlefieldServicesBoxerAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["You do not have 1,000 silver with you."],
                        [new LegacyNpcDialogOption("Oh, sorry.", LegacyNpcClosedOption)],
                        LegacyDisCityBattlefieldServicesBoxerAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }
}
