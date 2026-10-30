using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Client;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.Web;
using WolfConquer.Core.World;

internal static partial class Program
{
    static int Main(string[] args)
    {
        var command = args.Length > 0 && IsCommand(args[0])
            ? args[0].ToLowerInvariant()
            : "probe";

        if (command is "help" or "--help" or "-h")
        {
            return Usage();
        }

        if (command == "send-operator-gear-mail")
        {
            return SendOperatorGearMail(args);
        }

        if (command == "create-operator-test-kit") return CreateOperatorTestKit(args);
        if (command == "create-gm-policy") return CreateGmPolicy(args);
        if (command == "inspect-owned-player") return InspectOwnedPlayer(args);
        if (command == "inspect-runtime-player") return InspectRuntimePlayer(args);

        if (command == "patch-client")
        {
            return PatchClient(args);
        }

        if (command == "patch-client-emoney-shop-v2")
        {
            return PatchClientEmoneyShopV2(args);
        }

        if (command == "patch-client-emoney-shop-v2-source-backed")
        {
            return PatchClientEmoneyShopV2SourceBacked(args);
        }

        if (command == "patch-client-cn-res-source-backed")
        {
            return PatchClientCnResSourceBacked(args);
        }

        if (command == "patch-client-server-key-items")
        {
            return PatchClientServerKeyItems(args);
        }

        if (command == "patch-client-server-key-resources")
        {
            return PatchClientServerKeyResources(args);
        }

        if (command == "patch-client-magic-effect")
        {
            return PatchClientMagicEffect(args);
        }

        if (command == "patch-client-magic-effect-source-backed")
        {
            return PatchClientMagicEffectSourceBacked(args);
        }

        if (command == "patch-client-quest-info")
        {
            return PatchClientQuestInfo(args);
        }

        if (command == "patch-client-quest-info-passion")
        {
            return PatchClientQuestInfoPassion(args);
        }

        if (command == "patch-client-quest-info-random-source-backed")
        {
            return PatchClientQuestInfoRandomSourceBacked(args);
        }

        if (command == "patch-client-quest-info-random-passion-source-backed")
        {
            return PatchClientQuestInfoRandomPassionSourceBacked(args);
        }

        if (command == "patch-client-game-map-ex")
        {
            return PatchClientGameMapEx(args);
        }

        if (command == "patch-client-rune-desc")
        {
            return PatchClientRuneDesc(args);
        }

        if (command == "patch-client-gouyu-skill-text")
        {
            return PatchClientGouYuSkillText(args);
        }

        if (command == "patch-client-limit-shop-source-backed")
        {
            return PatchClientLimitShopSourceBacked(args);
        }

        if (command == "patch-client-item-ui-source-backed")
        {
            return PatchClientItemUiSourceBacked(args);
        }

        if (command == "patch-client-status-text-source-backed")
        {
            return PatchClientStatusTextSourceBacked(args);
        }

        if (command == "patch-client-monster-say-templates")
        {
            return PatchClientMonsterSayTemplates(args);
        }

        if (command == "patch-client-str-res-source-backed")
        {
            return PatchClientStrResSourceBacked(args);
        }

        if (command == "patch-db-shop-names")
        {
            return PatchDbShopNames(args);
        }

        if (command == "patch-db-sob-npc-names")
        {
            return PatchDbSobNpcNames(args);
        }

        if (command == "patch-db-visible-text")
        {
            return PatchDbVisibleText(args);
        }

        if (command == "patch-db-quest-info-mojibake")
        {
            return PatchDbQuestInfoMojibake(args);
        }

        if (command == "scan-client-text-files")
        {
            return ScanClientTextFiles(args);
        }

        if (command == "scan-cjk-text-debt")
        {
            return ScanCjkTextDebt(args);
        }

        if (command == "inspect-client-routing")
        {
            return InspectClientRouting(args);
        }

        if (command == "snapshot-client-routing")
        {
            return SnapshotClientRouting(args);
        }

        if (command == "client-structural-drift")
        {
            return ClientStructuralDrift(args);
        }

        if (command == "client-file-drift")
        {
            return ClientFileDrift(args);
        }

        if (command == "client-lua-drift-audit")
        {
            return ClientLuaDriftAudit(args);
        }

        if (command == "client-asset-presence-audit")
        {
            return ClientAssetPresenceAudit(args);
        }

        if (command == "client-overlay-item-ref-audit")
        {
            return ClientOverlayItemRefAudit(args);
        }

        if (command == "client-string-ref-audit")
        {
            return ClientStringRefAudit(args);
        }

        if (command == "client-playtest-audit")
        {
            return ClientPlaytestAudit(args);
        }

        if (command == "visible-login-seam-audit")
        {
            return VisibleLoginSeamAudit(args);
        }

        if (command == "combat-close-evidence")
        {
            return CombatCloseEvidence(args);
        }

        if (command == "monster-spawn-audit")
        {
            return MonsterSpawnAudit(args);
        }

        if (command == "monster-family-audit")
        {
            return MonsterFamilyAudit(args);
        }

        if (command is "character-automatic-proc-eligibility-audit" or
            "character-auto-proc-eligibility-audit" or
            "character-automatic-proc-audit")
        {
            return CharacterAutomaticProcEligibilityAudit(args);
        }

        if (command == "visible-login-line-markers")
        {
            return VisibleLoginLineMarkers(args);
        }

        if (command == "client-launcher-gate-audit")
        {
            return ClientLauncherGateAudit(args);
        }

        if (command == "client-launcher-safety-audit")
        {
            return ClientLauncherSafetyAudit(args);
        }

        if (command == "client-itemtype-warning-owner-audit")
        {
            return ClientItemTypeWarningOwnerAudit(args);
        }

        if (command == "client-texture-warning-owner-audit")
        {
            return ClientTextureWarningOwnerAudit(args);
        }

        if (command == "client-packet-catch-context-audit")
        {
            return ClientPacketCatchContextAudit(args);
        }

        if (command == "client-startup-audit")
        {
            return ClientStartupAudit(args);
        }

        if (command == "client-launcher-trace-audit")
        {
            return ClientLauncherTraceAudit(args);
        }

        if (command == "client-login-cache-audit")
        {
            return ClientLoginCacheAudit(args);
        }

        if (command == "client-launch-evidence")
        {
            return ClientLaunchEvidence(args);
        }

        if (command == "client-native-launch-probe")
        {
            return ClientNativeLaunchProbe(args);
        }

        if (command == "client-launch-dependency-audit")
        {
            return ClientLaunchDependencyAudit(args);
        }

        if (command == "client-launch-contract-audit")
        {
            return ClientLaunchContractAudit(args);
        }

        if (command == "client-pe-symbol-audit")
        {
            return ClientPeSymbolAudit(args);
        }

        if (command == "client-pe-dependency-audit")
        {
            return ClientPeDependencyAudit(args);
        }

        if (command == "client-config-decode")
        {
            return ClientConfigDecodeCommand(args);
        }

        if (command == "client-item-catalog-roundtrip")
        {
            return ClientItemCatalogRoundtrip(args);
        }

        if (command == "client-reference-intake") return ClientReferenceIntake(args);

        if (command == "client-item-catalog7937-decode") return ClientChinese7937CatalogDecodeCommand(args);

        if (command == "client-item-catalog-codec")
        {
            return ClientItemCatalogCodecCommand(args);
        }

        if (command == "client-item-catalog-localize")
        {
            return ClientItemCatalogLocalizeCommand(args);
        }

        if (command == "generated-buffer-string-scan")
        {
            return GeneratedBufferStringScan(args);
        }

        if (command == "license-route-capture-compare")
        {
            return LicenseRouteCaptureCompare(args);
        }

        if (command == "license-route-relay-compare")
        {
            return LicenseRouteRelayCompare(args);
        }

        if (command == "license-route-relay-correlation")
        {
            return LicenseRouteRelayCorrelation(args);
        }

        if (command == "cached-client-thread-evidence-map")
        {
            return CachedClientThreadEvidenceMap(args);
        }

        if (command == "cached-client-patch-result-summary")
        {
            return CachedClientPatchResultSummary(args);
        }

        if (command == "cached-client-branch-frontier-summary")
        {
            return CachedClientBranchFrontierSummary(args);
        }

        if (command == "cached-client-module-probe-compare")
        {
            return CachedClientModuleProbeCompare(args);
        }

        if (command == "raw-module-branch-map")
        {
            return RawModuleBranchMap(args);
        }

        if (command == "raw-module-branch-patch-spec")
        {
            return RawModuleBranchPatchSpec(args);
        }

        if (command == "raw-module-branch-spine")
        {
            return RawModuleBranchSpine(args);
        }

        if (command == "computed-pointer-scan")
        {
            return ComputedPointerScan(args);
        }

        if (command == "minidump-module-carve")
        {
            return MinidumpModuleCarve(args);
        }

        if (command == "export-legacy-game-maps")
        {
            return ExportLegacyGameMaps(args);
        }

        if (command == "probe-endpoints")
        {
            return ProbeEndpoints(args);
        }

        if (command == "tcp-relay-capture")
        {
            return TcpRelayCapture(args);
        }

        if (command == "tcp-relay-capture-summary")
        {
            return TcpRelayCaptureSummary(args);
        }

        if (command == "parity-log-summary")
        {
            return ParityLogSummary(args);
        }

        if (command == "legacy-account-smoke")
        {
            return LegacyAccountSmoke(args);
        }

        if (command == "legacy-guard-shield-smoke")
        {
            return LegacyGuardShieldSmoke(args);
        }

        if (command == "legacy-world-smoke")
        {
            return LegacyWorldSmoke(args);
        }

        if (command == "seed-owned-admission-fixture")
        {
            return SeedOwnedAdmissionFixture(args);
        }

        if (command == "seed-loot-roundtrip-fixture")
        {
            return SeedLootRoundtripFixture(args);
        }

        if (command == "legacy-loot-roundtrip-smoke")
        {
            return LegacyLootRoundtripSmoke(args);
        }

        if (command == "local-live-stack-smoke")
        {
            return LocalLiveStackSmoke(args);
        }

        if (command == "legacy-world-capture")
        {
            return LegacyWorldCapture(args);
        }

        if (command == "legacy-world-relay-decode")
        {
            return LegacyWorldRelayDecode(args);
        }

        if (command == "legacy-world-active-bridge-capture")
        {
            return LegacyWorldActiveBridgeCapture(args);
        }

        if (command == "legacy-world-active-bridge-summary")
        {
            return LegacyWorldActiveBridgeSummary(args);
        }

        if (command == "active-bridge-capture-runbook")
        {
            return ActiveBridgeCaptureRunbook(args);
        }

        if (command == "active-bridge-capture-status")
        {
            return ActiveBridgeCaptureStatus(args);
        }

        if (command == "legacy-world-capture-compare")
        {
            return LegacyWorldCaptureCompare(args);
        }

        if (command == "analyze-endpoint-probes")
        {
            return AnalyzeEndpointProbes(args);
        }

        if (command == "compare-endpoint-probes")
        {
            return CompareEndpointProbes(args);
        }

        if (command == "protocol-evidence")
        {
            return ProtocolEvidence(args);
        }

        if (command == "write-pktmon-capture-script")
        {
            return WritePktmonCaptureScript(args);
        }

        if (command == "pktmon-capture-readiness")
        {
            return PktmonCaptureReadiness(args);
        }

        if (command == "pktmon-capture-runbook")
        {
            return PktmonCaptureRunbook(args);
        }

        if (command == "world-relay-capture-runbook")
        {
            return WorldRelayCaptureRunbook(args);
        }

        if (command == "world-relay-capture-preflight")
        {
            return WorldRelayCapturePreflight(args);
        }

        if (command == "original-flow-capture-preflight")
        {
            return OriginalFlowCapturePreflight(args);
        }

        if (command == "loginweb-loader-readiness")
        {
            return LoginWebLoaderReadiness(args);
        }

        if (command == "local-world-handoff-status")
        {
            return LocalWorldHandoffStatus(args);
        }

        if (command == "pktmon-fixture-summary")
        {
            return PktmonFixtureSummary(args);
        }

        if (command == "pktmon-payload-summary")
        {
            return PktmonPayloadSummary(args);
        }

        var dbServerPath = ResolveDbServerPath(command, args);

        if (!Directory.Exists(dbServerPath))
        {
            Console.Error.WriteLine($"DBServer path not found: {dbServerPath}");
            return 1;
        }

        return command switch
        {
            "probe" => Probe(dbServerPath),
            "export" => Export(dbServerPath, ResolveOutputPath(args), ResolveIncludedGameMapExPaths(args)),
            _ => Usage()
        };
    }

