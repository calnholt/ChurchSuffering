using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Medals;

namespace Crusaders30XX.ECS.Factories
{
    public static class MedalFactory
    {
        private static readonly IReadOnlyDictionary<MedalId, Func<MedalBase>> MedalConstructors =
            new Dictionary<MedalId, Func<MedalBase>>
            {
                { MedalId.StLuke, () => new StLuke() },
                { MedalId.StMichael, () => new StMichael() },
                { MedalId.StNicholas, () => new StNicholas() },
                { MedalId.StPeter, () => new StPeter() },
                { MedalId.StPaulMiki, () => new StPaulMiki() },
                { MedalId.StLouieIX, () => new StLouieIX() },
                { MedalId.StSebastian, () => new StSebastian() },
                { MedalId.StFrancisDeSales, () => new StFrancisDeSales() },
                { MedalId.StHomobonus, () => new StHomobonus() },
                { MedalId.StClare, () => new StClare() },
                { MedalId.StElijah, () => new StElijah() },
                { MedalId.StJoanOfArc, () => new StJoanOfArc() },
                { MedalId.StJerome, () => new StJerome() },
                { MedalId.StLonginus, () => new StLonginus() },
                { MedalId.StBenedict, () => new StBenedict() },
                { MedalId.StSimonOfCyrene, () => new StSimonOfCyrene() },
                { MedalId.StAugustine, () => new StAugustine() },
                { MedalId.StOlaf, () => new StOlaf() },
                { MedalId.StRita, () => new StRita() },
                { MedalId.StChristopher, () => new StChristopher() },
                { MedalId.StLawrence, () => new StLawrence() },
                { MedalId.StLazarus, () => new StLazarus() },
            };

        public static MedalBase Create(MedalId medalId)
        {
            return MedalConstructors.TryGetValue(medalId, out var create)
                ? create()
                : null;
        }

        public static MedalBase Create(string medalId)
        {
            return GameIdExtensions.TryParseMedalId(medalId, out var parsed)
                ? Create(parsed)
                : null;
        }

        public static Dictionary<MedalId, MedalBase> GetAllMedals()
        {
            return MedalConstructors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value());
        }
    }
}
