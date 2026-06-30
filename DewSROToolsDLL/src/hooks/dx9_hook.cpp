#include "dx9_hook.h"
// ImGui
#include "imgui.h"
#include "imgui_impl_dx9.h"
#include "imgui_impl_win32.h"
#include "imgui_internal.h"
// Hook
#include <d3d9.h>
#include <MinHook.h>
#include <iostream>
// Internal
#include "../Settings.h"
#include "../net/NetActions.h"
#include "../net/LoginHook.h"
#include "Logging/Logger.h"
#include "../client/RewardWindow.h"
#include "../client/AchievementWindow.h"
#include <Windows.h>
#include <fstream>
#include <sstream>
#include <string>
#include <iostream>
#include "../mem/Process.h"
#include "../client/pk2/Pk2Reader.h"
#include "../client/pk2/IconCache.h"
#include "../client/SROSkinWindow.h"

static Pk2Reader  g_pk2;       // media.pk2
static Pk2Reader  g_dataPk2;   // data.pk2  (sounds, prim resources, etc.)
static IconCache* g_iconCache = nullptr;

#pragma region - Region Loading/Saving -

void SaveRegionData(State& ss)
{
    if (strlen(ss.regionName) == 0) return;

    std::string filename = std::string(ss.regionName) + "_data.txt";
    std::ofstream file(filename);
    if (!file.is_open()) return;

    for (const auto& n : ss.g_recordedNodes)
        file << "NODE " << n.id << " " << n.x << " " << n.y << "\n";

    for (const auto& e : ss.g_recordedEdges)
        file << "EDGE " << e.fromId << " " << e.toId << "\n";

    file.close();
}
void LoadRegionData(State& ss)
{
    if (strlen(ss.regionName) == 0) return;

    std::string filename = std::string(ss.regionName) + "_data.txt";
    std::ifstream file(filename);
    if (!file.is_open()) return; // File doesn't exist yet, that's fine

    ss.g_recordedNodes.clear();
    ss.g_recordedEdges.clear();
    ss.activeNode = "";
    int highestNode = -1;

    std::string line;
    while (std::getline(file, line))
    {
        std::istringstream iss(line);
        std::string type;
        iss >> type;

        if (type == "NODE")
        {
            std::string id;
            int x, y;
            if (iss >> id >> x >> y)
            {
                ss.g_recordedNodes.push_back({ id, x, y });

                // Parse the number out of "wp_X" to update the counter
                if (id.length() > 3) {
                    int num = std::stoi(id.substr(3));
                    if (num > highestNode) highestNode = num;
                }
            }
        }
        else if (type == "EDGE")
        {
            std::string from, to;
            if (iss >> from >> to)
            {
                ss.g_recordedEdges.push_back({ from, to });
            }
        }
    }

    // Restore state counters
    ss.nodeCounter = highestNode + 1;
    ss.totalRecorded = ss.g_recordedNodes.size();
    if (!ss.g_recordedNodes.empty())
    {
        ss.activeNode = ss.g_recordedNodes.back().id; // Default active to the last loaded node
    }
}

// nodeCounter must stay above every surviving id, otherwise new nodes reuse deleted ids.
static void RecomputeGraphCounters(State& ss)
{
    int highestNode = -1;
    for (const auto& n : ss.g_recordedNodes)
    {
        if (n.id.length() > 3)
        {
            int num = atoi(n.id.c_str() + 3);
            if (num > highestNode) highestNode = num;
        }
    }
    ss.nodeCounter = highestNode + 1;
    ss.totalRecorded = (int)ss.g_recordedNodes.size();
}

static void DeleteGraphNode(State& ss, std::string id)
{
    auto& nodes = ss.g_recordedNodes;
    auto& edges = ss.g_recordedEdges;

    nodes.erase(
        std::remove_if(nodes.begin(), nodes.end(),
            [&](const NavNode& n) { return n.id == id; }),
        nodes.end());

    edges.erase(
        std::remove_if(edges.begin(), edges.end(),
            [&](const NavEdge& e) { return e.fromId == id || e.toId == id; }),
        edges.end());

    if (ss.activeNode == id)
        ss.activeNode = nodes.empty() ? "" : nodes.back().id;

    RecomputeGraphCounters(ss);
    SaveRegionData(ss);
}

static void DeleteGraphEdge(State& ss, size_t index)
{
    if (index >= ss.g_recordedEdges.size()) return;
    ss.g_recordedEdges.erase(ss.g_recordedEdges.begin() + index);
    SaveRegionData(ss);
}

#pragma endregion

#pragma region - Tile Logic -

static LPDIRECT3DTEXTURE9 LoadDDJFromPk2(IDirect3DDevice9* device, const char* archivePath)
{
    static const size_t DDJ_HEADER = 20;
    static const uint32_t DDS_MAGIC = 0x20534444;

    std::vector<uint8_t> ddj;
    if (!g_pk2.ReadFile(archivePath, ddj) || ddj.size() <= DDJ_HEADER)
        return nullptr;

    const uint8_t* ddsData = ddj.data() + DDJ_HEADER;
    size_t ddsSize = ddj.size() - DDJ_HEADER;

    if (ddsSize < 4 || *reinterpret_cast<const uint32_t*>(ddsData) != DDS_MAGIC)
    {
        bool found = false;
        for (size_t off = 0; off + 4 <= ddj.size() && off <= 64; ++off)
        {
            if (*reinterpret_cast<const uint32_t*>(ddj.data() + off) == DDS_MAGIC)
            {
                ddsData = ddj.data() + off;
                ddsSize = ddj.size() - off;
                found = true;
                break;
            }
        }
        if (!found) return nullptr;
    }

    LPDIRECT3DTEXTURE9 tex = nullptr;
    D3DXCreateTextureFromFileInMemoryEx(
        device, ddsData, (UINT)ddsSize,
        D3DX_DEFAULT_NONPOW2, D3DX_DEFAULT_NONPOW2,
        D3DX_FROM_FILE, 0, D3DFMT_FROM_FILE, D3DPOOL_MANAGED,
        D3DX_DEFAULT, D3DX_DEFAULT, 0, nullptr, nullptr, &tex);
    return tex;
}

static int TileDistance(
    int x1,
    int y1,
    int x2,
    int y2)
{
    return max(abs(x1 - x2), abs(y1 - y2));
}
struct MinimapTile
{
    LPDIRECT3DTEXTURE9 texture = nullptr;

    int sectorX = 0;
    int sectorY = 0;

    bool loaded = false;

    uint64_t lastUsedFrame = 0;
};

static uint64_t g_currentFrame = 0;
static std::unordered_map<std::string, MinimapTile> g_minimapTiles;

static std::string MakeTileKey(int sectorX, int sectorY,
    const std::string& prefix, int floor)
{
    // "qt_a01:2:126:127" or ":0:161:95" for overworld
    return prefix + ":" + std::to_string(floor) + ":" +
        std::to_string(sectorX) + ":" + std::to_string(sectorY);
}

static void CleanupMinimapTiles(int playerSectorX, int playerSectorY)
{
    const int UNLOAD_RADIUS = 9;

    for (auto it = g_minimapTiles.begin(); it != g_minimapTiles.end();)
    {
        auto& tile = it->second;
        int dist = TileDistance(tile.sectorX, tile.sectorY,
            playerSectorX, playerSectorY);
        if (dist > UNLOAD_RADIUS)
        {
            if (tile.texture) tile.texture->Release();
            it = g_minimapTiles.erase(it);
        }
        else ++it;
    }
}

static void DrawRadarTile(
    ImDrawList* drawList,
    MinimapTile* tile,
    int sectorX,
    int sectorY,
    int playerX,
    int playerY,
    float zoomScale,
    ImVec2 center,
    float offsetX,
    float offsetY,
    bool isDungeon)
{
    const int TILE_SIZE = 192;
    
    int worldMinX, worldMinY;
    if (isDungeon)
    {
        worldMinX = sectorX * TILE_SIZE;
        worldMinY = sectorY * TILE_SIZE;
    }
    else
    {
        worldMinX = (sectorX - 135) * TILE_SIZE;
        worldMinY = (sectorY - 92) * TILE_SIZE;
    }

    int worldMaxX = worldMinX + TILE_SIZE;
    int worldMaxY = worldMinY + TILE_SIZE;

    auto WorldToRadar = [&](int wx, int wy) -> ImVec2
        {
            float dx = (wx - playerX) * zoomScale;
            float dy = (wy - playerY) * zoomScale;
            return ImVec2(center.x + dx + offsetX, center.y - dy + offsetY);
        };

    ImVec2 p1 = WorldToRadar(worldMinX, worldMinY);
    ImVec2 p2 = WorldToRadar(worldMaxX, worldMaxY);
    drawList->AddImage(ImTextureRef((ImTextureID)(uintptr_t)tile->texture), p1, p2, ImVec2(0, 1), ImVec2(1, 0));
}

static MinimapTile* GetOrLoadTile(
    IDirect3DDevice9* device,
    int sectorX,
    int sectorY,
    const std::string& dungeonFolder = "",
    const std::string& dungeonPrefix = "",
    int floor = 0)
{
    std::string key = MakeTileKey(sectorX, sectorY, dungeonPrefix, floor);

    auto it = g_minimapTiles.find(key);
    if (it != g_minimapTiles.end())
    {
        it->second.lastUsedFrame = g_currentFrame;
        return &it->second;
    }

    char path[MAX_PATH];
    if (dungeonFolder.empty())
    {
        sprintf_s(path, "minimap\\%dx%d.ddj", sectorX, sectorY);
    }
    else
    {
        sprintf_s(path, "minimap_d\\%s\\%s_floor%02d_%dx%d.ddj",
            dungeonFolder.c_str(),
            dungeonPrefix.c_str(),
            floor,
            sectorX,
            sectorY);
    }

    LPDIRECT3DTEXTURE9 tex = LoadDDJFromPk2(device, path);

    MinimapTile tile;
    tile.texture = tex;
    tile.loaded = true;
    tile.sectorX = sectorX;
    tile.sectorY = sectorY;
    tile.lastUsedFrame = g_currentFrame;

    auto [insertedIt, success] = g_minimapTiles.emplace(key, std::move(tile));
    return tex ? &insertedIt->second : nullptr;
}

#pragma endregion

#pragma region - Constant -

const char* PotionTypes[] = { "Herb", "Small", "Medium", "Large", "XL", "XXL" };
const char* UniPillTypes[] = { "Small", "Medium", "Large", "Special Small" };
const char* PurifPillTypes[] = { "Small", "Medium", "Large", "XL" };
const char* SpeedDrugTypes[] = { "Drug Of Wind", "Drug Of Typhoon" };
const char* ScrollTypes[] = { "Normal", "Special" };
const char* AmmoTypes[] = { "Arrows", "Bolts" };
const char* RecKitTypes[] = { "Small", "Medium", "Large" };
const char* AbnormalPillTypes[] = { "Small", "Medium" };

#pragma endregion

static std::string FormatSeconds(int totalSeconds) {
    int h = totalSeconds / 3600;
    int m = (totalSeconds % 3600) / 60;
    int s = totalSeconds % 60;
    char buf[16];
    snprintf(buf, sizeof(buf), "%02d:%02d:%02d", h, m, s);
    return buf;
}
static int walkX = 0;
static int walkY = 0;
static int walkZ = 0;
SoxOverlay g_soxOverlay;
static bool initialized = false;
static ImFont* g_fontWatermark = nullptr;
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
struct DungeonRoomBounds
{
    char roomName[128] = {};

    int minX = 0;
    int maxX = 0;
    int minY = 0;
    int maxY = 0;
};

static bool g_roomRecorderEnabled = false;
static DungeonRoomBounds g_currentRoom;
static std::vector<DungeonRoomBounds> g_recordedRooms;
typedef HRESULT(__stdcall* Present_t)(IDirect3DDevice9*, CONST RECT*, CONST RECT*, HWND, CONST RGNDATA*);
static Present_t oPresent = nullptr;

static bool showSessionStatsWindow  = false;
static bool showAdminToolsWindow    = false;
static bool showSettingsWindow      = false;
static bool showAchWindow           = false;
static bool showBotWindow           = false;
static bool showLatticeInspector    = false;
static bool showSkinTest            = false;

static bool AnyWindowOpen() {
    return showSessionStatsWindow || showAdminToolsWindow || showSettingsWindow || g_rewardWindow.isOpen || g_achWindow.isOpen || showBotWindow;
}

static HWND g_gameHwnd = nullptr;