    static bool IsCommand(string value)
    {
        return string.Equals(value, "probe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "export", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "probe-endpoints", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "tcp-relay-capture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "tcp-relay-capture-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "parity-log-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-account-smoke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-guard-shield-smoke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-smoke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "seed-owned-admission-fixture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "send-operator-gear-mail", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "create-operator-test-kit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "create-gm-policy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "inspect-owned-player", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "inspect-runtime-player", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "seed-loot-roundtrip-fixture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-loot-roundtrip-smoke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "local-live-stack-smoke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-capture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-relay-decode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-active-bridge-capture", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-active-bridge-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "active-bridge-capture-runbook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "active-bridge-capture-status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "legacy-world-capture-compare", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "analyze-endpoint-probes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "compare-endpoint-probes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "protocol-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "write-pktmon-capture-script", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "pktmon-capture-readiness", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "pktmon-capture-runbook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "world-relay-capture-runbook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "world-relay-capture-preflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "original-flow-capture-preflight", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "loginweb-loader-readiness", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "local-world-handoff-status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "pktmon-fixture-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "pktmon-payload-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "inspect-client-routing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "snapshot-client-routing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-structural-drift", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-file-drift", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-lua-drift-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-asset-presence-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-overlay-item-ref-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-string-ref-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-playtest-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "visible-login-seam-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "combat-close-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "monster-spawn-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "monster-family-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "character-automatic-proc-eligibility-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "character-auto-proc-eligibility-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "character-automatic-proc-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "visible-login-line-markers", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launcher-gate-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launcher-safety-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-itemtype-warning-owner-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-texture-warning-owner-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-packet-catch-context-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-startup-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launcher-trace-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-login-cache-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launch-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-native-launch-probe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launch-dependency-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-launch-contract-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-pe-symbol-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-pe-dependency-audit", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-config-decode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-item-catalog-roundtrip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-item-catalog-codec", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-item-catalog7937-decode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-reference-intake", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "client-item-catalog-localize", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "generated-buffer-string-scan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "license-route-capture-compare", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "license-route-relay-compare", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "license-route-relay-correlation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "cached-client-thread-evidence-map", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "cached-client-patch-result-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "cached-client-branch-frontier-summary", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "cached-client-module-probe-compare", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "raw-module-branch-map", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "raw-module-branch-patch-spec", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "raw-module-branch-spine", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "computed-pointer-scan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "minidump-module-carve", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "export-legacy-game-maps", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-emoney-shop-v2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-emoney-shop-v2-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-cn-res-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-server-key-items", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-server-key-resources", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-magic-effect", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-magic-effect-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-quest-info", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-quest-info-passion", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-quest-info-random-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-quest-info-random-passion-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-game-map-ex", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-rune-desc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-gouyu-skill-text", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-limit-shop-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-item-ui-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-status-text-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-monster-say-templates", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-client-str-res-source-backed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-db-shop-names", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-db-sob-npc-names", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-db-visible-text", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "patch-db-quest-info-mojibake", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "scan-client-text-files", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "scan-cjk-text-debt", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase);
    }

