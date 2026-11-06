using Autom;

using var client = new AutomClient(); // reads AUTOM_API_KEY
var submitted = await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api", IsAsync = true });
var job = await client.WaitForJobAsync(
    submitted.Json.GetProperty("job_id").GetString()!,
    onProgress: j => Console.WriteLine($"{j.GetProperty("status")} {j.GetProperty("progress")}%"));
Console.WriteLine(job.Json);
