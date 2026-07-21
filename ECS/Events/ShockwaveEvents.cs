using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Events;

public class ShockwaveEvent
{
    public Vector2 CenterPx { get; set; }
    public float DurationSec { get; set; }
    public float MaxRadiusPx { get; set; }
    public float RippleWidthPx { get; set; }
    public float Strength { get; set; }
    public float ChromaticAberrationAmp { get; set; }
    public float ChromaticAberrationFreq { get; set; }
    public float ShadingIntensity { get; set; }
}

public class RectangularShockwaveEvent
{
    public Vector2 BoundsCenterPx { get; set; }
    public Vector2 BoundsSizePx { get; set; }
    public float DurationSec { get; set; }
    public float MaxRadiusPx { get; set; }
    public float RippleWidthPx { get; set; }
    public float Strength { get; set; }
    public float ChromaticAberrationAmp { get; set; }
    public float ChromaticAberrationFreq { get; set; }
    public float ShadingIntensity { get; set; }
}