typedef IDirect3D9* (__stdcall* Direct3DCreate9_t)(UINT);
static Direct3DCreate9_t oCreate = nullptr;
typedef HRESULT(__stdcall* CreateDevice_t)(IDirect3D9*, UINT, D3DDEVTYPE, HWND, DWORD, D3DPRESENT_PARAMETERS*, IDirect3DDevice9**);
static CreateDevice_t oCreateDevice = nullptr;
typedef HRESULT(__stdcall* Reset_t)(IDirect3DDevice9*, D3DPRESENT_PARAMETERS*);
static Reset_t oReset = nullptr;

typedef HWND(WINAPI* GetForegroundWindow_t)();
typedef HWND(WINAPI* GetActiveWindow_t)();
static GetForegroundWindow_t oGetForegroundWindow = nullptr;
static GetActiveWindow_t oGetActiveWindow = nullptr;

HWND WINAPI hkGetForegroundWindow()
{
    if (Settings::keepFocused && g_gameHwnd)
        return g_gameHwnd;
    return oGetForegroundWindow();
}

HWND WINAPI hkGetActiveWindow()
{
    if (Settings::keepFocused && g_gameHwnd)
        return g_gameHwnd;
    return oGetActiveWindow();
}

static WNDPROC oWndProc = nullptr;

#pragma region - ImGui -

static void SetupImGuiStyle()
{
    ImGuiStyle& style = ImGui::GetStyle();
    // Rounding
    style.WindowRounding = 6.0f;
    style.FrameRounding = 4.0f;
    style.GrabRounding = 4.0f;
    style.PopupRounding = 4.0f;
    style.ScrollbarRounding = 4.0f;
    // Sizing
    style.WindowPadding = ImVec2(12, 12);
    style.FramePadding = ImVec2(8, 4);
    style.ItemSpacing = ImVec2(8, 6);
    style.WindowMinSize = ImVec2(220, 100);
    // Colors
    ImVec4* c = style.Colors;
    c[ImGuiCol_WindowBg] = ImVec4(0.08f, 0.10f, 0.13f, 0.95f);
    c[ImGuiCol_TitleBg] = ImVec4(0.05f, 0.07f, 0.10f, 1.00f);
    c[ImGuiCol_TitleBgActive] = ImVec4(0.08f, 0.14f, 0.24f, 1.00f);
    c[ImGuiCol_Separator] = ImVec4(0.20f, 0.28f, 0.38f, 1.00f);
    c[ImGuiCol_FrameBg] = ImVec4(0.10f, 0.14f, 0.20f, 1.00f);
    c[ImGuiCol_Button] = ImVec4(0.13f, 0.30f, 0.55f, 1.00f);
    c[ImGuiCol_ButtonHovered] = ImVec4(0.20f, 0.42f, 0.72f, 1.00f);
    c[ImGuiCol_ButtonActive] = ImVec4(0.08f, 0.20f, 0.45f, 1.00f);
    c[ImGuiCol_Header] = ImVec4(0.13f, 0.25f, 0.45f, 1.00f);
    c[ImGuiCol_HeaderHovered] = ImVec4(0.18f, 0.35f, 0.58f, 1.00f);
    c[ImGuiCol_Text] = ImVec4(0.85f, 0.90f, 1.00f, 1.00f);
    c[ImGuiCol_TextDisabled] = ImVec4(0.35f, 0.42f, 0.52f, 1.00f);
}

static void RenderWatermark(const char* text)
{
    ImGuiIO& io = ImGui::GetIO();
    ImGuiStyle& style = ImGui::GetStyle();
    if (g_fontWatermark) ImGui::PushFont(g_fontWatermark);
    float paddingX = 2.0f;
    float paddingY = 2.0f;
    ImVec2 textSize = ImGui::CalcTextSize(text);
    float windowWidth = textSize.x + style.WindowPadding.x * 2;
    float windowHeight = textSize.y + style.WindowPadding.y * 2;
    ImVec2 pos = ImVec2(
        io.DisplaySize.x - windowWidth - paddingX,
        io.DisplaySize.y - windowHeight - paddingY
    );
    if (g_fontWatermark) ImGui::PopFont(); // pop before Begin
    ImGui::SetNextWindowPos(pos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(ImVec2(windowWidth, windowHeight), ImGuiCond_Always);
    ImGui::SetNextWindowBgAlpha(0.0f);
    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoDecoration |
        ImGuiWindowFlags_NoInputs |
        ImGuiWindowFlags_NoMove |
        ImGuiWindowFlags_NoSavedSettings |
        ImGuiWindowFlags_NoFocusOnAppearing |
        ImGuiWindowFlags_NoNav |
        ImGuiWindowFlags_NoBackground;
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
    ImGui::Begin("##watermark", nullptr, flags);
    if (g_fontWatermark) ImGui::PushFont(g_fontWatermark);
    ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 0.8f), text);
    if (g_fontWatermark) ImGui::PopFont();
    ImGui::End();
    ImGui::PopStyleVar();
}

static void RenderFPS()
{
    ImGuiIO& io = ImGui::GetIO();
    ImGuiStyle& style = ImGui::GetStyle();
    float paddingX = 6.0f;
    float paddingY = 4.0f;
    float fps = io.Framerate;
    char buf[32];
    snprintf(buf, sizeof(buf), "FPS: %.0f", fps);
    ImVec2 textSize = ImGui::CalcTextSize(buf);
    float windowWidth = textSize.x + style.WindowPadding.x * 2;
    float windowHeight = textSize.y + style.WindowPadding.y * 2;

    ImVec2 pos = ImVec2(
        paddingX,
        io.DisplaySize.y - windowHeight - paddingY
    );
    ImGui::SetNextWindowPos(pos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(ImVec2(windowWidth, windowHeight), ImGuiCond_Always);
    ImGui::SetNextWindowBgAlpha(0.0f);
    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoDecoration |
        ImGuiWindowFlags_NoInputs |
        ImGuiWindowFlags_NoMove |
        ImGuiWindowFlags_NoSavedSettings |
        ImGuiWindowFlags_NoFocusOnAppearing |
        ImGuiWindowFlags_NoNav |
        ImGuiWindowFlags_NoBackground;
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
    ImGui::Begin("##fps", nullptr, flags);
    ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 0.8f), buf);
    ImGui::End();
    ImGui::PopStyleVar();
}

static void RenderSessionStats() {
    if (!BeginSROWindow("##session_stats", "Session Stats", &showSessionStatsWindow,
                        { 320.f, 500.f }, { 20.f, 20.f }, { 280.f, 200.f }, { 520.f, 720.f }))
        return;

    PlayerState ps = g_bridge.m_state;
    State& ss      = g_bridge.m_sessionState;

    if (ps.charName.empty())
    {
        ImGui::Spacing();
        ImGui::TextDisabled("Waiting for session...");
        EndSROWindow();
        return;
    }

    const float      labelCol = 80.0f;
    const ImVec4 clrValue(0.75f, 0.88f, 1.00f, 1.0f);
    const ImVec4 clrGreen(0.55f, 0.85f, 0.55f, 1.0f);
    const ImVec4 clrGold (1.00f, 0.85f, 0.30f, 1.0f);
    const ImVec4 clrRed  (0.90f, 0.55f, 0.55f, 1.0f);

    auto Row = [&](const char* label, ImVec4 color, const char* fmt, ...) {
        ImGui::TextDisabled("%s", label);
        ImGui::SameLine(labelCol);
        char buf[128];
        va_list args;
        va_start(args, fmt);
        vsnprintf(buf, sizeof(buf), fmt, args);
        va_end(args);
        ImGui::TextColored(color, "%s", buf);
    };

    // PLAYER
    ImGui::TextDisabled("PLAYER");
    ImGui::Separator();
    ImGui::Spacing();

    Row("Char",    clrValue, "%s", ps.charName.c_str());
    Row("Account", clrValue, "%s", ps.accName.c_str());
    Row("JID",     clrValue, "%d", ps.accJID);
    Row("Level",   clrValue, "%d", ps.currentLevel);

    ImGui::Spacing();

    int elapsed = (ss.syncTick > 0) ? (int)((GetTickCount() - ss.syncTick) / 1000) : 0;
    std::string sessionStr = FormatSeconds(ss.isAfk ? ss.sessionSeconds : ss.sessionSeconds + elapsed);
    std::string totalStr   = FormatSeconds(ss.totalSeconds + ss.sessionSeconds + (ss.isAfk ? 0 : elapsed));

    ImGui::TextDisabled("Session");
    ImGui::SameLine(labelCol);
    ImGui::TextColored(clrGreen, "%s", sessionStr.c_str());
    if (ss.isAfk) {
        ImGui::SameLine();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.9f, 0.7f, 0.2f, 1.0f));
        ImGui::Text("[AFK]");
        ImGui::PopStyleColor();
    }

    Row("Total", clrGreen, "%s", totalStr.c_str());

    ImGui::TextDisabled("Kills");
    ImGui::SameLine(labelCol);
    ImGui::TextColored(clrRed, "%d", ss.sessionKills);

    {
        uint64_t g = ps.gold;
        char goldBuf[32];
        if      (g >= 1000000) snprintf(goldBuf, sizeof(goldBuf), "%llu,%03llu,%03llu", g / 1000000, (g / 1000) % 1000, g % 1000);
        else if (g >= 1000)    snprintf(goldBuf, sizeof(goldBuf), "%llu,%03llu", g / 1000, g % 1000);
        else                   snprintf(goldBuf, sizeof(goldBuf), "%llu", g);
        ImGui::TextDisabled("Gold");
        ImGui::SameLine(labelCol);
        ImGui::TextColored(clrGold, "%s", goldBuf);
    }

    // POSITION
    ImGui::Spacing();
    ImGui::TextDisabled("POSITION");
    ImGui::Separator();
    ImGui::Spacing();

    if (!ss.curRegionName.empty())
        Row("Region",    clrValue, "%s", ss.curRegionName.c_str());
    Row("Region ID", clrValue, "%d", ss.currentRegionID);
    Row("World",     clrValue, "%d,  %d,  %d", ss.WorldX, ss.WorldY, ss.WorldZ);
    Row("Sector",    clrValue, "%d,  %d",       ss.SectorX, ss.SectorY);

    // INVENTORY
    ImGui::Spacing();
    ImGui::TextDisabled("INVENTORY");
    ImGui::Separator();
    ImGui::Spacing();

    {
        static int s_sortTarget = 0;
        static int s_sortType   = 0;
        static const char* kTargets[] = { "Player", "Pet" };
        static const char* kTypes[]   = { "By Type", "By Name", "Logical" };
        static const SortTarget kSortTargets[] = { SortTarget::Player, SortTarget::Pet };
        static const SortType   kSortTypes[]   = { SortType::ByType, SortType::ByName, SortType::Logical };

        static constexpr float ROW_H  = 22.f;
        const float spacing = ImGui::GetStyle().ItemSpacing.x;
        const float availW  = ImGui::GetContentRegionAvail().x;

        const float comboW  = (availW - spacing * 2.f) * 0.5f * 0.6f;
        const float btnW    = availW - comboW * 2.f - spacing * 2.f;

        SROCombo("##sort_target", &s_sortTarget, kTargets, 2, comboW, ROW_H);
        ImGui::SameLine();
        SROCombo("##sort_type",   &s_sortType,   kTypes,   3, comboW, ROW_H);
        ImGui::SameLine();
        if (SROButton("##sort_btn", "Sort", btnW, ROW_H))
            NetActions::SendSortRequest(kSortTypes[s_sortType], kSortTargets[s_sortTarget]);
    }

    // PENDING REWARDS
    if (!g_bridge.unclaimedRewards.empty())
    {
        ImGui::Spacing();
        ImGui::TextDisabled("PENDING REWARDS");
        ImGui::Separator();
        ImGui::Spacing();

        const float pillH    = 22.0f;
        const float pillPadX = 10.0f;
        float lineW          = ImGui::GetContentRegionAvail().x;
        float cursorX        = ImGui::GetCursorPosX();
        float startX         = cursorX;
        bool  firstOnLine    = true;

        for (int lvl : g_bridge.unclaimedRewards)
        {
            char label[16];
            snprintf(label, sizeof(label), "Lv %d", lvl);
            float pillW = ImGui::CalcTextSize(label).x + pillPadX * 2.0f;

            if (!firstOnLine && (cursorX + pillW > startX + lineW))
            {
                ImGui::NewLine();
                cursorX = startX;
                firstOnLine = true;
            }
            if (!firstOnLine) { ImGui::SameLine(0.0f, 4.0f); cursorX += pillW + 4.0f; }
            else              { cursorX += pillW; }
            firstOnLine = false;

            ImGui::PushStyleColor(ImGuiCol_Button,        ImVec4(0.45f, 0.30f, 0.05f, 1.0f));
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.65f, 0.45f, 0.08f, 1.0f));
            ImGui::PushStyleColor(ImGuiCol_ButtonActive,  ImVec4(0.30f, 0.20f, 0.03f, 1.0f));
            ImGui::PushStyleVar(ImGuiStyleVar_FrameRounding, 10.0f);
            char btnId[32];
            snprintf(btnId, sizeof(btnId), "Lv %d##rw%d", lvl, lvl);
            if (ImGui::Button(btnId, ImVec2(pillW, pillH)))
                g_bridge.Send("{\"type\":\"reward_reopen\",\"level\":" + std::to_string(lvl) + "}");
            ImGui::PopStyleVar();
            ImGui::PopStyleColor(3);
        }
        ImGui::NewLine();
    }

    EndSROWindow();
}

