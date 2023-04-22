using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using MultiHit.Windows;
using Dalamud.Game;
using Dalamud.Hooking;
using System;

using static MultiHit.LogType;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using Dalamud.Logging;
using Dalamud.Data;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Objects;
using System.Threading.Tasks;
using System.Collections.Generic;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Text;
using Lumina.Excel;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace MultiHit
{
    public unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "MultiHit";
        private const string CommandName = "/mhit";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("MultiHit");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        private readonly ObjectTable _objectTable;
        private readonly FlyTextGui _ftGui;
        private readonly GameGui _gameGui;

        private readonly ExcelSheet<Action> _actionSheet;
        public readonly List<Action> actionList;
        private HashSet<string> _validActionName;
        private HashSet<string> _interruptibleActionName;
        private HashSet<string> _showHitActionName;
        private HashSet<string> _showFinalActionName;
        private Dictionary<string, int> _finalDelay;
        private HashSet<string> _hasCustomActionName;
        private Dictionary<string, string> _customName;
        private Dictionary<string, List<Hit>> _multiHitMap;
        private string _lastAnimationName = "undefined";
        private HashSet<FlyTextKind> _validKinds = new HashSet<FlyTextKind>() {
            FlyTextKind.NamedAttack,
            FlyTextKind.NamedCriticalHit,
            FlyTextKind.NamedDirectHit,
            FlyTextKind.NamedCriticalDirectHit
        };

        private delegate void AddScreenLogDelegate(
                Character* target,
                Character* source,
                FlyTextKind logKind,
                int option,
                int actionKind,
                int actionId,
                int val1,
                int val2,
                int val3,
                int val4
            );
        private readonly Hook<AddScreenLogDelegate> _addScreenLogHook;

        private delegate void AddFlyTextDelegate(
            IntPtr addonFlyText,
            uint actorIndex,
            uint messageMax,
            IntPtr numbers,
            uint offsetNum,
            uint offsetNumMax,
            IntPtr strings,
            uint offsetStr,
            uint offsetStrMax,
            int unknown);
        private readonly Hook<AddFlyTextDelegate> _addFlyTextHook;

        private delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);
        private readonly Hook<ReceiveActionEffectDelegate> _receiveActionEffectHook;



        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] DataManager dataMgr,
            [RequiredVersion("1.0")] ObjectTable objectTable,
            [RequiredVersion("1.0")] GameGui gameGui,
            [RequiredVersion("1.0")] FlyTextGui ftGui,
            [RequiredVersion("1.0")] SigScanner scanner)
        {
            _objectTable = objectTable;
            _ftGui = ftGui;
            _gameGui = gameGui;

            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);
            
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open MultiHit window."
            });

            try
            {
                _actionSheet = dataMgr.GetExcelSheet<Action>();

                if (_actionSheet == null)
                    throw new NullReferenceException();

                this.updateAffectedAction();

                actionList = new();
                foreach(var action in _actionSheet)
                {
                    actionList.Add(action);
                }

                var receiveActionEffectFuncPtr = scanner.ScanText("4C 89 44 24 ?? 55 56 41 54 41 55 41 56");
                _receiveActionEffectHook = Hook<ReceiveActionEffectDelegate>.FromAddress(receiveActionEffectFuncPtr, ReceiveActionEffect);

                var addScreenLogPtr = scanner.ScanText("E8 ?? ?? ?? ?? BF ?? ?? ?? ?? 41 F6 87");
                _addScreenLogHook = Hook<AddScreenLogDelegate>.FromAddress(addScreenLogPtr, AddScreenLogDetour);

                var flyTextAddress = new FlyTextGuiAddressResolver();
                flyTextAddress.Setup(scanner);
                _addFlyTextHook = Hook<AddFlyTextDelegate>.FromAddress(flyTextAddress.AddFlyText, AddFlyTextDetour);

            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"An error occurred loading DamageInfoPlugin.");
                PluginLog.Error("Plugin will not be loaded.");

                _addScreenLogHook?.Disable();
                _addScreenLogHook?.Dispose();
                _addFlyTextHook?.Disable();
                _addFlyTextHook?.Dispose();
                _receiveActionEffectHook?.Disable();
                _receiveActionEffectHook?.Dispose();

                throw;
            }

            _receiveActionEffectHook?.Enable();
            _addScreenLogHook.Enable();
            _addFlyTextHook.Enable();
            _ftGui.FlyTextCreated += OnFlyTextCreated;


            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        private void AddScreenLogDetour(
                Character* target,
                Character* source,
                FlyTextKind logKind,
                int option,
                int actionKind,
                int actionId,
                int val1,
                int val2,
                int serverAttackType,
                int val4
            )
        {
            /*
            try
            {
                var targetId = target->GameObject.ObjectID;
                var sourceId = source->GameObject.ObjectID;

                DebugLog(ScreenLog, $"{option} {actionKind} {actionId}");
                DebugLog(ScreenLog, $"{val1} {val2} {serverAttackType} {val4}");
                var targetName = GetActorName(targetId);
                var targetIdx = GetActorIdx(targetId);
                var sourceName = GetActorName(sourceId);
                var sourceIdx = GetActorIdx(sourceId);
                DebugLog(ScreenLog, $"src {sourceId} {sourceName} {sourceIdx}");
                DebugLog(ScreenLog, $"tgt {targetId} {targetName} {targetIdx}");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error occurred in MultiHit.");
            }
            */

            _addScreenLogHook.Original(target, source, logKind, option, actionKind, actionId, val1, val2, serverAttackType, val4);
        }

        private void AddFlyTextDetour(
            IntPtr addonFlyText,
            uint actorIndex,
            uint messageMax,
            IntPtr numbers,
            uint offsetNum,
            uint offsetNumMax,
            IntPtr strings,
            uint offsetStr,
            uint offsetStrMax,
            int unknown)
        {
            if (!Configuration.Enabled)
            {
                _addFlyTextHook.Original(
                    addonFlyText,
                    actorIndex,
                    messageMax,
                    numbers,
                    offsetNum,
                    offsetNumMax,
                    strings,
                    offsetStr,
                    offsetStrMax,
                    unknown);
                return;
            }
            try
            {
                // Known valid flytext region within the atk arrays
                // patch 6.3
                var strIndex = 27;
                var numIndex = 30;
                // patch 6.2
                // var strIndex = 25;
                // var numIndex = 28;
                var atkArrayDataHolder = ((UIModule*)_gameGui.GetUIModule())->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
                PluginLog.Debug($"addonFlyText: {addonFlyText:X} actorIndex:{actorIndex} offsetNum: {offsetNum} offsetNumMax: {offsetNumMax} offsetStr: {offsetStr} offsetStrMax: {offsetStrMax} unknown:{unknown}");
                try
                {
                    var strArray = atkArrayDataHolder._StringArrays[strIndex];
                    var flyText1Ptr = strArray->StringArray[offsetStr];
                    if (flyText1Ptr == null || (nint)flyText1Ptr == IntPtr.Zero)
                    {
                        _addFlyTextHook.Original(
                            addonFlyText,
                            actorIndex,
                            messageMax,
                            numbers,
                            offsetNum,
                            offsetNumMax,
                            strings,
                            offsetStr,
                            offsetStrMax,
                            unknown);
                        return;
                    }
                    var flyText1Len = GetStrLenFromPtr(flyText1Ptr);
                    string text1 = Encoding.UTF8.GetString(flyText1Ptr, flyText1Len).Trim();
                    var flyText2Ptr = strArray->StringArray[offsetStr + 1];
                    var flyText2Len = GetStrLenFromPtr(flyText2Ptr);
                    string text2 = Encoding.UTF8.GetString(flyText2Ptr, flyText2Len).Trim();
                    var numArray = atkArrayDataHolder._NumberArrays[numIndex];
                    int kind = numArray->IntArray[offsetNum + 1];
                    FlyTextKind flyKind = (FlyTextKind)kind;
                    int val1 = numArray->IntArray[offsetNum + 2];
                    int val2 = numArray->IntArray[offsetNum + 3];
                    // patch 6.3
                    /*
                    int damageTypeIcon = numArray->IntArray[offsetNum + 4];
                    int color = numArray->IntArray[offsetNum + 6];
                    int icon = numArray->IntArray[offsetNum + 7];
                    */
                    // patch 6.2
                    int color = numArray->IntArray[offsetNum + 5];
                    int icon = numArray->IntArray[offsetNum + 6];
                    PluginLog.Debug($"kind:{flyKind} actorIndex:{actorIndex} val1:{val1} val2:{val2} text1:{text1}text2:{text2} color:{(uint)color:X} icon:{icon}");

                    if (_validActionName.Contains(text1) && _validKinds.Contains(flyKind))
                    {
                        var shownActionName = text1;
                        if(_hasCustomActionName.Contains(text1) && _customName.ContainsKey(text1))
                        {
                            var tempName = text1;
                            _customName.TryGetValue(text1, out tempName);
                            if(tempName != string.Empty)
                            {
                                shownActionName = tempName;
                            }
                        }
                        _multiHitMap.TryGetValue(text1, out var multiHitList);
                        int maxTime = 0;
                        if (multiHitList != null)
                        {
                            int tempIdx = 0;
                            foreach (var mulHit in multiHitList)
                            {
                                tempIdx += 1;
                                int hitIdx = tempIdx;
                                string tempText2 = _showHitActionName.Contains(text1) ? $"Hit#{hitIdx}" : text2;
                                if (tempText2.Equals(String.Empty))
                                {
                                    tempText2 = "\0";
                                }
                                int tempVal = (int)(val1 * (mulHit.percent * 1.0f / 100f));
                                maxTime = Math.Max(maxTime, mulHit.time);
                                uint tempColor = mulHit.color;
                                PluginLog.Debug($"{mulHit}.color: {mulHit.color:X}");
                                if ((tempColor & 0xFF) == 0) // if alpha is 0 then use the original color
                                {
                                    tempColor = (uint)color;
                                }
                                else
                                {
                                    byte[] bytes = BitConverter.GetBytes(tempColor);
                                    Array.Reverse(bytes, 0, bytes.Length);
                                    tempColor = BitConverter.ToUInt32(bytes, 0);
                                }
                                int delay = 1000 * mulHit.time / 30;
                                Task.Delay(delay).ContinueWith(_ =>
                                {
                                    try
                                    {
                                        if (text1 != _lastAnimationName && _interruptibleActionName.Contains(text1))
                                        {
                                            return;
                                        }
                                        lock (this)
                                        {
                                            _ftGui.AddFlyText((FlyTextKind)kind, actorIndex, (uint)tempVal, (uint)val2, shownActionName, tempText2, tempColor, (uint)icon);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        PluginLog.Debug($"An error has occurred in MultiHit AddFlyText");
                                    }
                                });
                            }
                        }
                        if (multiHitList == null || _showFinalActionName.Contains(text1))
                        {
                            string tempText2 = text2;
                            if (tempText2.Equals(String.Empty))
                            {
                                tempText2 = "\0";
                            }
                            int finalDelay = 0;
                            _finalDelay.TryGetValue(text1, out finalDelay);
                            // PluginLog.Debug($"maxTime:{maxTime} finalDelay:{finalDelay}");
                            int delay = 1000 * (maxTime + finalDelay) / 30;
                            Task.Delay(delay).ContinueWith(_ =>
                            {
                                try
                                {
                                    if (text1 != _lastAnimationName && _interruptibleActionName.Contains(text1))
                                    {
                                        return;
                                    }
                                    lock (this)
                                    {
                                        _ftGui.AddFlyText((FlyTextKind)kind, actorIndex, (uint)val1, (uint)val2, shownActionName, tempText2, (uint)color, (uint)icon);
                                    }
                                }
                                catch (Exception e)
                                {
                                    PluginLog.Debug($"An error has occurred in MultiHit AddFlyText");
                                }
                            });

                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    PluginLog.Error(e, $"Skipping");
                }
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in MultiHit");
            }
            _addFlyTextHook.Original(
                addonFlyText,
                actorIndex,
                messageMax,
                numbers,
                offsetNum,
                offsetNumMax,
                strings,
                offsetStr,
                offsetStrMax,
                unknown);
        }

        internal void updateAffectedAction()
        {
            var validActionName = new HashSet<string>();
            var interruptibleActionName = new HashSet<string>();
            var showHitActionName = new HashSet<string>();
            var showFinalActionName = new HashSet<string>();
            var finalDelay = new Dictionary<string, int>();
            var hasCustomActionName = new HashSet<string>();
            var customName = new Dictionary<string, string>();
            var multiHitMap = new Dictionary<string, List<Hit>>();
            foreach (var actionList in Configuration.actionGroups.Where(grp => grp.enabled).Select(grp => grp.actionList))
            {
                foreach (var act in actionList.Where(a => a.enabled))
                {
                    var action = _actionSheet.GetRow((uint)act.actionKey);
                    if (action == null)
                    {
                        continue;
                    }
                    string actionName = action.Name;
                    if (!validActionName.Contains(actionName))
                    {
                        validActionName.Add(actionName);
                    }
                    if (act.interruptible && !interruptibleActionName.Contains(actionName))
                    {
                        interruptibleActionName.Add(actionName);
                    }
                    if (act.showHit && !showHitActionName.Contains(actionName))
                    {
                        showHitActionName.Add(actionName);
                    }
                    if (act.showFinal && !showFinalActionName.Contains(actionName))
                    {
                        showFinalActionName.Add(actionName);
                        finalDelay[action.Name] = act.finalDelay;
                    }
                    if (act.hasCustomName && !hasCustomActionName.Contains(actionName))
                    {
                        hasCustomActionName.Add(actionName);
                        customName[action.Name] = act.customName;
                    }
                    multiHitMap[action.Name] = act.hitList;
                }
            }
            _validActionName = validActionName;
            _interruptibleActionName = interruptibleActionName;
            _showHitActionName = showHitActionName;
            _showFinalActionName = showFinalActionName;
            _finalDelay = finalDelay;
            _hasCustomActionName = hasCustomActionName;
            _customName = customName;
            _multiHitMap = multiHitMap;
        }

        private void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
        {
            if (!Configuration.Enabled)
            {
                _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                return;
            }
            try
            {
                int animationId = (int)_actionSheet.GetRow(effectHeader->ActionId).AnimationEnd.Row;
                if(animationId != -1)
                {
                    _lastAnimationName = _actionSheet.GetRow(effectHeader->ActionId).Name;
                }
                PluginLog.Debug($"--- source actor: {sourceCharacter->GameObject.ObjectID}, action id {effectHeader->ActionId}, anim id {effectHeader->AnimationId} numTargets: {effectHeader->TargetCount} animationId:{animationId} ---");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in MultiHit.");
            }

            _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        }

        internal void ExportGroups(string path)
        {
            try
            {
                DirectoryInfo d = new(path);
                foreach (var group in Configuration.actionGroups)
                {
                    var jsonStr = JsonConvert.SerializeObject(group);
                    var fileName = Path.Combine(d.FullName, group.name.Replace(' ', '_') + ".json");
                    File.WriteAllText(fileName, jsonStr);
                }
                PluginLog.Information($"Export {Configuration.actionGroups.Count} groups into {path}.");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"An error has occurred while exporting to {path}.");
            }
        }
        internal void ExportGroup(string path, ActionGroup group)
        {
            try
            {
                DirectoryInfo d = new(path);
                var jsonStr = JsonConvert.SerializeObject(group);
                var fileName = Path.Combine(d.FullName, group.name.Replace(' ', '_') + ".json");
                File.WriteAllText(fileName, jsonStr);
                PluginLog.Information($"Export 1 group into {path}.");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"An error has occurred while exporting to {path}.");
            }
        }
        internal void ImportGroup(string filename)
        {
            try
            {
                var jsonStr = File.ReadAllText(filename);
                var group = JsonConvert.DeserializeObject<ActionGroup>(jsonStr);
                if (group.name == null || group.actionList == null)
                {
                    return;
                }
                var tempActionList = new List<ActionMultiHit>();
                for (var i = 0; i < group.actionList.Count; i ++)
                {
                    var act = group.actionList[i];
                    var action = _actionSheet.GetRow((uint)act.actionKey);
                    if (action == null)
                    {
                        continue;
                    }
                    act.actionName = action.Name.ToString();
                    tempActionList.Add(act);
                }
                group.actionList = tempActionList;
                Configuration.actionGroups.Add(group);
                Configuration.Save();
                PluginLog.Information($"Imported group {group.name}.");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, $"An error has occurred while importing from {filename}.");
            }
        }

        private void OnFlyTextCreated(
                ref FlyTextKind kind,
                ref int val1,
                ref int val2,
                ref SeString text1,
                ref SeString text2,
                ref uint color,
                ref uint icon,
                ref float yOffset,
                ref bool handled
            )
        {
            return;
            /*
            try
            {
                var ftKind = kind;
                var ftVal1 = val1;
                var ftVal2 = val2;
                var ftText1 = text1?.TextValue.Replace("%", "%%");
                var ftText2 = text2?.TextValue.Replace("%", "%%");
                var ftColor = color;
                var ftIcon = icon;
                var ftDamageTypeIcon = damageTypeIcon;

                DebugLog(FlyText, $"flytext created: kind: {ftKind} ({(int)kind}), val1: {val1}, val2: {val2}, color: {color:X}, icon: {icon}, yOffset:{yOffset}");
                DebugLog(FlyText, $"text1: {ftText1} | text2: {ftText2}");

                if (!ftText1.StartsWith("[MultiHit]"))
                {
                    handled = true;
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        try
                        {
                            lock (this)
                            {
                                uint targetIdx = 0;
                                DebugLog(FlyText, $"targetIdx: {targetIdx}");
                                _ftGui.AddFlyText(ftKind, targetIdx, (uint)ftVal1, (uint)ftVal2, "[MultiHit]" + ftText1, ftText2, ftColor, ftIcon, ftDamageTypeIcon);
                            }
                        }
                        catch (Exception e)
                        {
                            DebugLog(FlyText, $"text1: {ftText1} | text2: {ftText2}");
                        }
                    });
                }
                else
                {
                    text1 = ftText1.Replace("[MultiHit]", "");
                }

            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in MultiHit");
            }
            */
        }

        public void Dispose()
        {
            _ftGui.FlyTextCreated -= OnFlyTextCreated;

            _addScreenLogHook?.Disable();
            _addScreenLogHook?.Dispose();
            _addFlyTextHook?.Disable();
            _addFlyTextHook?.Dispose();
            _receiveActionEffectHook?.Disable();
            _receiveActionEffectHook?.Dispose();

            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            
            this.CommandManager.RemoveHandler(CommandName);

        }

        private void OnCommand(string command, string args)
        {
            //MainWindow.IsOpen = true;
            ConfigWindow.IsOpen = true;
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }

        private int GetStrLenFromPtr(byte* p, uint maxSeekLen=64)
        {
            int strLen = 0;
            var tempP = p;
            while (*tempP != 0 && strLen < maxSeekLen) { tempP++; strLen++; }
            return strLen;
        }

        private SeString GetActorName(uint id)
        {
            return _objectTable.SearchById(id)?.Name ?? SeString.Empty;
        }
        private uint GetActorIdx(uint id)
        {
            uint idx = 0;
            using (IEnumerator<global::Dalamud.Game.ClientState.Objects.Types.GameObject> enumerator = _objectTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    global::Dalamud.Game.ClientState.Objects.Types.GameObject current = enumerator.Current;
                    if (!(current == null) && current.ObjectId == id)
                    {
                        return idx;
                    }
                    idx++;
                }
            }
            return 0;
        }
    }
}
