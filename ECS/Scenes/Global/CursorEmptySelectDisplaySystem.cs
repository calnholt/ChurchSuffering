using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems;

[DebugTab("Cursor Empty Select")]
public class CursorEmptySelectDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;

    public CursorEmptySelectDisplaySystem(EntityManager em, GraphicsDevice gd)
        : base(em)
    {
        _graphicsDevice = gd;
        EventManager.Subscribe<CursorStateEvent>(OnCursorStateEvent);
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Enumerable.Empty<Entity>();

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        // Not used - this system only responds to events
    }

    private void OnCursorStateEvent(CursorStateEvent e)
    {
        // Check for empty click: edge-triggered click with no UI element under cursor or UI element is not interactable
        bool isEmptyClick = e.IsAPressedEdge && (e.TopEntity == null || 
            (e.TopEntity != null && e.TopEntity.GetComponent<UIElement>()?.IsInteractable == false));
        
        if (isEmptyClick)
        {
            var shockwave = new ShockwaveEvent
            {
                CenterPx = e.Position,
                DurationSec = DurationSec,
                MaxRadiusPx = MaxRadiusPx,
                RippleWidthPx = RippleWidthPx,
                Strength = Strength,
                ChromaticAberrationAmp = ChromaticAberrationAmp,
                ChromaticAberrationFreq = ChromaticAberrationFreq,
                ShadingIntensity = ShadingIntensity
            };
            EventManager.Publish(shockwave);
        }
    }

    [DebugEditable(DisplayName = "Duration (sec)", Step = 0.1f, Min = 0.1f, Max = 2.0f)]
    public float DurationSec { get; set; } = 0.2f;

    [DebugEditable(DisplayName = "Max Radius (px)", Step = 50f, Min = 100f, Max = 2000f)]
    public float MaxRadiusPx { get; set; } = 200f;

    [DebugEditable(DisplayName = "Ripple Width (px)", Step = 1f, Min = 5f, Max = 50f)]
    public float RippleWidthPx { get; set; } = 10f;

    [DebugEditable(DisplayName = "Strength", Step = 0.1f, Min = 0.1f, Max = 2.0f)]
    public float Strength { get; set; } = 2.0f;

    [DebugEditable(DisplayName = "Chromatic Aberration Amp", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float ChromaticAberrationAmp { get; set; } = 0f;

    [DebugEditable(DisplayName = "Chromatic Aberration Freq", Step = 0.1f, Min = 0f, Max = 10f)]
    public float ChromaticAberrationFreq { get; set; } = 0f;

    [DebugEditable(DisplayName = "Shading Intensity", Step = 0.05f, Min = 0f, Max = 1.0f)]
    public float ShadingIntensity { get; set; } = 0.6f;
}