static void RenderAdminTools() {
    ImGui::SetNextWindowSize(ImVec2(340, 0), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(660, 20), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(300, 100), ImVec2(560, 760));
    ImGui::Begin("Admin Tools", &showAdminToolsWindow);

    State& ss = g_bridge.m_sessionState;

    if (!ss.isGM)
    {
        ImGui::TextDisabled("GM session required.");
        ImGui::End();
        return;
    }

    {
        ImGui::TextDisabled("DUNGEON ROOM RECORDER");
        ImGui::Separator();
        ImGui::Spacing();

        ImGui::Checkbox("Enable Room Recorder", &g_roomRecorderEnabled);

        if (g_roomRecorderEnabled)
        {
            auto& ss = g_bridge.m_sessionState;

            ImGui::Spacing();

            ImGui::Text("Room Name");
            ImGui::SetNextItemWidth(-1);
            ImGui::InputText("##roomname", g_currentRoom.roomName, sizeof(g_currentRoom.roomName));

            ImGui::Spacing();

            ImGui::TextColored(
                ImVec4(0.4f, 0.9f, 1.0f, 1.0f),
                "Current Sector: (%d, %d)",
                ss.SectorX,
                ss.SectorY
            );

            ImGui::Spacing();

            float half = ImGui::GetContentRegionAvail().x / 2.0f - 2;

            if (ImGui::Button("Set MinX", ImVec2(half, 24)))
                g_currentRoom.minX = ss.SectorX;

            ImGui::SameLine();

            if (ImGui::Button("Set MaxX", ImVec2(-1, 24)))
                g_currentRoom.maxX = ss.SectorX;

            if (ImGui::Button("Set MinY", ImVec2(half, 24)))
                g_currentRoom.minY = ss.SectorY;

            ImGui::SameLine();

            if (ImGui::Button("Set MaxY", ImVec2(-1, 24)))
                g_currentRoom.maxY = ss.SectorY;

            ImGui::Spacing();

            ImGui::Text("Current Bounds");
            ImGui::BulletText("MinXSec = %d", g_currentRoom.minX);
            ImGui::BulletText("MaxXSec = %d", g_currentRoom.maxX);
            ImGui::BulletText("MinYSec = %d", g_currentRoom.minY);
            ImGui::BulletText("MaxYSec = %d", g_currentRoom.maxY);

            ImGui::Spacing();

            if (ImGui::Button("Append Room", ImVec2(-1, 28)))
            {
                if (strlen(g_currentRoom.roomName) > 0)
                {
                    g_recordedRooms.push_back(g_currentRoom);

                    memset(g_currentRoom.roomName, 0, sizeof(g_currentRoom.roomName));

                    g_currentRoom.minX = 0;
                    g_currentRoom.maxX = 0;
                    g_currentRoom.minY = 0;
                    g_currentRoom.maxY = 0;
                }
            }

            ImGui::Spacing();

            ImGui::TextColored(
                ImVec4(0.5f, 1.0f, 0.5f, 1.0f),
                "Recorded Rooms: %d",
                (int)g_recordedRooms.size()
            );

            ImGui::Spacing();

            if (ImGui::Button("Export C# Code", ImVec2(-1, 30)))
            {
                FILE* f = fopen("DungeonRooms.txt", "w");

                if (f)
                {
                    for (const auto& room : g_recordedRooms)
                    {
                        fprintf(f,
                            "new()\n"
                            "{\n"
                            "    RoomName = \"%s\",\n"
                            "    MinXSec = %d,\n"
                            "    MaxXSec = %d,\n"
                            "    MinYSec = %d,\n"
                            "    MaxYSec = %d\n"
                            "},\n\n",
                            room.roomName,
                            room.minX,
                            room.maxX,
                            room.minY,
                            room.maxY
                        );
                    }

                    fclose(f);
                }
            }

            ImGui::Spacing();

            if (ImGui::CollapsingHeader("Recorded Rooms"))
            {
                for (size_t i = 0; i < g_recordedRooms.size(); i++)
                {
                    auto& r = g_recordedRooms[i];

                    ImGui::PushID((int)i);

                    ImGui::Text(
                        "%s (%d-%d, %d-%d)",
                        r.roomName,
                        r.minX,
                        r.maxX,
                        r.minY,
                        r.maxY
                    );

                    ImGui::SameLine();

                    if (ImGui::Button("Delete"))
                    {
                        g_recordedRooms.erase(g_recordedRooms.begin() + i);
                        ImGui::PopID();
                        break;
                    }

                    ImGui::PopID();
                }
            }
        }

        ImGui::Spacing();
        ImGui::TextDisabled("WAYPOINT RECORDER");
        ImGui::Separator();
        ImGui::Spacing();

        // Persistance
        ImGui::Text("Region Name:");
        ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x);
        ImGui::InputText("##region", ss.regionName, sizeof(ss.regionName));

        if (ImGui::Button("Load Region", ImVec2(ImGui::GetContentRegionAvail().x / 2 - 2, 22)))
        {
            LoadRegionData(ss);
        }
        ImGui::SameLine();
        if (ImGui::Button("Save Region", ImVec2(ImGui::GetContentRegionAvail().x, 22)))
        {
            SaveRegionData(ss);
        }
        if (ImGui::Button("Export Code", ImVec2(-1, 22)))
        {
            std::string filename = std::string(ss.regionName) + "_code.txt";
            FILE* f = fopen(filename.c_str(), "w");
            if (f)
            {
                for (auto& n : ss.g_recordedNodes)
                    fprintf(f, "_graph.AddNode(\"%s\", BotPosition.FromDisplayWorld(%d, %d));\n", n.id.c_str(), n.x, n.y);
                for (auto& e : ss.g_recordedEdges)
                    fprintf(f, "_graph.AddEdge(\"%s\", \"%s\");\n", e.fromId.c_str(), e.toId.c_str());
                fclose(f);
            }
        }

        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.6f, 0.1f, 0.1f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.8f, 0.2f, 0.2f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.4f, 0.05f, 0.05f, 1.0f));
        if (ImGui::Button("Clear Recorder", ImVec2(-1, 22)))
        {
            // Reset recorder state
            ss.recordMode = false;
            ss.g_recordedNodes.clear();
            ss.g_recordedEdges.clear();
            ss.activeNode.clear();
            ss.nodeCounter = 0;
            ss.totalRecorded = 0;
            memset(ss.jumpBuf, 0, sizeof(ss.jumpBuf));
        }
        ImGui::PopStyleColor(3);

        ImGui::Spacing();

        // Disable record button if no region name is set
        bool canRecord = strlen(ss.regionName) > 0;
        if (!canRecord) {
            ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 1.0f), "Enter a region name to start!");
            ImGui::BeginDisabled();
        }

        ImGui::PushStyleColor(ImGuiCol_Button, ss.recordMode
            ? ImVec4(0.6f, 0.1f, 0.1f, 1.0f)
            : ImVec4(0.1f, 0.4f, 0.1f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ss.recordMode
            ? ImVec4(0.8f, 0.2f, 0.2f, 1.0f)
            : ImVec4(0.2f, 0.6f, 0.2f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.05f, 0.25f, 0.05f, 1.0f));

        if (ImGui::Button(ss.recordMode ? "Recording ON##rec" : "Record OFF##rec", ImVec2(-1, 22)))
        {
            ss.recordMode = !ss.recordMode;
            if (ss.recordMode) {
                ss.lastSaveTime = ImGui::GetTime(); // Reset timer when starting
            }
        }
        ImGui::PopStyleColor(3);

        if (!canRecord) {
            ImGui::EndDisabled();
        }

        if (ss.recordMode)
        {
            double currentTime = ImGui::GetTime();
            if (currentTime - ss.lastSaveTime > 30.0) // 30 seconds
            {
                SaveRegionData(ss);
                ss.lastSaveTime = currentTime;
                ss.showSaveNotification = true;
                ss.notificationTime = currentTime;
            }

            if (ss.showSaveNotification)
            {
                if (currentTime - ss.notificationTime < 2.0)
                {
                    ImDrawList* draw = ImGui::GetForegroundDrawList();

                    ImVec2 windowPos = ImGui::GetWindowPos();
                    ImVec2 windowSize = ImGui::GetWindowSize();

                    const char* text = "Auto-saved...";

                    ImVec2 textSize = ImGui::CalcTextSize(text);

                    // Top-right inside the current window
                    ImVec2 pos(
                        windowPos.x + windowSize.x - textSize.x - 12.0f,
                        windowPos.y + 8.0f
                    );

                    draw->AddRectFilled(
                        ImVec2(pos.x - 6, pos.y - 4),
                        ImVec2(pos.x + textSize.x + 6, pos.y + textSize.y + 4),
                        IM_COL32(20, 20, 20, 200),
                        4.0f
                    );

                    draw->AddText(
                        pos,
                        IM_COL32(100, 255, 255, 255),
                        text
                    );
                }
                else
                {
                    ss.showSaveNotification = false;
                }
            }

            ImGui::Spacing();

            // Record/connect to active
            if (ImGui::Button("+ Connect##wpc", ImVec2(-1, 22)))
            {
                char id[64];
                snprintf(id, sizeof(id), "wp_%d", ss.nodeCounter++);

                ss.g_recordedNodes.push_back({ id, ss.WorldX, ss.WorldY });
                if (!ss.activeNode.empty())
                {
                    ss.g_recordedEdges.push_back({ ss.activeNode, id });
                }
                ss.activeNode = id;
                ss.totalRecorded++;
            }

            // Record without connecting
            if (ImGui::Button("+ No Edge##wpn", ImVec2(-1, 22)))
            {
                char id[64];
                snprintf(id, sizeof(id), "wp_%d", ss.nodeCounter++);
                ss.g_recordedNodes.push_back({ id, ss.WorldX, ss.WorldY });
                ss.activeNode = id;
                ss.totalRecorded++;
            }

        }

        ImGui::Spacing();
        ImGui::TextDisabled("GRAPH EDITOR");
        ImGui::Separator();
        ImGui::Spacing();

        ImGui::Text("Nodes: %d   Edges: %d",
            (int)ss.g_recordedNodes.size(), (int)ss.g_recordedEdges.size());

        if (ss.activeNode.empty())
            ImGui::TextColored(ImVec4(0.6f, 0.6f, 0.6f, 1.0f), "Active: (none)");
        else
            ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 1.0f), "Active: %s", ss.activeNode.c_str());

        ImGui::Spacing();

        ImGui::TextDisabled("Connect active to node #:");
        ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - 52);
        ImGui::InputText("##connect", ss.jumpBuf, sizeof(ss.jumpBuf));
        ImGui::SameLine();
        if (ImGui::Button("Edge##ce", ImVec2(-1, 22)))
        {
            if (!ss.activeNode.empty() && strlen(ss.jumpBuf) > 0)
            {
                std::string targetId = "wp_" + std::string(ss.jumpBuf);
                // Check the node actually exists
                bool exists = false;
                for (const auto& n : ss.g_recordedNodes)
                    if (n.id == targetId) { exists = true; break; }

                if (exists && targetId != ss.activeNode)
                {
                    ss.g_recordedEdges.push_back({ ss.activeNode, targetId });
                    memset(ss.jumpBuf, 0, sizeof(ss.jumpBuf));
                    SaveRegionData(ss);
                }
            }
        }

        ImGui::TextDisabled("Set active node #:");
        ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - 52);
        static char jumpNumBuf[16] = {};
        ImGui::InputText("##jn", jumpNumBuf, sizeof(jumpNumBuf));
        ImGui::SameLine();
        if (ImGui::Button("Go##jn", ImVec2(-1, 22)))
        {
            std::string targetId = "wp_" + std::string(jumpNumBuf);
            for (const auto& n : ss.g_recordedNodes)
            {
                if (n.id == targetId)
                {
                    ss.activeNode = targetId;
                    memset(jumpNumBuf, 0, sizeof(jumpNumBuf));
                    break;
                }
            }
        }

        if (ImGui::CollapsingHeader("Node List"))
        {
            ImGui::BeginChild("##nodelist", ImVec2(-1, 150), true);
            for (size_t i = 0; i < ss.g_recordedNodes.size(); i++)
            {
                const auto& n = ss.g_recordedNodes[i];
                ImGui::PushID((int)i);

                char label[96];
                snprintf(label, sizeof(label), "%s  (%d, %d)", n.id.c_str(), n.x, n.y);
                if (ImGui::Selectable(label, n.id == ss.activeNode,
                    ImGuiSelectableFlags_None,
                    ImVec2(ImGui::GetContentRegionAvail().x - 26, 0)))
                {
                    ss.activeNode = n.id;
                }

                ImGui::SameLine();
                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.6f, 0.1f, 0.1f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.4f, 0.05f, 0.05f, 1.0f));
                bool del = ImGui::SmallButton("X##dn");
                ImGui::PopStyleColor(3);
                ImGui::PopID();

                if (del)
                {
                    DeleteGraphNode(ss, n.id);
                    break; // re-iterate next frame
                }
            }
            ImGui::EndChild();
        }

        if (ImGui::CollapsingHeader("Edge List"))
        {
            ImGui::BeginChild("##edgelist", ImVec2(-1, 150), true);
            for (size_t i = 0; i < ss.g_recordedEdges.size(); i++)
            {
                const auto& e = ss.g_recordedEdges[i];
                ImGui::PushID((int)i);

                char label[160];
                snprintf(label, sizeof(label), "%s -> %s", e.fromId.c_str(), e.toId.c_str());
                ImGui::Selectable(label, false,
                    ImGuiSelectableFlags_None,
                    ImVec2(ImGui::GetContentRegionAvail().x - 26, 0));

                ImGui::SameLine();
                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.6f, 0.1f, 0.1f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.4f, 0.05f, 0.05f, 1.0f));
                bool del = ImGui::SmallButton("X##de");
                ImGui::PopStyleColor(3);
                ImGui::PopID();

                if (del)
                {
                    DeleteGraphEdge(ss, i);
                    break; //re-iterate next frame
                }
            }
            ImGui::EndChild();
        }

        if (ImGui::Button("Delete Active Node##undo", ImVec2(-1, 22)))
        {
            if (!ss.activeNode.empty())
                DeleteGraphNode(ss, ss.activeNode);
        }

        ImGui::TextDisabled("Radar: L-click selects, Ctrl+L-click deletes.");

        ImGui::Spacing();
        if (ImGui::Button("Log Move Sample (Hotkey)"))
        {
            auto& ss = g_bridge.m_sessionState;

            if (ss.hasActiveMoveSample)
            {
                auto now = std::chrono::steady_clock::now();

                long long ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                    now - ss.moveStartTime
                ).count();

                float dx = (float)(ss.moveEndX - ss.moveStartX);
                float dy = (float)(ss.moveEndY - ss.moveStartY);
                float dz = (float)(ss.moveEndZ - ss.moveStartZ);

                float dist = std::sqrt(dx * dx + dy * dy + dz * dz);
                float seconds = ms / 1000.0f;

                float measuredSpeed = (seconds > 0.0f) ? (dist / seconds) : 0.0f;

                float inferredDivisor = 50.0f / measuredSpeed; // since base run speed = 50

                std::ofstream file("move_profile.txt", std::ios::app);

                file << "MoveStart -> Trigger = " << ms << "ms\n";
                file << "Start = (" << ss.moveStartX << "," << ss.moveStartY << "," << ss.moveStartZ << ")\n";
                file << "End   = (" << ss.moveEndX << "," << ss.moveEndY << "," << ss.moveEndZ << ")\n";
                file << "Distance = " << dist << "\n";
                file << "MeasuredSpeed = " << measuredSpeed << "\n";
                file << "InferredDivisor = " << inferredDivisor << "\n";
                file << "---------------------------\n";

                file.close();

                ss.hasActiveMoveSample = false;
            }
        }
    }

    ImGui::End();
}

