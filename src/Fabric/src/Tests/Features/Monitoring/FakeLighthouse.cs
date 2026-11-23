using System.Reflection;
using System.Text;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// Stands in for bit Fabric's Lighthouse service (src/Server/Bit.Fabric.Server.Lighthouse) the way FakeCustomerApp stands
/// in for a customer's app: its Handler sits in the api's Lighthouse http client and answers POST /audit with a run the
/// real service recorded on 2026-11-13 (Lighthouse/*.json), its addresses moved to the page asked for. The slow app is a
/// public Blazor WebAssembly site's own runs, with two product images without alt added, so it has everything the home
/// page's films promise: images bigger than they need to be, files not cached, scripts holding up the first page, text
/// too faint to read, a button without a name and images without alt. The good app is a small static site's runs, with
/// the three improvements it was told about fixed. Like the real service, it reuses a run asked for again under the same
/// request (reuseKey), and counts the runs it would have made.
/// </summary>
public sealed class FakeLighthouse
{
    /// <summary>Where the tests' api finds it (Monitoring:LighthouseUrl).</summary>
    public const string Address = "http://lighthouse.test";

    public const string SlowApp = "slow-app";

    public const string GoodApp = "good-app";

    public sealed class Handler(FakeLighthouse lighthouse) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => lighthouse.Answer(request, cancellationToken);
    }

    /// <summary>Which recording every page answers with.</summary>
    public string Recording { get; set; } = GoodApp;

    /// <summary>Lighthouse couldn't load these paths, as for a page that answers an error.</summary>
    public HashSet<string> Unloadable { get; } = [];

    /// <summary>The service is down: it answers 500 to everything.</summary>
    public bool IsDown { get; set; }

    /// <summary>Every request: the address, the form factor and the request it belongs to.</summary>
    public List<(string Url, string FormFactor, string? ReuseKey)> Asked { get; } = [];

    /// <summary>The Chrome runs the real service would have made: a page asked for again under the same request is reused.</summary>
    public int Runs
    {
        get
        {
            lock (Asked)
                return Asked.Select(asked => asked.ReuseKey is null ? Guid.NewGuid().ToString() : $"{asked.ReuseKey}|{asked.FormFactor}|{asked.Url}").Distinct().Count();
        }
    }

    private async Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/audit")
            return new(HttpStatusCode.NotFound);

        if (IsDown)
            return Json("""{"error":"FAILED","message":"Chrome didn't start."}""", HttpStatusCode.InternalServerError);

        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var url = body.RootElement.GetProperty("url").GetString()!;
        var formFactor = body.RootElement.GetProperty("formFactor").GetString()!;
        var reuseKey = body.RootElement.TryGetProperty("reuseKey", out var key) ? key.GetString() : null;

        lock (Asked) Asked.Add((url, formFactor, reuseKey));

        var asked = new Uri(url);
        // A template app sends its root to the culture's, which Chrome follows.
        var final = asked.AbsolutePath is "/" ? new Uri(asked, "/en-US/") : asked;

        if (Unloadable.Contains(final.AbsolutePath))
            return Json($$"""{"requestedUrl":"{{url}}","finalUrl":null,"formFactor":"{{formFactor}}","lighthouseVersion":"13.6.0","runtimeError":"ERRORED_DOCUMENT_REQUEST","refusedRequests":0,"metrics":{},"findings":[]}""");

        var recording = Read($"{Recording}.{(formFactor is "desktop" ? "computer" : "phone")}.json")
            .Replace("{url}", final.ToString(), StringComparison.Ordinal)
            .Replace("{origin}", final.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal);

        return Json(recording);
    }

    private static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Lighthouse.{name}") ?? throw new InvalidOperationException($"No recording {name}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
