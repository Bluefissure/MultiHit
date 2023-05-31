using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace MultiHit
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        [NonSerialized]
        internal bool changed = false;

        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public bool Enabled = true;
        public List<ActionGroup> actionGroups = new();
        public bool validateActionGroups = true;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.changed = true;
            this.PluginInterface!.SavePluginConfig(this);
        }

        public void ApplyChange()
        {
            this.changed = false;
        }
    }
}
