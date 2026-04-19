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
        log.Info("login_hook", std::string("Connecting with ").append(username));
        g_bridge.Reconnect(username);
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