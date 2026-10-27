using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

internal static partial class Program
{
    private const int ClientNativeRuntimeInstructionByteSampleLength = 16;

    private static StringComparer ClientNativeRuntimePathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static ClientNativeRuntimeLaunchReport RunClientNativeRuntimeSnapshot(
        ClientNativeLaunchProbeOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var snapshots = new List<ClientNativeRuntimeSnapshot>();
        var observedProcessIds = new HashSet<int>();
        var debugLogBaselines = CaptureClientNativeRuntimeDebugLogBaselines(options.DebugLogPaths);
        Process? process = null;
        var startSucceeded = false;
        var nativeError = string.Empty;
        var killError = string.Empty;
        int? processId = null;
        int? exitCode = null;
        var exited = false;
        var timedOut = false;
        var killAttempted = false;
        var killed = false;
        var readiness = PendingClientNativeLaunchReadiness(options);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = options.ExecutablePath,
                WorkingDirectory = options.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            foreach (var argument in options.Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            ValidateClientNativeLaunchReadinessPath(options);
            process = Process.Start(startInfo);
            if (process is null)
            {
                nativeError = "Process.Start returned null.";
            }
            else
            {
                _ = process.SafeHandle; // Preserve the actual Process.Start identity before observing descendants.
                startSucceeded = true;
                processId = process.Id;
                observedProcessIds.Add(process.Id);
                snapshots.Add(CaptureClientNativeRuntimeSnapshot(
                    "after-start",
                    stopwatch.ElapsedMilliseconds,
                    process,
                    observedProcessIds,
                    debugLogBaselines,
                    options.TopThreadCount));

                readiness = WaitForClientNativeLaunchReadiness(options, process, stopwatch);
                if (readiness.Requested && readiness.Observed && readiness.Error.Length == 0)
                {
                    readiness = ResolveClientNativeLaunchReadyWindow(options, stopwatch.ElapsedMilliseconds);
                }

                var waitMilliseconds = readiness.WaitMilliseconds;
                timedOut = readiness.TimedOut;
                if (process.WaitForExit(waitMilliseconds))
                {
                    exited = true;
                    exitCode = process.ExitCode;
                    var exitSnapshot = CaptureClientNativeRuntimeSnapshot(
                        "after-exit",
                        stopwatch.ElapsedMilliseconds,
                        process,
                        observedProcessIds,
                        debugLogBaselines,
                        options.TopThreadCount);
                    snapshots.Add(exitSnapshot);

                    if ((options.KillAfterMs is not null ||
                        (readiness.Requested && !readiness.Observed && options.KillOnTimeout)) &&
                        exitSnapshot.Processes.Any(process => process.Exists))
                    {
                        killAttempted = true;
                        TryKillClientNativeRuntimeProcessTree(
                            process,
                            observedProcessIds,
                            out killed,
                            out exitCode,
                            out killError);
                        snapshots.Add(CaptureClientNativeRuntimeSnapshot(
                            "after-kill",
                            stopwatch.ElapsedMilliseconds,
                            process,
                            observedProcessIds,
                            debugLogBaselines,
                            options.TopThreadCount));
                    }
                }
                else
                {
                    timedOut = readiness.TimedOut ||
                        (options.KillAfterMs is null && (!readiness.Requested || readiness.Observed));
                    snapshots.Add(CaptureClientNativeRuntimeSnapshot(
                        readiness.Requested && (!readiness.Observed || readiness.Error.Length != 0)
                            ? "readiness-before-kill"
                            : options.KillAfterMs is null ? "timeout-before-kill" : "before-kill-after-bound",
                        stopwatch.ElapsedMilliseconds,
                        process,
                        observedProcessIds,
                        debugLogBaselines,
                        options.TopThreadCount));

                    if (options.KillAfterMs is not null || options.KillOnTimeout)
                    {
                        killAttempted = true;
                        TryKillClientNativeRuntimeProcessTree(
                            process,
                            observedProcessIds,
                            out killed,
                            out exitCode,
                            out killError);
                        snapshots.Add(CaptureClientNativeRuntimeSnapshot(
                            "after-kill",
                            stopwatch.ElapsedMilliseconds,
                            process,
                            observedProcessIds,
                            debugLogBaselines,
                            options.TopThreadCount));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            nativeError = FormatClientNativeLaunchProbeException(ex);
            if (readiness.Requested)
            {
                readiness = readiness with { Error = nativeError, WaitMilliseconds = 0 };
                if (startSucceeded && process is not null &&
                    (options.KillAfterMs is not null || options.KillOnTimeout))
                {
                    killAttempted = true;
                    TryKillClientNativeRuntimeProcessTree(
                        process,
                        observedProcessIds,
                        out killed,
                        out exitCode,
                        out killError);
                }
            }
        }
        finally
        {
            stopwatch.Stop();
            process?.Dispose();
        }

        return new ClientNativeRuntimeLaunchReport(
            startSucceeded,
            processId,
            nativeError,
            exited,
            exitCode,
            timedOut,
            killAttempted,
            killed,
            killError,
            stopwatch.ElapsedMilliseconds,
            snapshots,
            readiness);
    }

    private static ClientNativeRuntimeSnapshot CaptureClientNativeRuntimeSnapshot(
        string reason,
        long elapsedMilliseconds,
        Process rootProcess,
        HashSet<int> observedProcessIds,
        IReadOnlyList<ClientNativeRuntimeDebugLogBaseline> debugLogBaselines,
        int topThreadCount)
    {
        var rootProcessId = rootProcess.Id;
        var processIds = EnumerateClientNativeRuntimeProcessTreeIds(rootProcessId, observedProcessIds);
        foreach (var processId in processIds)
        {
            observedProcessIds.Add(processId);
        }

        var processSnapshots = processIds
            .Select(processId => CaptureClientNativeRuntimeProcessSnapshot(
                processId,
                rootProcessId,
                rootProcess,
                topThreadCount))
            .ToArray();
        var liveProcessIds = processSnapshots
            .Where(process => process.Exists)
            .Select(process => process.ProcessId)
            .ToHashSet();
        var tcpConnections = Array.Empty<WorldRelayCaptureTcpConnection>();
        var tcpError = string.Empty;

        if (liveProcessIds.Count > 0)
        {
            try
            {
                tcpConnections = EnumerateWindowsIPv4TcpConnections()
                    .Where(connection => liveProcessIds.Contains(connection.OwningProcessId))
                    .OrderBy(connection => connection.OwningProcessId)
                    .ThenBy(connection => connection.State, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(connection => connection.RemoteAddress, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(connection => connection.RemotePort)
                    .ThenBy(connection => connection.LocalPort)
                    .ToArray();
            }
            catch (Exception ex) when (ex is Win32Exception or PlatformNotSupportedException)
            {
                tcpError = FormatClientNativeLaunchProbeException(ex);
            }
        }

        return new ClientNativeRuntimeSnapshot(
            reason,
            elapsedMilliseconds,
            processSnapshots.Any(process => process.ProcessId == rootProcessId && process.Exists),
            processSnapshots,
            tcpConnections,
            tcpError,
            CaptureClientNativeRuntimeDebugLogs(debugLogBaselines));
    }

    private static IReadOnlyList<int> EnumerateClientNativeRuntimeProcessTreeIds(
        int rootProcessId,
        IReadOnlySet<int> observedProcessIds)
    {
        var ordered = new List<int>();
        var queued = new HashSet<int>();
        var queue = new Queue<int>();

        void Enqueue(int processId)
        {
            if (processId <= 0 || !queued.Add(processId))
            {
                return;
            }

            ordered.Add(processId);
            queue.Enqueue(processId);
        }

        Enqueue(rootProcessId);
        foreach (var processId in observedProcessIds.OrderBy(processId => processId))
        {
            Enqueue(processId);
        }

        IReadOnlyList<ClientNativeRuntimeProcessEntry> entries;
        try
        {
            entries = EnumerateClientNativeRuntimeProcessEntries();
        }
        catch (Exception ex) when (
            ex is Win32Exception or
                PlatformNotSupportedException or
                InvalidOperationException)
        {
            return ordered;
        }

        var childrenByParent = entries
            .GroupBy(entry => entry.ParentProcessId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(entry => entry.ProcessId)
                    .OrderBy(processId => processId)
                    .ToArray());

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                continue;
            }

            foreach (var childProcessId in children)
            {
                Enqueue(childProcessId);
            }
        }

        return ordered;
    }

    private static IReadOnlyList<ClientNativeRuntimeProcessEntry> EnumerateClientNativeRuntimeProcessEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows process snapshots are required.");
        }

        var snapshot = ClientNativeRuntimeNativeMethods.CreateToolhelp32Snapshot(
            ClientNativeRuntimeNativeMethods.Th32csSnapprocess,
            0);
        if (snapshot == ClientNativeRuntimeNativeMethods.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var entry = new ClientNativeRuntimeNativeMethods.ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<ClientNativeRuntimeNativeMethods.ProcessEntry32>(),
            };
            var entries = new List<ClientNativeRuntimeProcessEntry>();
            if (!ClientNativeRuntimeNativeMethods.Process32First(snapshot, ref entry))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ClientNativeRuntimeNativeMethods.ErrorNoMoreFiles)
                {
                    return entries;
                }

                throw new Win32Exception(error);
            }

