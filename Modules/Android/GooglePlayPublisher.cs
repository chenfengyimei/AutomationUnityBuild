using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class GooglePlayPublisher(BuildRunContext context)
{
    private const string AndroidPublisherScope = "https://www.googleapis.com/auth/androidpublisher";
    private static readonly Uri AndroidPublisherBaseUri = new("https://androidpublisher.googleapis.com/");
    private static readonly Uri AndroidPublisherUploadBaseUri = new("https://androidpublisher.googleapis.com/upload/");

    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private BuildLogger _logger => context.Logger;

    public async Task PublishAsync()
    {
        if (!_config.GooglePlayUploadEnabled)
        {
            _logger.Info("Google Play 上传: 关闭");
            return;
        }

        string packageName = _config.EffectiveGooglePlayPackageName();
        IReadOnlyList<UploadArtifact> artifacts = ResolveUploadArtifacts(requireFiles: !_options.DryRun);
        if (_options.DryRun)
        {
            _logger.Info($"[dry-run] Google Play: edits.insert package={packageName}");
            foreach (UploadArtifact artifact in artifacts)
            {
                _logger.Info($"[dry-run] Google Play: 上传 {artifact.Kind.ToUpperInvariant()} {artifact.Path}");
            }

            _logger.Info($"[dry-run] Google Play: tracks.update track={_config.GooglePlayTrack}, status={CanonicalReleaseStatus(_config.GooglePlayReleaseStatus)}");
            _logger.Info("[dry-run] Google Play: edits.commit");
            return;
        }

        GoogleServiceAccount serviceAccount = GoogleServiceAccount.Load(ResolveSecretPath(_config.GooglePlayServiceAccountJsonPath));
        using var httpClient = new HttpClient();
        string accessToken = await GoogleServiceAccountTokenProvider.GetAccessTokenAsync(httpClient, serviceAccount, AndroidPublisherScope);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        string? editId = null;
        try
        {
            editId = await CreateEditAsync(httpClient, packageName);
            List<string> versionCodes = [];
            foreach (UploadArtifact artifact in artifacts)
            {
                string versionCode = artifact.Kind == AndroidBuildFormats.Aab
                    ? await UploadBundleAsync(httpClient, packageName, editId, artifact.Path)
                    : await UploadApkAsync(httpClient, packageName, editId, artifact.Path);
                versionCodes.Add(versionCode);
            }

            await UpdateTrackAsync(httpClient, packageName, editId, versionCodes.Distinct(StringComparer.Ordinal).ToArray());
            await CommitEditAsync(httpClient, packageName, editId);
            _logger.Info($"Google Play 上传完成: package={packageName}, track={_config.GooglePlayTrack}, versionCodes={string.Join(",", versionCodes)}");
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(editId))
            {
                await TryDeleteEditAsync(httpClient, packageName, editId);
            }

            throw;
        }
    }

    private IReadOnlyList<UploadArtifact> ResolveUploadArtifacts(bool requireFiles)
    {
        var artifacts = new List<UploadArtifact>();
        if (AndroidBuildFormats.IncludesAab(_config.GooglePlayUploadArtifact))
        {
            if (requireFiles && !_config.ShouldBuildAab && !File.Exists(_paths.AabOutputPath))
            {
                throw new FileNotFoundException($"googlePlayUploadArtifact 包含 aab，但当前没有可上传的 AAB: {_paths.AabOutputPath}");
            }

            artifacts.Add(new UploadArtifact(AndroidBuildFormats.Aab, _paths.AabOutputPath));
        }

        if (AndroidBuildFormats.IncludesApk(_config.GooglePlayUploadArtifact))
        {
            if (requireFiles && !_config.ShouldBuildApk && !File.Exists(_paths.ApkOutputPath))
            {
                throw new FileNotFoundException($"googlePlayUploadArtifact 包含 apk，但当前没有可上传的 APK: {_paths.ApkOutputPath}");
            }

            artifacts.Add(new UploadArtifact(AndroidBuildFormats.Apk, _paths.ApkOutputPath));
        }

        if (!requireFiles)
        {
            return artifacts;
        }

        foreach (UploadArtifact artifact in artifacts)
        {
            if (!File.Exists(artifact.Path))
            {
                throw new FileNotFoundException($"Google Play 上传文件不存在: {artifact.Path}");
            }
        }

        return artifacts;
    }

    private async Task<string> CreateEditAsync(HttpClient httpClient, string packageName)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits";
        JsonNode json = await SendJsonAsync(httpClient, HttpMethod.Post, new Uri(AndroidPublisherBaseUri, url), content: new StringContent("{}", Encoding.UTF8, "application/json"));
        string editId = json?["id"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(editId))
        {
            throw new InvalidOperationException("Google Play edits.insert 没有返回 editId。");
        }

        _logger.Info($"Google Play edit 已创建: {editId}");
        return editId;
    }

    private async Task<string> UploadBundleAsync(HttpClient httpClient, string packageName, string editId, string aabPath)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/bundles?uploadType=media";
        JsonNode json = await UploadFileAsync(httpClient, new Uri(AndroidPublisherUploadBaseUri, url), aabPath, "application/octet-stream");
        string versionCode = ExtractVersionCode(json);
        _logger.Info($"Google Play AAB 上传完成: {Path.GetFileName(aabPath)}, versionCode={versionCode}");
        return versionCode;
    }

    private async Task<string> UploadApkAsync(HttpClient httpClient, string packageName, string editId, string apkPath)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/apks?uploadType=media";
        JsonNode json = await UploadFileAsync(httpClient, new Uri(AndroidPublisherUploadBaseUri, url), apkPath, "application/vnd.android.package-archive");
        string versionCode = ExtractVersionCode(json);
        _logger.Info($"Google Play APK 上传完成: {Path.GetFileName(apkPath)}, versionCode={versionCode}");
        return versionCode;
    }

    private async Task UpdateTrackAsync(HttpClient httpClient, string packageName, string editId, IReadOnlyList<string> versionCodes)
    {
        if (versionCodes.Count == 0)
        {
            throw new InvalidOperationException("没有可分配到 Google Play track 的 versionCode。");
        }

        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/tracks/{Uri.EscapeDataString(_config.GooglePlayTrack)}";
        JsonObject release = new()
        {
            ["status"] = CanonicalReleaseStatus(_config.GooglePlayReleaseStatus),
            ["versionCodes"] = new JsonArray(versionCodes.Select(code => JsonValue.Create(code)).ToArray())
        };

        if (!string.IsNullOrWhiteSpace(_config.GooglePlayReleaseName))
        {
            release["name"] = _config.GooglePlayReleaseName;
        }

        if (_config.GooglePlayUserFraction is not null)
        {
            release["userFraction"] = _config.GooglePlayUserFraction.Value;
        }

        JsonObject body = new()
        {
            ["track"] = _config.GooglePlayTrack,
            ["releases"] = new JsonArray(release)
        };

        await SendJsonAsync(httpClient, HttpMethod.Put, new Uri(AndroidPublisherBaseUri, url), JsonContent(body));
        _logger.Info($"Google Play track 已更新: {_config.GooglePlayTrack}, versionCodes={string.Join(",", versionCodes)}");
    }

    private async Task CommitEditAsync(HttpClient httpClient, string packageName, string editId)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}:commit";
        if (_config.GooglePlayChangesNotSentForReview)
        {
            url += "?changesNotSentForReview=true";
        }

        await SendJsonAsync(httpClient, HttpMethod.Post, new Uri(AndroidPublisherBaseUri, url), content: null);
        _logger.Info("Google Play edit 已提交。");
    }

    private async Task TryDeleteEditAsync(HttpClient httpClient, string packageName, string editId)
    {
        try
        {
            string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}";
            using HttpResponseMessage response = await httpClient.DeleteAsync(new Uri(AndroidPublisherBaseUri, url));
            if (response.IsSuccessStatusCode)
            {
                _logger.Warn($"Google Play edit 已回滚删除: {editId}");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Google Play edit 回滚失败，可能需要到 Play Console 检查 edit 状态: {ex.Message}");
        }
    }

    private async Task<JsonNode> UploadFileAsync(HttpClient httpClient, Uri uri, string path, string contentType)
    {
        await using FileStream stream = File.OpenRead(path);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await SendJsonAsync(httpClient, HttpMethod.Post, uri, content);
    }

    private async Task<JsonNode> SendJsonAsync(HttpClient httpClient, HttpMethod method, Uri uri, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = content
        };
        using HttpResponseMessage response = await httpClient.SendAsync(request);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Play API 请求失败({(int)response.StatusCode} {response.ReasonPhrase}): {SensitiveText.Redact(body)}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(body) ?? new JsonObject();
    }

    private static StringContent JsonContent(JsonNode node)
    {
        return new StringContent(node.ToJsonString(JsonOptions.IndentedCamelCase), Encoding.UTF8, "application/json");
    }

    private static string ExtractVersionCode(JsonNode json)
    {
        JsonNode? node = json["versionCode"];
        if (node is null)
        {
            throw new InvalidOperationException("Google Play 上传成功响应里没有 versionCode。");
        }

        return node.ToString();
    }

    private static string CanonicalReleaseStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "draft" => "draft",
            "inprogress" => "inProgress",
            "halted" => "halted",
            "completed" => "completed",
            _ => status.Trim()
        };
    }

    private static string ResolveSecretPath(string path)
    {
        string fullPath = Path.GetFullPath(PathTools.ExpandHome(path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Google Play Service Account JSON 不存在: {fullPath}");
        }

        return fullPath;
    }

    private sealed record UploadArtifact(string Kind, string Path);
}

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

internal static class GoogleServiceAccountTokenProvider
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