    static int ProtocolEvidence(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Protocol evidence requires at least one endpoint probe JSON path.");
            return 1;
        }

        var captures = new List<EndpointProbeCapture>();
        for (var index = 1; index < args.Length; index++)
        {
            var path = Path.GetFullPath(args[index]);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Endpoint probe JSON not found: {path}");
                return 1;
            }

            EndpointProbeCapture? capture;
            try
            {
                capture = JsonSerializer.Deserialize<EndpointProbeCapture>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Could not parse endpoint probe JSON '{path}': {ex.Message}");
                return 1;
            }

            if (capture is null)
            {
                Console.Error.WriteLine($"Endpoint probe JSON is empty: {path}");
                return 1;
            }

            captures.Add(capture);
        }

        Console.WriteLine($"Protocol evidence: {captures.Count:N0} capture(s)");
        foreach (var evidence in EndpointProtocolEvidenceReporter.Create(captures))
        {
            Console.WriteLine(
                $"{evidence.Role} {evidence.Address}:{evidence.Port} " +
                $"classification={evidence.Classification} captures={evidence.Captures:N0} " +
                $"samples={evidence.Samples:N0} byteCounts={string.Join(',', evidence.ByteCounts)} " +
                $"uniquePayloads={evidence.UniqueNonEmptyPayloads:N0} stablePrefixHex={FormatStablePrefix(evidence.StablePrefixHex)} " +
                $"serverFirstBytes={evidence.ServerFirstBytes:N0} evidence={evidence.EvidenceSummary}");
        }

        return 0;
    }

    static string FormatStablePrefix(string stablePrefixHex)
    {
        return string.IsNullOrEmpty(stablePrefixHex) ? "<none>" : stablePrefixHex;
    }

    static int PatchClient(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool patch-client <clientPath> <catalogJsonPath>");
            return 1;
        }
        var clientPath = Path.GetFullPath(args[1]);
        if (!Directory.Exists(clientPath))
        {
            Console.Error.WriteLine($"Client path not found: {clientPath}");
            return 1;
        }
        try
        {
            var catalog = ClientPatchCatalog.Load(args[2]);
            var result = EnglishClientPatch.Apply(clientPath, catalog);
            foreach (var file in result.Files)
            {
                Console.WriteLine($"{file.RelativePath}: applied={file.Applied} missing={file.Missing} changed={file.Changed}");
            }
            Console.WriteLine($"Applied: {result.Applied}; missing: {result.Missing}; changed files: {result.ChangedFiles}");
            return result.Missing == 0 ? 0 : 2;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Could not apply supplied patch catalog: {ex.Message}");
            return 1;
        }
    }

    static int Usage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  WolfConquer.DataTool probe [dbServerPath]");
        Console.WriteLine("  WolfConquer.DataTool export [dbServerPath] [outputPath] [--include-game-map-ex path] [...]");
        Console.WriteLine("  WolfConquer.DataTool export-legacy-game-maps [dbServerPath|GameMap.dat] [outputPath] [--include dbServerPath|GameMap.dat] [...]");
        Console.WriteLine("  WolfConquer.DataTool probe-endpoints [--endpoint Role=address:port] [--timeout-ms milliseconds] [--samples count] [--sample-delay-ms milliseconds] [--max-bytes bytes] [--output-json path]");
        Console.WriteLine("  WolfConquer.DataTool tcp-relay-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath> [--max-bytes N] [--idle-seconds N] [--accept-seconds N]");
        Console.WriteLine("  WolfConquer.DataTool tcp-relay-capture-summary <relayCaptureJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool parity-log-summary <logPathOrDirectory> [--top N]");
        Console.WriteLine("  WolfConquer.DataTool legacy-account-smoke <address> <port> <username> <password> <serverName> [--expect-ip ip] [--expect-port port] [--timeout-ms ms]");
        Console.WriteLine("  WolfConquer.DataTool legacy-guard-shield-smoke <address> <port> [--command text] [--expect-response text] [--timeout-ms ms]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-smoke <address> <port> <accountKey> [--timeout-ms ms]");
        Console.WriteLine("  WolfConquer.DataTool seed-owned-admission-fixture <NEW-absolute-wolf-owned-admission-GUID-dir> <account-key> <character-uid> <item-uid> [--profile RootD11_7899|VisibleDx8D13]");
        Console.WriteLine("  WolfConquer.DataTool send-operator-gear-mail <wolfconquer-operator-mail-domainN> <absolute-request.json>");
        Console.WriteLine("  WolfConquer.DataTool create-operator-test-kit <NEW-absolute-request.json> <domainN> <accountId> <characterUid> <basic-trojan|socket-materials|consumables>");
        Console.WriteLine("  WolfConquer.DataTool create-gm-policy <NEW-absolute-policy.json> <existing-owned-admission.json> <accountId> <characterUid> [inspect|mail|both] (creates inert policy only)");
        Console.WriteLine("  WolfConquer.DataTool inspect-owned-player <absolute-owned-admission.json> <accountId>");
        Console.WriteLine("  WolfConquer.DataTool inspect-runtime-player <wolfconquer-runtime-inspect-domainN> <domainN> <accountId> <characterUid> (diagnostic only)");
        Console.WriteLine("  WolfConquer.DataTool seed-loot-roundtrip-fixture <NEW-absolute-DBServer-dir> <account-key> <character-uid> <item-uid>");
        Console.WriteLine("  WolfConquer.DataTool legacy-loot-roundtrip-smoke <loopback-ip> <port> <account-key> <character-uid> <item-uid> [--timeout-ms ms] [--verify-only]");
        Console.WriteLine("  WolfConquer.DataTool local-live-stack-smoke <address> <gatewayPort> <accountPort> <username> <password> <serverName> [--login-web-base-url url] [--expect-world-ip ip] [--expect-world-port port] [--timeout-ms ms]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-capture <address> <port> <accountKey> <outputPath> [--timeout-ms ms] [--max-frames N] [--sample-bytes N]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-relay-decode <relayCaptureJsonPath> <outputJsonPath> [--max-frames N] [--sample-bytes N]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-active-bridge-capture <listenAddress> <listenPort> <targetAddress> <targetPort> <outputPath> [--max-bytes N] [--max-frames N] [--sample-bytes N] [--idle-seconds N] [--accept-seconds N] [--continue-after-max-frames] [--progress]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-active-bridge-summary <activeBridgeCaptureJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool active-bridge-capture-status [activeBridgeCaptureJsonPath] [clientExePath]");
        Console.WriteLine("  WolfConquer.DataTool legacy-world-capture-compare <expectedJson> <actualJson> [--ignore-indexes csv] [--start index] [--count n] [--actual-offset-start expectedIndex] [--actual-offset delta]");
        Console.WriteLine("  WolfConquer.DataTool analyze-endpoint-probes <endpointProbeJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool compare-endpoint-probes <endpointProbeJsonPath> <endpointProbeJsonPath> [...]");
        Console.WriteLine("  WolfConquer.DataTool protocol-evidence <endpointProbeJsonPath> [endpointProbeJsonPath ...]");
        Console.WriteLine("  WolfConquer.DataTool write-pktmon-capture-script login|world|original-flow [outputPath] [--duration-seconds seconds]");
        Console.WriteLine("  WolfConquer.DataTool pktmon-capture-readiness [rootPath] [--misplaced-root path]");
        Console.WriteLine("  WolfConquer.DataTool pktmon-capture-runbook [rootPath]");
        Console.WriteLine("  WolfConquer.DataTool world-relay-capture-runbook [rootPath]");
        Console.WriteLine("  WolfConquer.DataTool active-bridge-capture-runbook [rootPath]");
        Console.WriteLine("  WolfConquer.DataTool world-relay-capture-preflight [clientExePath]");
        Console.WriteLine("  WolfConquer.DataTool original-flow-capture-preflight [clientExePath]");
        Console.WriteLine("  WolfConquer.DataTool loginweb-loader-readiness [baseUrl] [--timeout-ms ms] [--expected-version-file path] [--expected-manifest-file path]");
        Console.WriteLine("  WolfConquer.DataTool local-world-handoff-status [candidateWorldEndpointOrPort]");
        Console.WriteLine("  WolfConquer.DataTool pktmon-fixture-summary [rootPath|pcapngPath]");
        Console.WriteLine("  WolfConquer.DataTool pktmon-payload-summary [rootPath|pcapngPath]");
        Console.WriteLine("  WolfConquer.DataTool inspect-client-routing [clientPath] [dbServerPath] [--local-address address]");
        Console.WriteLine("  WolfConquer.DataTool snapshot-client-routing [clientPath] [outputPath]");
        Console.WriteLine("  WolfConquer.DataTool client-structural-drift <originalClientPath> <patchedClientPath> <catalogJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool client-file-drift <originalClientPath> <patchedClientPath> [--include-logs] [--patch-files-only <catalogJsonPath>]");
        Console.WriteLine("  WolfConquer.DataTool client-lua-drift-audit <clientRoot> --reference-root <path> [--symbol <text>] [--top N]");
        Console.WriteLine("  WolfConquer.DataTool client-asset-presence-audit <clientRoot> <relativeAssetPath> [relativeAssetPath ...] [--reference label=path] [...] [--require-client]");
        Console.WriteLine("  WolfConquer.DataTool client-overlay-item-ref-audit <clientRoot> <baselineClientRoot|knownItemSourcePath> <relativeSideTablePath> [relativeSideTablePath ...]");
        Console.WriteLine("  WolfConquer.DataTool client-string-ref-audit <clientRoot> <relativeReferenceFile> <relativeResourceFile> [relativeResourceFile ...]");
        Console.WriteLine("  WolfConquer.DataTool client-playtest-audit <clientDebugLogPath> [--since timestamp] [--since-line N] [--server-log path] [...] [--list-all] [--no-fail-on-catch]");
        Console.WriteLine("  WolfConquer.DataTool visible-login-seam-audit --account-log path --gateway-log path --world-log path [--client-log path] [--gateway-since-line n] [--account-since-line n] [--world-since-line n] [--client-since-line n] [--line-markers path]");
        Console.WriteLine("  WolfConquer.DataTool combat-close-evidence --world-log path [--client-log path] [--world-since-line N] [--client-since-line N]");
        Console.WriteLine("  WolfConquer.DataTool monster-spawn-audit [--world-data-root path] [--db-server-root path] [--map mapId] [--json]");
        Console.WriteLine("  WolfConquer.DataTool monster-family-audit [--world-data-root path] [--db-server-root path] [--json]");
        Console.WriteLine("  WolfConquer.DataTool character-automatic-proc-eligibility-audit (--character-uid UID | --character-name NAME | --character-snapshot path) [--users-root path] [--spells-snapshot path] [--items-snapshot path] [--magic-snapshot path | --world-data-root path] [--alternate-equipment]");
        Console.WriteLine("  WolfConquer.DataTool client-launcher-gate-audit <clientRoot> [--top N]");
        Console.WriteLine("  WolfConquer.DataTool client-launcher-safety-audit <clientRoot> [--target-only] [--target-path path] [--evidence-path path] [--live-evidence-path path] [--quarantined-evidence-path path]");
        Console.WriteLine("  WolfConquer.DataTool client-itemtype-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--since timestamp] [--top N]");
        Console.WriteLine("  WolfConquer.DataTool client-texture-warning-owner-audit <clientDebugLogPath> <clientRoot> [--since-line N] [--texture id] [...] [--top N]");
        Console.WriteLine("  WolfConquer.DataTool client-packet-catch-context-audit <clientDebugLogPath> [--since-line N] [--opcode opcode] [...]");
        Console.WriteLine("  WolfConquer.DataTool client-startup-audit <logPath> [--all] [--top N] [--gap-minutes N] [--client-root path] [--reference-root label=path]");
        Console.WriteLine("  WolfConquer.DataTool client-launcher-trace-audit <launcherTraceReportPath> [--allow-external address:port] [...]");
        Console.WriteLine("  WolfConquer.DataTool visible-login-line-markers --account-log path --gateway-log path --world-log path [--client-log path] [--output-json path]");
        Console.WriteLine("  WolfConquer.DataTool client-login-cache-audit <clientRoot>");
        Console.WriteLine("  WolfConquer.DataTool client-launch-evidence [--string text] [--no-default-strings] [--max-string-offsets N] [--max-import-dlls N] <path|label=path> [...]");
        Console.WriteLine("  WolfConquer.DataTool client-native-launch-probe <exePath> [--workdir path] [--arg value] [...] [--timeout-ms N] [--kill-after-ms N] [--kill-on-timeout|--no-kill-on-timeout] [--debug-log path] [...] [--top-threads N] [--ready-file path] [--args arg ...]");
        Console.WriteLine("  WolfConquer.DataTool client-launch-dependency-audit <exePath> --workdir label=path [--workdir label=path ...] [--client-root path] [--max-import-dlls N] [--recursive] [--max-recursive-depth N]");
        Console.WriteLine("  WolfConquer.DataTool client-launch-contract-audit <oldClientRoot> <currentClientRoot> [--old-label label] [--current-label label] [--candidate relativePath] [...] [--workdir [label=]relativePath] [...] [--symbol-dll relativePath] [...] [--string text] [--no-default-strings] [--max-string-offsets N] [--max-import-dlls N]");
        Console.WriteLine("  WolfConquer.DataTool client-pe-symbol-audit [<exePath> --import-dll <dllName>] [--dll <dllPath>] [--compare] [--shim-summary] [--emit-def] [--proxy-module <moduleName>] [--max-imports N] [--max-exports N]");
        Console.WriteLine("  WolfConquer.DataTool client-pe-dependency-audit <absoluteLocalPePath>");
        Console.WriteLine("  " + ClientConfigDecodeUsage);
        Console.WriteLine("  WolfConquer.DataTool client-item-catalog-roundtrip <decoded-source-path> [--output-new <fresh-path>]");
        Console.WriteLine("  WolfConquer.DataTool client-item-catalog-codec <decode|encode> <input-path> [--output-new <fresh-path>]");
        Console.WriteLine("  " + Chinese7937CatalogDecodeUsage);
        Console.WriteLine("  WolfConquer.DataTool client-reference-intake <absolute-reference-root> <expected-version> [--output-new <absolute-findings.md>]");
        Console.WriteLine("  WolfConquer.DataTool client-item-catalog-localize <packed-source> --english-itemtype <67-field-source> [--include-text-fields] [--output-new <fresh-path>]");
        Console.WriteLine("  WolfConquer.DataTool generated-buffer-string-scan [--term text] [--context-bytes N] [--max-hits N] <path|label=path> [...]");
        Console.WriteLine("  WolfConquer.DataTool license-route-capture-compare <captureJsonPath> <captureJsonPath> [captureJsonPath ...]");
        Console.WriteLine("  WolfConquer.DataTool license-route-relay-compare <relayCaptureJsonPath> [relayCaptureJsonPath ...]");
        Console.WriteLine("  WolfConquer.DataTool license-route-relay-correlation <relayCaptureJsonPath> [relayCaptureJsonPath ...]");
        Console.WriteLine("  WolfConquer.DataTool cached-client-thread-evidence-map <patchResultJsonPath> [--probe-json patchResultJsonPath] [--module DataThread.dll] [--license-module license.dll]");
        Console.WriteLine("  WolfConquer.DataTool cached-client-patch-result-summary <patchResultJsonPath> [patchResultJsonPath ...]");
        Console.WriteLine("  WolfConquer.DataTool cached-client-branch-frontier-summary <patchResultJsonPath> [patchResultJsonPath ...] [--output-json path]");
        Console.WriteLine("  WolfConquer.DataTool cached-client-module-probe-compare <leftPatchResultJsonPath> <rightPatchResultJsonPath> [--left-field ModuleMemoryProbesBeforeRelayResponse|ModuleMemoryProbesAfterObserve] [--right-field ModuleMemoryProbesBeforeRelayResponse|ModuleMemoryProbesAfterObserve]");
        Console.WriteLine("  WolfConquer.DataTool raw-module-branch-map <rawModulePath> --rva hexRva [--rva hexRva ...] [--length N] [--module-name name] [--base hexAddress] [--follow-depth N] [--summary-only]");
        Console.WriteLine("  WolfConquer.DataTool raw-module-branch-patch-spec <rawModulePath> --module-name name --source-rva hexRva [--target-rva hexRva] --bytes N --name patchName [--mode short-jmp|near-jmp|near-jmp-pad|no-op]");
        Console.WriteLine("  WolfConquer.DataTool raw-module-branch-spine <rawModulePath> --rva hexRva [--length N] [--module-name name] [--base hexAddress] [--steps N] [--prelude-bytes N]");
        Console.WriteLine("  WolfConquer.DataTool computed-pointer-scan --target name=hexbytes [--target name=hexbytes ...] [--context-bytes N] <path> [path ...]");
        Console.WriteLine("  WolfConquer.DataTool minidump-module-carve <dumpPath> <moduleName> <outputPath> [--fill hexByte]");
        Console.WriteLine("  WolfConquer.DataTool patch-client <clientPath> <catalogJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool patch-client-emoney-shop-v2 [clientPath] [itemtype.txt|DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-emoney-shop-v2-source-backed [clientPath] [sourceFileOrDirectory ...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-cn-res-source-backed [clientPath] [sourceFileOrDirectory ...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-server-key-items [clientPath] [itemtype.txt|DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-server-key-resources [clientPath] [DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-magic-effect [clientPath] [MagicEffect.ini|DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-magic-effect-source-backed [clientPath] [MagicEffect.ini|magictype.txt|DBServerPath] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-quest-info [clientPath] [Questinfo.ini|DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-quest-info-passion [clientPath] [Questinfo.ini|DBServerPath] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-quest-info-random-source-backed [clientPath] [QuestinfoRandom.lua|Questinfo.ini|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-quest-info-random-passion-source-backed [clientPath] [QuestinfoRandomPassion.lua|Questinfo.ini|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-game-map-ex [clientPath] [GameMapEx.ini|DBServerPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-rune-desc [clientPath] [Server_Key.lua|clientPath] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-gouyu-skill-text [clientPath] [MagicEffect.ini|magictype.txt|DBServerPath] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-limit-shop-source-backed [clientPath] [itemtype.txt|Server_Key.lua|LimitShop.lua|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-item-ui-source-backed [clientPath] [itemtype.txt|Server_Key.lua|ItemQuenchTipInfo.ini|Storage.ini|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-status-text-source-backed [clientPath] [magictype.txt|MagicEffect.ini|StatusTips.ini|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-monster-say-templates [clientPath]");
        Console.WriteLine("  WolfConquer.DataTool patch-client-str-res-source-backed [clientPath] [StrRes.ini|sourceDirectory] [...]");
        Console.WriteLine("  WolfConquer.DataTool patch-db-shop-names [DBServerPath|Shop.dat] [clientPath|Shop.dat]");
        Console.WriteLine("  WolfConquer.DataTool patch-db-sob-npc-names [DBServerPath|SobNpcs.txt] [DBServerPath|SobNpcs.txt.bak]");
        Console.WriteLine("  WolfConquer.DataTool patch-db-visible-text <DBServerPath> <catalogJsonPath>");
        Console.WriteLine("  WolfConquer.DataTool patch-db-quest-info-mojibake <DBServerPath|Questinfo.ini>");
        Console.WriteLine("  WolfConquer.DataTool scan-client-text-files [clientPath]");
        Console.WriteLine("  WolfConquer.DataTool scan-cjk-text-debt [rootPath] [--top N]");
        return 0;
    }


}

