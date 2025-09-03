using Dalamud.Configuration;
using Dalamud.Plugin;
using System;


namespace Blackjack;

[Serializable]
public class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.SavePluginConfig(this);
    }
}
