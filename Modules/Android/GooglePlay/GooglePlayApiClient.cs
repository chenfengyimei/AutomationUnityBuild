using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AutomationUnityBuildIOS;

internal sealed class GooglePlayApiClient
{
    private static readonly Uri AndroidPublisherBaseUri = new("https://androidpublisher.googleapis.com/");
    private static readonly Uri AndroidPublisherUploadBaseUri = new("https://androidpublisher.googleapis.com/upload/");

    private readonly HttpClient _httpClient;
    private readonly BuildLogger _logger;

    public GooglePlayApiClient(HttpClient httpClient, string accessToken, BuildLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<string> CreateEditAsync(string packageName)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits";
        JsonNode json = await SendJsonAsync(HttpMethod.Post, new Uri(AndroidPublisherBaseUri, url), new StringContent("{}", Encoding.UTF8, "application/json"));
        string editId = json?["id"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(editId))
        {
            throw new InvalidOperationException("Google Play edits.insert 没有返回 editId。");
        }

        _logger.Info($"Google Play edit 已创建: {editId}");
        return editId;
    }

    public async Task<string> UploadBundleAsync(string packageName, string editId, string aabPath)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/bundles?uploadType=media";
        JsonNode json = await UploadFileAsync(new Uri(AndroidPublisherUploadBaseUri, url), aabPath, "application/octet-stream");
        string versionCode = ExtractVersionCode(json);
        _logger.Info($"Google Play AAB 上传完成: {Path.GetFileName(aabPath)}, versionCode={versionCode}");
        return versionCode;
    }

    public async Task<string> UploadApkAsync(string packageName, string editId, string apkPath)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/apks?uploadType=media";
        JsonNode json = await UploadFileAsync(new Uri(AndroidPublisherUploadBaseUri, url), apkPath, "application/vnd.android.package-archive");
        string versionCode = ExtractVersionCode(json);
        _logger.Info($"Google Play APK 上传完成: {Path.GetFileName(apkPath)}, versionCode={versionCode}");
        return versionCode;
    }

    public async Task UpdateTrackAsync(
        string packageName,
        string editId,
        string track,
        IReadOnlyList<string> versionCodes,
        string releaseStatus,
        string releaseName,
        double? userFraction)
    {
        if (versionCodes.Count == 0)
        {
            throw new InvalidOperationException("没有可分配到 Google Play track 的 versionCode。");
        }

        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}/tracks/{Uri.EscapeDataString(track)}";
        JsonObject release = new()
        {
            ["status"] = GooglePlayReleaseStatus.Normalize(releaseStatus),
            ["versionCodes"] = new JsonArray(versionCodes.Select(code => JsonValue.Create(code)).ToArray())
        };

        if (!string.IsNullOrWhiteSpace(releaseName))
        {
            release["name"] = releaseName;
        }

        if (userFraction is not null)
        {
            release["userFraction"] = userFraction.Value;
        }

        JsonObject body = new()
        {
            ["track"] = track,
            ["releases"] = new JsonArray(release)
        };

        await SendJsonAsync(HttpMethod.Put, new Uri(AndroidPublisherBaseUri, url), JsonContent(body));
        _logger.Info($"Google Play track 已更新: {track}, versionCodes={string.Join(",", versionCodes)}");
    }

    public async Task CommitEditAsync(string packageName, string editId, bool changesNotSentForReview)
    {
        string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}:commit";
        if (changesNotSentForReview)
        {
            url += "?changesNotSentForReview=true";
        }

        await SendJsonAsync(HttpMethod.Post, new Uri(AndroidPublisherBaseUri, url), content: null);
        _logger.Info("Google Play edit 已提交。");
    }

    public async Task TryDeleteEditAsync(string packageName, string editId)
    {
        try
        {
            string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}";
            using HttpResponseMessage response = await _httpClient.DeleteAsync(new Uri(AndroidPublisherBaseUri, url));
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

    private async Task<JsonNode> UploadFileAsync(Uri uri, string path, string contentType)
    {
        await using FileStream stream = File.OpenRead(path);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return await SendJsonAsync(HttpMethod.Post, uri, content);
    }

    private async Task<JsonNode> SendJsonAsync(HttpMethod method, Uri uri, HttpContent? content)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = content
        };
        using HttpResponseMessage response = await _httpClient.SendAsync(request);
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
}
