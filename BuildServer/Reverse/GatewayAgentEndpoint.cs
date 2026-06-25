using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Reverse;

public static class GatewayAgentEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/gateway-agent/status", GetStatusAsync);
        app.MapPost("/api/gateway-agent/connect", ConnectAsync);
        app.MapPost("/api/gateway-agent/disconnect", DisconnectAsync);
        app.MapGet("/api/gateway-agent/settings", GetSettingsAsync);
        app.MapPut("/api/gateway-agent/settings", UpdateSettingsAsync);
    }

    private static async Task<IResult> GetStatusAsync(HttpContext context, AuthService auth, GatewayAgentService agent)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(agent.GetStatus());
    }

    private static async Task<IResult> ConnectAsync(
        ConnectRequest request,
        HttpContext context,
        AuthService auth,
        GatewayAgentService agent)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        try
        {
            ConnectResult result = await agent.ConnectAsync(request.GatewayUrl, request.EnrollmentToken, request.AutoConnect);
            return Results.Ok(new { nodeId = result.NodeId, nodeName = result.NodeName, status = agent.GetStatus() });
        }
        catch (Exception ex)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DisconnectAsync(
        HttpContext context,
        AuthService auth,
        GatewayAgentService agent)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        await agent.DisconnectAsync();
        return Results.Ok(new { ok = true, status = agent.GetStatus() });
    }

    private static async Task<IResult> GetSettingsAsync(
        HttpContext context,
        AuthService auth,
        AgentCredentialStore credentialStore)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();

        AgentCredential? cred = await credentialStore.LoadAsync();
        return Results.Ok(new
        {
            gatewayUrl = cred?.GatewayUrl ?? "",
            nodeId = cred?.NodeId ?? "",
            autoConnect = cred?.AutoConnect ?? false,
            enrolled = cred is not null
        });
    }

    private static async Task<IResult> UpdateSettingsAsync(
        UpdateSettingsRequest request,
        HttpContext context,
        AuthService auth,
        AgentCredentialStore credentialStore)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        await credentialStore.UpdateAutoConnectAsync(request.AutoConnect);
        return Results.Ok(new { ok = true });
    }
}

public sealed record ConnectRequest(string GatewayUrl, string EnrollmentToken, bool AutoConnect = true);

public sealed record UpdateSettingsRequest(bool AutoConnect);