static IDirect3DTexture9* GetSkillIcon(const std::string& iconFile)
{
    if (iconFile.empty() || !g_iconCache) return nullptr;
    std::string base = iconFile;
    size_t dot = base.rfind('.');
    if (dot != std::string::npos) base = base.substr(0, dot);
    return g_iconCache->Get("icon/" + base + ".ddj");
}

static void RenderBotWindow() {

    PlayerState ps = g_bridge.m_state;
    State& ss = g_bridge.m_sessionState;
    bool hasPlayer = !ps.charName.empty();
    // To prevent problematic packets during login.
    if (hasPlayer) {
        ImGui::SetNextWindowSize(ImVec2(320, 520), ImGuiCond_FirstUseEver);
        ImGui::SetNextWindowPos(ImVec2(340, 20), ImGuiCond_FirstUseEver);
        ImGui::Begin("Bot Control");

        State& ss = g_bridge.m_sessionState;

        static int walkX = 0, walkY = 0, walkZ = 0, walkR = 25, regionID = 0;

        if (ss.hasSavedBotConfig) {
            walkX = ss.savedBotX;
            walkY = ss.savedBotY;
            walkZ = ss.savedBotZ;
            walkR = ss.savedBotR;
            regionID = ss.savedBotRegionId;
            ss.hasSavedBotConfig = false;
        }

        struct DefaultPreset
        {
            const char* name;
            int regionId;
            int x, y, z, r;
        };
        static const DefaultPreset defaultPresets[] = {
            { "Black Robber Den (SE)", 23700, 2671,  142, 207, 75 },
            { "Huns Garrison", 26737, -4124,  2430, 195, 75 },
            { "Stone Cave F3", 0x8001, 24533, 24403, 284, 75 },
            { "Desert Of Mysterious Death", 25731, -727, 1645, -42, 75 },
            { "Roc Mtn. Forest", 24431, -4520, 699, 2026, 40 },
            { "Beak Peak, Roc Mtn.", 22388, -3536, -781, 3379, 45 },
            { "Qin-Shi B4", 0x8004, 24407, 24561, -33, 50 },
        };

        struct UserPreset
        {
            char name[64];
            int x, y, z, r, regionID;
        };

        static std::vector<UserPreset> userPresets;
        static int selectedPreset = -1;   // >= 0: default index; <= -2: user index encoded as (-2 - i); -1: none
        static char presetFilter[64] = {};
        static bool savingPreset = false;
        static char newPresetName[64] = {};
        static int editingUserIdx = -1;
        static char editPresetName[64] = {};

        if (ImGui::BeginTabBar("##bottabs")) {

            // BOT TAB
            if (ImGui::BeginTabItem("Bot")) {

                // Status
                ImGui::TextDisabled("STATUS");
                ImGui::Separator();
                ImGui::Spacing();

                const char* stateStr = ss.botStateLabel.empty() ? "Idle" : ss.botStateLabel.c_str();
                ImVec4 stateColor = ImVec4(0.6f, 0.6f, 0.6f, 1.0f);

                if (ss.botStateLabel == "WalkingToTrainplace") stateColor = ImVec4(0.3f, 0.7f, 1.0f, 1.0f);
                else if (ss.botStateLabel == "Training")            stateColor = ImVec4(0.3f, 1.0f, 0.4f, 1.0f);
                else if (ss.botStateLabel == "Teleporting")         stateColor = ImVec4(0.8f, 0.4f, 1.0f, 1.0f);
                else if (ss.botStateLabel == "Returning")           stateColor = ImVec4(1.0f, 0.8f, 0.2f, 1.0f);
                else if (ss.botStateLabel == "Dead")                stateColor = ImVec4(1.0f, 0.3f, 0.3f, 1.0f);

                const float labelCol = 72.0f;
                auto Row = [&](const char* label, const char* fmt, ...) {
                    ImGui::TextDisabled("%s", label);
                    ImGui::SameLine(labelCol);
                    char buf[128];
                    va_list args;
                    va_start(args, fmt);
                    vsnprintf(buf, sizeof(buf), fmt, args);
                    va_end(args);
                    ImGui::TextColored(ImVec4(0.75f, 0.88f, 1.0f, 1.0f), "%s", buf);
                    };

                ImGui::TextDisabled("State");
                ImGui::SameLine(labelCol);
                ImGui::TextColored(stateColor, "%s", stateStr);

                if (!ss.curRegionName.empty())
                    Row("Region", "%s", ss.curRegionName.c_str());

                Row("Kills", "%d", ss.sessionKills);
                Row("Distance", "%.1f m", ss.distanceToTarget);

                if (ss.lastTargetUid != 0)
                    Row("Target UID", "%d", ss.lastTargetUid);
                else
                    Row("Target UID", "None");

                ImGui::Spacing();

                // Start / Stop
                float btnW = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;

                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.10f, 0.35f, 0.10f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.15f, 0.50f, 0.15f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.07f, 0.22f, 0.07f, 1.0f));
                if (ImGui::Button("Start##bot", ImVec2(btnW, 24)))
                    NetActions::SendStartBotRequest(walkX, walkY, walkZ, walkR, regionID);
                ImGui::PopStyleColor(3);

                ImGui::SameLine();

                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.35f, 0.10f, 0.10f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.50f, 0.15f, 0.15f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.22f, 0.07f, 0.07f, 1.0f));
                if (ImGui::Button("Stop##bot", ImVec2(btnW, 24)))
                    NetActions::SendStopBotRequest();
                ImGui::PopStyleColor(3);

                ImGui::Spacing();
                ImGui::TextDisabled("TRAIN PLACE");
                ImGui::Separator();
                ImGui::Spacing();

                float fieldW = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x * 3) / 4.0f;

                ImGui::PushItemWidth(fieldW);
                ImGui::InputInt("##tpx", &walkX, 0); ImGui::SameLine();
                ImGui::InputInt("##tpy", &walkY, 0); ImGui::SameLine();
                ImGui::InputInt("##tpz", &walkZ, 0); ImGui::SameLine();
                ImGui::InputInt("##tpr", &walkR, 0);
                ImGui::PopItemWidth();

                ImGui::TextDisabled(" X"); ImGui::SameLine(fieldW + 8);
                ImGui::TextDisabled("Y");  ImGui::SameLine(fieldW * 2 + 12);
                ImGui::TextDisabled("Z");  ImGui::SameLine(fieldW * 3 + 16);
                ImGui::TextDisabled("R");

                ImGui::Spacing();

                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.13f, 0.25f, 0.45f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.18f, 0.35f, 0.60f, 1.0f));
                ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.08f, 0.18f, 0.35f, 1.0f));
                if (ImGui::Button("Get Current Position##gcp", ImVec2(-1, 22))) {
                    walkX = ss.WorldX;
                    walkY = ss.WorldY;
                    walkZ = ss.WorldZ;
                    regionID = ss.currentRegionID;
                }
                ImGui::PopStyleColor(3);

                ImGui::Spacing();
                ImGui::TextDisabled("PRESETS");
                ImGui::Separator();
                ImGui::Spacing();

                ImGui::SetNextItemWidth(-1);
                ImGui::InputText("##filter", presetFilter, sizeof(presetFilter));
                ImGui::SameLine(0, 0);
                ImGui::TextDisabled(" Filter");

                ImGui::Spacing();

                if (ImGui::BeginChild("##presets", ImVec2(-1, 140), true)) {

                    // Default presets (read-only, dimmed)
                    for (int i = 0; i < IM_ARRAYSIZE(defaultPresets); i++) {
                        const DefaultPreset& p = defaultPresets[i];

                        if (strlen(presetFilter) > 0) {
                            std::string nameL = p.name;
                            std::string filterL = presetFilter;
                            std::transform(nameL.begin(), nameL.end(), nameL.begin(), ::tolower);
                            std::transform(filterL.begin(), filterL.end(), filterL.begin(), ::tolower);
                            if (nameL.find(filterL) == std::string::npos) continue;
                        }

                        bool sel = (selectedPreset == i);
                        char label[160];
                        snprintf(label, sizeof(label), "[default] %-20s  %d, %d, %d",
                            p.name, p.x, p.y, p.z);

                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.50f, 0.50f, 0.50f, 1.0f));
                        if (ImGui::Selectable(label, sel))
                            selectedPreset = i;
                        ImGui::PopStyleColor();

                        if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0)) {
                            walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r;
                            selectedPreset = i;
                        }
                    }

                    // User presets (editable)
                    for (int i = 0; i < (int)userPresets.size(); i++) {
                        UserPreset& p = userPresets[i];

                        if (strlen(presetFilter) > 0) {
                            std::string nameL = p.name;
                            std::string filterL = presetFilter;
                            std::transform(nameL.begin(), nameL.end(), nameL.begin(), ::tolower);
                            std::transform(filterL.begin(), filterL.end(), filterL.begin(), ::tolower);
                            if (nameL.find(filterL) == std::string::npos) continue;
                        }

                        // Inline rename mode
                        if (editingUserIdx == i) {
                            ImGui::SetNextItemWidth(-1);
                            bool entered = ImGui::InputText("##editname", editPresetName, sizeof(editPresetName),
                                ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);
                            if (entered) {
                                if (strlen(editPresetName) > 0)
                                    strncpy(p.name, editPresetName, sizeof(p.name) - 1);
                                editingUserIdx = -1;
                            }
                            if (ImGui::IsKeyPressed(ImGuiKey_Escape))
                                editingUserIdx = -1;
                            continue;
                        }

                        int encIdx = -2 - i;
                        bool sel = (selectedPreset == encIdx);

                        char label[160];
                        snprintf(label, sizeof(label), "%-28s  %d, %d, %d##user%d",
                            p.name, p.x, p.y, p.z, i);

                        if (ImGui::Selectable(label, sel))
                            selectedPreset = encIdx;

                        if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0)) {
                            walkX = p.x;
                            walkY = p.y;
                            walkZ = p.z;
                            walkR = p.r;
                            regionID = p.regionID;
                            selectedPreset = encIdx;
                        }

                        if (ImGui::BeginPopupContextItem()) {
                            if (ImGui::MenuItem("Rename")) {
                                editingUserIdx = i;
                                strncpy(editPresetName, p.name, sizeof(editPresetName) - 1);
                                editPresetName[sizeof(editPresetName) - 1] = '\0';
                            }
                            if (ImGui::MenuItem("Update Coords")) {
                                p.x = walkX; p.y = walkY; p.z = walkZ;
                                p.r = walkR; p.regionID = regionID;
                            }
                            ImGui::Separator();
                            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.35f, 0.35f, 1.0f));
                            if (ImGui::MenuItem("Delete")) {
                                if (selectedPreset == encIdx)
                                    selectedPreset = -1;
                                else if (selectedPreset < -1) {
                                    int selIdx = -2 - selectedPreset;
                                    if (selIdx > i) selectedPreset = -2 - (selIdx - 1);
                                }
                                userPresets.erase(userPresets.begin() + i);
                            }
                            ImGui::PopStyleColor();
                            ImGui::EndPopup();
                        }
                    }
                }
                ImGui::EndChild();

                ImGui::Spacing();

                // Save-as-preset name entry row
                if (savingPreset) {
                    float okW = 46.0f;
                    ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - okW - ImGui::GetStyle().ItemSpacing.x);
                    bool entered = ImGui::InputText("##newname", newPresetName, sizeof(newPresetName),
                        ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);
                    ImGui::SameLine();
                    bool confirmed = entered || ImGui::Button("OK##saveok", ImVec2(okW, 0));
                    if (confirmed) {
                        if (strlen(newPresetName) > 0) {
                            UserPreset np = {};
                            strncpy(np.name, newPresetName, sizeof(np.name) - 1);
                            np.x = walkX; np.y = walkY; np.z = walkZ;
                            np.r = walkR; np.regionID = regionID;
                            userPresets.push_back(np);
                        }
                        savingPreset = false;
                        newPresetName[0] = '\0';
                    }
                    if (ImGui::IsKeyPressed(ImGuiKey_Escape)) {
                        savingPreset = false;
                        newPresetName[0] = '\0';
                    }
                }
                else {
                    float halfW = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;

                    ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.13f, 0.25f, 0.45f, 1.0f));
                    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.18f, 0.35f, 0.60f, 1.0f));
                    ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.08f, 0.18f, 0.35f, 1.0f));

                    if (ImGui::Button("Use Selected##preset", ImVec2(halfW, 22)) && selectedPreset != -1) {
                        if (selectedPreset >= 0) {
                            const DefaultPreset& p = defaultPresets[selectedPreset];
                            walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r, regionID = p.regionId;
                        }
                        else {
                            int idx = -2 - selectedPreset;
                            UserPreset& p = userPresets[idx];
                            walkX = p.x; walkY = p.y; walkZ = p.z;
                            walkR = p.r; regionID = p.regionID;
                        }
                    }
                    ImGui::SameLine();
                    if (ImGui::Button("Save as Preset##savepreset", ImVec2(-1, 22))) {
                        savingPreset = true;
                        newPresetName[0] = '\0';
                    }

                    ImGui::PopStyleColor(3);
                }

                ImGui::EndTabItem();
            }

            // SKILLS TAB
            if (ImGui::BeginTabItem("Skills")) {

                State& ss = g_bridge.m_sessionState;

                static const float ICON_SZ = 26.0f;
                static const float ROW_H = 32.0f;

                auto DrawSkillRow = [&](const SkillEntry& sk, const char* selId, bool dimmed) -> bool {
                    ImDrawList* dl = ImGui::GetWindowDrawList();
                    ImVec2 rowStart = ImGui::GetCursorScreenPos();
                    float  rowWidth = ImGui::GetContentRegionAvail().x;

                    // Full-row hit target
                    bool clicked = ImGui::InvisibleButton(selId, ImVec2(rowWidth, ROW_H));
                    bool hovered = ImGui::IsItemHovered();
                    bool active = ImGui::IsItemActive();

                    // Hover / active highlight
                    if (hovered || active)
                        dl->AddRectFilled(
                            rowStart,
                            ImVec2(rowStart.x + rowWidth, rowStart.y + ROW_H),
                            ImGui::GetColorU32(active ? ImGuiCol_HeaderActive : ImGuiCol_HeaderHovered));

                    // Icon
                    float iconX = rowStart.x + 3.0f;
                    float iconY = rowStart.y + (ROW_H - ICON_SZ) * 0.5f;
                    IDirect3DTexture9* tex = GetSkillIcon(sk.iconFile);
                    if (tex)
                        dl->AddImage(ImTextureRef((ImTextureID)(uintptr_t)tex),
                            ImVec2(iconX, iconY), ImVec2(iconX + ICON_SZ, iconY + ICON_SZ),
                            ImVec2(0, 0), ImVec2(1, 1),
                            dimmed ? IM_COL32(160, 160, 160, 130) : IM_COL32(255, 255, 255, 255));
                    else
                        dl->AddRectFilled(
                            ImVec2(iconX, iconY), ImVec2(iconX + ICON_SZ, iconY + ICON_SZ),
                            IM_COL32(35, 40, 60, dimmed ? 100 : 200), 3.0f);

                    // Name text
                    ImU32 textColor = dimmed ? IM_COL32(130, 130, 130, 190) : IM_COL32(220, 220, 220, 255);
                    float textX = iconX + ICON_SZ + 5.0f;
                    float textY = rowStart.y + (ROW_H - ImGui::GetFontSize()) * 0.5f;
                    dl->AddText(ImVec2(textX, textY), textColor, sk.readableName.c_str());

                    return clicked;
                    };

                ImGui::TextDisabled("AVAILABLE SKILLS");
                ImGui::Separator();
                ImGui::Spacing();

                static char skillFilter[64] = {};
                ImGui::SetNextItemWidth(-1);
                ImGui::InputText("##skillfilter", skillFilter, sizeof(skillFilter));

                ImGui::Spacing();

                float halfW = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;

                if (ImGui::BeginChild("##skillpool", ImVec2(-1, 145), true)) {
                    for (int i = 0; i < (int)ss.availableSkills.size(); i++) {
                        auto& sk = ss.availableSkills[i];

                        if (strlen(skillFilter) > 0) {
                            std::string name = sk.readableName;
                            std::string filter = skillFilter;
                            std::transform(name.begin(), name.end(), name.begin(), ::tolower);
                            std::transform(filter.begin(), filter.end(), filter.begin(), ::tolower);
                            if (name.find(filter) == std::string::npos) continue;
                        }

                        char selId[32]; snprintf(selId, sizeof(selId), "##pool%d", i);
                        DrawSkillRow(sk, selId, sk.isPassive);

                        if (!sk.isPassive && ImGui::BeginPopupContextItem()) {
                            if (ImGui::MenuItem("Add to Attack Queue"))
                                NetActions::SendSkillAdd(sk.id, false);
                            if (ImGui::MenuItem("Add to Buff List"))
                                NetActions::SendSkillAdd(sk.id, true);
                            ImGui::EndPopup();
                        }
                    }
                }
                ImGui::EndChild();

                ImGui::Spacing();

                if (ImGui::BeginChild("##attackqueue", ImVec2(halfW, 165), true)) {
                    ImGui::TextDisabled("Attack Queue");
                    ImGui::Separator();
                    ImGui::Spacing();

                    for (int i = 0; i < (int)ss.attackSkills.size(); i++) {
                        auto& sk = ss.attackSkills[i];
                        char selId[32]; snprintf(selId, sizeof(selId), "##atk%d", i);
                        DrawSkillRow(sk, selId, false);

                        if (ImGui::BeginPopupContextItem()) {
                            if (i > 0 && ImGui::MenuItem("Move Up"))
                                NetActions::SendSkillMove(sk.id, -1);
                            if (i < (int)ss.attackSkills.size() - 1 && ImGui::MenuItem("Move Down"))
                                NetActions::SendSkillMove(sk.id, 1);
                            ImGui::Separator();
                            if (ImGui::MenuItem("Remove"))
                                NetActions::SendSkillRemove(sk.id, false);
                            ImGui::EndPopup();
                        }
                    }

                    if (ss.attackSkills.empty())
                        ImGui::TextDisabled("(empty)");
                }
                ImGui::EndChild();

                ImGui::SameLine();

                if (ImGui::BeginChild("##bufflist", ImVec2(-1, 165), true)) {
                    ImGui::TextDisabled("Buffs (Walk)");
                    ImGui::Separator();
                    ImGui::Spacing();

                    for (int i = 0; i < (int)ss.buffSkills.size(); i++) {
                        auto& sk = ss.buffSkills[i];
                        char selId[32]; snprintf(selId, sizeof(selId), "##buf%d", i);
                        DrawSkillRow(sk, selId, false);

                        if (ImGui::BeginPopupContextItem()) {
                            if (ImGui::MenuItem("Remove"))
                                NetActions::SendSkillRemove(sk.id, true);
                            ImGui::EndPopup();
                        }
                    }

                    if (ss.buffSkills.empty())
                        ImGui::TextDisabled("(empty)");
                }
                ImGui::EndChild();

                ImGui::Spacing();
                ImGui::TextDisabled("Right-click a skill to add or remove it. Dimmed = passive.");

                ImGui::EndTabItem();
            }

            // SETTINGS TAB
            if (ImGui::BeginTabItem("Settings")) {

                ImGui::BeginChild("##settings_scroll", ImVec2(0, -32), true);

                float inputWidthShort = 50.0f;

                if (ImGui::CollapsingHeader("Auto Potion & Protection")) {
                    ImGui::Checkbox("Use HP Pot", &ss.botSettings.AutoPotion.AutoUseHP);
                    if (ss.botSettings.AutoPotion.AutoUseHP) {
                        ImGui::SameLine(120); ImGui::SetNextItemWidth(inputWidthShort);
                        ImGui::SliderInt("%##hp", &ss.botSettings.AutoPotion.HPPotHealthThreshold, 0, 100, "");
                        ImGui::SameLine(); ImGui::SetNextItemWidth(60.0f);
                        ImGui::InputInt("ms##hpdel", &ss.botSettings.AutoPotion.HPDelay, 0, 0);
                    }

                    ImGui::Checkbox("Use MP Pot", &ss.botSettings.AutoPotion.AutoUseMP);
                    if (ss.botSettings.AutoPotion.AutoUseMP) {
                        ImGui::SameLine(120); ImGui::SetNextItemWidth(inputWidthShort);
                        ImGui::SliderInt("%##mp", &ss.botSettings.AutoPotion.MPPotManaThreshold, 0, 100, "");
                        ImGui::SameLine(); ImGui::SetNextItemWidth(60.0f);
                        ImGui::InputInt("ms##mpdel", &ss.botSettings.AutoPotion.MPDelay, 0, 0);
                    }

                    ImGui::Checkbox("Use Vigor", &ss.botSettings.AutoPotion.UseVigorPotions);
                    if (ss.botSettings.AutoPotion.UseVigorPotions) {
                        ImGui::SameLine(120); ImGui::SetNextItemWidth(inputWidthShort);
                        ImGui::SliderInt("%##vig", &ss.botSettings.AutoPotion.VigorHPMPThreshold, 0, 100, "");
                        ImGui::SameLine();
                        ImGui::Checkbox("Prioritize", &ss.botSettings.AutoPotion.PreferVigorFirst);
                    }

                    ImGui::Separator();
                    ImGui::Checkbox("Auto Universal Pills", &ss.botSettings.AutoPotion.AutoUseContPills);
                    ImGui::Checkbox("Auto Purification Pills", &ss.botSettings.AutoPotion.AutoUsePurifPills);
                    ImGui::Separator();

                    ImGui::Checkbox("Heal Pets", &ss.botSettings.AutoPotion.HealPets);
                    if (ss.botSettings.AutoPotion.HealPets) {
                        ImGui::SameLine(120); ImGui::SetNextItemWidth(inputWidthShort);
                        ImGui::SliderInt("%##pet", &ss.botSettings.AutoPotion.HealPetHPThreshold, 0, 100, "");
                    }
                }

                if (ImGui::CollapsingHeader("Town Supplies (Buy)")) {

                    auto RenderBuyRow = [&](const char* label, bool* buyBool, int* refill, int* threshold, const char** comboItems, int comboSize, int* selectedEnum) {
                        ImGui::Checkbox(label, buyBool);
                        if (*buyBool) {
                            ImGui::PushItemWidth(45.0f);
                            ImGui::TextDisabled(" Buy:"); ImGui::SameLine(); ImGui::InputInt(std::string("##rf_").append(label).c_str(), refill, 0, 0); ImGui::SameLine();
                            ImGui::TextDisabled("Min:");  ImGui::SameLine(); ImGui::InputInt(std::string("##th_").append(label).c_str(), threshold, 0, 0); ImGui::SameLine();
                            ImGui::PopItemWidth();

                            if (comboItems != nullptr) {
                                ImGui::SetNextItemWidth(80.0f);
                                ImGui::Combo(std::string("##type_").append(label).c_str(), selectedEnum, comboItems, comboSize);
                            }
                        }
                        ImGui::Spacing();
                        };

                    RenderBuyRow("HP Potions", &ss.botSettings.Consumables.BuyHpPotions, &ss.botSettings.Consumables.HpPotionRefillAmount, &ss.botSettings.Consumables.HpPotionReturnThreshold, PotionTypes, 6, (int*)&ss.botSettings.Consumables.HPType);
                    RenderBuyRow("MP Potions", &ss.botSettings.Consumables.BuyMpPotions, &ss.botSettings.Consumables.MpPotionRefillAmount, &ss.botSettings.Consumables.MpPotionReturnThreshold, PotionTypes, 6, (int*)&ss.botSettings.Consumables.MPType);
                    RenderBuyRow("Vigor Pots", &ss.botSettings.Consumables.BuyVigorPotions, &ss.botSettings.Consumables.VigorPotionRefillAmount, &ss.botSettings.Consumables.VigorPotionReturnThreshold, nullptr, 0, nullptr);
                    RenderBuyRow("Univ. Pills", &ss.botSettings.Consumables.BuyUniversalPills, &ss.botSettings.Consumables.UniversalPillsRefillAmount, &ss.botSettings.Consumables.UniversalPillsReturnThreshold, UniPillTypes, 4, (int*)&ss.botSettings.Consumables.UniPillType);
                    RenderBuyRow("Purif. Pills", &ss.botSettings.Consumables.BuyPurifPills, &ss.botSettings.Consumables.PurifPillsRefillAmount, &ss.botSettings.Consumables.PurifPillsReturnThreshold, PurifPillTypes, 4, (int*)&ss.botSettings.Consumables.PurificationPillType);
                    RenderBuyRow("Speed Drugs", &ss.botSettings.Consumables.BuySpeedDrugs, &ss.botSettings.Consumables.SpeedDrugsRefillAmount, &ss.botSettings.Consumables.SpeedDrugsReturnThreshold, SpeedDrugTypes, 2, (int*)&ss.botSettings.Consumables.DrugType);
                    RenderBuyRow("Ammo/Arrows", &ss.botSettings.Consumables.BuyAmmo, &ss.botSettings.Consumables.AmmoRefillAmount, &ss.botSettings.Consumables.AmmoReturnThreshold, AmmoTypes, 2, (int*)&ss.botSettings.Consumables.AmmoType);

                    ImGui::Separator();

                    ImGui::Checkbox("Buy Return Scrolls", &ss.botSettings.Consumables.BuyReturnScrolls);
                    if (ss.botSettings.Consumables.BuyReturnScrolls) {
                        ImGui::SameLine(150); ImGui::SetNextItemWidth(50.0f);
                        ImGui::InputInt("Count##ret", &ss.botSettings.Consumables.ReturnScrollRefillAmount, 0, 0);
                    }
                }

                if (ImGui::CollapsingHeader("Maintenance & Recall")) {
                    ImGui::TextDisabled("CITY REPAIR RULES");
                    ImGui::Checkbox("Repair Equipment at Smith", &ss.botSettings.Maintenance.RepairWeapon);
                    if (ss.botSettings.Maintenance.RepairWeapon) {
                        ImGui::SetNextItemWidth(120.0f);
                        ImGui::SliderInt("Durability % Trip", &ss.botSettings.Maintenance.RepairDurabilityThreshold, 0, 100);
                    }

                    ImGui::Spacing();
                    ImGui::TextDisabled("EMERGENCY TOWN RECALLS");
                    ImGui::Checkbox("Return to Town if Dead", &ss.botSettings.BackTownMonitor.ReturnIfDead);
                    ImGui::Checkbox("Return to Town if Inventory Full", &ss.botSettings.BackTownMonitor.ReturnIfInventoryFull);
                }

                if (ImGui::CollapsingHeader("Combat & Berserk")) {
                    ImGui::Checkbox("Ignore Dimension Pillars", &ss.botSettings.Attack.IgnoreDimensionPillars);
                    ImGui::Separator();

                    ImGui::TextDisabled("BERSERK (ZERK) MODES");
                    ImGui::Checkbox("Use Zerk Instantly when Full", &ss.botSettings.Attack.UseZerkRightAwayWhenFull);

                    if (!ss.botSettings.Attack.UseZerkRightAwayWhenFull) {
                        ImGui::Checkbox("Zerk on Normal Giants", &ss.botSettings.Attack.UseZerkOnNormalGiants);
                        ImGui::Checkbox("Zerk on Party Mobs", &ss.botSettings.Attack.UseZerkOnPartyMobs);
                        ImGui::Checkbox("Zerk on Party Giants", &ss.botSettings.Attack.UseZerkOnPartyGiants);
                        ImGui::Checkbox("Zerk on Uniques", &ss.botSettings.Attack.UseZerkOnUniques);
                        ImGui::Checkbox("Zerk if Surrendered/Swarmed", &ss.botSettings.Attack.UseZerkIfNMobsAttackingSimulataneously);
                        if (ss.botSettings.Attack.UseZerkIfNMobsAttackingSimulataneously) {
                            ImGui::SameLine(); ImGui::SetNextItemWidth(40.0f);
                            ImGui::InputInt("Mobs", &ss.botSettings.Attack.ZerkMobCount, 0, 0);
                        }
                    }
                }

                if (ImGui::CollapsingHeader("Looting & Buffs")) {
                    ImGui::Checkbox("Pick Gold", &ss.botSettings.Pickup.PickGold);
                    ImGui::Checkbox("Loot Absolutely Everything", &ss.botSettings.Pickup.PickAll);
                    ImGui::Checkbox("Pick Ammo If Low", &ss.botSettings.Pickup.PickAmmoIfAmountLowerThan);

                    ImGui::Separator();
                    ImGui::TextDisabled("WALK BUFFING");
                    ImGui::Checkbox("Cast Speed Buffs While Walking", &ss.botSettings.Autowalker.CastSpeedBuffWhileWalking);
                    ImGui::Checkbox("Cast Noise Buffs While Walking", &ss.botSettings.Autowalker.CastNoiseBuffWhileWalking);
                }

                ImGui::EndChild();

                ImGui::Separator();
                ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.15f, 0.40f, 0.15f, 1.0f));
                if (ImGui::Button("Save & Apply Configuration", ImVec2(-1, 24)))
                    NetActions::SendSaveBotSettings(ss.botSettings);
                ImGui::PopStyleColor();

                ImGui::EndTabItem();
            }

            ImGui::EndTabBar();
        }

        ImGui::End();
    }
    
}

