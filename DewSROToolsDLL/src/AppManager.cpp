#include "AppManager.h"
#include "hooks/dx9_hook.h"
#include "Windows.h"

const bool Debug = false;
void AppManager::Initialize()
{
    if (Debug) {
       AllocConsole();
    }
    dx9_hook::init();
}