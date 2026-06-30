#pragma once
#include <Windows.h>
#include <string>
#include <thread>
#include <atomic>
#include <functional>
#include <unordered_map>

#define BRIDGE_PORT 9001
extern std::string g_bridgeHost;


struct SkillEntry
{
    uint32_t id;
    std::string readableName;
    std::string iconFile;
    bool isPassive;
};

struct NavNode
{
    std::string id;
    int x, y;
};

struct NavEdge
{
    std::string fromId;
    std::string toId;
};

struct PlayerState
{
    std::string accName;
    std::string charName;
    int accJID = 0;
    int charId = 0;
    bool IsAfk = false;
    int ZerkLevel = 0;
    int strength = 0, intelligence = 0;
    int hp = 0, mp = 0;
    int maxHp = 0, maxMp = 0;
    int sessionKills = 0;
    int unusedStatPoints = 0;
    int currentLevel = 0;
    uint64_t gold = 0;
    
};
enum class PotionType : int { Herb, Small, Medium, Large, XL, XXL };
enum class UniversalPillType : int { Small, Medium, Large, SpecialSmall };
enum class PurificationPillType : int { Small, Medium, Large, XL };
enum class SpeedDrugsType : int { DrugOfWind, DrugOfTyphoon };
enum class ScrollType : int { Normal, Special };
enum class AmmunitionType : int { Arrows, Bolts };
enum class RecoveryKitType : int { Small, Medium, Large };
enum class AbnormalPillType : int { Small, Medium };
struct ConsumableSettings
{
    // HP Potions
    bool BuyHpPotions = false;
    int HpPotionRefillAmount = 100;
    int HpPotionReturnThreshold = 10;
    PotionType HPType = PotionType::Herb;

    // MP Potions
    bool BuyMpPotions = false;
    int MpPotionRefillAmount = 100;
    int MpPotionReturnThreshold = 10;
    PotionType MPType = PotionType::Herb;

    bool BuyReturnScrolls = false;
    int ReturnScrollRefillAmount = 5; // Default helper value
    ScrollType ReturnScrollType = ScrollType::Normal;

    // Vigor Potions
    bool BuyVigorPotions = false;
    int VigorPotionRefillAmount = 100;
    int VigorPotionReturnThreshold = 10;

    // Univ. Pills
    bool BuyUniversalPills = false;
    int UniversalPillsRefillAmount = 100;
    int UniversalPillsReturnThreshold = 10;
    UniversalPillType UniPillType = UniversalPillType::Small;

    // Purif. Pills
    bool BuyPurifPills = false;
    int PurifPillsRefillAmount = 100;
    int PurifPillsReturnThreshold = 10;
    UniversalPillType PurificationPillType = UniversalPillType::Small; // Matches C# property naming mismatch

    // Speed Drugs
    bool BuySpeedDrugs = false;
    int SpeedDrugsRefillAmount = 100;
    int SpeedDrugsReturnThreshold = 10;
    SpeedDrugsType DrugType = SpeedDrugsType::DrugOfWind;

    // Recovery Kits
    bool BuyRecKits = false;
    int RecKitsRefillAmount = 100;
    int RecKitsReturnThreshold = 10;
    RecoveryKitType RecKitsType = RecoveryKitType::Small;

    // HGP Potions
    bool BuyHGPPotions = false;
    int HGPPotionsRefillAmount = 100;
    int HGPPotionsReturnThreshold = 10;

    // Abn. State Pills
    bool BuyAbnPill = false;
    int AbnPillRefillAmount = 100;
    int AbnPillReturnThreshold = 10;
    AbnormalPillType AbnPillType = AbnormalPillType::Small;

    // Horses
    bool BuyHorses = false;
    int HorsesRefillAmount = 5;
    AbnormalPillType HorsesType = AbnormalPillType::Small; // Matches C# type mismatch

    // Ammo
    bool BuyAmmo = false;
    int AmmoRefillAmount = 500;
    int AmmoReturnThreshold = 5;
    AmmunitionType AmmoType = AmmunitionType::Arrows;
};

struct AutowalkerSettings
{
    bool CastSpeedBuffWhileWalking = false;
    bool CastNoiseBuffWhileWalking = false;
};

struct AutoPotionSettings
{
    bool AutoUseHP = false;
    bool AutoUseMP = false;
    bool UseVigorPotions = false;

    int HPPotHealthThreshold = 0;
    int MPPotManaThreshold = 0;
    int VigorHPMPThreshold = 0;
    bool PreferVigorFirst = false;

    int HPDelay = 1000;
    int MPDelay = 1000;

    bool HealPets = false;
    int HealPetHPThreshold = 0;
    int HealPetsDelay = 1000;

    bool UseHGPPotions = false;
    int HGPPotionsThreshold = 0;

