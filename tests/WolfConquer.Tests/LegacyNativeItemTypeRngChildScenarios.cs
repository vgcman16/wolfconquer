using System.Runtime.InteropServices;
using System.Text.Json;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;
using Row = WolfConquer.Core.World.LegacyNativeItemRuneMetadata;

namespace WolfConquer.Tests;

// Only the fresh child invokes this synchronous entry. No shared-testhost CRT seeding.
internal static class LegacyNativeItemTypeRngChildScenarios
{
    public static string Execute(string scenario, string guard, string coreHash, string testsHash)
    {
        Require(OperatingSystem.IsWindows(), "Windows CRT is mandatory.");
        var thread = Environment.CurrentManagedThreadId;
        object data = scenario switch
        {
            "RefineryTwo" => Refinery(2, thread),
            "RefineryOne" => Refinery(1, thread),
            "RefineryZero" => Refinery(0, thread),
            "TrueQuality" => TrueQuality(thread, 2),
            "TrueMeteors" => TrueMeteors(thread, 60),
            "ZeroQuality" => TrueQuality(thread, 0),
            "ZeroMeteors" => TrueMeteors(thread, 0),
            _ => throw new InvalidOperationException("Unknown isolated ItemType RNG scenario."),
        };
        OnThread(thread);
        return JsonSerializer.Serialize(new
        {
            Guard = guard, Scenario = scenario, CoreSha256 = coreHash, TestsSha256 = testsHash,
            Completed = true, ThreadId = thread, SameSynchronousThread = true,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            SourceDeclaredAbi = "Cdecl long srand(ulong); Cdecl long rand()",
            OriginalX86AbiEquivalenceClaimed = false,
            Data = data,
        });
    }

    private static object Refinery(byte wanted, int thread)
    {
        // A bounded native seeded search chooses a real observed branch, not a managed RNG oracle.
        const ulong searchLimit = 512;
        for (ulong seed = 1; seed <= searchLimit; seed++)
        {
            Seed(seed, thread);
            var first = Draw(thread);
            var second = Draw(thread);
            var third = Draw(thread);
            var firstRoll = (int)(first % 100);
            var secondRoll = (int)(second % 100);
            var expected = firstRoll < 30 ? (byte)2 : secondRoll < 30 ? (byte)1 : (byte)0;
            if (expected != wanted)
                continue;
            var draws = firstRoll < 30 ? 1 : 2;
            var expectedNext = draws == 1 ? second : third;
            Seed(seed, thread);
            OnThread(thread);
            var actual = Table.GetNextRefineryItem();
            OnThread(thread);
            var actualNext = Draw(thread);
            Require(actual == expected, "Literal refinery branch mismatch.");
            Require(actualNext == expectedNext, "Refinery continuation next-value mismatch.");
            return new
            {
                Seed = seed, SearchLimit = searchLimit, Expected = expected, Actual = actual,
                ReferenceFirst = first, ReferenceSecond = second, ReferenceThird = third,
                FirstRoll = firstRoll, SecondRoll = secondRoll, ExpectedDraws = draws,
                ExpectedNext = expectedNext, ActualNext = actualNext,
                UniversalPrngStateOrOriginalAbiClaimed = false,
            };
        }
        throw new InvalidOperationException("Required refinery branch absent from bounded native seed controls.");
    }

    private static object TrueQuality(int thread, uint offered)
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            var items = new Table();
            var row = new Row { ID = 125007, Level = 0, GoldWorth = 12345 };
            items.Add(125007, row);
            LegacyNativePoolItems.ItemsBase = items;
            var cost = Table.GetUpEpQualityInfo(125007);
            Require(cost == 3, "Genuine live row fixture cost must remain three.");
            OnThread(thread);
            var actual = Table.UpQualityDB(125007, offered);
            OnThread(thread);
            Require(ReferenceEquals(items[125007], row) && row.Level == 0 &&
                row.GoldWorth == 12345 && items.Count == 1, "Upgrade predicate unexpectedly mutated item data.");
            return new
            {
                Cost = cost, Offered = offered, TruncatedHundredDivCost = 100 / cost,
                LiteralThreshold = (double)offered * (100 / cost), Actual = actual,
                SubCostRealReseedPathCompleted = true,
                ExactClockSeedProbabilityOrDrawEquivalenceClaimed = false,
            };
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = previous;
        }
    }

    private static object TrueMeteors(int thread, uint offered)
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            var cost = Table.GetUpEpLevelInfo(130089);
            Require(cost == 61, "Genuine ID-only fixture cost must remain sixty-one.");
            OnThread(thread);
            var actual = Table.UpItemMeteors(130089, offered);
            OnThread(thread);
            Require(LegacyNativePoolItems.ItemsBase is null, "Meteor predicate must not create a global table.");
            return new
            {
                Cost = cost, Offered = offered, TruncatedHundredDivCost = 100 / cost,
                LiteralThreshold = (double)offered * (100 / cost), Actual = actual,
                SubCostRealReseedPathCompleted = true,
                ExactClockSeedProbabilityOrDrawEquivalenceClaimed = false,
            };
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = previous;
        }
    }

    private static void Seed(ulong value, int thread)
    {
        OnThread(thread);
        LegacyNativeBaseFunc.srand(value);
        OnThread(thread);
    }

    private static long Draw(int thread)
    {
        OnThread(thread);
        var value = LegacyNativeBaseFunc.rand();
        OnThread(thread);
        return value;
    }

    private static void OnThread(int expected) =>
        Require(Environment.CurrentManagedThreadId == expected, "CRT caller scenario changed its synchronous thread.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
