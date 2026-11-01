using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyCommittedPublicationFailureTests
{
    [Fact]
    public Task StoreRetainsTheActualCommittedExceptionAndFencesTheAdmittedOwner() =>
        OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 200), PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
            {
                var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
                var before = f.Current();
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(f.Recovery.IsBlocked);
                var files = f.Data.Files();
                int output = f.ActorFrames.Count;
                var hp = binding.Player.HitPoints;
                var mana = binding.Player.Mana;
                var spells = binding.Client.MySpells;
                var proficiencies = binding.Client.MyProfs;
                Assert.True(hp > 1);
                var proposed = before with { Character = before.Character with { MinHitPoints = hp - 1 } };
                var cause = CaptureDeclaredCause();
                var primary = new LegacyCommittedSpellPublicationException(cause);
                int calls = 0;

                var thrown = Assert.Throws<LegacyCommittedSpellPublicationException>(() =>
                    f.Store.TryCommitSession(connection, before, proposed, () =>
                    {
                        calls++;
                        Assert.Same(before, f.Current());
                        Assert.Equal(hp, binding.Player.HitPoints);
                        return ThrowDeclaredCommittedFailure(primary);
                    }));

                Assert.Equal(1, calls);
                Assert.Same(primary, thrown);
                Assert.Same(cause, thrown.InnerException);
                Assert.Contains(nameof(ThrowDeclaredCommittedFailure), thrown.StackTrace, StringComparison.Ordinal);
                Assert.Contains(nameof(CaptureDeclaredCause), cause.StackTrace, StringComparison.Ordinal);
                Assert.True(f.Recovery.IsBlocked);
                Assert.True(binding.IsRetired);
                Assert.False(f.Store.TryGet(connection, out _));
                Assert.Same(binding, binding.Client.OrdinarySpellBinding);
                Assert.Same(spells, binding.Client.MySpells);
                Assert.Same(proficiencies, binding.Client.MyProfs);
                Assert.Equal(hp, binding.Player.HitPoints);
                Assert.Equal(mana, binding.Player.Mana);
                Assert.False(binding.Client.OnAutoAttack);
                Assert.Equal(output, f.ActorFrames.Count);
                f.Data.AssertFiles(files);
                return Task.CompletedTask;
            });

    [Theory]
    [InlineData("typed")]
    [InlineData("generic")]
    [InlineData("no-cause")]
    public Task ActualNativeLeaseForwardsDeclaredFailureAndRetiresItsOwner(string kind) =>
        OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 200), PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
            {
                var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
                var before = f.Current();
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.False(f.Recovery.IsBlocked);
                var files = f.Data.Files(); int output = f.ActorFrames.Count;
                var hp = binding.Player.HitPoints; var mana = binding.Player.Mana;
                var spells = binding.Client.MySpells; var proficiencies = binding.Client.MyProfs;
                var cause = CaptureDeclaredCause();
                var primary = kind == "typed" ? CaptureDeclaredPrimary(cause) : null;
                Exception? declared = kind switch { "typed" => primary, "generic" => cause, _ => null };
                LegacyCommittedSpellPublicationException thrown;
                using (var lease = binding.EnterMutation())
                {
                    Assert.Same(before, lease.ExpectedSession);
                    lease.PreparePublication(before.PlayerSpells);
                    Assert.True(lease.IsCurrentPlain());
                    // Denial-only component call: no disk save is performed.
                    thrown = Assert.Throws<LegacyCommittedSpellPublicationException>(
                        () => lease.FailCommittedPublication(declared));
                }
                if (kind == "typed")
                {
                    Assert.Same(primary, thrown); Assert.Same(cause, thrown.InnerException);
                    Assert.Contains(nameof(CaptureDeclaredPrimary), thrown.StackTrace, StringComparison.Ordinal);
                }
                else if (kind == "generic") Assert.Same(cause, thrown.InnerException);
                else Assert.Null(thrown.InnerException);
                if (kind != "no-cause")
                    Assert.Contains(nameof(CaptureDeclaredCause), cause.StackTrace, StringComparison.Ordinal);
                Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
                Assert.False(f.Store.TryGet(connection, out _));
                Assert.Same(binding, binding.Client.OrdinarySpellBinding);
                Assert.Same(spells, binding.Client.MySpells); Assert.Same(proficiencies, binding.Client.MyProfs);
                Assert.Equal(hp, binding.Player.HitPoints); Assert.Equal(mana, binding.Player.Mana);
                Assert.False(binding.Client.OnAutoAttack); Assert.Equal(output, f.ActorFrames.Count);
                f.Data.AssertFiles(files);
                return Task.CompletedTask;
            });

    // Declared throwing component callback: it performs no disk transaction.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ThrowDeclaredCommittedFailure(LegacyCommittedSpellPublicationException error) => throw error;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IOException CaptureDeclaredCause()
    {
        try { throw new IOException("declared persistence failure"); }
        catch (IOException error) { return error; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LegacyCommittedSpellPublicationException CaptureDeclaredPrimary(Exception cause)
    {
        try { throw new LegacyCommittedSpellPublicationException(cause); }
        catch (LegacyCommittedSpellPublicationException error) { return error; }
    }
}
