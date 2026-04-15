#include "AppManager.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"
#include <iostream>
#include "net/DllBridge.h"

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
    // TODO: register handlers
}