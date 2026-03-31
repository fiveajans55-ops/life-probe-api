using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    ok = true,
    service = "life-probe-api"
}));

app.MapGet("/probe", async (string host) =>
{
    host = NormalizeHost(host);

    if (string.IsNullOrWhiteSpace(host))
    {
        return Results.Ok(new
        {
            ok = false,
            host = "",
            dnsResolved = false,
            statusCode = 0,
            finalUrl = "",
            error = "empty_host"
        });
    }

    bool dnsResolved = false;

    try
    {
        var addresses = await Dns.GetHostAddressesAsync(host);
        dnsResolved = addresses.Length > 0;
    }
    catch
    {
        return Results.Ok(new
        {
            ok = false,
            host,
            dnsResolved = false,
            statusCode = 0,
            finalUrl = "",
            error = "dns_fail"
        });
    }

    try
    {
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        };

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("HeroScanner-LifeProbe/1.0");

        using var response = await client.GetAsync("https://" + host);

        return Results.Ok(new
        {
            ok = (int)response.StatusCode < 400,
            host,
            dnsResolved,
            statusCode = (int)response.StatusCode,
            finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "",
            error = ""
        });
    }
    catch (TaskCanceledException)
    {
        return Results.Ok(new
        {
            ok = false,
            host,
            dnsResolved,
            statusCode = 0,
            finalUrl = "",
            error = "timeout"
        });
    }
    catch (HttpRequestException)
    {
        return Results.Ok(new
        {
            ok = false,
            host,
            dnsResolved,
            statusCode = 0,
            finalUrl = "",
            error = "network"
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            ok = false,
            host,
            dnsResolved,
            statusCode = 0,
            finalUrl = "",
            error = ex.GetType().Name
        });
    }
});

app.Run();

static string NormalizeHost(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    value = value.Trim().ToLowerInvariant();

    if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        value = uri.Host;

    if (value.StartsWith("www."))
        value = value[4..];

    return value;
}
