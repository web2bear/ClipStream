namespace ClipStream.Core.Models;

public abstract record RoutingCondition;

public sealed record TextMatchesRegexCondition(string Pattern) : RoutingCondition;

public sealed record KindIsCondition(FragmentKind Kind) : RoutingCondition;

public sealed record SourceProcessIsCondition(string ProcessName) : RoutingCondition;

public sealed record FormatPresentCondition(string FormatName) : RoutingCondition;

public sealed record RoutingRule(
    Guid Id,
    Guid TargetStreamId,
    int Priority,
    RoutingCondition Condition);
