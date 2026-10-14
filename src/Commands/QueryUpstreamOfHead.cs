using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryUpstreamOfHead : Command
    {
        public QueryUpstreamOfHead(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "rev-parse --symbolic-full-name @{upstream}";
        }

        public async Task<string> GetResultAsync()
        {
            // Fails when HEAD is detached or current branch has no (or a gone) upstream.
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            return rs.IsSuccess ? rs.StdOut.Trim() : string.Empty;
        }
    }
}
