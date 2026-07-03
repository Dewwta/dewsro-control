#include "Control.h"
#include "hooks/dx9_hook.h"
#include "hooks/patcher.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"
#include "net/NetActions.h"
#include "ClientVersion.h"
#include "Logging/Logger.h"
#include "client/RewardWindow.h"
#include <sstream>
#include "Settings.h"
#include "client/AchievementWindow.h"
#include "client/pk2/Pk2Reader.h"

static int DetectStoneCaveFloor(float worldZ)
{
    if (worldZ < 116.f)  return 1;
    if (worldZ < 254.f)  return 2;
    if (worldZ < 393.f)  return 3;
    return 4;
}

static std::string ExtractHostFromDivisionInfo(const std::vector<uint8_t>& data)
{
    std::string best, cur;
    for (uint8_t b : data) {
        char c = (char)b;
        bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                     (c >= '0' && c <= '9') || c == '-' || c == '.';
        if (valid) {
            cur += c;
        } else {
            if (cur.find('.') != std::string::npos && cur.size() > best.size())
                best = cur;
            cur.clear();
        }
    }
    if (cur.find('.') != std::string::npos && cur.size() > best.size())
        best = cur;
    return best;
}


void RegisterAllHandlers() {
    g_bridge.RegisterHandler("login_ack", [](const std::string& _) {
        auto& log = GetLogger();
        log.Info("Control_Handler::login_ack", "Proxy connection acknowledged — sending client hello");
        NetActions::SendClientHello(g_bridge.m_username, DLL_COMPAT_VERSION);
    });

    g_bridge.RegisterHandler("version_mismatch", [](const std::string& json) {
        std::string expected = g_bridge.ExtractStr(json, "expected");
        std::string got      = g_bridge.ExtractStr(json, "got");
        std::string msg =
            "This client is not compatible with the server.\n\n"
            "Client version:   " + got + "\n"
            "Required version: " + expected + "\n\n"
            "Please update your client files and try again.";
        MessageBoxA(NULL, msg.c_str(), "DewSRO - Version Mismatch", MB_OK | MB_ICONERROR);
        ExitProcess(1);
    });

    g_bridge.RegisterHandler("session_init", [](const std::string& json) {
        g_bridge.m_state.charName = g_bridge.ExtractStr(json, "charName");
        g_bridge.m_state.accJID = g_bridge.ExtractInt(json, "jid");
        g_bridge.m_state.accName = g_bridge.ExtractStr(json, "accName");
    });

    g_bridge.RegisterHandler("char_init", [](const std::string& json) {
        auto& log = GetLogger();
        g_bridge.m_state.hp = g_bridge.ExtractInt(json, "hp");
        g_bridge.m_state.mp = g_bridge.ExtractInt(json, "mp");
        g_bridge.m_state.sessionKills = g_bridge.ExtractInt(json, "sessionKills");
        g_bridge.m_state.unusedStatPoints = g_bridge.ExtractInt(json, "unusedStatPoints");
        g_bridge.m_state.currentLevel = g_bridge.ExtractInt(json, "currentLevel");
        g_bridge.m_state.gold = g_bridge.ExtractUint64(json, "gold");
        log.Info("Control_Handler::char_init", "Character date received.");
    });

    g_bridge.RegisterHandler("stat_init", [](const std::string& json) {
        g_bridge.m_state.maxHp = g_bridge.ExtractInt(json, "maxHp");
        g_bridge.m_state.maxMp = g_bridge.ExtractInt(json, "maxMp");
        g_bridge.m_state.strength = g_bridge.ExtractInt(json, "strength");
        g_bridge.m_state.intelligence = g_bridge.ExtractInt(json, "intelligence");
    });

    g_bridge.RegisterHandler("session_sync", [](const std::string& json) {
        g_bridge.m_sessionState.sessionSeconds = g_bridge.ExtractInt(json, "sessionSeconds");
        g_bridge.m_sessionState.totalSeconds = g_bridge.ExtractInt(json, "totalSeconds");
        g_bridge.m_sessionState.sessionKills = g_bridge.ExtractInt(json, "sessionKills");
        g_bridge.m_sessionState.isAfk = g_bridge.ExtractInt(json, "isAfk");
        g_bridge.m_sessionState.accountJID = g_bridge.ExtractInt(json, "accountJID");
        g_bridge.m_sessionState.isGM = g_bridge.ExtractInt(json, "isGM");
        g_bridge.m_sessionState.syncTick = GetTickCount();
    });

    g_bridge.RegisterHandler("kill_update", [](const std::string& json) {
        g_bridge.m_sessionState.sessionKills = g_bridge.ExtractInt(json, "sessionKills");
    });

    g_bridge.RegisterHandler("movement_sync", [](const std::string& json) {
        auto& ss = g_bridge.m_sessionState;

        ss.curRegionName = g_bridge.ExtractStr(json, "regionReadableName");
        ss.curRegionCodeName = g_bridge.ExtractStr(json, "regionName");
        ss.currentRegionID = g_bridge.ExtractInt(json, "regionId");
        ss.WorldX = g_bridge.ExtractInt(json, "wX");
        ss.WorldY = g_bridge.ExtractInt(json, "wY");
        ss.WorldZ = g_bridge.ExtractInt(json, "wZ");
        ss.SectorX = g_bridge.ExtractInt(json, "xSec");
        ss.SectorY = g_bridge.ExtractInt(json, "ySec");

        auto& s = ss;

        static int lastX = 0, lastY = 0, lastZ = 0;

        if (s.hasActiveMoveSample)
        {
            s.moveEndX = s.WorldX;
            s.moveEndY = s.WorldY;
            s.moveEndZ = s.WorldZ;
        }

        s.hasActiveMoveSample = true;
        s.moveStartTime = std::chrono::steady_clock::now();

        s.moveStartX = lastX;
        s.moveStartY = lastY;
        s.moveStartZ = lastZ;

        lastX = s.WorldX;
        lastY = s.WorldY;
        lastZ = s.WorldZ;

        ss.curRegionName = g_bridge.ExtractStr(json, "regionReadableName");
        ss.curRegionCodeName = g_bridge.ExtractStr(json, "regionName");
        ss.WorldX = g_bridge.ExtractInt(json, "wX");
        ss.WorldY = g_bridge.ExtractInt(json, "wY");
        ss.WorldZ = g_bridge.ExtractInt(json, "wZ");
        ss.SectorX = g_bridge.ExtractInt(json, "xSec");
        ss.SectorY = g_bridge.ExtractInt(json, "ySec");

        auto& code = ss.curRegionCodeName;
        auto pipe = code.find('|');
        if (pipe != std::string::npos)
        {
            std::string suffix = code.substr(pipe + 1);
            if (suffix.rfind("floor:", 0) == 0)
                ss.currentFloor = std::stoi(suffix.substr(6));
        }
        else if (code == "Stone Cave")
        {
            ss.currentFloor = DetectStoneCaveFloor((float)ss.WorldZ);
        }
        else
        {
            ss.currentFloor = 0; // overworld, no floor
        }
        });

    g_bridge.RegisterHandler("level_reward", [](const std::string& json) {
        int level = g_bridge.ExtractInt(json, "level");
        std::vector<RewardOption> options;

        auto arrStart = json.find("\"options\":[");
        if (arrStart != std::string::npos) {
            arrStart += 11; 
            auto arrEnd = json.find(']', arrStart);
            std::string arr = json.substr(arrStart, arrEnd - arrStart);

            size_t pos = 0;
            while (pos < arr.size()) {
                auto objStart = arr.find('{', pos);
                auto objEnd = arr.find('}', objStart);
                if (objStart == std::string::npos) break;

                std::string obj = arr.substr(objStart, objEnd - objStart + 1);

                RewardOption opt;
                opt.code = g_bridge.ExtractStr(obj, "code");
                opt.name = g_bridge.ExtractStr(obj, "name");
                opt.plus = g_bridge.ExtractInt(obj, "plus");
                opt.qty =  g_bridge.ExtractInt(obj, "qty");
                opt.icon = g_bridge.ExtractStr(obj, "icon");
                if (!opt.code.empty())
                    options.push_back(opt);

                pos = objEnd + 1;
            }
        }

        g_rewardWindow.Open(level, std::move(options));
    });

    g_bridge.RegisterHandler("unclaimed_rewards", [](const std::string& json) {
        g_bridge.unclaimedRewards.clear();
        auto start = json.find('[');
        auto end = json.find(']');
       
        if (start == std::string::npos || end == std::string::npos) return;

        std::string arr = json.substr(start + 1, end - start - 1);
        if (arr.empty()) return;

        std::stringstream ss(arr);
        std::string token;
        while (std::getline(ss, token, ',')) {
            try { g_bridge.unclaimedRewards.push_back(std::stoi(token)); }
            catch (...) {}
        }
    });

    g_bridge.RegisterHandler("session_clear", [](const std::string& _) {
        g_bridge.ClearSession();
    });

    g_bridge.RegisterHandler("gold_update", [](const std::string& json) {
        g_bridge.m_state.gold = g_bridge.ExtractInt(json, "remainGold");
        
    });

    g_bridge.RegisterHandler("lvl_update", [](const std::string& json) {
        g_bridge.m_state.currentLevel = g_bridge.ExtractInt(json, "lvl");
    });

    g_bridge.RegisterHandler("achievements", [](const std::string& json) {
        std::vector<AchievementEntry> entries;
        
        auto arrStart = json.find("\"items\":[");
        if (arrStart == std::string::npos) return;
        arrStart += 9;
        auto arrEnd = json.rfind(']');
        if (arrEnd == std::string::npos) return;

        std::string arr = json.substr(arrStart, arrEnd - arrStart);
        size_t pos = 0;
        while (pos < arr.size())
        {
            auto objStart = arr.find('{', pos);
            if (objStart == std::string::npos) break;
            // find matching closing brace
            int depth = 0; size_t objEnd = objStart;
            for (size_t k = objStart; k < arr.size(); k++) {
                if (arr[k] == '{') depth++;
                else if (arr[k] == '}') { if (--depth == 0) { objEnd = k; break; } }
            }
            std::string obj = arr.substr(objStart, objEnd - objStart + 1);

            AchievementEntry e;
            e.name = g_bridge.ExtractStr(obj, "name");
            e.desc = g_bridge.ExtractStr(obj, "desc");
            e.type = g_bridge.ExtractStr(obj, "type");
            e.goal = (long long)g_bridge.ExtractInt(obj, "goal");
            e.progress = (long long)g_bridge.ExtractInt(obj, "progress");
            auto cpos = obj.find("\"completed\":");
            if (cpos != std::string::npos) {
                cpos += 12;
                while (cpos < obj.size() && obj[cpos] == ' ') cpos++;
                e.completed = obj.substr(cpos, 4) == "true";
            }
            
            e.completedAt = g_bridge.ExtractStr(obj, "completedAt");
            if (!e.name.empty()) entries.push_back(e);

            pos = objEnd + 1;
        }

        g_achWindow.Open(std::move(entries));
    });
    
    g_bridge.RegisterHandler("skill_pool_sync", [](const std::string& json) {
        auto& ss = g_bridge.m_sessionState;
        ss.availableSkills.clear();

        size_t skillsPos = json.find("\"skills\"");
        if (skillsPos == std::string::npos) return;

        size_t arrStart = json.find('[', skillsPos);
        size_t arrEnd = json.find(']', arrStart);
        if (arrStart == std::string::npos || arrEnd == std::string::npos) return;

        std::string arr = json.substr(arrStart, arrEnd - arrStart + 1);
        size_t pos = 0;
        while ((pos = arr.find("\"id\"", pos)) != std::string::npos) {
            SkillEntry sk;

            size_t colon = arr.find(':', pos);
            size_t comma = arr.find(',', colon);
            sk.id = (uint32_t)std::stoul(arr.substr(colon + 1, comma - colon - 1));

            size_t namePos = arr.find("\"name\"", pos);
            size_t nameColon = arr.find(':', namePos);
            size_t nameStart = arr.find('"', nameColon + 1) + 1;
            size_t nameEnd = arr.find('"', nameStart);
            sk.readableName = arr.substr(nameStart, nameEnd - nameStart);

            size_t passivePos = arr.find("\"passive\"", pos);
            size_t passiveColon = arr.find(':', passivePos);
            size_t passiveVal = arr.find_first_not_of(' ', passiveColon + 1);
            sk.isPassive = arr.substr(passiveVal, 4) == "true";

            size_t iconPos = arr.find("\"icon\"", pos);
            size_t nextIdPos = arr.find("\"id\"", pos + 1);
            if (iconPos != std::string::npos && (nextIdPos == std::string::npos || iconPos < nextIdPos)) {
                size_t iconColon = arr.find(':', iconPos);
                size_t iconQ1 = arr.find('"', iconColon + 1);
                if (iconQ1 != std::string::npos) {
                    size_t iconQ2 = arr.find('"', iconQ1 + 1);
                    if (iconQ2 != std::string::npos)
                        sk.iconFile = arr.substr(iconQ1 + 1, iconQ2 - iconQ1 - 1);
                }
            }

            ss.availableSkills.push_back(sk);
            pos = nameEnd;
        }
        auto& log = GetLogger();
        log.Info("skill_list_sync", "Pool size at resolution: " + std::to_string(ss.availableSkills.size()));

        for (auto& sk : ss.attackSkills) {
            log.Info("skill_list_sync", "Resolving ID " + std::to_string(sk.id));
            auto it = std::find_if(ss.availableSkills.begin(), ss.availableSkills.end(),
                [&](const SkillEntry& a) { return a.id == sk.id; });
            if (it != ss.availableSkills.end()) {
                sk.readableName = it->readableName;
                sk.iconFile = it->iconFile;
            }
        }
        for (auto& sk : ss.buffSkills) {
            log.Info("skill_list_sync", "Resolving ID " + std::to_string(sk.id));
            auto it = std::find_if(ss.availableSkills.begin(), ss.availableSkills.end(),
                [&](const SkillEntry& a) { return a.id == sk.id; });
            if (it != ss.availableSkills.end()) {
                sk.readableName = it->readableName;
                sk.iconFile = it->iconFile;
            }
        }
    });

    g_bridge.RegisterHandler("skill_list_sync", [](const std::string& json) {
        auto& ss = g_bridge.m_sessionState;
        ss.attackSkills.clear();
        ss.buffSkills.clear();

        auto parseSkillArray = [&](const std::string& key, std::vector<SkillEntry>& out) {
            size_t keyPos = json.find("\"" + key + "\"");
            if (keyPos == std::string::npos) return;

            size_t arrStart = json.find('[', keyPos);
            size_t arrEnd = json.find(']', arrStart);
            if (arrStart == std::string::npos || arrEnd == std::string::npos) return;

            std::string arr = json.substr(arrStart, arrEnd - arrStart + 1);
            size_t pos = 0;
            while ((pos = arr.find("\"id\"", pos)) != std::string::npos) {
                SkillEntry sk;

                size_t colon = arr.find(':', pos);
                size_t comma = arr.find(',', colon);
                sk.id = (uint32_t)std::stoul(arr.substr(colon + 1, comma - colon - 1));

                size_t namePos = arr.find("\"name\"", pos);
                size_t nameColon = arr.find(':', namePos);
                size_t nameStart = arr.find('"', nameColon + 1) + 1;
                size_t nameEnd = arr.find('"', nameStart);
                sk.readableName = "";
                sk.isPassive = false;

                out.push_back(sk);
                pos = nameEnd;
            }
            };

            parseSkillArray("attack", ss.attackSkills);
            parseSkillArray("buffs", ss.buffSkills);

            for (auto& sk : ss.attackSkills)
            {
                auto it = std::find_if(ss.availableSkills.begin(), ss.availableSkills.end(),
                    [&](const SkillEntry& a) { return a.id == sk.id; });

                if (it != ss.availableSkills.end()) {
                    sk.readableName = it->readableName;
                    sk.iconFile = it->iconFile;
                }
            }

            for (auto& sk : ss.buffSkills)
            {
                auto it = std::find_if(ss.availableSkills.begin(), ss.availableSkills.end(),
                    [&](const SkillEntry& a) { return a.id == sk.id; });

                if (it != ss.availableSkills.end()) {
                    sk.readableName = it->readableName;
                    sk.iconFile = it->iconFile;
                }
            }
        });

    g_bridge.RegisterHandler("bot_config_sync", [](const std::string& json) {
        g_bridge.m_sessionState.savedBotX = g_bridge.ExtractInt(json, "x");
        g_bridge.m_sessionState.savedBotY = g_bridge.ExtractInt(json, "y");
        g_bridge.m_sessionState.savedBotZ = g_bridge.ExtractInt(json, "z");
        g_bridge.m_sessionState.savedBotR = g_bridge.ExtractInt(json, "r");
        g_bridge.m_sessionState.savedBotRegionId = g_bridge.ExtractInt(json, "regionId");

        g_bridge.m_sessionState.hasSavedBotConfig = true;
    });

    g_bridge.RegisterHandler("bot_state_update", [](const std::string& json) {
        auto& ss = g_bridge.m_sessionState;

        ss.botStateLabel = g_bridge.ExtractStr(json, "botState");
        ss.distanceToTarget = g_bridge.ExtractFloat(json, "distanceToTarget");
        ss.lastTargetUid = g_bridge.ExtractInt(json, "targetUid");

        // Parse mob positions: "mobs":[{"x":...,"y":...,"refObjId":...},...]
        NearbyMobEntry tmp[k_MaxNearbyMobs]{};
        int cnt = 0;
        size_t aPos = json.find("\"mobs\"");
        if (aPos != std::string::npos) {
            aPos = json.find('[', aPos);
            size_t aEnd = (aPos != std::string::npos) ? json.find(']', aPos) : std::string::npos;
            if (aPos != std::string::npos && aEnd != std::string::npos) {
                size_t p = aPos + 1;
                while (p < aEnd && cnt < k_MaxNearbyMobs) {
                    size_t ob = json.find('{', p);
                    if (ob == std::string::npos || ob >= aEnd) break;
                    size_t oe = json.find('}', ob);
                    if (oe == std::string::npos || oe >= aEnd) break;
                    std::string obj = json.substr(ob, oe - ob + 1);
                    tmp[cnt].x        = g_bridge.ExtractInt(obj, "x");
                    tmp[cnt].y        = g_bridge.ExtractInt(obj, "y");
                    tmp[cnt].refObjId = static_cast<uint32_t>(g_bridge.ExtractUint64(obj, "refObjId"));
                    ++cnt;
                    p = oe + 1;
                }
            }
        }
        // Write array before count so render thread never sees stale entries
        memcpy(ss.nearbyMobs, tmp, sizeof(NearbyMobEntry) * cnt);
        ss.nearbyMobCount = cnt;
    });

    g_bridge.RegisterHandler("bot_settings_sync", [](const std::string& json) {
        auto& settings = g_bridge.m_sessionState.botSettings;

        // CONSUMABLES CATEGORY
        settings.Consumables.BuyHpPotions = g_bridge.ExtractBool(json, "BuyHpPotions");
        settings.Consumables.HpPotionRefillAmount = g_bridge.ExtractInt(json, "HpPotionRefillAmount");
        settings.Consumables.HpPotionReturnThreshold = g_bridge.ExtractInt(json, "HpPotionReturnThreshold");
        settings.Consumables.HPType = (PotionType)g_bridge.ExtractInt(json, "HPType");

        settings.Consumables.BuyMpPotions = g_bridge.ExtractBool(json, "BuyMpPotions");
        settings.Consumables.MpPotionRefillAmount = g_bridge.ExtractInt(json, "MpPotionRefillAmount");
        settings.Consumables.MpPotionReturnThreshold = g_bridge.ExtractInt(json, "MpPotionReturnThreshold");
        settings.Consumables.MPType = (PotionType)g_bridge.ExtractInt(json, "MPType");

        settings.Consumables.BuyReturnScrolls = g_bridge.ExtractBool(json, "BuyReturnScrolls");
        settings.Consumables.ReturnScrollRefillAmount = g_bridge.ExtractInt(json, "ReturnScrollRefillAmount");
        settings.Consumables.ReturnScrollType = (ScrollType)g_bridge.ExtractInt(json, "ReturnScrollType");

        settings.Consumables.BuyVigorPotions = g_bridge.ExtractBool(json, "BuyVigorPotions");
        settings.Consumables.VigorPotionRefillAmount = g_bridge.ExtractInt(json, "VigorPotionRefillAmount");
        settings.Consumables.VigorPotionReturnThreshold = g_bridge.ExtractInt(json, "VigorPotionReturnThreshold");

        settings.Consumables.BuyUniversalPills = g_bridge.ExtractBool(json, "BuyUniversalPills");
        settings.Consumables.UniversalPillsRefillAmount = g_bridge.ExtractInt(json, "UniversalPillsRefillAmount");
        settings.Consumables.UniversalPillsReturnThreshold = g_bridge.ExtractInt(json, "UniversalPillsReturnThreshold");
        settings.Consumables.UniPillType = (UniversalPillType)g_bridge.ExtractInt(json, "UniPillType");

        settings.Consumables.BuyPurifPills = g_bridge.ExtractBool(json, "BuyPurifPills");
        settings.Consumables.PurifPillsRefillAmount = g_bridge.ExtractInt(json, "PurifPillsRefillAmount");
        settings.Consumables.PurifPillsReturnThreshold = g_bridge.ExtractInt(json, "PurifPillsReturnThreshold");
        settings.Consumables.PurificationPillType = (UniversalPillType)g_bridge.ExtractInt(json, "PurificationPillType");

        settings.Consumables.BuySpeedDrugs = g_bridge.ExtractBool(json, "BuySpeedDrugs");
        settings.Consumables.SpeedDrugsRefillAmount = g_bridge.ExtractInt(json, "SpeedDrugsRefillAmount");
        settings.Consumables.SpeedDrugsReturnThreshold = g_bridge.ExtractInt(json, "SpeedDrugsReturnThreshold");
        settings.Consumables.DrugType = (SpeedDrugsType)g_bridge.ExtractInt(json, "DrugType");

        settings.Consumables.BuyAmmo = g_bridge.ExtractBool(json, "BuyAmmo");
        settings.Consumables.AmmoRefillAmount = g_bridge.ExtractInt(json, "AmmoRefillAmount");
        settings.Consumables.AmmoReturnThreshold = g_bridge.ExtractInt(json, "AmmoReturnThreshold");
        settings.Consumables.AmmoType = (AmmunitionType)g_bridge.ExtractInt(json, "AmmoType");

        // AUTO POTION CATEGORY
        settings.AutoPotion.AutoUseHP = g_bridge.ExtractBool(json, "AutoUseHP");
        settings.AutoPotion.AutoUseMP = g_bridge.ExtractBool(json, "AutoUseMP");
        settings.AutoPotion.UseVigorPotions = g_bridge.ExtractBool(json, "UseVigorPotions");
        settings.AutoPotion.HPPotHealthThreshold = g_bridge.ExtractInt(json, "HPPotHealthThreshold");
        settings.AutoPotion.MPPotManaThreshold = g_bridge.ExtractInt(json, "MPPotManaThreshold");
        settings.AutoPotion.VigorHPMPThreshold = g_bridge.ExtractInt(json, "VigorHPMPThreshold");
        settings.AutoPotion.PreferVigorFirst = g_bridge.ExtractBool(json, "PreferVigorFirst");
        settings.AutoPotion.HPDelay = g_bridge.ExtractInt(json, "HPDelay");
        settings.AutoPotion.MPDelay = g_bridge.ExtractInt(json, "MPDelay");
        settings.AutoPotion.HealPets = g_bridge.ExtractBool(json, "HealPets");
        settings.AutoPotion.HealPetHPThreshold = g_bridge.ExtractInt(json, "HealPetHPThreshold");
        settings.AutoPotion.AutoUseContPills = g_bridge.ExtractBool(json, "AutoUseUnivPills"); // Note: maps to property name
        settings.AutoPotion.AutoUsePurifPills = g_bridge.ExtractBool(json, "AutoUsePurifPills");

        // MAINTENANCE CATEGORY
        settings.Maintenance.RepairWeapon = g_bridge.ExtractBool(json, "RepairWeapon");
        settings.Maintenance.RepairDurabilityThreshold = g_bridge.ExtractInt(json, "RepairDurabilityThreshold");

        // RECALL TRIGGERS
        settings.BackTownMonitor.ReturnIfDead = g_bridge.ExtractBool(json, "ReturnIfDead");
        settings.BackTownMonitor.ReturnIfInventoryFull = g_bridge.ExtractBool(json, "ReturnIfInventoryFull");

        // ATTACK & ZERK CATEGORY
        settings.Attack.IgnoreDimensionPillars = g_bridge.ExtractBool(json, "IgnoreDimensionPillars");
        settings.Attack.UseZerkRightAwayWhenFull = g_bridge.ExtractBool(json, "UseZerkRightAwayWhenFull");
        settings.Attack.UseZerkOnNormalGiants = g_bridge.ExtractBool(json, "UseZerkOnNormalGiants");
        settings.Attack.UseZerkOnPartyMobs = g_bridge.ExtractBool(json, "UseZerkOnPartyMobs");
        settings.Attack.UseZerkOnPartyGiants = g_bridge.ExtractBool(json, "UseZerkOnPartyGiants");
        settings.Attack.UseZerkOnUniques = g_bridge.ExtractBool(json, "UseZerkOnUniques");
        settings.Attack.UseZerkIfNMobsAttackingSimulataneously = g_bridge.ExtractBool(json, "UseZerkIfNMobsAttackingSimulataneously");
        settings.Attack.ZerkMobCount = g_bridge.ExtractInt(json, "ZerkMobCount");

        // LOOT & WALKING BUFFS
        settings.Pickup.PickGold = g_bridge.ExtractBool(json, "PickGold");
        settings.Pickup.PickAll = g_bridge.ExtractBool(json, "PickAll");
        settings.Pickup.PickAmmoIfAmountLowerThan = g_bridge.ExtractBool(json, "PickAmmoIfAmountLowerThan");

        settings.Autowalker.CastSpeedBuffWhileWalking = g_bridge.ExtractBool(json, "CastSpeedBuffWhileWalking");
        settings.Autowalker.CastNoiseBuffWhileWalking = g_bridge.ExtractBool(json, "CastNoiseBuffWhileWalking");
        });
}

void Control::Initialize()
{
    auto& log = GetLogger();

    log.Init();
    Settings::Load();

    {
        char exePath[MAX_PATH];
        GetModuleFileNameA(NULL, exePath, MAX_PATH);
        std::string dir(exePath);
        dir = dir.substr(0, dir.find_last_of("\\/"));
        std::wstring pk2Path(dir.begin(), dir.end());
        pk2Path += L"\\media.pk2";

        Pk2Reader pk2;
        std::vector<uint8_t> divData;
        if (pk2.Open(pk2Path) && pk2.ReadFile("DIVISIONINFO.TXT", divData))
        {
            g_bridgeHost = ExtractHostFromDivisionInfo(divData);
            log.Info("Control::Initialize", "Bridge host: " + g_bridgeHost);
        }
        else
        {
            log.Err("Control::Initialize", "Failed to read DIVISIONINFO.TXT from media.pk2 — bridge will not connect");
        }
    }

    dx9_hook::init();
    log.Info("Control::Initialize", "Initialzed d3d9_hook.");

    RegisterAllHandlers();
    log.Info("Control::Initialize", "Registered g_bridge handlers.");
    Patcher::PatchAll();

}

