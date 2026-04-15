#include "Control.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"

void RegisterAllHandlers() {
    g_bridge.RegisterHandler("login_ack", [](const std::string& _) {
        std::cout << "Bridge connected. Waiting for ack..." << std::endl;
    });

    g_bridge.RegisterHandler("session_init", [](const std::string& json) {
        g_bridge.m_state.charName = g_bridge.ExtractStr(json, "charName");
        g_bridge.m_state.accJID = g_bridge.ExtractInt(json, "jid");
        g_bridge.m_state.accName = g_bridge.ExtractStr(json, "accName");

        std::cout << "Character loaded: " << g_bridge.m_state.charName << std::endl;
    });

    g_bridge.RegisterHandler("char_init", [](const std::string& json) {
        g_bridge.m_state.hp = g_bridge.ExtractInt(json, "hp");
        g_bridge.m_state.mp = g_bridge.ExtractInt(json, "mp");
        g_bridge.m_state.sessionKills = g_bridge.ExtractInt(json, "sessionKills");
        g_bridge.m_state.unusedStatPoints = g_bridge.ExtractInt(json, "unusedStatPoints");
        g_bridge.m_state.currentLevel = g_bridge.ExtractInt(json, "currentLevel");
        g_bridge.m_state.gold = g_bridge.ExtractUint64(json, "gold");
        
    });

    g_bridge.RegisterHandler("stat_init", [](const std::string& json) {
        g_bridge.m_state.maxHp = g_bridge.ExtractInt(json, "maxHp");
        g_bridge.m_state.maxMp = g_bridge.ExtractInt(json, "maxMp");
        g_bridge.m_state.strength = g_bridge.ExtractInt(json, "strength");
        g_bridge.m_state.intelligence = g_bridge.ExtractInt(json, "intelligence");
    });

}

const bool Debug = true;
void Control::Initialize()
{
    if (Debug) {
       AllocConsole();
       FILE* f;
       freopen_s(&f, "CONOUT$", "w", stdout);
       freopen_s(&f, "CONOUT$", "w", stderr);
       freopen_s(&f, "CONIN$", "r", stdin);
    }
    dx9_hook::init();

    RegisterAllHandlers();

}