    bool AutoUseContPills = false; // Fixed name from the ImGui mismatch to hold AutoUseUnivPills
    bool AutoUsePurifPills = false;
};

struct PickupSettings
{
    bool PickAmmoIfAmountLowerThan = false;
    int AmmoAmount = 0;
    bool PickDelay = false;
    int PickDelayTime = 0;
    bool PickGold = false;
    bool PickAll = false;
};

struct BackTownMonitorSettings
{
    bool ReturnIfDead = false;
    bool ReturnIfInventoryFull = false;
};

struct MaintenanceSettings
{
    bool RepairWeapon = true;
    int RepairDurabilityThreshold = 7;
};

struct AttackSettings
{
    bool IgnoreDimensionPillars = false;
    bool UseZerkRightAwayWhenFull = false;
    bool UseZerkOnNormalGiants = false;
    bool UseZerkOnPartyMobs = false;
    bool UseZerkOnPartyGiants = false;
    bool UseZerkOnUniques = false;
    bool UseZerkIfNMobsAttackingSimulataneously = false;
    int ZerkMobCount = 0;
};

//MAIN BUNDLE STRUCT
struct BotSettings
{
    ConsumableSettings Consumables;
    MaintenanceSettings Maintenance;
    AutowalkerSettings Autowalker;
    AutoPotionSettings AutoPotion;
    PickupSettings Pickup;
    BackTownMonitorSettings BackTownMonitor;
    AttackSettings Attack;
};
struct State
{
    int sessionSeconds = 0;
    int totalSeconds = 0;
    int sessionKills = 0;
    int isAfk = 0;
    int isGM = 0;
    int accountJID = 0;

    DWORD syncTick = 0;

    // REGION
    std::string curRegionName;
    std::string curRegionCodeName = "";
    int currentRegionID = 0;
    int currentFloor = 0; // 0 = overworld
    int WorldX = 0;
    int WorldY = 0;
    int WorldZ = 0;
    int SectorX = 0;
    int SectorY = 0;

    // Recording waypoints
    std::string activeNode = "";
    int nodeCounter = 0;
    bool recordMode = false;
    int totalRecorded = 0;
    char jumpBuf[64] = {};

    // Radar
    std::vector<NavNode> g_recordedNodes;
    std::vector<NavEdge> g_recordedEdges;
    float radarZoom = 0.5f;
    float radarOffsetX = 0.0f;
    float radarOffsetY = 0.0f;
    char regionName[128] = "Jangan";
    double lastSaveTime = 0.0;
    bool showSaveNotification = false;
    double notificationTime = 0.0;
    int tileRadius = 6;
    float pointFontSize = 13.0f;

    std::vector<SkillEntry> availableSkills;
    std::vector<SkillEntry> attackSkills;
    std::vector<SkillEntry> buffSkills;
    std::vector<uint32_t> pendingAttackIds;
    std::vector<uint32_t> pendingBuffIds;

    std::string botStateLabel = "Idle";
    float distanceToTarget = 0.0f;
    int lastTargetUid = 0;

    int savedBotX = 0;
    int savedBotY = 0;
    int savedBotZ = 0;
    int savedBotR = 25;
    int savedBotRegionId = 0;
    bool hasSavedBotConfig = false;

    BotSettings botSettings;

    // Move speed testing
    bool hasActiveMoveSample = false;

    std::chrono::steady_clock::time_point moveStartTime;

    int moveStartX = 0;
    int moveStartY = 0;
    int moveStartZ = 0;

    int moveEndX = 0;
    int moveEndY = 0;
    int moveEndZ = 0;
};
using BridgeHandler = std::function<void(const std::string& json)>;
class DllBridge
{
public:
    void RegisterHandler(const std::string& type, BridgeHandler handler);
    std::string m_username;
    std::atomic<bool> m_connected{ false };
    std::atomic<bool> m_started{ false };
    PlayerState m_state;
    State m_sessionState;
    std::function<void(const std::string& type, const std::string& json)> OnEvent;
    std::vector<int> unclaimedRewards;
    void ClearSession();
    void SetIdentity(const std::string& username);
    void Connect();
    void Send(const std::string& msg);
    std::string ExtractStr(const std::string& json, const std::string& key);
    int ExtractInt(const std::string& json, const std::string& key);
    uint64_t ExtractUint64(const std::string& json, const std::string& key);
    void ForceReconnect(const std::string& username);
    float ExtractFloat(const std::string& json, const std::string& key);
    bool ExtractBool(const std::string& json, const std::string& key);
private:
    void* m_socket = nullptr; // opaque
    void RunLoop();
    
    void Dispatch(const std::string& json);
    std::unordered_map<std::string, BridgeHandler> m_handlers;
};

extern DllBridge g_bridge;