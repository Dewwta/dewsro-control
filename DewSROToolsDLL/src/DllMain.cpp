#include "DllMain.h"
#include "AppManager.h"

static DWORD WINAPI InitThreadProc(LPVOID)
{
    Control::Initialize();
    return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        CreateThread(nullptr, 0, InitThreadProc, nullptr, 0, nullptr);
        break;
    }
    return TRUE;
}