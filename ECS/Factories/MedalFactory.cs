using System.Collections.Generic;
using Crusaders30XX.ECS.Objects.Medals;

namespace Crusaders30XX.ECS.Factories
{
    /// <summary>
    /// Factory for creating MedalBase instances from medal IDs
    /// </summary>
    public static class MedalFactory
    {
        /// <summary>
        /// Creates a MedalBase instance from a medal ID string
        /// </summary>
        /// <param name="medalId">The medal ID (e.g., "st_luke")</param>
        /// <returns>The corresponding MedalBase instance, or null if not found</returns>
        public static MedalBase Create(string medalId)
        {
            return medalId switch
            {
                "st_luke" => new StLuke(),
                "st_michael" => new StMichael(),
                "st_nicholas" => new StNicholas(),
                "st_peter" => new StPeter(),
                "st_paul_miki" => new StPaulMiki(),
                "st_louie_ix" => new StLouieIX(),
                "st_sebastian" => new StSebastian(),
                "st_francis_de_sales" => new StFrancisDeSales(),
                "st_homobonus" => new StHomobonus(),
                "st_clare" => new StClare(),
                "st_elijah" => new StElijah(),
                "st_joan_of_arc" => new StJoanOfArc(),
                "st_jerome" => new StJerome(),
                "st_longinus" => new StLonginus(),
                "st_benedict" => new StBenedict(),
                "st_simon_of_cyrene" => new StSimonOfCyrene(),
                "st_augustine" => new StAugustine(),
                "st_olaf" => new StOlaf(),
                "st_rita" => new StRita(),
                "st_christopher" => new StChristopher(),
                "st_lawrence" => new StLawrence(),
                _ => null
            };
        }

        /// <summary>
        /// Returns a dictionary of all available medals, keyed by medal ID
        /// </summary>
        /// <returns>A dictionary mapping medal IDs to MedalBase instances</returns>
        public static Dictionary<string, MedalBase> GetAllMedals()
        {
            return new Dictionary<string, MedalBase>
            {
                { "st_luke", new StLuke() },
                { "st_michael", new StMichael() },
                { "st_nicholas", new StNicholas() },
                { "st_peter", new StPeter() },
                { "st_paul_miki", new StPaulMiki() },
                { "st_louie_ix", new StLouieIX() },
                { "st_sebastian", new StSebastian() },
                { "st_francis_de_sales", new StFrancisDeSales() },
                { "st_homobonus", new StHomobonus() },
                { "st_clare", new StClare() },
                { "st_elijah", new StElijah() },
                { "st_joan_of_arc", new StJoanOfArc() },
                { "st_jerome", new StJerome() },
                { "st_longinus", new StLonginus() },
                { "st_benedict", new StBenedict() },
                { "st_simon_of_cyrene", new StSimonOfCyrene() },
                { "st_augustine", new StAugustine() },
                { "st_olaf", new StOlaf() },
                { "st_rita", new StRita() },
                { "st_christopher", new StChristopher() },
                { "st_lawrence", new StLawrence() },
            };
        }
    }
}
