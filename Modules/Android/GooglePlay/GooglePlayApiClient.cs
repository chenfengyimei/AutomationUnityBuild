using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AutomationUnityBuildIOS;

internal sealed class GooglePlayApiClient
{
    private static readonly Uri AndroidPublisherBaseUri = new("https://androidpublisher.googleapis.com/");
    private static readonly Uri AndroidPublisherUploadBaseUri = new("https://androidpublisher.googleapis.com/upload/");

    private readonly HttpClient _httpClient;
    private readonly string _accessToken;
    private readonly BuildLogger _logger;

    public GooglePlayApiClient(HttpClient httpClient, string accessToken, BuildLogger logger)
    {
        _httpClient = httpClient;
        _accessToken = accessToken;
        _logger = logger;
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

        if (userFraction is not null && ReleaseStatusAllowsUserFraction(release["status"]!.GetValue<string>()))
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
            Uri reviewDeferredUri = new(AndroidPublisherBaseUri, url + "?changesNotSentForReview=true");
            GooglePlayApiError? error = await TrySendJsonAsync(HttpMethod.Post, reviewDeferredUri, content: null);
            if (error is null)
            {
                _logger.Info("Google Play edit 已提交。");
                return;
            }

            if (!error.IsChangesNotSentForReviewUnsupported)
            {
                throw error.ToException();
            }

            _logger.Warn("Google Play 当前应用会自动送审，API 不允许设置 changesNotSentForReview；已自动改用普通提交。");
        }

        await SendJsonAsync(HttpMethod.Post, new Uri(AndroidPublisherBaseUri, url), content: null);
        _logger.Info("Google Play edit 已提交。");
    }

    public async Task TryDeleteEditAsync(string packageName, string editId)
    {
        try
        {
            string url = $"androidpublisher/v3/applications/{Uri.EscapeDataString(packageName)}/edits/{Uri.EscapeDataString(editId)}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(AndroidPublisherBaseUri, url));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using HttpResponseMessage response = await _httpClient.SendAsync(request);
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
        using HttpResponseMessage response = await SendWithRetryAsync(method, uri, content);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw GooglePlayApiError.Create(response, body).ToException();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(body) ?? new JsonObject();
    }

    private async Task<GooglePlayApiError?> TrySendJsonAsync(HttpMethod method, Uri uri, HttpContent? content)
    {
        using HttpResponseMessage response = await SendWithRetryAsync(method, uri, content);
        string body = await response.Content.ReadAsStringAsync();
        return response.IsSuccessStatusCode
            ? null
            : GooglePlayApiError.Create(response, body);
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpMethod method, Uri uri, HttpContent? content)
    {
        const int maxAttempts = 3;
        byte[]? bufferedContent = content is null ? null : await content.ReadAsByteArrayAsync();
        string? mediaType = content?.Headers.ContentType?.MediaType;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            if (bufferedContent is not null)
            {
                request.Content = new ByteArrayContent(bufferedContent);
                if (!string.IsNullOrWhiteSpace(mediaType))
                {
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
                }
            }

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (attempt < maxAttempts && IsTransient(response))
                {
                    response.Dispose();
                    await Task.Delay(BackoffDelay(attempt));
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
                await Task.Delay(BackoffDelay(attempt));
            }
        }

        throw lastException ?? new HttpRequestException("Google Play API request failed.");
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

    private static bool ReleaseStatusAllowsUserFraction(string status)
    {
        return status is "inProgress" or "halted";
    }

    private static bool IsTransient(HttpResponseMessage response)
    {
        int status = (int)response.StatusCode;
        return status == 429 || status >= 500;
    }

    private static TimeSpan BackoffDelay(int attempt)
    {
        return TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt - 1));
    }

    private sealed record GooglePlayApiError(int StatusCode, string? ReasonPhrase, string Body)
    {
        public bool IsChangesNotSentForReviewUnsupported =>
            StatusCode == 400 &&
            Body.Contains("changesNotSentForReview must not be set", StringComparison.OrdinalIgnoreCase);

        public InvalidOperationException ToException()
        {
            return new InvalidOperationException(
                $"Google Play API 请求失败({StatusCode} {ReasonPhrase}): {SensitiveText.Redact(Body)}");
        }

        public static GooglePlayApiError Create(HttpResponseMessage response, string body)
        {
            return new GooglePlayApiError((int)response.StatusCode, response.ReasonPhrase, body);
        }
    }
}
