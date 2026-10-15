using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class PullSubmodules : Popup
    {
        public List<SubmoduleBranchTarget> Targets
        {
            get;
        }

        // Always starts unchecked (merge), it is not remembered.
        public bool UseRebase
        {
            get;
            set;
        } = false;

        public PullSubmodules(Repository repo, List<SubmoduleBranchTarget> targets)
        {
            _repo = repo;
            Targets = targets;
            CanTerminate = true;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Pull Submodules");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            var useRebase = UseRebase;

            foreach (var target in Targets)
            {
                if (token.IsCancellationRequested)
                    break;

                var submodule = target.Module;
                var fullpath = target.FullPath;
                var changes = await new Commands.CountLocalChanges(fullpath, false).GetResultAsync();
                if (changes > 0)
                {
                    log.AppendLine($"Skipped '{submodule.Path}': has local changes\n");
                    continue;
                }

                if (!await SubmoduleBranchTarget.EnsureOnBranchAsync(submodule, fullpath, _repo.FullPath, log))
                    continue;

                var info = await SubmoduleTrackingInfo.QueryAsync(fullpath);
                if (info.Local == null)
                {
                    log.AppendLine($"Skipped '{submodule.Path}': {info.SkipReason}\n");
                    continue;
                }

                var pull = new Commands.Pull(fullpath, info.Remote, null, useRebase);
                pull.Context = _repo.FullPath;
                await pull.WithCancellation(token).Use(log).ExecAsync();
            }

            log.Complete();
            _repo.MarkSubmodulesDirtyManually();
            _repo.MarkWorkingCopyDirtyManually();
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
