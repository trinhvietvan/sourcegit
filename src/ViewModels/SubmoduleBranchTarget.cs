using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Decides how `Commit/Pull/Push All Submodules` handle one submodule: work on its current branch, checkout a
    ///     branch first (detached HEAD exactly at the tip of that branch, so nothing can be lost), or skip it.
    /// </summary>
    public class SubmoduleBranchTarget
    {
        public Models.Submodule Module { get; private init; }
        public string FullPath { get; private init; }
        public string Branch { get; private set; } = string.Empty;
        public string CheckoutBranch { get; private set; } = string.Empty;
        public string CheckoutBasedOn { get; private set; } = string.Empty;
        public string SkipReason { get; private set; } = string.Empty;

        public bool IsOnBranch => !string.IsNullOrEmpty(Branch);
        public bool WillCheckout => !string.IsNullOrEmpty(CheckoutBranch);
        public bool WillSkip => !string.IsNullOrEmpty(SkipReason);

        public string StatusText
        {
            get
            {
                if (IsOnBranch)
                    return Branch;
                if (WillCheckout)
                    return App.Text("Submodules.Detached.Checkout", CheckoutBranch);
                return App.Text("Submodules.Detached.Skip");
            }
        }

        public static async Task<SubmoduleBranchTarget> QueryAsync(Models.Submodule module, string fullpath)
        {
            var target = new SubmoduleBranchTarget() { Module = module, FullPath = fullpath };
            var branches = await new Commands.QueryBranches(fullpath).GetResultAsync();

            var current = branches.Find(x => x.IsLocal && x.IsCurrent);
            if (current is { IsDetachedHead: false })
            {
                target.Branch = current.Name;
                return target;
            }

            var head = current?.Head;
            if (string.IsNullOrEmpty(head))
            {
                target.SkipReason = "HEAD is detached";
                return target;
            }

            foreach (var name in await GetCandidateBranchesAsync(module, fullpath, branches, head))
            {
                var local = branches.Find(x => x.IsLocal && !x.IsDetachedHead && x.Name.Equals(name, StringComparison.Ordinal));
                if (local != null)
                {
                    // An existing local branch is only safe to checkout when it points to HEAD itself.
                    if (!local.Head.Equals(head, StringComparison.Ordinal))
                        continue;

                    target.CheckoutBranch = name;
                    return target;
                }

                var remote = branches.Find(x => !x.IsLocal && x.Remote == "origin" && x.Name.Equals(name, StringComparison.Ordinal) && x.Head.Equals(head, StringComparison.Ordinal)) ??
                    branches.Find(x => !x.IsLocal && x.Name.Equals(name, StringComparison.Ordinal) && x.Head.Equals(head, StringComparison.Ordinal));
                if (remote != null)
                {
                    target.CheckoutBranch = name;
                    target.CheckoutBasedOn = remote.FriendlyName;
                    return target;
                }
            }

            target.SkipReason = "HEAD is detached and not at the tip of any branch";
            return target;
        }

        /// <summary>
        ///     Re-checks the submodule (its state may have changed since the popup was opened) and makes sure it is on a
        ///     branch, checking out `CheckoutBranch` when HEAD is detached at its tip. Returns false (and logs why) when
        ///     the submodule should be skipped.
        /// </summary>
        public static async Task<bool> EnsureOnBranchAsync(Models.Submodule module, string fullpath, string context, Models.ICommandLog log)
        {
            var target = await QueryAsync(module, fullpath);
            if (target.WillSkip)
            {
                log.AppendLine($"Skipped '{module.Path}': {target.SkipReason}\n");
                return false;
            }

            if (!target.WillCheckout)
                return true;

            log.AppendLine($"'{module.Path}': HEAD is detached at the tip of '{target.CheckoutBranch}', checkout it first.\n");
            return await target.CheckoutAsync(context, log);
        }

        /// <summary>
        ///     Checkout `CheckoutBranch` (creating a local tracking branch from `CheckoutBasedOn` when needed).
        /// </summary>
        public async Task<bool> CheckoutAsync(string context, Models.ICommandLog log)
        {
            var checkout = new Commands.Checkout(FullPath);
            checkout.Context = context;
            checkout.Use(log);

            if (string.IsNullOrEmpty(CheckoutBasedOn))
                return await checkout.BranchAsync(CheckoutBranch, false);

            return await checkout.BranchAsync(CheckoutBranch, CheckoutBasedOn, false, false);
        }

        // Ordered by preference: branch configured in `.gitmodules`, default branch of the remote, then the only branch
        // pointing to HEAD (if there is exactly one).
        private static async Task<List<string>> GetCandidateBranchesAsync(Models.Submodule module, string fullpath, List<Models.Branch> branches, string head)
        {
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(module.Branch) && module.Branch != ".")
                candidates.Add(module.Branch);

            var remoteName = branches.Exists(x => !x.IsLocal && x.Remote == "origin")
                ? "origin"
                : branches.Find(x => !x.IsLocal)?.Remote;
            if (!string.IsNullOrEmpty(remoteName))
            {
                var remoteHead = await new Commands.QueryRemoteHead(fullpath, remoteName).GetResultAsync();
                var prefix = remoteName + "/";
                if (remoteHead.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var name = remoteHead.Substring(prefix.Length);
                    if (!candidates.Contains(name))
                        candidates.Add(name);
                }
            }

            var atHead = new HashSet<string>();
            foreach (var b in branches)
            {
                if (!b.IsDetachedHead && b.Head.Equals(head, StringComparison.Ordinal))
                    atHead.Add(b.Name);
            }

            if (atHead.Count == 1)
            {
                foreach (var name in atHead)
                {
                    if (!candidates.Contains(name))
                        candidates.Add(name);
                }
            }

            return candidates;
        }
    }
}
