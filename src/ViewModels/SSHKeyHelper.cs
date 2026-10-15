using System.IO;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class SSHKeyHelper : ObservableObject
    {
        public AvaloniaList<Models.SSHKeyPair> Keys
        {
            get;
        }

        public Models.SSHKeyPair SelectedKey
        {
            get => _selectedKey;
            set => SetProperty(ref _selectedKey, value);
        }

        public SSHKeyGenerator Generator
        {
            get => _generator;
            private set => SetProperty(ref _generator, value);
        }

        public SSHKeyHelper()
        {
            Keys = new AvaloniaList<Models.SSHKeyPair>();

            var keys = Models.SSHKeyPair.ScanUserKeys();
            if (keys.Count > 0)
            {
                Keys.AddRange(keys);
                SelectedKey = keys[0];
            }
        }

        public void OpenGenerator()
        {
            Generator = new SSHKeyGenerator();
        }

        public void CloseGenerator()
        {
            Generator = null;
        }

        public void Generate()
        {
            var key = _generator.Run();
            if (key == null)
                return;

            Keys.Add(key);
            SelectedKey = key;
            Generator = null;
        }

        public void DeleteSelected()
        {
            var key = SelectedKey;
            if (key == null)
                return;

            try
            {
                if (File.Exists(key.PrivateKeyPath))
                    File.Delete(key.PrivateKeyPath);
                if (File.Exists(key.PublicKeyPath))
                    File.Delete(key.PublicKeyPath);

                var idx = Keys.IndexOf(key);
                if (idx >= 0)
                {
                    Keys.RemoveAt(idx);

                    if (Keys.Count == 0)
                        SelectedKey = null;
                    else if (idx > Keys.Count - 1)
                        SelectedKey = Keys[Keys.Count - 1];
                    else
                        SelectedKey = Keys[idx];
                }
            }
            catch
            {
                // Ignore any errors during deletion
            }
        }

        private Models.SSHKeyPair _selectedKey = null;
        private SSHKeyGenerator _generator = null;
    }
}