internal sealed class ImportProbeResult
{
    public int Failures { get; private set; }

    public void RecordFailure(string message)
    {
        Failures++;
        Console.Error.WriteLine(message);
    }
}

internal sealed record ItemSeed(int Id, string Name, IReadOnlyList<string> Fields);

internal sealed record MagicSeed(int Id, int TypeId, string Name, IReadOnlyList<string> Fields);

internal sealed record ParityLogSummaryOptions(string Path, int TopCount);

internal sealed record ParityLogSummaryReport(
    string Path,
    int TopCount,
    int ScannedFileCount,
    long MatchingLineCount,
    IReadOnlyDictionary<string, long> CategoryCounts,
    IReadOnlyDictionary<string, long> PrefixCounts);

internal sealed record ParityLogMarker(string Category, IReadOnlyList<string> Aliases);

internal static class ParityLogMarkerDefinitions
{
    public static readonly IReadOnlyList<ParityLogMarker> All =
    [
        new("PARITY-GAP", ["PARITY-GAP"]),
        new("PARITY-BUG", ["PARITY-BUG"]),
        new("deferred", ["deferred"]),
        new("skipped", ["skipped"]),
        new("malformed", ["malformed"]),
        new("rejected", ["rejected"]),
        new("exception", ["exception"]),
        new("not modeled", ["not modeled", "not-modeled"]),
        new("not loaded", ["not loaded", "not-loaded"]),
        new("not working", ["not working", "not-working"]),
        new("unhandled click", ["unhandled click", "unhandled-click"]),
        new("freeze", ["freeze", "frozen"]),
    ];
}

