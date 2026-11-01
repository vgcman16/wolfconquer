namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    internal void EndBasicPhysicalAttempt(WorldServerFrameHandler.BasicPhysicalAttempt attempt) =>
        framePersistenceOwner?.EndBasicPhysicalAttempt(this, attempt);

}
