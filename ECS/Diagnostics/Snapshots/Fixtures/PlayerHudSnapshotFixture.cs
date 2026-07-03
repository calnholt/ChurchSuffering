using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class PlayerHudSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "player-hud";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant?.FileSlug ?? "default";

		private static readonly Color BackdropColor = new(64, 38, 26);
		private PlayerHudSnapshotVariant _variant;
		private Texture2D _pixel;
		private Texture2D _portrait;
		private Texture2D _enemyPortrait;
		private Entity _player;
		private Entity _enemy;
		private float _portraitScale;
		private float _enemyPortraitScale;

		private PlayerHudRootDisplaySystem _rootDisplay;
		private PlayerHudHealthDisplaySystem _healthDisplay;
		private PlayerHudCourageDisplaySystem _courageDisplay;
		private PlayerHudTemperanceDisplaySystem _temperanceDisplay;
		private PlayerHudActionPointDisplaySystem _actionPointDisplay;
		private PlayerHudPledgeDisplaySystem _pledgeDisplay;
		private AppliedPassivesDisplaySystem _passivesDisplay;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = PlayerHudSnapshotVariant.Parse(args);
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			_portrait = ctx.ImageAssets.GetRequiredTexture("crusader_sword");
			_portraitScale = 0.36f;

			EntityFactory.CreateGameState(ctx.World);
			var phaseEntity = ctx.World.EntityManager.GetEntity("PhaseState");
			var phase = phaseEntity.GetComponent<PhaseState>();
			phase.Main = MainPhase.PlayerTurn;
			phase.Sub = SubPhase.Action;

			_player = EntityFactory.CreatePlayer(ctx.World);
			ctx.World.EntityManager.RemoveComponent<ParallaxLayer>(_player);
			var playerTransform = _player.GetComponent<Transform>();
			playerTransform.Position = new Vector2(Game1.VirtualWidth / 2f, 260f);
			playerTransform.Scale = Vector2.One;
			var portraitInfo = _player.GetComponent<PortraitInfo>();
			portraitInfo.TextureWidth = _portrait.Width;
			portraitInfo.TextureHeight = _portrait.Height;
			portraitInfo.BaseScale = _portraitScale;
			portraitInfo.CurrentScale = _portraitScale;
			if (!_player.HasComponent<ActorPresentationState>())
			{
				ctx.World.AddComponent(_player, new ActorPresentationState());
			}

			var deckEntity = ctx.World.CreateEntity("Deck");
			var deck = new Deck();
			ctx.World.AddComponent(deckEntity, deck);
			_player.GetComponent<Player>().DeckEntity = deckEntity;
			var eligibleCard = EntityFactory.CreateCardFromDefinition(
				ctx.World.EntityManager,
				"strike",
				CardData.CardColor.White);
			if (eligibleCard == null)
			{
				throw new DisplaySnapshotSetupException(
					"Failed to create the player HUD pledge card");
			}
			deck.Hand.Add(eligibleCard);

			ConfigureVariant(ctx, phaseEntity);
			CreateSystems(ctx);
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				BackdropColor);
			ctx.SpriteBatch.Draw(
				_portrait,
				_player.GetComponent<Transform>().Position,
				null,
				Color.White,
				0f,
				new Vector2(_portrait.Width / 2f, _portrait.Height / 2f),
				_portraitScale,
				SpriteEffects.None,
				0f);
			if (_enemy != null && _enemyPortrait != null)
			{
				ctx.SpriteBatch.Draw(
					_enemyPortrait,
					_enemy.GetComponent<Transform>().Position,
					null,
					Color.White,
					0f,
					new Vector2(_enemyPortrait.Width / 2f, _enemyPortrait.Height / 2f),
					_enemyPortraitScale,
					SpriteEffects.None,
					0f);
			}

			_rootDisplay.Draw();
			_healthDisplay.Draw();
			_courageDisplay.Draw();
			_temperanceDisplay.Draw();
			_actionPointDisplay.Draw();
			_pledgeDisplay.Draw();
			_passivesDisplay.Draw();
		}

		private void ConfigureVariant(DisplaySnapshotContext ctx, Entity phaseEntity)
		{
			var hp = _player.GetComponent<HP>();
			var courage = _player.GetComponent<Courage>();
			var temperance = _player.GetComponent<Temperance>();
			var actionPoints = _player.GetComponent<ActionPoints>();
			var equippedTemperance = _player.GetComponent<EquippedTemperanceAbility>();
			var passives = _player.GetComponent<AppliedPassives>();

			hp.Max = 20;
			hp.Current = 18;
			courage.Amount = 12;
			temperance.Amount = 2;
			actionPoints.Current = 1;
			equippedTemperance.AbilityId = "angelic_aura";
			passives.Passives.Clear();
			passives.Passives[AppliedPassiveType.Aegis] = 2;
			passives.Passives[AppliedPassiveType.Armor] = 1;

			switch (_variant.Id)
			{
				case PlayerHudSnapshotVariantId.Unavailable:
					ctx.World.AddComponent(
						phaseEntity,
						new PledgeAvailabilityState { PledgedThisActionPhase = true });
					break;
				case PlayerHudSnapshotVariantId.IncomingDamage:
					CreateIncomingAttack(ctx, 1, 4);
					CreateIncomingAttack(ctx, 2, 7);
					break;
				case PlayerHudSnapshotVariantId.LowHealth:
					hp.Current = 3;
					break;
				case PlayerHudSnapshotVariantId.Expanded:
					hp.Max = 150;
					hp.Current = 120;
					courage.Amount = 123;
					temperance.Amount = 8;
					actionPoints.Current = 12;
					equippedTemperance.AbilityId = "radiance";
					passives.Passives[AppliedPassiveType.Power] = 10;
					passives.Passives[AppliedPassiveType.Thorns] = 25;
					break;
				case PlayerHudSnapshotVariantId.EnemyHealth:
					// Keep the cursor-parallax player HUD outside the capture so this enemy baseline is deterministic.
					_player.GetComponent<Transform>().Position = new Vector2(-1000f, 260f);
					CreateEnemyHealthSnapshot(ctx);
					break;
			}
		}

		private void CreateEnemyHealthSnapshot(DisplaySnapshotContext ctx)
		{
			_enemyPortrait = ctx.ImageAssets.GetRequiredTexture("Skeleton");
			_enemyPortraitScale = Game1.VirtualHeight * 0.36f / _enemyPortrait.Height;
			_enemy = ctx.World.CreateEntity("EnemyHealthSnapshot");
			ctx.World.AddComponent(_enemy, new Enemy());
			ctx.World.AddComponent(_enemy, new Transform { Position = new Vector2(960f, 260f) });
			ctx.World.AddComponent(_enemy, new HP { Current = 32, Max = 50 });
			ctx.World.AddComponent(_enemy, new PortraitInfo
			{
				TextureWidth = _enemyPortrait.Width,
				TextureHeight = _enemyPortrait.Height,
				BaseScale = _enemyPortraitScale,
				CurrentScale = _enemyPortraitScale,
			});
		}

		private static void CreateIncomingAttack(
			DisplaySnapshotContext ctx,
			int attackSequence,
			int actualDamage)
		{
			var enemy = ctx.World.CreateEntity($"Enemy_{attackSequence}");
			var intent = new AttackIntent { ActiveAttackSequence = attackSequence };
			intent.Planned.Add(new PlannedAttack());
			ctx.World.AddComponent(enemy, intent);
			var progressEntity = ctx.World.CreateEntity($"AttackProgress_{attackSequence}");
			ctx.World.AddComponent(progressEntity, new EnemyAttackProgress
			{
				AttackSequence = attackSequence,
				Enemy = enemy,
				ActualDamage = actualDamage,
			});
		}

		private void CreateSystems(DisplaySnapshotContext ctx)
		{
			var layout = new PlayerHudLayoutSystem(ctx.World.EntityManager);
			var feedback = new PlayerHudFeedbackSystem(ctx.World.EntityManager);
			_rootDisplay = new PlayerHudRootDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);
			_healthDisplay = new PlayerHudHealthDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch)
			{
				LowHealthAlphaMin = 0.55f,
				LowHealthAlphaMax = 0.55f,
				IncomingDamageAlphaMin = 0.35f,
				IncomingDamageAlphaMax = 0.35f,
			};
			_courageDisplay = new PlayerHudCourageDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);
			_temperanceDisplay = new PlayerHudTemperanceDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);
			_actionPointDisplay = new PlayerHudActionPointDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);
			_pledgeDisplay = new PlayerHudPledgeDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			_passivesDisplay = new AppliedPassivesDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);

			ctx.World.AddSystem(layout);
			ctx.World.AddSystem(feedback);
			ctx.World.AddSystem(_rootDisplay);
			ctx.World.AddSystem(_healthDisplay);
			ctx.World.AddSystem(_courageDisplay);
			ctx.World.AddSystem(_temperanceDisplay);
			ctx.World.AddSystem(_actionPointDisplay);
			ctx.World.AddSystem(_pledgeDisplay);
			ctx.World.AddSystem(_passivesDisplay);
		}
	}
}
