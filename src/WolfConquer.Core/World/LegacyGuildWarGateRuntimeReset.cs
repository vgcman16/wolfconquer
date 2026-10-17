using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static class LegacyGuildWarGateRuntimeReset
{
    public static int ResetGateOwners(
        GuildWarData? guildWarInfo,
        IEnumerable<SobNpc>? sobNpcs,
        SobNpcRuntimeStateStore? sobNpcStates)
    {
        return ResetGateOwners(
            guildWarInfo,
            sobNpcs,
            sobNpcStates,
            usersRoot: null,
            syndicateFormationTypes: null);
    }

    public static int ResetGateOwners(
        GuildWarData? guildWarInfo,
        IEnumerable<SobNpc>? sobNpcs,
        SobNpcRuntimeStateStore? sobNpcStates,
        string? usersRoot,
        IEnumerable<SyndicateFormationType>? syndicateFormationTypes)
    {
        if (!TryGetOwnerGuildId(guildWarInfo, out var ownerGuildId) ||
            sobNpcs is null ||
            sobNpcStates is null)
        {
            return 0;
        }

        var hasConstructGateState = TryCreateConstructGateState(
            usersRoot,
            ownerGuildId,
            syndicateFormationTypes,
            out var constructGateState);
        var resetCount = 0;
        foreach (var sobNpc in sobNpcs)
        {
            if (IsGuildWarGate(sobNpc))
            {
                sobNpcStates.ResetGuildWarGate(
                    sobNpc,
                    ownerGuildId,
                    hasConstructGateState ? constructGateState.Hitpoints : null,
                    hasConstructGateState
                        ? LegacySobNpcEventRules.GetGuildWarGateCloseMesh(sobNpc, constructGateState.Level)
                        : null,
                    hasConstructGateState
                        ? LegacySobNpcEventRules.GetGuildWarGateDemolitionMesh(sobNpc, constructGateState.Level)
                        : null);
                resetCount++;
            }
        }

        return resetCount;
    }

    public static bool TryGetOwnerGuildId(GuildWarData? guildWarInfo, out uint ownerGuildId)
    {
        ownerGuildId = 0;
        if (guildWarInfo?.Info?.Id is not { } id ||
            id <= 0)
        {
            return false;
        }

        ownerGuildId = checked((uint)id);
        return true;
    }

    public static bool TryCreateConstructGateState(
        string? usersRoot,
        uint ownerGuildId,
        IEnumerable<SyndicateFormationType>? syndicateFormationTypes,
        out LegacyGuildWarGateConstructState gateState)
    {
        if (!LegacyGuildConstructTable.TryLoadGuildConstructsByUsersRoot(
                usersRoot,
                ownerGuildId,
                out var constructs) ||
            !LegacyGuildConstructTable.TryFindConstruct(constructs, id: 1, out var gateConstruct) ||
            !TryFindFormationType(syndicateFormationTypes, gateConstruct, out var formation) ||
            formation.Values.Count <= 1 ||
            formation.Values[1] <= 0)
        {
            gateState = default;
            return false;
        }

        gateState = new LegacyGuildWarGateConstructState(
            checked((uint)Math.Min(formation.Values[1], uint.MaxValue)),
            gateConstruct.Level,
            gateConstruct.FormationType);
        return true;
    }

    private static bool IsGuildWarGate(SobNpc sobNpc)
    {
        return sobNpc.MapId == LegacySobNpcEventRules.GuildWarMapId &&
            sobNpc.Id is LegacySobNpcEventRules.GuildWarLeftGateUid or
                LegacySobNpcEventRules.GuildWarRightGateUid;
    }

    private static bool TryFindFormationType(
        IEnumerable<SyndicateFormationType>? syndicateFormationTypes,
        LegacyGuildConstruct construct,
        out SyndicateFormationType formationType)
    {
        if (syndicateFormationTypes is not null)
        {
            foreach (var candidate in syndicateFormationTypes)
            {
                if (candidate.Type == construct.FormationType &&
                    candidate.Level == construct.Level)
                {
                    formationType = candidate;
                    return true;
                }
            }
        }

        formationType = default!;
        return false;
    }
}

public readonly record struct LegacyGuildWarGateConstructState(
    uint Hitpoints,
    uint Level,
    int FormationType);
