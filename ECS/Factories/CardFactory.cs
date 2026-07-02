using System.Collections.Generic;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Factories
{
    /// <summary>
    /// Factory for creating CardBase instances from card IDs
    /// </summary>
    public static class CardFactory
    {
        /// <summary>
        /// Creates a CardBase instance from a card ID string
        /// </summary>
        /// <param name="cardId">The card ID (e.g., "absolution")</param>
        /// <returns>The corresponding CardBase instance, or null if not found</returns>
        public static CardBase Create(string cardId)
        {
            return cardId switch
            {
                "absolution" => new Absolution(),
                "ark_of_the_covenant" => new ArkOfTheCovenant(),
                "battering_blow" => new BatteringBlow(),
                "burn" => new Burn(),
                "carpe_diem" => new CarpeDiem(),
                "colorless_3_block" => new ColorlessBlock(),
                "consecrate" => new Consecrate(),
                "courageous" => new Courageous(),
                "crimson_rite" => new CrimsonRite(),
                "crusade" => new Crusade(),
                "curse" => new Curse(),
                "dagger" => new Dagger(),
                "deus_vult" => new DeusVult(),
                "divine_protection" => new DivineProtection(),
                "dowse_with_holy_water" => new DowseWithHolyWater(),
                "ember_harvest" => new EmberHarvest(),
                "exaltation" => new Exaltation(),
                "fervor" => new Fervor(),
                "forge_strike" => new ForgeStrike(),
                "fury" => new Fury(),
                "hold_the_line" => new HoldTheLine(),
                "hammer" => new Hammer(),
                "hidden_kunai" => new HiddenKunai(),
                "impale" => new Impale(),
                "increase_faith" => new IncreaseFaith(),
                "iron_covenant" => new IronCovenant(),
                "kunai" => new Kunai(),
                "litany_of_wrath" => new LitanyOfWrath(),
                "mantlet" => new Mantlet(),
                "quick_wit" => new QuickWit(),
                "rally_the_faithful" => new RallyTheFaithful(),
                "relentless_strike" => new RelentlessStrike(),
                "pouch_of_kunai" => new PouchOfKunai(),
                "ravage" => new Ravage(),
                "razor_storm" => new RazorStorm(),
                "reckoning" => new Reckoning(),
                "reap" => new Reap(),
                "renounce_and_hone" => new RenounceAndHone(),
                "sacrifice" => new Sacrifice(),
                "serpent_crush" => new SerpentCrush(),
                "seize" => new Seize(),
                "shield_of_faith" => new ShieldOfFaith(),
                "smite" => new Smite(),
                "stab" => new Stab(),
                "steadfast_resolve" => new SteadfastResolve(),
                "stalwart" => new Stalwart(),
                "steel_the_spirit" => new SteelTheSpirit(),
                "stoked_assault" => new StokedAssault(),
                "strike" => new Strike(),
                "sudden_thrust" => new SuddenThrust(),
                "stoke_the_furnace" => new StokeTheFurnace(),
                "sword" => new Sword(),
                "temper_the_blade" => new TemperTheBlade(),
                "tempest" => new Tempest(),
                "unburdened_strike" => new UnburdenedStrike(),
                "vanguards_promise" => new VanguardsPromise(),
                "vindicate" => new Vindicate(),
                "whirlwind" => new Whirlwind(),
                "zealous_vow" => new ZealousVow(),
                _ => null
            };
        }

        /// <summary>
        /// Returns a dictionary of all available cards, keyed by card ID
        /// </summary>
        /// <returns>A dictionary mapping card IDs to CardBase instances</returns>
        public static Dictionary<string, CardBase> GetAllCards()
        {
            return new Dictionary<string, CardBase>
            {
                { "absolution", new Absolution() },
                { "ark_of_the_covenant", new ArkOfTheCovenant() },
                { "battering_blow", new BatteringBlow() },
                { "burn", new Burn() },
                { "carpe_diem", new CarpeDiem() },
                { "colorless_3_block", new ColorlessBlock() },
                { "consecrate", new Consecrate() },
                { "courageous", new Courageous() },
                { "crimson_rite", new CrimsonRite() },
                { "crusade", new Crusade() },
                { "dagger", new Dagger() },
                { "deus_vult", new DeusVult() },
                { "divine_protection", new DivineProtection() },
                { "dowse_with_holy_water", new DowseWithHolyWater() },
                { "ember_harvest", new EmberHarvest() },
                { "exaltation", new Exaltation() },
                { "fervor", new Fervor() },
                { "forge_strike", new ForgeStrike() },
                { "fury", new Fury() },
                { "impale", new Impale() },
                { "increase_faith", new IncreaseFaith() },
                { "iron_covenant", new IronCovenant() },
                { "hold_the_line", new HoldTheLine() },
                { "hammer", new Hammer() },
                { "hidden_kunai", new HiddenKunai() },
                { "kunai", new Kunai() },
                { "litany_of_wrath", new LitanyOfWrath() },
                { "mantlet", new Mantlet() },
                { "quick_wit", new QuickWit() },
                { "rally_the_faithful", new RallyTheFaithful() },
                { "relentless_strike", new RelentlessStrike() },
                { "pouch_of_kunai", new PouchOfKunai() },
                { "ravage", new Ravage() },
                { "razor_storm", new RazorStorm() },
                { "reckoning", new Reckoning() },
                { "reap", new Reap() },
                { "renounce_and_hone", new RenounceAndHone() },
                { "sacrifice", new Sacrifice() },
                { "serpent_crush", new SerpentCrush() },
                { "seize", new Seize() },
                { "shield_of_faith", new ShieldOfFaith() },
                { "smite", new Smite() },
                { "stab", new Stab() },
                { "steadfast_resolve", new SteadfastResolve() },
                { "stalwart", new Stalwart() },
                { "steel_the_spirit", new SteelTheSpirit() },
                { "stoked_assault", new StokedAssault() },
                { "strike", new Strike() },
                { "sudden_thrust", new SuddenThrust() },
                { "stoke_the_furnace", new StokeTheFurnace() },
                { "sword", new Sword() },
                { "temper_the_blade", new TemperTheBlade() },
                { "tempest", new Tempest() },
                { "unburdened_strike", new UnburdenedStrike() },
                { "vanguards_promise", new VanguardsPromise() },
                { "vindicate", new Vindicate() },
                { "whirlwind", new Whirlwind() },
                { "zealous_vow", new ZealousVow() }
            };
        }
    }
}
