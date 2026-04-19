#pragma once
#include <Windows.h>
#include <string>

class Settings
{
public:
    static void Load()
    {
        std::string path = GetSettingsPath();
        const char* p = path.c_str();

        keepFocused = GetPrivateProfileIntA("General", "KeepFocused", 1, p) != 0;
        showFPSCounter = GetPrivateProfileIntA("General", "showFPSCounter", 1, p) != 0;
        showWatermark = GetPrivateProfileIntA("General", "showWatermark", 1, p) != 0;

        char paKeyBuf[8] = { 0 };
        GetPrivateProfileStringA("General", "PlayerActions_Keybind", "Z", paKeyBuf, sizeof(paKeyBuf), p);
        showPlayerActionsKey = paKeyBuf[0];
        
        char settingsKeyBuf[8] = { 0 };
        GetPrivateProfileStringA("General", "Settings_Keybind", "F", settingsKeyBuf, sizeof(settingsKeyBuf), p);
        showSettingsKey = settingsKeyBuf[0];

        Settings::Save();

    }

    static void Save()
    {
        std::string path = GetSettingsPath();
        const char* p = path.c_str();

        WritePrivateProfileStringA("General", "KeepFocused", keepFocused ? "true" : "false", p);
        WritePrivateProfileStringA("General", "showFPSCounter", showFPSCounter ? "true" : "false", p);
        WritePrivateProfileStringA("General", "showWatermark", showWatermark ? "true" : "false", p);

        char paKey[2] = { (char)showPlayerActionsKey, '\0' };
        WritePrivateProfileStringA("General", "PlayerActions_Keybind", paKey, p);

        char settingsKey[2] = { (char)showSettingsKey, '\0' };
        WritePrivateProfileStringA("General", "Settings_Keybind", settingsKey, p);
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