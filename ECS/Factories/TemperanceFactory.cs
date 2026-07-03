using System.Collections.Generic;
using Crusaders30XX.ECS.Objects.Temperance;

namespace Crusaders30XX.ECS.Factories
{
    public static class TemperanceFactory
    {
        public static TemperanceBase Create(string temperanceId)
        {
            return temperanceId switch
            {
                "angelic_aura" => new AngelicAura(),
                "fling_fling" => new FlingFling(),
                "iron_resolve" => new IronResolve(),
                "measured_breath" => new MeasuredBreath(),
                "radiance" => new Radiance(),
                "static_surge" => new StaticSurge(),
                "unsheath" => new Unsheath(),
                _ => null
            };
        }

        public static Dictionary<string, TemperanceBase> GetAllTemperanceAbilities()
        {
            return new Dictionary<string, TemperanceBase>
            {
                { "angelic_aura", new AngelicAura() },
                { "fling_fling", new FlingFling() },
                { "iron_resolve", new IronResolve() },
                { "measured_breath", new MeasuredBreath() },
                { "radiance", new Radiance() },
                { "static_surge", new StaticSurge() },
                { "unsheath", new Unsheath() },
            };
        }
    }
}