            do
            {
                entries.Add(new ClientNativeRuntimeProcessEntry(
                    unchecked((int)entry.ProcessId),
                    unchecked((int)entry.ParentProcessId)));
                entry.Size = (uint)Marshal.SizeOf<ClientNativeRuntimeNativeMethods.ProcessEntry32>();
            }
            while (ClientNativeRuntimeNativeMethods.Process32Next(snapshot, ref entry));

            var lastError = Marshal.GetLastWin32Error();
            if (lastError != ClientNativeRuntimeNativeMethods.ErrorNoMoreFiles)
            {
                throw new Win32Exception(lastError);
            }

            return entries;
        }
        finally
        {
            ClientNativeRuntimeNativeMethods.CloseHandle(snapshot);
        }
    }

    private static ClientNativeRuntimeProcessSnapshot CaptureClientNativeRuntimeProcessSnapshot(
        int processId,
        int rootProcessId,
        Process rootProcess,
        int topThreadCount)
    {
        Process? process = null;
        var disposeProcess = false;
        try
        {
            if (processId == rootProcessId)
            {
                if (ClientNativeRuntimeHasExited(rootProcess))
                {
                    return ClientNativeRuntimeProcessSnapshot.Exited(processId);
                }

                process = rootProcess;
            }
            else
            {
                process = Process.GetProcessById(processId);
                disposeProcess = true;
                if (ClientNativeRuntimeHasExited(process))
                {
                    return ClientNativeRuntimeProcessSnapshot.Exited(processId);
                }
            }

            process.Refresh();
            var modules = CaptureClientNativeRuntimeModules(process);
            var threads = CaptureClientNativeRuntimeThreads(process, modules.Modules, topThreadCount);
            var responding = TryGetClientNativeRuntimeResponding(process);
            return new ClientNativeRuntimeProcessSnapshot(
                processId,
                true,
                TryGetClientNativeRuntimeProcessName(process),
                TryGetProcessExecutablePath(process) ?? string.Empty,
                responding.Responding,
                responding.Error,
                TryGetClientNativeRuntimeMainWindowHandle(process),
                TryGetClientNativeRuntimeMainWindowTitle(process),
                modules.ModuleNames,
                modules.Error,
                threads.Threads,
                threads.Error);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
                InvalidOperationException or
                Win32Exception or
                NotSupportedException)
        {
            return ClientNativeRuntimeProcessSnapshot.Unreadable(
                processId,
                FormatClientNativeLaunchProbeException(ex));
        }
        finally
        {
            if (disposeProcess)
            {
                process?.Dispose();
            }
        }
    }

    private static void TryKillClientNativeRuntimeProcessTree(
        Process rootProcess,
        IReadOnlySet<int> observedProcessIds,
        out bool killed,
        out int? exitCode,
        out string killError)
    {
        killed = false;
        exitCode = null;
        var errors = new List<string>();
        ClientNativeCleanupCapture? capture = null;
        try
        {
            capture = ClientNativeCleanupCapture.Prepare(rootProcess);
            errors.AddRange(capture.Errors);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or PlatformNotSupportedException)
        {
            errors.Add("Cleanup identity capture refused: " + FormatClientNativeLaunchProbeException(ex));
        }
        using (capture)
        {
            // Historical integer observations confer no termination authority.
            // All handles were captured and attributed before the first kill.
            if (capture is not null)
            {
                foreach (var processId in observedProcessIds.Where(id => !capture.ProcessIds.Contains(id)))
                    errors.Add($"Unproven historical descendant pid={processId}; not terminated.");
                foreach (var owned in capture.Processes.Reverse())
                {
                    try
                    {
                        if (ClientNativeRuntimeHasExited(owned)) continue;
                        owned.Kill(entireProcessTree: false);
                        if (!owned.WaitForExit(owned.Id == rootProcess.Id ? 3000 : 1000))
                            errors.Add($"Owned cleanup did not observe exit pid={owned.Id}.");
                        killed = true;
                    }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
                    { errors.Add($"pid={owned.Id} {FormatClientNativeLaunchProbeException(ex)}"); }
                }
            }
        }

        if (ClientNativeRuntimeHasExited(rootProcess))
        {
            try
            {
                exitCode = rootProcess.ExitCode;
            }
            catch (InvalidOperationException)
            {
                exitCode = null;
            }
        }

        killError = string.Join("; ", errors.Distinct(StringComparer.Ordinal));
    }

    private static bool ClientNativeRuntimeHasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static string TryGetClientNativeRuntimeProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return "<unknown>";
        }
    }

    private static (bool? Responding, string Error) TryGetClientNativeRuntimeResponding(Process process)
    {
        try
        {
            return (process.Responding, string.Empty);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
        {
            return (null, FormatClientNativeLaunchProbeException(ex));
        }
    }

    private static IntPtr TryGetClientNativeRuntimeMainWindowHandle(Process process)
    {
        try
        {
            return process.MainWindowHandle;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return IntPtr.Zero;
        }
    }

    private static string TryGetClientNativeRuntimeMainWindowTitle(Process process)
    {
        try
        {
            return process.MainWindowTitle;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return string.Empty;
        }
    }

    private static ClientNativeRuntimeModuleSnapshot CaptureClientNativeRuntimeModules(Process process)
    {
        try
        {
            var moduleNames = new List<string>();
            var moduleInfos = new List<ClientNativeRuntimeModuleInfo>();
            foreach (ProcessModule module in process.Modules)
            {
                var moduleName = Path.GetFileName(module.ModuleName);
                if (!string.IsNullOrWhiteSpace(moduleName))
                {
                    moduleNames.Add(moduleName);

                    var baseAddress = ToClientNativeRuntimeAddress(module.BaseAddress);
                    if (baseAddress != 0 && module.ModuleMemorySize > 0)
                    {
                        moduleInfos.Add(new ClientNativeRuntimeModuleInfo(
                            moduleName,
                            baseAddress,
                            AddClientNativeRuntimeAddressOffset(baseAddress, module.ModuleMemorySize)));
                    }
                }
            }

            return new ClientNativeRuntimeModuleSnapshot(
                moduleNames
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(moduleName => moduleName, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                moduleInfos
                    .OrderBy(module => module.BaseAddress)
                    .ToArray(),
                string.Empty);
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
        {
            return new ClientNativeRuntimeModuleSnapshot([], [], FormatClientNativeLaunchProbeException(ex));
        }
    }

    private static ulong ToClientNativeRuntimeAddress(IntPtr pointer) =>
        unchecked((ulong)pointer.ToInt64());

    private static ulong AddClientNativeRuntimeAddressOffset(ulong address, int offset)
    {
        var unsignedOffset = unchecked((ulong)offset);
        return ulong.MaxValue - address < unsignedOffset
            ? ulong.MaxValue
            : address + unsignedOffset;
    }

    private static ClientNativeRuntimeThreadListSnapshot CaptureClientNativeRuntimeThreads(
        Process process,
        IReadOnlyList<ClientNativeRuntimeModuleInfo> modules,
        int topThreadCount)
    {
        if (topThreadCount == 0)
        {
            return new ClientNativeRuntimeThreadListSnapshot([], string.Empty);
        }

        try
        {
            var architecture = CaptureClientNativeRuntimeProcessArchitecture(process);
            var processReadHandle = TryOpenClientNativeRuntimeProcessReadHandle(process.Id, out var processReadError);
            var threads = new List<ClientNativeRuntimeThreadCandidate>();
            try
            {
                foreach (ProcessThread thread in process.Threads)
                {
                    try
                    {
                        var state = thread.ThreadState.ToString();
                        threads.Add(new ClientNativeRuntimeThreadCandidate(
                            thread.Id,
                            Math.Max(0, (long)Math.Round(thread.TotalProcessorTime.TotalMilliseconds)),
                            state,
                            TryGetClientNativeRuntimeThreadWaitReason(thread, state)));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                    {
                    }
                }

                return new ClientNativeRuntimeThreadListSnapshot(
                    threads
                        .OrderByDescending(thread => thread.TotalProcessorTimeMs)
                        .ThenBy(thread => thread.ThreadId)
                        .Take(topThreadCount)
                        .Select(thread => CaptureClientNativeRuntimeThreadSnapshot(
                            thread,
                            modules,
                            architecture,
                            processReadHandle,
                            processReadError))
                        .ToArray(),
                    string.Empty);
            }
            finally
            {
                if (processReadHandle != IntPtr.Zero)
                {
                    ClientNativeRuntimeNativeMethods.CloseHandle(processReadHandle);
                }
            }
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                Win32Exception or
                NotSupportedException)
        {
            return new ClientNativeRuntimeThreadListSnapshot([], FormatClientNativeLaunchProbeException(ex));
        }
    }

    private static string TryGetClientNativeRuntimeThreadWaitReason(ProcessThread thread, string state)
    {
        if (!string.Equals(state, "Wait", StringComparison.OrdinalIgnoreCase))
        {
            return "<not-waiting>";
        }

        try
        {
            return thread.WaitReason.ToString();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return "<unknown>";
        }
    }

    private static ClientNativeRuntimeThreadSnapshot CaptureClientNativeRuntimeThreadSnapshot(
        ClientNativeRuntimeThreadCandidate thread,
        IReadOnlyList<ClientNativeRuntimeModuleInfo> modules,
        ClientNativeRuntimeProcessArchitectureSnapshot architecture,
        IntPtr processReadHandle,
        string processReadError)
    {
        var startAddress = TryGetClientNativeRuntimeThreadStartAddress(thread.ThreadId);
        var startModuleName = "<unknown>";
        ulong? startModuleRva = null;

        if (startAddress.Address is ulong address)
        {
            var module = ResolveClientNativeRuntimeThreadStartModule(address, modules);
            if (module is not null)
            {
                startModuleName = module.Name;
                startModuleRva = address - module.BaseAddress;
            }
        }

        var instructionPointer = CaptureClientNativeRuntimeThreadInstructionPointer(
            thread.ThreadId,
            modules,
            architecture,
            processReadHandle,
            processReadError);

        return new ClientNativeRuntimeThreadSnapshot(
            thread.ThreadId,
            thread.TotalProcessorTimeMs,
            thread.ThreadState,
            thread.WaitReason,
            startAddress.Address,
            startModuleName,
            startModuleRva,
            startAddress.Error,
            instructionPointer.Address,
            instructionPointer.ModuleName,
            instructionPointer.ModuleRva,
            instructionPointer.Bytes,
            instructionPointer.Error);
    }

    private static ClientNativeRuntimeThreadStartAddressSnapshot TryGetClientNativeRuntimeThreadStartAddress(
        int threadId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ClientNativeRuntimeThreadStartAddressSnapshot(
                null,
                "type=PlatformNotSupportedException message=Windows thread snapshots are required.");
        }

        var threadHandle = ClientNativeRuntimeNativeMethods.OpenThread(
            ClientNativeRuntimeNativeMethods.ThreadQueryInformation,
            false,
            unchecked((uint)threadId));
        if (threadHandle == IntPtr.Zero)
        {
            var queryInformationError = Marshal.GetLastWin32Error();
            threadHandle = ClientNativeRuntimeNativeMethods.OpenThread(
                ClientNativeRuntimeNativeMethods.ThreadQueryLimitedInformation,
                false,
                unchecked((uint)threadId));
            if (threadHandle == IntPtr.Zero)
            {
                var limitedInformationError = Marshal.GetLastWin32Error();
                return new ClientNativeRuntimeThreadStartAddressSnapshot(
                    null,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"OpenThread queryError={queryInformationError} limitedQueryError={limitedInformationError}"));
            }
        }

        try
        {
            var status = ClientNativeRuntimeNativeMethods.NtQueryInformationThread(
                threadHandle,
                ClientNativeRuntimeNativeMethods.ThreadQuerySetWin32StartAddress,
                out var startAddress,
                IntPtr.Size,
                IntPtr.Zero);
            if (status != 0)
            {
                return new ClientNativeRuntimeThreadStartAddressSnapshot(
                    null,
                    string.Create(CultureInfo.InvariantCulture, $"NtQueryInformationThread ntstatus=0x{status:X8}"));
            }

            return new ClientNativeRuntimeThreadStartAddressSnapshot(
                unchecked((ulong)startAddress.ToInt64()),
                string.Empty);
        }
        finally
        {
            ClientNativeRuntimeNativeMethods.CloseHandle(threadHandle);
        }
    }

    private static ClientNativeRuntimeModuleInfo? ResolveClientNativeRuntimeThreadStartModule(
        ulong address,
        IReadOnlyList<ClientNativeRuntimeModuleInfo> modules)
    {
        return modules
            .Where(module => address >= module.BaseAddress && address < module.EndAddress)
            .OrderByDescending(module => module.BaseAddress)
            .FirstOrDefault();
    }

    private static ClientNativeRuntimeProcessArchitectureSnapshot CaptureClientNativeRuntimeProcessArchitecture(
        Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ClientNativeRuntimeProcessArchitectureSnapshot(
                false,
                "type=PlatformNotSupportedException message=Windows thread context snapshots are required.");
        }

        if (!Environment.Is64BitOperatingSystem)
        {
            return new ClientNativeRuntimeProcessArchitectureSnapshot(false, string.Empty);
        }

        try
        {
            if (!ClientNativeRuntimeNativeMethods.IsWow64Process(process.Handle, out var isWow64))
            {
                return new ClientNativeRuntimeProcessArchitectureSnapshot(
                    false,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"IsWow64Process error={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}"));
            }

            return new ClientNativeRuntimeProcessArchitectureSnapshot(isWow64, string.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return new ClientNativeRuntimeProcessArchitectureSnapshot(false, FormatClientNativeLaunchProbeException(ex));
        }
    }

    private static IntPtr TryOpenClientNativeRuntimeProcessReadHandle(int processId, out string error)
    {
        error = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            error = "type=PlatformNotSupportedException message=Windows process memory reads are required.";
            return IntPtr.Zero;
        }

        var processHandle = ClientNativeRuntimeNativeMethods.OpenProcess(
            ClientNativeRuntimeNativeMethods.ProcessVmRead,
            false,
            unchecked((uint)processId));
        if (processHandle == IntPtr.Zero)
        {
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"OpenProcess vmReadError={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}");
        }

        return processHandle;
    }

    private static ClientNativeRuntimeThreadInstructionPointerSnapshot CaptureClientNativeRuntimeThreadInstructionPointer(
        int threadId,
        IReadOnlyList<ClientNativeRuntimeModuleInfo> modules,
        ClientNativeRuntimeProcessArchitectureSnapshot architecture,
        IntPtr processReadHandle,
        string processReadError)
    {
        if (!OperatingSystem.IsWindows())
        {
            return ClientNativeRuntimeThreadInstructionPointerSnapshot.Unknown(
                "type=PlatformNotSupportedException message=Windows thread context snapshots are required.");
        }

        if (architecture.Error.Length > 0)
        {
            return ClientNativeRuntimeThreadInstructionPointerSnapshot.Unknown(architecture.Error);
        }

        var threadHandle = ClientNativeRuntimeNativeMethods.OpenThread(
            ClientNativeRuntimeNativeMethods.ThreadGetContext |
                ClientNativeRuntimeNativeMethods.ThreadSuspendResume |
                ClientNativeRuntimeNativeMethods.ThreadQueryInformation,
            false,
            unchecked((uint)threadId));
        if (threadHandle == IntPtr.Zero)
        {
            var queryInformationError = Marshal.GetLastWin32Error();
            threadHandle = ClientNativeRuntimeNativeMethods.OpenThread(
                ClientNativeRuntimeNativeMethods.ThreadGetContext |
                    ClientNativeRuntimeNativeMethods.ThreadSuspendResume |
                    ClientNativeRuntimeNativeMethods.ThreadQueryLimitedInformation,
                false,
                unchecked((uint)threadId));
            if (threadHandle == IntPtr.Zero)
            {
                var limitedInformationError = Marshal.GetLastWin32Error();
                return ClientNativeRuntimeThreadInstructionPointerSnapshot.Unknown(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"OpenThread contextError={queryInformationError} limitedContextError={limitedInformationError}"));
            }
        }

        var suspended = false;
        try
        {
            var suspendResult = ClientNativeRuntimeNativeMethods.SuspendThread(threadHandle);
            if (suspendResult == uint.MaxValue)
            {
                return ClientNativeRuntimeThreadInstructionPointerSnapshot.Unknown(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"SuspendThread error={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}"));
            }

            suspended = true;
            var context = TryGetClientNativeRuntimeThreadContext(threadHandle, architecture);
            if (context.Error.Length > 0 || context.Address is not ulong address)
            {
                return ClientNativeRuntimeThreadInstructionPointerSnapshot.Unknown(context.Error);
            }

            var moduleName = "<unknown>";
            ulong? moduleRva = null;
            var module = ResolveClientNativeRuntimeThreadStartModule(address, modules);
            if (module is not null)
            {
                moduleName = module.Name;
                moduleRva = address - module.BaseAddress;
            }

            var bytes = TryReadClientNativeRuntimeInstructionBytes(
                processReadHandle,
                processReadError,
                address,
                out var readError);

            return new ClientNativeRuntimeThreadInstructionPointerSnapshot(
                address,
                moduleName,
                moduleRva,
                bytes,
                readError);
        }
        finally
        {
            if (suspended)
            {
                ClientNativeRuntimeNativeMethods.ResumeThread(threadHandle);
            }

            ClientNativeRuntimeNativeMethods.CloseHandle(threadHandle);
        }
    }

    private static ClientNativeRuntimeThreadContextSnapshot TryGetClientNativeRuntimeThreadContext(
        IntPtr threadHandle,
        ClientNativeRuntimeProcessArchitectureSnapshot architecture)
    {
        if (architecture.IsWow64)
        {
            if (!Environment.Is64BitProcess)
            {
                return new ClientNativeRuntimeThreadContextSnapshot(
                    null,
                    "type=PlatformNotSupportedException message=WOW64 thread context requires a 64-bit probe process.");
            }

            var context = ClientNativeRuntimeNativeMethods.Wow64Context.Create(
                ClientNativeRuntimeNativeMethods.Wow64ContextControl);
            if (!ClientNativeRuntimeNativeMethods.Wow64GetThreadContext(threadHandle, ref context))
            {
                return new ClientNativeRuntimeThreadContextSnapshot(
                    null,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Wow64GetThreadContext error={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}"));
            }

            return new ClientNativeRuntimeThreadContextSnapshot(context.Eip, string.Empty);
        }

        return new ClientNativeRuntimeThreadContextSnapshot(
            null,
            "type=PlatformNotSupportedException message=Only WOW64/x86 target thread context is supported.");
    }

    private static byte[]? TryReadClientNativeRuntimeInstructionBytes(
        IntPtr processReadHandle,
        string processReadError,
        ulong address,
        out string error)
    {
        error = string.Empty;
        if (processReadHandle == IntPtr.Zero)
        {
            error = processReadError.Length > 0 ? processReadError : "OpenProcess vmReadError=<unknown>";
            return null;
        }

        var buffer = new byte[ClientNativeRuntimeInstructionByteSampleLength];
        if (!ClientNativeRuntimeNativeMethods.ReadProcessMemory(
            processReadHandle,
            ToClientNativeRuntimePointer(address),
            buffer,
            buffer.Length,
            out var bytesRead))
        {
            error = string.Create(
                CultureInfo.InvariantCulture,
                $"ReadProcessMemory error={Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)}");
            return null;
        }

        var byteCount = bytesRead.ToInt64();
        if (byteCount <= 0)
        {
            error = "ReadProcessMemory read=0";
            return null;
        }

        if (byteCount >= buffer.Length)
        {
            return buffer;
        }

        var sampled = new byte[byteCount];
        Array.Copy(buffer, sampled, sampled.Length);
        return sampled;
    }

    private static IntPtr ToClientNativeRuntimePointer(ulong address) =>
        unchecked((IntPtr)(long)address);

    private static IReadOnlyList<ClientNativeRuntimeDebugLogBaseline> CaptureClientNativeRuntimeDebugLogBaselines(
        IReadOnlyList<string> paths)
    {
        return paths
            .Select(path =>
            {
                var read = ReadClientNativeRuntimeDebugLog(path);
                return new ClientNativeRuntimeDebugLogBaseline(
                    path,
                    read.Exists,
                    read.Length ?? 0,
                    read.Error);
            })
            .ToArray();
    }

    private static IReadOnlyList<ClientNativeRuntimeDebugLogSnapshot> CaptureClientNativeRuntimeDebugLogs(
        IReadOnlyList<ClientNativeRuntimeDebugLogBaseline> baselines)
    {
        return baselines
            .Select(baseline =>
            {
                var read = ReadClientNativeRuntimeDebugLog(baseline.Path);
                long? delta = null;
                if (read.Length is long length)
                {
                    delta = length - baseline.InitialLength;
                }

                return new ClientNativeRuntimeDebugLogSnapshot(
                    baseline.Path,
                    baseline.InitialExists,
                    baseline.InitialLength,
                    baseline.InitialError,
                    read.Exists,
                    read.Length,
                    delta,
                    read.Error);
            })
            .ToArray();
    }

    private static ClientNativeRuntimeDebugLogRead ReadClientNativeRuntimeDebugLog(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists
                ? new ClientNativeRuntimeDebugLogRead(true, file.Length, string.Empty)
                : new ClientNativeRuntimeDebugLogRead(false, null, string.Empty);
        }
        catch (Exception ex) when (
            ex is IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
        {
            return new ClientNativeRuntimeDebugLogRead(false, null, FormatClientNativeLaunchProbeException(ex));
        }
    }

    private static string FormatClientNativeRuntimeDebugLogPaths(IReadOnlyList<string> paths)
    {
        return paths.Count == 0 ? "<none>" : string.Join(", ", paths);
    }

    private static void PrintClientNativeRuntimeSnapshots(IReadOnlyList<ClientNativeRuntimeSnapshot> snapshots)
    {
        Console.WriteLine($"RuntimeSnapshots: {snapshots.Count.ToString(CultureInfo.InvariantCulture)}");
        foreach (var snapshot in snapshots)
        {
            Console.WriteLine(
                $"RuntimeSnapshot: reason={snapshot.Reason} " +
                $"elapsedMs={snapshot.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} " +
                $"rootExists={FormatClientLaunchEvidenceBool(snapshot.RootExists)} " +
                $"processCount={snapshot.Processes.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"tcpCount={snapshot.TcpConnections.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"debugLogs={snapshot.DebugLogs.Count.ToString(CultureInfo.InvariantCulture)}");

            foreach (var process in snapshot.Processes)
            {
                Console.WriteLine(
                    $"  Process: pid={process.ProcessId.ToString(CultureInfo.InvariantCulture)} " +
                    $"exists={FormatClientLaunchEvidenceBool(process.Exists)} " +
                    $"name={FormatClientNativeRuntimeText(process.Name)} " +
                    $"path={FormatClientNativeRuntimeText(process.ExecutablePath)} " +
                    $"responding={FormatClientNativeRuntimeNullableBool(process.Responding)} " +
                    $"mainWindowHandle={FormatClientNativeRuntimeWindowHandle(process.MainWindowHandle)} " +
                    $"mainWindowTitle={FormatClientNativeRuntimeText(process.MainWindowTitle)}");

                Console.WriteLine(
                    $"  Modules: pid={process.ProcessId.ToString(CultureInfo.InvariantCulture)} " +
                    $"count={process.ModuleNames.Count.ToString(CultureInfo.InvariantCulture)} " +
                    $"names={FormatClientNativeRuntimeList(process.ModuleNames)} " +
                    $"error={FormatClientNativeRuntimeText(process.ModuleError)}");

                if (process.ThreadError.Length > 0)
                {
                    Console.WriteLine(
                        $"  Threads: pid={process.ProcessId.ToString(CultureInfo.InvariantCulture)} " +
                        $"error={FormatClientNativeRuntimeText(process.ThreadError)}");
                }

                foreach (var thread in process.TopThreads)
                {
                    Console.WriteLine(
                        $"  Thread: pid={process.ProcessId.ToString(CultureInfo.InvariantCulture)} " +
                        $"id={thread.ThreadId.ToString(CultureInfo.InvariantCulture)} " +
                        $"cpuMs={thread.TotalProcessorTimeMs.ToString(CultureInfo.InvariantCulture)} " +
                        $"state={thread.ThreadState} waitReason={thread.WaitReason} " +
                        $"startAddress={FormatClientNativeRuntimeNullableHex(thread.StartAddress)} " +
                        $"startModule={FormatClientNativeRuntimeText(thread.StartModuleName)} " +
                        $"startRva={FormatClientNativeRuntimeNullableHex(thread.StartModuleRva)} " +
                        $"startError={FormatClientNativeRuntimeText(thread.StartAddressError)} " +
                        $"instructionPointer={FormatClientNativeRuntimeNullableHex(thread.InstructionPointer)} " +
                        $"instructionModule={FormatClientNativeRuntimeText(thread.InstructionModuleName)} " +
                        $"instructionRva={FormatClientNativeRuntimeNullableHex(thread.InstructionModuleRva)} " +
                        $"instructionBytes={FormatClientNativeRuntimeBytes(thread.InstructionBytes)} " +
                        $"instructionError={FormatClientNativeRuntimeText(thread.InstructionPointerError)}");
                }
            }

            if (snapshot.TcpError.Length > 0)
            {
                Console.WriteLine($"  TcpError: {snapshot.TcpError}");
            }

            foreach (var connection in snapshot.TcpConnections)
            {
                Console.WriteLine(
                    $"  Tcp: pid={connection.OwningProcessId.ToString(CultureInfo.InvariantCulture)} " +
                    $"state={connection.State} " +
                    $"local={connection.LocalAddress}:{connection.LocalPort.ToString(CultureInfo.InvariantCulture)} " +
                    $"remote={connection.RemoteAddress}:{connection.RemotePort.ToString(CultureInfo.InvariantCulture)}");
            }

            foreach (var debugLog in snapshot.DebugLogs)
            {
                Console.WriteLine(
                    $"  DebugLog: path={debugLog.Path} " +
                    $"initialExists={FormatClientLaunchEvidenceBool(debugLog.InitialExists)} " +
                    $"initialLength={debugLog.InitialLength.ToString(CultureInfo.InvariantCulture)} " +
                    $"exists={FormatClientLaunchEvidenceBool(debugLog.Exists)} " +
                    $"length={FormatClientNativeRuntimeNullableLong(debugLog.Length)} " +
                    $"delta={FormatClientNativeRuntimeNullableLong(debugLog.Delta)} " +
                    $"error={FormatClientNativeRuntimeText(FormatClientNativeRuntimeDebugLogError(debugLog))}");
            }
        }
    }

    private static string FormatClientNativeRuntimeDebugLogError(ClientNativeRuntimeDebugLogSnapshot debugLog)
    {
        if (debugLog.Error.Length > 0)
        {
            return debugLog.Error;
        }

        return debugLog.InitialError;
    }

    private static string FormatClientNativeRuntimeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<none>";
        }

        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string FormatClientNativeRuntimeList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "<none>" : string.Join(",", values.Select(FormatClientNativeRuntimeText));
    }

    private static string FormatClientNativeRuntimeNullableBool(bool? value)
    {
        return value is bool typedValue ? FormatClientLaunchEvidenceBool(typedValue) : "<unknown>";
    }

    private static string FormatClientNativeRuntimeNullableLong(long? value)
    {
        return value is long typedValue ? typedValue.ToString(CultureInfo.InvariantCulture) : "<unknown>";
    }

    private static string FormatClientNativeRuntimeNullableHex(ulong? value)
    {
        return value is ulong typedValue
            ? "0x" + typedValue.ToString("X", CultureInfo.InvariantCulture)
            : "<unknown>";
    }

    private static string FormatClientNativeRuntimeBytes(byte[]? bytes)
    {
        return bytes is { Length: > 0 } ? BitConverter.ToString(bytes) : "<unknown>";
    }

    private static string FormatClientNativeRuntimeWindowHandle(IntPtr handle)
    {
        return "0x" + handle.ToInt64().ToString("X", CultureInfo.InvariantCulture);
    }
}

