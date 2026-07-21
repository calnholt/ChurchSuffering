using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
    public sealed class GuardianAngelSnapshotFixture : IDisplaySnapshotFixture
    {
        public string Id => "guardian-angel";
        public int WarmupFrames => 0;
        public string OutputFileName => _variant;

        private string _variant = "idle";
        private Texture2D _background;
        private Texture2D _pixel;
        private GuardianAngelDisplaySystem _display;

        public void Setup(DisplaySnapshotContext ctx, string[] args)
        {
            _variant = ParseVariant(args ?? Array.Empty<string>());
            _background = ctx.Content.Load<Texture2D>("Battle_Backgrounds/gothic-battle-background");
            _pixel = ctx.ImageAssets.GetPixel(Color.White);

            var player = ctx.World.CreateEntity("Player");
            ctx.World.AddComponent(player, new Player());
            ctx.World.AddComponent(player, new Transform { Position = new Vector2(760f, 690f) });

            _display = new GuardianAngelDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets)
            {
                DebugMotionOffsetOverride = new Vector2(8f, -5f),
                SparkleSpawnRate = 0,
            };
            Advance(0f);

            (string text, GuardianFlightGesture gesture, float sampleTime) = _variant switch
            {
                "message" => ("Stay close. We have this!", GuardianFlightGesture.Flourish, 0.25f),
                "card-hop" => ("A good honest strike!", GuardianFlightGesture.CardHop, 0.325f),
                "medal-loop" => ("Saint Michael, guard our flank!", GuardianFlightGesture.MedalLoop, 0.575f),
                "enemy-recoil" => ("Big foot coming down!", GuardianFlightGesture.EnemyBrace, 0.4f),
                _ => (string.Empty, GuardianFlightGesture.None, 0f),
            };
            if (!string.IsNullOrEmpty(text))
            {
                _display.DebugShowSpeech(text, gesture);
                Advance(sampleTime);
            }

            var guardian = ctx.World.EntityManager.GetEntity("GuardianAngel");
            if (guardian != null)
            {
                GuardianFlightSample gestureSample = GuardianAngelFlightService.SampleGesture(gesture, sampleTime, GestureDuration(gesture));
                guardian.GetComponent<Transform>().Position = new Vector2(760f + 215f + 8f, 690f - 135f - 5f) + gestureSample.Offset;
                guardian.GetComponent<Transform>().Rotation = 0f;
                ctx.World.EntityManager.RemoveComponent<PositionTween>(guardian);
                ctx.World.EntityManager.RemoveComponent<ParallaxLayer>(guardian);
            }
        }

        public void Draw(DisplaySnapshotContext ctx)
        {
            ctx.SpriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.White);
            ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(10, 6, 12, 90));
            _display.Draw();
        }

        private void Advance(float seconds)
        {
            const float step = 1f / 60f;
            if (seconds <= 0f)
            {
                _display.Update(new GameTime(TimeSpan.Zero, TimeSpan.Zero));
                return;
            }
            float elapsed = 0f;
            while (elapsed + step <= seconds + 0.0001f)
            {
                elapsed += step;
                _display.Update(new GameTime(TimeSpan.FromSeconds(elapsed), TimeSpan.FromSeconds(step)));
            }
        }

        private static string ParseVariant(string[] args)
        {
            if (args.Length == 0) return "idle";
            if (args.Length == 1 && args[0] is "idle" or "message" or "card-hop" or "medal-loop" or "enemy-recoil")
                return args[0];
            throw new DisplaySnapshotSetupException(
                "guardian-angel expects one variant: idle, message, card-hop, medal-loop, or enemy-recoil");
        }

        private static float GestureDuration(GuardianFlightGesture gesture) => gesture switch
        {
            GuardianFlightGesture.EnemyBrace => 0.8f,
            GuardianFlightGesture.MedalLoop => 1.15f,
            GuardianFlightGesture.CardHop => 0.65f,
            GuardianFlightGesture.Flourish => 0.85f,
            _ => 1f,
        };
    }
}
