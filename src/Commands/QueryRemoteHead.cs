using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     Queries the default branch of a remote (`refs/remotes/<remote>/HEAD`), e.g. `origin/main`.
    /// </summary>
    public class QueryRemoteHead : Command
    {
        public QueryRemoteHead(string repo, string remote)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"symbolic-ref -q --short refs/remotes/{remote}/HEAD";
        }

        public async Task<string> GetResultAsync()
        {
            // Fails when `refs/remotes/<remote>/HEAD` is not set (e.g. the remote was added instead of cloned).
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }
    }
}