internal sealed record ClientNativeRuntimeLaunchReport(
    bool StartSucceeded,
    int? ProcessId,
    string NativeError,
    bool Exited,
    int? ExitCode,
    bool TimedOut,
    bool KillAttempted,
    bool Killed,
    string KillError,
    long ElapsedMilliseconds,
    IReadOnlyList<ClientNativeRuntimeSnapshot> Snapshots,
    ClientNativeLaunchReadiness Readiness);

internal sealed record ClientNativeRuntimeSnapshot(
    string Reason,
    long ElapsedMilliseconds,
    bool RootExists,
    IReadOnlyList<ClientNativeRuntimeProcessSnapshot> Processes,
    IReadOnlyList<WorldRelayCaptureTcpConnection> TcpConnections,
    string TcpError,
    IReadOnlyList<ClientNativeRuntimeDebugLogSnapshot> DebugLogs);

internal sealed record ClientNativeRuntimeProcessSnapshot(
    int ProcessId,
    bool Exists,
    string Name,
    string ExecutablePath,
    bool? Responding,
    string RespondingError,
    IntPtr MainWindowHandle,
    string MainWindowTitle,
    IReadOnlyList<string> ModuleNames,
    string ModuleError,
    IReadOnlyList<ClientNativeRuntimeThreadSnapshot> TopThreads,
    string ThreadError)
{
    public static ClientNativeRuntimeProcessSnapshot Exited(int processId) =>
        new(
            processId,
            false,
            "<exited>",
            string.Empty,
            null,
            string.Empty,
            IntPtr.Zero,
            string.Empty,
            [],
            string.Empty,
            [],
            string.Empty);

    public static ClientNativeRuntimeProcessSnapshot Unreadable(int processId, string error) =>
        new(
            processId,
            false,
            "<unreadable>",
            string.Empty,
            null,
            error,
            IntPtr.Zero,
            string.Empty,
            [],
            error,
            [],
            error);
}