internal sealed record PktmonCaptureSpec(
    string Kind,
    string FilterName,
    int Port,
    int PacketSize,
    int FileSizeMb,
    string FileStem,
    string Prompt)
{
    public string OutputPath { get; init; } = "";

    public int? DurationSeconds { get; init; }

    public IReadOnlyList<PktmonCaptureFilter> AdditionalFilters { get; init; } = [];
}

internal sealed record PktmonCaptureFilter(string FilterName, int Port);

internal sealed record PktmonCaptureReadinessOptions(
    string RootPath,
    string? MisplacedRootPath);

internal sealed record PktmonCaptureReadinessReport(
    string RootPath,
    IReadOnlyList<PktmonCaptureReadinessItem> Items,
    IReadOnlyList<PktmonMisplacedCaptureItem> MisplacedItems)
{
    public bool Ready => ReadyMode != "missing";

    public string ReadyMode
    {
        get
        {
            if (ReadyItemExists("original-flow"))
            {
                return "original-flow";
            }

            if (ReadyItemExists("login") && ReadyItemExists("world"))
            {
                return "login-world";
            }

            return "missing";
        }
    }

    public string NextAction
    {
        get
        {
            if (Ready)
            {
                return "none";
            }

            var partialOriginalFlow = Items.FirstOrDefault(item =>
                string.Equals(item.Kind, "original-flow", StringComparison.OrdinalIgnoreCase) &&
                item.ScriptReady &&
                item.PcapngReady &&
                !item.HasRequiredServicePayloads);
            if (partialOriginalFlow is not null)
            {
                var missingPorts = string.Join(",", partialOriginalFlow.MissingServicePayloadPorts);
                var observedPorts = partialOriginalFlow.ServicePayloadPorts.Count == 0
                    ? "<none>"
                    : string.Join(",", partialOriginalFlow.ServicePayloadPorts);
                return
                    "Rerun the combined original-flow pktmon script while performing one normal client " +
                    $"loader/login/world-entry flow; existing original-flow capture is missing TCP payloads " +
                    $"for service ports {missingPorts} (observed service ports: {observedPorts}).";
            }

            return "Run the combined original-flow pktmon script from an elevated PowerShell, then perform one normal client loader/login/world-entry flow.";
        }
    }

    private bool ReadyItemExists(string kind)
    {
        return Items.Any(item =>
            string.Equals(item.Kind, kind, StringComparison.OrdinalIgnoreCase) &&
            item.ScriptReady &&
            item.PcapngReady &&
            item.HasRequiredServicePayloads);
    }
}

