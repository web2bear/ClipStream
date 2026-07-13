using System.Text.RegularExpressions;
using ClipStream.Core.Models;
using ClipStream.Core.Repositories;
using ClipStream.Core.Routing;
using ClipStream.Infrastructure.Persistence;

namespace ClipStream.Infrastructure.Routing;

public sealed class RoutingEngine : IRoutingEngine
{
    private readonly IRoutingRuleRepository _ruleRepository;
    private readonly IStreamRepository _streamRepository;

    public RoutingEngine(IRoutingRuleRepository ruleRepository, IStreamRepository streamRepository)
    {
        _ruleRepository = ruleRepository;
        _streamRepository = streamRepository;
    }

    public async Task<Guid> RouteAsync(ClipboardFragment fragment, CancellationToken cancellationToken = default)
    {
        var rules = await _ruleRepository.GetAllAsync(cancellationToken);
        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            if (Matches(rule.Condition, fragment))
            {
                return rule.TargetStreamId;
            }
        }

        await _streamRepository.EnsureDefaultStreamAsync(cancellationToken);
        return SqliteStreamRepository.GetDefaultStreamId();
    }

    private static bool Matches(RoutingCondition condition, ClipboardFragment fragment) =>
        condition switch
        {
            TextMatchesRegexCondition regex => !string.IsNullOrEmpty(fragment.PreviewText)
                && Regex.IsMatch(fragment.PreviewText, regex.Pattern, RegexOptions.IgnoreCase),
            KindIsCondition kind => fragment.Kind == kind.Kind,
            SourceProcessIsCondition source => string.Equals(
                fragment.SourceProcessName,
                source.ProcessName,
                StringComparison.OrdinalIgnoreCase),
            FormatPresentCondition format => fragment.Payloads.Any(p =>
                string.Equals(p.FormatName, format.FormatName, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
}
