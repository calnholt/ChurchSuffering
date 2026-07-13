#nullable enable

using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Components;

public struct UIInteractionSettings : IComponent
{
    public bool SuppressAllClicks;
    public bool TutorialActive;
}

public struct UIInteractionSettingsSingleton : ITag
{
}

public struct TutorialInteractionPermitted : ITag
{
}

public struct EquipmentHighlightSettings : IComponent
{
    public float GlowSpread;
    public float GlowSpreadSpeed;
    public float GlowSpreadAmplitude;
    public float MaxAlpha;
    public float GlowPulseSpeed;
    public float GlowEasingPower;
    public float GlowMinIntensity;
    public float GlowMaxIntensity;
    public int GlowLayers;
    public int CornerRadius;
    public int HighlightBorderThickness;
    public byte GlowColorR;
    public byte GlowColorG;
    public byte GlowColorB;
}

public struct HighlightSettingsSingleton : ITag
{
}

[System.Flags]
public enum ScheduledTimerFlags : byte
{
    None = 0,
    Running = 1 << 0,
    Repeating = 1 << 1,
}

public struct ScheduledTimer : IComponent
{
    public float RemainingSeconds;
    public float IntervalSeconds;
    public int Sequence;
    public ScheduledTimerFlags Flags;
}
