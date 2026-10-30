using System.Text.Json.Serialization;
using JetBrains.Annotations;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace FactoryClassic.Server.Halloween;

public sealed class HalloweenRequest : IRequestData
{
    [JsonPropertyName("location")] public string Location { get; set; } = "";
}

public sealed class HalloweenResponse
{
    [JsonPropertyName("halloween")] public bool Halloween { get; set; }
}

/// <summary>
/// The client asks when its time-of-day preset loads. The raid end of the session that rolled forgets the
/// answer, so the next raid rolls again; that route passes SPT's own response through untouched.
/// </summary>
[Injectable, UsedImplicitly]
public class HalloweenRoutes(
    JsonUtil jsonUtil,
    HttpResponseUtil httpResponseUtil,
    HalloweenService halloween
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<HalloweenRequest>(
            "/factoryclassic/halloween",
            (url, info, sessionId, output, cancellationToken) =>
                ValueTask.FromResult(httpResponseUtil.NoBody(new HalloweenResponse { Halloween = halloween.Outcome(info?.Location ?? "", sessionId.ToString()) }))),

        new RouteAction<HalloweenRequest>(
            "/client/match/local/end",
            (url, info, sessionId, output, cancellationToken) =>
            {
                halloween.Clear(sessionId.ToString());
                return ValueTask.FromResult(output ?? "");
            }),
    ]
)
{
}
