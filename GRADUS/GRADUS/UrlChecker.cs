using System.Net.Http;

public class UrlChecker
{
    public string Url { get; private set; }
    public bool IsAccessible { get; private set; }

    public UrlChecker(string url)
    {
        Url = url;
    }

    public async Task CheckUrlAsync(HttpClient client)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, Url);

            var response = await client.SendAsync(request);
            IsAccessible = response.IsSuccessStatusCode;
        }
        catch
        {
            IsAccessible = false;
        }
    }

    public override string ToString()
    {
        return $"URL: {Url}, Accessible: {IsAccessible}";
    }
}
