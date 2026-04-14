#pragma once
#include <windows.h>
#include <MinHook.h>
#include "DllBridge.h"
#include "../hooks/dx9_hook.h"

typedef bool(__thiscall* LoginFn)(void*, LPCSTR, LPCSTR, char, int);
static LoginFn o_Login = nullptr;

// __fastcall with dummy edx = correct way to hook __thiscall in MSVC x86
static bool __fastcall hk_Login(void* thisPtr, void* edx,
    LPCSTR username, LPCSTR password, char shardId, int a5)
{
    if (username) {
        g_bridge.SetIdentity(username);
        g_bridge.Connect();
        
    }

    return o_Login(thisPtr, username, password, shardId, a5);
}

inline void InstallLoginHook() {
    MH_CreateHook((LPVOID)0x004C9290, &hk_Login, (LPVOID*)&o_Login);
    MH_EnableHook((LPVOID)0x004C9290);
}