namespace ChurchSuffering.ECS.Services
{
    public static class CrusaderPortraitAssets
    {
        public const string DialogPortraitAsset = "crusader_sword";

        public static string ResolveBattlePortraitAsset(string weaponId) => weaponId switch
        {
            "hammer" => "crusader_hammer",
            "dagger" => "crusader_dagger",
            _ => "crusader_sword",
        };

        public static string ResolveWeaponCardArtAsset(string weaponId) => $"CardArt/{weaponId}";
    }
}
