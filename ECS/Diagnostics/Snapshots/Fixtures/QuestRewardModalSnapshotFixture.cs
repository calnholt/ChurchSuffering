using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
    public sealed class QuestRewardModalSnapshotFixture : IDisplaySnapshotFixture
    {
        public string Id => "quest-reward-modal";
        public int WarmupFrames => 2;

        private RewardModalDisplaySystem _modal;
        private QuestRewardSnapshotVariant _variant;
        private Texture2D _pixel;

        public string OutputFileName => _variant?.FileSlug ?? "quest-reward-modal";

        private static readonly Color BackdropColor = new(40, 44, 48);

        public void Setup(DisplaySnapshotContext ctx, string[] args)
        {
            _variant = QuestRewardSnapshotVariant.Parse(args);

            if (_variant.HasCardReward)
            {
                foreach (var cardKey in EnumerateOfferCardKeys(_variant.DeckRewardOffer))
                {
                    if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out var color, out var isUpgraded))
                    {
                        throw new DisplaySnapshotSetupException($"Invalid reward card key: '{cardKey}'");
                    }
                    var probe = EntityFactory.CreateCardFromDefinition(
                        ctx.World.EntityManager,
                        cardId,
                        color,
                        isUpgraded: isUpgraded);
                    if (probe == null)
                    {
                        throw new DisplaySnapshotSetupException(
                            $"Failed to create reward card: '{cardKey}'");
                    }
                    ctx.World.EntityManager.DestroyEntity(probe.Id);
                }
            }

            _modal = new RewardModalDisplaySystem(
                ctx.World.EntityManager,
                ctx.GraphicsDevice,
                ctx.SpriteBatch,
                ctx.ImageAssets);
            ctx.World.AddSystem(_modal);

            _modal.Open(
                message: null,
                rewardGold: _variant.RewardGold,
                hasCardReward: _variant.HasCardReward,
                rewardCardKey: _variant.RewardCardKey,
                rewardCardKeys: _variant.RewardCardKeys,
                deckRewardOffer: _variant.DeckRewardOffer);

            _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Draw(DisplaySnapshotContext ctx)
        {
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;
            ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), BackdropColor);
            _modal.Draw();
        }

        private static System.Collections.Generic.IEnumerable<string> EnumerateOfferCardKeys(DeckRewardOfferSave offer)
        {
            if (offer?.options == null) yield break;
            foreach (var option in offer.options)
            {
                if (option == null) continue;
                if (!string.IsNullOrWhiteSpace(option.outgoingCardKey)) yield return option.outgoingCardKey;
                if (!string.IsNullOrWhiteSpace(option.incomingCardKey)) yield return option.incomingCardKey;
                if (!string.IsNullOrWhiteSpace(option.upgradedCardKey)) yield return option.upgradedCardKey;
            }
        }
    }
}
