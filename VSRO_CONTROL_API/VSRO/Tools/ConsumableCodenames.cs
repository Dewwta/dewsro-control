namespace VSRO_CONTROL_API.VSRO.Bots
{
    public static class ConsumableCodeNames
    {
        public static readonly Dictionary<PotionType, string> HpPotions = new()
        {
            [PotionType.Herb] = "ITEM_ETC_HP_POTION_01",
            [PotionType.Small] = "ITEM_ETC_HP_POTION_02",
            [PotionType.Medium] = "ITEM_ETC_HP_POTION_03",
            [PotionType.Large] = "ITEM_ETC_HP_POTION_04",
            [PotionType.XL] = "ITEM_ETC_HP_POTION_05",
            [PotionType.XXL] = "ITEM_ETC_HP_POTION_06",
        };

        public static readonly Dictionary<PotionType, string> MpPotions = new()
        {
            [PotionType.Herb] = "ITEM_ETC_MP_POTION_01",
            [PotionType.Small] = "ITEM_ETC_MP_POTION_02",
            [PotionType.Medium] = "ITEM_ETC_MP_POTION_03",
            [PotionType.Large] = "ITEM_ETC_MP_POTION_04",
            [PotionType.XL] = "ITEM_ETC_MP_POTION_05",
            [PotionType.XXL] = "ITEM_ETC_MP_POTION_06",
        };

        public static readonly Dictionary<PotionType, string> VigorPotions = new()
        {
            // Only one type in SRO — assign same string to all or just use one entry
            [PotionType.Small] = "ITEM_ETC_ALL_SPOTION_01",
            [PotionType.Medium] = "ITEM_ETC_ALL_SPOTION_01",
            [PotionType.Large] = "ITEM_ETC_ALL_SPOTION_01",
            [PotionType.XL] = "ITEM_ETC_ALL_SPOTION_01",
            [PotionType.XXL] = "ITEM_ETC_ALL_SPOTION_01"
        };

        public static readonly Dictionary<UniversalPillType, string> UniversalPills = new()
        {
            [UniversalPillType.Small] = "ITEM_ETC_CURE_ALL_01",
            [UniversalPillType.Medium] = "ITEM_ETC_CURE_ALL_02",
            [UniversalPillType.Large] = "ITEM_ETC_CURE_ALL_03",
            [UniversalPillType.SpecialSmall] = "ITEM_ETC_CURE_ALL_04",
            [UniversalPillType.SpecialMedium] = "ITEM_ETC_CURE_ALL_05",
        };

        public static readonly Dictionary<PurificationPillType, string> PurificationPills = new()
        {
            [PurificationPillType.Small] = "ITEM_ETC_CURE_RANDOM_01",
            [PurificationPillType.Medium] = "ITEM_ETC_CURE_RANDOM_02",
            [PurificationPillType.Large] = "ITEM_ETC_CURE_RANDOM_03",
            [PurificationPillType.XL] = "ITEM_ETC_CURE_RANDOM_04",
        };

        public static readonly Dictionary<SpeedDrugsType, string> SpeedDrugs = new()
        {
            [SpeedDrugsType.DrugOfWind] = "ITEM_ETC_ARCHEMY_POTION_SPEED_05",
            [SpeedDrugsType.DrugOfTyphoon] = "ITEM_ETC_ARCHEMY_POTION_SPEED_11",
        };

        public static readonly Dictionary<RecoveryKitType, string> RecoveryKits = new()
        {
            [RecoveryKitType.Small] = "ITEM_ETC_COS_HP_POTION_01",
            [RecoveryKitType.Medium] = "ITEM_ETC_COS_HP_POTION_02",
            [RecoveryKitType.Large] = "ITEM_ETC_COS_HP_POTION_03",
        };

        public static readonly Dictionary<AbnormalPillType, string> AbnormalPills = new()
        {
            [AbnormalPillType.Small] = "NONE",
            [AbnormalPillType.Medium] = "NONE",
        };

        public static readonly Dictionary<AmmunitionType, string> Ammunition = new()
        {
            [AmmunitionType.Arrows] = "ITEM_ETC_AMMO_ARROW_01",
            [AmmunitionType.Bolts] = "ITEM_ETC_AMMO_BOLT_01",
        };

        // HGP has no type enum — single codename
        public const string HGPPotion = "ITEM_COS_P_HGP_POTION_01";

        // Return scrolls
        public static readonly Dictionary<ScrollType, string> ReturnScrolls = new()
        {
            [ScrollType.Normal] = "ITEM_ETC_SCROLL_RETURN_01",
            [ScrollType.Special] = "ITEM_ETC_SCROLL_RETURN_02",
        };
    }
}