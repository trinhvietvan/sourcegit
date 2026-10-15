using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PushSubmodules : Popup
    {
        public List<SubmoduleBranchTarget> Targets
        {
            get;
        }

        public bool PushAllTags
        {
            get;
            set;
        } = false;

        public bool ForcePush
        {
            get;
            set;
        } = false;

        public PushSubmodules(Repository repo, List<SubmoduleBranchTarget> targets)
        {
            _repo = repo;
            Targets = targets;
            CanTerminate = true;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Push Submodules");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;

            foreach (var target in Targets)
            {
                if (token.IsCancellationRequested)
                    break;

                var submodule = target.Module;
                var fullpath = target.FullPath;
                if (!await SubmoduleBranchTarget.EnsureOnBranchAsync(submodule, fullpath, _repo.FullPath, log))
                    continue;

                var info = await SubmoduleTrackingInfo.QueryAsync(fullpath);
                if (info.Local == null)
                {
                    log.AppendLine($"Skipped '{submodule.Path}': {info.SkipReason}\n");
                    continue;
                }

                if (info.Local.Ahead.Count == 0 && !PushAllTags)
                {
                    log.AppendLine($"Skipped '{submodule.Path}': nothing to push\n");
                    continue;
                }

                var push = new Commands.Push(fullpath, info.Local, info.Remote, info.Upstream, PushAllTags, false, false, ForcePush, false);
                push.Context = _repo.FullPath;
                await push.WithCancellation(token).Use(log).ExecAsync();
            }

            log.Complete();
            _repo.MarkSubmodulesDirtyManually();
            _cancellation = null;
            return true;
        }

        public override void Terminate()
        {
            var _ = _cancellation?.CancelAsync();
        }

        private readonly Repository _repo = null;
        private CancellationTokenSource _cancellation = null;
    }
}