static void RenderSettings() {
    ImGui::SetNextWindowSize(ImVec2(280, 0), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(60, 60), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(220, 80), ImVec2(400, 400));
    ImGui::Begin("Settings", &showSettingsWindow);
    ImGui::TextDisabled("GENERAL");
    ImGui::Separator();
    ImGui::Spacing();
    bool kf = Settings::keepFocused;
    if (ImGui::Checkbox("Keep Focus", &kf)) {
        Settings::keepFocused = kf;
        Settings::Save();
    }
    bool showFPS = Settings::showFPSCounter;
    if (ImGui::Checkbox("Show FPS counter", &showFPS)) {
        Settings::showFPSCounter = showFPS;
        Settings::Save();
    }
    bool showWaterMark = Settings::showWatermark;
    if (ImGui::Checkbox("Show Watermark", &showWaterMark)) {
        Settings::showWatermark = showWaterMark;
        Settings::Save();
    }
    ImGui::Spacing();
    ImGui::TextDisabled("NavMesh");
    ImGui::Separator();

    ImGui::Spacing();
    ImGui::TextDisabled("DEV TOOLS");
    ImGui::Separator();
    ImGui::Spacing();

    if (ImGui::Button(showLatticeInspector ? "Close Lattice Inspector" : "Open Lattice Inspector", ImVec2(-1, 22)))
        showLatticeInspector = !showLatticeInspector;

    ImGui::Spacing();
    ImGui::TextDisabled("PK2 TEST");
    ImGui::Separator();
    if (ImGui::Button("Read DIVISIONINFO.TXT")) {
        auto& log = GetLogger();
        if (!g_pk2.IsOpen()) {
            char exePath[MAX_PATH];
            GetModuleFileNameA(NULL, exePath, MAX_PATH);
            std::string dir(exePath);
            dir = dir.substr(0, dir.find_last_of("\\/"));
            std::wstring pk2Path(dir.begin(), dir.end());
            pk2Path += L"\\media.pk2";
            if (!g_pk2.Open(pk2Path))
                log.Err("PK2Test", "Failed to open media.pk2 at: " + dir);
        }
        if (g_pk2.IsOpen()) {
            std::vector<uint8_t> data;
            if (g_pk2.ReadFile("DIVISIONINFO.TXT", data)) {
                std::string content(data.begin(), data.end());
                log.Info("PK2Test", "DIVISIONINFO.TXT (" + std::to_string(data.size()) + " bytes): " + content);
            } else {
                log.Err("PK2Test", "DIVISIONINFO.TXT not found in media.pk2");
            }
        }
    }

    ImGui::End();
}

