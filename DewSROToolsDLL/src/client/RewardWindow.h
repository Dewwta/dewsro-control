#pragma once
#include <string>
#include <vector>
#include "../Logging/Logger.h"
#include <map>
#include <d3d9.h>
#include <d3dx9.h>
#include "SoxOverlay.h"

struct RewardOption
{
    std::string code;
    std::string name;
    std::string icon; 
    int plus = 0;
    int qty = 1;
};

struct RewardWindow
{

    bool isOpen = false;
    int  level = 0;
    std::vector<RewardOption> options;
    std::map<std::string, IDirect3DTexture9*> iconCache;
    IDirect3DDevice9* device = nullptr;
    int selectedIndex = -1;

    static bool Contains(const std::string& str, const std::string& sub) {
        return str.find(sub) != std::string::npos;
    }

    enum class SealType { None, Star, Moon, Sun };

    static SealType GetSealType(const std::string& code) {
        if (!Contains(code, "_RARE")) return SealType::None;
        if (Contains(code, "_A_RARE")) return SealType::Star;
        if (Contains(code, "_B_RARE")) return SealType::Moon;
        if (Contains(code, "_C_RARE")) return SealType::Sun;
        return SealType::None;
    }

    IDirect3DTexture9* GetIcon(const std::string& path) {
        if (path.empty()) return nullptr;
        auto it = iconCache.find(path);
        if (it != iconCache.end()) return it->second;

        // build full path from client root
        std::string fullPath = "icon/" + path;
        IDirect3DTexture9* tex = nullptr;
        D3DXCreateTextureFromFileA(device, fullPath.c_str(), &tex);
        iconCache[path] = tex;
        return tex;
    }

    void ReleaseIcons() {
        for (auto& [k, v] : iconCache)
            if (v) v->Release();
        iconCache.clear();
    }

    void Open(int lvl, std::vector<RewardOption> opts) {
        auto& log = GetLogger();
        log.Dbg("RewardWindow::Open", "Opening for level " + std::to_string(lvl));
        
        level = lvl;
        options = std::move(opts);
        selectedIndex = -1;
        isOpen = true;
    }

    void Close() {
        auto& log = GetLogger();
        log.Dbg("RewardWindow::Close", "Close() called");
        isOpen = false;
        level = 0;
        options.clear();
    }

    void Render();
};

extern RewardWindow g_rewardWindow;