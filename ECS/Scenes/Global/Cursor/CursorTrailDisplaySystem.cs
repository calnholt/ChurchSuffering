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

[DebugTab("Cursor Trail")]
public class CursorTrailDisplaySystem : Core.System
{
    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sb;
    private readonly ContentManager _content;

    private Effect _blurEffect;
    private GaussianBlurOverlay _blurOverlay;

    private RenderTarget2D _trailRt;
    private RenderTarget2D _blurA;
    private RenderTarget2D _blurB;

    private Vector2 _cursorPos;
    private bool _hasCursorPos;

    // Erase blend: punches a soft hole by multiplying dest by (1 - srcAlpha)
    private static readonly BlendState EraseBlend = new BlendState
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.InverseSourceAlpha,
        AlphaBlendFunction = BlendFunction.Add
    };

    // Additive blend for compositing trail over scene
    private static readonly BlendState AdditiveAlpha = new BlendState
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add
    };

    [DebugEditable(DisplayName = "Trail Decay", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TrailDecay { get; set; } = 0.95f;

    [DebugEditable(DisplayName = "Blur Radius", Step = 0.5f, Min = 0f, Max = 20f)]
    public float BlurRadius { get; set; } = 8.5f;

    [DebugEditable(DisplayName = "Trail Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TrailAlpha { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Trail R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TrailR { get; set; } = 0.6f;

    [DebugEditable(DisplayName = "Trail G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TrailG { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Trail B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TrailB { get; set; } = 1.0f;

    [DebugEditable(DisplayName = "Stamp Radius", Step = 1f, Min = 2f, Max = 128f)]
    public int StampRadius { get; set; } = 27;

    [DebugEditable(DisplayName = "Cutout Radius", Step = 1f, Min = 2f, Max = 128f)]
    public int CutoutRadius { get; set; } = 25;

    public CursorTrailDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ContentManager content)
        : base(em)
    {
        _gd = gd;
        _sb = sb;
        _content = content;

        EventManager.Subscribe<CursorStateEvent>(OnCursorState);
        EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
    }

    protected override IEnumerable<Entity> GetRelevantEntities() => Enumerable.Empty<Entity>();

    private void OnCursorState(CursorStateEvent e)
    {
        _cursorPos = e.Position;
        _hasCursorPos = true;
    }

    private void OnDeleteCaches(DeleteCachesEvent e)
    {
        DisposeTargets();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (!ShaderRuntimeOptions.ShadersEnabled) return;

        if (!_hasCursorPos) return;

        EnsureLoaded();
        if (_blurOverlay == null) return;
        EnsureTargets();

        // Save whatever render target is currently active
        var prevTargets = _gd.GetRenderTargets();

        // --- Step 1: Decay existing trail into _blurA ---
        _gd.SetRenderTarget(_blurA);

        _gd.Clear(Color.Transparent);

        // Draw previous trail with decay (fade via tint color)
        Color decayColor = new Color(TrailDecay, TrailDecay, TrailDecay, 1f);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(_trailRt, _gd.Viewport.Bounds, decayColor);
        _sb.End();

        // --- Step 2: Stamp cursor circle onto _blurA (additive) ---
        var stampTex = PrimitiveTextureFactory.GetAntiAliasedCircle(_gd, StampRadius);
        Color stampColor = new Color(TrailR, TrailG, TrailB, 1f);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(stampTex, _cursorPos, null, stampColor, 0f, new Vector2(StampRadius, StampRadius), 1f, SpriteEffects.None, 0f);
        _sb.End();

        // --- Step 3: Horizontal blur _blurA → _blurB ---
        _gd.SetRenderTarget(_blurB);
        _gd.Clear(Color.Transparent);
        _blurOverlay.BlurDirection = new Vector2(1, 0);
        _blurOverlay.BlurRadius = BlurRadius;
        _blurOverlay.Begin(_sb);
        _blurOverlay.Draw(_sb, _blurA);
        _blurOverlay.End(_sb);

        // --- Step 4: Vertical blur _blurB → _trailRt ---
        _gd.SetRenderTarget(_trailRt);
        _gd.Clear(Color.Transparent);
        _blurOverlay.BlurDirection = new Vector2(0, 1);
        _blurOverlay.Begin(_sb);
        _blurOverlay.Draw(_sb, _blurB);
        _blurOverlay.End(_sb);

        // Restore previous render target
        if (prevTargets.Length > 0)
            _gd.SetRenderTargets(prevTargets);
        else
            _gd.SetRenderTarget(null);
    }

    /// <summary>
    /// Draw the blurred trail over the current scene. Call during DrawScene() before cursor draw.
    /// The caller is responsible for ending/beginning any surrounding SpriteBatch.
    /// </summary>
    /// <param name="restoreTarget">The render target to restore after compositing (typically _sceneRt)</param>
    public void DrawTrail(RenderTarget2D restoreTarget)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled) return;
        if (_trailRt == null || _blurB == null || !_hasCursorPos) return;

        // Copy trail into _blurB so we can punch a hole without modifying _trailRt
        _gd.SetRenderTarget(_blurB);
        _gd.Clear(Color.Transparent);

        // Draw trail data into temp buffer
        _sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(_trailRt, _gd.Viewport.Bounds, Color.White);
        _sb.End();

        // Erase a soft circle at current cursor position so the trail doesn't show under the cursor sprite
        var cutoutTex = PrimitiveTextureFactory.GetAntiAliasedCircle(_gd, CutoutRadius);
        _sb.Begin(SpriteSortMode.Immediate, EraseBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(cutoutTex, _cursorPos, null, Color.White, 0f, new Vector2(CutoutRadius, CutoutRadius), 1f, SpriteEffects.None, 0f);
        _sb.End();

        // Explicitly restore to the provided target
        _gd.SetRenderTarget(restoreTarget);

        // Composite trail to scene
        Color tint = new Color(1f, 1f, 1f, TrailAlpha);
        _sb.Begin(SpriteSortMode.Immediate, AdditiveAlpha, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(_blurB, _gd.Viewport.Bounds, tint);
        _sb.End();
    }

    private void EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled) return;
        if (_blurEffect == null)
        {
            try { _blurEffect = _content.Load<Effect>("Shaders/GaussianBlur"); }
            catch { _blurEffect = null; }
        }
        if (_blurEffect != null && _blurOverlay == null)
        {
            _blurOverlay = new GaussianBlurOverlay(_blurEffect);
        }
    }

    private void EnsureTargets()
    {
        int w = Game1.VirtualWidth;
        int h = Game1.VirtualHeight;
        if (_trailRt != null && _trailRt.Width == w && _trailRt.Height == h) return;

        DisposeTargets();
        _trailRt = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _blurA = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _blurB = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
    }

    private void DisposeTargets()
    {
        _trailRt?.Dispose(); _trailRt = null;
        _blurA?.Dispose(); _blurA = null;
        _blurB?.Dispose(); _blurB = null;
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        throw new NotImplementedException();
    }
}