static void RenderSkinTest()
{
    if (!BeginSROWindow("##skintest", "Skin Test", &showSkinTest,
                        { 380.f, 300.f }, { 200.f, 180.f }, { 200.f, 200.f }, { 900.f, 900.f }))
        return;

    ImGui::TextDisabled("SRO skin test window — BeginSROWindow / EndSROWindow");
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();
    ImGui::TextWrapped("Resize to test 9-slice scaling. Scroll to test custom scrollbar.");
    ImGui::Spacing();
    for (int i = 1; i <= 20; i++)
        ImGui::TextDisabled("Row %d — sample content", i);

    EndSROWindow();
}

static void RenderLatticeInspector()
{
    ImGui::SetNextWindowSize(ImVec2(640, 660), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(80, 60),    ImGuiCond_FirstUseEver);
    ImGui::Begin("Lattice Inspector", &showLatticeInspector);

    struct Probe { const char* section; const char* label; const char* path; };
    static const Probe probes[] = {
        // Corners - direct in ifcommon/
        { "Corners  (ifcommon/)",       "left_up",    "interface\\ifcommon\\com_lattice_left_up.ddj"    },
        { nullptr,                      "right_up",   "interface\\ifcommon\\com_lattice_right_up.ddj"   },
        { nullptr,                      "left_down",  "interface\\ifcommon\\com_lattice_left_down.ddj"  },
        { nullptr,                      "right_down", "interface\\ifcommon\\com_lattice_right_down.ddj" },
        // Outlines/edges - direct in ifcommon/
        { "Outlines (ifcommon/)",       "outline_left_side",  "interface\\ifcommon\\com_lattice_outline_left_side.ddj"  },
        { nullptr,                      "outline_right_side", "interface\\ifcommon\\com_lattice_outline_right_side.ddj" },
        { nullptr,                      "outline_left_up",    "interface\\ifcommon\\com_lattice_outline_left_up.ddj"    },
        { nullptr,                      "outline_left_down",  "interface\\ifcommon\\com_lattice_outline_left_down.ddj"  },
        { nullptr,                      "outline_right_up",   "interface\\ifcommon\\com_lattice_outline_right_up.ddj"   },
        { nullptr,                      "outline_right_down", "interface\\ifcommon\\com_lattice_outline_right_down.ddj" },
        // BG tile - direct in ifcommon/
        { "BG Tile  (ifcommon/)",       "com_bg_tile",    "interface\\ifcommon\\com_bg_tile.ddj"    },
        { nullptr,                      "com_bg_tile_01", "interface\\ifcommon\\com_bg_tile_01.ddj" },
        { nullptr,                      "com_bg_tile_02", "interface\\ifcommon\\com_bg_tile_02.ddj" },
        // Corners - lattice_window/ subfolder
        { "Corners  (lattice_window/)", "left_up",    "interface\\ifcommon\\lattice_window\\com_lattice_left_up.ddj"    },
        { nullptr,                      "right_up",   "interface\\ifcommon\\lattice_window\\com_lattice_right_up.ddj"   },
        { nullptr,                      "left_down",  "interface\\ifcommon\\lattice_window\\com_lattice_left_down.ddj"  },
        { nullptr,                      "right_down", "interface\\ifcommon\\lattice_window\\com_lattice_right_down.ddj" },
        // Outlines/edges - lattice_window/ subfolder
        { "Outlines (lattice_window/)", "outline_left_side",   "interface\\ifcommon\\lattice_window\\com_lattice_outline_left_side.ddj"   },
        { nullptr,                      "outline_right_side",  "interface\\ifcommon\\lattice_window\\com_lattice_outline_right_side.ddj"  },
        { nullptr,                      "outline_up_side",     "interface\\ifcommon\\lattice_window\\com_lattice_outline_up_side.ddj"     },
        { nullptr,                      "outline_down_side",   "interface\\ifcommon\\lattice_window\\com_lattice_outline_down_side.ddj"   },
        { nullptr,                      "outline_top_side",    "interface\\ifcommon\\lattice_window\\com_lattice_outline_top_side.ddj"    },
        { nullptr,                      "outline_bottom_side", "interface\\ifcommon\\lattice_window\\com_lattice_outline_bottom_side.ddj" },
        { nullptr,                      "outline_left_up",     "interface\\ifcommon\\lattice_window\\com_lattice_outline_left_up.ddj"     },
        { nullptr,                      "outline_left_down",   "interface\\ifcommon\\lattice_window\\com_lattice_outline_left_down.ddj"   },
        { nullptr,                      "outline_right_up",    "interface\\ifcommon\\lattice_window\\com_lattice_outline_right_up.ddj"    },
        { nullptr,                      "outline_right_down",  "interface\\ifcommon\\lattice_window\\com_lattice_outline_right_down.ddj"  },
        { nullptr,                      "outline_up_left",     "interface\\ifcommon\\lattice_window\\com_lattice_outline_up_left.ddj"     },
        { nullptr,                      "outline_up_right",    "interface\\ifcommon\\lattice_window\\com_lattice_outline_up_right.ddj"    },
        { nullptr,                      "outline_down_left",   "interface\\ifcommon\\lattice_window\\com_lattice_outline_down_left.ddj"   },
        { nullptr,                      "outline_down_right",  "interface\\ifcommon\\lattice_window\\com_lattice_outline_down_right.ddj"  },
        // BG tile - bg_tile/ subfolder
        { "BG Tile  (bg_tile/)",        "com_bg_tile",    "interface\\ifcommon\\bg_tile\\com_bg_tile.ddj"    },
        { nullptr,                      "com_bg_tile_01", "interface\\ifcommon\\bg_tile\\com_bg_tile_01.ddj" },
        { nullptr,                      "com_bg_tile_02", "interface\\ifcommon\\bg_tile\\com_bg_tile_02.ddj" },
    };

    const ImVec4 clrHit (0.40f, 0.90f, 0.40f, 1.0f);
    const ImVec4 clrMiss(0.90f, 0.35f, 0.35f, 1.0f);

    // Folder browse
    ImGui::TextDisabled("PK2 FOLDER BROWSER");
    ImGui::Separator();
    ImGui::Spacing();

    static char s_folderInput[256] = "interface\\ifcommon\\lattice_window";
    static std::vector<std::string> s_folderListing;
    static bool s_listingReady = false;

    ImGui::SetNextItemWidth(-90.0f);
    ImGui::InputText("##folder", s_folderInput, sizeof(s_folderInput));
    ImGui::SameLine();
    if (ImGui::Button("List##pk2ls", ImVec2(-1, 0)))
    {
        s_folderListing = g_pk2.List(s_folderInput);
        s_listingReady  = true;
    }

    if (s_listingReady)
    {
        ImGui::Spacing();
        if (s_folderListing.empty())
        {
            ImGui::TextColored(clrMiss, "Folder not found or empty.");
        }
        else
        {
            ImGui::TextDisabled("%d entries:", (int)s_folderListing.size());
            if (ImGui::BeginChild("##pk2ls_results", ImVec2(0, 120), true))
            {
                for (const auto& name : s_folderListing)
                {
                    ImGui::TextDisabled("%s", name.c_str());
                    if (ImGui::IsItemClicked())
                    {
                        std::string next = std::string(s_folderInput) + "\\" + name;
                        strncpy(s_folderInput, next.c_str(), sizeof(s_folderInput) - 1);
                        s_folderInput[sizeof(s_folderInput) - 1] = '\0';
                        s_listingReady = false;
                    }
                }
            }
            ImGui::EndChild();
            ImGui::TextDisabled("Click an entry to navigate into it.");
        }
    }

    ImGui::Spacing();

    //Skin test launcher
    ImGui::TextDisabled("SKIN TEST");
    ImGui::Separator();
    ImGui::Spacing();

    if (ImGui::Button(showSkinTest ? "Close Skin Test" : "Open Skin Test", ImVec2(-1, 22)))
        showSkinTest = !showSkinTest;

    ImGui::Spacing();

    // Path probes
    ImGui::TextDisabled("Probes: %d paths across two candidate folder layouts.", (int)IM_ARRAYSIZE(probes));
    ImGui::TextDisabled("Hover a row label to see the full archive path.");
    ImGui::Separator();
    ImGui::Spacing();

    ImGui::BeginChild("##li_scroll", ImVec2(0, 0), false);

    const char* lastSection = nullptr;
    for (const auto& p : probes)
    {
        if (p.section && (!lastSection || strcmp(p.section, lastSection) != 0))
        {
            if (lastSection) { ImGui::Spacing(); ImGui::Spacing(); }
            ImGui::TextDisabled("%s", p.section);
            ImGui::Separator();
            ImGui::Spacing();
            lastSection = p.section;
        }

        IDirect3DTexture9* tex = g_iconCache ? g_iconCache->Get(p.path) : nullptr;

        if (tex)
        {
            ImGui::Image(ImTextureRef((ImTextureID)(uintptr_t)tex), ImVec2(64.0f, 64.0f));
        }
        else
        {
            ImVec2 pos = ImGui::GetCursorScreenPos();
            auto*  dl  = ImGui::GetWindowDrawList();
            dl->AddRectFilled(pos, ImVec2(pos.x + 64, pos.y + 64), IM_COL32(50, 15, 15, 255));
            dl->AddRect      (pos, ImVec2(pos.x + 64, pos.y + 64), IM_COL32(140, 40, 40, 255));
            dl->AddLine(ImVec2(pos.x + 10, pos.y + 10), ImVec2(pos.x + 54, pos.y + 54), IM_COL32(140, 40, 40, 200), 2.0f);
            dl->AddLine(ImVec2(pos.x + 54, pos.y + 10), ImVec2(pos.x + 10, pos.y + 54), IM_COL32(140, 40, 40, 200), 2.0f);
            ImGui::Dummy(ImVec2(64.0f, 64.0f));
        }

        ImGui::SameLine(0.0f, 10.0f);
        ImGui::BeginGroup();
        ImGui::TextColored(tex ? clrHit : clrMiss, tex ? " HIT " : "MISS");
        ImGui::TextDisabled("%s", p.label);
        if (ImGui::IsItemHovered())
            ImGui::SetTooltip("%s", p.path);
        ImGui::EndGroup();

        ImGui::Spacing();
    }

    ImGui::EndChild();
    ImGui::End();
}

