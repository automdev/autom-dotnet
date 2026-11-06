using Autom;

using var client = new AutomClient(); // reads AUTOM_API_KEY
var response = await client.GoogleSearchAsync(new GoogleSearchParams { Query = "web scraping api" });
Console.WriteLine(response.Json);
