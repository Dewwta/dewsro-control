#include "AppManager.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"
#include <iostream>

const bool Debug = true;
void AppManager::Initialize()
{
    if (Debug) {
       AllocConsole();
       FILE* f;
       freopen_s(&f, "CONOUT$", "w", stdout);
       freopen_s(&f, "CONOUT$", "w", stderr);
       freopen_s(&f, "CONIN$", "r", stdin);
    }
    dx9_hook::init();

}