using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GrokBotRoute;

public sealed class WindowsGrokBotRuntime : IGrokBotRuntime
{
    public bool ExecutableExists(string path)
    {
        return File.Exists(path);
    }

    public bool IsRunning(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var process in Process.GetProcessesByName(name))
        {
            try
            {
                var processPath = process.MainModule?.FileName;
                if (processPath is not null
                    && string.Equals(processPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Win32Exception)
            {
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    public bool HasInteractiveSession(string targetUserId)
    {
        return TryChooseSession(targetUserId, out _);
    }

    public void Start(string path, string targetUserId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Grok Bot starts only on Windows.");
        }

        if (!TryChooseSession(targetUserId, out var sessionId))
        {
            throw new InvalidOperationException("No interactive session is available.");
        }

        StartInSession(path, sessionId);
    }

    private static bool TryChooseSession(string targetUserId, out uint sessionId)
    {
        sessionId = 0;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var chosen = InteractiveSessionPicker.Choose(
            QuerySessions(),
            targetUserId,
            NativeMethods.WTSGetActiveConsoleSessionId());
        if (chosen is null)
        {
            return false;
        }

        sessionId = chosen.Value;
        return true;
    }

    private static List<InteractiveSession> QuerySessions()
    {
        var sessions = new List<InteractiveSession>();
        if (!NativeMethods.WTSEnumerateSessions(
                IntPtr.Zero,
                0,
                1,
                out var buffer,
                out var count))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            var current = buffer;
            var size = Marshal.SizeOf<NativeMethods.WtsSessionInfo>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<NativeMethods.WtsSessionInfo>(current);
                sessions.Add(new InteractiveSession(
                    info.SessionId,
                    StateName(info.State),
                    QueryString(info.SessionId, NativeMethods.WtsInfoClass.DomainName),
                    QueryString(info.SessionId, NativeMethods.WtsInfoClass.UserName)));
                current = IntPtr.Add(current, size);
            }
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }

        return sessions;
    }

    private static string QueryString(uint sessionId, NativeMethods.WtsInfoClass infoClass)
    {
        if (!NativeMethods.WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                infoClass,
                out var buffer,
                out _))
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringUni(buffer) ?? string.Empty;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    private static string StateName(int state)
    {
        return state switch
        {
            0 => "Active",
            1 => "Connected",
            4 => "Disconnected",
            _ => "Other",
        };
    }

    private static void StartInSession(string path, uint sessionId)
    {
        if (!NativeMethods.WTSQueryUserToken(sessionId, out var userToken))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            const uint access = 0x0001 | 0x0002 | 0x0008 | 0x0080 | 0x0100;
            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    access,
                    IntPtr.Zero,
                    2,
                    1,
                    out var primary))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!NativeMethods.CreateEnvironmentBlock(out var environment, primary, false))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    var startup = new NativeMethods.StartupInfo
                    {
                        cb = Marshal.SizeOf<NativeMethods.StartupInfo>(),
                        lpDesktop = @"winsta0\default",
                    };
                    var command = new StringBuilder("\"" + path + "\"");
                    if (!NativeMethods.CreateProcessAsUser(
                            primary,
                            path,
                            command,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            false,
                            0x00000400,
                            environment,
                            Path.GetDirectoryName(path),
                            ref startup,
                            out var processInfo))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    NativeMethods.CloseHandle(processInfo.hProcess);
                    NativeMethods.CloseHandle(processInfo.hThread);
                }
                finally
                {
                    NativeMethods.DestroyEnvironmentBlock(environment);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(primary);
            }
        }
        finally
        {
            NativeMethods.CloseHandle(userToken);
        }
    }

    private static class NativeMethods
    {
        internal enum WtsInfoClass
        {
            UserName = 5,
            DomainName = 7,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WtsSessionInfo
        {
            public uint SessionId;
            public IntPtr WinStationName;
            public int State;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct StartupInfo
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSEnumerateSessions(
            IntPtr server,
            int reserved,
            int version,
            out IntPtr sessionInfo,
            out int count);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSQuerySessionInformation(
            IntPtr server,
            uint sessionId,
            WtsInfoClass infoClass,
            out IntPtr buffer,
            out uint bytes);

        [DllImport("wtsapi32.dll")]
        internal static extern void WTSFreeMemory(IntPtr memory);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool DuplicateTokenEx(
            IntPtr existingToken,
            uint desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr newToken);

        [DllImport("userenv.dll", SetLastError = true)]
        internal static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

        [DllImport("userenv.dll", SetLastError = true)]
        internal static extern bool DestroyEnvironmentBlock(IntPtr environment);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcessAsUser(
            IntPtr token,
            string applicationName,
            StringBuilder commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfo startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);
    }
}
