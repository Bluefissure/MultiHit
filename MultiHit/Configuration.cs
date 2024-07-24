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


        public bool Enabled = true;
        public List<ActionGroup> actionGroups = new();
        public bool validateActionGroups = true;

        public void Save()
        {
            this.changed = true;
            Plugin.PluginInterface.SavePluginConfig(this);
        }

        public void ApplyChange()
        {
            this.changed = false;
        }
    }
}
