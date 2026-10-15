using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SourceGit.Models
{
    public record SSHKeyType(string Name, string Description, string Cmdline)
    {
        public static readonly List<SSHKeyType> Supported = [
            new SSHKeyType("ED25519", "Recommended", "-t ed25519"),
            new SSHKeyType("RSA", "4096 Bits", "-t rsa -b 4096"),
        ];
    }

    public class SSHKeyPair
    {
        public string PrivateKeyPath { get; set; }
        public string PublicKeyPath { get; set; }
        public string RawPublicKey { get; set; }
        public string Fingerprint { get; set; } = "--- (invalid)";
        public string Name => Path.GetFileName(PrivateKeyPath);

        public static string UserSSHDir
        {
            get => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        }

        /// <summary>
        ///     Finds all key pairs (`name` + `name.pub`) in `~/.ssh`, sorted by name.
        /// </summary>
        public static List<SSHKeyPair> ScanUserKeys()
        {
            var keys = new List<SSHKeyPair>();
            var sshDir = new DirectoryInfo(UserSSHDir);
            if (!sshDir.Exists)
                return keys;

            foreach (var file in sshDir.GetFiles("*.pub"))
            {
                var privateKeyPath = file.FullName.Substring(0, file.FullName.Length - 4);
                if (!File.Exists(privateKeyPath))
                    continue;

                try
                {
                    keys.Add(new(privateKeyPath, file.FullName));
                }
                catch
                {
                    // Ignore unreadable keys
                }
            }

            keys.Sort((l, r) => string.Compare(l.Name, r.Name, StringComparison.Ordinal));
            return keys;
        }

        public SSHKeyPair(string privateKey, string publicKey)
        {
            PrivateKeyPath = privateKey;
            PublicKeyPath = publicKey;
            RawPublicKey = File.ReadAllText(publicKey);

            var parts = RawPublicKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                return;

            try
            {
                var blob = Convert.FromBase64String(parts[1]);
                Fingerprint = "SHA256:" + Convert.ToBase64String(SHA256.HashData(blob)).TrimEnd('=');
            }
            catch
            {
                // Ignore errors and keep the fingerprint as invalid
            }
        }
    }
}
