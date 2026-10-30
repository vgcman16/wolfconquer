using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyGmRuntimeTests
{
    [Theory]
    [InlineData("domain")]
    [InlineData("owner")]
    [InlineData("profile")]
    public Task InvalidPolicyBindingDoesNotConsumeTheRealRuntimeFrameClaim(string fault) =>
        WithUnclaimedGmRuntimeAsync((fixture, runtime) =>
        {
            var valid = GmOwnerPolicy(fixture.ExpectedDomain);
            var supplied = fault == "domain" ? GmOwnerPolicy(Guid.NewGuid()) : valid;
            var files = fixture.Files();
            var error = Assert.Throws<ArgumentException>(() => new WorldServerFrameHandler(
                fixture.Loader, null, LegacyItemWorldDataFixture.Create([], []), LegacyRuntimeUpdateLoopOptions.Disabled,
                TextWriter.Null, protocolProfile: fault == "profile" ? ConquerProtocolProfile.RootD11_7899 :
                    ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: fault == "owner" ? null : runtime,
                gmPolicy: supplied));
            Assert.Equal("gmPolicy", error.ParamName);
            fixture.AssertFiles(files);
            var accepted = new WorldServerFrameHandler(fixture.Loader, null, LegacyItemWorldDataFixture.Create([], []),
                LegacyRuntimeUpdateLoopOptions.Disabled, TextWriter.Null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime, gmPolicy: valid);
            Assert.Same(runtime, GmOwnerField<LegacyOwnedAdmissionRuntime>(accepted, "_ownedAdmissionRuntime"));
            Assert.Same(valid, GmOwnerField<LegacyGmPolicy>(accepted, "_gmPolicy"));
            fixture.AssertFiles(files);
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData("remove")]
    [InlineData("sameIdsCopy")]
    [InlineData("transportClose")]
    public Task QueuedKitCannotUseARetiredOrReplacedOriginalActor(string change) => WithGmAsync(async owner =>
    {
        var before = owner.Fixture.Files();
        var high = owner.HighWater;
        var original = owner.Current;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.Native.OrdinarySpellBinding);
        var operations = GmOwnerField<WorldOperationBarrier>(owner.Handler, "_worldOperations");
        var held = await operations.EnterSharedAsync();
        Task? close = null;
        // The async ingress executes capture synchronously until its first
        // exclusive-barrier await. This real shared lease holds that exact seam.
        var command = CommandAsync(owner, "@wolf kit consumables 8cd1749ceaf443c7aa00d5a004cae329");
        Exception? primary = null;
        try
        {
            Assert.False(command.IsCompleted);
            if (change == "remove")
            {
                Assert.True(owner.Store.TryRemove(owner.Connection, out var removed));
                Assert.Same(original, removed);
                Assert.True(binding.IsRetired);
            }
            else if (change == "sameIdsCopy")
            {
                owner.Store.Set(owner.Connection, original with { }, out var replacement);
                Assert.NotSame(original, replacement);
                Assert.Same(replacement, owner.Current);
                Assert.Equal(original.AccountId, replacement.AccountId);
                Assert.Equal(original.Character.Uid, replacement.Character.Uid);
                Assert.Same(binding, owner.Native.OrdinarySpellBinding);
            }
            else
            {
                close = owner.CloseAsync();
                Assert.False(close.IsCompleted);
                var trades = GmOwnerField<LegacyTradeStateStore>(owner.Handler, "_tradeStates");
                lock (trades.SyncRoot) Assert.True(trades.HasConnectionEnded(owner.Connection));
            }
        }
        catch (Exception error) { primary = error; }
        finally
        {
            try { held.Dispose(); }
            catch (Exception cleanup) { primary = primary is null ? cleanup : new AggregateException(primary, cleanup); }
        }
        await AwaitGmOwnerTasksAsync(primary, close is null ? [command] : [command, close]);
        if (change == "transportClose") Assert.Empty(owner.SentSnapshot());
        else AssertRefusal(owner);
        Assert.Equal(high, owner.HighWater);
        Assert.Empty(owner.Mail());
        owner.Fixture.AssertFiles(before);
        Assert.False(LegacyTradeJournal.HasPending(owner.Fixture.Users));
        Assert.DoesNotContain("Frame exception", owner.Log.ToString(), StringComparison.OrdinalIgnoreCase);
    });

    [Theory]
    [InlineData("slot")]
    [InlineData("item")]
    public Task NativeGraphDriftCannotAuthorizeKitAllocation(string fault) => WithGmAsync(async owner =>
    {
        var before = owner.Fixture.Files();
        var high = owner.HighWater;
        var current = owner.Current;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.Native.OrdinarySpellBinding);
        var inventory = Assert.IsType<LegacyNativeInventoryRuntime>(owner.Native.Inventory);
        var item = Assert.Single(inventory.ClientItems).Value;
        var durability = item.Durability;
        Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
        try
        {
            if (fault == "slot") owner.Native.Inventory = null;
            else item.Durability = durability == ushort.MaxValue ? (ushort)0 : (ushort)(durability + 1);
            Assert.False(owner.Store.IsBoundSpellOwnerCurrent(binding));
            await CommandAsync(owner, "@wolf kit consumables 9ec6100604b24e7e87f5ed71023a9037");
            AssertRefusal(owner);
            Assert.Equal(high, owner.HighWater);
            Assert.Same(current, owner.Current);
            Assert.Empty(owner.Mail());
            owner.Fixture.AssertFiles(before);
        }
        finally { owner.Native.Inventory = inventory; item.Durability = durability; }
        Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
    });

    [Fact]
    public Task ActualUnadmittedPinCannotAuthorizeEitherInspectionOrMail() => WithGmAsync(async owner =>
    {
        var before = owner.Fixture.Files();
        var high = owner.HighWater;
        var current = owner.Current;
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(owner.Connection, current));
        LegacySecondaryPasswordRuntime.Invalidate(owner.Connection, current);
        Assert.False(LegacySecondaryPasswordRuntime.IsAdmitted(owner.Connection, current));
        foreach (var command in new[] { "@wolf inspect", "@wolf kit consumables c1346194279e44a0a7c2a985744005c4" })
        {
            owner.Sent.Clear();
            await CommandAsync(owner, command);
            AssertRefusal(owner);
            Assert.Equal(high, owner.HighWater);
            Assert.Same(current, owner.Current);
            Assert.Empty(owner.Mail());
            owner.Fixture.AssertFiles(before);
        }
    });

    private static LegacyGmPolicy GmOwnerPolicy(Guid domain) => LegacyGmPolicy.Parse(JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 1, expectedDomain = domain.ToString("D"),
        grants = new[] { new { accountId = OperatorGearMailFixture.AccountId, characterUid = OperatorGearMailFixture.Uid,
            capabilities = new[] { "inspect.self" } } }
    }));

    private static T GmOwnerField<T>(WorldServerFrameHandler handler, string name) where T : class =>
        Assert.IsType<T>(typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));

    private static async Task AwaitGmOwnerTasksAsync(Exception? primary, params Task[] tasks)
    {
        var errors = new List<Exception>();
        if (primary is not null) errors.Add(primary);
        foreach (var task in tasks)
            try { await task.WaitAsync(TimeSpan.FromSeconds(20)); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("GM owner assertion and pending operation failures.", errors);
    }

    private static async Task WithUnclaimedGmRuntimeAsync(Func<LegacyOwnedAdmissionRuntimeFixture, LegacyOwnedAdmissionRuntime, Task> test)
    {
        LegacyOwnedAdmissionRuntimeFixture? fixture = null;
        LegacyOwnedAdmissionRuntime? runtime = null;
        var errors = new List<Exception>();
        try
        {
            fixture = new LegacyOwnedAdmissionRuntimeFixture();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out runtime, out var reason), reason);
            await test(fixture, Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime));
        }
        catch (Exception error) { errors.Add(error); }
        if (runtime is not null)
            try { await runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        if (fixture is not null && errors.Count == 0)
            try { fixture.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("GM startup owner assertion and cleanup failures.", errors);
    }
}
