#include "Control.h"
#include "hooks/dx9_hook.h"
#include "hooks/patcher.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"
#include "Logging/Logger.h"
#include "client/RewardWindow.h"
#include <sstream>

void RegisterAllHandlers() {
    g_bridge.RegisterHandler("login_ack", [](const std::string& _) {
        auto& log = GetLogger();
        log.Info("Control_Handler::login_ack", "Proxy connection acknowledged");
    });

    g_bridge.RegisterHandler("session_init", [](const std::string& json) {
        auto& log = GetLogger();
        g_bridge.m_state.charName = g_bridge.ExtractStr(json, "charName");
        g_bridge.m_state.accJID = g_bridge.ExtractInt(json, "jid");
        g_bridge.m_state.accName = g_bridge.ExtractStr(json, "accName");
        
        log.Info("Control_Handler::session_init", "Character loaded: " + g_bridge.m_state.charName);
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
        auto& log = GetLogger();
        g_bridge.m_state.maxHp = g_bridge.ExtractInt(json, "maxHp");
        g_bridge.m_state.maxMp = g_bridge.ExtractInt(json, "maxMp");
        g_bridge.m_state.strength = g_bridge.ExtractInt(json, "strength");
        g_bridge.m_state.intelligence = g_bridge.ExtractInt(json, "intelligence");
        log.Info("Control_Handler::stat_init", "Stats received.");
    });

    g_bridge.RegisterHandler("session_sync", [](const std::string& json) {
        g_bridge.m_sessionState.sessionSeconds = g_bridge.ExtractInt(json, "sessionSeconds");
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
        auto& log = GetLogger();

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
                log.Dbg("level_reward", "icon path: '" + opt.icon + "'");
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
        auto& log = GetLogger();
        log.Info("Control_Handler::session_clear", "Session cleared");
        g_bridge.ClearSession();
    });

}

void Control::Initialize()
{
    auto& log = GetLogger();

    log.Alloc();
    log.SetState(true);

    dx9_hook::init();
    
    log.Info("Control::Initialize", "Initialzed d3d9_hook.");

    RegisterAllHandlers();
    log.Info("Control::Initialize", "Registered g_bridge handlers.");
    Patcher::PatchAll();

}

