using Microsoft.AspNetCore.Diagnostics;

namespace BuildServer;

public static class ApiDiagnostics
{
    public const string RequestIdHeader = "X-Request-Id";

    public static void UseApiDiagnostics(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            string requestId = ResolveRequestId(context);
            context.TraceIdentifier = requestId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;
                return Task.CompletedTask;
            });

            await next();

            if (ShouldWriteStatusProblem(context))
            {
                await Problem(
                    context,
                    context.Response.StatusCode,
                    DefaultTitle(context.Response.StatusCode),
                    DefaultDetail(context.Response.StatusCode),
                    DefaultCode(context.Response.StatusCode)).ExecuteAsync(context);
            }
        });
    }

    public static async Task WriteExceptionAsync(HttpContext context)
    {
        Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        int statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            FileNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        await Problem(
            context,
            statusCode,
            DefaultTitle(statusCode),
            statusCode == StatusCodes.Status500InternalServerError ? "服务器内部错误。" : exception?.Message,
            DefaultCode(statusCode)).ExecuteAsync(context);
    }

    public static IResult ClientError(HttpContext context, Exception exception)
    {
        return Problem(
            context,
            StatusCodes.Status400BadRequest,
            "请求参数无效",
            exception.Message,
            "bad_request");
    }

    public static IResult Unauthorized(HttpContext context, string? detail = null)
    {
        return Problem(
            context,
            StatusCodes.Status401Unauthorized,
            "未登录或认证失败",
            detail ?? "请重新登录后再操作。",
            "unauthorized");
    }

    public static IResult Forbidden(HttpContext context, string? detail = null)
    {
        return Problem(
            context,
            StatusCodes.Status403Forbidden,
            "没有权限",
            detail ?? "当前账号没有执行此操作的权限。",
            "forbidden");
    }

    public static IResult NotFound(HttpContext context, string? detail = null)
    {
        return Problem(
            context,
            StatusCodes.Status404NotFound,
            "资源不存在",
            detail ?? "请求的资源不存在或已经被删除。",
            "not_found");
    }

    public static IResult Problem(HttpContext context, int statusCode, string title, string? detail, string code)
    {
        return Results.Problem(
            detail: detail,
            instance: context.Request.Path,
            statusCode: statusCode,
            title: title,
            type: $"https://httpstatuses.com/{statusCode}",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["code"] = code
            });
    }

    private static string ResolveRequestId(HttpContext context)
    {
        string? requestedId = context.Request.Headers[RequestIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requestedId) && requestedId.Length <= 128)
        {
            return requestedId;
        }

        return context.TraceIdentifier;
    }

    private static bool ShouldWriteStatusProblem(HttpContext context)
    {
        return context.Response.StatusCode >= 400 &&
               !context.Response.HasStarted &&
               string.IsNullOrWhiteSpace(context.Response.ContentType) &&
               context.Response.ContentLength is null;
    }

    private static string DefaultTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "请求参数无效",
            StatusCodes.Status401Unauthorized => "未登录或认证失败",
            StatusCodes.Status403Forbidden => "没有权限",
            StatusCodes.Status404NotFound => "资源不存在",
            StatusCodes.Status429TooManyRequests => "请求过于频繁",
            _ => statusCode >= 500 ? "服务器内部错误" : "请求失败"
        };
    }

    private static string DefaultDetail(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status401Unauthorized => "请重新登录后再操作。",
            StatusCodes.Status403Forbidden => "当前账号没有执行此操作的权限。",
            StatusCodes.Status404NotFound => "请求的资源不存在或已经被删除。",
            _ => "请求没有成功完成。"
        };
    }

    private static string DefaultCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "bad_request",
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status404NotFound => "not_found",
            StatusCodes.Status429TooManyRequests => "rate_limited",
            _ => statusCode >= 500 ? "server_error" : "request_failed"
        };
    }
}
