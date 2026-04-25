#include "Control.h"
#include "hooks/dx9_hook.h"
#include "hooks/patcher.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"
#include "Logging/Logger.h"
#include "client/RewardWindow.h"
#include <sstream>
#include "Settings.h"
#include "client/AchievementWindow.h"

void RegisterAllHandlers() {
    g_bridge.RegisterHandler("login_ack", [](const std::string& _) {
        auto& log = GetLogger();
        log.Info("Control_Handler::login_ack", "Proxy connection acknowledged");
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
        g_bridge.m_sessionState.syncTick = GetTickCount();
    });

    g_bridge.RegisterHandler("kill_update", [](const std::string& json) {
        g_bridge.m_sessionState.sessionKills = g_bridge.ExtractInt(json, "sessionKills");
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
}

void Control::Initialize()
{
    auto& log = GetLogger();

    log.Alloc();
    log.SetState(false);
    Settings::Load();
    dx9_hook::init();
    log.Info("Control::Initialize", "Initialzed d3d9_hook.");

    RegisterAllHandlers();
    log.Info("Control::Initialize", "Registered g_bridge handlers.");
    Patcher::PatchAll();

}

