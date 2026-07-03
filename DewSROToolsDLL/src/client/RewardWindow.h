#pragma once
#include <string>
#include <vector>
#include "../Logging/Logger.h"
#include <d3d9.h>
#include "pk2/IconCache.h"
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
    IconCache* iconSource = nullptr;

    // selected[0] is the oldest pick; a third click replaces it (FIFO).
    static constexpr int K_PICKS = 2;
    int selected[K_PICKS] = { -1, -1 };

    bool IsSelected(int i) const {
        return i == selected[0] || i == selected[1];
    }
    int SelectedCount() const {
        return (selected[0] >= 0 ? 1 : 0) + (selected[1] >= 0 ? 1 : 0);
    }
    void ToggleSelect(int i) {
        if (i == selected[0]) { selected[0] = selected[1]; selected[1] = -1; return; }
        if (i == selected[1]) { selected[1] = -1; return; }
        if      (selected[0] < 0) selected[0] = i;
        else if (selected[1] < 0) selected[1] = i;
        else { selected[0] = selected[1]; selected[1] = i; } // replace oldest
    }

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
        if (path.empty() || !iconSource) return nullptr;
        std::string base = path;
        size_t dot = base.rfind('.');
        if (dot != std::string::npos) base = base.substr(0, dot);
        return iconSource->Get("icon/" + base + ".ddj");
    }

    void Open(int lvl, std::vector<RewardOption> opts) {
        auto& log = GetLogger();
        log.Dbg("RewardWindow::Open", "Opening for level " + std::to_string(lvl));
        
        level = lvl;
        options = std::move(opts);
        selected[0] = selected[1] = -1;
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