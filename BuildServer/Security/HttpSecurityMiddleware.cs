using BuildServer;

namespace BuildServer.Security;

public static class HttpSecurityMiddleware
{
    public static void UseBuildServerSecurity(this WebApplication app)
    {
        BuildServerOptions options = app.Services.GetRequiredService<BuildServerOptions>();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "same-origin");
            context.Response.Headers.TryAdd(
                "Content-Security-Policy",
                "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");

            if (!IsSafeMethod(context.Request.Method) && HasCrossSiteOrigin(context.Request, options))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Cross-site request rejected.");
                return;
            }

            await next();
        });
    }

    private static bool IsSafeMethod(string method)
    {
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
    }

    private static bool HasCrossSiteOrigin(HttpRequest request, BuildServerOptions options)
    {
        string origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri))
        {
            return true;
        }

        if (IsConfiguredAllowedOrigin(originUri, options))
        {
            return false;
        }

        HostString host = request.Host;
        bool sameHost = string.Equals(originUri.Host, host.Host, StringComparison.OrdinalIgnoreCase);
        int requestPort = host.Port ?? (request.IsHttps ? 443 : 80);
        int originPort = originUri.IsDefaultPort ? (originUri.Scheme == "https" ? 443 : 80) : originUri.Port;
        bool samePort = requestPort == originPort;
        bool sameScheme = string.Equals(originUri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase);
        return !(sameHost && samePort && sameScheme);
    }

    private static bool IsConfiguredAllowedOrigin(Uri originUri, BuildServerOptions options)
    {
        IEnumerable<string> origins = options.AllowedOrigins;
        if (!string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            origins = origins.Append(options.PublicBaseUrl);
        }

        foreach (string configuredOrigin in origins)
        {
            if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out Uri? allowedUri))
            {
                continue;
            }

            if (string.Equals(originUri.Scheme, allowedUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(originUri.Host, allowedUri.Host, StringComparison.OrdinalIgnoreCase) &&
                EffectivePort(originUri) == EffectivePort(allowedUri))
            {
                return true;
            }
        }

        return false;
    }

    private static int EffectivePort(Uri uri)
    {
        return uri.IsDefaultPort ? (uri.Scheme == "https" ? 443 : 80) : uri.Port;
    }
}
