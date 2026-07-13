using ClipStream.Core.Models;
using ClipStream.Infrastructure.Routing;

namespace ClipStream.Core.Tests;

public class RoutingEngineTests
{
    [Fact]
    public async Task RouteAsync_KindRule_ReturnsTargetStream()
    {
        var targetStreamId = Guid.NewGuid();
        var ruleRepo = new InMemoryRoutingRuleRepository(
        [
            new RoutingRule(Guid.NewGuid(), targetStreamId, 1, new KindIsCondition(FragmentKind.Text))
        ]);
        var streamRepo = new InMemoryStreamRepository();
        var engine = new RoutingEngine(ruleRepo, streamRepo);

        var fragment = new ClipboardFragment(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            FragmentKind.Text,
            "hello",
            null,
            null,
            [],
            new Dictionary<string, string>());

        var result = await engine.RouteAsync(fragment);
        Assert.Equal(targetStreamId, result);
    }
}

internal sealed class InMemoryRoutingRuleRepository(IReadOnlyList<RoutingRule> rules) : Core.Repositories.IRoutingRuleRepository
{
    public Task<IReadOnlyList<RoutingRule>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(rules);

    public Task SaveAsync(RoutingRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(Guid ruleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryStreamRepository : Core.Repositories.IStreamRepository
{
    public Task<IReadOnlyList<ClipStreamEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ClipStreamEntity>>([]);

    public Task<ClipStreamEntity?> GetByIdAsync(Guid streamId, CancellationToken cancellationToken = default)
        => Task.FromResult<ClipStreamEntity?>(null);

    public Task<ClipStreamEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult<ClipStreamEntity?>(null);

    public Task SaveAsync(ClipStreamEntity stream, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task EnsureDefaultStreamAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
