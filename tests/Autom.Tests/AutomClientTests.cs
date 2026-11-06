using System.Net;
using System.Text;
using System.Text.Json;
using Autom;
using Xunit;

namespace Autom.Tests;

public sealed class AutomClientTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Requests { get; } = new();

        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => Json(200, "{\"ok\":true}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request, body));
            return Responder(request);
        }
    }

    private static HttpResponseMessage Json(int status, string body) => new((HttpStatusCode)status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private readonly RecordingHandler _handler = new();
    private readonly AutomClient _client;

    public AutomClientTests()
    {
        _client = new AutomClient(new AutomClientOptions
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(_handler),
            RetryDelay = TimeSpan.FromMilliseconds(1),
        });
    }

    private AutomClient ClientWithBaseUrl(string baseUrl) =>
        new(new AutomClientOptions { ApiKey = "k", BaseUrl = baseUrl, HttpClient = new HttpClient(_handler) });

    [Fact]
    public void RequiresApiKey()
    {
        Environment.SetEnvironmentVariable("AUTOM_API_KEY", null);
        Assert.Throws<AutomConfigurationException>(() => new AutomClient());
        Assert.Throws<AutomConfigurationException>(() => new AutomClient(new AutomClientOptions { ApiKey = "   " }));
        Assert.Throws<AutomConfigurationException>(() => new AutomClient(new AutomClientOptions { ApiKey = "k", BaseUrl = "ftp://x" }));
    }

    [Fact]
    public async Task FeaturedOperation()
    {
        _handler.Responder = _ => Json(200, "{\"hello\":\"world\"}");
        var response = await _client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" });
        Assert.Equal("world", response.Json.GetProperty("hello").GetString());
        var request = _handler.Requests[0].Request;
        Assert.Equal("GET", request.Method.Method);
        Assert.Equal("https://api.autom.dev/v1/google/search", request.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.StartsWith("autom-dotnet/", string.Join(" ", request.Headers.GetValues("User-Agent")));
    }

    [Fact]
    public async Task QuerySerialization()
    {
        using var client = ClientWithBaseUrl("https://example.test/");
        await client.RequestAsync("GET", "/v1/anything", new Dictionary<string, object?>
        {
            ["text"] = "a b&c",
            ["flag"] = false,
            ["count"] = 0,
            ["ratio"] = 1.5,
            ["whole"] = 2.0,
            ["list"] = new[] { "x", "y" },
            ["nothing"] = null,
        });
        Assert.Equal(
            "https://example.test/v1/anything?text=a%20b%26c&flag=false&count=0&ratio=1.5&whole=2&list=x&list=y",
            _handler.Requests[0].Request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task PostSendsJsonBody()
    {
        await _client.RequestAsync("POST", "/v1/anything", new Dictionary<string, object?> { ["query"] = "x", ["page"] = 2, ["gone"] = null });
        var (request, body) = _handler.Requests[0];
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"query\":\"x\",\"page\":2}", body);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
    }

    [Fact]
    public async Task PostKeepsGlobalParametersInQuery()
    {
        await _client.RequestAsync("POST", "/v1/anything", new Dictionary<string, object?> { ["query"] = "x", ["is_async"] = true, ["fields"] = "a" });
        var (request, body) = _handler.Requests[0];
        Assert.Equal("{\"query\":\"x\"}", body);
        Assert.Equal("?is_async=true&fields=a", request.RequestUri!.Query);
    }

    [Fact]
    public async Task PathParameters()
    {
        await _client.RequestAsync("GET", "/v1/items/{id}", new Dictionary<string, object?> { ["id"] = "a/b c" });
        Assert.Equal("https://api.autom.dev/v1/items/a%2Fb%20c", _handler.Requests[0].Request.RequestUri!.AbsoluteUri);
        await Assert.ThrowsAsync<AutomSerializationException>(() => _client.RequestAsync("GET", "/v1/items/{id}"));
    }

    [Fact]
    public async Task UnsupportedMethod()
    {
        await Assert.ThrowsAsync<AutomConfigurationException>(() => _client.FinderGoogleCountriesAsync(new FinderGoogleCountriesParams { Query = "test" }, new RequestOptions { Method = "POST" }));
    }

    [Fact]
    public async Task DualOperationCanUsePost()
    {
        await _client.BingSearchAsync(new BingSearchParams { Query = "test" }, new RequestOptions { Method = "POST" });
        var (request, body) = _handler.Requests[0];
        Assert.Equal("POST", request.Method.Method);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(body!).RootElement.ValueKind);
    }

    [Fact]
    public async Task EveryEndpoint()
    {
        var calls = new Dictionary<string, Func<Task<ApiResponse>>>
        {
            ["bing_search"] = () => _client.BingSearchAsync(new BingSearchParams { Query = "test" }),
            ["brave_images"] = () => _client.BraveImagesAsync(new BraveImagesParams { Query = "test" }),
            ["brave_search"] = () => _client.BraveSearchAsync(new BraveSearchParams { Query = "test" }),
            ["brave_videos"] = () => _client.BraveVideosAsync(new BraveVideosParams { Query = "test" }),
            ["duckduckgo_search"] = () => _client.DuckduckgoSearchAsync(new DuckduckgoSearchParams { Query = "test" }),
            ["finder_google_countries"] = () => _client.FinderGoogleCountriesAsync(new FinderGoogleCountriesParams { Query = "test" }),
            ["finder_google_languages"] = () => _client.FinderGoogleLanguagesAsync(new FinderGoogleLanguagesParams { Query = "test" }),
            ["finder_google_locations"] = () => _client.FinderGoogleLocationsAsync(new FinderGoogleLocationsParams { Query = "test" }),
            ["google_images"] = () => _client.GoogleImagesAsync(new GoogleImagesParams { Query = "test" }),
            ["google_jobs"] = () => _client.GoogleJobsAsync(new GoogleJobsParams { Query = "developer" }),
            ["google_maps"] = () => _client.GoogleMapsAsync(new GoogleMapsParams { Query = "test" }),
            ["google_news"] = () => _client.GoogleNewsAsync(new GoogleNewsParams { Query = "test" }),
            ["google_search"] = () => _client.GoogleSearchAsync(new GoogleSearchParams { Query = "test" }),
            ["google_search_ai"] = () => _client.GoogleSearchAiAsync(new GoogleSearchAiParams { Query = "test" }),
            ["google_search_autocomplete"] = () => _client.GoogleSearchAutocompleteAsync(new GoogleSearchAutocompleteParams { Query = "test" }),
            ["google_search_light"] = () => _client.GoogleSearchLightAsync(new GoogleSearchLightParams { Query = "test" }),
            ["google_shopping"] = () => _client.GoogleShoppingAsync(new GoogleShoppingParams { Query = "test" }),
            ["google_videos"] = () => _client.GoogleVideosAsync(new GoogleVideosParams { Query = "test", Page = 1 }),
            ["jobs"] = () => _client.JobsAsync(new JobsParams { Id = "test" }),
            ["search"] = () => _client.SearchAsync(new SearchParams { Query = "test" }),
            ["stats"] = () => _client.StatsAsync(new StatsParams()),
            ["usage"] = () => _client.UsageAsync(),
            ["yahoo_search"] = () => _client.YahooSearchAsync(new YahooSearchParams { Query = "test" }),
        };
        foreach (var (name, endpoint) in Endpoints.All)
        {
            await calls[name]();
            var request = _handler.Requests[^1].Request;
            Assert.Equal("https://api.autom.dev" + endpoint.Path.Replace("{id}", "test"), request.RequestUri!.GetLeftPart(UriPartial.Path));
            Assert.Equal(endpoint.Methods[0], request.Method.Method);
        }
        Assert.Equal(Endpoints.All.Count, calls.Count);
    }

    [Fact]
    public async Task BinaryResponse()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) { Headers = { { "Content-Type", "image/png" } } },
        };
        var response = await _client.RequestAsync("GET", "/v1/anything");
        Assert.False(response.IsJson);
        Assert.Equal(4, response.Bytes.Length);
        Assert.Equal("image/png", response.ContentType);
    }

    [Fact]
    public async Task NonJsonSuccessForJsonOperation()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>", Encoding.UTF8, "text/html") };
        await Assert.ThrowsAsync<AutomResponseDecodingException>(() => _client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" }));
    }

    [Fact]
    public async Task ApiError()
    {
        _handler.Responder = _ => Json(401, "{\"error\":\"Invalid API Key\"}");
        var error = await Assert.ThrowsAsync<AutomApiException>(() => _client.RequestAsync("GET", "/v1/usage"));
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("Invalid API Key", error.Message);
        Assert.Equal("Invalid API Key", error.ResponseBody!.Value.GetProperty("error").GetString());
    }

    [Fact]
    public async Task NonJsonErrorBody()
    {
        _handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("Bad gateway") };
        var error = await Assert.ThrowsAsync<AutomApiException>(
            () => _client.RequestAsync("GET", "/v1/usage", null, new RequestOptions { MaxRetries = 0 }));
        Assert.Equal(502, error.StatusCode);
        Assert.Equal("Bad gateway", Encoding.UTF8.GetString(error.RawResponse.Body));
    }

    [Fact]
    public async Task TransportError()
    {
        _handler.Responder = _ => throw new HttpRequestException("network down");
        await Assert.ThrowsAsync<AutomTransportException>(() => _client.RequestAsync("GET", "/v1/usage"));
    }

    [Fact]
    public async Task DeserializesIntoTypes()
    {
        _handler.Responder = _ => Json(200, "{\"value\":42}");
        var response = await _client.RequestAsync("GET", "/v1/usage");
        Assert.Equal(42, response.Deserialize<Payload>()!.Value);
    }

    private sealed record Payload(int Value);

    [Fact]
    public async Task RetriesThenSucceeds()
    {
        var calls = 0;
        _handler.Responder = _ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new HttpRequestException("reset");
            }
            if (calls == 2)
            {
                var busy = Json(429, "{}");
                busy.Headers.TryAddWithoutValidation("Retry-After", "0");
                return busy;
            }
            return calls == 3 ? Json(503, "{}") : Json(200, "{}");
        };
        var response = await _client.RequestAsync("GET", "/v1/usage");
        Assert.True(response.IsJson);
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task RetryLimitsAndClientErrors()
    {
        using var limited = new AutomClient(new AutomClientOptions
        {
            ApiKey = "k",
            HttpClient = new HttpClient(_handler),
            MaxRetries = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1),
        });
        _handler.Responder = _ => Json(503, "{}");
        await Assert.ThrowsAsync<AutomApiException>(() => limited.RequestAsync("GET", "/v1/usage"));
        Assert.Equal(3, _handler.Requests.Count);
        _handler.Responder = _ => Json(402, "{}");
        await Assert.ThrowsAsync<AutomApiException>(() => limited.RequestAsync("GET", "/v1/usage"));
        Assert.Equal(4, _handler.Requests.Count);
        _handler.Responder = _ => Json(500, "{}");
        await Assert.ThrowsAsync<AutomApiException>(
            () => limited.RequestAsync("GET", "/v1/usage", null, new RequestOptions { MaxRetries = 0 }));
        Assert.Equal(5, _handler.Requests.Count);
    }

    [Theory]
    [InlineData("Rate limit exceeded: You've exceeded the rate limit on your subscription", 2)]
    [InlineData("Rate limit exceeded for the API key: quota daily", 1)]
    [InlineData("Invalid API Key", 1)]
    public async Task RateLimitsAreRetriedButNotQuotas(string message, int expectedCalls)
    {
        var calls = 0;
        _handler.Responder = _ => ++calls == 1 ? Json(401, JsonSerializer.Serialize(new { error = message })) : Json(200, "{}");
        try
        {
            await _client.RequestAsync("GET", "/v1/usage");
        }
        catch (AutomApiException)
        {
        }
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public async Task AcceptsGlobalParameters()
    {
        await _client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api", Fields = "a,b.c", IsAsync = true, SoftFail = false });
        var query = _handler.Requests[0].Request.RequestUri!.Query;
        Assert.Contains("fields=a%2Cb.c", query);
        Assert.Contains("is_async=true", query);
        Assert.Contains("soft_fail=false", query);
    }

    [Fact]
    public async Task BatchKeepsOrderAndIsolatesFailures()
    {
        var running = 0;
        var peak = 0;
        var results = await _client.BatchAsync(Enumerable.Range(1, 6), async (n, ct) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await Task.Delay(5, ct);
            Interlocked.Decrement(ref running);
            if (n == 4)
            {
                throw new InvalidOperationException("boom");
            }
            return n * 10;
        }, concurrency: 2);
        Assert.Equal(2, peak);
        Assert.Equal(6, results.Count);
        Assert.False(results[3].Ok);
        Assert.Equal(60, results[5].Value);
        Assert.Equal(3, results[3].Index);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    [Fact]
    public async Task WaitForJob()
    {
        var statuses = new Queue<string>(new[] { "Queued", "Running", "CompletedJob" });
        _handler.Responder = _ => Json(200, "{\"status\":\"" + statuses.Dequeue() + "\",\"progress\":50}");
        var seen = 0;
        var job = await _client.WaitForJobAsync("abc", TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(1), _ => seen++);
        Assert.Equal("CompletedJob", job.Json.GetProperty("status").GetString());
        Assert.Equal(3, _handler.Requests.Count);
        Assert.Equal("/v1/jobs/abc", _handler.Requests[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal(3, seen);
    }

    [Fact]
    public async Task WaitForJobTimesOut()
    {
        _handler.Responder = _ => Json(200, "{\"status\":\"Running\"}");
        await Assert.ThrowsAsync<AutomJobTimeoutException>(
            () => _client.WaitForJobAsync("abc", TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(1)));
    }
}
