# Autom .NET SDK

The official .NET SDK for the [Autom](https://www.autom.dev) SERP API.

Google, Bing and Brave search results as structured JSON — web, maps, news, shopping, images, jobs and AI Mode.

The SDK exposes **23 typed async methods**, one per documented endpoint (with `required` properties checked at compile time), plus a generic `RequestAsync`.

## Requirements

- .NET 8 or newer

## Installation

```sh
dotnet add package Autom
```

## Get your API key

[Sign up for Autom](https://app.autom.dev/register) or sign in, then copy your API key from the [dashboard](https://app.autom.dev). `new AutomClient()` reads the `AUTOM_API_KEY` environment variable.

## Quick start

```csharp
using Autom;

using var client = new AutomClient();
var response = await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" });
Console.WriteLine(response.Json);
```

Parameter classes mirror the [API reference](https://docs.autom.dev). Use `response.Deserialize<MyType>()` to map the JSON onto your own types.

## Async jobs

Any call accepts `is_async: true` (useful past the 60-second server limit): the response is then a `job_id`. `WaitForJobAsync` polls `GET /v1/jobs/{id}` until a terminal status (`CompletedJob`, `Error`, `NotFound`, `BadRequest`):

```csharp
var submitted = await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api", IsAsync = true });
var job = await client.WaitForJobAsync(
    submitted.Json.GetProperty("job_id").GetString()!,
    onProgress: j => Console.WriteLine($"{j.GetProperty("status")} {j.GetProperty("progress")}%"));
```

## GET or POST

Methods use GET when the endpoint supports it (query string). POST sends the same parameters as JSON:

```csharp
await client.BingSearchAsync(new BingSearchParams { Query = "test" }, new RequestOptions { Method = "POST" });
```

## Any endpoint

```csharp
var response = await client.RequestAsync("GET", "/v1/usage");
```

## Errors

```csharp
try
{
    await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" });
}
catch (AutomApiException exception)
{
    Console.Error.WriteLine($"{exception.StatusCode}: {exception.Message}");
}
```

| Exception | When |
| --- | --- |
| `AutomConfigurationException` | Missing API key, invalid options or unsupported method. |
| `AutomSerializationException` | A required parameter is missing or a value can't be sent. |
| `AutomTransportException` | Network failure or timeout. |
| `AutomApiException` | Non-2xx status (`StatusCode`, `ErrorMessage`, `ResponseBody`, `RawResponse`). |
| `AutomResponseDecodingException` | A success response could not be decoded. |

All of them derive from `AutomException`. Caller cancellation surfaces as `OperationCanceledException`.

## Retries

Requests are retried up to 3 times after a network error, a `408`, `500`, `502`, `503` or `504`, or a rate limit — which the API returns as `401` (or `429`) with a `Rate limit exceeded` message. Per-key quota errors (`…: quota daily`) are not retried, nor are `400`, `402`, `404` or client-side timeouts. The delay starts at 1 second and grows 1.5× per retry (capped at 30 seconds); a `Retry-After` header wins.

```csharp
using var client = new AutomClient(new AutomClientOptions { MaxRetries = 5, RetryDelay = TimeSpan.FromMilliseconds(500) });
await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" }, new RequestOptions { MaxRetries = 0 }); // one request without retries
```

## Batch requests

`BatchAsync` runs a function per item with at most 10 in flight (`concurrency`) and returns one `BatchResult` per item, in input order. A failing item never stops the others.

```csharp
string[] inputs = { "web scraping api", "serp api", "rank tracker" };
var results = await client.BatchAsync(inputs, (value, ct) =>
    client.GoogleSearchAsync(new GoogleSearchParams { Query = value }, cancellationToken: ct), concurrency: 5);
foreach (var result in results)
{
    if (result.Ok)
    {
        Console.WriteLine(result.Value!.Json);
    }
    else
    {
        Console.Error.WriteLine($"{inputs[result.Index]}: {result.Error!.Message}");
    }
}
```

| Constant | Default | Description |
| --- | --- | --- |
| `AutomClient.DefaultConcurrency` | `10` | Tasks run at the same time by batch |
| `AutomClient.DefaultTimeout` | `TimeSpan.FromSeconds(120)` | Request timeout |
| `AutomClient.MaxRetries` | `3` | Retry attempts after a network error, a 408/5xx or a rate limit |
| `AutomClient.RetryBackoffFactor` | `1.5` | Exponential backoff multiplier |
| `AutomClient.DefaultRetryDelay` | `TimeSpan.FromSeconds(1)` | Delay before the first retry |

## Configuration

```csharp
using var client = new AutomClient(new AutomClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("AUTOM_API_KEY"),
    Timeout = TimeSpan.FromSeconds(60), // default 120 s
    HttpClient = myHttpClient,           // optional, you keep ownership
});
```

## Endpoints

23 methods, generated from the [OpenAPI document](https://app.autom.dev/openapi).

| Method | HTTP | Description |
| --- | --- | --- |
| `BingSearchAsync` | GET / POST `/v1/bing/search` | Query Bing Search via API and retrieve structured organic results (title, link, snippet, position). |
| `BraveImagesAsync` | GET / POST `/v1/brave/images` | Search Brave Images via API and retrieve structured image results with pagination and SafeSearch. |
| `BraveSearchAsync` | GET / POST `/v1/brave/search` | Search Brave Search via API and retrieve structured organic results with pagination. |
| `BraveVideosAsync` | GET / POST `/v1/brave/videos` | Search Brave Videos via API and retrieve structured video results with pagination and SafeSearch. |
| `DuckduckgoSearchAsync` | GET / POST `/v1/duckduckgo/search` | Query DuckDuckGo via API and retrieve structured organic results with region and language. |
| `FinderGoogleCountriesAsync` | GET `/v1/finder/google-countries` | Search Google countries and retrieve country codes (free Finder endpoint). |
| `FinderGoogleLanguagesAsync` | GET `/v1/finder/google-languages` | Search Google languages and retrieve language codes (free Finder endpoint). |
| `FinderGoogleLocationsAsync` | GET `/v1/finder/google-locations` | Find supported Google locations and retrieve location IDs and country codes (free endpoint). |
| `GoogleImagesAsync` | GET / POST `/v1/google/images` | Search Google Images via API and retrieve image results (URLs, titles, sources, dimensions). |
| `GoogleJobsAsync` | GET / POST `/v1/google/jobs` | Scrape Google Jobs results via API and retrieve structured job listings with title, company, location, and pagination. |
| `GoogleMapsAsync` | GET / POST `/v1/google/maps` | Search Google Maps via API and retrieve structured place results (address, website, phone). |
| `GoogleNewsAsync` | GET / POST `/v1/google/news` | Search Google News via API and retrieve structured news results with pagination. |
| `GoogleSearchAsync` | GET / POST `/v1/google/search` | Scrape Google Search results via API and retrieve structured SERP data with pagination. |
| `GoogleSearchAiAsync` | GET / POST `/v1/google/search/ai` | Query Google AI Mode via API. JSON with text_blocks, references, and shopping_results. |
| `GoogleSearchAutocompleteAsync` | GET / POST `/v1/google/search/autocomplete` | Get Google Search autocomplete suggestions via API for a given query. |
| `GoogleSearchLightAsync` | GET / POST `/v1/google/search/light` | Scrape lightweight Google Search results via API: fast, cost-efficient, organic results only. |
| `GoogleShoppingAsync` | GET / POST `/v1/google/shopping` | Scrape Google Shopping results via API and retrieve structured product listings with prices, ratings, and merchant data. |
| `GoogleVideosAsync` | GET / POST `/v1/google/videos` | Search Google Videos via API and retrieve structured video results with metadata. |
| `JobsAsync` | GET `/v1/jobs/{id}` | Poll the status, progress (0–100) and result of a job started with is_async=true. Polling is free. Poll until status is terminal: Completed… |
| `SearchAsync` | GET / POST `/v1/search` | One call walks engines in order and returns the first organic results. engines=auto tries Google, then Brave, Bing, DuckDuckGo, Yahoo, and… |
| `StatsAsync` | GET `/v1/stats` | Infrastructure health for an engine or endpoint over the last hour. Not scoped to your account or tenant. Enabled per account: HTTP 403 mea… |
| `UsageAsync` | GET `/v1/usage` | Retrieve real-time usage statistics and credit balance for your Autom account. |
| `YahooSearchAsync` | GET / POST `/v1/yahoo/search` | Query Yahoo via API and retrieve structured organic results with country and language. |

## All Autom SDKs

| SDK | Package | Install |
| --- | --- | --- |
| Node.js | [`@automdev/sdk`](https://www.npmjs.com/package/@automdev/sdk) | `npm install @automdev/sdk` |
| Python | [`automdev`](https://pypi.org/project/automdev/) | `pip install automdev` |
| Go | [`autom-go`](https://pkg.go.dev/github.com/automdev/autom-go) | `go get github.com/automdev/autom-go` |
| .NET | [`Autom`](https://www.nuget.org/packages/Autom) | `dotnet add package Autom` |
| Ruby | [`autom`](https://rubygems.org/gems/autom) | `gem install autom` |
| PHP | [`autom/sdk`](https://packagist.org/packages/autom/sdk) | `composer require autom/sdk` |
| Java | [`autom-java`](https://github.com/automdev/autom-java) | build from source, see its README |
| CLI | [`@automdev/cli`](https://www.npmjs.com/package/@automdev/cli) | `npm install -g @automdev/cli` |
| MCP server | [`@automdev/mcp`](https://www.npmjs.com/package/@automdev/mcp) | `npx -y @automdev/mcp` |

## License

MIT — see [LICENSE](LICENSE).
