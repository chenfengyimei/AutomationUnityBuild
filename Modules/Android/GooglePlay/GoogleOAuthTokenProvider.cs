using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutomationUnityBuildIOS;

internal static class GoogleOAuthTokenProvider
{
    public static async Task<string> GetAccessTokenAsync(HttpClient httpClient, GoogleServiceAccount serviceAccount, string scope)
    {
        string assertion = CreateJwtAssertion(serviceAccount, scope);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });

        using HttpResponseMessage response = await httpClient.PostAsync(serviceAccount.TokenUri, content);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"获取 Google OAuth Token 失败({(int)response.StatusCode} {response.ReasonPhrase}): {SensitiveText.Redact(body)}");
        }

        JsonNode json = JsonNode.Parse(body) ?? new JsonObject();
        string accessToken = json["access_token"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Google OAuth Token 响应里没有 access_token。");
        }

        return accessToken;
    }

    private static string CreateJwtAssertion(GoogleServiceAccount serviceAccount, string scope)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT"
        }));
        string payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = serviceAccount.ClientEmail,
            scope,
            aud = serviceAccount.TokenUri,
            iat = now,
            exp = now + 3600
        }));
        string signingInput = $"{header}.{payload}";

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(serviceAccount.PrivateKey);
        byte[] signature = rsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
