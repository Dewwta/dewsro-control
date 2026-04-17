#pragma once
#include <windows.h>
#include <MinHook.h>
#include "DllBridge.h"
#include "Logging/Logger.h"

typedef bool(__thiscall* LoginFn)(void*, LPCSTR, LPCSTR, char, int);
static LoginFn o_Login = nullptr;

// MSVC x86
static bool __fastcall hk_Login(void* thisPtr, void* edx,
    LPCSTR username, LPCSTR password, char shardId, int a5)
{
    auto& log = GetLogger();
    if (username) {
        g_bridge.SetIdentity(username);
        g_bridge.Connect();
        LPCSTR connMsg = "Connecting with ";
        std::string msg = std::string(connMsg).append(username).c_str();
        log.Info("login_hook", msg);
    }
    else {
        log.Err("login_hook", "Couldn't retrieve username for dll bridge connection!");
    }

    return o_Login(thisPtr, username, password, shardId, a5);
}

inline void InstallLoginHook() {
    MH_CreateHook((LPVOID)0x004C9290, &hk_Login, (LPVOID*)&o_Login);
    MH_EnableHook((LPVOID)0x004C9290);
}