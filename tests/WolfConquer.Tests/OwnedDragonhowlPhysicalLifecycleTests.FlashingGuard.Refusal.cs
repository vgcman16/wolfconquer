using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Theory]
    [InlineData("set", "managed-removal")]
    [InlineData("set", "registration-loss")]
    [InlineData("set", "flag-alias-loss")]
    [InlineData("set", "equal-registration")]
    [InlineData("transaction", "managed-removal")]
    [InlineData("transaction", "registration-loss")]
    [InlineData("transaction", "flag-alias-loss")]
    [InlineData("transaction", "equal-registration")]
    public Task GenericPublicationCannotOrphanTheActuallyEarnedNativeFlashingLease(string route, string corruption) => Run(async f =>
    {
        await EarnFlashingGuardStateAsync(f);
        var earned = CaptureFlashingGuardState(f);
        // The same public route first accepts the unaltered actual registration.
        // Transaction success above comes from the configured real file journal.
        PublishUnchangedFlashingGuardControl(f, route);
        AssertFlashingGuardState(f, earned);
        var before = CaptureFlashingGuardState(f);
        var proposed = CorruptFlashingGuardManagedData(before.ActorSession, corruption);
        var writerCalls = 0;
        bool WriterMustNotRun()
        {
            writerCalls++;
            throw new InvalidOperationException("flashingGuardUnexpectedWriter");
        }
        WorldSession? final = null;
        InvalidOperationException error;
        if (route == "set")
            error = Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(Actor), proposed));
        else
            error = Assert.Throws<InvalidOperationException>(() =>
                f.Store.TryCommitSession(f.Connection(Actor), before.ActorSession, proposed, WriterMustNotRun, out final));
        AssertFlashingGuardBoundary(error);
        Assert.Equal(0, writerCalls); Assert.Null(final);
        AssertFlashingGuardState(f, before);
        // Refusal did not poison the valid live lease or generic publisher.
        PublishUnchangedFlashingGuardControl(f, route);
        AssertFlashingGuardState(f, before);
    });

    private static WorldSession CorruptFlashingGuardManagedData(WorldSession before, string corruption)
    {
        var registration = Assert.IsType<LegacyCanAttackFlashingRegistration>(before.CanAttackFlashingRegistration);
        switch (corruption)
        {
            case "registration-loss":
                Assert.True(Flag(before, Flashing));
                return before with { CanAttackFlashingRegistration = null };
            case "equal-registration":
            {
                // This proposed record is DATA only. Equal UID/timer values do
                // not grant the private lease installed by actual Physical ingress.
                var replacement = registration with { };
                Assert.Equal(registration, replacement); Assert.NotSame(registration, replacement);
                return before with { CanAttackFlashingRegistration = replacement };
            }
            case "managed-removal":
            {
                var proof = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
                var words = LegacyWindWalkerStatusProjection.Read(before.Character); words[0] &= ~1UL;
                var attributes = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
                foreach (var key in attributes.Keys.Where(key => key.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray())
                    attributes[key] = string.Join('/', words) + "/";
                Assert.True(attributes.Remove(LegacyStatusFlagDeathRegistration.Key(Flashing)));
                Assert.True(proof.IsCurrentPlain());
                var proposed = before with { Character = before.Character with { Attributes = attributes },
                    CanAttackFlashingRegistration = null };
                Assert.False(Flag(proposed, Flashing)); Assert.True(Flag(before, Flashing));
                return proposed; // No native flag, lease, holder or file has been changed.
            }
            case "flag-alias-loss":
            {
                var proof = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
                var attributes = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
                // First retain the valid original registration in a separate
                // proposed DATA map. Editing only that map then bypasses the
                // record's normal invalid-registration init cleanup, not the
                // native owner. No live holder or original map is modified.
                var proposed = before with { Character = before.Character with { Attributes = attributes } };
                Assert.Same(registration, proposed.CanAttackFlashingRegistration);
                var words = LegacyWindWalkerStatusProjection.Read(before.Character); words[0] &= ~1UL;
                foreach (var key in attributes.Keys.Where(key => key.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("BitVector", StringComparison.OrdinalIgnoreCase)).ToArray())
                    attributes[key] = string.Join('/', words) + "/";
                Assert.Same(registration, proposed.CanAttackFlashingRegistration);
                Assert.False(Flag(proposed, Flashing)); Assert.True(Flag(before, Flashing));
                Assert.True(proof.IsCurrentPlain());
                return proposed;
            }
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
    }
}
