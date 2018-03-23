using System;

namespace GitUIPluginInterfaces
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class PluginTypeAttribute : Attribute
    {
        public Type PluginType { get; }

        public PluginTypeAttribute(Type pluginType)
        {
            if (!typeof(IGitPlugin).IsAssignableFrom(pluginType))
            {
                throw new ArgumentException($"Type must implement {nameof(IGitPlugin)}.", nameof(pluginType));
            }

            if (!pluginType.IsClass || pluginType.IsAbstract)
            {
                throw new ArgumentException("Must be a concrete class.", nameof(pluginType));
            }

            PluginType = pluginType;
        }
    }
}