internal sealed record ClientNativeRuntimeThreadSnapshot(
    int ThreadId,
    long TotalProcessorTimeMs,
    string ThreadState,
    string WaitReason,
    ulong? StartAddress,
    string StartModuleName,
    ulong? StartModuleRva,
    string StartAddressError,
    ulong? InstructionPointer,
    string InstructionModuleName,
    ulong? InstructionModuleRva,
    byte[]? InstructionBytes,
    string InstructionPointerError);

internal sealed record ClientNativeRuntimeThreadCandidate(
    int ThreadId,
    long TotalProcessorTimeMs,
    string ThreadState,
    string WaitReason);

internal sealed record ClientNativeRuntimeThreadStartAddressSnapshot(
    ulong? Address,
    string Error);

internal sealed record ClientNativeRuntimeThreadInstructionPointerSnapshot(
    ulong? Address,
    string ModuleName,
    ulong? ModuleRva,
    byte[]? Bytes,
    string Error)
{
    public static ClientNativeRuntimeThreadInstructionPointerSnapshot Unknown(string error) =>
        new(null, "<unknown>", null, null, error);
}

internal sealed record ClientNativeRuntimeThreadContextSnapshot(
    ulong? Address,
    string Error);

internal sealed record ClientNativeRuntimeProcessArchitectureSnapshot(
    bool IsWow64,
    string Error);

