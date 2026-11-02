using System.Diagnostics;
using System.Globalization;

namespace WolfConquer.Tests;

// Fixture-only diagnostics. Disabled by default; never supplies gameplay state.
internal sealed class FixturePhaseTiming : IDisposable
{
    private readonly string name;
    private readonly Dictionary<string, (long Ticks, int Count)> phases = new(StringComparer.Ordinal);
    private FixturePhaseTiming(string name) => this.name = name;

    internal static FixturePhaseTiming? Create(string name)
    {
        try
        {
            return string.Equals(Environment.GetEnvironmentVariable("WOLF_TEST_PHASE_TIMING"), "1", StringComparison.Ordinal)
                ? new(name) : null;
        }
        catch (Exception) { return null; }
    }

    internal Scope Measure(string phase)
    {
        try { return new(this, phase, Stopwatch.GetTimestamp()); }
        catch (Exception) { return default; }
    }

    internal readonly struct Scope(FixturePhaseTiming? owner, string phase, long started) : IDisposable
    {
        public void Dispose()
        {
            try { owner?.Add(phase, Stopwatch.GetTimestamp() - started); } catch (Exception) { }
        }
    }

    public void Dispose() => Emit();

    private void Add(string phase, long elapsed)
    {
        phases.TryGetValue(phase, out var prior);
        phases[phase] = (prior.Ticks + elapsed, prior.Count + 1);
    }

    internal void Emit()
    {
        try
        {
            var parts = phases.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
                pair.Key + "=" + (pair.Value.Ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                "ms/" + pair.Value.Count.ToString(CultureInfo.InvariantCulture));
            Console.Error.WriteLine("WOLF_TEST_PHASE_TIMING " + name + " " + string.Join(" ", parts));
        }
        catch (Exception) { }
    }
}
