using System.Reflection;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Tests;

// Invoked only by the existing fresh Windows child. No Assert, xUnit service,
// accepting sender, socket, SQL, or worker is used in this compiled entry.
internal static class LegacyNativeQuestAcceptanceDeliveryChildScenarios
{
    public static string Execute(string scenario, string corePath, string testPath)
    {
        Require(OperatingSystem.IsWindows(), "Actual Windows standard-handle APIs are required.");
        Require(SamePath(typeof(LegacyNativeQuestsRuntime).Assembly.Location, corePath) &&
            SamePath(typeof(LegacyNativeQuestAcceptanceDeliveryChildScenarios).Assembly.Location, testPath),
            "The compiled scenario must use the current explicitly supplied assemblies.");
        bool kingdom = scenario switch
        {
            "QuestAcceptanceOrdinaryDeliveryDiagnosticFault" => false,
            "QuestAcceptanceKingdomDeliveryDiagnosticFault" => true,
            _ => throw new InvalidOperationException("Unknown quest delivery child scenario."),
        };

        // This is the same child-only cached StartupPath binding used by the
        // existing native queue scenarios. Even an unexpected logging call may
        // touch only this owned empty directory, never the pwsh installation.
        var startup = typeof(LegacyNativeApplication).GetField("startupPath",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var priorStartup = startup.GetValue(null);
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        string logs = Path.Combine(temporary, "wolf-native-quest-delivery-" + Guid.NewGuid().ToString("N"));
        Require(SamePath(Path.GetDirectoryName(logs)!, temporary), "Owned log root escaped temp.");
        RequireOrdinaryChain(temporary);
        Directory.CreateDirectory(logs);
        RequireOrdinaryChain(logs);
        startup.SetValue(null, logs);
        try
        {
            bool returned = false, handlesRestored = false, diagnosticFault = false, noExceptionFiles = false;
            Exception? acceptFailure = null;
            Row? outRow = null;
            var client = new LegacyNativeGameClientRuntime();
            var player = client.Player;
            player.UID = 17;
            var book = new LegacyNativeQuestsRuntime(player);
            player.QuestGUI = book;
            var oldSource = book.src;
            var oldAccepted = book.AcceptedQuests;
            var definition = new LegacyNativeQuestInfo.DBQuest
            { MissionId = kingdom ? (ushort)35024 : (ushort)600, Intentions = 3 };
            Require(client.Socket is null && !client.Fake, "The real missing-Socket send path is required.");
            var input = LegacyNativeConsoleKernel32.GetStdHandle(-10);
            var output = LegacyNativeConsoleKernel32.GetStdHandle(-11);
            var error = LegacyNativeConsoleKernel32.GetStdHandle(-12);
            Require(input != IntPtr.Zero && input.ToInt64() != -1 &&
                output != IntPtr.Zero && output.ToInt64() != -1 &&
                error != IntPtr.Zero && error.ToInt64() != -1, "Actual redirected child handles are required.");
            try
            {
                // Before the first actual NativeConsole member invocation. Its
                // explicit initializer sees this -1 and faults before SaveException.
                Require(LegacyNativeConsoleKernel32.SetStdHandle(-11, new IntPtr(-1)),
                    "Could not set the owned child's OS output slot.");
                Require(LegacyNativeConsoleKernel32.GetStdHandle(-11).ToInt64() == -1,
                    "The owned output slot did not retain the invalid handle.");
                try
                {
                    returned = kingdom
                        ? book.AcceptKingDomMission(definition, 0x12345678, out outRow!)
                        : book.Accept(definition, 0x12345678);
                }
                catch (Exception exception) { acceptFailure = exception; }
            }
            finally
            {
                // Always restore ALL slots before any result marker or diagnostics.
                bool restoredInput = LegacyNativeConsoleKernel32.SetStdHandle(-10, input);
                bool restoredOutput = LegacyNativeConsoleKernel32.SetStdHandle(-11, output);
                bool restoredError = LegacyNativeConsoleKernel32.SetStdHandle(-12, error);
                handlesRestored = restoredInput && restoredOutput && restoredError &&
                    LegacyNativeConsoleKernel32.GetStdHandle(-10) == input &&
                    LegacyNativeConsoleKernel32.GetStdHandle(-11) == output &&
                    LegacyNativeConsoleKernel32.GetStdHandle(-12) == error;
                Require(handlesRestored, "Owned child OS handles were not restored.");
            }

            // If the genuine Send never reached its diagnostic call, this first
            // invocation would initialize successfully after handle restoration.
            // Cached type failure therefore proves the fault ran during Send.
            try { _ = LegacyNativeConsole.AllowSource(); }
            catch (TypeInitializationException exception)
            {
                diagnosticFault = exception.TypeName == typeof(LegacyNativeConsole).FullName &&
                    exception.GetBaseException().Message == "Unable to get the Console Handle";
            }
            noExceptionFiles = Directory.GetFileSystemEntries(logs).Length == 0;
            Require(diagnosticFault, "The actual native diagnostic initializer was not faulted by Send.");
            Require(noExceptionFiles, "A supposedly pre-body diagnostic fault wrote exception files.");

            book.src.TryGetValue(definition.MissionId, out var row);
            bool sourceCommitted = row is not null && book.src.Count == 1 && !ReferenceEquals(book.src, oldSource);
            bool acceptedCommitted = kingdom
                ? ReferenceEquals(book.AcceptedQuests, oldAccepted) && book.AcceptedQuests.Count == 0
                : row is not null && book.AcceptedQuests.Count == 1 &&
                    !ReferenceEquals(book.AcceptedQuests, oldAccepted) &&
                    ReferenceEquals(book.AcceptedQuests[definition.MissionId], row);
            return JsonSerializer.Serialize(new
            {
                Returned = returned,
                AcceptException = acceptFailure?.GetType().FullName ?? string.Empty,
                AcceptExceptionRootMessage = acceptFailure?.GetBaseException().Message ?? string.Empty,
                DiagnosticFaultVerified = diagnosticFault,
                HandlesRestored = handlesRestored,
                NoExceptionFiles = noExceptionFiles,
                SourceCommitted = sourceCommitted,
                AcceptedIndexCommitted = acceptedCommitted,
                CanonicalOut = !kingdom || ReferenceEquals(outRow, row),
                OriginalMapsUnchanged = oldSource.Count == 0 && oldAccepted.Count == 0,
                ExactRow = row is not null && row.UID == definition.MissionId &&
                    row.Status == Row.QuestStatus.Accepted && row.Time == 0x12345678 &&
                    row.Intentions.Length == 3 && row.Intentions.All(value => value == 0),
                MetadataUnchanged = definition.Intentions == 3 &&
                    definition.MissionId == (kingdom ? 35024 : 600),
                SocketMissing = client.Socket is null,
            });
        }
        finally
        {
            startup.SetValue(null, priorStartup);
            RequireOrdinaryChain(logs);
            // Unexpected files are retained for diagnosis. Never recursively
            // erase the evidence or follow descendants during this child cleanup.
            if (Directory.GetFileSystemEntries(logs).Length == 0) Directory.Delete(logs, false);
        }
    }

    private static bool SamePath(string left, string right) => string.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void RequireOrdinaryChain(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "Owned child path contains a reparse point.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