internal sealed record PktmonMisplacedCaptureItem(
    string Kind,
    int Port,
    string RootPath,
    PcapngFileSummary Pcapng);

internal sealed record PktmonCaptureReadinessItem(
    string Kind,
    int Port,
    string ScriptPath,
    bool ScriptExists,
    string TimedScriptPath,
    bool TimedScriptExists,
    string PcapngPath,
    PcapngFileSummary Pcapng,
    IReadOnlyList<int> ServicePayloadPorts)
{
    public bool ScriptReady => ScriptExists || TimedScriptExists;

    public bool PcapngExists => Pcapng.Exists;

    public bool PcapngReady => Pcapng.Ready;

    public IReadOnlyList<int> RequiredServicePayloadPorts => Kind switch
    {
        "login" => [9960],
        "world" => [5818],
        "original-flow" => [80, 9960, 5818],
        _ => [],
    };

    public IReadOnlyList<int> MissingServicePayloadPorts => RequiredServicePayloadPorts
        .Where(port => !HasServicePayload(port))
        .ToArray();

    public bool HasRequiredServicePayloads => RequiredServicePayloadPorts.Count > 0
        ? MissingServicePayloadPorts.Count == 0
        : ServicePayloadPorts.Count > 0;

    private bool HasServicePayload(int port)
    {
        return ServicePayloadPorts.Contains(port);
    }
}