static void RenderRadar(IDirect3DDevice9* device, int playerX, int playerY, const std::string& activeNodeId) {
    g_currentFrame++;
    PlayerState ps = g_bridge.m_state;
    State& ss = g_bridge.m_sessionState;
    bool hasPlayer = !ps.charName.empty();

    if (hasPlayer && ss.isGM) {
        ImGui::SetNextWindowSize(ImVec2(300, 360), ImGuiCond_FirstUseEver);
        ImGui::Begin("Nav Radar");
        State& ss = g_bridge.m_sessionState;

        float playerZ = (float)ss.WorldZ;

        const float Z_DISPLAY_THRESHOLD = 40.0f;

        //auto NodeVisible = [&](const auto& node) -> bool {
        //    return std::abs(node.z - playerZ) <= Z_DISPLAY_THRESHOLD;
        //};
        //auto EdgeVisible = [&](const auto& edge) -> bool {
        //    float z1 = 0.0f, z2 = 0.0f;
        //    for (const auto& node : ss.g_recordedNodes)
        //    {
        //        if (node.id == edge.fromId) z1 = (float)node.z;
        //        if (node.id == edge.toId)   z2 = (float)node.z;
        //    }
        //    // Edge visible if either endpoint is within range of player Z
        //    return std::abs(z1 - playerZ) <= Z_DISPLAY_THRESHOLD
        //        || std::abs(z2 - playerZ) <= Z_DISPLAY_THRESHOLD;
        //};

        bool isDungeon = ss.currentFloor > 0;
        std::string dungeonFolder = "";
        std::string dungeonPrefix = "";

        if (isDungeon)
        {
            auto& code = ss.curRegionCodeName;
            if (code.rfind("Qin-Shi Tomb", 0) == 0)
            {
                dungeonFolder = "jinsi";
                dungeonPrefix = "qt_a01";
            }
            else if (code == "Stone Cave")
            {
                dungeonFolder = "donwhang";
                dungeonPrefix = "dh_a01";
            }
        }

        CleanupMinimapTiles(ss.SectorX, ss.SectorY);
        float radarSize = ImGui::GetContentRegionAvail().x;

        ImGui::SliderInt("Tile Radius", &ss.tileRadius, 1, 12);

        // Font size slider for the nodes
        static float s_pointFontSize = 18.0f; // Default size
        ImGui::SliderFloat("Point Font Size", &s_pointFontSize, 8.0f, 32.0f, "%.1f");

        ImGui::BeginChild("##radar_canvas", ImVec2(radarSize, radarSize), false,
            ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

        bool canvasHovered = ImGui::IsWindowHovered();
        static bool s_draggingCanvas = false;

        if (canvasHovered && ImGui::IsMouseClicked(ImGuiMouseButton_Right))
            s_draggingCanvas = true;

        if (!ImGui::IsMouseDown(ImGuiMouseButton_Right))
            s_draggingCanvas = false;

        if (s_draggingCanvas)
        {
            ImVec2 delta = ImGui::GetIO().MouseDelta;
            ss.radarOffsetX += delta.x;
            ss.radarOffsetY += delta.y;
            ImGui::GetIO().MouseDelta = ImVec2(0, 0);
        }

        if (canvasHovered)
        {
            float wheel = ImGui::GetIO().MouseWheel;
            if (wheel != 0.0f)
            {
                float oldZoom = ss.radarZoom;
                float newZoom = ImClamp(oldZoom + wheel * 0.1f, 0.05f, 5.0f);
                float ratio = newZoom / oldZoom;
                ss.radarOffsetX *= ratio;
                ss.radarOffsetY *= ratio;
                ss.radarZoom = newZoom;
            }
        }

        float zoomScale = ss.radarZoom;
        ImVec2 canvasOrigin = ImGui::GetCursorScreenPos();
        ImVec2 center = ImVec2(
            canvasOrigin.x + radarSize / 2.0f,
            canvasOrigin.y + radarSize / 2.0f);

        ImDrawList* drawList = ImGui::GetWindowDrawList();

        drawList->AddRectFilled(
            canvasOrigin,
            ImVec2(canvasOrigin.x + radarSize, canvasOrigin.y + radarSize),
            IM_COL32(20, 20, 20, 255));

        for (int dy = -ss.tileRadius; dy <= ss.tileRadius; dy++)
        {
            for (int dx = -ss.tileRadius; dx <= ss.tileRadius; dx++)
            {
                int sx = ss.SectorX + dx;
                int sy = ss.SectorY + dy;

                MinimapTile* tile = GetOrLoadTile(
                    device, sx, sy,
                    dungeonFolder, dungeonPrefix, ss.currentFloor);

                if (!tile || !tile->texture) continue;

                DrawRadarTile(drawList, tile, sx, sy,
                    playerX, playerY, zoomScale, center,
                    ss.radarOffsetX, ss.radarOffsetY, isDungeon);
            }
        }

        auto WorldToRadar = [&](int wx, int wy) -> ImVec2 {
            float dx = (wx - playerX) * zoomScale;
            float dy = (wy - playerY) * zoomScale;
            return ImVec2(
                center.x + dx + ss.radarOffsetX,
                center.y - dy + ss.radarOffsetY);
            };

        for (const auto& edge : ss.g_recordedEdges)
        {
            //if (!EdgeVisible(edge)) continue;
            ImVec2 p1, p2;
            bool found1 = false, found2 = false;
            for (const auto& node : ss.g_recordedNodes)
            {
                if (node.id == edge.fromId) { p1 = WorldToRadar(node.x, node.y); found1 = true; }
                if (node.id == edge.toId) { p2 = WorldToRadar(node.x, node.y); found2 = true; }
            }
            if (found1 && found2)
                drawList->AddLine(p1, p2, IM_COL32(255, 20, 147, 255), 2.0f);
        }

        for (const auto& node : ss.g_recordedNodes)
        {
            //if (!NodeVisible(node)) continue;
            // Highlight active node green/add element
            ImVec2 pos = WorldToRadar(node.x, node.y);
            ImU32 color = (node.id == activeNodeId)
                ? IM_COL32(0, 255, 0, 255)
                : IM_COL32(255, 255, 255, 255);
            drawList->AddCircleFilled(pos, 4.0f, color);

            drawList->AddText(
                ImGui::GetFont(),
                s_pointFontSize, // Uses the new slider variable
                ImVec2(pos.x + 5, pos.y + 5),
                IM_COL32(183, 0, 255, 255),
                node.id.c_str());
        }

        ImVec2 playerPos = WorldToRadar(playerX, playerY);
        drawList->AddCircleFilled(playerPos, 5.0f, IM_COL32(0, 220, 255, 255));

        // L-click selects the nearest node, Ctrl+L-click deletes it.
        if (canvasHovered && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
        {
            ImVec2 mouse = ImGui::GetMousePos();
            const float PICK_RADIUS = 10.0f;
            std::string hitId;
            float bestDistSq = PICK_RADIUS * PICK_RADIUS;
            for (const auto& node : ss.g_recordedNodes)
            {
                ImVec2 pos = WorldToRadar(node.x, node.y);
                float dx = mouse.x - pos.x;
                float dy = mouse.y - pos.y;
                float distSq = dx * dx + dy * dy;
                if (distSq <= bestDistSq)
                {
                    bestDistSq = distSq;
                    hitId = node.id;
                }
            }
            if (!hitId.empty())
            {
                if (ImGui::GetIO().KeyCtrl)
                    DeleteGraphNode(ss, hitId);
                else
                    ss.activeNode = hitId;
            }
        }

        ImGui::Dummy(ImVec2(radarSize, radarSize));
        ImGui::EndChild();

        ImGui::End();
    }
    
}

#pragma endregion

#pragma region - WinAPI -

LRESULT CALLBACK hkWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

static bool s_deviceLost = false;

HRESULT __stdcall hkPresent(IDirect3DDevice9* device, CONST RECT* pSrcRect, CONST RECT* pDestRect, HWND hDestWindow, CONST RGNDATA* pDirtyRegion)
{
    if (s_deviceLost)
        return oPresent(device, pSrcRect, pDestRect, hDestWindow, pDirtyRegion);

    if (!initialized)
    {
        D3DDEVICE_CREATION_PARAMETERS params;
        device->GetCreationParameters(&params);
        g_gameHwnd = params.hFocusWindow;
        ImGui::CreateContext();
        SetupImGuiStyle();
        ImGuiIO& io = ImGui::GetIO();
        io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;

        // Load watermark
        ImFontConfig cfg;
        cfg.OversampleH = 3;
        cfg.OversampleV = 3;
        cfg.PixelSnapH = false;
        g_fontWatermark = io.Fonts->AddFontFromFileTTF(
            "C:\\Windows\\Fonts\\arial.ttf", 15.0f, &cfg);
        ImGui_ImplWin32_Init(params.hFocusWindow);
        ImGui_ImplDX9_Init(device);
        oWndProc = (WNDPROC)GetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC);
        SetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC, (LONG)hkWndProc);
        {
            char exePath[MAX_PATH];
            GetModuleFileNameA(NULL, exePath, MAX_PATH);
            std::string dir(exePath);
            dir = dir.substr(0, dir.find_last_of("\\/"));
            std::wstring clientDir(dir.begin(), dir.end());
            g_pk2.Open(clientDir + L"\\media.pk2");
            g_dataPk2.Open(clientDir + L"\\data.pk2");
        }
        g_iconCache = new IconCache(device, g_pk2);
        SROSkin_Init(g_iconCache);
        SROSkin_InitSound(&g_dataPk2, params.hFocusWindow);
        g_rewardWindow.iconSource = g_iconCache;
        g_soxOverlay.texture = g_iconCache->Get("icon/item/etc/icon_edge_rare.ddj");
        initialized = true;
    }
    
    if (!ImGui::GetIO().WantTextInput)
    {
        if (GetAsyncKeyState(Settings::showSessionStatsKey) & 1) showSessionStatsWindow = !showSessionStatsWindow;
        if (GetAsyncKeyState(Settings::showAdminToolsKey) & 1) showAdminToolsWindow = !showAdminToolsWindow;
        if (GetAsyncKeyState(Settings::showSettingsKey) & 1) showSettingsWindow = !showSettingsWindow;
        if (GetAsyncKeyState(Settings::showBotWindow) & 1) showBotWindow = !showBotWindow;

        if (GetAsyncKeyState(Settings::showAchKey) & 1) {
            if (g_achWindow.isOpen) {
                g_achWindow.Close();
            }
            else {
                NetActions::SendAchievementsRequest();
            }
        }
    }

    ImGui_ImplDX9_NewFrame();
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();
    SROSkin_NewFrame();
    if (Settings::showWatermark) RenderWatermark("V1.201 BETA - @Dewwta");
    if (Settings::showFPSCounter) RenderFPS();

    if (showSessionStatsWindow) RenderSessionStats();
    if (showAdminToolsWindow)   RenderAdminTools();
    if (showSettingsWindow)     RenderSettings();
    if (showBotWindow)          RenderBotWindow();
    if (showLatticeInspector)   RenderLatticeInspector();
    if (showSkinTest)           RenderSkinTest();

    if (showAdminToolsWindow && !g_bridge.m_state.charName.empty()) {
        RenderRadar(
            device,
            g_bridge.m_sessionState.WorldX,
            g_bridge.m_sessionState.WorldY,
            g_bridge.m_sessionState.activeNode);
    }
    g_rewardWindow.Render();
    g_achWindow.Render();
    ImGui::EndFrame();
    ImGui::Render();
    ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());
    
    return oPresent(device, pSrcRect, pDestRect, hDestWindow, pDirtyRegion);
}

