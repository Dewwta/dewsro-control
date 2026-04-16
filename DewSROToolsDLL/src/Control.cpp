#include "Control.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"
#include "Logging/Logger.h"

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
        auto& log = GetLogger();
        
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
}

