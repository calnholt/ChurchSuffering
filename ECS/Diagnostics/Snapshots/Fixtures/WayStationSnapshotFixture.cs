using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.RunSetup;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class WayStationSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "waystation";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant.FileSlug();

		private WayStationSnapshotVariant _variant;
		private WayStationBackgroundDisplaySystem _background;
		private IncenseDisplaySystem _incense;
		private WayStationPoiDisplaySystem _poi;
		private WayStationDialogueSystem _dialogue;
		private WayStationClimbSettingsModalSystem _controller;
		private WayStationPenanceBackdropDisplaySystem _backdrop;
		private WayStationPenanceMotionSystem _motion;
		private WayStationPenanceMastheadDisplaySystem _masthead;
		private WayStationPenanceWeaponDisplaySystem _weapons;
		private WayStationPenanceTrackDisplaySystem _track;
		private WayStationPenanceNodeDisplaySystem _nodes;
		private WayStationPenanceTallyDisplaySystem _tally;
		private WayStationPenanceFooterDisplaySystem _footer;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = WayStationSnapshotVariantParser.Parse(args);
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.WayStation;
			SaveCache.GetAll().collection.pendingClimbPoints = 0;
			SaveCache.GetAll().waystation = new WayStationMetaSave
			{
				climbCompletions = 3,
				highestPenanceByWeapon = new Dictionary<string, int>
				{
					["sword"] = 12,
					["dagger"] = 12,
					["hammer"] = 12,
				},
			};

			var setup = WayStationRunSetupService.GetRunSetup(ctx.World.EntityManager);
			setup.SelectedWeapon = StartingWeapon.Hammer;
			setup.SelectedPenanceLevel = 12;

			_background = new WayStationBackgroundDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_incense = new IncenseDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.Content);
			_poi = new WayStationPoiDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_dialogue = new WayStationDialogueSystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_controller = new WayStationClimbSettingsModalSystem(ctx.World);
			_backdrop = new WayStationPenanceBackdropDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.Content, ctx.ImageAssets);
			_motion = new WayStationPenanceMotionSystem(ctx.World.EntityManager);
			_masthead = new WayStationPenanceMastheadDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_weapons = new WayStationPenanceWeaponDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_track = new WayStationPenanceTrackDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_nodes = new WayStationPenanceNodeDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_tally = new WayStationPenanceTallyDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);
			_footer = new WayStationPenanceFooterDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, ctx.ImageAssets);

			ctx.World.AddSystem(_background);
			ctx.World.AddSystem(_incense);
			ctx.World.AddSystem(_poi);
			ctx.World.AddSystem(_dialogue);
			ctx.World.AddSystem(_controller);
			ctx.World.AddSystem(_backdrop);
			ctx.World.AddSystem(_motion);
			ctx.World.AddSystem(_masthead);
			ctx.World.AddSystem(_weapons);
			ctx.World.AddSystem(_track);
			ctx.World.AddSystem(_nodes);
			ctx.World.AddSystem(_tally);
			ctx.World.AddSystem(_footer);

			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.WayStation, PreviousScene = SceneId.Snapshot });
			EventManager.Publish(new OpenWayStationClimbSettingsModalEvent());
			PinSettled(ctx);
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			PinSettled(ctx);
			_backdrop.DrawUnderlay(() =>
			{
				_background.Draw();
				_incense.Draw();
				_poi.Draw();
				_dialogue.Draw();
			});
			_masthead.Draw();
			_weapons.Draw();
			_track.Draw();
			_nodes.Draw();
			_tally.Draw();
			_footer.Draw();
		}

		private static void PinSettled(DisplaySnapshotContext ctx)
		{
			var state = ctx.World.EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)?.GetComponent<WayStationPenanceModalState>();
			if (state == null) throw new DisplaySnapshotSetupException("Waystation Penance modal did not open.");
			state.RequestedVisible = true;
			state.Phase = WayStationPenanceModalPhase.Visible;
			state.ElapsedSeconds = 0f;
			state.InteractionEnabled = true;
			foreach (var entity in ctx.World.EntityManager.GetEntitiesWithComponent<WayStationPenanceMotion>())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
				if (tally != null) tally.DisplayedCount = tally.CurrentCount;
				motion.Opacity = tally?.IsActive == false ? 0f : 1f;
				motion.Offset = Microsoft.Xna.Framework.Vector2.Zero;
				motion.Scale = 1f;
				motion.WidthProgress = tally?.IsActive == false ? 0f : 1f;
				motion.Glow = 0f;
				motion.TransitionKind = WayStationPenanceTransitionKind.None;
			}
		}
	}
}
