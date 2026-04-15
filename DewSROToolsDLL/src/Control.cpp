#include "Control.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"

void RegisterAllHandlers() {
    g_bridge.RegisterHandler("loginAck", [](const std::string&) {
        std::cout << "Bridge connected. Waiting for ack..." << std::endl;
    });

    g_bridge.RegisterHandler("session_init", [](const std::string& json) {
        g_bridge.m_state.charName = g_bridge.ExtractStr(json, "charName");
        g_bridge.m_state.hp = g_bridge.ExtractInt(json, "hp");

        g_bridge.m_state.valid = true;
        std::cout << "Character loaded: " << g_bridge.m_state.charName << std::endl;
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

