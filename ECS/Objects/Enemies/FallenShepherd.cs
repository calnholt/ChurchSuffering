using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Utils;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class FallenShepherd : EnemyBase
{
    private bool _fearSelected = false;

    private static readonly List<EnemyAttackId> Phase1SmallAttacks =
    [
        EnemyAttackId.FallenShepherdCrooksScar,
        EnemyAttackId.FallenShepherdBreakFaith,
        EnemyAttackId.FallenShepherdBloodletting,
        EnemyAttackId.FallenShepherdCowTheFlock,
    ];

    private static readonly List<EnemyAttackId> Phase2SmallAttacks =
    [
        EnemyAttackId.FallenShepherdShepherdsVigil,
        EnemyAttackId.FallenShepherdHush,
        EnemyAttackId.FallenShepherdCrooksScar,
        EnemyAttackId.FallenShepherdCowTheFlock,
    ];

    private static readonly List<EnemyAttackId> Phase3Attacks =
    [
        EnemyAttackId.FallenShepherdPurgeTheHeretic,
        EnemyAttackId.FallenShepherdFearTheShepherd,
        EnemyAttackId.FallenShepherdFinalSermon,
        EnemyAttackId.FallenShepherdPhase3,
    ];

    public FallenShepherd()
    {
        Id = EnemyId.FallenShepherd;
        Name = "Fallen Shepherd";
        HP = 29;
        IsBoss = true;
        Phases = 3;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        bool isHeavyTurn = turnNumber <= 1 || turnNumber % 2 == 1;

        return CurrentPhase switch
        {
            2 when isHeavyTurn => [EnemyAttackId.FallenShepherdPhase2],
            2 => ArrayUtils.TakeRandomWithoutReplacement(Phase2SmallAttacks, 3),
            3 => GetPhase3Attacks(),
            _ when isHeavyTurn => [EnemyAttackId.FallenShepherdPhase1],
            _ => ArrayUtils.TakeRandomWithoutReplacement(Phase1SmallAttacks, 3),
        };
    }

    private IEnumerable<EnemyAttackId> GetPhase3Attacks()
    {
        var pool = _fearSelected
            ? Phase3Attacks.Where(a => a != EnemyAttackId.FallenShepherdFearTheShepherd).ToList()
            : Phase3Attacks;
        var result = ArrayUtils.TakeRandomWithoutReplacement(pool, 1).ToList();
        if (result.FirstOrDefault() == EnemyAttackId.FallenShepherdFearTheShepherd)
            _fearSelected = true;
        return result;
    }
}

public class FallenShepherdPhase1 : EnemyAttackBase
{
    private const int BlockerRequirement = 1;

    public FallenShepherdPhase1()
    {
        Id = EnemyAttackId.FallenShepherdPhase1;
        Name = "Cast Out";
        Damage = 9;
        ConditionType = ConditionType.MustBeBlockedByAtLeast1Card;
        Text = $"{EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, BlockerRequirement)}\n\nEach card used to block this attack becomes colorless.";

        OnAttackReveal = _ =>
        {
            EventManager.Publish(new MustBeBlockedEvent
            {
                Threshold = BlockerRequirement,
                Type = MustBeBlockedSystem.MustBeBlockedByType.AtLeast,
            });
        };

        OnBlockProcessed = (entityManager, card) =>
        {
            FallenShepherdCardRestrictionHelper.Apply<Colorless>(entityManager, card);
        };
    }
}

public class FallenShepherdCrooksScar : EnemyAttackBase
{
    private const int ScarAmount = 1;

    public FallenShepherdCrooksScar()
    {
        Id = EnemyAttackId.FallenShepherdCrooksScar;
        Name = "Crook's Scar";
        Damage = 3;
        ConditionType = ConditionType.OnHit;
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Scar, ScarAmount, ConditionType);

        OnAttackHit = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Scar,
                Delta = ScarAmount,
            });
        };
    }
}

public class FallenShepherdBreakFaith : EnemyAttackBase
{
    public FallenShepherdBreakFaith()
    {
        Id = EnemyAttackId.FallenShepherdBreakFaith;
        Name = "Break Faith";
        Damage = 3;
        Text = "Each card used to block this attack becomes brittle.";

        OnBlockProcessed = (entityManager, card) =>
        {
            FallenShepherdCardRestrictionHelper.Apply<Brittle>(entityManager, card);
        };
    }
}

public class FallenShepherdBloodletting : EnemyAttackBase
{
    private const int BleedAmount = 3;

    public FallenShepherdBloodletting()
    {
        Id = EnemyAttackId.FallenShepherdBloodletting;
        Name = "Bloodletting";
        Damage = 3;
        ConditionType = ConditionType.OnHit;
        Text = $"On hit - Gain {BleedAmount} bleed.";

        OnAttackHit = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Bleed,
                Delta = BleedAmount,
            });
        };
    }
}

public class FallenShepherdCowTheFlock : EnemyAttackBase
{
    private const int IntimidateAmount = 1;

