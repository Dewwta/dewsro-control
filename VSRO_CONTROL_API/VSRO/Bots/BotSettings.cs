namespace VSRO_CONTROL_API.VSRO.Bots
{
    #region - Enums! :3 -

    public enum PotionType
    {
        Herb,
        Small,
        Medium,
        Large,
        XL,
        XXL
    }
    public enum UniversalPillType
    {
        Small,
        Medium,
        Large,
        SpecialSmall,
        SpecialMedium
    }
    public enum PurificationPillType
    {
        Small,
        Medium,
        Large,
        XL
    }
    public enum SpeedDrugsType
    {
        DrugOfWind,
        DrugOfTyphoon
    }
    public enum ScrollType
    {
        Normal,
        Special,
    }
    public enum AmmunitionType
    {
        Arrows,
        Bolts
    }
    public enum RecoveryKitType
    {
        Small,
        Medium,
        Large
    }
    public enum HorseType
    {
        RedHorse,
        ShadowHorse,
        DragonHorse,
        IroncladHorse
    }
    public enum AbnormalPillType
    {
        Small,
        Medium
    }

    #endregion

    /// <summary>
    /// Runtime configuration for the bot brain.
    /// Injected at bot start, persisted via SaveBotConfig.
    /// </summary>
    public class BotSettings
    {
        // Add:
        // Prioritize Uniques before normal kills
        // Prioritize buff consumables like damage or defense before skills
        public ConsumableSettings Consumables { get; set; } = new();
        public MaintenanceSettings Maintenance { get; set; } = new();
        public AutowalkerSettings Autowalker { get; set; } = new();
        public AutoPotionSettings AutoPotion { get; set; } = new();
        public PickupSettings Pickup { get; set; } = new();
        public BackTownMonitorSettings BackTownMonitor { get; set; } = new();
        public AttackSettings Attack { get; set; } = new();

        public class ConsumableSettings
        {
            #region - Buys -

            // HP Potions
            public bool BuyHpPotions { get; set; } = false;
            public int HpPotionRefillAmount { get; set; } = 100;
            public int HpPotionReturnThreshold { get; set; } = 10;
            public PotionType HPType { get; set; } = PotionType.Herb;

            // MP Potions
            public bool BuyMpPotions { get; set; } = false;
            public int MpPotionRefillAmount { get; set; } = 100;
            public int MpPotionReturnThreshold { get; set; } = 10;
            public PotionType MPType { get; set; } = PotionType.Herb;

            public bool BuyReturnScrolls { get; set; } = false;
            public int ReturnScrollRefillAmount { get; set; }
            public ScrollType ReturnScrollType { get; set; }
            // Must buy the amount and keep that amount, no threshold, when it goes to town, it should buy 1 + the 4 it already has if its e.g. 5.

            // Vigor Potions
            public bool BuyVigorPotions { get; set; } = false;
            public int VigorPotionRefillAmount { get; set; } = 100;
            public int VigorPotionReturnThreshold { get; set; } = 10;
            // No type for vigor pots

            // Univ. Pills
            public bool BuyUniversalPills { get; set; } = false;
            public int UniversalPillsRefillAmount { get; set; } = 100;
            public int UniversalPillsReturnThreshold { get; set; } = 10;
            public UniversalPillType UniPillType { get; set; }

            // Purif. Pills
            public bool BuyPurifPills { get; set; } = false;
            public int PurifPillsRefillAmount { get; set; } = 100;
            public int PurifPillsReturnThreshold { get; set; } = 10;
            public PurificationPillType PurificationPillType { get; set; }

            // Speed Drugs
            public bool BuySpeedDrugs { get; set; } = false;
            public int SpeedDrugsRefillAmount { get; set; } = 100;
            public int SpeedDrugsReturnThreshold { get; set; } = 10;
            public SpeedDrugsType DrugType { get; set; }

            // Recovery Kits
            public bool BuyRecKits { get; set; } = false;
            public int RecKitsRefillAmount { get; set; } = 100;
            public int RecKitsReturnThreshold { get; set; } = 10;
            public RecoveryKitType RecKitsType { get; set; }

            // HGP Potions
            public bool BuyHGPPotions { get; set; } = false;
            public int HGPPotionsRefillAmount { get; set; } = 100;
            public int HGPPotionsReturnThreshold { get; set; } = 10;

            // Abn. State Pills
            public bool BuyAbnPill { get; set; } = false;
            public int AbnPillRefillAmount { get; set; } = 100;
            public int AbnPillReturnThreshold { get; set; } = 10;
            public AbnormalPillType AbnPillType { get; set; }

            // Horses
            public bool BuyHorses { get; set; } = false;
            public int HorsesRefillAmount { get; set; } = 100;
            // Same as return scrolls.
            public AbnormalPillType HorsesType { get; set; }


            // Ammo
            public bool BuyAmmo { get; set; } = false;
            public int AmmoRefillAmount { get; set; } = 500;
            /// <summary>Return to town when arrow stack drops below this.</summary>
            public int AmmoReturnThreshold { get; set; } = 50;
            public AmmunitionType AmmoType { get; set; }

            #endregion

            
        }
        public class AutowalkerSettings
        {
            public bool CastSpeedBuffWhileWalking { get; set; } = false;
            public bool CastNoiseBuffWhileWalking { get; set; } = false;
        }
        public class AutoPotionSettings
        {
            public bool AutoUseHP { get; set; } = false;
            public bool AutoUseMP { get; set; } = false;
            public bool UseVigorPotions { get; set; } = false;

            // When health gets below this amount, auto pot. Vigors
            public int HPPotHealthThreshold { get; set; } // 0-100 Should be a % value
            public int MPPotManaThreshold { get; set; } // 0-100 Should be a % value
            public int VigorHPMPThreshold { get; set; } // 0-100 Should be a % value
            public bool PreferVigorFirst { get; set; } = false;

            public int HPDelay { get; set; } = 1000; // 1000ms
            public int MPDelay { get; set; } = 1000; // 1000ms
            
            public bool HealPets { get; set; } = false;
            public int HealPetHPThreshold { get; set; }
            public int HealPetsDelay { get; set; } = 1000; // 1000ms

            public bool UseHGPPotions { get; set; } = false;
            public int HGPPotionsThreshold { get; set; } // 0-100 Should be a % value

            public bool AutoUseUnivPills { get; set; } = false;
            public bool AutoUsePurifPills { get; set; } = false;

        }
        public class PickupSettings
        {
            public bool PickAmmoIfAmountLowerThan { get; set; } = false;
            public int AmmoAmount { get; set; }
            public bool PickDelay { get; set; } = false;
            public int PickDelayTime { get; set; }

            public bool PickGold { get; set; } = false;

            // overrides pick types
            public bool PickAll { get; set; } = false;


        }
        public class BackTownMonitorSettings
        {
            public bool ReturnIfDead { get; set; } = false;
            public bool ReturnIfInventoryFull { get; set; } = false;
        }
        public class MaintenanceSettings
        {
            public bool RepairWeapon { get; set; } = true;
            /// <summary>Return to town when weapon durability drops below this percent (0–100).</summary>
            public int RepairDurabilityThreshold { get; set; } = 7;
        }
        public class AttackSettings
        {
            public bool IgnoreDimensionPillars { get; set; } = false;

            // Overrides all below.
            public bool UseZerkRightAwayWhenFull { get; set; } = false;

            public bool UseZerkOnNormalGiants { get; set; } = false;
            public bool UseZerkOnPartyMobs { get; set; } = false;
            public bool UseZerkOnPartyGiants { get; set; } = false;
            public bool UseZerkOnUniques { get; set; } = false;
            public bool UseZerkIfNMobsAttackingSimulataneously { get; set; } = false;
            public int ZerkMobCount { get; set; }
        }
    }
}