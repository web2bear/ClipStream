using ClipStream.Core.Models;

namespace ClipStream.Core.Routing;

public interface IRoutingEngine
{
    Task<Guid> RouteAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default);
}
