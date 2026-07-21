using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Enemy Intent Pips")] 
	public class EnemyIntentPipsSystem : Core.System
	{
		public const string TargetEntityName = "EnemyIntentPips";

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private Texture2D _pixel;
        private readonly System.Collections.Generic.Dictionary<(int radius, int thickness), Texture2D> _ringCache = new();

		[DebugEditable(DisplayName = "Pip Radius", Step = 1, Min = 2, Max = 64)]
		public int PipRadius { get; set; } = 9;
		[DebugEditable(DisplayName = "Pip Gap", Step = 1, Min = 0, Max = 64)]
		public int PipGap { get; set; } = 10;
		[DebugEditable(DisplayName = "Offset Y", Step = 2, Min = -400, Max = 400)]
		public int OffsetY { get; set; } = -292;
		[DebugEditable(DisplayName = "Row Gap", Step = 1, Min = 0, Max = 64)]
		public int RowGap { get; set; } = 16;
		[DebugEditable(DisplayName = "Pip Thickness", Step = 1, Min = 1, Max = 64)]
		public int PipThickness { get; set; } = 4;

		public EnemyIntentPipsSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
            EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Enemy>().Where(e => e.HasComponent<AttackIntent>());
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var transform = entity.GetComponent<Transform>();
			var info = entity.GetComponent<PortraitInfo>();
			var intent = entity.GetComponent<AttackIntent>();
			var next = entity.GetComponent<NextTurnAttackIntent>();
			var target = EnsureTargetEntity();
			var targetUi = target.GetComponent<UIElement>();
			var targetTransform = target.GetComponent<Transform>();
			if (transform == null || info == null || intent == null || targetUi == null || targetTransform == null)
			{
				if (targetUi != null)
				{
					targetUi.Bounds = Rectangle.Empty;
					targetUi.IsHidden = true;
				}
				return;
			}

			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager))
			{
				targetUi.Bounds = Rectangle.Empty;
				targetUi.IsHidden = true;
				return;
			}

			Rectangle bounds = CalculatePipBounds(
				transform.Position,
				intent.Planned.Count,
				next?.Planned.Count ?? 0,
				PipRadius,
				PipGap,
				OffsetY,
				RowGap);
			targetTransform.Position = Vector2.Zero;
			targetUi.Bounds = bounds;
			targetUi.IsHidden = bounds.IsEmpty;
		}

		public void Draw()
		{
			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager)) return;

			// TODO: cache so we dont need to keep fetching
			var enemyEntity = EntityManager.GetEntity("Enemy");
			if (enemyEntity == null) return;
			enemyEntity.GetComponent<AppliedPassives>().Passives.TryGetValue(AppliedPassiveType.Stealth, out var stealthStacks);
			if (stealthStacks > 0) 
			{
				return;
			}
			foreach (var e in GetRelevantEntities())
			{
				var t = e.GetComponent<Transform>();
				var info = e.GetComponent<PortraitInfo>();
				var intent = e.GetComponent<AttackIntent>();
				if (t == null || info == null || intent == null) continue;
				int count = intent.Planned.Count;

				// Determine if we are currently in the enemy's turn; if so, do not cross out the current (soonest) attack
				bool isEnemyTurn = false;
				{
					var psEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
					var ps = psEntity?.GetComponent<PhaseState>();
					isEnemyTurn = ps != null && ps.Main == MainPhase.EnemyTurn && (ps.Sub == SubPhase.Block || ps.Sub == SubPhase.EnemyAttack || ps.Sub == SubPhase.EnemyEnd);
				}

				// Determine how many upcoming attacks should be crossed out based on stun stacks on the enemy
				int stunStacks = 0;
				var appliedPassives = e.GetComponent<AppliedPassives>();
				if (appliedPassives?.Passives != null && appliedPassives.Passives.TryGetValue(AppliedPassiveType.Stun, out var s))
				{
					stunStacks = System.Math.Max(0, s);
				}

				// Compute row center above enemy
				var center = new Vector2(t.Position.X, t.Position.Y + OffsetY);

				// Current turn pips (if any)
				if (count > 0)
				{
					int diameter = PipRadius * 2;
					int totalWidth = count * diameter + (count - 1) * PipGap;
					int startX = (int)System.Math.Round(center.X - totalWidth / 2f);

					// Determine the next-to-resolve step (min ResolveStep)
					int minStep = intent.Planned.Min(p => p.ResolveStep);

					// Compute which current pips are crossed out, consuming from stun stacks starting with earliest ResolveStep
					bool[] crossedCurrent = new bool[count];
					int remainingToCross = stunStacks;
					if (remainingToCross > 0)
					{
						var order = intent.Planned
							.Select((pa, idx) => new { idx, step = pa.ResolveStep })
							.OrderBy(x => x.step)
							.ToList();
						for (int oi = 0; oi < order.Count && remainingToCross > 0; oi++)
						{
							crossedCurrent[order[oi].idx] = true;
							remainingToCross--;
						}
						// During the enemy turn, shift crossed pips one index to the right for display
						if (isEnemyTurn)
						{
							var shifted = new bool[count];
							for (int ci = 0; ci < count; ci++)
							{
								if (!crossedCurrent[ci]) continue;
								int si = ci + 1;
								if (si < count) shifted[si] = true;
							}
							crossedCurrent = shifted;
						}
					}

					for (int i = 0; i < count; i++)
					{
						var pa = intent.Planned[i];
						int x = startX + i * (diameter + PipGap) + PipRadius;
						int y = (int)center.Y;
						bool isNext = pa.ResolveStep == minStep;
						bool isCrossed = crossedCurrent[i];
						var pipColor = isCrossed ? Color.DarkGray : (isNext ? Color.Yellow : Color.LightGray);
						DrawCircle(new Vector2(x, y), PipRadius, pipColor, PipThickness);
						if (isCrossed)
						{
							// Draw red X over the pip
							var a = new Vector2(x - PipRadius + 2, y - PipRadius + 2);
							var b = new Vector2(x + PipRadius - 2, y + PipRadius - 2);
							var c = new Vector2(x - PipRadius + 2, y + PipRadius - 2);
							var d = new Vector2(x + PipRadius - 2, y - PipRadius + 2);
							DrawLine(a, b, Color.Red, 3);
							DrawLine(c, d, Color.Red, 3);
						}
					}
				}

				// Next turn preview row (smaller/faded) if present
				var next = e.GetComponent<NextTurnAttackIntent>();
				if (next != null && next.Planned.Count > 0)
				{
					int nCount = next.Planned.Count;
					int nDiameter = System.Math.Max(2, (int)System.Math.Round(PipRadius * 0.75f)) * 2;
					int nRadius = nDiameter / 2;
					int nTotalWidth = nCount * nDiameter + (nCount - 1) * PipGap;
					int nStartX = (int)System.Math.Round(center.X - nTotalWidth / 2f);
					int y2 = (int)center.Y + RowGap + nRadius + PipRadius;
				
					// Compute which next-turn pips are crossed out if there are remaining stun stacks after current row
					bool[] crossedNext = new bool[nCount];
					int remainingAfterCurrent = 0;
					{
						// Recompute remaining by simulating how many current would be crossed, same as above
						int consumedByCurrent = 0;
						if (stunStacks > 0 && intent.Planned.Count > 0)
						{
							var order = intent.Planned
								.Select((pa, idx) => new { idx, step = pa.ResolveStep })
								.OrderBy(x => x.step)
								.ToList();
							int skip = (isEnemyTurn && order.Count > 0) ? 1 : 0; // cannot consume the current attack during enemy turn
							int consumable = System.Math.Max(0, order.Count - skip);
							consumedByCurrent = System.Math.Min(stunStacks, consumable);
						}
						remainingAfterCurrent = System.Math.Max(0, stunStacks - consumedByCurrent);
					}
					if (remainingAfterCurrent > 0)
					{
						var orderNext = next.Planned
							.Select((pa, idx) => new { idx, step = pa.ResolveStep })
							.OrderBy(x => x.step)
							.ToList();
						int toCross = System.Math.Min(remainingAfterCurrent, orderNext.Count);
						for (int k = 0; k < toCross; k++) crossedNext[orderNext[k].idx] = true;
					}
					for (int i = 0; i < nCount; i++)
					{
						int x2 = nStartX + i * (nDiameter + PipGap) + nRadius;
						bool isCrossed = crossedNext[i];
						DrawCircle(new Vector2(x2, y2), nRadius, new Color(200, 200, 200, 180), PipThickness);
						if (isCrossed)
						{
							// Draw red X over the next-turn pip
							var a = new Vector2(x2 - nRadius + 2, y2 - nRadius + 2);
							var b = new Vector2(x2 + nRadius - 2, y2 + nRadius - 2);
							var c = new Vector2(x2 - nRadius + 2, y2 + nRadius - 2);
							var d = new Vector2(x2 + nRadius - 2, y2 - nRadius + 2);
							DrawLine(a, b, Color.Red, 2);
							DrawLine(c, d, Color.Red, 2);
						}
					}
				}
			}
		}

		private Texture2D GetRingTexture(int radius, int thickness)
		{
			if (radius < 1) radius = 1;
			if (thickness < 1) thickness = 1;
			var key = (radius, thickness);
			if (_ringCache.TryGetValue(key, out var existing) && existing != null && !existing.IsDisposed) return existing;

			int d = radius * 2;
			var tex = new Texture2D(_graphicsDevice, d, d);
			var data = new Color[d * d];

			// Anti-aliased ring via difference of two AA discs (outer - inner)
			float outerRadius = radius - 0.5f;
			float innerRadius = System.Math.Max(0f, radius - thickness) + 0.5f;
			float smooth = 1.0f; // pixel-wide smoothing band

			for (int y = 0; y < d; y++)
			{
				float dy = y - radius + 0.5f;
				for (int x = 0; x < d; x++)
				{
					float dx = x - radius + 0.5f;
					float dist = System.MathF.Sqrt(dx * dx + dy * dy);

					float outerAlpha;
					if (dist <= outerRadius - smooth) outerAlpha = 1f;
					else if (dist >= outerRadius + smooth) outerAlpha = 0f;
					else outerAlpha = 0.5f + 0.5f * (outerRadius - dist) / smooth;

					float innerAlpha;
					if (dist <= innerRadius - smooth) innerAlpha = 1f;
					else if (dist >= innerRadius + smooth) innerAlpha = 0f;
					else innerAlpha = 0.5f + 0.5f * (innerRadius - dist) / smooth;

					float ringAlpha = outerAlpha - innerAlpha;
					if (ringAlpha < 0f) ringAlpha = 0f;
					if (ringAlpha > 1f) ringAlpha = 1f;

					byte A = (byte)MathHelper.Clamp((int)System.Math.Round(ringAlpha * 255f), 0, 255);
					data[y * d + x] = Color.FromNonPremultiplied(255, 255, 255, A);
				}
			}

			tex.SetData(data);
			_ringCache[key] = tex;
			return tex;
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			LoggingService.Append("EnemyIntentPipsSystem.OnDeleteCaches", new System.Text.Json.Nodes.JsonObject
			{
				["cacheCount"] = _ringCache.Count
			});
			foreach (var kv in _ringCache)
			{
				try { kv.Value?.Dispose(); } catch { }
			}
			_ringCache.Clear();
		}

		private void DrawCircle(Vector2 center, int radius, Color color, int thickness)
		{
			var tex = GetRingTexture(radius, thickness);
			_spriteBatch.Draw(tex, center, sourceRectangle: null, color: color, rotation: 0f, origin: new Vector2(radius, radius), scale: 1f, effects: SpriteEffects.None, layerDepth: 0f);
		}

		private void DrawLine(Vector2 a, Vector2 b, Color color, int thickness)
		{
			float dx = b.X - a.X; float dy = b.Y - a.Y;
			float len = System.MathF.Max(1f, System.MathF.Sqrt(dx * dx + dy * dy));
			float ang = System.MathF.Atan2(dy, dx);
			_spriteBatch.Draw(_pixel, position: a, sourceRectangle: null, color: color, rotation: ang, origin: Vector2.Zero, scale: new Vector2(len, thickness), effects: SpriteEffects.None, layerDepth: 0f);
		}

		internal static Rectangle CalculatePipBounds(
			Vector2 enemyPosition,
			int currentCount,
			int nextCount,
			int pipRadius,
			int pipGap,
			int offsetY,
			int rowGap)
		{
			int radius = System.Math.Max(1, pipRadius);
			int gap = System.Math.Max(0, pipGap);
			int centerY = (int)System.Math.Round(enemyPosition.Y + offsetY);
			var rows = new System.Collections.Generic.List<Rectangle>();

			if (currentCount > 0)
			{
				int diameter = radius * 2;
				int width = currentCount * diameter + (currentCount - 1) * gap;
				rows.Add(new Rectangle(
					(int)System.Math.Round(enemyPosition.X - width / 2f),
					centerY - radius,
					width,
					diameter));
			}

			if (nextCount > 0)
			{
				int nextRadius = System.Math.Max(1, (int)System.Math.Round(radius * 0.75f));
				int diameter = nextRadius * 2;
				int width = nextCount * diameter + (nextCount - 1) * gap;
				int nextCenterY = centerY + System.Math.Max(0, rowGap) + nextRadius + radius;
				rows.Add(new Rectangle(
					(int)System.Math.Round(enemyPosition.X - width / 2f),
					nextCenterY - nextRadius,
					width,
					diameter));
			}

			if (rows.Count == 0) return Rectangle.Empty;
			int left = rows.Min(row => row.Left);
			int top = rows.Min(row => row.Top);
			int right = rows.Max(row => row.Right);
			int bottom = rows.Max(row => row.Bottom);
			return new Rectangle(left, top, right - left, bottom - top);
		}

		private Entity EnsureTargetEntity()
		{
			var entity = EntityManager.GetEntity(TargetEntityName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(TargetEntityName);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement
				{
					IsInteractable = false,
					IsHidden = true,
					ShowHoverHighlight = false,
				});
			}
			return entity;
		}
	}
}
