using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using MultiHit.Windows;
using Dalamud.Game;
using Dalamud.Hooking;
using System;

using static MultiHit.LogType;
using Action = Lumina.Excel.GeneratedSheets.Action;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using Dalamud.Logging;
using ImGuiNET;
using Dalamud.Data;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.ClientState.Objects;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using FFXIVClientStructs.Interop;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Text;
using FFXIVClientStructs.FFXIV.Component.Excel;
using Lumina.Excel;

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
        private int _lastAnimationId = -1;

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

        private delegate void WhatEverFuncDelegate(nint a1, nint a2, nint a3, uint a4, int a5, uint a6, nint a7, int a8, char a9);
        private readonly Hook<WhatEverFuncDelegate> _whatEverFuncHook;

        private delegate nint WhatEverFunc2Delegate(nint a1, nint a2, nint a3, nint a4, nint a5);
        private readonly Hook<WhatEverFunc2Delegate> _whatEverFunc2Hook;



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

                var receiveActionEffectFuncPtr = scanner.ScanText("4C 89 44 24 ?? 55 56 41 54 41 55 41 56");
                _receiveActionEffectHook = Hook<ReceiveActionEffectDelegate>.FromAddress(receiveActionEffectFuncPtr, ReceiveActionEffect);

                var addScreenLogPtr = scanner.ScanText("E8 ?? ?? ?? ?? BF ?? ?? ?? ?? 41 F6 87");
                _addScreenLogHook = Hook<AddScreenLogDelegate>.FromAddress(addScreenLogPtr, AddScreenLogDetour);

                var whatEverFuncPtr = scanner.ScanText("E8 ?? ?? ?? ?? 48 FF C7 48 83 FF 08 0F 8C ?? ?? ?? ?? 4C 8B 7C 24 ??");
                _whatEverFuncHook = Hook<WhatEverFuncDelegate>.FromAddress(whatEverFuncPtr, WhatEverFuncDetour);
                var whatEverFunc2Ptr = scanner.ScanText("E8 ?? ?? ?? ?? 80 7E 21 00");
                _whatEverFunc2Hook = Hook<WhatEverFunc2Delegate>.FromAddress(whatEverFunc2Ptr, WhatEverFunc2Detour);

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
                _whatEverFuncHook?.Disable();
                _whatEverFuncHook?.Dispose();
                _whatEverFunc2Hook?.Disable();
                _whatEverFunc2Hook?.Dispose();

                throw;
            }

            _whatEverFuncHook?.Enable();
            _whatEverFunc2Hook?.Enable();
            _receiveActionEffectHook?.Enable();
            _addScreenLogHook.Enable();
            _addFlyTextHook.Enable();
            _ftGui.FlyTextCreated += OnFlyTextCreated;


            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        private void WhatEverFuncDetour(nint a1, nint a2, nint a3, uint a4, int a5, uint a6, nint a7, int a8, char a9)
        {
            try
            {
                //PluginLog.Information($"[WhatEver] a1:{a1:X} a2:{a2:X} a3:{a3:X} a4:{a4} a5:{a5} a6:{a6} a7:{a7:X} a8:{a8} a9:{(int)a9}");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error occurred in MultiHit.");
            }
            _whatEverFuncHook.Original(a1, a2, a3, a4, a5, a6, a7, a8, a9);
        }


        private nint WhatEverFunc2Detour(nint a1, nint a2, nint a3, nint a4, nint a5)
        {
            try
            {
                byte v3_33 = Marshal.ReadByte(a3 + 33);
                //PluginLog.Information($"[WhatEver] a1:{a1:X} a2:{a2:X} a3:{a3:X} v3_33:{v3_33} a4:{a4:X} a5:{a5:X}");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error occurred in MultiHit.");
            }
            return _whatEverFunc2Hook.Original(a1, a2, a3, a4, a5);
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
                PluginLog.Error(e, "An error occurred in MultiHit.");
            }

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
            try
            {
                // Known valid flytext region within the atk arrays
                var strIndex = 27;
                var numIndex = 30;
                var atkArrayDataHolder = ((UIModule*)_gameGui.GetUIModule())->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
                DebugLog(FlyText, $"addonFlyText: {addonFlyText:X} actorIndex:{actorIndex} offsetNum: {offsetNum} offsetNumMax: {offsetNumMax} offsetStr: {offsetStr} offsetStrMax: {offsetStrMax} unknown:{unknown}");
                try
                {
                    var strArray = atkArrayDataHolder.GetStringArrayData(strIndex);
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
                    var numArray = atkArrayDataHolder.GetNumberArrayData(numIndex);
                    int kind = numArray->IntArray[offsetNum + 1];
                    int val1 = numArray->IntArray[offsetNum + 2];
                    int val2 = numArray->IntArray[offsetNum + 3];
                    int damageTypeIcon = numArray->IntArray[offsetNum + 4];
                    int color = numArray->IntArray[offsetNum + 6];
                    int icon = numArray->IntArray[offsetNum + 7];
                    //_ftGui.AddFlyText(ftKind, targetIdx, (uint)ftVal1, (uint)ftVal2, "[MultiHit]" + ftText1, ftText2, ftColor, ftIcon, ftDamageTypeIcon);
                    DebugLog(FlyText, $"kind:{kind} actorIndex:{actorIndex} val1:{val1} val2:{val2} text1:{text1} text2:{text2} color:{color} icon:{icon} damageTypeIcon:{damageTypeIcon}");
                    if (text1 == "Brutal Shell" || text1 == "Demon Slice" || text1 == "Prominence")
                    {
                        int num_hits = 5;
                        int left_val = val1;
                        for (int i = 0; i < num_hits; i++)
                        {
                            int tmp_val = (i == num_hits - 1) ? left_val : val1 / num_hits;
                            left_val -= tmp_val;
                            int tmp_i = i + 1;
                            Task.Delay(1000 * 70 / 30 / num_hits * i + 1000 / 30 * 10).ContinueWith(_ =>
                            {
                                try
                                {
                                    int animationId = (int)_actionSheet.GetRow(16139).AnimationEnd.Row;
                                    if (animationId != _lastAnimationId)
                                    {
                                        return;
                                    }
                                    lock (this)
                                    {
                                        _ftGui.AddFlyText((FlyTextKind)kind, actorIndex, (uint)tmp_val, (uint)val2, text1, $"Hit#{tmp_i}", (uint)color, (uint)icon, (uint)damageTypeIcon);
                                    }
                                }
                                catch (Exception e)
                                {
                                    DebugLog(FlyText, $"An error has occurred in MultiHit AddFlyText");
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

        private void ReceiveActionEffect(uint sourceId, Character* sourceCharacter, IntPtr pos, EffectHeader* effectHeader, EffectEntry* effectArray, ulong* effectTail)
        {
            try
            {
                int animationId = (int)_actionSheet.GetRow(effectHeader->ActionId).AnimationEnd.Row;
                if(animationId != -1)
                {
                    _lastAnimationId = animationId;
                }
                DebugLog(Effect, $"--- source actor: {sourceCharacter->GameObject.ObjectID}, action id {effectHeader->ActionId}, anim id {effectHeader->AnimationId} numTargets: {effectHeader->TargetCount} animationId:{animationId} ---");
            }
            catch (Exception e)
            {
                PluginLog.Error(e, "An error has occurred in MultiHit.");
            }

            _receiveActionEffectHook.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTail);
        }

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
            _whatEverFuncHook?.Disable();
            _whatEverFuncHook?.Dispose();
            _whatEverFunc2Hook?.Disable();
            _whatEverFunc2Hook?.Dispose();

            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            
            this.CommandManager.RemoveHandler(CommandName);

        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            MainWindow.IsOpen = true;
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
        private void DebugLog(LogType type, string str)
        {
            PluginLog.Information($"[{type}] {str}");
        }
    }
}
