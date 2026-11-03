using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WolfConquer.Tools.OwnedSql
{
    // An unnamed Job is attached atomically during CreateProcess. No PID reopening,
    // process-name search, service control, or breakaway permission is used.
    public sealed class OwnedSqlChild : IDisposable
    {
        private WolfConquer.DevChecks.OwnedProcessJob job;
        private IntPtr process;
        private readonly string image;
        private long creationTime;
        public uint Id { get; private set; }
        private OwnedSqlChild(string image) { this.image = Path.GetFullPath(image); }
        private static void Check(bool result) { if (!result) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        public static void CreateFreshDirectory(string path) { Check(Native.CreateDirectoryW(Path.GetFullPath(path), IntPtr.Zero)); }

        public static OwnedSqlChild Start(string image, string[] arguments, string directory, string stdout, string stderr)
        {
            var child = new OwnedSqlChild(image);
            IntPtr input = IntPtr.Zero, output = IntPtr.Zero, error = IntPtr.Zero;
            IntPtr attributes = IntPtr.Zero, handles = IntPtr.Zero, jobList = IntPtr.Zero, environment = IntPtr.Zero;
            var pi = new Native.ProcessInformation(); bool initialized = false;
            try
            {
                child.job = new WolfConquer.DevChecks.OwnedProcessJob();
                var security = new Native.SecurityAttributes { Length = Marshal.SizeOf<Native.SecurityAttributes>(), Inherit = 1 };
                input = Native.CreateFileW("NUL", 0x80000000, 3, ref security, 3, 0x80, IntPtr.Zero);
                output = Native.CreateFileW(stdout, 0x40000000, 1, ref security, 1, 0x80, IntPtr.Zero);
                error = Native.CreateFileW(stderr, 0x40000000, 1, ref security, 1, 0x80, IntPtr.Zero);
                Check(input != new IntPtr(-1) && output != new IntPtr(-1) && error != new IntPtr(-1));
                IntPtr size = IntPtr.Zero;
                Native.InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
                Check(size != IntPtr.Zero); attributes = Marshal.AllocHGlobal(size);
                Check(Native.InitializeProcThreadAttributeList(attributes, 2, 0, ref size)); initialized = true;
                handles = Marshal.AllocHGlobal(3 * IntPtr.Size);
                Marshal.WriteIntPtr(handles, 0, input); Marshal.WriteIntPtr(handles, IntPtr.Size, output); Marshal.WriteIntPtr(handles, 2 * IntPtr.Size, error);
                Check(Native.UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x20002), handles, new IntPtr(3 * IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
                jobList = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(jobList, child.job.SuspendedLaunchHandle);
                Check(Native.UpdateProcThreadAttribute(attributes, 0, new IntPtr(0x2000D), jobList, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
                environment = Marshal.StringToHGlobalUni(CleanEnvironment());
                var startup = new Native.StartupInfoEx(); startup.Startup.Size = Marshal.SizeOf<Native.StartupInfoEx>();
                startup.Startup.Flags = 0x100; startup.Startup.Input = input; startup.Startup.Output = output; startup.Startup.Error = error;
                startup.Attributes = attributes;
                var command = new StringBuilder(Quote(child.image));
                foreach (string argument in arguments) command.Append(' ').Append(Quote(argument));
                Check(Native.CreateProcessW(child.image, command, IntPtr.Zero, IntPtr.Zero, true,
                    0x08080404, environment, Path.GetFullPath(directory), ref startup, out pi));
                child.process = pi.Process; child.Id = pi.ProcessId;
                Native.FileTime created, exited, kernel, user;
                Check(Native.GetProcessTimes(child.process, out created, out exited, out kernel, out user)); child.creationTime = created.Value;
                child.RequireCurrent(); Check(Native.ResumeThread(pi.Thread) != uint.MaxValue);
                return child;
            }
            catch { child.Dispose(); throw; }
            finally
            {
                if (pi.Thread != IntPtr.Zero) Native.CloseHandle(pi.Thread);
                foreach (IntPtr handle in new[] { input, output, error }) if (handle != IntPtr.Zero && handle != new IntPtr(-1)) Native.CloseHandle(handle);
                if (initialized) Native.DeleteProcThreadAttributeList(attributes);
                foreach (IntPtr allocation in new[] { attributes, handles, jobList, environment }) if (allocation != IntPtr.Zero) Marshal.FreeHGlobal(allocation);
            }
        }

        public bool Wait(int milliseconds)
        {
            if (milliseconds < 0 || process == IntPtr.Zero) throw new InvalidOperationException("Invalid owned child wait.");
            uint result = Native.WaitForSingleObject(process, (uint)milliseconds);
            if (result == 0) return true; if (result == 258) return false;
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public uint ExitCode { get { uint result = 0; Check(GetExited() && Native.GetExitCodeProcess(process, out result)); return result; } }
        private bool GetExited() { return process != IntPtr.Zero && Wait(0); }
        public void RequireCurrent()
        {
            if (process == IntPtr.Zero || Wait(0)) throw new InvalidOperationException("Owned child is no longer running.");
            var actual = new StringBuilder(32768); uint length = (uint)actual.Capacity;
            Check(Native.QueryFullProcessImageNameW(process, 0, actual, ref length));
            Native.FileTime created, exited, kernel, user;
            Check(Native.GetProcessTimes(process, out created, out exited, out kernel, out user));
            if (!String.Equals(Path.GetFullPath(actual.ToString()), image, StringComparison.OrdinalIgnoreCase) || created.Value != creationTime)
                throw new InvalidOperationException("Owned child identity changed.");
        }
        public void Settle(int milliseconds)
        {
            if (job == null) return;
            job.TerminateAndSettle(milliseconds);
            if (process != IntPtr.Zero && !Wait(0)) throw new InvalidOperationException("Owned process handle is not reaped; retain the fixture directory.");
        }
        public void Dispose()
        {
            if (job != null) { job.Dispose(); job = null; }
            if (process != IntPtr.Zero) { Native.CloseHandle(process); process = IntPtr.Zero; }
        }
        private static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0) throw new ArgumentException("Invalid child argument.");
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        private static string CleanEnvironment()
        {
            var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                string key = (string)entry.Key;
                if (key.StartsWith("MYSQL", StringComparison.OrdinalIgnoreCase) || key.StartsWith("MARIADB", StringComparison.OrdinalIgnoreCase) || key.StartsWith("WOLF_SQL_AUTH_FIXTURE", StringComparison.OrdinalIgnoreCase)) continue;
                values[key] = (string)entry.Value;
            }
            var result = new StringBuilder(); foreach (var pair in values) result.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
            return result.Append('\0').ToString();
        }
    }
}