internal sealed record ClientNativeRuntimeDebugLogSnapshot(
    string Path,
    bool InitialExists,
    long InitialLength,
    string InitialError,
    bool Exists,
    long? Length,
    long? Delta,
    string Error);

internal sealed record ClientNativeRuntimeProcessEntry(int ProcessId, int ParentProcessId);

internal sealed record ClientNativeRuntimeModuleSnapshot(
    IReadOnlyList<string> ModuleNames,
    IReadOnlyList<ClientNativeRuntimeModuleInfo> Modules,
    string Error);

internal sealed record ClientNativeRuntimeModuleInfo(
    string Name,
    ulong BaseAddress,
    ulong EndAddress);

internal sealed record ClientNativeRuntimeThreadListSnapshot(
    IReadOnlyList<ClientNativeRuntimeThreadSnapshot> Threads,
    string Error);

internal sealed record ClientNativeRuntimeDebugLogBaseline(
    string Path,
    bool InitialExists,
    long InitialLength,
    string InitialError);

internal sealed record ClientNativeRuntimeDebugLogRead(
    bool Exists,
    long? Length,
    string Error);

internal static class ClientNativeRuntimeNativeMethods
{
    public const uint Th32csSnapprocess = 0x00000002;
    public const uint ProcessVmRead = 0x0010;
    public const uint ThreadSuspendResume = 0x0002;
    public const uint ThreadGetContext = 0x0008;
    public const uint ThreadQueryInformation = 0x0040;
    public const uint ThreadQueryLimitedInformation = 0x0800;
    public const int ThreadQuerySetWin32StartAddress = 9;
    public const uint I386ContextFlag = 0x00010000;
    public const uint I386ContextControl = I386ContextFlag | 0x00000001;
    public const uint I386ContextInteger = I386ContextFlag | 0x00000002;
    public const uint Wow64ContextControl = I386ContextControl | I386ContextInteger;
    public const int ErrorNoMoreFiles = 18;
    public static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenThread(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint threadId);

