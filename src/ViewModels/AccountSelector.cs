using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Lets user bind one of the hosting accounts to a remote (used by `Clone`, `AddRemote` and `EditRemote`):
    ///       * HTTPS remote: `credential.https://{host}.username = {account}` (so the credential helper picks its token)
    ///       * SSH remote: the account's SSH key is used as the remote's SSH key (done by the owner via `AccountChanged`)
    ///       * Optional: `user.name` / `user.email` of the repository are set to the account's.
    /// </summary>
    public class AccountSelector : ObservableObject
    {
        public event Action<Models.HostingAccount> AccountChanged;

        public List<Models.HostingAccount> Accounts
        {
            get => _accounts;
            private set
            {
                if (SetProperty(ref _accounts, value))
                    OnPropertyChanged(nameof(HasAccounts));
            }
        }

        public bool HasAccounts
        {
            get => _accounts.Count > 0;
        }

        public Models.HostingAccount Selected
        {
            get => _selected;
            set
            {
                if (SetProperty(ref _selected, value))
                {
                    OnPropertyChanged(nameof(CanApplyIdentity));
                    if (value == null)
                        ApplyIdentity = false;

                    AccountChanged?.Invoke(value);
                }
            }
        }

        public bool CanApplyIdentity
        {
            get => _selected != null && !string.IsNullOrEmpty(_selected.Email);
        }

        public bool ApplyIdentity
        {
            get => _applyIdentity;
            set => SetProperty(ref _applyIdentity, value);
        }

        /// <summary>
        ///     Updates the available accounts for the given remote URL. When `autoSelect` is set and there is exactly one
        ///     account of this host, it is selected.
        /// </summary>
        public void SetURL(string url, bool autoSelect)
        {
            var accounts = new List<Models.HostingAccount>();
            if (Models.Remote.TryGetHost(url, out var host))
            {
                foreach (var account in Preferences.Instance.HostingAccounts)
                {
                    if (account.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
                        accounts.Add(account);
                }
            }

            _host = host;

            // Keep the same list instance when nothing changed, otherwise the ComboBox clears its selection.
            var changed = accounts.Count != _accounts.Count;
            for (var i = 0; !changed && i < accounts.Count; i++)
                changed = accounts[i] != _accounts[i];
            if (changed)
                Accounts = accounts;

            if (_selected != null && !accounts.Contains(_selected))
                Selected = null;

            if (autoSelect && _selected == null && accounts.Count == 1)
                Selected = accounts[0];
        }

        public void ClearSelection()
        {
            Selected = null;
        }

        /// <summary>
        ///     Finds the account currently bound to an existing remote.
        /// </summary>
        public async Task LoadAsync(string repo, Models.Remote remote)
        {
            SetURL(remote.URL, false);
            if (_accounts.Count == 0)
                return;

            Models.HostingAccount found = null;
            if (Models.Remote.IsSSH(remote.URL))
            {
                if (!string.IsNullOrEmpty(remote.PrivateSSHKey))
                    found = _accounts.Find(x => remote.PrivateSSHKey.Equals(x.SSHKey, StringComparison.Ordinal));
            }
            else
            {
                _boundHost = _host;
                _boundUsername = await new Commands.Config(repo).GetAsync(GetCredentialUsernameKey(_host));
                if (!string.IsNullOrEmpty(_boundUsername))
                    found = _accounts.Find(x => x.Username.Equals(_boundUsername, StringComparison.Ordinal));
            }

            // Do NOT raise `AccountChanged`, the remote already uses this account.
            _selected = found;
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(CanApplyIdentity));
        }

        /// <summary>
        ///     `-c key=value` arguments for `git clone`, so the credential is picked while cloning.
        /// </summary>
        public string GetCloneConfigArgs(bool useSSH)
        {
            if (useSSH || _selected == null || string.IsNullOrEmpty(_host))
                return string.Empty;

            return $"-c {$"{GetCredentialUsernameKey(_host)}={_selected.Username}".Quoted()} ";
        }

        public async Task ApplyAsync(string repo, bool useSSH, Models.ICommandLog log)
        {
            if (string.IsNullOrEmpty(_host))
                return;

            if (!useSSH)
            {
                var key = GetCredentialUsernameKey(_host);
                if (_selected != null)
                {
                    if (_host != _boundHost || !_selected.Username.Equals(_boundUsername, StringComparison.Ordinal))
                        await new Commands.Config(repo).Use(log).SetAsync(key, _selected.Username);
                }
                else if (_host == _boundHost && !string.IsNullOrEmpty(_boundUsername))
                {
                    await new Commands.Config(repo).Use(log).SetAsync(key, null);
                }
            }

            if (_applyIdentity && CanApplyIdentity)
            {
                var name = string.IsNullOrEmpty(_selected.DisplayName) ? _selected.Username : _selected.DisplayName;
                await new Commands.Config(repo).Use(log).SetAsync("user.name", name);
                await new Commands.Config(repo).Use(log).SetAsync("user.email", _selected.Email);
            }
        }

        private static string GetCredentialUsernameKey(string host)
        {
            return $"credential.https://{host}.username";
        }

        private List<Models.HostingAccount> _accounts = [];
        private Models.HostingAccount _selected = null;
        private bool _applyIdentity = false;
        private string _host = null;
        private string _boundHost = null;
        private string _boundUsername = null;
    }
}
