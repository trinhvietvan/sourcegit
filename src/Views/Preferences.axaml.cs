using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceGit.Views
{
    public partial class Preferences : ChromelessWindow
    {
        public static readonly DirectProperty<Preferences, string> GitVersionProperty =
            AvaloniaProperty.RegisterDirect<Preferences, string>(
                nameof(GitVersion),
                static o => o.GitVersion);

        public string GitVersion
        {
            get => _gitVersion;
            set => SetAndRaise(GitVersionProperty, ref _gitVersion, value);
        }

        public static readonly DirectProperty<Preferences, bool> ShowGitVersionWarningProperty =
            AvaloniaProperty.RegisterDirect<Preferences, bool>(
                nameof(ShowGitVersionWarning),
                static o => o.ShowGitVersionWarning);

        public bool ShowGitVersionWarning
        {
            get => _showGitVersionWarning;
            set => SetAndRaise(ShowGitVersionWarningProperty, ref _showGitVersionWarning, value);
        }

        public string DefaultUser
        {
            get;
            set;
        }

        public string DefaultEmail
        {
            get;
            set;
        }

        public Models.CRLFMode CRLFMode
        {
            get;
            set;
        } = null;

        public bool EnablePruneOnFetch
        {
            get;
            set;
        }

        public bool EnableGPGCommitSigning
        {
            get;
            set;
        }

        public bool EnableGPGTagSigning
        {
            get;
            set;
        }

        public static readonly DirectProperty<Preferences, Models.GPGFormat> GPGFormatProperty =
            AvaloniaProperty.RegisterDirect<Preferences, Models.GPGFormat>(
                nameof(GPGFormat),
                static o => o.GPGFormat,
                static (o, v) => o.GPGFormat = v);

        public Models.GPGFormat GPGFormat
        {
            get => _gpgFormat;
            set => SetAndRaise(GPGFormatProperty, ref _gpgFormat, value);
        }

        public static readonly DirectProperty<Preferences, string> GPGExecutableFileProperty =
            AvaloniaProperty.RegisterDirect<Preferences, string>(
                nameof(GPGExecutableFile),
                static o => o.GPGExecutableFile,
                static (o, v) => o.GPGExecutableFile = v);

        public string GPGExecutableFile
        {
            get => _gpgExecutableFile;
            set => SetAndRaise(GPGExecutableFileProperty, ref _gpgExecutableFile, value);
        }

        public string GPGUserKey
        {
            get;
            set;
        }

        public bool EnableHTTPSSLVerify
        {
            get;
            set;
        } = false;

        public static readonly DirectProperty<Preferences, AI.Service> SelectedOpenAIServiceProperty =
            AvaloniaProperty.RegisterDirect<Preferences, AI.Service>(
                nameof(SelectedOpenAIService),
                static o => o.SelectedOpenAIService,
                static (o, v) => o.SelectedOpenAIService = v);

        public AI.Service SelectedOpenAIService
        {
            get => _selectedOpenAIService;
            set => SetAndRaise(SelectedOpenAIServiceProperty, ref _selectedOpenAIService, value);
        }

        public static readonly DirectProperty<Preferences, Models.HostingAccount> SelectedHostingAccountProperty =
            AvaloniaProperty.RegisterDirect<Preferences, Models.HostingAccount>(
                nameof(SelectedHostingAccount),
                static o => o.SelectedHostingAccount,
                static (o, v) => o.SelectedHostingAccount = v);

        public Models.HostingAccount SelectedHostingAccount
        {
            get => _selectedHostingAccount;
            set => SetAndRaise(SelectedHostingAccountProperty, ref _selectedHostingAccount, value);
        }

        public static readonly DirectProperty<Preferences, Models.CustomAction> SelectedCustomActionProperty =
            AvaloniaProperty.RegisterDirect<Preferences, Models.CustomAction>(
                nameof(SelectedCustomAction),
                static o => o.SelectedCustomAction,
                static (o, v) => o.SelectedCustomAction = v);

        public Models.CustomAction SelectedCustomAction
        {
            get => _selectedCustomAction;
            set => SetAndRaise(SelectedCustomActionProperty, ref _selectedCustomAction, value);
        }

        public AvaloniaList<string> AvailableSSHKeys
        {
            get;
        } = [];

        public Preferences()
        {
            var pref = ViewModels.Preferences.Instance;
            DataContext = pref;
            CloseOnESC = true;
            RefreshAvailableSSHKeys();

            if (pref.IsGitConfigured())
            {
                var config = new Commands.Config(null).ReadAll();

                if (config.TryGetValue("user.name", out var name))
                    DefaultUser = name;
                if (config.TryGetValue("user.email", out var email))
                    DefaultEmail = email;
                if (config.TryGetValue("user.signingkey", out var signingKey))
                    GPGUserKey = signingKey;
                if (config.TryGetValue("core.autocrlf", out var crlf))
                    CRLFMode = Models.CRLFMode.Supported.Find(x => x.Value == crlf);
                if (config.TryGetValue("fetch.prune", out var pruneOnFetch))
                    EnablePruneOnFetch = (pruneOnFetch == "true");
                if (config.TryGetValue("commit.gpgsign", out var gpgCommitSign))
                    EnableGPGCommitSigning = (gpgCommitSign == "true");
                if (config.TryGetValue("tag.gpgsign", out var gpgTagSign))
                    EnableGPGTagSigning = (gpgTagSign == "true");
                if (config.TryGetValue("gpg.format", out var gpgFormat))
                    GPGFormat = Models.GPGFormat.Supported.Find(x => x.Value == gpgFormat) ?? Models.GPGFormat.Supported[0];

                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    GPGExecutableFile = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;

                if (config.TryGetValue("http.sslverify", out var sslVerify))
                    EnableHTTPSSLVerify = sslVerify == "true";
                else
                    EnableHTTPSSLVerify = true;
            }

            UpdateGitVersion();
            InitializeComponent();
        }

        protected override async void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GPGFormatProperty)
            {
                var config = await new Commands.Config(null).ReadAllAsync();
                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    GPGExecutableFile = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    GPGExecutableFile = gpgProgram;
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (Design.IsDesignMode)
                return;

            var config = await new Commands.Config(null).ReadAllAsync();
            await SetIfChangedAsync(config, "user.name", DefaultUser, "");
            await SetIfChangedAsync(config, "user.email", DefaultEmail, "");
            await SetIfChangedAsync(config, "user.signingkey", GPGUserKey, "");
            await SetIfChangedAsync(config, "core.autocrlf", CRLFMode?.Value, null);
            await SetIfChangedAsync(config, "fetch.prune", EnablePruneOnFetch ? "true" : "false", "false");
            await SetIfChangedAsync(config, "commit.gpgsign", EnableGPGCommitSigning ? "true" : "false", "false");
            await SetIfChangedAsync(config, "tag.gpgsign", EnableGPGTagSigning ? "true" : "false", "false");
            await SetIfChangedAsync(config, "http.sslverify", EnableHTTPSSLVerify ? "" : "false", "");
            await SetIfChangedAsync(config, "gpg.format", GPGFormat.Value, "openpgp");

            if (!GPGFormat.Value.Equals("ssh", StringComparison.Ordinal))
            {
                var oldGPG = string.Empty;
                if (GPGFormat.Value == "openpgp" && config.TryGetValue("gpg.program", out var openpgp))
                    oldGPG = openpgp;
                else if (config.TryGetValue($"gpg.{GPGFormat.Value}.program", out var gpgProgram))
                    oldGPG = gpgProgram;

                bool changed = false;
                if (!string.IsNullOrEmpty(oldGPG))
                    changed = oldGPG != GPGExecutableFile;
                else if (!string.IsNullOrEmpty(GPGExecutableFile))
                    changed = true;

                if (changed)
                    await new Commands.Config(null).SetAsync($"gpg.{GPGFormat.Value}.program", GPGExecutableFile);
            }

            var preferences = ViewModels.Preferences.Instance;
            preferences.UpdateAvailableAIModels();
            preferences.Save();
        }

        private async void SelectThemeOverrideFile(object _, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Theme Overrides File") { Patterns = ["*.json"] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ThemeOverrides = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select theme override file: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private void OpenThemeRepository(object _, RoutedEventArgs e)
        {
            Native.OS.OpenBrowser($"https://github.com/sourcegit-scm/sourcegit-theme");
            e.Handled = true;
        }

        private async void SelectGitExecutable(object _, RoutedEventArgs e)
        {
            var pattern = OperatingSystem.IsWindows() ? "git.exe" : "git";
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("Git Executable") { Patterns = [pattern] }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                {
                    ViewModels.Preferences.Instance.GitInstallPath = selected[0].Path.LocalPath;
                    UpdateGitVersion();
                }
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select git executable: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void SelectDefaultCloneDir(object _, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions() { AllowMultiple = false };
            try
            {
                var selected = await StorageProvider.OpenFolderPickerAsync(options);
                if (selected.Count == 1)
                {
                    var folder = selected[0];
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    ViewModels.Preferences.Instance.GitDefaultCloneDir = folderPath;
                }
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select default clone directory: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void SelectGPGExecutable(object _, RoutedEventArgs e)
        {
            var patterns = new List<string>();
            if (OperatingSystem.IsWindows())
                patterns.Add($"{GPGFormat.Program}.exe");
            else
                patterns.Add(GPGFormat.Program);

            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType("GPG Program") { Patterns = patterns }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    GPGExecutableFile = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select gpg program: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void SelectShellOrTerminal(object _, RoutedEventArgs e)
        {
            var type = ViewModels.Preferences.Instance.ShellOrTerminalType;
            if (type == -1)
                return;

            var shell = Models.ShellOrTerminal.Supported[type];
            var options = new FilePickerOpenOptions() { AllowMultiple = false };
            if (shell.Type != "custom")
            {
                options = new FilePickerOpenOptions()
                {
                    FileTypeFilter = [new FilePickerFileType(shell.Name)
                    {
                        Patterns = shell.Exec.Split('|',
                            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    }],
                    AllowMultiple = false,
                };
            }

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ShellOrTerminalPath = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select shell/terminal: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private async void SelectExternalMergeTool(object _, RoutedEventArgs e)
        {
            var type = ViewModels.Preferences.Instance.ExternalMergeToolType;
            if (type <= 0 || type >= Models.ExternalMerger.Supported.Count)
            {
                ViewModels.Preferences.Instance.ExternalMergeToolType = 0;
                e.Handled = true;
                return;
            }

            var tool = Models.ExternalMerger.Supported[type];
            var options = new FilePickerOpenOptions()
            {
                FileTypeFilter = [new FilePickerFileType(tool.Name) { Patterns = tool.GetPatternsToFindExecFile() }],
                AllowMultiple = false,
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 })
                    ViewModels.Preferences.Instance.ExternalMergeToolPath = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select merge tool: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private static async Task SetIfChangedAsync(Dictionary<string, string> cached, string key, string value, string defValue)
        {
            bool changed = false;
            if (cached.TryGetValue(key, out var old))
                changed = old != value;
            else if (!string.IsNullOrEmpty(value) && value != defValue)
                changed = true;

            if (changed)
                await new Commands.Config(null).SetAsync(key, value);
        }

        private async void OnUseNativeWindowFrameChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox box)
            {
                ViewModels.Preferences.Instance.UseSystemWindowFrame = box.IsChecked == true;
                await this.ShowDialogAsync(new ConfirmRestart());
            }

            e.Handled = true;
        }

        private void OnGitInstallPathChanged(object sender, TextChangedEventArgs e)
        {
            UpdateGitVersion();
            ViewModels.EmbeddedGit.Instance.Refresh();
        }

        private async void OnUseEmbeddedGit(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await ViewModels.EmbeddedGit.Instance.UseEmbeddedGitAsync();
            UpdateGitVersion();
        }

        private async void OnUseSystemGit(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (!ViewModels.EmbeddedGit.Instance.UseSystemGit())
            {
                await new Alert().ShowAsync(this, App.Text("Preferences.Git.Embedded.SystemGitNotFound"), true);
                return;
            }

            UpdateGitVersion();
        }

        private void OnCancelEmbeddedGit(object sender, RoutedEventArgs e)
        {
            ViewModels.EmbeddedGit.Instance.Cancel();
            e.Handled = true;
        }

        private void OnAddOpenAIService(object sender, RoutedEventArgs e)
        {
            var service = new AI.Service() { Name = "Unnamed Service" };
            ViewModels.Preferences.Instance.OpenAIServices.Add(service);
            SelectedOpenAIService = service;

            e.Handled = true;
        }

        private void OnRemoveSelectedOpenAIService(object sender, RoutedEventArgs e)
        {
            if (SelectedOpenAIService == null)
                return;

            ViewModels.Preferences.Instance.OpenAIServices.Remove(SelectedOpenAIService);
            SelectedOpenAIService = null;
            e.Handled = true;
        }

        private async void OnAddHostingAccount(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var vm = new ViewModels.AddHostingAccount(null);
            await this.ShowDialogAsync(vm);
            if (vm.Result != null)
                SelectedHostingAccount = vm.Result;
        }

        private async void OnUpdateHostingAccountToken(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Button { DataContext: Models.HostingAccount account })
                return;

            var vm = new ViewModels.AddHostingAccount(account);
            await this.ShowDialogAsync(vm);
            if (vm.Result != null)
                SelectedHostingAccount = vm.Result;
        }

        private async void OnSelectHostingAccountSSHKey(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Button { DataContext: Models.HostingAccount account })
                return;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var startupDir = Path.Combine(home, ".ssh");
            if (!Directory.Exists(startupDir))
                startupDir = home;

            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new("SSHKey") { Patterns = ["*"] }],
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(startupDir),
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is not { Count: 1 })
                    return;

                var path = selected[0].Path.LocalPath;
                if (path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase))
                    path = path.Substring(0, path.Length - 4);

                if (!File.Exists(path) || !File.Exists(path + ".pub"))
                {
                    await new Alert().ShowAsync(this, App.Text("Preferences.Accounts.SSHKey.Error.NoPair", path), true);
                    return;
                }

                if (!AvailableSSHKeys.Contains(path))
                    AvailableSSHKeys.Add(path);

                account.SSHKey = path;
                ViewModels.Preferences.Instance.Save();
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select SSH key: {ex.Message}", true);
            }
        }

        private void OnClearHostingAccountSSHKey(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Button { DataContext: Models.HostingAccount account })
                return;

            account.SSHKey = string.Empty;
            ViewModels.Preferences.Instance.Save();
        }

        private async void OnUploadHostingAccountSSHKey(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not Button { DataContext: Models.HostingAccount account } button)
                return;

            var publicKeyFile = account.SSHKey + ".pub";
            if (!File.Exists(publicKeyFile))
            {
                await new Alert().ShowAsync(this, App.Text("Preferences.Accounts.SSHKey.Error.NoPair", account.SSHKey), true);
                return;
            }

            button.IsEnabled = false;
            try
            {
                var token = await new Commands.Credential(account.Host, account.Username).FillAsync();
                if (string.IsNullOrEmpty(token))
                {
                    await new Alert().ShowAsync(this, App.Text("Preferences.Accounts.SSHKey.Error.NoToken"), true);
                    return;
                }

                var publicKey = (await File.ReadAllTextAsync(publicKeyFile)).Trim();
                var title = $"SourceGit ({Environment.MachineName})";
                var rs = await Models.HostingAPI.UploadSSHKeyAsync(account, token, title, publicKey);
                var msg = rs == Models.UploadSSHKeyResult.AlreadyExists ?
                    App.Text("Preferences.Accounts.SSHKey.AlreadyExists", account.ProviderName) :
                    App.Text("Preferences.Accounts.SSHKey.Uploaded", account.ProviderName);
                await new Alert().ShowAsync(this, msg, false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                var provider = Models.HostingProvider.Get(account.Provider);
                await new Alert().ShowAsync(this, App.Text("Preferences.Accounts.SSHKey.Error.Scope", provider.SSHKeyScopes, ex.Message), true);
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, App.Text("Preferences.Accounts.SSHKey.Error.Upload", ex.Message), true);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async void OnOpenSSHKeyHelper(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var before = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in Models.SSHKeyPair.ScanUserKeys())
                before.Add(key.PrivateKeyPath);

            await new SSHKeyHelper() { DataContext = new ViewModels.SSHKeyHelper() }.ShowDialog(this);

            // Pick the newest key generated in the helper for the account it was opened from.
            string generated = null;
            var generatedTime = DateTime.MinValue;
            foreach (var key in Models.SSHKeyPair.ScanUserKeys())
            {
                if (before.Contains(key.PrivateKeyPath))
                    continue;

                var time = File.GetLastWriteTimeUtc(key.PrivateKeyPath);
                if (generated == null || time > generatedTime)
                {
                    generated = key.PrivateKeyPath;
                    generatedTime = time;
                }
            }

            RefreshAvailableSSHKeys();

            if (generated != null && sender is Button { DataContext: Models.HostingAccount account })
            {
                account.SSHKey = generated;
                ViewModels.Preferences.Instance.Save();
            }
        }

        private void RefreshAvailableSSHKeys()
        {
            // Keys used by accounts are kept even if they are not in `~/.ssh`. The list is updated in place (instead of
            // being replaced) so that the selection of the ComboBox is never reset.
            var paths = new List<string>();
            foreach (var key in Models.SSHKeyPair.ScanUserKeys())
                paths.Add(key.PrivateKeyPath);

            foreach (var account in ViewModels.Preferences.Instance.HostingAccounts)
            {
                if (!string.IsNullOrEmpty(account.SSHKey) && !paths.Contains(account.SSHKey))
                    paths.Add(account.SSHKey);
            }

            for (var i = AvailableSSHKeys.Count - 1; i >= 0; i--)
            {
                if (!paths.Contains(AvailableSSHKeys[i]))
                    AvailableSSHKeys.RemoveAt(i);
            }

            foreach (var path in paths)
            {
                if (!AvailableSSHKeys.Contains(path))
                    AvailableSSHKeys.Add(path);
            }
        }

        private async void OnRemoveSelectedHostingAccount(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var account = SelectedHostingAccount;
            if (account == null)
                return;

            var confirm = new Confirm();
            confirm.SetData(App.Text("Preferences.Accounts.ConfirmRemove", account.Description), Models.ConfirmButtonType.YesNo);
            if (!await confirm.ShowDialog<bool>(this))
                return;

            // Erase the token from the credential helper too, so it can not be used by git anymore.
            await new Commands.Credential(account.Host, account.Username).RejectAsync();

            var pref = ViewModels.Preferences.Instance;
            pref.HostingAccounts.Remove(account);
            pref.Save();
            SelectedHostingAccount = null;
        }

        private void OnAddCustomAction(object sender, RoutedEventArgs e)
        {
            var action = new Models.CustomAction() { Name = "Unnamed Action (Global)" };
            ViewModels.Preferences.Instance.CustomActions.Add(action);
            SelectedCustomAction = action;

            e.Handled = true;
        }

        private async void SelectExecutableForCustomAction(object sender, RoutedEventArgs e)
        {
            var options = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                FileTypeFilter = [new("Executable file(script)") { Patterns = ["*"] }]
            };

            try
            {
                var selected = await StorageProvider.OpenFilePickerAsync(options);
                if (selected is { Count: 1 } && sender is Button { DataContext: Models.CustomAction action })
                    action.Executable = selected[0].Path.LocalPath;
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, $"Failed to select executable for custom action: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private void OnRemoveSelectedCustomAction(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            ViewModels.Preferences.Instance.CustomActions.Remove(SelectedCustomAction);
            SelectedCustomAction = null;
            e.Handled = true;
        }

        private void OnMoveSelectedCustomActionUp(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            var idx = ViewModels.Preferences.Instance.CustomActions.IndexOf(SelectedCustomAction);
            if (idx > 0)
                ViewModels.Preferences.Instance.CustomActions.Move(idx - 1, idx);

            e.Handled = true;
        }

        private void OnMoveSelectedCustomActionDown(object sender, RoutedEventArgs e)
        {
            if (SelectedCustomAction == null)
                return;

            var idx = ViewModels.Preferences.Instance.CustomActions.IndexOf(SelectedCustomAction);
            if (idx < ViewModels.Preferences.Instance.CustomActions.Count - 1)
                ViewModels.Preferences.Instance.CustomActions.Move(idx + 1, idx);

            e.Handled = true;
        }

        private async void EditCustomActionControls(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: Models.CustomAction act })
                return;

            await this.ShowDialogAsync(new ViewModels.ConfigureCustomActionControls(act.Controls));
            e.Handled = true;
        }

        private void UpdateGitVersion()
        {
            GitVersion = Native.OS.GitVersionString;
            ShowGitVersionWarning = !string.IsNullOrEmpty(GitVersion) && Native.OS.GitVersion < Models.GitVersions.MINIMAL;
        }

        private string _gitVersion = string.Empty;
        private bool _showGitVersionWarning = false;
        private Models.GPGFormat _gpgFormat = Models.GPGFormat.Supported[0];
        private string _gpgExecutableFile = string.Empty;
        private AI.Service _selectedOpenAIService = null;
        private Models.HostingAccount _selectedHostingAccount = null;
        private Models.CustomAction _selectedCustomAction = null;
    }
}
