using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
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
    private Vector2 _previousStampPos;
    private bool _hasPreviousStampPos;
    private PlayerInputDevice _cursorSource;
    private bool _hasCursorSource;
    private readonly List<Vector2> _stampPositions = new();

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

    [DebugEditable(DisplayName = "Outer R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OuterR { get; set; } = 1f;

    [DebugEditable(DisplayName = "Outer G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OuterG { get; set; } = 1f;

    [DebugEditable(DisplayName = "Outer B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OuterB { get; set; } = 1f;

    [DebugEditable(DisplayName = "Core R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreR { get; set; } = 1f;

    [DebugEditable(DisplayName = "Core G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreG { get; set; } = 0f;

    [DebugEditable(DisplayName = "Core B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreB { get; set; } = 0f;

    [DebugEditable(DisplayName = "Stamp Radius", Step = 1f, Min = 2f, Max = 128f)]
    public int StampRadius { get; set; } = 27;

    [DebugEditable(DisplayName = "Core Radius", Step = 1f, Min = 2f, Max = 128f)]
    public int CoreRadius { get; set; } = 14;

    [DebugEditable(DisplayName = "Stamp Spacing", Step = 1f, Min = 1f, Max = 128f)]
    public float StampSpacing { get; set; } = 10f;

    [DebugEditable(DisplayName = "Max Bridge Distance", Step = 25f, Min = 50f, Max = 3000f)]
    public float MaxBridgeDistance { get; set; } = 800f;

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
        if (_hasCursorSource && e.Source != _cursorSource)
        {
            _hasPreviousStampPos = false;
        }

        _cursorPos = e.Position;
        _cursorSource = e.Source;
        _hasCursorPos = true;
        _hasCursorSource = true;
    }

    private void OnDeleteCaches(DeleteCachesEvent e)
    {
        DisposeTargets();
        _hasPreviousStampPos = false;
        _hasCursorSource = false;
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
        float elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float frameDecay = CalculateFrameDecay(TrailDecay, elapsedSeconds);
        Color decayColor = new Color(frameDecay, frameDecay, frameDecay, 1f);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _sb.Draw(_trailRt, _gd.Viewport.Bounds, decayColor);
        _sb.End();

        // --- Step 2: Fill cursor movement gaps with a white outer trail and red core ---
        BuildStampPositions(
            _stampPositions,
            _hasPreviousStampPos ? _previousStampPos : null,
            _cursorPos,
            StampSpacing,
            MaxBridgeDistance);
        _previousStampPos = _cursorPos;
        _hasPreviousStampPos = true;

        int outerRadius = Math.Max(1, StampRadius);
        var stampTex = PrimitiveTextureFactory.GetAntiAliasedCircle(_gd, outerRadius);
        Color stampColor = new Color(OuterR, OuterG, OuterB, 1f);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Game1.Display.SpriteBatchTransform);
        foreach (Vector2 position in _stampPositions)
        {
            _sb.Draw(stampTex, position, null, stampColor, 0f, new Vector2(outerRadius, outerRadius), 1f, SpriteEffects.None, 0f);
        }
        _sb.End();

        int coreRadius = Math.Max(1, Math.Min(CoreRadius, outerRadius));
        var coreTex = PrimitiveTextureFactory.GetAntiAliasedCircle(_gd, coreRadius);
        Color coreColor = new Color(CoreR, CoreG, CoreB, 1f);
        _sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Game1.Display.SpriteBatchTransform);
        foreach (Vector2 position in _stampPositions)
        {
            _sb.Draw(coreTex, position, null, coreColor, 0f, new Vector2(coreRadius, coreRadius), 1f, SpriteEffects.None, 0f);
        }
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
        _sb.Begin(SpriteSortMode.Immediate, EraseBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Game1.Display.SpriteBatchTransform);
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
        int w = Game1.Display.RenderWidth;
        int h = Game1.Display.RenderHeight;
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

    internal static void BuildStampPositions(
        List<Vector2> positions,
        Vector2? previousPosition,
        Vector2 currentPosition,
        float spacing,
        float maxBridgeDistance)
    {
        positions.Clear();
        if (!previousPosition.HasValue)
        {
            positions.Add(currentPosition);
            return;
        }

        Vector2 previous = previousPosition.Value;
        float distance = Vector2.Distance(previous, currentPosition);
        if (distance <= 0f || distance > Math.Max(0f, maxBridgeDistance))
        {
            positions.Add(currentPosition);
            return;
        }

        int segmentCount = Math.Max(1, (int)Math.Ceiling(distance / Math.Max(1f, spacing)));
        for (int i = 1; i <= segmentCount; i++)
        {
            positions.Add(Vector2.Lerp(previous, currentPosition, i / (float)segmentCount));
        }
    }

    internal static float CalculateFrameDecay(float decayAtSixtyFps, float elapsedSeconds)
    {
        float clampedDecay = MathHelper.Clamp(decayAtSixtyFps, 0f, 1f);
        return MathF.Pow(clampedDecay, Math.Max(0f, elapsedSeconds) * 60f);
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        throw new NotImplementedException();
    }
}
