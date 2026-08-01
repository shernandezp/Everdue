using System.Net;

namespace Everdue.Server.Tests.Support;

/// <summary>One POST as the stub receiver saw it — headers included, because the signature is the point.</summary>
public sealed record ReceivedWebhook(string Body, IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Stands in for a subscriber's endpoint.
///
/// A recording handler rather than a real socket: the assertions are about what Everdue sends and how it reacts to
/// what comes back, and a live listener would add a port and a race to every test that uses it.
/// </summary>
public sealed class StubWebhookReceiver : HttpMessageHandler
{
    private readonly Queue<HttpStatusCode> _responses = new();

    public List<ReceivedWebhook> Received { get; } = [];

    /// <summary>The status to answer with when no scripted response is left. 200 unless a test says otherwise.</summary>
    public HttpStatusCode Default { get; set; } = HttpStatusCode.OK;

    /// <summary>Throws instead of answering — a refused connection or a DNS failure.</summary>
    public bool Unreachable { get; set; }

    /// <summary>Queues one answer per call, so a test can say "fail, fail, then succeed".</summary>
    public StubWebhookReceiver Answering(params HttpStatusCode[] statuses)
    {
        foreach (var status in statuses)
        {
            _responses.Enqueue(status);
        }

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Received.Add(new ReceivedWebhook(
            await (request.Content?.ReadAsStringAsync(cancellationToken) ?? Task.FromResult(string.Empty)),
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase)));

        if (Unreachable)
        {
            throw new HttpRequestException("The stub receiver is unreachable.");
        }

        var status = _responses.Count > 0 ? _responses.Dequeue() : Default;

        return new HttpResponseMessage(status) { Content = new StringContent("stub") };
    }
}
