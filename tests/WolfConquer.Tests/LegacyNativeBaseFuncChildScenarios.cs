using System.Runtime.InteropServices;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Invoked only by the fresh child fixture. No xUnit assertion or async dispatch inside this entry.
internal static class LegacyNativeBaseFuncChildScenarios
{
    public static string Execute(string scenario, string guard, string coreHash, string testsHash)
    {
        Require(OperatingSystem.IsWindows(), "Windows CRT is mandatory.");
        var thread = Environment.CurrentManagedThreadId;
        object data;
        switch (scenario)
        {
            case "SeedRepeatability":
                Seed(123456789UL, thread);
                var first = Draw(thread);
                var second = Draw(thread);
                Seed(123456789UL, thread);
                var repeatedFirst = Draw(thread);
                var repeatedSecond = Draw(thread);
                Require(first == repeatedFirst && second == repeatedSecond, "Seeded stream did not repeat.");
                data = new { First = first, Second = second, RepeatedFirst = repeatedFirst, RepeatedSecond = repeatedSecond };
                break;
            case "False100":
                data = Probe(100, 17UL, thread);
                break;
            case "Zero":
                data = Probe(0, 29UL, thread);
                break;
            case "Negative":
                data = new[] { Probe(-1, 41UL, thread), Probe(int.MinValue, 43UL, thread) };
                break;
            case "One":
                data = Probe(1, 59UL, thread);
                break;
            case "IntMaximum":
                data = Probe(int.MaxValue, 61UL, thread);
                break;
            case "SharedStream":
                int[] maxima = { 2, 7, 100, int.MaxValue };
                Seed(71UL, thread);
                long[] reference = { Draw(thread), Draw(thread), Draw(thread), Draw(thread), Draw(thread) };
                Seed(71UL, thread);
                var actual = new int[maxima.Length];
                for (var i = 0; i < maxima.Length; i++)
                {
                    actual[i] = Roll(maxima[i], false, thread);
                    Require(actual[i] == (int)(reference[i] % maxima[i]), "Shared stream remainder mismatch.");
                }
                var next = Draw(thread);
                Require(next == reference[^1], "Shared stream next-value mismatch.");
                data = new { Maxima = maxima, Reference = reference, Actual = actual, Next = next };
                break;
            case "TrueUnit":
                var trueResult = Roll(1, true, thread);
                Require(trueResult == 0, "Modulo one after real reseed must be zero.");
                data = new { Actual = trueResult, ReseedBranch = true, ExactClockSeedOrOriginalAbiClaimed = false };
                break;
            default:
                throw new InvalidOperationException("Unknown isolated CRT scenario.");
        }
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

    private static Dictionary<string, object> Probe(int maximum, ulong seed, int thread)
    {
        Seed(seed, thread);
        var first = Draw(thread);
        var second = Draw(thread);
        Seed(seed, thread);
        var actual = Roll(maximum, false, thread);
        var next = Draw(thread);
        var expected = (int)(first % (maximum <= 0 ? 1 : maximum));
        Require(actual == expected, "Literal signed long remainder mismatch.");
        Require(next == second, "One-draw next-value control mismatch.");
        return new Dictionary<string, object>
        {
            ["Maximum"] = maximum, ["Expected"] = expected, ["Actual"] = actual,
            ["ReferenceFirst"] = first, ["ExpectedNext"] = second, ["ActualNext"] = next,
        };
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

    private static int Roll(int maximum, bool real, int thread)
    {
        OnThread(thread);
        var value = LegacyNativeBaseFunc.RandGet(maximum, real);
        OnThread(thread);
        return value;
    }

    private static void OnThread(int expected) =>
        Require(Environment.CurrentManagedThreadId == expected, "CRT scenario changed its synchronous managed thread.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
