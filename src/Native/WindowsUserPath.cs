using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

using Microsoft.Win32;

namespace SourceGit.Native
{
    /// <summary>
    ///     Reads/writes the user's `Path` environment variable (`HKCU\Environment`, no admin rights needed). The raw value
    ///     is kept as `REG_EXPAND_SZ` so entries like `%USERPROFILE%\bin` are not expanded when it is written back.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class WindowsUserPath
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, string lParam, uint flags, uint timeout, out UIntPtr result);

        /// <summary>
        ///     The folder that should be put into `Path` for the given git executable. For Git for Windows / PortableGit it
        ///     is `cmd` (what the official installer adds), even if `bin\git.exe` is selected.
        /// </summary>
        public static string GetGitDir(string gitExecutable)
        {
            if (string.IsNullOrEmpty(gitExecutable) || !File.Exists(gitExecutable))
                return null;

            var dir = Path.GetDirectoryName(Path.GetFullPath(gitExecutable))!;
            if (Path.GetFileName(dir).Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                var cmd = Path.Combine(Path.GetDirectoryName(dir)!, "cmd");
                if (File.Exists(Path.Combine(cmd, "git.exe")))
                    return cmd;
            }

            return dir;
        }

        public static bool Contains(string dir)
        {
            var normalized = Normalize(dir);
            foreach (var entry in Split(ReadRaw(Registry.CurrentUser, USER_ENV_KEY)))
            {
                if (Normalize(entry).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        ///     Puts the folder at the beginning of the user's `Path` (it is still after the system `Path` in new processes).
        /// </summary>
        public static void Add(string dir)
        {
            var entries = Split(ReadRaw(Registry.CurrentUser, USER_ENV_KEY));
            RemoveFrom(entries, dir);
            entries.Insert(0, dir);
            Write(entries);
        }

        public static void Remove(string dir)
        {
            var entries = Split(ReadRaw(Registry.CurrentUser, USER_ENV_KEY));
            if (RemoveFrom(entries, dir))
                Write(entries);
        }

        /// <summary>
        ///     The `git.exe` a newly started terminal will run: Windows builds a new process' `Path` from the system `Path`
        ///     followed by the user's.
        /// </summary>
        public static string ResolveGit()
        {
            var entries = Split(ReadRaw(Registry.LocalMachine, SYSTEM_ENV_KEY));
            entries.AddRange(Split(ReadRaw(Registry.CurrentUser, USER_ENV_KEY)));

            foreach (var entry in entries)
            {
                try
                {
                    var file = Path.Combine(Normalize(entry), "git.exe");
                    if (File.Exists(file))
                        return file;
                }
                catch
                {
                    // Ignore invalid entries
                }
            }

            return null;
        }

        public static bool IsSameDir(string a, string b)
        {
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) &&
                Normalize(a).Equals(Normalize(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadRaw(RegistryKey root, string subkey)
        {
            using var key = root.OpenSubKey(subkey, false);
            return key?.GetValue("Path", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? string.Empty;
        }

        private static void Write(List<string> entries)
        {
            using var key = Registry.CurrentUser.CreateSubKey(USER_ENV_KEY, true);
            key.SetValue("Path", string.Join(';', entries), RegistryValueKind.ExpandString);

            // Let Explorer (and so the terminals started from it) reload the environment.
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, UIntPtr.Zero, "Environment", SMTO_ABORTIFHUNG, 5000, out _);
        }

        private static List<string> Split(string raw)
        {
            var entries = new List<string>();
            foreach (var entry in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                entries.Add(entry);
            return entries;
        }

        private static bool RemoveFrom(List<string> entries, string dir)
        {
            var normalized = Normalize(dir);
            return entries.RemoveAll(x => Normalize(x).Equals(normalized, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        private static string Normalize(string dir)
        {
            try
            {
                var expanded = Environment.ExpandEnvironmentVariables(dir.Trim().Trim('"'));
                return Path.GetFullPath(expanded).TrimEnd('\\', '/');
            }
            catch
            {
                return dir.Trim();
            }
        }

        private const string USER_ENV_KEY = "Environment";
        private const string SYSTEM_ENV_KEY = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    }
}