    [DllImport("ntdll.dll")]
    public static extern int NtQueryInformationThread(
        IntPtr threadHandle,
        int threadInformationClass,
        out IntPtr threadInformation,
        int threadInformationLength,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SuspendThread(IntPtr threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int ResumeThread(IntPtr threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Wow64GetThreadContext(IntPtr threadHandle, ref Wow64Context context);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemory(
        IntPtr processHandle,
        IntPtr baseAddress,
        byte[] buffer,
        int size,
        out IntPtr bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWow64Process(IntPtr processHandle, out bool wow64Process);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 processEntry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Wow64Context
    {
        public uint ContextFlags;
        public uint Dr0;
        public uint Dr1;
        public uint Dr2;
        public uint Dr3;
        public uint Dr6;
        public uint Dr7;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 112)]
        public byte[] FloatSave;

        public uint SegGs;
        public uint SegFs;
        public uint SegEs;
        public uint SegDs;
        public uint Edi;
        public uint Esi;
        public uint Ebx;
        public uint Edx;
        public uint Ecx;
        public uint Eax;
        public uint Ebp;
        public uint Eip;
        public uint SegCs;
        public uint EFlags;
        public uint Esp;
        public uint SegSs;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] ExtendedRegisters;

        public static Wow64Context Create(uint flags)
        {
            return new Wow64Context
            {
                ContextFlags = flags,
                FloatSave = new byte[112],
                ExtendedRegisters = new byte[512],
            };
        }
    }

}
