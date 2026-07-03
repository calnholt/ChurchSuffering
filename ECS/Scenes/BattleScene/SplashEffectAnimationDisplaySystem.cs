using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays attack splash images over targets when HP damage occurs.
    /// Subscribes to ModifyHpEvent; shows enemy-attack-splash.png when player is attacked,
    /// player-attack-splash.png when player attacks.
    /// Also displays gain-aegis.png when aegis is applied via ApplyPassiveEvent.
    /// </summary>
    [DebugTab("Splash Effect Animation")]
    public class SplashEffectAnimationDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();

        private class AnimationInstance
        {
            public int TargetEntityId;
            public Entity Target;
            public Guid PresentationId;
            public Texture2D Texture;
            public float AgeSeconds;
            public float FadeInDurationSeconds;
            public float HoldDurationSeconds;
            public float FadeOutDurationSeconds;
            public float TotalDurationSeconds;
        }

        private readonly List<AnimationInstance> _animations = new List<AnimationInstance>();

        // Debug controls
        [DebugEditable(DisplayName = "Fade In Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2.0f)]
        public float FadeInDurationSeconds { get; set; } = 0.05f;

        [DebugEditable(DisplayName = "Hold Duration (s)", Step = 0.01f, Min = 0.0f, Max = 2.0f)]
        public float HoldDurationSeconds { get; set; } = 0.35f;

        [DebugEditable(DisplayName = "Fade Out Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2.0f)]
        public float FadeOutDurationSeconds { get; set; } = 0.15f;

        [DebugEditable(DisplayName = "Image Scale (% of Viewport)", Step = 0.01f, Min = 0.01f, Max = 1.0f)]
        public float ImageScale { get; set; } = 0.16f;

        [DebugEditable(DisplayName = "Offset % X (-1..1)", Step = 0.01f, Min = -1f, Max = 1f)]
        public float OffsetPercentX { get; set; } = 0f;

        [DebugEditable(DisplayName = "Offset % Y (-1..1)", Step = 0.01f, Min = -1f, Max = 1f)]
        public float OffsetPercentY { get; set; } = -0.15f;

        [DebugEditable(DisplayName = "Offset X", Step = 1, Min = -2000, Max = 2000)]
        public int OffsetX { get; set; } = 0;

        [DebugEditable(DisplayName = "Offset Y", Step = 1, Min = -2000, Max = 2000)]
        public int OffsetY { get; set; } = 0;

        [DebugEditable(DisplayName = "Max Concurrent", Step = 1, Min = 1, Max = 64)]
        public int MaxConcurrent { get; set; } = 8;

        public SplashEffectAnimationDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;
            LoadTextures();
            EventManager.Subscribe<ModifyHpEvent>(OnModifyHp);
            EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
        }

        private void LoadTextures()
        {
            string[] textureKeys = { 
                "enemy-attack-splash", 
                "player-attack-splash", 
                "gain-aegis", 
                "gain-burn", 
                "gain-armor", 
                "gain-aggression", 
                "gain-power", 
                "gain-bleed", 
                "gain-frostbite", 
                "gain-slow", 
                "gain-sub-zero",
                "gain-windchill",
                "gain-wounded",
                "gain-inferno",
                "gain-enflamed"
            };
            foreach (var key in textureKeys)
            {
                _textures[key] = _imageAssets.TryGetTexture(key);
            }
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
            for (int i = _animations.Count - 1; i >= 0; i--)
            {
                var anim = _animations[i];
                anim.AgeSeconds += dt;
                
                bool expired = anim.AgeSeconds >= anim.TotalDurationSeconds;
                if (expired)
                {
                    PublishCompletion(anim);
                    _animations.RemoveAt(i);
                    continue;
                }
                _animations[i] = anim;
            }
            base.Update(gameTime);
        }

        private void OnModifyHp(ModifyHpEvent e)
        {
            // Only show for damage. Player self-damage effects still get damage text,
            // but not an attack splash.
            if (e.Delta >= 0)
                return;

            var target = ResolveTarget(e.Target);
            if (target == null) return;

            // Determine which texture to use
            Texture2D textureToUse = null;
            bool isPlayerTarget = target.HasComponent<Player>();
            bool isPlayerSource = e.Source != null && e.Source.HasComponent<Player>();
            bool isEnemyTarget = target.HasComponent<Enemy>();

            if (isPlayerTarget && e.DamageType == ModifyTypeEnum.Attack)
            {
                // Player is being attacked - use enemy attack splash
                _textures.TryGetValue("enemy-attack-splash", out textureToUse);
            }
            else if (isEnemyTarget && (isPlayerSource || e.DamageType == ModifyTypeEnum.Effect))
            {
                // Player attacks and enemy-facing effect damage use the enemy hit splash.
                _textures.TryGetValue("player-attack-splash", out textureToUse);
            }
            else
            {
                // Neither player target nor source, skip
                return;
            }

            if (textureToUse == null) return;

            if (_animations.Count >= MaxConcurrent)
            {
                // Drop oldest
                PublishCompletion(_animations[0]);
                _animations.RemoveAt(0);
            }

            float totalDuration = FadeInDurationSeconds + HoldDurationSeconds + FadeOutDurationSeconds;

            if (e.PresentationId != Guid.Empty)
            {
                EventManager.Publish(new BattlePresentationStarted
                {
                    PresentationId = e.PresentationId,
                    Target = target,
                    Kind = BattlePresentationKind.DamageSplash
                });
            }

            _animations.Add(new AnimationInstance
            {
                TargetEntityId = target.Id,
                Target = target,
                PresentationId = e.PresentationId,
                Texture = textureToUse,
                AgeSeconds = 0f,
                FadeInDurationSeconds = FadeInDurationSeconds,
                HoldDurationSeconds = HoldDurationSeconds,
                FadeOutDurationSeconds = FadeOutDurationSeconds,
                TotalDurationSeconds = totalDuration
            });
        }

        private static void PublishCompletion(AnimationInstance anim)
        {
            if (anim == null || anim.PresentationId == Guid.Empty) return;
            EventManager.Publish(new BattlePresentationCompleted
            {
                PresentationId = anim.PresentationId,
                Target = anim.Target,
                Kind = BattlePresentationKind.DamageSplash
            });
        }

        private void OnApplyPassive(ApplyPassiveEvent e)
        {
            // Only show for aegis gains (positive delta)
            LoggingService.Append("SplashEffectAnimationDisplaySystem.OnApplyPassive", new System.Text.Json.Nodes.JsonObject { ["passiveType"] = e.Type.ToString(), ["delta"] = e.Delta });
            if (e.Delta <= 0)
                return;

            var target = e.Target;
            if (target == null) return;
            string textureKey = e.Type switch
            {
                AppliedPassiveType.Aegis => "gain-aegis",
                AppliedPassiveType.Burn => "gain-burn",
                AppliedPassiveType.Armor => "gain-armor",
                AppliedPassiveType.Aggression => "gain-aggression",
                AppliedPassiveType.Galvanize => "gain-aggression",
                AppliedPassiveType.Sharpen => "gain-aggression",
                AppliedPassiveType.Might => "gain-power",
                AppliedPassiveType.Power => "gain-power",
                AppliedPassiveType.Bleed => "gain-bleed",
                AppliedPassiveType.Frostbite => "gain-frostbite",
                AppliedPassiveType.Slow => "gain-slow",
                AppliedPassiveType.SubZero => "gain-sub-zero",
                AppliedPassiveType.Wounded => "gain-wounded",
                AppliedPassiveType.Inferno => "gain-inferno",
                AppliedPassiveType.Enflamed => "gain-enflamed",
                _ => null
            };
            LoggingService.Append("SplashEffectAnimationDisplaySystem.OnApplyPassive.TextureResolved", new System.Text.Json.Nodes.JsonObject { ["textureKey"] = textureKey ?? "null", ["targetName"] = target.Name });
            if (textureKey == null) return;
            if (!_textures.TryGetValue(textureKey, out var textureToUse) || textureToUse == null) return;
            if (_animations.Count >= MaxConcurrent)
            {
                // Drop oldest
                _animations.RemoveAt(0);
            }

            float totalDuration = FadeInDurationSeconds + HoldDurationSeconds + FadeOutDurationSeconds;
            _animations.Add(new AnimationInstance
            {
                TargetEntityId = target.Id,
                Target = target,
                Texture = textureToUse,
                AgeSeconds = 0f,
                FadeInDurationSeconds = FadeInDurationSeconds,
                HoldDurationSeconds = HoldDurationSeconds,
                FadeOutDurationSeconds = FadeOutDurationSeconds,
                TotalDurationSeconds = totalDuration
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
            if (_animations.Count == 0) return;

            foreach (var anim in _animations)
            {
                if (anim.Texture == null) continue;

                // Compute current position (follows target)
                var currentCenter = ComputeBodyCenter(anim.Target);
                var pos = new Vector2(currentCenter.X, currentCenter.Y);

                // Apply percentage offsets relative to target visual bounds if available
                float px = 0f, py = 0f;
                var pInfo = anim.Target?.GetComponent<PortraitInfo>();
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

                // Compute alpha based on phase
                float alpha = ComputeAlpha(anim);

                // Draw texture centered at position
                var origin = new Vector2(anim.Texture.Width / 2f, anim.Texture.Height / 2f);
                var color = Color.White * alpha;

                // Scale in accordance with viewport width rather than texture size
                // ImageScale now represents fraction of VirtualWidth the image should occupy
                float finalScale = (ImageScale * Game1.VirtualWidth) / anim.Texture.Width;

                _spriteBatch.Draw(
                    anim.Texture,
                    pos,
                    null,
                    color,
                    0f,
                    origin,
                    finalScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private float ComputeAlpha(AnimationInstance anim)
        {
            float age = anim.AgeSeconds;
            
            // Phase 1: Fade In
            if (age < anim.FadeInDurationSeconds)
            {
                float t = anim.FadeInDurationSeconds > 0f ? age / anim.FadeInDurationSeconds : 1f;
                return MathHelper.Clamp(t, 0f, 1f);
            }
            
            // Phase 2: Hold at 100%
            float holdStart = anim.FadeInDurationSeconds;
            float holdEnd = holdStart + anim.HoldDurationSeconds;
            if (age < holdEnd)
            {
                return 1f;
            }
            
            // Phase 3: Fade Out
            float fadeOutStart = holdEnd;
            float fadeOutEnd = fadeOutStart + anim.FadeOutDurationSeconds;
            if (age < fadeOutEnd)
            {
                float t = anim.FadeOutDurationSeconds > 0f 
                    ? (age - fadeOutStart) / anim.FadeOutDurationSeconds 
                    : 1f;
                return MathHelper.Clamp(1f - t, 0f, 1f);
            }
            
            // Past all phases, should be removed but return 0 just in case
            return 0f;
        }

        [DebugAction("Test Aegis Animation")]
        public void Debug_TestAegisAnimation()
        {
            _animations.Add(new AnimationInstance
            {
                TargetEntityId = 0,
                Target = EntityManager.GetEntity("Player"),
                Texture = _textures["gain-aegis"],
                AgeSeconds = 0f,
                FadeInDurationSeconds = FadeInDurationSeconds,
                HoldDurationSeconds = HoldDurationSeconds,
                FadeOutDurationSeconds = FadeOutDurationSeconds,
                TotalDurationSeconds = FadeInDurationSeconds + HoldDurationSeconds + FadeOutDurationSeconds
            });
        }
        [DebugAction("Test Attack Animations")]
        public void Debug_TestPlayerAttackAnimation()
        {
            _animations.Add(new AnimationInstance
            {
                TargetEntityId = 0,
                Target = EntityManager.GetEntity("Player"),
                Texture = _textures["enemy-attack-splash"],
                AgeSeconds = 0f,
                FadeInDurationSeconds = FadeInDurationSeconds,
                HoldDurationSeconds = HoldDurationSeconds,
                FadeOutDurationSeconds = FadeOutDurationSeconds,
                TotalDurationSeconds = FadeInDurationSeconds + HoldDurationSeconds + FadeOutDurationSeconds
            });
            _animations.Add(new AnimationInstance
            {
                TargetEntityId = 0,
                Target = EntityManager.GetEntity("Enemy"),
                Texture = _textures["player-attack-splash"],
                AgeSeconds = 0f,
                FadeInDurationSeconds = FadeInDurationSeconds,
                HoldDurationSeconds = HoldDurationSeconds,
                FadeOutDurationSeconds = FadeOutDurationSeconds,
                TotalDurationSeconds = FadeInDurationSeconds + HoldDurationSeconds + FadeOutDurationSeconds
            });
        }
    }
}
