namespace MultiHit;

public enum ActionEffectType : byte
{
    Nothing = 0,
    Miss = 1,
    FullResist = 2,
    Damage = 3,
    Heal = 4,
    BlockedDamage = 5,
    ParriedDamage = 6,
    Invulnerable = 7,
    NoEffectText = 8,
    Unknown_0 = 9,
    MpLoss = 10,
    MpGain = 11,
    TpLoss = 12,
    TpGain = 13,
    GpGain = 14,
    ApplyStatusEffectTarget = 15,
    ApplyStatusEffectSource = 16,
    StatusNoEffect = 20,
    Unknown0 = 27,
    Unknown1 = 28,
    Knockback = 33,
    Mount = 40,
    VFX = 59,
    JobGauge = 61,
};

public enum PositionalState
{
    Ignore,
    Success,
    Failure
}

public enum AttackType
{
    Unknown = 0,
    Slashing = 1,
    Piercing = 2,
    Blunt = 3,
    Shot = 4,
    Magical = 5,
    Unique = 6,
    Physical = 7,
    LimitBreak = 8,
}

public enum SeDamageType
{
    None = 0,
    Physical = 60011,
    Magical = 60012,
    Unique = 60013,
}

public enum DamageType
{
    None = 0,
    Physical = 1,
    Magical = 2,
    Unique = 3,
}

public enum ActionStep
{
    None,
    Effect,
    Screenlog,
    Flytext,
}

public enum LogType
{
    FlyText,
    Castbar,
    ScreenLog,
    Effect,
    FlyTextWrite
}
