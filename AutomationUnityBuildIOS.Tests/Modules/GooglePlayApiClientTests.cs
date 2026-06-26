using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public sealed class GooglePlayApiClientTests
{
    [Fact]
    public async Task UpdateTrackAsync_RetriesTransientResponse_AndOmitsUserFractionForCompletedRelease()
    {
        var handler = new CaptureHandler(
            () => new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("rate limited")
            },
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        using var httpClient = new HttpClient(handler);
        using BuildLogger logger = TestHelpers.CreateTestLogger();
        var client = new GooglePlayApiClient(httpClient, "secret-token", logger);

        await client.UpdateTrackAsync(
            "com.example.game",
            "edit-1",
            "production",
            ["123"],
            "completed",
            "1.2.3",
            userFraction: 0.25);

        Assert.Equal(2, handler.Bodies.Count);
        Assert.DoesNotContain("userFraction", handler.Bodies.Last(), StringComparison.Ordinal);
        Assert.All(handler.AuthorizationHeaders, header =>
        {
            Assert.NotNull(header);
            Assert.Equal("Bearer", header!.Scheme);
            Assert.Equal("secret-token", header.Parameter);
        });
    }

    [Fact]
    public async Task CommitEditAsync_WhenChangesNotSentForReviewIsUnsupported_RetriesWithoutQueryParameter()
    {
        var handler = new CaptureHandler(
            () => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""
                {
                  "error": {
                    "code": 400,
                    "message": "Changes are sent for review automatically. The query parameter changesNotSentForReview must not be set.",
                    "status": "INVALID_ARGUMENT"
                  }
                }
                """)
            },
            () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });

        using var httpClient = new HttpClient(handler);
        using BuildLogger logger = TestHelpers.CreateTestLogger();
        var client = new GooglePlayApiClient(httpClient, "secret-token", logger);

        await client.CommitEditAsync("com.example.game", "edit-1", changesNotSentForReview: true);

        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains("changesNotSentForReview=true", handler.RequestUris[0].Query, StringComparison.Ordinal);
        Assert.DoesNotContain("changesNotSentForReview", handler.RequestUris[1].Query, StringComparison.Ordinal);
    }

    private sealed class CaptureHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new(responses);

        public List<string> Bodies { get; } = [];
        public List<Uri> RequestUris { get; } = [];
        public List<AuthenticationHeaderValue?> AuthorizationHeaders { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            AuthorizationHeaders.Add(request.Headers.Authorization);
            Bodies.Add(request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return _responses.Count == 0
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }
                : _responses.Dequeue().Invoke();
        }
    }
}
