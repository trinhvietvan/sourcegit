using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Saves/erases/reads credentials with the configured git credential helper (`git credential approve|reject|fill`).
    ///     It never prompts the user (a missing credential simply makes `fill` fail), except `FillInteractiveAsync` which
    ///     lets the credential helper show its own sign-in UI (e.g. Git Credential Manager's browser sign-in).
    /// </summary>
    public class Credential : Command
    {
        public Credential(string host, string username)
        {
            Editor = EditorType.None;
            RaiseError = false;

            _host = host;
            _username = username;
        }

        public async Task<Result> ApproveAsync(string password)
        {
            return await RunAsync("approve", password, false).ConfigureAwait(false);
        }

        public async Task<Result> RejectAsync()
        {
            return await RunAsync("reject", null, false).ConfigureAwait(false);
        }

        public async Task<string> FillAsync()
        {
            var rs = await RunAsync("fill", null, false).ConfigureAwait(false);
            return rs.IsSuccess ? ParseOutput(rs.StdOut, "password") : null;
        }

        /// <summary>
        ///     Returns `(username, password)`, or `null` when user cancelled or the credential helper failed.
        /// </summary>
        public async Task<(string Username, string Password)?> FillInteractiveAsync()
        {
            var rs = await RunAsync("fill", null, true).ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var username = ParseOutput(rs.StdOut, "username");
            var password = ParseOutput(rs.StdOut, "password");
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return null;

            return (username, password);
        }

        private static string ParseOutput(string output, string key)
        {
            var prefix = key + "=";
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length);
            }

            return null;
        }

        private async Task<Result> RunAsync(string action, string password, bool interactive)
        {
            // An empty `core.askPass` with no `GIT_ASKPASS`/`SSH_ASKPASS` makes git fall back to the terminal prompt,
            // which is disabled by `GIT_TERMINAL_PROMPT=0`.
            Args = $"-c core.askPass= credential {action}";

            var start = CreateGitStartInfo(true);
            start.RedirectStandardInput = true;
            start.StandardInputEncoding = new UTF8Encoding(false);
            start.Environment.Remove("GIT_ASKPASS");
            start.Environment.Remove("SSH_ASKPASS");
            start.Environment.Remove("SSH_ASKPASS_REQUIRE");
            start.Environment.Remove("SOURCEGIT_LAUNCH_AS_ASKPASS");
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            if (!interactive)
                start.Environment["GCM_INTERACTIVE"] = "never";

            var input = new StringBuilder();
            input.Append("protocol=https\n");
            input.Append("host=").Append(_host).Append('\n');
            if (!string.IsNullOrEmpty(_username))
                input.Append("username=").Append(_username).Append('\n');
            if (!string.IsNullOrEmpty(password))
                input.Append("password=").Append(password).Append('\n');
            input.Append('\n');

            try
            {
                using var proc = Process.Start(start)!;
                await proc.StandardInput.WriteAsync(input.ToString()).ConfigureAwait(false);
                proc.StandardInput.Close();

                var stdout = proc.StandardOutput.ReadToEndAsync();
                var stderr = proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync().ConfigureAwait(false);

                return new Result()
                {
                    IsSuccess = proc.ExitCode == 0,
                    StdOut = await stdout.ConfigureAwait(false),
                    StdErr = (await stderr.ConfigureAwait(false)).Trim(),
                };
            }
            catch (Exception e)
            {
                return Result.Failed(e.Message);
            }
        }

        private readonly string _host;
        private readonly string _username;
    }
}
