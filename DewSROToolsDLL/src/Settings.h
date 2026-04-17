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
        showOnKey = GetPrivateProfileIntA("General", "ShowOnKey", 'Z', p);
    }

    static void Save()
    {
        std::string path = GetSettingsPath();
        const char* p = path.c_str();

        WritePrivateProfileStringA("General", "KeepFocused", keepFocused ? "true" : "false", p);
        WritePrivateProfileStringA("General", "ShowOnKey", std::to_string(showOnKey).c_str(), p);
    }

    static bool keepFocused;
    static int  showOnKey;

private:
    static std::string GetSettingsPath()
    {
        char buf[MAX_PATH];
        GetModuleFileNameA(NULL, buf, MAX_PATH);

        std::string path(buf);
        // strip exe filename, keep directory
        path = path.substr(0, path.find_last_of("\\/"));
        path += "\\VSRO_Tools\\settings.ini";

        // create folder if it doesn't exist
        std::string folder = path.substr(0, path.find_last_of("\\/"));
        CreateDirectoryA(folder.c_str(), NULL);

        return path;
    }
};

inline bool Settings::keepFocused = true;
inline int  Settings::showOnKey = 'Z';