    public FallenShepherdCowTheFlock()
    {
        Id = EnemyAttackId.FallenShepherdCowTheFlock;
        Name = "Cow the Flock";
        Damage = 3;
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, IntimidateAmount);

        OnAttackReveal = _ =>
        {
            EventManager.Publish(new IntimidateEvent { Amount = IntimidateAmount });
        };
    }
}

public class FallenShepherdPhase2 : EnemyAttackBase
{
    private const int ShackledAmount = 2;

    public FallenShepherdPhase2()
    {
        Id = EnemyAttackId.FallenShepherdPhase2;
        Name = "Binding Sermon";
        Damage = 10;
        BlockRequiredToPreventEffect = Random.Shared.Next(0, 100) <= 50 ? 6 : 7;
        Text = EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, $"Gain {ShackledAmount} shackled.");

        OnDamageThresholdMet = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Shackled,
                Delta = ShackledAmount,
            });
        };
    }
}

public class FallenShepherdShepherdsVigil : EnemyAttackBase
{
    private const int GuardAmount = 3;

    public FallenShepherdShepherdsVigil()
    {
        Id = EnemyAttackId.FallenShepherdShepherdsVigil;
        Name = "Shepherd's Vigil";
        Damage = 3;
        ConditionType = ConditionType.OnHit;
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Guard, GuardAmount, ConditionType);

        OnAttackHit = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Guard,
                Delta = GuardAmount,
            });
        };
    }
}

public class FallenShepherdHush : EnemyAttackBase
{
    private const int SilencedAmount = 1;

    public FallenShepherdHush()
    {
        Id = EnemyAttackId.FallenShepherdHush;
        Name = "Hush";
        Damage = 3;
        ConditionType = ConditionType.OnHit;
        Text = $"On hit - Gain {SilencedAmount} silenced.";

        OnAttackHit = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Silenced,
                Delta = SilencedAmount,
            });
        };
    }
}

public class FallenShepherdPurgeTheHeretic : EnemyAttackBase
{
    private const int BurnAmount = 1;

    public FallenShepherdPurgeTheHeretic()
    {
        Id = EnemyAttackId.FallenShepherdPurgeTheHeretic;
        Name = "Purge the Heretic";
        Damage = 8;
        Text = $"On reveal - Gain {BurnAmount} burn.";

        OnAttackReveal = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Burn,
                Delta = BurnAmount,
            });
        };
    }
}

public class FallenShepherdFearTheShepherd : EnemyAttackBase
{
    private const int FearAmount = 1;

    public FallenShepherdFearTheShepherd()
    {
        Id = EnemyAttackId.FallenShepherdFearTheShepherd;
        Name = "Fear the Shepherd";
        Damage = 9;
        Text = $"On reveal - Gain {FearAmount} fear.";

        OnAttackReveal = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Fear,
                Delta = FearAmount,
            });
        };
    }
}

public class FallenShepherdFinalSermon : EnemyAttackBase
{
    private const int SilencedAmount = 1;

    public FallenShepherdFinalSermon()
    {
        Id = EnemyAttackId.FallenShepherdFinalSermon;
        Name = "Final Sermon";
        Damage = 9;
        Text = $"On reveal - Gain {SilencedAmount} silenced.";

        OnAttackReveal = entityManager =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Silenced,
                Delta = SilencedAmount,
            });
        };
    }
}

public class FallenShepherdPhase3 : EnemyAttackBase
{
    public FallenShepherdPhase3()
    {
        Id = EnemyAttackId.FallenShepherdPhase3;
        Name = "Have No Mercy";
        Damage = 9;
        BlockRequiredToPreventEffect = Random.Shared.Next(0, 100) <= 50 ? 3 : 4;
        Text = EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, "Discard the selected card from your hand.");

        OnAttackReveal = entityManager =>
        {
            EventManager.Publish(new MarkedForSpecificDiscardEvent
            {
                Amount = 1,
            });

            var markedCard = entityManager.GetEntitiesWithComponent<MarkedForSpecificDiscard>().FirstOrDefault();
            string cardName = markedCard?.GetComponent<CardData>()?.Card?.Name;
            if (!string.IsNullOrWhiteSpace(cardName))
            {
                Text = EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, $"Discard {cardName} from your hand.");
            }
        };

        OnDamageThresholdMet = _ =>
        {
            EventManager.Publish(new DiscardMarkedForSpecificDiscardEvent());
        };
    }
}

internal static class FallenShepherdCardRestrictionHelper
{
    public static void Apply<T>(EntityManager entityManager, Entity card)
        where T : class, IComponent, new()
    {
        if (card?.GetComponent<CardData>() == null || card.GetComponent<AssignedBlockCard>()?.IsEquipment == true)
        {
            return;
        }

        if (!card.HasComponent<T>())
        {
            entityManager.AddComponent(card, new T());
        }

        RunScopedStateService.SyncCardRestrictionsFromComponents(card);
    }
}
