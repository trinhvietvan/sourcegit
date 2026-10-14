using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SubmoduleTrackingInfo
    {
        public string FullPath { get; private init; }
        public Models.Branch Local { get; private set; } = null;
        public Models.Branch Upstream { get; private set; } = null;
        public Models.Remote Remote { get; private set; } = null;
        public string SkipReason { get; private set; } = string.Empty;

        public static async Task<SubmoduleTrackingInfo> QueryAsync(string fullpath)
        {
            var info = new SubmoduleTrackingInfo() { FullPath = fullpath };

            var branches = await new Commands.QueryBranches(fullpath).GetResultAsync();
            var local = branches.Find(x => x.IsLocal && x.IsCurrent);
            if (local == null || local.IsDetachedHead)
            {
                info.SkipReason = "HEAD is detached";
                return info;
            }

            if (string.IsNullOrEmpty(local.Upstream) || local.IsUpstreamGone)
            {
                info.SkipReason = $"branch '{local.Name}' has no upstream";
                return info;
            }

            var upstream = branches.Find(x => !x.IsLocal && x.FullName.Equals(local.Upstream, StringComparison.Ordinal));
            if (upstream == null)
            {
                info.SkipReason = $"upstream '{local.Upstream}' not found";
                return info;
            }

            var remotes = await new Commands.QueryRemotes(fullpath).GetResultAsync();
            var remote = remotes.Find(x => x.Name.Equals(upstream.Remote, StringComparison.Ordinal));
            if (remote == null)
            {
                info.SkipReason = $"remote '{upstream.Remote}' not found";
                return info;
            }

            info.Local = local;
            info.Upstream = upstream;
            info.Remote = remote;
            return info;
        }
    }
}
