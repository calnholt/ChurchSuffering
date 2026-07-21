using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Displays the queued battle events at the top-center of the screen as a row of nodes
	/// with connecting lines. The current node is larger. Enemy nodes show their PNG.
	/// </summary>
	[DebugTab("Queued Events Display")]
	public class QueuedEventsDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private readonly Texture2D _pixel;
		private readonly Dictionary<int, Texture2D> _circleByRadius = new Dictionary<int, Texture2D>();
		private readonly Dictionary<string, Texture2D> _enemyTextureCache = new Dictionary<string, Texture2D>();
		private readonly Dictionary<string, Texture2D> _enemySmoothTextureCache = new Dictionary<string, Texture2D>();
		private const string RootEntityName = "QueuedEventsUIRoot";

		// Layout
		[DebugEditable(DisplayName = "Offset Y", Step = 1, Min = 0, Max = 400)]
		public int OffsetY { get; set; } = 56;
		[DebugEditable(DisplayName = "Node Spacing", Step = 2, Min = 16, Max = 400)]
		public int NodeSpacing { get; set; } = 208; // center-to-center
		[DebugEditable(DisplayName = "Base Radius", Step = 1, Min = 4, Max = 200)]
		public int BaseRadius { get; set; } = 20;
		[DebugEditable(DisplayName = "Current Scale", Step = 0.05f, Min = 1f, Max = 3f)]
		public float CurrentNodeScale { get; set; } = 1.7f;
		[DebugEditable(DisplayName = "Image Padding", Step = 1, Min = 0, Max = 32)]
		public int ImagePadding { get; set; } = 0;

		// Lines
		[DebugEditable(DisplayName = "Line Thickness", Step = 1, Min = 1, Max = 40)]
		public int LineThickness { get; set; } = 4;
		[DebugEditable(DisplayName = "Line Length", Step = 2, Min = 10, Max = 500)]
		public int DesiredLineLength { get; set; } = 80; // will be clamped to avoid touching
		[DebugEditable(DisplayName = "Node-Line Gap", Step = 1, Min = 0, Max = 50)]
		public int NodeLineGap { get; set; } = 40; // space between node edge and line end

		// Cross-out overlay for completed events
		[DebugEditable(DisplayName = "Cross Thickness", Step = 1, Min = 1, Max = 40)]
		public int CrossThickness { get; set; } = 5;
		[DebugEditable(DisplayName = "Cross Margin", Step = 1, Min = 0, Max = 30)]
		public int CrossMargin { get; set; } = 2;
		[DebugEditable(DisplayName = "Cross Length Extra", Step = 1, Min = -100, Max = 400)]
		public int CrossLengthExtra { get; set; } = 10;

		// Smoothing for enemy icons
		[DebugEditable(DisplayName = "Smooth Enemy Icons")]
		public bool SmoothEnemyIcons { get; set; } = true;
		[DebugEditable(DisplayName = "Smooth Kernel Size", Step = 2, Min = 1, Max = 9)]
		public int SmoothKernelSize { get; set; } = 3; // odd preferred

		// Appearance
		[DebugEditable(DisplayName = "Node Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float NodeAlpha { get; set; } = 0.75f;

		public QueuedEventsDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// Ensure a root transform entity exists for positioning this UI row
			EnsureRootEntity();
			// Re-center the root on viewport changes
			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;
			var root = EntityManager.GetEntity(RootEntityName);
			var t = root?.GetComponent<Transform>();
			if (t != null)
			{
				t.Position = new Vector2(w / 2f, OffsetY);
			}
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.Battle) return;
			var qe = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()?.GetComponent<QueuedEvents>();
			if (qe == null || qe.Events == null || qe.Events.Count == 0) return;

			EnsureRootEntity();
			var root = EntityManager.GetEntity(RootEntityName);
			var tRoot = root?.GetComponent<Transform>();
			if (tRoot == null) return;
			float rootX = tRoot.Position.X;
			int y = (int)System.Math.Round(tRoot.Position.Y);
			int count = qe.Events.Count;
			int spacing = System.Math.Max(8, NodeSpacing);
			// Compute centers
			int totalWidth = spacing * (count - 1);
			float startX = rootX - totalWidth / 2f;

			// Precompute radii per index
			int current = System.Math.Max(0, System.Math.Min(qe.CurrentIndex, count - 1));
			int[] radii = new int[count];
			for (int i = 0; i < count; i++)
			{
				float scale = (i == current) ? System.Math.Max(1f, CurrentNodeScale) : 1f;
				radii[i] = System.Math.Max(1, (int)System.Math.Round(BaseRadius * scale));
			}

			// Adjust startX so the visual bounding box (including radii) is truly centered
			if (count > 0)
			{
				float firstCenter = startX;
				float lastCenter = startX + (count - 1) * spacing;
				float leftEdge = firstCenter - radii[0];
				float rightEdge = lastCenter + radii[count - 1];
				float span = rightEdge - leftEdge;
				float desiredLeft = rootX - span / 2f;
				float delta = desiredLeft - leftEdge;
				startX += delta;
			}

			// Draw lines between nodes first
			for (int i = 0; i < count - 1; i++)
			{
				float cx0 = startX + i * spacing;
				float cx1 = startX + (i + 1) * spacing;
				float midX = (cx0 + cx1) * 0.5f;
				int r0 = radii[i];
				int r1 = radii[i + 1];
				float centerDist = System.Math.Abs(cx1 - cx0);
				float maxLen = System.Math.Max(0f, centerDist - (r0 + r1) - 2 * NodeLineGap);
				float len = System.Math.Min(DesiredLineLength, maxLen);
				if (len <= 0.5f) continue;
				var rect = new Rectangle((int)System.MathF.Round((float)(midX - len / 2f)), y - LineThickness / 2, (int)System.MathF.Round(len), LineThickness);
				_spriteBatch.Draw(_pixel, rect, Color.Black);
			}

			// Draw nodes
			for (int i = 0; i < count; i++)
			{
				float cx = startX + i * spacing;
				int r = radii[i];
				var circle = GetCircle(r);
				var pos = new Vector2(System.MathF.Round(cx - r), (float)(y - r));
				byte nodeA = (byte)System.Math.Round(MathHelper.Clamp(NodeAlpha, 0f, 1f) * 255f);
				var nodeColor = Color.FromNonPremultiplied(255, 255, 255, nodeA);
				_spriteBatch.Draw(circle, position: pos, color: nodeColor);

				// If enemy, draw its image centered within the node
				var evt = qe.Events[i];
				if (evt != null && evt.EventType == QueuedEventType.Enemy)
				{
					var tex = TryGetEnemyTexture(evt.EventId);
					if (tex != null)
					{
						var drawTex = SmoothEnemyIcons ? GetOrBuildSmoothed(evt.EventId, tex) : tex;
						int padding = System.Math.Max(0, ImagePadding);
						float maxDiam = (r * 2f) - padding * 2f;
						float scale = System.Math.Min(maxDiam / drawTex.Width, maxDiam / drawTex.Height);
						var origin = new Vector2(drawTex.Width / 2f, drawTex.Height / 2f);
						var center = new Vector2(System.MathF.Round(cx), (float)y);
						_spriteBatch.Draw(drawTex, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
					}
				}

				// Cross out past events (index less than current)
				if (i < current)
				{
					int margin = System.Math.Max(0, CrossMargin);
					float usableRadius = System.Math.Max(0f, r - margin);
					float diagLength = System.Math.Max(1f, 2f * usableRadius + 2f * CrossLengthExtra); // allow extending beyond margins
					int thickness = System.Math.Max(1, CrossThickness);
					var center = new Vector2(System.MathF.Round(cx), (float)y);
					DrawCenteredRotatedRect(center, diagLength, thickness, -0.78539816f, Color.DarkRed); // -45°
					DrawCenteredRotatedRect(center, diagLength, thickness, 0.78539816f, Color.DarkRed);  // +45°
				}
			}
		}

		private void DrawCenteredRotatedRect(Vector2 center, float length, int thickness, float radians, Color color)
		{
			float w = System.Math.Max(1f, length);
			float h = System.Math.Max(1f, thickness);
			var origin = new Vector2(0.5f, 0.5f); // center of 1x1 source pixel
			var scale = new Vector2(w, h); // scale 1x1 pixel to desired size
			_spriteBatch.Draw(_pixel, center, null, color, radians, origin, scale, SpriteEffects.None, 0f);
		}

		private Texture2D GetCircle(int radius)
		{
			if (radius < 1) radius = 1;
			if (_circleByRadius.TryGetValue(radius, out var tex)) return tex;
			tex = Rendering.PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
			_circleByRadius[radius] = tex;
			return tex;
		}

		private Texture2D TryGetEnemyTexture(string enemyId)
		{
			if (string.IsNullOrEmpty(enemyId)) return null;
			if (_enemyTextureCache.TryGetValue(enemyId, out var cached)) return cached;
			string contentName = EnemyPortraitContent.ToAssetName(enemyId);
			try
			{
				var tex = _content.Load<Texture2D>(contentName);
				_enemyTextureCache[enemyId] = tex;
				return tex;
			}
			catch
			{
				_enemyTextureCache[enemyId] = null;
				return null;
			}
		}

		private void EnsureRootEntity()
		{
			var e = EntityManager.GetEntity(RootEntityName);
			if (e == null)
			{
				e = EntityManager.CreateEntity(RootEntityName);
				EntityManager.AddComponent(e, new Transform { Position = new Vector2(Game1.VirtualWidth / 2f, OffsetY), ZOrder = 5000 });
				EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
			}
		}

		// Build a smoothed (box-blurred) copy of the source texture once, reusing it afterwards
		private Texture2D GetOrBuildSmoothed(string enemyId, Texture2D source)
		{
			if (string.IsNullOrEmpty(enemyId) || source == null) return source;
			if (_enemySmoothTextureCache.TryGetValue(enemyId, out var cached) && cached != null) return cached;
			int w = source.Width;
			int h = source.Height;
			var data = new Color[w * h];
			source.GetData(data);
			var rgba = new Vector4[w * h];
			for (int i = 0; i < data.Length; i++)
			{
				var c = data[i];
				rgba[i] = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
			}
			int k = System.Math.Max(1, SmoothKernelSize);
			// simple two-pass box blur
			var temp = new Vector4[w * h];
			int r = k / 2;
			// horizontal
			for (int y = 0; y < h; y++)
			{
				int row = y * w;
				for (int x = 0; x < w; x++)
				{
					Vector4 acc = Vector4.Zero;
					int cnt = 0;
					for (int dx = -r; dx <= r; dx++)
					{
						int xx = x + dx;
						if (xx < 0 || xx >= w) continue;
						acc += rgba[row + xx];
						cnt++;
					}
					temp[row + x] = acc / System.Math.Max(1, cnt);
				}
			}
			// vertical
			var outp = new Vector4[w * h];
			for (int x = 0; x < w; x++)
			{
				for (int y = 0; y < h; y++)
				{
					Vector4 acc = Vector4.Zero;
					int cnt = 0;
					for (int dy = -r; dy <= r; dy++)
					{
						int yy = y + dy;
						if (yy < 0 || yy >= h) continue;
						acc += temp[yy * w + x];
						cnt++;
					}
					outp[y * w + x] = acc / System.Math.Max(1, cnt);
				}
			}
			// Convert back to Color[] (premultiply alpha to reduce fringes)
			var outData = new Color[w * h];
			for (int i = 0; i < outData.Length; i++)
			{
				float a = outp[i].W;
				float rch = MathHelper.Clamp(outp[i].X, 0f, 1f);
				float gch = MathHelper.Clamp(outp[i].Y, 0f, 1f);
				float bch = MathHelper.Clamp(outp[i].Z, 0f, 1f);
				byte A = (byte)MathHelper.Clamp(a * 255f, 0f, 255f);
				byte R = (byte)MathHelper.Clamp(rch * A, 0f, 255f);
				byte G = (byte)MathHelper.Clamp(gch * A, 0f, 255f);
				byte B = (byte)MathHelper.Clamp(bch * A, 0f, 255f);
				outData[i] = new Color(R, G, B, A);
			}
			var smoothed = new Texture2D(_graphicsDevice, w, h);
			smoothed.SetData(outData);
			_enemySmoothTextureCache[enemyId] = smoothed;
			return smoothed;
		}
	}
}


