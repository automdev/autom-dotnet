using Autom;

using var client = new AutomClient(); // reads AUTOM_API_KEY
try
{
    var response = await client.RequestAsync("GET", "/v1/usage");
    Console.WriteLine(response.Text);
}
catch (AutomApiException exception)
{
    Console.Error.WriteLine($"HTTP {exception.StatusCode}: {exception.Message}");
}
