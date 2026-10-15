using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CommitSubmodules : Popup
    {
        public List<SubmoduleBranchTarget> Targets
        {
            get;
        }

        [Required(ErrorMessage = "Commit message is required!!!")]
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value, true);
        }

        public bool IncludeUntracked
        {
            get;
            set;
        } = true;

        public override string SureButtonText
        {
            get => App.Text("WorkingCopy.Commit");
        }

        public override string AlternativeSureButtonText
        {
            get => App.Text("WorkingCopy.CommitAndPush");
        }

        public CommitSubmodules(Repository repo, List<SubmoduleBranchTarget> targets)
        {
            _repo = repo;
            Targets = targets;
            CanTerminate = true;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var log = _repo.CreateLog("Commit Submodules");
            Use(log);

            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            var message = _message.Trim();
            var includeUntracked = IncludeUntracked;
            var push = IsAlternativeSure;

            foreach (var listed in Targets)
            {
                if (token.IsCancellationRequested)
                    break;

                var submodule = listed.Module;
                var fullpath = listed.FullPath;
                var changes = await new Commands.CountLocalChanges(fullpath, includeUntracked).GetResultAsync();
                if (changes == 0)
                {
                    log.AppendLine($"Skipped '{submodule.Path}': no local changes\n");
                    continue;
                }

                // Commits made on a detached HEAD are easily lost, so only commit on a branch.
                if (!await SubmoduleBranchTarget.EnsureOnBranchAsync(submodule, fullpath, _repo.FullPath, log))
                    continue;

                var add = new Commands.Add(fullpath, includeUntracked);
                add.Context = _repo.FullPath;
                if (!await add.Use(log).ExecAsync())
                    continue;

                var commit = new Commands.Commit(fullpath, message, false, false, false, false);
                commit.Context = _repo.FullPath;
                if (!await commit.Use(log).RunAsync() || !push || token.IsCancellationRequested)
                    continue;

                // Push the branch just committed to its upstream (same rules as `Push All Submodules`).
                var info = await SubmoduleTrackingInfo.QueryAsync(fullpath);
                if (info.Local == null)
                {
                    log.AppendLine($"Skipped pushing '{submodule.Path}': {info.SkipReason}\n");
                    continue;
                }

                var pushCmd = new Commands.Push(fullpath, info.Local, info.Remote, info.Upstream, false, false, false, false, false);
                pushCmd.Context = _repo.FullPath;
                await pushCmd.WithCancellation(token).Use(log).ExecAsync();
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
        private string _message = string.Empty;
        private CancellationTokenSource _cancellation = null;
    }
}
