using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using MultiHit.Windows;
using Dalamud.Game;
using Dalamud.Hooking;
using System;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using Dalamud.Logging;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using System.Threading.Tasks;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Text;
using Lumina.Excel;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Reflection;
using Dalamud.Plugin.Services;
using static Dalamud.Plugin.Services.IFlyTextGui;

namespace MultiHit
{
    public unsafe class Plugin : IDalamudPlugin
    {
        public string Name => "MultiHit";
        private const string CommandName = "/mhit";
        private const char SpecialChar = '\u00A7';
        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; }
        [PluginService]
        internal static ICommandManager CommandManager { get; set; }
        [PluginService]
        internal static IObjectTable ObjectTable { get; set; }
        [PluginService]
        internal static IFlyTextGui FTGui { get; set; }
        [PluginService]
        internal static IGameGui GameGui { get; set; }
        [PluginService]
        internal static IGameInteropProvider Hook { get; set; }
        [PluginService]
        internal static IClientState ClientState { get; set; }
        [PluginService]
        internal static IDataManager DataManager { get; set; }
        [PluginService]
        internal static ISigScanner Scanner { get; set; }
        [PluginService]
        internal static IPluginLog Log { get; set; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("MultiHit");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        internal static object[] _ftLocks = Enumerable.Repeat(new object(), 50).ToArray();

        private readonly ExcelSheet<Action> _actionSheet;
        public readonly List<Action> actionList;
        public readonly Dictionary<uint, Action> actionDict;
        private HashSet<string> _validActionName;
        private HashSet<string> _interruptibleActionName;
        private HashSet<string> _showHitActionName;
        private Dictionary<string, Hit> _finalHitMap;
        private HashSet<string> _hasCustomActionName;
        private Dictionary<string, string> _customName;
        private Dictionary<string, List<Hit>> _multiHitMap;
        private string _lastAnimationName = "undefined";
        private HashSet<FlyTextKind> _validKinds = new HashSet<FlyTextKind>() {
            FlyTextKind.Damage,
            FlyTextKind.DamageCrit,
            FlyTextKind.DamageDh,
            FlyTextKind.DamageCritDh
        };

        /*
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
        */

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


        private OnFlyTextCreatedDelegate _flyTextCreated;
        private delegate void CrashingTick(
                IntPtr a1,
                IntPtr a2,
                IntPtr a3,
                IntPtr a4
            );
        // private readonly Hook<CrashingTick> _crashingTickHook;

        private delegate void ReceiveActionEffectDelegate(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail);
        private readonly Hook<ReceiveActionEffectDelegate> _receiveActionEffectHook;



        public Plugin()
        {
            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);
            
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open MultiHit window."
            });

            try
            {
                _actionSheet = DataManager.GetExcelSheet<Action>();

                if (_actionSheet == null)
                    throw new NullReferenceException();

                actionList = new();
                actionDict = new();
                foreach (var action in _actionSheet)
                {
                    actionList.Add(action);
                    actionDict.Add(action.RowId, action);
                }

                this.updateAffectedAction();

                var receiveActionEffectFuncPtr = Scanner.ScanText("40 55 56 57 41 54 41 55 41 56 48 8D AC 24 ?? ?? ?? ??");
                _receiveActionEffectHook = Hook.HookFromAddress<ReceiveActionEffectDelegate>(receiveActionEffectFuncPtr, ReceiveActionEffect);

                /*
                var addScreenLogPtr = scanner.ScanText("E8 ?? ?? ?? ?? BF ?? ?? ?? ?? 41 F6 87");
                _addScreenLogHook = Hook<AddScreenLogDelegate>.FromAddress(addScreenLogPtr, AddScreenLogDetour);
                var crashingTickPtr = scanner.ScanText("E8 ?? ?? ?? ?? 48 8B 45 28 48 8B CE");
                _crashingTickHook = _hook.HookFromAddress<CrashingTick>(crashingTickPtr, CrashingTickDetour);
                */

                var addFlyTextAddress = Scanner.ScanText("E8 ?? ?? ?? ?? FF C7 41 D1 C7");
                _addFlyTextHook = Hook.HookFromAddress<AddFlyTextDelegate>(addFlyTextAddress, AddFlyTextDetour);


                _flyTextCreated = (OnFlyTextCreatedDelegate)FTGui.GetType().GetField("FlyTextCreated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(FTGui);

            }
            catch (Exception ex)
            {
                Log.Error(ex, $"An error occurred loading MultiHit Plugin.");
                Log.Error("Plugin will not be loaded.");

                // _addScreenLogHook?.Disable();
                // _addScreenLogHook?.Dispose();
                // _crashingTickHook?.Disable();
                // _crashingTickHook?.Dispose();
                _addFlyTextHook?.Disable();
                _addFlyTextHook?.Dispose();
                _receiveActionEffectHook?.Disable();
                _receiveActionEffectHook?.Dispose();

                throw;
            }

            _receiveActionEffectHook?.Enable();
            // _addScreenLogHook.Enable();
            // _crashingTickHook?.Enable();
            _addFlyTextHook?.Enable();
            //_ftGui.FlyTextCreated += OnFlyTextCreated;


            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        /*
        private void CrashingTickDetour(nint a1, nint a2, nint a3, nint a4)
        {
            try
            {
                _crashingTickHook.Original(a1, a2, a3, a4);
            }
            catch (Exception e)
            {
                _pluginLog.Error(e, "An error occurred in MultiHit CrashingTickDetour.");
            }
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
                _pluginLog.Error(e, "An error occurred in MultiHit.");
            }

            _addScreenLogHook.Original(target, source, logKind, option, actionKind, actionId, val1, val2, serverAttackType, val4);
        }
        */

        [Obsolete]
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
            if (!Configuration.Enabled || actorIndex <= 1 || actorIndex >= 50)
            {
                // don't lock this since locks may not be enough
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
                // actual index
                var strIndex = 27;
                var numIndex = 30;
                // dalamud's call
                // var strIndex = 25;
                // var numIndex = 28;
                var atkArrayDataHolder = ((UIModule*)GameGui.GetUIModule())->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
                Log.Debug($"addonFlyText: {addonFlyText:X} actorIndex:{actorIndex} offsetNum: {offsetNum} offsetNumMax: {offsetNumMax} offsetStr: {offsetStr} offsetStrMax: {offsetStrMax} unknown:{unknown}");
                try
                {
                    var strArray = atkArrayDataHolder._StringArrays[strIndex];
                    var flyText1Ptr = strArray->StringArray[offsetStr];
                    if (flyText1Ptr == null || (nint)flyText1Ptr == IntPtr.Zero)
                    {
                        lock (_ftLocks[actorIndex])
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
                        }
                        return;
                    }
                    var numArray = atkArrayDataHolder._NumberArrays[numIndex];
                    var kind = numArray->IntArray[offsetNum + 1];
                    var val1 = numArray->IntArray[offsetNum + 2];
                    var val2 = numArray->IntArray[offsetNum + 3];
                    int damageTypeIcon = numArray->IntArray[offsetNum + 4];
                    int color = numArray->IntArray[offsetNum + 6];
                    int icon = numArray->IntArray[offsetNum + 7];
                    var text1 = Marshal.PtrToStringUTF8((nint)flyText1Ptr);
                    var flyText2Ptr = strArray->StringArray[offsetStr + 1];
                    var text2 = Marshal.PtrToStringUTF8((nint)flyText2Ptr);
                    Log.Debug($"text1:{text1} text2:{text2}");
                    if (text1 == null || text2 == null)
                    {
                        lock (_ftLocks[actorIndex])
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
                        }
                        return;
                    }
                    if (text1.EndsWith(SpecialChar) && text1.Length >= 1)
                    {
                        var bytes = Encoding.UTF8.GetBytes(text1.Substring(0, text1.Length - 1));
                        Marshal.WriteByte((nint)flyText1Ptr + bytes.Length, 0);
                        lock (_ftLocks[actorIndex])
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
                        }
                        return;
                    }
                    FlyTextKind flyKind = (FlyTextKind)kind;
                    // _pluginLog.Debug($"flyKind:{flyKind}");

                    if (_validActionName.Contains(text1) && _validKinds.Contains(flyKind))
                    {
                        Log.Debug($"kind:{flyKind} actorIndex:{actorIndex} val1:{val1} val2:{val2} text1:{text1} text2:{text2} color:{(uint)color:X} icon:{icon}");
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
                        if (shownActionName == null || val1 <= 0 || val1 > int.MaxValue)
                        {
                            Log.Debug($"val1:{val1} is not valid");
                            lock (_ftLocks[actorIndex])
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
                            }
                            return;
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
                                var tempText2 = _showHitActionName.Contains(text1) ? $"Hit#{hitIdx}" : text2;
                                if (tempText2 == null || tempText2.Equals(string.Empty))
                                {
                                    tempText2 = "\0";
                                }
                                int tempVal = (int)(val1 * (mulHit.percent * 1.0f / 100f));
                                maxTime = Math.Max(maxTime, mulHit.time);
                                uint tempColor = mulHit.color;
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
                                        lock (_ftLocks[actorIndex])
                                        {
                                            TryAddFlyText((FlyTextKind)kind, actorIndex, tempVal, val2, shownActionName, tempText2, tempColor, (uint)icon, (uint)damageTypeIcon);
                                            //_ftGui.AddFlyText((FlyTextKind)kind, actorIndex, (uint)tempVal, (uint)val2, shownActionName, tempText2, tempColor, (uint)icon, (uint)damageTypeIcon);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Error(e, "An error has occurred in MultiHit AddFlyText");
                                    }
                                });
                            }
                        }
                        if (multiHitList == null || _finalHitMap.ContainsKey(text1))
                        {
                            var tempText2 = text2;
                            if (tempText2 == null || tempText2.Equals(string.Empty))
                            {
                                tempText2 = "\0";
                            }
                            _finalHitMap.TryGetValue(text1, out var finalHit);
                            int finalDelay = finalHit.time;
                            int delay = 1000 * (maxTime + finalDelay) / 30;
                            uint tempColor = finalHit.color;
                            if ((tempColor & 0xFF) == 0)
                            {
                                tempColor = (uint)color;
                            }
                            else
                            {
                                byte[] bytes = BitConverter.GetBytes(tempColor);
                                Array.Reverse(bytes, 0, bytes.Length);
                                tempColor = BitConverter.ToUInt32(bytes, 0);
                            }
                            Task.Delay(delay).ContinueWith(_ =>
                            {
                                try
                                {
                                    if (text1 != _lastAnimationName && _interruptibleActionName.Contains(text1))
                                    {
                                        return;
                                    }
                                    lock (_ftLocks[actorIndex])
                                    {
                                        TryAddFlyText((FlyTextKind)kind, actorIndex, val1, val2, shownActionName, tempText2, (uint)tempColor, (uint)icon, (uint)damageTypeIcon);
                                        //_ftGui.AddFlyText((FlyTextKind)kind, actorIndex, (uint)val1, (uint)val2, shownActionName, tempText2, (uint)tempColor, (uint)icon, (uint)damageTypeIcon);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "An error has occurred in MultiHit AddFlyText");
                                }
                            });
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Skipping");
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "An error has occurred in MultiHit");
            }
            // Not helpful actually
            lock (_ftLocks[actorIndex])
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
            }
        }


        private void TryAddFlyText(FlyTextKind kind, uint actorIndex, int val1, int val2, string text1, string text2, uint color, uint icon, uint damageTypeIcon)
        {

            float yOffset = 0;
            var handled = false;

            var tmpKind = kind;
            var tmpVal1 = val1;
            var tmpVal2 = val2;
            var tmpText1 = new SeString(new TextPayload(text1));
            var tmpText2 = new SeString(new TextPayload(text2));
            var tmpColor = color;
            var tmpIcon = icon;
            var tmpDamageTypeIcon = damageTypeIcon;
            var tmpYOffset = yOffset;

            if (_flyTextCreated == null)
            {
                Log.Debug("No delegate found.");
            }
            else
            {
                Log.Debug($"Found flyTextCreated delegates: {_flyTextCreated.GetInvocationList().Length}");
                _flyTextCreated.Invoke(
                    ref tmpKind,
                    ref tmpVal1,
                    ref tmpVal2,
                    ref tmpText1,
                    ref tmpText2,
                    ref tmpColor,
                    ref tmpIcon,
                    ref tmpDamageTypeIcon,
                    ref tmpYOffset,
                    ref handled
                 );
            }

            if(handled)
            {
                return;
            }

            FTGui.AddFlyText(tmpKind, actorIndex, (uint)tmpVal1, (uint)tmpVal2, tmpText1.ToString() + SpecialChar, tmpText2.ToString(), tmpColor, tmpIcon, tmpDamageTypeIcon);
        }

        internal void validateActionGroups()
        {
            if(!Configuration.validateActionGroups) { return; }
            var actionGroups = CollectionsMarshal.AsSpan(Configuration.actionGroups);
            for (var groupIdx = 0; groupIdx < Configuration.actionGroups.Count; groupIdx++)
            {
                ref var group = ref actionGroups[groupIdx];
                var actionList = CollectionsMarshal.AsSpan(group.actionList);
                for (var actionIdx = 0; actionIdx < group.actionList.Count; actionIdx++)
                {
                    ref var mulHit = ref actionList[actionIdx];
                    while(mulHit.hitList.Select(hit => hit.percent).Sum() > 100)
                    {
                        mulHit.hitList.RemoveAt(mulHit.hitList.Count - 1);
                    }
                }
            }
        }

        internal void updateAffectedAction()
        {
            var validActionName = new HashSet<string>();
            var interruptibleActionName = new HashSet<string>();
            var showHitActionName = new HashSet<string>();
            var finalHitMap = new Dictionary<string, Hit>();
            var hasCustomActionName = new HashSet<string>();
            var customName = new Dictionary<string, string>();
            var multiHitMap = new Dictionary<string, List<Hit>>();
            foreach (var actionList in Configuration.actionGroups.Where(grp => grp.enabled).Select(grp => grp.actionList))
            {
                foreach (var mulHit in actionList.Where(a => a.enabled))
                {
                    var action = actionDict.GetValueOrDefault((uint)mulHit.actionKey);
                    if (action == null)
                    {
                        continue;
                    }
                    string actionName = action.Name;
                    if (!validActionName.Contains(actionName))
                    {
                        validActionName.Add(actionName);
                    }
                    if (mulHit.interruptible && !interruptibleActionName.Contains(actionName))
                    {
                        interruptibleActionName.Add(actionName);
                    }
                    if (mulHit.showHit && !showHitActionName.Contains(actionName))
                    {
                        showHitActionName.Add(actionName);
                    }
                    if (mulHit.showFinal && !finalHitMap.ContainsKey(actionName))
                    {
                        finalHitMap[action.Name] = mulHit.finalHit;
                    }
                    if (mulHit.hasCustomName && !hasCustomActionName.Contains(actionName))
                    {
                        hasCustomActionName.Add(actionName);
                        customName[action.Name] = mulHit.customName;
                    }
                    multiHitMap[action.Name] = mulHit.hitList;
                }
            }
            _validActionName = validActionName;
            _interruptibleActionName = interruptibleActionName;
            _showHitActionName = showHitActionName;
            _finalHitMap = finalHitMap;
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
                if (sourceCharacter == null)
                {
                    _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                    return;
                }
                var oID = sourceCharacter->GameObject.EntityId;
                if(ClientState.LocalPlayer == null || oID != ClientState.LocalPlayer.GameObjectId)
                {
                    Log.Debug($"--- source actor: {sourceCharacter->GameObject.EntityId} is not self, skipping");
                    _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                    return;
                }
                var action = actionDict.GetValueOrDefault(effectHeader->ActionId);
                if (action == null)
                {
                    Log.Debug("action is null");
                    _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
                    return;
                }
                int animationId = (int)action.AnimationEnd.Row;
                if(animationId != -1)
                {
                    _lastAnimationName = action.Name;
                }
                Log.Debug($"--- source actor: {sourceCharacter->GameObject.EntityId}, action id {effectHeader->ActionId}, anim id {effectHeader->AnimationId} numTargets: {effectHeader->TargetCount} animationId:{animationId} ---");
            }
            catch (Exception e)
            {
                Log.Error(e, "An error has occurred in MultiHit.");
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
                Log.Information($"Export {Configuration.actionGroups.Count} groups into {path}.");
            }
            catch (Exception e)
            {
                Log.Error(e, $"An error has occurred while exporting to {path}.");
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
                Log.Information($"Export 1 group into {path}.");
            }
            catch (Exception e)
            {
                Log.Error(e, $"An error has occurred while exporting to {path}.");
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
                    var action = actionDict.GetValueOrDefault((uint)act.actionKey);
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
                Log.Information($"Imported group {group.name}.");
            }
            catch (Exception e)
            {
                Log.Error(e, $"An error has occurred while importing from {filename}.");
            }
        }

        /*
        private void OnFlyTextCreated(
                ref FlyTextKind kind,
                ref int val1,
                ref int val2,
                ref SeString text1,
                ref SeString text2,
                ref uint color,
                ref uint icon,
                ref uint damageTypeIcon,
                ref float yOffset,
                ref bool handled
            )
        {
            return;
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
                            lock(_ftLock)
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
                _pluginLog.Error(e, "An error has occurred in MultiHit");
            }
        }
        */

        public void Dispose()
        {
            //_ftGui.FlyTextCreated -= OnFlyTextCreated;

            //_addScreenLogHook?.Disable();
            //_addScreenLogHook?.Dispose();
            // _crashingTickHook?.Disable();
            // _crashingTickHook?.Dispose();
            _addFlyTextHook?.Disable();
            _addFlyTextHook?.Dispose();
            _receiveActionEffectHook?.Disable();
            _receiveActionEffectHook?.Dispose();

            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            
            CommandManager.RemoveHandler(CommandName);

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
            return ObjectTable.SearchById(id)?.Name ?? SeString.Empty;
        }
        private uint GetActorIdx(uint id)
        {
            uint idx = 0;
            using (var enumerator = ObjectTable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var current = enumerator.Current;
                    if (!(current == null) && current.GameObjectId == id)
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
