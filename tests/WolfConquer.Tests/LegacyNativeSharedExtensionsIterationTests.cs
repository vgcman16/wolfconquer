using System.Reflection;
using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSharedExtensionsIterationTests
{
    [Fact]
    public void GenericExtensionSignaturesKeepRequiredDelegatesAndOptionalNullPredicate()
    {
        foreach (var name in new[] { "Iterate", "Iteratefun", "Iteratefunbool" })
        {
            var method = typeof(LegacyNativeSharedExtensions).GetMethod(name,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.Same(typeof(LegacyNativeSharedExtensions), method.DeclaringType);
            Assert.True(method.IsPublic && method.IsStatic && method.IsGenericMethodDefinition);
            Assert.Equal(typeof(void), method.ReturnType);
            Assert.NotNull(method.GetCustomAttribute<ExtensionAttribute>());
            var type = Assert.Single(method.GetGenericArguments());
            Assert.Equal("T", type.Name);
            Assert.Equal(GenericParameterAttributes.None, type.GenericParameterAttributes);
            Assert.Empty(type.GetGenericParameterConstraints());
            var parameters = method.GetParameters();
            Assert.Equal(name == "Iterate" ? new[] { "collection", "action" } : new[] { "collection", "action", "applied" },
                parameters.Select(parameter => parameter.Name));
            Assert.Equal(type.MakeArrayType(), parameters[0].ParameterType);
            Assert.Equal(typeof(Action<>).MakeGenericType(type), parameters[1].ParameterType);
            Assert.All(parameters.Take(2), parameter =>
            {
                Assert.False(parameter.IsOptional);
                Assert.False(parameter.HasDefaultValue);
            });
            if (name != "Iterate")
            {
                Assert.Equal(typeof(Func<,>).MakeGenericType(type, typeof(bool)), parameters[2].ParameterType);
                Assert.True(parameters[2].IsOptional);
                Assert.True(parameters[2].HasDefaultValue);
                Assert.Null(parameters[2].DefaultValue);
            }
        }
    }

    [Fact]
    public void IterateVisitsEveryValueInOrderIncludingDuplicates()
    {
        var seen = new List<int>();
        LegacyNativeSharedExtensions.Iterate(new[] { 3, 1, 3, 0 }, seen.Add);
        Assert.Equal(new[] { 3, 1, 3, 0 }, seen);
    }

    [Fact]
    public void IteratePassesSameReferencesAndNullElements()
    {
        var first = new object();
        var second = new object();
        var seen = new List<object?>();
        LegacyNativeSharedExtensions.Iterate(new object?[] { first, null, first, second }, seen.Add);
        Assert.Collection(seen, value => Assert.Same(first, value), value => Assert.Null(value),
            value => Assert.Same(first, value), value => Assert.Same(second, value));
    }

    [Fact]
    public void EmptyArraysNeverDereferenceNullActionsOrEvaluatePredicates()
    {
        LegacyNativeSharedExtensions.Iterate<int>(Array.Empty<int>(), null!);
        LegacyNativeSharedExtensions.Iteratefun<int>(Array.Empty<int>(), null!);
        LegacyNativeSharedExtensions.Iteratefunbool<int>(Array.Empty<int>(), null!);
        LegacyNativeSharedExtensions.Iteratefun<int>(Array.Empty<int>(), null!, _ => throw new InvalidOperationException("unvisited"));
        LegacyNativeSharedExtensions.Iteratefunbool<int>(Array.Empty<int>(), null!, _ => throw new InvalidOperationException("unvisited"));
    }

    [Fact]
    public void NullArraysFaultBeforeAnyDelegateSideEffect()
    {
        var actions = 0;
        var predicates = 0;
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iterate<int>(null!, _ => actions++));
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefun<int>(null!, _ => actions++, _ => { predicates++; return true; }));
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefunbool<int>(null!, _ => actions++, _ => { predicates++; return true; }));
        Assert.Equal(0, actions);
        Assert.Equal(0, predicates);
    }

    [Fact]
    public void UnfilteredNullActionsFaultAtTheFirstElement()
    {
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iterate<int>(new[] { 7, 8 }, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefun<int>(new[] { 7, 8 }, null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefunbool<int>(new[] { 7, 8 }, null!));
    }

    [Fact]
    public void IteratePropagatesTheSameSentinelAfterEarlierEffects()
    {
        var sentinel = new InvalidOperationException("action");
        var seen = new List<int>();
        var actual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeSharedExtensions.Iterate(new[] { 1, 2, 3 }, value =>
            {
                seen.Add(value);
                if (value == 2) throw sentinel;
            }));
        Assert.Same(sentinel, actual);
        Assert.Equal(new[] { 1, 2 }, seen);
    }

    [Fact]
    public void IterateReadsFutureSlotsChangedByAction()
    {
        var values = new[] { 1, 2, 3 };
        var seen = new List<int>();
        LegacyNativeSharedExtensions.Iterate(values, value =>
        {
            seen.Add(value);
            if (value == 1) values[1] = 9;
        });
        Assert.Equal(new[] { 1, 9, 3 }, seen);
        Assert.Equal(new[] { 1, 9, 3 }, values);
    }

    [Fact]
    public void BothFunVariantsTreatNullPredicateAsUnconditionalFullTraversal()
    {
        var first = new List<int>();
        var second = new List<int>();
        LegacyNativeSharedExtensions.Iteratefun(new[] { 2, 2, 3 }, first.Add, null);
        LegacyNativeSharedExtensions.Iteratefunbool(new[] { 2, 2, 3 }, second.Add);
        Assert.Equal(new[] { 2, 2, 3 }, first);
        Assert.Equal(new[] { 2, 2, 3 }, second);
    }

    [Fact]
    public void BothFunVariantsEvaluatePredicateBeforeActionWithoutEarlyBreak()
    {
        var first = new List<string>();
        var second = new List<string>();
        LegacyNativeSharedExtensions.Iteratefun(new[] { 1, 2, 3 }, value => first.Add("a" + value),
            value => { first.Add("p" + value); return value != 2; });
        LegacyNativeSharedExtensions.Iteratefunbool(new[] { 1, 2, 3 }, value => second.Add("a" + value),
            value => { second.Add("p" + value); return value != 2; });
        Assert.Equal(new[] { "p1", "a1", "p2", "p3", "a3" }, first);
        Assert.Equal(new[] { "p1", "a1", "p2", "p3", "a3" }, second);
    }

    [Fact]
    public void BothFunVariantsAllowNullActionWhenEveryPredicateIsFalse()
    {
        var first = new List<int>();
        var second = new List<int>();
        LegacyNativeSharedExtensions.Iteratefun<int>(new[] { 1, 2, 3 }, null!, value => { first.Add(value); return false; });
        LegacyNativeSharedExtensions.Iteratefunbool<int>(new[] { 1, 2, 3 }, null!, value => { second.Add(value); return false; });
        Assert.Equal(new[] { 1, 2, 3 }, first);
        Assert.Equal(new[] { 1, 2, 3 }, second);
    }

    [Fact]
    public void BothFunVariantsFaultOnlyAtTheFirstPredicateAllowedNullAction()
    {
        var first = new List<int>();
        var second = new List<int>();
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefun<int>(
            new[] { 1, 2, 3 }, null!, value => { first.Add(value); return value == 2; }));
        Assert.Throws<NullReferenceException>(() => LegacyNativeSharedExtensions.Iteratefunbool<int>(
            new[] { 1, 2, 3 }, null!, value => { second.Add(value); return value == 2; }));
        Assert.Equal(new[] { 1, 2 }, first);
        Assert.Equal(new[] { 1, 2 }, second);
    }

    [Fact]
    public void BothFunVariantsKeepTheValueCapturedBeforePredicateReplacesCurrentSlot()
    {
        var firstValues = new[] { 1, 2 };
        var secondValues = new[] { 1, 2 };
        var first = new List<int>();
        var second = new List<int>();
        var firstIndex = 0;
        var secondIndex = 0;
        LegacyNativeSharedExtensions.Iteratefun(firstValues, first.Add,
            _ => { firstValues[firstIndex++] = 99; return true; });
        LegacyNativeSharedExtensions.Iteratefunbool(secondValues, second.Add,
            _ => { secondValues[secondIndex++] = 99; return true; });
        Assert.Equal(new[] { 1, 2 }, first);
        Assert.Equal(new[] { 1, 2 }, second);
        Assert.Equal(new[] { 99, 99 }, firstValues);
        Assert.Equal(new[] { 99, 99 }, secondValues);
    }

    [Fact]
    public void BothFunVariantsObserveFutureSlotsChangedByPredicate()
    {
        var firstValues = new[] { 1, 2, 3 };
        var secondValues = new[] { 1, 2, 3 };
        var first = new List<int>();
        var second = new List<int>();
        LegacyNativeSharedExtensions.Iteratefun(firstValues, first.Add,
            value => { if (value == 1) firstValues[1] = 9; return true; });
        LegacyNativeSharedExtensions.Iteratefunbool(secondValues, second.Add,
            value => { if (value == 1) secondValues[1] = 9; return true; });
        Assert.Equal(new[] { 1, 9, 3 }, first);
        Assert.Equal(new[] { 1, 9, 3 }, second);
        Assert.Equal(new[] { 1, 9, 3 }, firstValues);
        Assert.Equal(new[] { 1, 9, 3 }, secondValues);
    }

    [Fact]
    public void BothFunVariantsKeepCapturedReferenceAndSeeMutationThroughThatReference()
    {
        var first = new[] { 1 };
        var second = new[] { 2 };
        var firstReplacement = new[] { 99 };
        var secondReplacement = new[] { 88 };
        var firstValues = new[] { first };
        var secondValues = new[] { second };
        var firstSeen = new List<int[]>();
        var secondSeen = new List<int[]>();
        LegacyNativeSharedExtensions.Iteratefun(firstValues, firstSeen.Add,
            _ => { firstValues[0] = firstReplacement; first[0] = 7; return true; });
        LegacyNativeSharedExtensions.Iteratefunbool(secondValues, secondSeen.Add,
            _ => { secondValues[0] = secondReplacement; second[0] = 8; return true; });
        Assert.Same(first, Assert.Single(firstSeen));
        Assert.Same(second, Assert.Single(secondSeen));
        Assert.Equal(7, firstSeen[0][0]);
        Assert.Equal(8, secondSeen[0][0]);
        Assert.Same(firstReplacement, firstValues[0]);
        Assert.Same(secondReplacement, secondValues[0]);
    }

    [Fact]
    public void BothFunVariantsPropagatePredicateSentinelAfterEarlierAction()
    {
        var firstSentinel = new InvalidOperationException("predicate1");
        var secondSentinel = new InvalidOperationException("predicate2");
        var firstPredicates = new List<int>();
        var secondPredicates = new List<int>();
        var firstActions = new List<int>();
        var secondActions = new List<int>();
        var firstActual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeSharedExtensions.Iteratefun(new[] { 1, 2, 3 }, firstActions.Add,
                value => { firstPredicates.Add(value); if (value == 2) throw firstSentinel; return true; }));
        var secondActual = Assert.Throws<InvalidOperationException>(() =>
            LegacyNativeSharedExtensions.Iteratefunbool(new[] { 1, 2, 3 }, secondActions.Add,
                value => { secondPredicates.Add(value); if (value == 2) throw secondSentinel; return true; }));
        Assert.Same(firstSentinel, firstActual);
        Assert.Same(secondSentinel, secondActual);
        Assert.Equal(new[] { 1, 2 }, firstPredicates);
        Assert.Equal(new[] { 1, 2 }, secondPredicates);
        Assert.Equal(new[] { 1 }, firstActions);
        Assert.Equal(new[] { 1 }, secondActions);
    }

    [Fact]
    public void BothFunVariantsPropagateActionSentinelAndStopLaterPredicates()
    {
        var firstSentinel = new InvalidOperationException("action1");
        var secondSentinel = new InvalidOperationException("action2");
        var firstPredicates = new List<int>();
        var secondPredicates = new List<int>();
        var firstActions = new List<int>();
        var secondActions = new List<int>();
        var firstActual = Assert.Throws<InvalidOperationException>(() => LegacyNativeSharedExtensions.Iteratefun(new[] { 1, 2, 3 },
            value => { firstActions.Add(value); if (value == 2) throw firstSentinel; },
            value => { firstPredicates.Add(value); return true; }));
        var secondActual = Assert.Throws<InvalidOperationException>(() => LegacyNativeSharedExtensions.Iteratefunbool(new[] { 1, 2, 3 },
            value => { secondActions.Add(value); if (value == 2) throw secondSentinel; },
            value => { secondPredicates.Add(value); return true; }));
        Assert.Same(firstSentinel, firstActual);
        Assert.Same(secondSentinel, secondActual);
        Assert.Equal(new[] { 1, 2 }, firstPredicates);
        Assert.Equal(new[] { 1, 2 }, secondPredicates);
        Assert.Equal(new[] { 1, 2 }, firstActions);
        Assert.Equal(new[] { 1, 2 }, secondActions);
    }

    [Fact]
    public void BothFunVariantsReadFutureSlotsChangedByAction()
    {
        var firstValues = new[] { 1, 2, 3 };
        var secondValues = new[] { 1, 2, 3 };
        var first = new List<int>();
        var second = new List<int>();
        LegacyNativeSharedExtensions.Iteratefun(firstValues,
            value => { first.Add(value); if (value == 1) firstValues[1] = 8; });
        LegacyNativeSharedExtensions.Iteratefunbool(secondValues,
            value => { second.Add(value); if (value == 1) secondValues[1] = 8; });
        Assert.Equal(new[] { 1, 8, 3 }, first);
        Assert.Equal(new[] { 1, 8, 3 }, second);
    }

    [Fact]
    public void BothFunVariantsPassDuplicateReferencesAndNullElements()
    {
        var value = new object();
        var first = new List<object?>();
        var second = new List<object?>();
        LegacyNativeSharedExtensions.Iteratefun(new object?[] { value, null, value }, first.Add, _ => true);
        LegacyNativeSharedExtensions.Iteratefunbool(new object?[] { value, null, value }, second.Add, _ => true);
        Assert.Collection(first, item => Assert.Same(value, item), item => Assert.Null(item), item => Assert.Same(value, item));
        Assert.Collection(second, item => Assert.Same(value, item), item => Assert.Null(item), item => Assert.Same(value, item));
    }
}