HRESULT __stdcall hkReset(IDirect3DDevice9* device, D3DPRESENT_PARAMETERS* pp)
{
    auto& log = GetLogger();
    static int s_resetCount = 0;
    s_resetCount++;
    ImGui_ImplDX9_InvalidateDeviceObjects();
    HRESULT hr = oReset(device, pp);
    if (SUCCEEDED(hr))
    {
        s_deviceLost = false;
        ImGui_ImplDX9_CreateDeviceObjects();
        log.Info("hkReset", "Reset #" + std::to_string(s_resetCount) + " succeeded (hr=" + std::to_string(hr) + ")");
    }
    else
    {
        s_deviceLost = true;
        log.Warn("hkReset", "Reset #" + std::to_string(s_resetCount) + " failed (hr=" + std::to_string(hr) + "), marking device lost");
    }
    return hr;
}

HRESULT __stdcall hkCreateDevice(
    IDirect3D9* d3d,
    UINT adapter,
    D3DDEVTYPE devType,
    HWND hwnd,
    DWORD flags,
    D3DPRESENT_PARAMETERS* pp,
    IDirect3DDevice9** outDevice)
{
    if (pp)
    {
        pp->PresentationInterval = D3DPRESENT_INTERVAL_IMMEDIATE;
    }
    HRESULT hr = oCreateDevice(d3d, adapter, devType, hwnd, flags, pp, outDevice);
    if (SUCCEEDED(hr) && outDevice && *outDevice)
    {
        void** vtable = *reinterpret_cast<void***>(*outDevice);
        // Hook Present (index 17)
        oPresent = reinterpret_cast<Present_t>(vtable[17]);
        DWORD oldProtect;
        VirtualProtect(&vtable[17], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect);
        vtable[17] = reinterpret_cast<void*>(hkPresent);
        VirtualProtect(&vtable[17], sizeof(void*), oldProtect, &oldProtect);
        // Hook Reset (index 16)
        oReset = reinterpret_cast<Reset_t>(vtable[16]);
        VirtualProtect(&vtable[16], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect);
        vtable[16] = reinterpret_cast<void*>(hkReset);
        VirtualProtect(&vtable[16], sizeof(void*), oldProtect, &oldProtect);
    }
    return hr;
}

IDirect3D9* __stdcall hkDirect3DCreate9(UINT sdkVersion)
{
    IDirect3D9* d3d = oCreate(sdkVersion);
    if (d3d)
    {
        // We do a little trolling
        void** vtable = *reinterpret_cast<void***>(d3d);
        oCreateDevice = reinterpret_cast<CreateDevice_t>(vtable[16]);
        DWORD oldProtect;
        VirtualProtect(&vtable[16], sizeof(void*), PAGE_EXECUTE_READWRITE, &oldProtect);
        vtable[16] = reinterpret_cast<void*>(hkCreateDevice);
        VirtualProtect(&vtable[16], sizeof(void*), oldProtect, &oldProtect);
    }
    return d3d;
}

LRESULT CALLBACK hkWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (AnyWindowOpen()) {
        ImGui_ImplWin32_WndProcHandler(hwnd, msg, wParam, lParam);
        ImGuiIO& io = ImGui::GetIO();
        if (io.WantCaptureMouse) {
            switch (msg) {
            case WM_LBUTTONDOWN:
            case WM_LBUTTONUP:
            case WM_RBUTTONDOWN:
            case WM_RBUTTONUP:
            case WM_MOUSEMOVE:
            case WM_MOUSEWHEEL:
                return 0;
            }
        }
        if (io.WantCaptureKeyboard) {
            switch (msg) {
            case WM_KEYDOWN:
            case WM_KEYUP:
            case WM_CHAR:
                return 0;
            }
        }
    }
    switch (msg)
    {
    case WM_KILLFOCUS:
        if (Settings::keepFocused)
            return 0;
        break;
    case WM_ACTIVATE:
        if (Settings::keepFocused && LOWORD(wParam) == WA_INACTIVE)
            return CallWindowProcA(oWndProc, hwnd, WM_ACTIVATE,
                MAKEWPARAM(WA_ACTIVE, 0), lParam);
        break;
    case WM_ACTIVATEAPP:
        if (Settings::keepFocused && wParam == FALSE)
            return CallWindowProcA(oWndProc, hwnd, WM_ACTIVATEAPP, TRUE, lParam);
        break;
    }
    return CallWindowProcA(oWndProc, hwnd, msg, wParam, lParam);
}

#pragma endregion

#pragma region - Init -

void dx9_hook::init()
{
    auto& log = GetLogger();
    HMODULE d3d9 = GetModuleHandleA("d3d9.dll");
    void* create9addr = GetProcAddress(d3d9, "Direct3DCreate9");
    log.Info("dx9_hook::init", "Installing D3d9 hook");
    MH_Initialize();
    MH_CreateHook(create9addr, hkDirect3DCreate9,
        reinterpret_cast<void**>(&oCreate));
    MH_EnableHook(create9addr);
    log.Info("dx9_hook::init", "Installing login hook");
    InstallLoginHook();

    bool isLauncherRequired = true;
    if (!isLauncherRequired)
    {
       
        WriteMemoryValue<uint8_t>(0x008329EB, 0xEB); // jne -> jmp
        
        WriteMemoryValue<uint8_t>(0x00830C67, 0xEB); // je -> jmp
    }
    // Skip mutex check for "Silkroad Client" already executed
    WriteMemoryValue<uint8_t>(0x0083297F, 0xEB); // jne -> jmp



}

#pragma endregion