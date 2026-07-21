namespace AutomationUnityBuildIOS;

internal sealed class TiktokUploadService(BuildRunContext context)
{
    private BuildConfig Config => context.Config;
    private BuildPaths Paths => context.Paths;
    private BuildLogger Logger => context.Logger;
    private readonly HttpClient _httpClient = new();

    public async Task UploadAsync()
    {
        Logger.Info($"TikTok 小游戏上传: 已启用 appId={Config.TiktokAppId}");

        string webglDir = Paths.TiktokWebglOutputDirectory;
        if (!Directory.Exists(webglDir))
        {
            throw new DirectoryNotFoundException($"TikTok WebGL 输出目录不存在: {webglDir}");
        }

        string zipPath = Path.Combine(Paths.ArtifactsRunRoot, "tiktok-build.zip");
        Logger.Info($"正在打包 WebGL 产物: {zipPath}");
        if (File.Exists(zipPath)) File.Delete(zipPath);
        System.IO.Compression.ZipFile.CreateFromDirectory(webglDir, zipPath);
        Logger.Info($"TikTok 包体大小: {new FileInfo(zipPath).Length / 1024 / 1024} MB");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{Config.TiktokApiEndpoint}/api/v1/minigame/upload");
        request.Headers.Add("X-Tiktok-AppId", Config.TiktokAppId);
        request.Headers.Add("X-Tiktok-AccessToken", Config.TiktokAccessToken);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Config.TiktokAppId), "app_id");
        content.Add(new StringContent(Config.TiktokGameName), "game_name");
        using var fileStream = File.OpenRead(zipPath);
        content.Add(new StreamContent(fileStream), "file", "tiktok-build.zip");

        request.Content = content;
        Logger.Info("正在上传 TikTok 小游戏包体...");

        HttpResponseMessage response = await _httpClient.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            Logger.Info($"TikTok 小游戏上传成功: {responseBody}");
        }
        else
        {
            Logger.Error($"TikTok 小游戏上传失败 ({response.StatusCode}): {responseBody}");
            throw new InvalidOperationException($"TikTok 上传失败: HTTP {response.StatusCode}, {responseBody}");
        }
    }
}