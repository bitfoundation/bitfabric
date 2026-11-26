using System.Diagnostics;

namespace Bit.Fabric.Client.Core.Infrastructure.Services;

/// <summary>
/// Sends a request marked with <see cref="Leave"/> without the trace context (<c>traceparent</c>, <c>tracestate</c>), and
/// hands every other one to the propagator it wraps. In the browser, a cross-origin request with a header of its own is
/// preflighted first, and a server that doesn't answer the preflight, as a Better Stack status page doesn't, refuses it
/// (See StatusPageService). Client.Web's Program sets it as <see cref="DistributedContextPropagator.Current"/> before any
/// <see cref="HttpClient"/> is made, since each handler keeps the propagator it finds when it is made.
/// </summary>
public class TraceContextPropagator(DistributedContextPropagator inner) : DistributedContextPropagator
{
    /// <summary>Set on a request's <see cref="HttpRequestMessage.Options"/> to send it without the trace context.</summary>
    public static readonly HttpRequestOptionsKey<bool> Leave = new("Bit.Fabric.LeaveTraceContext");

    public override IReadOnlyCollection<string> Fields => inner.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter)
    {
        if (carrier is HttpRequestMessage request && request.Options.TryGetValue(Leave, out var leave) && leave)
            return;

        inner.Inject(activity, carrier, setter);
    }

    public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter, out string? traceId, out string? traceState)
        => inner.ExtractTraceIdAndState(carrier, getter, out traceId, out traceState);

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier, PropagatorGetterCallback? getter)
        => inner.ExtractBaggage(carrier, getter);
}
