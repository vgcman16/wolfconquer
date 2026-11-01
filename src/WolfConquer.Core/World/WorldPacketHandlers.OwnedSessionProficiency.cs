namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> ApplyOwnedSessionProficiencyAsync(WorldPacketHandlerContext context,
        WorldSession session, uint rawExperience, IReadOnlyList<uint> weaponIds, string activity, CancellationToken cancellationToken)
    {
        if (context.Sessions is not { } store || session.NativeGameClient?.OrdinarySpellBinding is not { } binding ||
            !ReferenceEquals(context.Connection.NativeGameClient, binding.Client))
            throw new InvalidOperationException("ownedProficiencyHandlerOwnerUnavailable");
        var calculation = new BasicPhysicalProficiencyCalculation(session.PlayerProficiencies, weaponIds, rawExperience);
        if (!calculation.HasChanges) return session;
        var output = calculation.Frames; // Serialization/allocations precede the actual writer.
        if (!store.TryCommitOwnedProficiency(context, session, calculation, out var receipt, out var published, out var reason) ||
            receipt is null || published is null) throw new InvalidOperationException("ownedProficiencyRefused:" + reason);
        try
        {
            foreach (var frame in output)
            {
                if (!store.TryReadOwnedProficiencyPublication(receipt, out var current) || !ReferenceEquals(current, published))
                    throw new InvalidOperationException("ownedProficiencyOutputOwnerChanged");
                await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!store.TryReadOwnedProficiencyPublication(receipt, out current) || !ReferenceEquals(current, published))
                    throw new InvalidOperationException("ownedProficiencyOutputOwnerChanged");
            }
        }
        catch (Exception error) { store.FenceOwnedProficiencyOutputFailure(receipt, error); throw; }
        await context.Log.WriteLineAsync($"WorldServer sent owned legacy {activity} {FormatSession(context with { Session = published })} " +
            $"rawExperience={rawExperience} ratedExperience={calculation.RatedExperience}; real saved-origin/native/holder publication precedes2041.")
            .ConfigureAwait(false);
        return published;
    }
}
