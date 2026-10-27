using System.Net.Http.Headers;

namespace CommunitiesForever.Upload.Windows;

/// <summary>The app's one shared HttpClient (CLAUDE.md rule 11): created once, disposed on exit.</summary>
public static class SiteHttp
{
    public static HttpClient Create(string appVersion)
    {
        var handler = new SocketsHttpHandler
        {
            // Recycle connections now and then, so a changed DNS record for the site is picked up.
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(100) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CommunitiesForever-Uploader", appVersion));
        return http;
    }
}
