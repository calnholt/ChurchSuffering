using System;
using System.Text.Json.Nodes;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

[DebugTab("Card Sheen")]
internal sealed class CardSheenOverlayPass : ICardOverlayPass, ICardOverlaySnapshotTimeControl
{
	private readonly EntityManager _entityManager;
	private readonly ContentManager _content;

	private Effect _effect;
	private CardSheenOverlay _overlay;
	private bool _failed;
	private float _timeSeconds;

	[DebugEditable(DisplayName = "Angle Degrees", Step = 1f, Min = 0f, Max = 360f)]
	public float AngleDegrees { get; set; } = 105f;

	[DebugEditable(DisplayName = "Band Width", Step = 0.01f, Min = 0.01f, Max = 0.5f)]
	public float BandWidthNormalized { get; set; } = 0.11f;

	[DebugEditable(DisplayName = "Feather", Step = 0.01f, Min = 0.001f, Max = 0.3f)]
	public float FeatherNormalized { get; set; } = 0.06f;

	[DebugEditable(DisplayName = "Core Width", Step = 0.01f, Min = 0.001f, Max = 0.2f)]
	public float CoreWidthNormalized { get; set; } = 0.025f;

	[DebugEditable(DisplayName = "Corner Radius Px", Step = 1f, Min = 0f, Max = 80f)]
	public float CornerRadiusPx { get; set; } = 11f;

	[DebugEditable(DisplayName = "Intensity", Step = 0.01f, Min = 0f, Max = 2f)]
	public float Intensity { get; set; } = 0.55f;

	[DebugEditable(DisplayName = "Sheen Duration Seconds", Step = 0.01f, Min = 0.01f, Max = 3f)]
	public float SheenDurationSeconds { get; set; } = 0.84f;

	[DebugEditable(DisplayName = "Repeat Delay Seconds", Step = 0.01f, Min = 0f, Max = 10f)]
	public float RepeatDelaySeconds { get; set; } = 2f;

	[DebugEditable(DisplayName = "Gold Fringe Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float GoldFringeStrength { get; set; } = 0.28f;

	[DebugEditable(DisplayName = "Blue Fringe Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float BlueFringeStrength { get; set; } = 0.18f;

	[DebugEditable(DisplayName = "Core Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float CoreStrength { get; set; } = 0.55f;

	public CardSheenOverlayPass(
		EntityManager entityManager,
		ContentManager content)
	{
		_entityManager = entityManager;
		_content = content;
	}

	public string Name => "CardSheen";

	public void Update(GameTime gameTime)
	{
		_timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
		foreach (Entity entity in _entityManager.GetEntitiesWithComponent<CardSheen>())
		{
			var sheen = entity.GetComponent<CardSheen>();
			if (sheen == null) continue;
			if (!sheen.IsActive)
			{
				sheen.HasActivationTime = false;
				continue;
			}

			if (!sheen.HasActivationTime)
			{
				sheen.ActivationTimeSeconds = _timeSeconds;
				sheen.HasActivationTime = true;
			}
		}

		if (!ShaderRuntimeOptions.ShadersEnabled || _failed || _overlay != null) return;
		foreach (var entity in _entityManager.GetEntitiesWithComponent<CardSheen>())
		{
			EnsureLoaded();
			return;
		}
	}

	public bool AppliesTo(Entity card)
	{
		if (!ShaderRuntimeOptions.ShadersEnabled || _failed || card == null) return false;
		CardSheen sheen = card.GetComponent<CardSheen>();
		return sheen?.IsActive == true && sheen.HasActivationTime;
	}

	public void Render(CardOverlayPassContext context)
	{
		if (context == null || !ShaderRuntimeOptions.ShadersEnabled || !EnsureLoaded()) return;
		CardSheen sheen = context.Card.GetComponent<CardSheen>();
		if (sheen?.IsActive != true || !sheen.HasActivationTime) return;

		CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
			_entityManager,
			context.Card,
			context.Position,
			context.Scale,
			context.Rotation);
		ConfigureOverlay(sheen, context.Scale);
		_overlay.Resolution = context.LogicalSize;
		_overlay.CardCenter = context.ToSurface(geometry.Center);
		_overlay.CardRotation = context.Rotation;
		Vector2 size = new(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
		context.Apply("CardSheen", (spriteBatch, source) =>
		{
			_overlay.BeginComposite(spriteBatch);
			_overlay.DrawComposite(spriteBatch, source, size);
			_overlay.End(spriteBatch);
		});
	}

	private void ConfigureOverlay(CardSheen sheen, float scale)
	{
		_overlay.TimeSeconds = MathHelper.Max(0f, _timeSeconds - sheen.ActivationTimeSeconds);
		_overlay.SheenDurationSeconds = MathHelper.Max(0.001f, SheenDurationSeconds);
		_overlay.RepeatDelaySeconds = MathHelper.Max(0f, RepeatDelaySeconds);
		_overlay.AngleRadians = MathHelper.ToRadians(AngleDegrees);
		_overlay.BandWidthNormalized = BandWidthNormalized;
		_overlay.FeatherNormalized = FeatherNormalized;
		_overlay.CoreWidthNormalized = CoreWidthNormalized;
		_overlay.CornerRadiusPx = CornerRadiusPx * scale;
		_overlay.Intensity = Intensity;
		_overlay.GoldFringeColor = new Vector3(1f, 240f / 255f, 164f / 255f) * GoldFringeStrength;
		_overlay.BlueFringeColor = new Vector3(101f / 255f, 209f / 255f, 1f) * BlueFringeStrength;
		_overlay.CoreColor = new Vector3(1f, 254f / 255f, 240f / 255f) * CoreStrength;
	}

	private bool EnsureLoaded()
	{
		if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
		try
		{
			_effect ??= _content.Load<Effect>("Shaders/CardSheen");
			_overlay ??= new CardSheenOverlay(_effect);
			return _overlay.IsAvailable;
		}
		catch (Exception exception)
		{
			LoggingService.Append("CardSheenOverlayPass.EnsureLoaded", new JsonObject
			{
				["error"] = "Failed to load shader",
				["exception"] = exception.Message,
			});
			_failed = true;
			return false;
		}
	}

	public void Reset()
	{
		_effect = null;
		_overlay = null;
		_failed = false;
	}

	public void SetSnapshotTime(float timeSeconds) => _timeSeconds = Math.Max(0f, timeSeconds);
}
