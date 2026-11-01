using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    private ProficiencyInputImage? proficiencyInput;

    internal static async Task RunWithProficiencyImageAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, byte[] actorImage, Func<OwnedEquipmentBehaviorFixture, Task> exercise,
        params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(exercise);
        if (options.Enabled || options.TimeProvider is null)
            throw new ArgumentException("Owned proficiency inputs require disabled runtime options with a real clock.", nameof(options));
        var input = new ProficiencyInputImage(actor, actorImage);
        OwnedEquipmentBehaviorFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = new OwnedEquipmentBehaviorFixture { proficiencyInput = input };
            await fixture.InitializeAsync(actor, catalog, options, peers);
            await exercise(fixture);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    // Called only after the ordinary input saver and before actual Bootstrap.
    // This closed data input cannot choose another path, callback or owner.
    private void ApplyProficiencyInputImage()
    {
        if (proficiencyInput is not { } input) return;
        var path = Path.Combine(Data.Proficiencies, input.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        input.ApplyToSavedActor(path);
        proficiencyInput = null;
    }

    private sealed class ProficiencyInputImage
    {
        internal uint Uid { get; }
        private readonly byte[] image, canonical;
        internal ProficiencyInputImage(OwnedEquipmentBehaviorInput actor, byte[] source)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (actor.Character.Uid is <= 0 or > uint.MaxValue || source.Length is < 4 or > 16 * 1024 * 1024)
                throw new ArgumentException("Invalid owned proficiency input image.", nameof(source));
            Uid = checked((uint)actor.Character.Uid); image = (byte[])source.Clone();
            var parsed = PlayerProficiencySnapshotLoader.Parse(image);
            if (parsed.Proficiencies.Any(row => row.Id == 0) ||
                parsed.Proficiencies.Select(row => row.Id).Distinct().Count() != parsed.Proficiencies.Count)
                throw new ArgumentException("Proficiency input requires unique positive IDs.", nameof(source));
            canonical = PlayerProficiencySnapshotLoader.ToLegacyBytes(actor.Proficiencies ?? PlayerProficiencySnapshot.Empty);
            if (!PlayerProficiencySnapshotLoader.ToLegacyBytes(parsed).AsSpan().SequenceEqual(canonical))
                throw new ArgumentException("Raw proficiency input must match every declared typed value and row order.", nameof(source));
        }

        internal void ApplyToSavedActor(string actualActorPath)
        {
            var saved = PlayerProficiencySnapshotLoader.Parse(File.ReadAllBytes(actualActorPath));
            if (!PlayerProficiencySnapshotLoader.ToLegacyBytes(saved).AsSpan().SequenceEqual(canonical))
                throw new InvalidOperationException("Declared proficiency input changed before Bootstrap.");
            File.WriteAllBytes(actualActorPath, image);
        }
    }
}
