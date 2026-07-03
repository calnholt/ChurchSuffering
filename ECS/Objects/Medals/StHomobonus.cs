using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StHomobonus : MedalBase
    {
        private static readonly ClimbResourceSave BonusResources = new ClimbResourceSave { red = 1, white = 1, black = 1 };

        public StHomobonus()
        {
            Id = "st_homobonus";
            Name = "St. Homobonus";
            MaxCount = 3;
            Text = $"After {MaxCount} encounters, gain 1 red, 1 white, and 1 black resource.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ShowQuestRewardOverlay>(OnEncounterComplete, priority: 1);
        }

        private void OnEncounterComplete(ShowQuestRewardOverlay evt)
        {
            if (evt?.IsEncounterReward != true) return;

            CurrentCount++;
            if (CurrentCount < MaxCount) return;

            CurrentCount = 0;
            ApplyClimbResourceBonus(evt);
            EmitActivateEvent();
        }

        private static void ApplyClimbResourceBonus(ShowQuestRewardOverlay evt)
        {
            var climb = SaveCache.GetClimbState();
            if (climb == null) return;

            climb.resources ??= new ClimbResourceSave { red = 0, white = 0, black = 0 };
            ClimbRuleService.AddResources(climb.resources, BonusResources);

            if (climb.pendingEncounterReward != null)
            {
                climb.pendingEncounterReward.resources ??= new ClimbResourceSave { red = 0, white = 0, black = 0 };
                ClimbRuleService.AddResources(climb.pendingEncounterReward.resources, BonusResources);
            }

            SaveCache.SaveClimbState(climb);

            if (evt.ClimbResources == null)
            {
                evt.ClimbResources = new ClimbResourceSave { red = 0, white = 0, black = 0 };
            }
            ClimbRuleService.AddResources(evt.ClimbResources, BonusResources);
        }

        public override void Activate()
        {
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ShowQuestRewardOverlay>(OnEncounterComplete);
        }
    }
}
