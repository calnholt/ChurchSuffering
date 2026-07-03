using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays floating damage/heal text when HP is modified.
    /// Subscribes to ModifyHpEvent; spawns ephemeral floaters at target body center.
    /// </summary>
    [DebugTab("Damage/Healing Text")]
    public class DamageModificationDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.ContentFont;

		private class Floater
        {
			public int TargetEntityId;
			public Entity Target;
			public Guid PresentationId;
            public Vector2 StartWorldPos;
            public float AgeSeconds;
            public float LifetimeSeconds;
            public int Amount; // signed; negative damage, positive heal
            public bool IsHeal;
            public float PopScale; // 0..1 internal state
        }

        private readonly List<Floater> _floaters = new List<Floater>();

        // Debug controls
        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 3.0f)]
        public float BaseTextScale { get; set; } = 0.5f;

        [DebugEditable(DisplayName = "Pop Duration (s)", Step = 0.01f, Min = 0.01f, Max = 1.0f)]
        public float PopDurationSeconds { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Float Duration (s)", Step = 0.05f, Min = 0.1f, Max = 4.0f)]
        public float FloatDurationSeconds { get; set; } = 1.2f;

        [DebugEditable(DisplayName = "Rise Pixels", Step = 2, Min = -400, Max = 400)]
        public int RisePixels { get; set; } = 52;

        [DebugEditable(DisplayName = "Horizontal Jitter", Step = 1, Min = 0, Max = 120)]
        public int HorizontalJitter { get; set; } = 8;

		[DebugEditable(DisplayName = "Offset % X (-1..1)", Step = 0.01f, Min = -1f, Max = 1f)]
		public float OffsetPercentX { get; set; } = 0f;

		[DebugEditable(DisplayName = "Offset % Y (-1..1)", Step = 0.01f, Min = -1f, Max = 1f)]
		public float OffsetPercentY { get; set; } = -.5f;

		[DebugEditable(DisplayName = "Offset X", Step = 1, Min = -2000, Max = 2000)]
		public int OffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Offset Y", Step = 1, Min = -2000, Max = 2000)]
		public int OffsetY { get; set; } = 0;

        [DebugEditable(DisplayName = "Damage R", Step = 1, Min = 0, Max = 255)]
        public int DamageR { get; set; } = 220;
        [DebugEditable(DisplayName = "Damage G", Step = 1, Min = 0, Max = 255)]
        public int DamageG { get; set; } = 60;
        [DebugEditable(DisplayName = "Damage B", Step = 1, Min = 0, Max = 255)]
        public int DamageB { get; set; } = 60;

        [DebugEditable(DisplayName = "Heal R", Step = 1, Min = 0, Max = 255)]
        public int HealR { get; set; } = 255;
        [DebugEditable(DisplayName = "Heal G", Step = 1, Min = 0, Max = 255)]
        public int HealG { get; set; } = 255;
        [DebugEditable(DisplayName = "Heal B", Step = 1, Min = 0, Max = 255)]
        public int HealB { get; set; } = 255;

        [DebugEditable(DisplayName = "Max Concurrent", Step = 1, Min = 1, Max = 64)]
        public int MaxConcurrent { get; set; } = 16;

        [DebugEditable(DisplayName = "Spawn From HPBar Center")] 
        public bool SpawnFromHpBarCenter { get; set; } = false;

        [DebugEditable(DisplayName = "Outline Offset", Step = 1, Min = 0, Max = 10)]
        public int OutlineOffset { get; set; } = 6;

        public DamageModificationDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            EventManager.Subscribe<ModifyHpEvent>(OnModifyHp);
            EventManager.Subscribe<HealEvent>(e => OnModifyHp(new ModifyHpEvent { Target = e.Target, Delta = e.Delta, DamageType = ModifyTypeEnum.Heal }));
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            // Presentation-only; reacts to events
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = _floaters.Count - 1; i >= 0; i--)
            {
                var f = _floaters[i];
                f.AgeSeconds += dt;
                if (PopDurationSeconds > 0f)
                {
                    float tp = MathHelper.Clamp(f.AgeSeconds / PopDurationSeconds, 0f, 1f);
                    float overshoot = 1.2f;
                    float s = tp < 1f ? MathHelper.Lerp(0.2f, overshoot, 1f - (float)Math.Pow(1f - tp, 3f)) : 1f;
                    f.PopScale = s;
                }
                else
                {
                    f.PopScale = 1f;
                }
                bool expired = f.AgeSeconds >= f.LifetimeSeconds;
                if (expired)
                {
					PublishCompletion(f);
                    _floaters.RemoveAt(i);
                    continue;
                }
                _floaters[i] = f;
            }
            base.Update(gameTime);
        }

        private void OnModifyHp(ModifyHpEvent e)
        {
            LoggingService.Append("DamageModificationDisplaySystem.OnModifyHp", new JsonObject {
                { "Delta", e.Delta },
                { "DamageType", e.DamageType.ToString() }
            });
            var target = ResolveTarget(e.Target);
            if (target == null) return;
            int amt = e.Delta;
            if (amt == 0) return;
            bool isHeal = amt > 0;
            if (_floaters.Count >= MaxConcurrent)
            {
                // Drop oldest
				PublishCompletion(_floaters[0]);
                _floaters.RemoveAt(0);
            }

            Vector2 center = ComputeBodyCenter(target);
            if (SpawnFromHpBarCenter)
            {
                var anchor = target.GetComponent<HPBarAnchor>();
                if (anchor != null)
                {
                    center = new Vector2(anchor.Rect.X + anchor.Rect.Width / 2f, anchor.Rect.Y + anchor.Rect.Height / 2f);
                }
            }

            // Small random x jitter so multiple floaters are readable
            float jx = HorizontalJitter > 0 ? (Random.Shared.NextSingle() * 2f - 1f) * HorizontalJitter : 0f;

			if (e.PresentationId != Guid.Empty)
			{
				EventManager.Publish(new BattlePresentationStarted
				{
					PresentationId = e.PresentationId,
					Target = target,
					Kind = BattlePresentationKind.DamageNumber
				});
			}

            _floaters.Add(new Floater
            {
                TargetEntityId = target.Id,
                Target = target,
				PresentationId = e.PresentationId,
                StartWorldPos = new Vector2(center.X + jx, center.Y),
                Amount = Math.Abs(amt),
                IsHeal = isHeal,
                AgeSeconds = 0f,
                LifetimeSeconds = Math.Max(0.05f, FloatDurationSeconds),
                PopScale = 0f
            });
        }

		private static void PublishCompletion(Floater floater)
		{
			if (floater == null || floater.PresentationId == Guid.Empty) return;
			EventManager.Publish(new BattlePresentationCompleted
			{
				PresentationId = floater.PresentationId,
				Target = floater.Target,
				Kind = BattlePresentationKind.DamageNumber
			});
		}

        private Entity ResolveTarget(Entity explicitTarget)
        {
            if (explicitTarget != null) return explicitTarget;
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player != null) return player;
            return EntityManager.GetEntitiesWithComponent<HP>().FirstOrDefault();
        }

        private Vector2 ComputeBodyCenter(Entity entity)
        {
            var t = entity.GetComponent<Transform>();
            if (t == null) return Vector2.Zero;
            var portrait = entity.GetComponent<PortraitInfo>();
            if (portrait != null && portrait.TextureWidth > 0 && portrait.TextureHeight > 0)
            {
                // Use portrait center
                return t.Position;
            }
            return t.Position;
        }

        public void Draw()
        {
            if (_font == null) return;
            if (_floaters.Count == 0) return;

            // Draw pass
            foreach (var f in _floaters)
            {
                float t01 = MathHelper.Clamp(f.AgeSeconds / Math.Max(0.0001f, f.LifetimeSeconds), 0f, 1f);
                // Rise with ease-out
                float rise = RisePixels * (1f - (float)Math.Pow(1f - t01, 2f));
			// Start from target body center each frame so percentage offsets follow the target
			var currentCenter = ComputeBodyCenter(f.Target);
			var pos = new Vector2(currentCenter.X, currentCenter.Y - rise);
			// Apply percentage offsets relative to target visual bounds if available, else screen center as fallback
			float px = 0f, py = 0f;
			var pInfo = f.Target?.GetComponent<PortraitInfo>();
			if (pInfo != null && pInfo.TextureWidth > 0 && pInfo.TextureHeight > 0)
			{
				float baseScale = (pInfo.BaseScale > 0f) ? pInfo.BaseScale : 1f;
				float halfW = (pInfo.TextureWidth * baseScale) * 0.5f;
				float halfH = (pInfo.TextureHeight * baseScale) * 0.5f;
				px = OffsetPercentX * halfW;
				py = OffsetPercentY * halfH;
			}
			else
			{
				// Fallback: use viewport half-size to interpret percents
				px = OffsetPercentX * (Game1.VirtualWidth / 2f);
				py = OffsetPercentY * (Game1.VirtualHeight / 2f);
			}
			pos.X += px + OffsetX;
			pos.Y += py + OffsetY;
                // Fade out towards the end
                float fadeStart = 0.4f; // start fading after 40% lifetime
                float alpha = 1f;
                if (t01 > fadeStart)
                {
                    float ft = (t01 - fadeStart) / (1f - fadeStart);
                    alpha = MathHelper.Clamp(1f - ft, 0f, 1f);
                }

                string text = (f.IsHeal ? "+" : "-") + f.Amount.ToString();
                float scale = BaseTextScale * Math.Max(0.1f, f.PopScale);
                var color = f.IsHeal ? new Color(HealR, HealG, HealB) : new Color(DamageR, DamageG, DamageB);
                color *= alpha;
                // Center text
                var size = _font.MeasureString(text) * scale;
                var drawPos = new Vector2(pos.X - size.X / 2f, pos.Y - size.Y / 2f);

                // Draw black outline (all pixel combinations within OutlineOffset)
                Color outlineColor = Color.Black * alpha;
                for (int ox = -OutlineOffset; ox <= OutlineOffset; ox++)
                {
                    for (int oy = -OutlineOffset; oy <= OutlineOffset; oy++)
                    {
                        if (ox == 0 && oy == 0) continue; // skip center
                        var outlinePos = new Vector2(drawPos.X + ox, drawPos.Y + oy);
                        _spriteBatch.DrawString(_font, text, outlinePos, outlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }
                }

                // Draw main colored text on top
                _spriteBatch.DrawString(_font, text, drawPos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