internal sealed record PktmonFixtureSummaryReport(
    string? RootPath,
    IReadOnlyList<PktmonFixtureSummaryItem> Items)
{
    public bool Ready => Items.All(item => item.Pcapng.Ready);
}

internal sealed record PktmonFixtureSummaryItem(
    string Kind,
    int? Port,
    PcapngFileSummary Pcapng);

internal sealed record PktmonPayloadSummaryReport(
    string? RootPath,
    IReadOnlyList<PktmonPayloadSummaryItem> Items)
{
    public bool Ready => Items.All(item => item.Payload.Pcapng.Ready);
}

internal sealed record PktmonPayloadSummaryItem(
    string Kind,
    int? Port,
    IReadOnlyList<int> WatchedPorts,
    PcapngPayloadFileSummary Payload);

internal sealed record PcapngPayloadFileSummary(
    PcapngFileSummary Pcapng,
    IReadOnlyList<TcpPayloadCandidate> Candidates);

internal sealed record TcpPayloadCandidate(
    string BlockKind,
    int PacketBlockIndex,
    int PacketLength,
    int SourcePort,
    int DestinationPort,
    int PayloadLength,
    string FirstHex,
    string FirstBase64);

internal sealed record TcpPayloadServicePortCount(int Port, int Count);

internal sealed record PcapngFileSummary(
    string Path,
    bool Exists,
    long Length,
    string Sha256,
    bool HeaderValid,
    string Version,
    string ByteOrder,
    int SectionHeaderBlocks,
    int InterfaceDescriptionBlocks,
    int EnhancedPacketBlocks,
    int SimplePacketBlocks,
    int TotalBlocks,
    string Validation)
{
    public int PacketBlocks => EnhancedPacketBlocks + SimplePacketBlocks;

    public bool Ready => Exists && HeaderValid && PacketBlocks > 0;
}

