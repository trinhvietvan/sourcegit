using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     GitHub commands of Git Credential Manager (`git credential-manager github list|login`). Unlike
    ///     `git credential fill`, `login` always signs in again, so it can add a second account of the same host.
    /// </summary>
    public class GitHubCredentialManager : Command
    {
        public GitHubCredentialManager(string host)
        {
            Editor = EditorType.None;
            RaiseError = false;

            _url = $"https://{host}";
        }

        /// <summary>
        ///     Returns the accounts stored for the host, or `null` when Git Credential Manager is not available.
        /// </summary>
        public async Task<List<string>> ListAsync()
        {
            var rs = await RunAsync($"list --url {_url} --no-ui", CancellationToken.None).ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var accounts = new List<string>();
            var lines = rs.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
                accounts.Add(line);

            return accounts;
        }

        /// <summary>
        ///     Signs in with the account currently used by the web browser and stores its token.
        /// </summary>
        public async Task<bool> LoginWithBrowserAsync(CancellationToken cancellation)
        {
            var rs = await RunAsync($"login --url {_url} --browser", cancellation).ConfigureAwait(false);
            return rs.IsSuccess;
        }

        private async Task<Result> RunAsync(string args, CancellationToken cancellation)
        {
            Args = $"credential-manager github {args}";

            var start = CreateGitStartInfo(true);
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";

            try
            {
                using var proc = Process.Start(start)!;
                using var _ = cancellation.Register(() =>
                {
                    if (!proc.HasExited)
                        Native.OS.TerminateProcess(proc);
                });

                var stdout = proc.StandardOutput.ReadToEndAsync(CancellationToken.None);
                var stderr = proc.StandardError.ReadToEndAsync(CancellationToken.None);
                await proc.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

                return new Result()
                {
                    IsSuccess = proc.ExitCode == 0 && !cancellation.IsCancellationRequested,
                    StdOut = await stdout.ConfigureAwait(false),
                    StdErr = (await stderr.ConfigureAwait(false)).Trim(),
                };
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }
        }

        private readonly string _url;
    }
}
