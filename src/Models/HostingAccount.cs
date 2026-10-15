using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.Models
{
    public enum HostingProviderType
    {
        GitHub = 0,
        GitLab,
        Bitbucket,
    }

    public class HostingProvider(HostingProviderType type, string name, string defaultHost, bool allowCustomHost, bool needEmail, string scopes, string sshKeyScopes)
    {
        public HostingProviderType Type { get; } = type;
        public string Name { get; } = name;
        public string DefaultHost { get; } = defaultHost;
        public bool AllowCustomHost { get; } = allowCustomHost;
        public bool NeedEmail { get; } = needEmail;
        public string Scopes { get; } = scopes;
        public string SSHKeyScopes { get; } = sshKeyScopes;

        public static readonly List<HostingProvider> Supported = [
            new HostingProvider(HostingProviderType.GitHub, "GitHub", "github.com", false, false, "repo, read:user, user:email", "write:public_key"),
            new HostingProvider(HostingProviderType.GitLab, "GitLab", "gitlab.com", false, false, "read_user, read_repository, write_repository", "api"),
            new HostingProvider(HostingProviderType.Bitbucket, "Bitbucket", "bitbucket.org", false, true, "read:user:bitbucket, read:repository:bitbucket, write:repository:bitbucket", "SSH keys (write)"),
        ];

        public static HostingProvider Get(HostingProviderType type)
        {
            return Supported.Find(x => x.Type == type) ?? Supported[0];
        }

        public string GetTokenCreationURL(string host)
        {
            return Type switch
            {
                HostingProviderType.GitHub => $"https://{host}/settings/tokens/new?description=SourceGit&scopes=repo,read:user,user:email",
                HostingProviderType.GitLab => $"https://{host}/-/user_settings/personal_access_tokens?name=SourceGit&scopes=read_user,read_repository,write_repository",
                _ => "https://id.atlassian.com/manage-profile/security/api-tokens",
            };
        }
    }

    /// <summary>
    ///     An account of a git hosting service. The token itself is NEVER stored here, it is kept by the git credential
    ///     helper (e.g. Git Credential Manager) under `https://{Username}@{Host}`.
    /// </summary>
    public class HostingAccount : ObservableObject
    {
        public HostingProviderType Provider { get; set; } = HostingProviderType.GitHub;
        public string Host { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string AvatarURL { get; set; } = string.Empty;

        /// <summary>
        ///     Private SSH key used for the SSH remotes of this host (when the remote has no key of its own).
        /// </summary>
        public string SSHKey
        {
            get => _sshKey;
            set
            {
                // `null` is pushed back by the ComboBox when its selection is cleared, use `string.Empty` to clear the key.
                if (value != null)
                    SetProperty(ref _sshKey, value);
            }
        }

        [JsonIgnore]
        public string ProviderName
        {
            get => HostingProvider.Get(Provider).Name;
        }

        [JsonIgnore]
        public string Name
        {
            get => string.IsNullOrEmpty(DisplayName) ? Username : DisplayName;
        }

        [JsonIgnore]
        public string Description
        {
            get => $"{Username}@{Host}";
        }

        public bool IsSameAs(HostingAccount other)
        {
            return Provider == other.Provider &&
                Host.Equals(other.Host, StringComparison.OrdinalIgnoreCase) &&
                Username.Equals(other.Username, StringComparison.Ordinal);
        }

        private string _sshKey = string.Empty;
    }
}
