using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Factories
{
    public static class CardFactory
    {
        private static readonly IReadOnlyDictionary<CardId, Func<CardBase>> CardConstructors =
            new Dictionary<CardId, Func<CardBase>>
            {
                { CardId.Absolution, () => new Absolution() },
                { CardId.ArkOfTheCovenant, () => new ArkOfTheCovenant() },
                { CardId.BatteringBlow, () => new BatteringBlow() },
                { CardId.BattleScars, () => new BattleScars() },
                { CardId.Burn, () => new Burn() },
                { CardId.CarpeDiem, () => new CarpeDiem() },
                { CardId.Colorless3Block, () => new ColorlessBlock() },
                { CardId.Consecrate, () => new Consecrate() },
                { CardId.Courageous, () => new Courageous() },
                { CardId.CrimsonRite, () => new CrimsonRite() },
                { CardId.Crusade, () => new Crusade() },
                { CardId.Curse, () => new Curse() },
                { CardId.Dagger, () => new Dagger() },
                { CardId.DeusVult, () => new DeusVult() },
                { CardId.DivineProtection, () => new DivineProtection() },
                { CardId.DowseWithHolyWater, () => new DowseWithHolyWater() },
                { CardId.EmberHarvest, () => new EmberHarvest() },
                { CardId.Exaltation, () => new Exaltation() },
                { CardId.Excavate, () => new Excavate() },
                { CardId.Fervor, () => new Fervor() },
                { CardId.ForgeStrike, () => new ForgeStrike() },
                { CardId.Fury, () => new Fury() },
                { CardId.HoldTheLine, () => new HoldTheLine() },
                { CardId.Hammer, () => new Hammer() },
                { CardId.HiddenKunai, () => new HiddenKunai() },
                { CardId.Impale, () => new Impale() },
                { CardId.IncreaseFaith, () => new IncreaseFaith() },
                { CardId.IronCovenant, () => new IronCovenant() },
                { CardId.Kunai, () => new Kunai() },
                { CardId.LitanyOfWrath, () => new LitanyOfWrath() },
                { CardId.Mantlet, () => new Mantlet() },
                { CardId.MaleficRite, () => new MaleficRite() },
                { CardId.QuickWit, () => new QuickWit() },
                { CardId.RallyTheFaithful, () => new RallyTheFaithful() },
                { CardId.RelentlessStrike, () => new RelentlessStrike() },
                { CardId.PouchOfKunai, () => new PouchOfKunai() },
                { CardId.Ravage, () => new Ravage() },
                { CardId.RazorStorm, () => new RazorStorm() },
                { CardId.Reckoning, () => new Reckoning() },
                { CardId.Reap, () => new Reap() },
                { CardId.RenounceAndHone, () => new RenounceAndHone() },
                { CardId.Sacrifice, () => new Sacrifice() },
                { CardId.SerpentCrush, () => new SerpentCrush() },
                { CardId.Seize, () => new Seize() },
                { CardId.ShieldOfFaith, () => new ShieldOfFaith() },
                { CardId.Smite, () => new Smite() },
                { CardId.Stab, () => new Stab() },
                { CardId.SteadfastResolve, () => new SteadfastResolve() },
                { CardId.Stalwart, () => new Stalwart() },
                { CardId.SteelTheSpirit, () => new SteelTheSpirit() },
                { CardId.StokedAssault, () => new StokedAssault() },
                { CardId.Strike, () => new Strike() },
                { CardId.SuddenThrust, () => new SuddenThrust() },
                { CardId.StokeTheFurnace, () => new StokeTheFurnace() },
                { CardId.Sword, () => new Sword() },
                { CardId.TemperTheBlade, () => new TemperTheBlade() },
                { CardId.Tempest, () => new Tempest() },
                { CardId.Thaw, () => new Thaw() },
                { CardId.UnburdenedStrike, () => new UnburdenedStrike() },
                { CardId.VanguardsPromise, () => new VanguardsPromise() },
                { CardId.Vindicate, () => new Vindicate() },
                { CardId.Whirlwind, () => new Whirlwind() },
                { CardId.ZealousVow, () => new ZealousVow() },
            };

        public static CardBase Create(CardId cardId)
        {
            return CardConstructors.TryGetValue(cardId, out var create)
                ? create()
                : null;
        }

        public static CardBase Create(string cardId)
        {
            return GameIdExtensions.TryParseCardId(cardId, out var parsed)
                ? Create(parsed)
                : null;
        }

        public static Dictionary<CardId, CardBase> GetAllCards()
        {
            return CardConstructors
                .Where(entry => entry.Key != CardId.Curse)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value());
        }
    }
}
