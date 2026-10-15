using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SourceGit.Models
{
    public enum UploadSSHKeyResult
    {
        Uploaded,
        AlreadyExists,
    }

    /// <summary>
    ///     REST APIs of the supported git hosting services. Responses are read with `JsonDocument` so no extra types are
    ///     needed for the JSON source generator.
    /// </summary>
    public static class HostingAPI
    {
        public static async Task<HostingAccount> QueryAccountAsync(HostingProvider provider, string host, string email, string token)
        {
            using var client = CreateClient();
            using var req = CreateRequest(HttpMethod.Get, GetUserAPI(provider.Type, host), provider.Type, email, token);
            using var doc = await SendAsync(client, req);

            var root = doc.RootElement;
            var account = new HostingAccount() { Provider = provider.Type, Host = host };

            switch (provider.Type)
            {
                case HostingProviderType.GitHub:
                    account.Username = GetString(root, "login");
                    account.DisplayName = GetString(root, "name");
                    account.Email = GetString(root, "email");
                    account.AvatarURL = GetString(root, "avatar_url");
                    break;
                case HostingProviderType.GitLab:
                    account.Username = GetString(root, "username");
                    account.DisplayName = GetString(root, "name");
                    account.Email = GetString(root, "email");
                    if (string.IsNullOrEmpty(account.Email))
                        account.Email = GetString(root, "public_email");
                    account.AvatarURL = GetString(root, "avatar_url");
                    break;
                default:
                    account.Username = GetString(root, "username");
                    account.DisplayName = GetString(root, "display_name");
                    account.Email = email;
                    if (root.TryGetProperty("links", out var links) && links.TryGetProperty("avatar", out var avatar))
                        account.AvatarURL = GetString(avatar, "href");
                    break;
            }

            if (string.IsNullOrEmpty(account.Username))
                throw new Exception("No username in the response");

            return account;
        }

        public static async Task<UploadSSHKeyResult> UploadSSHKeyAsync(HostingAccount account, string token, string title, string publicKey)
        {
            using var client = CreateClient();

            string api;
            string body;
            switch (account.Provider)
            {
                case HostingProviderType.GitHub:
                    api = account.Host.Equals("github.com", StringComparison.Ordinal) ? "https://api.github.com/user/keys" : $"https://{account.Host}/api/v3/user/keys";
                    body = BuildJson(("title", title), ("key", publicKey));
                    break;
                case HostingProviderType.GitLab:
                    api = $"https://{account.Host}/api/v4/user/keys";
                    body = BuildJson(("title", title), ("key", publicKey));
                    break;
                default:
                    {
                        // Bitbucket only accepts the account UUID (or Atlassian account id) here, not the username.
                        using var userReq = CreateRequest(HttpMethod.Get, GetUserAPI(account.Provider, account.Host), account.Provider, account.Email, token);
                        using var user = await SendAsync(client, userReq);
                        var uuid = GetString(user.RootElement, "uuid");
                        if (string.IsNullOrEmpty(uuid))
                            throw new Exception("No uuid in the response");

                        api = $"https://api.bitbucket.org/2.0/users/{Uri.EscapeDataString(uuid)}/ssh-keys";
                        body = BuildJson(("label", title), ("key", publicKey));
                        break;
                    }
            }

            using var req = CreateRequest(HttpMethod.Post, api, account.Provider, account.Email, token);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await client.SendAsync(req);
            if (resp.IsSuccessStatusCode)
                return UploadSSHKeyResult.Uploaded;

            var detail = await resp.Content.ReadAsStringAsync();
            if ((resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) &&
                detail.Contains("already", StringComparison.OrdinalIgnoreCase))
                return UploadSSHKeyResult.AlreadyExists;

            if (detail.Length > 300)
                detail = detail.Substring(0, 300);

            throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase} {detail}".Trim(), null, resp.StatusCode);
        }

        private static string GetUserAPI(HostingProviderType provider, string host)
        {
            return provider switch
            {
                HostingProviderType.GitHub => host.Equals("github.com", StringComparison.Ordinal) ? "https://api.github.com/user" : $"https://{host}/api/v3/user",
                HostingProviderType.GitLab => $"https://{host}/api/v4/user",
                _ => "https://api.bitbucket.org/2.0/user",
            };
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SourceGit");
            return client;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, string api, HostingProviderType provider, string email, string token)
        {
            var req = new HttpRequestMessage(method, api);
            switch (provider)
            {
                case HostingProviderType.GitHub:
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    req.Headers.Accept.ParseAdd("application/vnd.github+json");
                    break;
                case HostingProviderType.GitLab:
                    req.Headers.Add("PRIVATE-TOKEN", token);
                    break;
                default:
                    // Bitbucket Cloud API tokens are verified with `email:token`, while git uses `username:token`.
                    var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{token}"));
                    req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
                    break;
            }

            return req;
        }

        private static async Task<JsonDocument> SendAsync(HttpClient client, HttpRequestMessage req)
        {
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}", null, resp.StatusCode);

            var json = await resp.Content.ReadAsStringAsync();
            return JsonDocument.Parse(json);
        }

        private static string BuildJson(params (string Key, string Value)[] fields)
        {
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (key, value) in fields)
                    writer.WriteString(key, value);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static string GetString(JsonElement elem, string name)
        {
            if (elem.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString() ?? string.Empty;

            return string.Empty;
        }
    }
}
