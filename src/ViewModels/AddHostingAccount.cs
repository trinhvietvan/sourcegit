using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    /// <summary>
    ///     Adds a hosting account (or updates the token of an existing one). The token is verified with the provider's API
    ///     and then saved by the git credential helper, so it is never written into `preference.json`.
    /// </summary>
    public class AddHostingAccount : ObservableObject
    {
        public List<Models.HostingProvider> Providers
        {
            get => Models.HostingProvider.Supported;
        }

        public Models.HostingProvider Provider
        {
            get => _provider;
            set
            {
                if (SetProperty(ref _provider, value) && value != null)
                {
                    Host = value.DefaultHost;
                    OnPropertyChanged(nameof(IsHostEditable));
                    OnPropertyChanged(nameof(CanSignInWithBrowser));
                }
            }
        }

        public bool IsUpdate
        {
            get => _editing != null;
        }

        public bool IsHostEditable
        {
            get => !IsUpdate && _provider is { AllowCustomHost: true };
        }

        /// <summary>
        ///     The OAuth token that Git Credential Manager gets for GitHub can read the user's profile, which is not
        ///     guaranteed for the other providers.
        /// </summary>
        public bool CanSignInWithBrowser
        {
            get => !IsUpdate && _provider is { Type: Models.HostingProviderType.GitHub };
        }

        public string Host
        {
            get => _host;
            set => SetProperty(ref _host, value);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string Token
        {
            get => _token;
            set => SetProperty(ref _token, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }

        public Models.HostingAccount Result
        {
            get;
            private set;
        }

        public AddHostingAccount(Models.HostingAccount editing)
        {
            _editing = editing;

            if (editing != null)
            {
                _provider = Models.HostingProvider.Get(editing.Provider);
                _host = editing.Host;
                _email = editing.Email;
            }
            else
            {
                _provider = Models.HostingProvider.Supported[0];
                _host = _provider.DefaultHost;
            }
        }

        public void OpenTokenPage()
        {
            var host = NormalizeHost(_host);
            Native.OS.OpenBrowser(_provider.GetTokenCreationURL(string.IsNullOrEmpty(host) ? _provider.DefaultHost : host));
        }

        public async Task<bool> SignInWithBrowserAsync()
        {
            Error = string.Empty;

            var host = NormalizeHost(_host);
            IsBusy = true;

            (string Username, string Password)? credential;
            try
            {
                credential = await new Commands.Credential(host, null).FillInteractiveAsync();
            }
            finally
            {
                IsBusy = false;
            }

            if (credential == null)
            {
                Error = App.Text("AddHostingAccount.Error.SignIn");
                return false;
            }

            Token = credential.Value.Password;
            return await SaveAsync();
        }

        public async Task<bool> SaveAsync()
        {
            Error = string.Empty;

            var host = NormalizeHost(_host);
            var token = _token?.Trim() ?? string.Empty;
            var email = _email?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(host) || host.IndexOfAny(['/', ' ', '\n', '\r', '\0']) >= 0)
            {
                Error = App.Text("AddHostingAccount.Error.Host");
                return false;
            }

            if (_provider.NeedEmail && string.IsNullOrEmpty(email))
            {
                Error = App.Text("AddHostingAccount.Error.Email");
                return false;
            }

            if (string.IsNullOrEmpty(token) || token.IndexOfAny(['\n', '\r', '\0']) >= 0)
            {
                Error = App.Text("AddHostingAccount.Error.Token");
                return false;
            }

            IsBusy = true;

            try
            {
                // 1. Verify the token and get the user it belongs to.
                Models.HostingAccount account;
                try
                {
                    account = await Models.HostingAPI.QueryAccountAsync(_provider, host, email, token);
                }
                catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    Error = App.Text("AddHostingAccount.Error.InvalidToken", _provider.Name);
                    return false;
                }
                catch (Exception e)
                {
                    Error = App.Text("AddHostingAccount.Error.Query", e.Message);
                    return false;
                }

                if (_editing != null && !_editing.Username.Equals(account.Username, StringComparison.Ordinal))
                {
                    Error = App.Text("AddHostingAccount.Error.UserMismatch", _editing.Username, account.Username);
                    return false;
                }

                // 2. Save the token with the git credential helper, then read it back to make sure it was really stored.
                var credential = new Commands.Credential(host, account.Username);
                var approve = await credential.ApproveAsync(token);
                if (!approve.IsSuccess)
                {
                    Error = App.Text("AddHostingAccount.Error.NotStored", approve.StdErr);
                    return false;
                }

                var stored = await new Commands.Credential(host, account.Username).FillAsync();
                if (!token.Equals(stored, StringComparison.Ordinal))
                {
                    Error = App.Text("AddHostingAccount.Error.NotStored", approve.StdErr);
                    return false;
                }

                // 3. Remember the account (without token).
                var accounts = Preferences.Instance.HostingAccounts;
                var idx = -1;
                for (var i = 0; i < accounts.Count; i++)
                {
                    if (accounts[i].IsSameAs(account))
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx >= 0)
                {
                    account.SSHKey = accounts[idx].SSHKey;
                    accounts[idx] = account;
                }
                else
                    accounts.Add(account);

                Preferences.Instance.Save();
                Result = account;
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string NormalizeHost(string host)
        {
            host = host?.Trim() ?? string.Empty;
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(8);
            else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(7);

            return host.TrimEnd('/').ToLowerInvariant();
        }

        private readonly Models.HostingAccount _editing = null;
        private Models.HostingProvider _provider = null;
        private string _host = string.Empty;
        private string _email = string.Empty;
        private string _token = string.Empty;
        private bool _isBusy = false;
        private string _error = string.Empty;
    }
}
