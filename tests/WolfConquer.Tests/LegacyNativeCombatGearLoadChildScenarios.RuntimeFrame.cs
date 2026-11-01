using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static void RunRuntimeOwnedFrame(Scope scope, RuntimeMetadataFiles files)
    {
        // A genuinely empty complete metadata set proves startup ownership;
        // it grants no populated gear/proc behavior or unresolved schema claim.
        files.RewriteGear("");
        var metadata = LoadRuntime(files);
        using var data = new LegacyOwnedAdmissionRuntimeFixture();
        Require(LegacyOwnedAdmissionRuntime.TryOpen(data.Loader, data.Options(), out var runtime, out var reason) && runtime is not null,
            "Actual existing owned domain must open: " + reason);
        try
        {
            var before = data.Files();
            var rawGear = new List<CombatGear>();
            var catalog = NativeCallerWorldDataFixture.Create([], [], combatGear: rawGear);
            var wrongProfile = Assert.Throws<ArgumentException>(() => new WorldServerFrameHandler(data.Loader, null, catalog,
                LegacyRuntimeUpdateLoopOptions.Disabled, scope.Log, protocolProfile: ConquerProtocolProfile.RootD11_7899,
                ownedAdmission: runtime, nativeCombatGear: metadata));
            Assert.Equal("nativeCombatGear", wrongProfile.ParamName);
            var absentOwned = Assert.Throws<ArgumentException>(() => new WorldServerFrameHandler(data.Loader, null, catalog,
                LegacyRuntimeUpdateLoopOptions.Disabled, scope.Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
                nativeCombatGear: metadata));
            Assert.Equal("nativeCombatGear", absentOwned.ParamName);
            data.AssertFiles(before);

            var frame = new WorldServerFrameHandler(data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                scope.Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime, nativeCombatGear: metadata);
            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
                .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(frame));
            Require(frame.TryCaptureNativeCombatGearMetadata(store, out var current) && ReferenceEquals(current, RequireProof()),
                "Current actual owned Frame must acquire the exact configured publication.");
            Require(!frame.TryCaptureNativeCombatGearMetadata(new WorldSessionStore(), out var foreign) && foreign is null,
                "A different Store cannot acquire the Frame metadata capability.");
            rawGear.Add(CombatGearParser.ParseRow("declared-catalog-drift.txt", 1,
                "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@"));
            Require(!frame.TryCaptureNativeCombatGearMetadata(store, out var drifted) && drifted is null,
                "Changed target catalog cannot use the native publication.");
            rawGear.Clear();
            Require(frame.TryCaptureNativeCombatGearMetadata(store, out current), "Restored exact empty catalog should align.");
            _ = LoadRuntime(files);
            Require(!metadata.IsCurrent && !frame.TryCaptureNativeCombatGearMetadata(store, out var superseded) && superseded is null,
                "An equal-content ambient reload cannot replace the Frame's explicit runtime owner.");
            data.AssertFiles(before);
            runtime!.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Require(!frame.TryCaptureNativeCombatGearMetadata(store, out var retired) && retired is null,
                "A retired actual owned runtime cannot provide native metadata.");
        }
        finally { runtime!.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
}
