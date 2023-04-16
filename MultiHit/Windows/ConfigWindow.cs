using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using ImPlotNET;
using Newtonsoft.Json;

namespace MultiHit.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;
    private string addGroupNameText = "";
    private string addActionFilterText = "";
    private int selectedGroupIdx = -1;
    private int selectedActionIdx = -1;

    private readonly FileDialogManager _dialogManager = SetupFileManager();
    private bool _dialogOpen;
    private string _lastExportDirectory = ".";

    public ConfigWindow(Plugin plugin) : base(
        "MultiHit Configuration Window")
    {
        this.Size = new Vector2(700, 500);
        this.SizeCondition = ImGuiCond.FirstUseEver;

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
        ImGui.SameLine();
        if (ImGui.Button("Export"))
        {
            if (_dialogOpen)
            {
                _dialogManager.Reset();
                _dialogOpen = false;
            }
            else
            {
                // Use the current input as start directory if it exists,
                // otherwise the current mod directory, otherwise the current application directory.
                var startDir = _lastExportDirectory ?? ".";

                _dialogManager.OpenFolderDialog("Export", (b, s) =>
                {
                    Plugin.ExportGroups(s);
                    _lastExportDirectory = s;
                    _dialogOpen = false;
                }, startDir);
                _dialogOpen = true;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Import"))
        {
            if (_dialogOpen)
            {
                _dialogManager.Reset();
                _dialogOpen = false;
            }
            else
            {
                // Use the current input as start directory if it exists,
                // otherwise the current mod directory, otherwise the current application directory.
                var startDir = ".";

                _dialogManager.OpenFileDialog("Import", ".json", (b, strs) =>
                {
                    foreach (var s in strs)
                    {
                        Plugin.ImportGroup(s);
                    }
                    _dialogOpen = false;
                }, 10, startDir);
                _dialogOpen = true;
            }
        }
        ImGui.SameLine();
        var applyText = "Apply Changes";
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(applyText).X - 10);
        if(Configuration.changed && ImGui.Button(applyText))
        {
            Plugin.updateAffectedAction();
            Configuration.ApplyChange();
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

        _dialogManager.Draw();
    }
    public void DrawGroup()
    {
        //ImGui.Text("Group Window");
        if (ImGui.BeginChild("#MultiHitGroupList",
                new Vector2((float)(ImGui.GetContentRegionMax().X), (float)(ImGui.GetContentRegionAvail().Y * 0.95))
            ))
        {
            var groupToDeleteIdx = -1;
            var actionGroups = CollectionsMarshal.AsSpan(Configuration.actionGroups);
            for (var groupIdx = 0; groupIdx < Configuration.actionGroups.Count; groupIdx++)
            {
                ref var group = ref actionGroups[groupIdx];
                if(!group.enabled)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                }
                var open = ImGui.TreeNode(group.name + $"##Group{groupIdx}");
                if (!group.enabled)
                {
                    ImGui.PopStyleColor();
                }
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
                    if (ImGui.Selectable("Edit Group Name"))
                    {

                    }
                    if (ImGui.Selectable("Enable Group"))
                    {
                        group.enabled = true;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Disable Group"))
                    {
                        group.enabled = false;
                        Configuration.Save();
                    }
                    if (ImGui.Selectable("Export Group"))
                    {
                        if (_dialogOpen)
                        {
                            _dialogManager.Reset();
                            _dialogOpen = false;
                        }
                        else
                        {
                            var startDir = _lastExportDirectory ?? ".";
                            var tempGroup = group;
                            _dialogManager.OpenFolderDialog("Export", (b, s) =>
                            {
                                Plugin.ExportGroup(s, tempGroup);
                                _lastExportDirectory = s;
                                _dialogOpen = false;
                            }, startDir);
                            _dialogOpen = true;
                        }
                        PluginLog.Debug($"Exporting group#{groupIdx}");
                    }
                    if (ImGui.Selectable("Delete Group"))
                    {
                        groupToDeleteIdx = groupIdx;
                        PluginLog.Debug($"To delete group#{groupIdx}");
                    }
                    ImGui.EndPopup();
                }
                if (open)
                {
                    var actionToDeleteIdx = -1;
                    if (group.actionList != null)
                    {
                        var actionList = CollectionsMarshal.AsSpan(group.actionList);
                        for (int actionIdx = 0; actionIdx < group.actionList.Count; actionIdx++)
                        {
                            ref var action = ref actionList[actionIdx];
                            if (!action.enabled || !group.enabled)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
                            }
                            var actionOpen = ImGui.Selectable(action.ToString(), selectedGroupIdx == groupIdx && selectedActionIdx == actionIdx);
                            if (!action.enabled || !group.enabled)
                            {
                                ImGui.PopStyleColor();
                            }
                            if (ImGui.BeginPopupContextItem())
                            {
                                if (ImGui.Selectable("Enable Action"))
                                {
                                    action.enabled = true;
                                    Configuration.Save();
                                }
                                if (ImGui.Selectable("Disable Action"))
                                {
                                    action.enabled = false;
                                    Configuration.Save();
                                }
                                if (ImGui.Selectable("Delete Action"))
                                {
                                    actionToDeleteIdx = actionIdx;
                                    PluginLog.Debug($"To delete action {action}");
                                }
                                ImGui.EndPopup();
                            }
                            if (actionOpen)
                            {
                                PluginLog.Debug($"Selecting {action}");
                                selectedGroupIdx = groupIdx;
                                selectedActionIdx = actionIdx;
                            }
                        }
                        if (actionToDeleteIdx != -1)
                        {
                            group.actionList.RemoveAt(actionToDeleteIdx);
                            selectedGroupIdx = -1;
                            selectedActionIdx = -1;
                            Configuration.Save();
                        }
                    }
                    ImGui.TreePop();
                }
            }
            
            if (groupToDeleteIdx != -1)
            {
                Configuration.actionGroups.RemoveAt(groupToDeleteIdx);
                selectedGroupIdx = -1;
                selectedActionIdx = -1;
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
        if(selectedGroupIdx == -1 || selectedActionIdx == -1)
        {
            var hintText = "Please select action";
            ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(hintText).X) * 0.5f);
            ImGui.Text(hintText);
            return;
        }
        var actionList = CollectionsMarshal.AsSpan(Configuration.actionGroups[selectedGroupIdx].actionList);
        ref var action = ref actionList[selectedActionIdx];
        var titleText = $"{action}";
        ImGui.Spacing();
        ImGui.SetCursorPosX((ImGui.GetWindowSize().X - ImGui.CalcTextSize(titleText).X) * 0.5f);
        ImGui.Text(titleText);
        if(ImGui.Checkbox("Enabled", ref action.enabled))
        {
            Configuration.Save();
        }
        if (ImGui.Checkbox("Interruptible", ref action.interruptible))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Whether other animation can interrupte the upcoming flytext.");
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Show Hit", ref action.showHit))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Whether to show the Hit#i in flytext.");
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Show Final", ref action.showFinal))
        {
            Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Whether to show the original hit as final hit.");
        }
        if (action.showFinal)
        {
            int delay = (int)action.finalDelay;
            ImGui.SameLine();
            ImGui.Text("Delay: ");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Delay the final hit (after the last hit).");
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt("##DelayHit_Final", ref delay, 1, 5))
            {
                delay = Math.Min(delay, 300);
                delay = Math.Max(delay, 0);
                action.finalDelay = delay;
                Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("30 = 1 second");
            }
        }
        if (ImGui.Button("Add Hit"))
        {
            if (action.hitList.Count < 100)
            {
                action.hitList.Add(new Hit());
                Configuration.Save();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            action.hitList.Clear();
            Configuration.Save();
        }
        if (ImGui.BeginChild("detailActionList", 
            new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y * 0.8f)
            , false))
        {
            var hitList = CollectionsMarshal.AsSpan(action.hitList);
            int hitIdxToDelete = -1;
            for(int hitIdx = 0; hitIdx < action.hitList.Count; hitIdx++)
            {
                ref var hit = ref hitList[hitIdx];
                ImGui.Text($"Hit {hitIdx + 1, 3}: ");
                ImGui.SameLine();
                int pct = hit.percent;
                ImGui.Text("Percentage: "); ImGui.SameLine();
                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt($"##PercentageHit_{hitIdx}", ref pct, 1, 5))
                {
                    pct = Math.Min(pct, 100);
                    pct = Math.Max(pct, 0);
                    hit.percent = pct;
                    Configuration.Save();
                }
                ImGui.SameLine();
                int delay = (int)hit.time;
                ImGui.Text("Delay: "); ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                if (ImGui.InputInt($"##DelayHit_{hitIdx}", ref delay, 1, 5))
                {
                    delay = Math.Min(delay, 300);
                    delay = Math.Max(delay, 0);
                    hit.time = delay;
                    Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("30 = 1 second");
                }
                ImGui.SameLine();
                var uintCol = hit.color;
                var R = uintCol >> 24;
                var G = (uintCol >> 16) & 0xFF;
                var B = (uintCol >> 8) & 0xFF;
                var A = uintCol & 0xFF;
                var col = new Vector4(R / 255.0f, G / 255.0f, B / 255.0f, A / 255.0f);
                if (ImGui.ColorEdit4($"ColorPicker##ColorPickerHit_{hitIdx}", ref col,
                    ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreview | ImGuiColorEditFlags.AlphaBar))
                {
                    hit.color = ((uint)(col.X * 255.0) << 24) | ((uint)(col.Y * 255.0) << 16) | ((uint)(col.Z * 255.0) << 8) | (uint)(col.W * 255.0);
                    Configuration.Save();
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Custom colors only work when alpha is not 0.");
                }
                ImGui.SameLine();
                if (ImGui.Button($"Delete##DeleteHit_{hitIdx}"))
                {
                    hitIdxToDelete = hitIdx;
                }
            }
            if (hitIdxToDelete != -1)
            {
                action.hitList.RemoveAt(hitIdxToDelete);
                Configuration.Save();
            }
            ImGui.EndChild();
        }
    }
    public static FileDialogManager SetupFileManager()
    {
        var fileManager = new FileDialogManager
        {
            AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
        };

        // Remove Videos and Music.
        fileManager.CustomSideBarItems.Add(("Videos", string.Empty, 0, -1));
        fileManager.CustomSideBarItems.Add(("Music", string.Empty, 0, -1));

        return fileManager;
    }
}
