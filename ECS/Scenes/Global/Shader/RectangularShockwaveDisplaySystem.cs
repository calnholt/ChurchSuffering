using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

[DebugTab("Rectangular Shockwave System")]
public class RectangularShockwaveDisplaySystem : Core.System
{
    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sb;
    private readonly ContentManager _content;

    private Effect _effect;
    private RectangularShockwaveOverlay _overlay;
    private float _timeSeconds;

    private struct ActiveWave
    {
        public RectangularShockwaveEvent Evt;
        public float StartTime;
    }

    private readonly List<ActiveWave> _waves = new();

    public bool HasActiveWaves => ShaderRuntimeOptions.ShadersEnabled && _waves.Count > 0;

    public RectangularShockwaveDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ContentManager content)
        : base(em)
    {
        _gd = gd;
        _sb = sb;
        _content = content;
        EventManager.Subscribe<RectangularShockwaveEvent>(OnRectangularShockwaveEvent);
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Enumerable.Empty<Entity>();

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!ShaderRuntimeOptions.ShadersEnabled) return;
        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null) EnsureLoaded();
        if (_overlay == null) return;

        float now = _timeSeconds;
        for (int i = _waves.Count - 1; i >= 0; i--)
        {
            var w = _waves[i];
            float tNorm = (now - w.StartTime) / MathHelper.Max(1e-4f, w.Evt.DurationSec);
            if (tNorm >= 1f) _waves.RemoveAt(i);
        }
    }

    private void OnRectangularShockwaveEvent(RectangularShockwaveEvent e)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled) return;
        EnsureLoaded();
        if (_overlay == null) return;
        Console.WriteLine($"[RectangularShockwaveDisplaySystem] Adding rectangular shockwave event: {e.BoundsCenterPx}, {e.BoundsSizePx}, {e.DurationSec}, {e.MaxRadiusPx}, {e.RippleWidthPx}, {e.Strength}, {e.ChromaticAberrationAmp}, {e.ChromaticAberrationFreq}, {e.ShadingIntensity}");
        _waves.Add(new ActiveWave { Evt = e, StartTime = _timeSeconds });
    }

    private void EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled) return;
        if (_effect == null)
        {
            try { _effect = _content.Load<Effect>("Shaders/RectangularShockwave"); }
            catch { _effect = null; }
        }
        if (_effect != null && _overlay == null)
        {
            _overlay = new RectangularShockwaveOverlay(_effect);
        }
    }

    // Composites all active waves over sceneSrc and presents to backbuffer using ping-pong render targets
    public void Composite(Texture2D sceneSrc, RenderTarget2D ppA, RenderTarget2D ppB, RenderTarget2D finalTarget = null)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled)
        {
            if (sceneSrc == null) return;
            if (finalTarget != null && ReferenceEquals(sceneSrc, finalTarget))
            {
                _gd.SetRenderTarget(null);
                return;
            }

            _gd.SetRenderTarget(finalTarget);
            _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _sb.Draw(sceneSrc, _gd.Viewport.Bounds, Color.White);
            _sb.End();
            return;
        }

        if (_overlay == null || sceneSrc == null)
        {
            // Fallback: blit original scene
            if (finalTarget != null && ReferenceEquals(sceneSrc, finalTarget))
            {
                _gd.SetRenderTarget(null);
                return;
            }

            _gd.SetRenderTarget(finalTarget);
            _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _sb.Draw(sceneSrc, _gd.Viewport.Bounds, Color.White);
            _sb.End();
            return;
        }

        // Nothing to do
        if (_waves.Count == 0)
        {
            if (finalTarget != null && ReferenceEquals(sceneSrc, finalTarget))
            {
                _gd.SetRenderTarget(null);
                return;
            }

            _gd.SetRenderTarget(finalTarget);
            _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _sb.Draw(sceneSrc, _gd.Viewport.Bounds, Color.White);
            _sb.End();
            return;
        }

        // Start with the original scene in src
        Texture2D src = sceneSrc;

        float now = _timeSeconds;
        foreach (var w in _waves)
        {
            RenderTarget2D dst = null;

            if (ppA != null && !ReferenceEquals(src, ppA))
            {
                dst = ppA;
            }
            else if (ppB != null && !ReferenceEquals(src, ppB))
            {
                dst = ppB;
            }
            else if (ppA != null)
            {
                dst = ppA;
            }
            else if (ppB != null)
            {
                dst = ppB;
            }

            if (dst == null || ReferenceEquals(dst, src))
            {
                // Unable to secure a distinct render target; stop compositing further waves.
                break;
            }

            _gd.SetRenderTarget(dst);
            _gd.Clear(Color.Black);

            float tNorm = MathHelper.Clamp((now - w.StartTime) / MathHelper.Max(1e-4f, w.Evt.DurationSec), 0f, 1f);

            _overlay.BoundsCenterPx = w.Evt.BoundsCenterPx;
            _overlay.BoundsSizePx = w.Evt.BoundsSizePx;
            _overlay.TimeNorm = tNorm;
            _overlay.MaxRadiusPx = w.Evt.MaxRadiusPx;
            _overlay.RippleWidthPx = w.Evt.RippleWidthPx;
            _overlay.Strength = w.Evt.Strength;
            _overlay.ChromaticAberrationAmp = w.Evt.ChromaticAberrationAmp;
            _overlay.ChromaticAberrationFreq = w.Evt.ChromaticAberrationFreq;
            _overlay.ShadingIntensity = w.Evt.ShadingIntensity;

            _overlay.Begin(_sb);
            _overlay.Draw(_sb, src);
            _overlay.End(_sb);

            src = dst;
        }

        if (finalTarget != null && ReferenceEquals(finalTarget, src))
        {
            _gd.SetRenderTarget(null);
            return;
        }

        _gd.SetRenderTarget(finalTarget);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(src, _gd.Viewport.Bounds, Color.White);
        _sb.End();
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        throw new System.NotImplementedException();
    }

    [DebugAction("Trigger Rectangular Shockwave")]
    public void Debug_TriggerRectangularShockwave()
    {
        var center = new Vector2(Game1.VirtualWidth * 0.5f, Game1.VirtualHeight * 0.5f);
        var size = new Vector2(200f, 150f);
        var e = new RectangularShockwaveEvent
        {
            BoundsCenterPx = center,
            BoundsSizePx = size,
            DurationSec = 0.6f,
            MaxRadiusPx = Math.Min(Game1.VirtualWidth, Game1.VirtualHeight) * 0.6f,
            RippleWidthPx = 18f,
            Strength = 1.0f,
            ChromaticAberrationAmp = 0.05f,
            ChromaticAberrationFreq = 3.14159f,
            ShadingIntensity = 0.6f
        };
        EventManager.Publish(e);
    }
}
