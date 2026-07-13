using ClipStream.Core.Models;

namespace ClipStream.Core.Repositories;

public interface IRoutingRuleRepository
{
    Task<IReadOnlyList<RoutingRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RoutingRule rule, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default);
}
