#pragma once
#include <windows.h>
#include <MinHook.h>
#include "DllBridge.h"
#include <iostream>

typedef bool(__thiscall* LoginFn)(void*, LPCSTR, LPCSTR, char, int);
static LoginFn o_Login = nullptr;

// MSVC x86
static bool __fastcall hk_Login(void* thisPtr, void* edx,
    LPCSTR username, LPCSTR password, char shardId, int a5)
{
    if (username) {
        g_bridge.SetIdentity(username);
        g_bridge.Connect();
        std::cout << "Connecting to proxy...\nUsername: " << username << std::endl;

    }
    else {
        std::cout << "Didnt get username! proxy bridge failed" << std::endl;
    }
    return o_Login(thisPtr, username, password, shardId, a5);
}

inline void InstallLoginHook() {
    MH_CreateHook((LPVOID)0x004C9290, &hk_Login, (LPVOID*)&o_Login);
    MH_EnableHook((LPVOID)0x004C9290);
}