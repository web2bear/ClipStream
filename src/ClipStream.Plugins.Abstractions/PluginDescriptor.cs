namespace ClipStream.Plugins.Abstractions;

public sealed record PluginDescriptor(
    string Id,
    string Name,
    string Version,
    int Priority);

public interface IClipStreamPlugin
{
    PluginDescriptor Descriptor { get; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ClipStreamPluginAttribute : Attribute
{
    public ClipStreamPluginAttribute(Type pluginType) => PluginType = pluginType;

    public Type PluginType { get; }
}
