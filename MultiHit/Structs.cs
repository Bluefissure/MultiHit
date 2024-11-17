using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MultiHit;

public unsafe struct CastbarInfo
{
    public AtkUnitBase* unitBase;
    public AtkImageNode* gauge;
    public AtkImageNode* bg;

    public bool Valid()
    {
        return unitBase != null && gauge != null && bg != null;
    }

    public void ResetIfValid()
    {
        if (Valid())
            Reset();
    }

    public void Reset()
    {
        gauge->AtkResNode.Color.R = 0xFF;
        gauge->AtkResNode.Color.G = 0xFF;
        gauge->AtkResNode.Color.B = 0xFF;
        gauge->AtkResNode.Color.A = 0xFF;

        bg->AtkResNode.Color.R = 0xFF;
        bg->AtkResNode.Color.G = 0xFF;
        bg->AtkResNode.Color.B = 0xFF;
        bg->AtkResNode.Color.A = 0xFF;
    }

    public void Color(Vector4 gaugeColor, Vector4 bgColor)
    {
        gauge->AtkResNode.Color.R = (byte)(gaugeColor.X * 255);
        gauge->AtkResNode.Color.G = (byte)(gaugeColor.Y * 255);
        gauge->AtkResNode.Color.B = (byte)(gaugeColor.Z * 255);
        gauge->AtkResNode.Color.A = (byte)(gaugeColor.W * 255);

        bg->AtkResNode.Color.R = (byte)(bgColor.X * 255);
        bg->AtkResNode.Color.G = (byte)(bgColor.Y * 255);
        bg->AtkResNode.Color.B = (byte)(bgColor.Z * 255);
        bg->AtkResNode.Color.A = (byte)(bgColor.W * 255);
    }

    public static bool operator !=(CastbarInfo cb1, CastbarInfo cb2) => !cb1.Equals(cb2);
    public static bool operator ==(CastbarInfo cb1, CastbarInfo cb2) => cb1.Equals(cb2);
    public bool Equals(CastbarInfo other) => unitBase == other.unitBase && gauge == other.gauge && bg == other.bg;
    public override bool Equals(object obj) => obj is CastbarInfo other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(unchecked((int)(long)unitBase), unchecked((int)(long)gauge), unchecked((int)(long)bg));
}

[StructLayout(LayoutKind.Explicit)]
public struct EffectHeader
{
    [FieldOffset(8)] public uint ActionId;
    [FieldOffset(28)] public ushort AnimationId;
    [FieldOffset(33)] public byte TargetCount;
}

public struct EffectEntry
{
    public ActionEffectType type;
    public byte param0;
    public byte param1;
    public byte param2;
    public byte mult;
    public byte flags;
    public ushort value;

    public byte AttackType => (byte)(param1 & 0xF);

    public override string ToString()
    {
        return
            $"Type: {type}, p0: {param0:D3}, p1: {param1:D3}, p2: {param2:D3} 0x{param2:X2} '{Convert.ToString(param2, 2).PadLeft(8, '0')}', mult: {mult:D3}, flags: {flags:D3} | {Convert.ToString(flags, 2).PadLeft(8, '0')}, value: {value:D6} ATTACK TYPE: {AttackType} DAMAGE TYPE: {((AttackType)AttackType).ToDamageType()}";
    }
}

public struct EffectTail
{

}

public struct ActionEffectInfo
{
    public ActionStep step;
    public ulong tick;

    public uint actionId;
    public ActionEffectType type;
    public DamageType damageType;
    public FlyTextKind kind;
    public uint sourceId;
    public ulong targetId;
    public uint value;
    public PositionalState positionalState;

    public bool Equals(ActionEffectInfo other) => step == other.step && tick == other.tick && actionId == other.actionId && type == other.type && damageType == other.damageType && kind == other.kind && sourceId == other.sourceId && targetId == other.targetId && value == other.value && positionalState == other.positionalState;
    public override bool Equals(object obj) => obj is ActionEffectInfo other && Equals(other);
    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add((int)step);
        hashCode.Add(tick);
        hashCode.Add(actionId);
        hashCode.Add((int)type);
        hashCode.Add(damageType);
        hashCode.Add((int)kind);
        hashCode.Add(sourceId);
        hashCode.Add(targetId);
        hashCode.Add(value);
        hashCode.Add((int)positionalState);
        return hashCode.ToHashCode();
    }

    public override string ToString() => $"{nameof(step)}: {step}, {nameof(tick)}: {tick}, {nameof(actionId)}: {actionId}, {nameof(type)}: {type}, {nameof(damageType)}: {damageType}, {nameof(kind)}: {kind}, {nameof(sourceId)}: {sourceId}, {nameof(targetId)}: {targetId}, {nameof(value)}: {value}, {nameof(positionalState)}: {positionalState}";
}

public class Ref<T> where T : struct
{
    public T Value { get; set; }
}


public struct Hit
{
    public int time;  // 30 for 1 second
    public int percent;  // percentage %
    public uint color = 0;  // percentage %
    public Hit(int time = 0, int percent = 0, uint color = 0) : this()
    {
        this.time = time;
        this.percent = percent;
        this.color = color;
    }
}


public struct ActionMultiHit
{
    public int actionKey = -1;
    public string actionName;  // note this may be different in different language
    public bool enabled;
    public bool interruptible;
    public bool showHit = true;
    public bool showFinal = false;
    public bool hasCustomName = false;
    public string customName = "";
    public List<Hit> hitList;
    public Hit finalHit;
    public ActionMultiHit(
        int actionKey,
        string actionName = "",
        bool enabled = true,
        bool interruptible = true,
        bool showHit = true,
        bool showFinal = false,
        bool hasCustomName = false,
        string customName = "",
        List<Hit>? hitList = null,
        Hit? finalHit = null) : this()
    {
        this.actionKey = actionKey;
        this.actionName = actionName;
        this.enabled = enabled;
        this.interruptible = interruptible;
        this.showHit = showHit;
        this.showFinal = showFinal;
        this.hasCustomName = hasCustomName;
        this.customName = customName;
        this.hitList = hitList ?? new();
        this.finalHit = finalHit ?? new(1, 100);
    }

    public override string ToString() => $"{actionName}#{actionKey}";
}


public struct ActionGroup
{
    public string name;
    public bool enabled;
    public List<ActionMultiHit> actionList;

    public ActionGroup(string name, bool enabled = true, List<ActionMultiHit>? actionList = null) : this()
    {
        this.name = name;
        this.enabled = enabled;
        this.actionList = actionList ?? new();
    }
}

public struct FlyTextParam
{
    public FlyTextKind kind;
    public uint actorIndex;
    public uint val1;
    public uint val2;
    public SeString text1;
    public SeString text2;
    public uint color;
    public uint icon;
    public uint damageIcon;
    public FlyTextParam(FlyTextKind kind, uint actorIndex, uint val1, uint val2, SeString text1, SeString text2, uint color, uint icon, uint damageIcon)
    {
        this.kind = kind;
        this.actorIndex = actorIndex;
        this.val1 = val1;
        this.val2 = val2;
        this.text1 = text1;
        this.text2 = text2;
        this.color = color;
        this.icon = icon;
        this.damageIcon = damageIcon;
    }
}
