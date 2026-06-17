using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class GoogleServiceAccount
{
    [JsonPropertyName("client_email")]
    public string ClientEmail { get; set; } = "";

    [JsonPropertyName("private_key")]
    public string PrivateKey { get; set; } = "";

    [JsonPropertyName("token_uri")]
    public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";

    public static GoogleServiceAccount Load(string path)
    {
        GoogleServiceAccount? serviceAccount = JsonSerializer.Deserialize<GoogleServiceAccount>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (serviceAccount is null ||
            string.IsNullOrWhiteSpace(serviceAccount.ClientEmail) ||
            string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
        {
            throw new InvalidOperationException("Google Play Service Account JSON 缺少 client_email 或 private_key。");
        }

        if (string.IsNullOrWhiteSpace(serviceAccount.TokenUri))
        {
            serviceAccount.TokenUri = "https://oauth2.googleapis.com/token";
        }

        return serviceAccount;
    }
}
