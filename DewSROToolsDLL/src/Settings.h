#pragma once
#include <Windows.h>
#include <string>

class Settings
{
public:
    static std::string VKToString(int vk)
    {
        if (vk >= 'A' && vk <= 'Z')
            return std::string(1, (char)vk);

        if (vk >= VK_F1 && vk <= VK_F12)
            return "F" + std::to_string(vk - VK_F1 + 1);

        switch (vk)
        {
        case VK_INSERT: return "INSERT";
        case VK_DELETE: return "DELETE";
        case VK_HOME: return "HOME";
        case VK_END: return "END";
        case VK_PRIOR: return "PAGEUP";
        case VK_NEXT: return "PAGEDOWN";

        case VK_SHIFT: return "SHIFT";
        case VK_CONTROL: return "CTRL";
        case VK_MENU: return "ALT";

        case VK_LBUTTON: return "MOUSE1";
        case VK_RBUTTON: return "MOUSE2";
        case VK_MBUTTON: return "MOUSE3";
        case VK_XBUTTON1: return "MOUSE4";
        case VK_XBUTTON2: return "MOUSE5";
        }

        return "Unknown";
    }
    static int StringToVK(std::string key)
    {
        // normalize to uppercase
        for (auto& c : key)
            c = toupper(c);

        if (key.length() == 1)
            return key[0];

        if (key == "F1") return VK_F1;
        if (key == "F2") return VK_F2;
        if (key == "F3") return VK_F3;
        if (key == "F4") return VK_F4;
        if (key == "F5") return VK_F5;
        if (key == "F6") return VK_F6;
        if (key == "F7") return VK_F7;
        if (key == "F8") return VK_F8;
        if (key == "F9") return VK_F9;
        if (key == "F10") return VK_F10;
        if (key == "F11") return VK_F11;
        if (key == "F12") return VK_F12;

        if (key == "INSERT") return VK_INSERT;
        if (key == "DELETE") return VK_DELETE;
        if (key == "HOME") return VK_HOME;
        if (key == "END") return VK_END;
        if (key == "PAGEUP") return VK_PRIOR;
        if (key == "PAGEDOWN") return VK_NEXT;

        if (key == "SHIFT") return VK_SHIFT;
        if (key == "CTRL") return VK_CONTROL;
        if (key == "ALT") return VK_MENU;

        if (key == "MOUSE1") return VK_LBUTTON;
        if (key == "MOUSE2") return VK_RBUTTON;
        if (key == "MOUSE3") return VK_MBUTTON;
        if (key == "MOUSE4") return VK_XBUTTON1;
        if (key == "MOUSE5") return VK_XBUTTON2;

        return 0;
    }
    static void Load()
    {
        std::string path = GetSettingsPath();
        const char* p = path.c_str();

        keepFocused = GetPrivateProfileIntA("General", "KeepFocused", 1, p) != 0;
        showFPSCounter = GetPrivateProfileIntA("General", "showFPSCounter", 1, p) != 0;
        showWatermark = GetPrivateProfileIntA("General", "showWatermark", 1, p) != 0;

        char paKeyBuf[8] = { 0 };
        GetPrivateProfileStringA("General", "PlayerActions_Keybind", "F", paKeyBuf, sizeof(paKeyBuf), p);
        showPlayerActionsKey = StringToVK(paKeyBuf);
        
        char settingsKeyBuf[8] = { 0 };
        GetPrivateProfileStringA("General", "Settings_Keybind", "Z", settingsKeyBuf, sizeof(settingsKeyBuf), p);
        showSettingsKey = StringToVK(settingsKeyBuf);

        Settings::Save();

    }

    static void Save()
    {
        std::string path = GetSettingsPath();
        const char* p = path.c_str();

        WritePrivateProfileStringA("General", "KeepFocused", keepFocused ? "1" : "0", p);
        WritePrivateProfileStringA("General", "showFPSCounter", showFPSCounter ? "1" : "0", p);
        WritePrivateProfileStringA("General", "showWatermark", showWatermark ? "1" : "0", p);

        WritePrivateProfileStringA("General", "PlayerActions_Keybind",
            VKToString(showPlayerActionsKey).c_str(), p);

        WritePrivateProfileStringA("General", "Settings_Keybind",
            VKToString(showSettingsKey).c_str(), p);
    }

    static bool keepFocused;
    static bool showFPSCounter;
    static bool showWatermark;
    static int  showPlayerActionsKey;
    static int  showSettingsKey;

private:
    static std::string GetSettingsPath()
    {
        char buf[MAX_PATH];
        GetModuleFileNameA(NULL, buf, MAX_PATH);

        std::string path(buf);
        // strip exe filename
        path = path.substr(0, path.find_last_of("\\/"));
        path += "\\VSRO_Tools\\settings.ini";

        std::string folder = path.substr(0, path.find_last_of("\\/"));
        CreateDirectoryA(folder.c_str(), NULL);

        return path;
    }
};

inline bool Settings::keepFocused = true;
inline bool Settings::showFPSCounter = true;
inline bool Settings::showWatermark = true;
inline int  Settings::showPlayerActionsKey = 'Z';
inline int  Settings::showSettingsKey = 'F';