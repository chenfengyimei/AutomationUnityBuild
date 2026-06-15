using System.Text.Json;
using System.Text.Json.Nodes;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

namespace BuildServer;

public static class McpEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/mcp", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        AuthService auth,
        JsonDatabase database,
        BuildQueueService queue)
    {
        JsonObject request = JsonNode.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync())?.AsObject()
            ?? throw new InvalidOperationException("无效 MCP 请求。");
        JsonNode? id = request["id"]?.DeepClone();
        string method = request["method"]?.GetValue<string>() ?? "";

        try
        {
            if (method == "initialize")
            {
                return Results.Json(Response(id, new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "AutomationUnityBuildIOS BuildServer",
                        ["version"] = "0.1.0"
                    },
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject()
                    }
                }));
            }

            (CurrentUser User, McpClientRecord Client)? mcp = await auth.GetMcpUserAsync(context);
            if (mcp is null)
            {
                return Results.Json(Error(id, -32001, "MCP Agent 未认证。请设置 X-Agent-Token。"));
            }

            return method switch
            {
                "tools/list" => Results.Json(Response(id, ListTools())),
                "tools/call" => Results.Json(Response(id, await CallToolAsync(request, database, queue, mcp.Value.User, mcp.Value.Client))),
                _ => Results.Json(Error(id, -32601, $"未知 MCP 方法: {method}"))
            };
        }
        catch (Exception ex)
        {
            return Results.Json(Error(id, -32000, ex.Message));
        }
    }

    private static JsonObject ListTools()
    {
        return new JsonObject
        {
            ["tools"] = new JsonArray
            {
                Tool("list_projects", "列出可用项目。"),
                Tool("list_configs", "列出项目下的打包配置。"),
                Tool("start_ios_build", "提交 Unity iOS 打包任务，默认建议 dryRun=true。"),
                Tool("get_build_status", "查询打包任务状态。"),
                Tool("tail_build_log", "读取打包任务最近日志。"),
                Tool("list_build_artifacts", "列出打包任务产物。")
            }
        };
    }

    private static async Task<JsonObject> CallToolAsync(
        JsonObject request,
        JsonDatabase database,
        BuildQueueService queue,
        CurrentUser user,
        McpClientRecord client)
    {
        JsonObject parameters = request["params"]?.AsObject() ?? [];
        string name = parameters["name"]?.GetValue<string>() ?? "";
        JsonObject arguments = parameters["arguments"]?.AsObject() ?? [];

        object result = name switch
        {
            "list_projects" => await database.ReadAsync(db => db.Projects
                .Where(project => project.Enabled && IsProjectAllowed(client, project.Id))
                .ToList()),
            "list_configs" => await database.ReadAsync(db => db.Configs
                .Where(config =>
                    config.Enabled &&
                    IsProjectAllowed(client, config.ProjectId) &&
                    (!arguments.TryGetPropertyValue("projectId", out JsonNode? projectId) || config.ProjectId == projectId?.GetValue<string>()))
                .ToList()),
            "start_ios_build" => await queue.EnqueueAsync(ParseStartBuild(arguments, client), user, BuildSources.Mcp, client),
            "get_build_status" => await database.ReadAsync<object>(db =>
            {
                BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == Required(arguments, "jobId"));
                return job is null || !IsProjectAllowed(client, job.ProjectId)
                    ? new { error = "job not found" }
                    : job;
            }),
            "tail_build_log" => await TailLogAsync(database, client, Required(arguments, "jobId"), arguments["lines"]?.GetValue<int>() ?? 200),
            "list_build_artifacts" => await database.ReadAsync(db =>
            {
                string jobId = Required(arguments, "jobId");
                BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == jobId);
                return job is null || !IsProjectAllowed(client, job.ProjectId)
                    ? new List<BuildArtifactRecord>()
                    : db.Artifacts.Where(artifact => artifact.JobId == jobId).ToList();
            }),
            _ => throw new InvalidOperationException($"未知 MCP 工具: {name}")
        };

        return ToolResult(result);
    }

    private static StartBuildRequest ParseStartBuild(JsonObject arguments, McpClientRecord client)
    {
        bool dryRun = arguments["dryRun"]?.GetValue<bool>() ?? !client.AllowFullBuild;
        return new StartBuildRequest(
            Required(arguments, "projectId"),
            Required(arguments, "configId"),
            arguments["branch"]?.GetValue<string>(),
            arguments["buildNumber"]?.GetValue<string>(),
            dryRun,
            arguments["skipGit"]?.GetValue<bool>() ?? false,
            arguments["skipUnity"]?.GetValue<bool>() ?? false,
            arguments["skipXcode"]?.GetValue<bool>() ?? false,
            arguments["allowNonMac"]?.GetValue<bool>() ?? dryRun,
            arguments["notes"]?.GetValue<string>());
    }

    private static async Task<string> TailLogAsync(JsonDatabase database, McpClientRecord client, string jobId, int lines)
    {
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null || !IsProjectAllowed(client, job.ProjectId) || !File.Exists(job.WorkerLogPath))
        {
            return "";
        }

        Queue<string> queue = new();
        foreach (string line in File.ReadLines(job.WorkerLogPath))
        {
            queue.Enqueue(line);
            while (queue.Count > Math.Clamp(lines, 20, 1000))
            {
                queue.Dequeue();
            }
        }

        return string.Join(Environment.NewLine, queue);
    }

    private static bool IsProjectAllowed(McpClientRecord client, string projectId)
    {
        return client.AllowedProjectIds.Count == 0 ||
               client.AllowedProjectIds.Contains(projectId, StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject Tool(string name, string description)
    {
        return new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = true
            }
        };
    }

    private static JsonObject ToolResult(object result)
    {
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = json
                }
            }
        };
    }

    private static JsonObject Response(JsonNode? id, JsonNode result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
    }

    private static JsonObject Error(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private static string Required(JsonObject arguments, string name)
    {
        string? value = arguments[name]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{name} 不能为空。")
            : value;
    }
}
