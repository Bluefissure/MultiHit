using System;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
namespace MultiHit.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private string addGroupNameText = "";
    private string addActionFilterText = "";

    public ConfigWindow(Plugin plugin) : base(
        "MultiHit Configuration Window")
    {
        this.Size = new Vector2(700, 500);
        this.SizeCondition = ImGuiCond.Always;

        this.Plugin = plugin;
        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Checkbox("Enabled", ref Configuration.Enabled))
        {
            this.Configuration.Save();
        }
        ImGui.Spacing();
        if (ImGui.BeginChild("#MultiHitGroup",
                new Vector2((float)(ImGui.GetContentRegionMax().X * 0.3), (float)(ImGui.GetContentRegionAvail().Y * 0.98)),
                true
            ))
        {
            this.DrawGroup();
            ImGui.EndChild();
        }
        ImGui.SameLine();
        if (ImGui.BeginChild("#MultiHitDetails",
                new Vector2((float)(ImGui.GetContentRegionMax().X * 0.68), (float)(ImGui.GetContentRegionAvail().Y * 0.98)),
                true
            ))
        {
            this.DrawDetail();
            ImGui.EndChild();
        }
    }
    public void DrawGroup()
    {
        //ImGui.Text("Group Window");
        if (ImGui.BeginChild("#MultiHitGroupList",
                new Vector2((float)(ImGui.GetContentRegionMax().X), (float)(ImGui.GetContentRegionAvail().Y * 0.9))
            ))
        {
            var groupToDeleteIdx = -1;
            for (var groupIdx = 0; groupIdx < Configuration.actionGroups.Count; groupIdx++)
            {
                var group = Configuration.actionGroups[groupIdx];
                var open = ImGui.TreeNode(group.name + $"##Group{groupIdx}");

                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.BeginMenu("Add new action"))
                    {
                        ImGui.SetNextItemWidth(200);
                        ImGui.InputText("##Filter", ref addActionFilterText, 128);
                        if (ImGui.BeginChild("ActionList",
                            new Vector2((float)(ImGui.GetContentRegionAvail().X), 300),
                            true
                            ))
                        {
                            foreach (var action in Plugin.actionList.Where(act =>
                                act.Name.ToString().Contains(addActionFilterText)
                                || act.RowId.ToString().Contains(addActionFilterText)))
                            {
                                if (ImGui.Selectable(action.Name + $"##Action{action.RowId}"))
                                {
                                    PluginLog.Information($"{action.Name} #{action.RowId}");
                                    group.actionList.Add(new ActionMultiHit((int)action.RowId, action.Name));
                                    Configuration.Save();
                                    ImGui.CloseCurrentPopup();
                                }
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip($"#{action.RowId}");
                                }
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndMenu();
                    }
                    if (ImGui.Selectable("Edit group name"))
                    {

                    }
                    if (ImGui.Selectable("Delete Group"))
                    {
                        groupToDeleteIdx = groupIdx;
                        PluginLog.Information($"To delete group#{groupIdx}");

                    }
                    ImGui.EndPopup();
                }
                if (open)
                {
                    var actionToDeleteIdx = -1;
                    for (int actionIdx = 0; actionIdx < group.actionList.Count; actionIdx++)
                    {
                        var action = group.actionList[actionIdx];
                        var actionOpen = ImGui.Selectable(action.ToString());
                        if (ImGui.BeginPopupContextItem())
                        {
                            if (ImGui.Selectable("Delete Action"))
                            {
                                actionToDeleteIdx = actionIdx;
                                PluginLog.Information($"To delete action {action}");
                            }
                            ImGui.EndPopup();
                        }
                        if (actionOpen)
                        {

                        }
                    }
                    if (actionToDeleteIdx != -1)
                    {
                        group.actionList.RemoveAt(actionToDeleteIdx);
                        Configuration.Save();
                    }
                    ImGui.TreePop();
                }
            }
            
            if (groupToDeleteIdx != -1)
            {
                Configuration.actionGroups.RemoveAt(groupToDeleteIdx);
                Configuration.Save();
            }
            ImGui.EndChild();
        }
        if (ImGui.Button("Add Group"))
        {
            ImGui.OpenPopup("Add Group Popup");
        }
        if (ImGui.BeginPopup("Add Group Popup"))
        {
            var len = Configuration.actionGroups.Count;
            var defaultName = $"Group {len + 1}";
            var name = addGroupNameText == "" ? defaultName : addGroupNameText;
            if (ImGui.InputText("##AddGroupPopupNameEdit", ref name, 64))
            {
                if(name != defaultName)
                {
                    addGroupNameText = name;
                }
            }
            if (ImGui.Button("Add"))
            {
                addGroupNameText = "";
                Configuration.actionGroups.Add(new ActionGroup(name));
                Configuration.Save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }
    public void DrawDetail()
    {
        ImGui.Text("Detail Window");
    }
}
