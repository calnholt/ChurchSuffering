using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

[DebugTab("UI Select Display")]
public class UISelectDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;

    public UISelectDisplaySystem(EntityManager em, GraphicsDevice gd)
        : base(em)
    {
        _graphicsDevice = gd;
        EventManager.Subscribe<CursorStateEvent>(OnCursorStateEvent);
        EventManager.Subscribe<HotKeySelectEvent>(OnHotKeySelectEvent);
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Enumerable.Empty<Entity>();

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        // Not used - this system only responds to events
    }

    private void OnCursorStateEvent(CursorStateEvent e)
    {
        // Check for UI element click: edge-triggered click with a valid UI element under cursor
        if (!e.IsAPressedEdge) return;
        
        if (e.TopEntity == null) return;
        
        var uiElement = e.TopEntity.GetComponent<UIElement>();
        if (uiElement == null) return;
		if (uiElement.IsPreventDefaultClick) return;
        
        // Only trigger for interactable UI elements
        if (!uiElement.IsInteractable) return;
        
        // Check if bounds are valid (non-degenerate)
        var bounds = uiElement.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2) return;
        
        // Create rectangular shockwave using UI element bounds
        var shockwave = new RectangularShockwaveEvent
        {
            BoundsCenterPx = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f),
            BoundsSizePx = new Vector2(bounds.Width, bounds.Height),
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

    private void OnHotKeySelectEvent(HotKeySelectEvent e)
    {
        if (e.Entity == null) return;
        
        var uiElement = e.Entity.GetComponent<UIElement>();
        if (uiElement == null) return;
        
        // Only trigger for interactable UI elements
        if (!uiElement.IsInteractable) return;
        
        // Check if bounds are valid (non-degenerate)
        var bounds = uiElement.Bounds;
        if (bounds.Width < 2 || bounds.Height < 2) return;
        
        // Create rectangular shockwave using UI element bounds
        var shockwave = new RectangularShockwaveEvent
        {
            BoundsCenterPx = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f),
            BoundsSizePx = new Vector2(bounds.Width, bounds.Height),
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

    [DebugEditable(DisplayName = "Duration (sec)", Step = 0.1f, Min = 0.1f, Max = 2.0f)]
    public float DurationSec { get; set; } = 0.4f;

    [DebugEditable(DisplayName = "Max Radius (px)", Step = 50f, Min = 100f, Max = 2000f)]
    public float MaxRadiusPx { get; set; } = 400f;

    [DebugEditable(DisplayName = "Ripple Width (px)", Step = 1f, Min = 5f, Max = 50f)]
    public float RippleWidthPx { get; set; } = 12f;

    [DebugEditable(DisplayName = "Strength", Step = 0.1f, Min = 0.1f, Max = 2.0f)]
    public float Strength { get; set; } = 1.5f;

    [DebugEditable(DisplayName = "Chromatic Aberration Amp", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float ChromaticAberrationAmp { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Chromatic Aberration Freq", Step = 0.1f, Min = 0f, Max = 10f)]
    public float ChromaticAberrationFreq { get; set; } = 3.14159f;

    [DebugEditable(DisplayName = "Shading Intensity", Step = 0.05f, Min = 0f, Max = 1.0f)]
    public float ShadingIntensity { get; set; } = 0.6f;
}