internal sealed record PcapngBlockValidation(
    bool HeaderValid,
    string Version,
    string ByteOrder,
    int SectionHeaderBlocks,
    int InterfaceDescriptionBlocks,
    int EnhancedPacketBlocks,
    int SimplePacketBlocks,
    int TotalBlocks,
    string Validation);

internal sealed record NpcSeed(
    int Id,
    int Kind,
    int Lookface,
    int MapId,
    int X,
    int Y,
    int? OptionalFlag,
    int SourceLine,
    string RawLine,
    IReadOnlyList<string>? Fields);

internal sealed record PortalSeed(int SourceMapId, int SourceX, int SourceY, int TargetMapId, int TargetX, int TargetY);

internal sealed record MonsterSeed(
    int Id,
    string Name,
    int? Level,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<SpecialDropSeed> SpecialDrops);

internal sealed record MonsterSpawnSeed(
    int GeneratorId,
    int MapId,
    int MonsterTypeId,
    int BoundX,
    int BoundY,
    int BoundWidth,
    int BoundHeight,
    int MaxNpc,
    int RestSeconds,
    int MaxPerGenerator,
    int TimerBegin,
    int TimerEnd,
    int BornX,
    int BornY,
    int? SourceMapFolderId,
    string? SourceMonsterName,
    string SourceRelativePath,
    IReadOnlyDictionary<string, string> Attributes);

internal sealed record SpecialDropSeed(int ItemTypeId, int ChanceOrCount);
