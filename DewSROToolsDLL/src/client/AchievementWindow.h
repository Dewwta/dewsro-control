#pragma once
#include <string>
#include <vector>
#include "imgui.h"

struct AchievementEntry
{
    std::string name;
    std::string desc;
    std::string type;
    long long   goal = 1;
    long long   progress = 0;
    bool        completed = false;
    std::string completedAt;
};

struct AchievementWindow
{
    bool isOpen = false;
    std::vector<AchievementEntry> entries;

    // Detail panel state
    bool        showDetail = false;
    int         detailIndex = -1;

    static constexpr ImVec4 BAR_BG = { 0.10f, 0.18f, 0.35f, 1.0f }; // blue
    static constexpr ImVec4 BAR_FILL = { 0.55f, 0.85f, 0.55f, 1.0f }; 
    static constexpr ImVec4 COL_DONE = { 0.4f,  1.0f,  0.4f,  1.0f };
    static constexpr ImVec4 COL_WIP = { 0.75f, 0.88f, 1.0f,  1.0f };
    static constexpr ImVec4 COL_LOCK = { 0.40f, 0.40f, 0.45f, 1.0f };

    void Open(std::vector<AchievementEntry> e) {
        entries = std::move(e);
        showDetail = false;
        detailIndex = -1;
        isOpen = true;
    }

    void Close() {
        isOpen = false;
        showDetail = false;
        detailIndex = -1;
        entries.clear();
    }

    void Render();
};

extern AchievementWindow g_achWindow;