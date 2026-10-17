using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

internal static partial class Program
{
    static IReadOnlyList<WorldRelayCapturePreflightProcess> EnumerateWorldRelayCaptureClientProcesses(string clientExePath)
    {
        var fullClientExePath = WorldRelayCapturePreflightLogic.NormalizeClientExePath(clientExePath);
        var processName = Path.GetFileNameWithoutExtension(fullClientExePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return [];
        }

        var processes = new List<WorldRelayCapturePreflightProcess>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                var processPath = TryGetProcessExecutablePath(process);
                if (processPath is not null && WorldRelayCapturePreflightLogic.PathsEqual(processPath, fullClientExePath))
                {
                    processes.Add(new WorldRelayCapturePreflightProcess(process.Id, processPath));
                }
            }
        }

        return processes
            .OrderBy(process => process.Id)
            .ToArray();
    }

    static string? TryGetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (
            ex is Win32Exception or
                InvalidOperationException or
                NotSupportedException)
        {
            return null;
        }
    }

    static IReadOnlyList<WorldRelayCaptureTcpConnection> EnumerateWindowsIPv4TcpConnections()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows TCP owner tables are required.");
        }

        var bufferLength = 0;
        var result = NativeMethods.GetExtendedTcpTable(
            IntPtr.Zero,
            ref bufferLength,
            sort: true,
            NativeMethods.AfInet,
            NativeMethods.TcpTableOwnerPidAll,
            reserved: 0);
        if (result != NativeMethods.ErrorInsufficientBuffer && result != NativeMethods.ErrorSuccess)
        {
            throw new Win32Exception(result);
        }

        var buffer = Marshal.AllocHGlobal(bufferLength);
        try
        {
            result = NativeMethods.GetExtendedTcpTable(
                buffer,
                ref bufferLength,
                sort: true,
                NativeMethods.AfInet,
                NativeMethods.TcpTableOwnerPidAll,
                reserved: 0);
            if (result != NativeMethods.ErrorSuccess)
            {
                throw new Win32Exception(result);
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<NativeMethods.MibTcpRowOwnerPid>();
            var connections = new List<WorldRelayCaptureTcpConnection>(rowCount);

            for (var index = 0; index < rowCount; index++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MibTcpRowOwnerPid>(
                    IntPtr.Add(rowPointer, index * rowSize));
                connections.Add(new WorldRelayCaptureTcpConnection(
                    (int)row.OwningPid,
                    FormatIPv4Address(row.LocalAddr),
                    FormatTcpPort(row.LocalPort),
                    FormatIPv4Address(row.RemoteAddr),
                    FormatTcpPort(row.RemotePort),
                    FormatTcpState(row.State)));
            }

            return connections;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    static string FormatIPv4Address(uint address)
    {
        return new IPAddress(BitConverter.GetBytes(address)).ToString();
    }

    static int FormatTcpPort(uint port)
    {
        return (ushort)IPAddress.NetworkToHostOrder((short)port);
    }

    static string FormatTcpState(uint state)
    {
        return Enum.IsDefined(typeof(TcpState), (int)state)
            ? ((TcpState)state).ToString()
            : $"Unknown({state})";
    }

    static IReadOnlyList<LocalWorldHandoffProcess> EnumerateWolfConquerCommandLineProcesses()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows process command-line metadata is required.");
        }

        const string script = """
        $OutputEncoding = [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        Get-CimInstance Win32_Process |
            Where-Object {
                $_.ProcessId -ne $PID -and
                $_.CommandLine -and
                ($_.CommandLine -like '*WolfConquer.AccountServer*' -or
                 $_.CommandLine -like '*WolfConquer.WorldServer*' -or
                 $_.CommandLine -like '*WolfConquer.LoginWeb*')
            } |
            ForEach-Object {
                $line = $_.ProcessId.ToString() + [char]9 + $_.Name + [char]9 + $_.CommandLine
                [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($line))
            }
        """;

        using var process = new Process();
        process.StartInfo.FileName = "powershell.exe";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-EncodedCommand");
        process.StartInfo.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start PowerShell process metadata query.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Timed out while reading process command-line metadata.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PowerShell process metadata query failed with exit code {process.ExitCode}: {error.Trim()}");
        }

        var processes = new List<LocalWorldHandoffProcess>();
        foreach (var rawLine in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            string decoded;
            try
            {
                decoded = Encoding.UTF8.GetString(Convert.FromBase64String(rawLine.Trim()));
            }
            catch (FormatException)
            {
                continue;
            }

            var fields = decoded.Split('\t', 3);
            if (fields.Length != 3 || !int.TryParse(fields[0], out var processId))
            {
                continue;
            }

            processes.Add(new LocalWorldHandoffProcess(processId, fields[1], fields[2]));
        }

        return processes
            .OrderBy(process => process.ProcessId)
            .ToArray();
    }
}
