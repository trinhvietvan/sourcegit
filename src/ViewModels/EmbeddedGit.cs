using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Manages a private copy of Git for Windows (PortableGit) downloaded from the official GitHub releases.
    /// </summary>
    public class EmbeddedGit : ObservableObject
    {
        public static EmbeddedGit Instance { get; } = new();

        public bool IsSupported
        {
            get => OperatingSystem.IsWindows();
        }

        public string InstallDir
        {
            get;
        }

        public string GitExecutable
        {
            get => Path.Combine(InstallDir, "cmd", "git.exe");
        }

        public string InstalledVersion
        {
            get => _installedVersion;
            private set
            {
                if (SetProperty(ref _installedVersion, value))
                    OnPropertyChanged(nameof(IsInstalled));
            }
        }

        public bool IsInstalled
        {
            get => !string.IsNullOrEmpty(_installedVersion);
        }

        public bool IsInUse
        {
            get => _isInUse;
            private set => SetProperty(ref _isInUse, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public double Progress
        {
            get => _progress;
            private set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            private set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public bool IsError
        {
            get => _isError;
            private set => SetProperty(ref _isError, value);
        }

        private EmbeddedGit()
        {
            InstallDir = GetInstallDir();
            Refresh();
        }

        public void Refresh()
        {
            if (!IsSupported)
                return;

            var versionFile = Path.Combine(InstallDir, VERSION_FILE);
            InstalledVersion = File.Exists(GitExecutable) && File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : string.Empty;
            var current = Native.OS.GitExecutable;
            IsInUse = IsInstalled && !string.IsNullOrEmpty(current) && string.Equals(Path.GetFullPath(current), GitExecutable, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Switches to the git found on this system (registry or PATH). Returns false if there is none.
        /// </summary>
        public bool UseSystemGit()
        {
            if (IsBusy)
                return true;

            var system = Native.OS.FindGitExecutable();

            // The embedded git can be found in PATH too (e.g. it was added to the user's PATH), skip it.
            if (!string.IsNullOrEmpty(system) && IsInInstallDir(system))
                system = FindGitOnPathOutsideInstallDir();

            if (string.IsNullOrEmpty(system) || !File.Exists(system))
                return false;

            IsError = false;
            Status = string.Empty;
            Preferences.Instance.GitInstallPath = system;
            Refresh();
            return true;
        }

        /// <summary>
        ///     Installs or updates PortableGit when needed, then switches to it.
        /// </summary>
        public async Task UseEmbeddedGitAsync()
        {
            if (!IsSupported || IsBusy)
                return;

            IsBusy = true;
            IsError = false;
            Progress = 0;
            IsProgressIndeterminate = true;
            _cancellation = new CancellationTokenSource();

            var token = _cancellation.Token;
            var downloaded = InstallDir + ".download.exe";
            var staging = InstallDir + ".new";
            var backup = InstallDir + ".old";

            try
            {
                // The parent folder (e.g. `%LOCALAPPDATA%\SourceGit`) is not created by the app itself.
                Directory.CreateDirectory(Path.GetDirectoryName(InstallDir)!);

                using var client = new HttpClient();
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SourceGit");

                // 1. Find the PortableGit asset in the latest release.
                Status = App.Text("Preferences.Git.Embedded.Checking");
                ReleaseAsset release;
                try
                {
                    release = await QueryLatestReleaseAsync(client, token);
                }
                catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    // Offline or GitHub unreachable: fall back to the copy that is already installed.
                    if (!IsInstalled)
                        throw;

                    UseInstalled();
                    Status = App.Text("Preferences.Git.Embedded.UpdateCheckFailed", InstalledVersion, e.Message);
                    return;
                }

                if (release.Version.Equals(InstalledVersion, StringComparison.Ordinal))
                {
                    UseInstalled();
                    Status = App.Text("Preferences.Git.Embedded.UpToDate", release.Version);
                    return;
                }

                // 2. Download and verify SHA-256 (published by GitHub as the asset digest).
                Status = App.Text("Preferences.Git.Embedded.Downloading", release.Version);
                IsProgressIndeterminate = false;
                await Task.Run(() => DownloadAsync(client, release, downloaded, token), token);

                // 3. Extract. Running the self-extracting archive also runs `post-install.bat` for us.
                Status = App.Text("Preferences.Git.Embedded.Extracting");
                IsProgressIndeterminate = true;
                await Task.Run(() => ExtractAsync(downloaded, staging, token), token);
                await File.WriteAllTextAsync(Path.Combine(staging, VERSION_FILE), release.Version, token);

                // 4. Swap into place. Fails (and rolls back) if some git process is still running from the old copy.
                await Task.Run(() =>
                {
                    DeleteDirectory(backup);
                    if (Directory.Exists(InstallDir))
                        Directory.Move(InstallDir, backup);

                    try
                    {
                        Directory.Move(staging, InstallDir);
                    }
                    catch
                    {
                        if (Directory.Exists(backup) && !Directory.Exists(InstallDir))
                            Directory.Move(backup, InstallDir);
                        throw;
                    }

                    // Leftover is harmless. It will be removed by the next update.
                    TryDeleteDirectory(backup);
                });

                UseInstalled();
                Status = App.Text("Preferences.Git.Embedded.Installed", release.Version);
            }
            catch (OperationCanceledException)
            {
                Status = App.Text("Preferences.Git.Embedded.Canceled");
            }
            catch (Exception e)
            {
                IsError = true;
                Status = App.Text("Preferences.Git.Embedded.Failed", e.Message);
            }
            finally
            {
                TryDeleteFile(downloaded);
                await Task.Run(() => TryDeleteDirectory(staging));
                _cancellation = null;
                IsProgressIndeterminate = false;
                IsBusy = false;
                Refresh();
            }
        }

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        private bool IsInInstallDir(string file)
        {
            var dir = Path.GetFullPath(InstallDir).TrimEnd('\\') + "\\";
            return Path.GetFullPath(file).StartsWith(dir, StringComparison.OrdinalIgnoreCase);
        }

        private string FindGitOnPathOutsideInstallDir()
        {
            var paths = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var entry in paths.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    var file = Path.Combine(Environment.ExpandEnvironmentVariables(entry.Trim('"')), "git.exe");
                    if (File.Exists(file) && !IsInInstallDir(file))
                        return file;
                }
                catch
                {
                    // Ignore invalid entries
                }
            }

            return null;
        }

        private void UseInstalled()
        {
            Preferences.Instance.GitInstallPath = GitExecutable;
            Refresh();
        }

        private static string GetInstallDir()
        {
            // Keep it next to the portable data folder in portable mode. Otherwise use the (non-roaming) local app data.
            var configDir = Native.OS.BasicDirectories.ConfigDir;
            var portableDir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty, "data");
            var root = string.Equals(configDir, portableDir, StringComparison.OrdinalIgnoreCase)
                ? configDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceGit");

            return Path.Combine(root, "git");
        }

        private static async Task<ReleaseAsset> QueryLatestReleaseAsync(HttpClient client, CancellationToken token)
        {
            var suffix = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "-arm64.7z.exe" : "-64-bit.7z.exe";

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var json = await client.GetStringAsync(LATEST_RELEASE_API, timeout.Token);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (!name.StartsWith("PortableGit-", StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                if (!digest.StartsWith("sha256:", StringComparison.Ordinal))
                    throw new Exception($"No SHA-256 digest published for '{name}'");

                return new ReleaseAsset()
                {
                    Version = tag.TrimStart('v'),
                    Url = asset.GetProperty("browser_download_url").GetString(),
                    Size = asset.GetProperty("size").GetInt64(),
                    SHA256 = digest.Substring(7),
                };
            }

            throw new Exception($"No PortableGit package found in release '{tag}'");
        }

        private async Task DownloadAsync(HttpClient client, ReleaseAsset asset, string dest, CancellationToken token)
        {
            using var rsp = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token);
            rsp.EnsureSuccessStatusCode();

            var total = rsp.Content.Headers.ContentLength ?? asset.Size;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            await using (var input = await rsp.Content.ReadAsStreamAsync(token))
            await using (var output = File.Create(dest))
            {
                var buffer = new byte[81920];
                var received = 0L;
                var lastReported = -1;
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    hash.AppendData(buffer, 0, read);
                    received += read;

                    var percent = total > 0 ? (int)(received * 100 / total) : 0;
                    if (percent != lastReported)
                    {
                        lastReported = percent;
                        Dispatcher.UIThread.Post(() => Progress = percent);
                    }
                }
            }

            var actual = Convert.ToHexString(hash.GetHashAndReset());
            if (!actual.Equals(asset.SHA256, StringComparison.OrdinalIgnoreCase))
                throw new Exception("SHA-256 checksum mismatch. The downloaded file has been discarded.");
        }

        private static async Task ExtractAsync(string sfx, string dest, CancellationToken token)
        {
            DeleteDirectory(dest);

            var start = new ProcessStartInfo();
            start.FileName = sfx;
            start.Arguments = $"-o\"{dest}\" -y";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            using var proc = Process.Start(start)!;
            try
            {
                await proc.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                if (!proc.HasExited)
                    proc.Kill(true);
                throw;
            }

            if (proc.ExitCode != 0)
                throw new Exception($"Extracting PortableGit failed (exit code {proc.ExitCode})");

            if (!File.Exists(Path.Combine(dest, "cmd", "git.exe")))
                throw new Exception("Extracted package does not contain 'cmd\\git.exe'");

            if (File.Exists(Path.Combine(dest, "post-install.bat")))
                throw new Exception("PortableGit post-install step did not finish");
        }

        private static void DeleteDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                return;

            // Some files in PortableGit are read-only, which makes `Directory.Delete` fail.
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(dir, true);
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                DeleteDirectory(dir);
            }
            catch
            {
                // Ignore
            }
        }

        private static void TryDeleteFile(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore
            }
        }

        private class ReleaseAsset
        {
            public string Version { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public long Size { get; set; } = 0;
            public string SHA256 { get; set; } = string.Empty;
        }

        private const string LATEST_RELEASE_API = "https://api.github.com/repos/git-for-windows/git/releases/latest";
        private const string VERSION_FILE = ".sourcegit-embedded-version";

        private string _installedVersion = string.Empty;
        private bool _isInUse = false;
        private bool _isBusy = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _status = string.Empty;
        private bool _isError = false;
        private CancellationTokenSource _cancellation = null;
    }
}
