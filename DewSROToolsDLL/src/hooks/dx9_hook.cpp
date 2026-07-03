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
#include <algorithm>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <iostream>
#include "../mem/Process.h"
#include "../client/pk2/Pk2Reader.h"
#include "../client/pk2/IconCache.h"
#include "../client/SROSkinWindow.h"

static Pk2Reader  g_pk2;       // media.pk2
static Pk2Reader  g_dataPk2;   // data.pk2
static IconCache* g_iconCache = nullptr;

#pragma region - Region Loading/Saving -

// DUPLICATED, REMOVE, I HATE THIS SHIT
static int DetectStoneCaveFloor(float worldZ)
{
    if (worldZ < 116.f)  return 1;
    if (worldZ < 254.f)  return 2;
    if (worldZ < 393.f)  return 3;
    return 4;
}

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

        // g_currentFrame is incremented at the TOP of RenderRadar, before this runs.
        // RenderBotWindow always renders before RenderRadar, so its tiles are stamped
        // with (g_currentFrame - 1). Protect any tile used this rendering frame or the
        // immediately preceding stamp. their texture pointers are live in ImDrawList
        // commands that haven't been submitted to the GPU yet.
        if (tile.lastUsedFrame + 1 >= g_currentFrame)
        {
            ++it;
            continue;
        }

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
static std::string g_clientVersion;
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
static bool showSkinTest            = false;
static bool showLogsWindow          = false;

static bool AnyWindowOpen() {
    return showSessionStatsWindow || showAdminToolsWindow || showSettingsWindow || g_rewardWindow.isOpen || g_achWindow.isOpen || showBotWindow || showLogsWindow;
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

// SV.T structure: [uint32_t LE length (plain-text)] [Blowfish ECB encrypted payload].
// Encryption uses the JoyMax LE Blowfish variant (same as PK2), key = first 8 bytes
// of "SILKROADVERSION" = "SILKROAD".
static void ReadClientVersion()
{
    if (!g_pk2.IsOpen()) return;

    std::vector<uint8_t> buf;
    if (!g_pk2.ReadFile("SV.T", buf) || buf.size() < 8) return;

    // Bytes 0-3: plain-text LE uint32 — length of the version string.
    uint32_t strLen = uint32_t(buf[0])
                    | (uint32_t(buf[1]) << 8)
                    | (uint32_t(buf[2]) << 16)
                    | (uint32_t(buf[3]) << 24);

    if (strLen == 0 || strLen > 64) return;  // sanity: version strings are short

    // Bytes 4+: Blowfish ECB encrypted payload.
    Blowfish bf;
    static const char kSvKey[] = "SILKROAD";
    bf.SetKey(reinterpret_cast<const uint8_t*>(kSvKey), 8);

    uint8_t* payload    = buf.data() + 4;
    size_t   payloadLen = (buf.size() - 4) & ~size_t(7);  // round to 8-byte boundary
    bf.DecryptEcb(payload, payloadLen);

    if (strLen <= payloadLen) {
        const char* vstart = reinterpret_cast<const char*>(payload);
        // strLen may include null-padding inside the Blowfish block; trim to the real string.
        size_t vlen = strnlen(vstart, strLen);
        g_clientVersion = std::string(vstart, vlen);
    }
}

static void RenderWatermark(const char* text)
{
    ImGuiIO& io = ImGui::GetIO();
    if (g_fontWatermark) ImGui::PushFont(g_fontWatermark);
    ImVec2 textSize = ImGui::CalcTextSize(text);
    if (g_fontWatermark) ImGui::PopFont();

    const float padX = 6.0f, padY = 4.0f;

    ImVec2 winSize = ImVec2(textSize.x + padX * 2.f, textSize.y + padY * 2.f);
    ImVec2 pos = ImVec2(io.DisplaySize.x - winSize.x - 2.f,
                        io.DisplaySize.y - winSize.y - 2.f);

    ImGui::SetNextWindowPos(pos, ImGuiCond_Always);
    ImGui::SetNextWindowSize(winSize, ImGuiCond_Always);
    ImGui::SetNextWindowBgAlpha(0.0f);
    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoDecoration |
        ImGuiWindowFlags_NoInputs |
        ImGuiWindowFlags_NoMove |
        ImGuiWindowFlags_NoSavedSettings |
        ImGuiWindowFlags_NoFocusOnAppearing |
        ImGuiWindowFlags_NoNav |
        ImGuiWindowFlags_NoBackground;
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(padX, padY));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);
    ImGui::Begin("##watermark", nullptr, flags);
    if (g_fontWatermark) ImGui::PushFont(g_fontWatermark);
    ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 0.8f), text);
    if (g_fontWatermark) ImGui::PopFont();
    ImGui::End();
    ImGui::PopStyleVar(2);
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
    
    PlayerState ps = g_bridge.m_state;
    State& ss = g_bridge.m_sessionState;
    if (ps.charName.empty()) return;

    if (!BeginSROWindow("##session_stats", "Session Stats", &showSessionStatsWindow,
                        { 320.f, 500.f }, { 20.f, 20.f }, { 280.f, 200.f }, { 520.f, 720.f }))
        return;

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
        ImGui::TextColored(ImVec4(1.0f, 0.86f, 0.59f, 0.8f), "PENDING REWARDS");
        ImGui::Separator();
        ImGui::Spacing();

        const float btnH     = 22.0f;
        const float padX     = 10.0f;
        const float lineW    = ImGui::GetContentRegionAvail().x;
        float cursorX        = ImGui::GetCursorPosX();
        const float startX   = cursorX;
        bool  firstOnLine    = true;

        for (int lvl : g_bridge.unclaimedRewards)
        {
            char label[16];  snprintf(label, sizeof(label), "Lv %d", lvl);
            char btnId[32];  snprintf(btnId, sizeof(btnId), "##rw%d", lvl);
            const float btnW = ImGui::CalcTextSize(label).x + padX * 2.0f;

            if (!firstOnLine && (cursorX + btnW > startX + lineW))
            {
                ImGui::NewLine();
                cursorX = startX;
                firstOnLine = true;
            }
            if (!firstOnLine) { ImGui::SameLine(0.0f, 4.0f); cursorX += btnW + 4.0f; }
            else              { cursorX += btnW; }
            firstOnLine = false;

            if (SROButton(btnId, label, btnW, btnH))
                g_bridge.Send("{\"type\":\"reward_reopen\",\"level\":" + std::to_string(lvl) + "}");
        }
        ImGui::NewLine();
    }

    EndSROWindow();
}

// GM-only log viewer, opened from Admin Tools.
static void RenderLogsWindow() {
    if (!g_bridge.m_sessionState.isGM) return;

    ImGui::SetNextWindowSize(ImVec2(640, 380), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(380, 200), ImVec2(1400, 900));
    if (!ImGui::Begin("Logs", &showLogsWindow)) { ImGui::End(); return; }

    static char s_filter[128] = {};
    static bool s_autoScroll  = true;
    static bool s_showInfo = true, s_showWarn = true, s_showErr = true, s_showDbg = true;

    // Controls row
    if (ImGui::Button("Clear")) GetLogger().Clear();
    ImGui::SameLine();
    ImGui::Checkbox("Auto-scroll", &s_autoScroll);
    ImGui::SameLine(); ImGui::Checkbox("Info", &s_showInfo);
    ImGui::SameLine(); ImGui::Checkbox("Warn", &s_showWarn);
    ImGui::SameLine(); ImGui::Checkbox("Err",  &s_showErr);
    ImGui::SameLine(); ImGui::Checkbox("Dbg",  &s_showDbg);
    ImGui::SetNextItemWidth(-1);
    ImGui::InputTextWithHint("##logfilter", "Filter (location or message)...",
                             s_filter, sizeof(s_filter));

    ImGui::Separator();

    ImGui::BeginChild("##logscroll", ImVec2(0, 0), false,
                      ImGuiWindowFlags_HorizontalScrollbar);
    {
        auto& logger = GetLogger();
        std::lock_guard<std::mutex> lock(logger.Mutex());

        const bool levelOn[4] = { s_showInfo, s_showWarn, s_showErr, s_showDbg };
        std::string fl = s_filter;
        std::transform(fl.begin(), fl.end(), fl.begin(), ::tolower);

        // Collect visible entries so the clipper works over a stable index range
        static std::vector<const LogEntry*> s_visible;
        s_visible.clear();
        for (const auto& en : logger.Entries()) {
            if (en.level < 0 || en.level > 3 || !levelOn[en.level]) continue;
            if (!fl.empty()) {
                std::string hay = en.loc + " " + en.msg;
                std::transform(hay.begin(), hay.end(), hay.begin(), ::tolower);
                if (hay.find(fl) == std::string::npos) continue;
            }
            s_visible.push_back(&en);
        }

        static const ImVec4 kLevelCol[4] = {
            { 0.35f, 0.80f, 1.00f, 1.f },   // INFO — cyan
            { 1.00f, 0.85f, 0.30f, 1.f },   // WARN — yellow
            { 1.00f, 0.35f, 0.35f, 1.f },   // ERR  — red
            { 0.60f, 0.60f, 0.60f, 1.f },   // DBG  — grey
        };
        static const char* kLevelTag[4] = { "INFO", "WARN", "ERR ", "DBG " };

        ImGuiListClipper clipper;
        clipper.Begin((int)s_visible.size());
        while (clipper.Step()) {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                const LogEntry& en = *s_visible[i];
                ImGui::TextColored(kLevelCol[en.level], "[%s] [%s] %s",
                                   kLevelTag[en.level], en.loc.c_str(), en.msg.c_str());
            }
        }
        clipper.End();

        if (s_autoScroll && ImGui::GetScrollY() >= ImGui::GetScrollMaxY() - 4.f)
            ImGui::SetScrollHereY(1.0f);
    }
    ImGui::EndChild();

    ImGui::End();
}

static void RenderAdminTools() {
    PlayerState ps = g_bridge.m_state;
    State& ss = g_bridge.m_sessionState;
    if (ps.charName.empty()) return;

    ImGui::SetNextWindowSize(ImVec2(340, 0), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(660, 20), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(300, 100), ImVec2(560, 760));
    ImGui::Begin("Admin Tools", &showAdminToolsWindow);

    if (!ss.isGM)
    {
        ImGui::TextDisabled("GM session required.");
        ImGui::End();
        return;
    }

    if (ImGui::Button(showLogsWindow ? "Hide Logs" : "Show Logs", ImVec2(-1, 24)))
        showLogsWindow = !showLogsWindow;
    ImGui::Spacing();

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

static void RenderBotWindow_Legacy() {

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

static void RenderBotWindow(IDirect3DDevice9* device)
{
    PlayerState ps = g_bridge.m_state;
    State& ss = g_bridge.m_sessionState;
    if (ps.charName.empty()) return;

    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));

    if (!BeginSROWindow("##botwndtest", "Bot Control", &showBotWindow,
            ImVec2(820.f, 560.f), ImVec2(-1.f, -1.f),
            ImVec2(500.f, 380.f), ImVec2(1400.f, 900.f), false))
        return;

    static int  walkX = 0, walkY = 0, walkZ = 0, walkR = 25, regionID = 0;

    if (ss.hasSavedBotConfig) {
        walkX    = ss.savedBotX;    walkY  = ss.savedBotY;
        walkZ    = ss.savedBotZ;    walkR  = ss.savedBotR;
        regionID = ss.savedBotRegionId;
        ss.hasSavedBotConfig = false;
    }

    struct DefaultPreset { const char* name; int regionId, x, y, z, r; };
    static const DefaultPreset kDefPresets[] = {
        { "Black Robber Den (SE)",        23700,  2671,   142,  207, 75 },
        { "Huns Garrison",                26737, -4124,  2430,  195, 75 },
        { "Stone Cave F3",               0x8001, 24533, 24403,  284, 75 },
        { "Desert Of Mysterious Death",   25731,  -727,  1645,  -42, 75 },
        { "Roc Mtn. Forest",             24431, -4520,   699, 2026, 40 },
        { "Beak Peak, Roc Mtn.",         22388, -3536,  -781, 3379, 45 },
        { "Qin-Shi B4",                 0x8004, 24407, 24561,  -33, 50 },
    };
    struct UserPreset { char name[64]; int x, y, z, r, regionID; };
    static std::vector<UserPreset> kUsrPresets;
    static int   selectedPreset  = -1;
    static char  presetFilter[64] = {};
    static bool  savingPreset    = false;
    static char  newPresetName[64] = {};

    // State colour
    const char* stateStr = ss.botStateLabel.empty() ? "Idle" : ss.botStateLabel.c_str();
    ImVec4 stateColor = { 0.6f, 0.6f, 0.6f, 1.f };
    if      (ss.botStateLabel == "WalkingToTrainplace") stateColor = { 0.3f, 0.7f, 1.0f, 1.f };
    else if (ss.botStateLabel == "Training")            stateColor = { 0.3f, 1.0f, 0.4f, 1.f };
    else if (ss.botStateLabel == "Teleporting")         stateColor = { 0.8f, 0.4f, 1.0f, 1.f };
    else if (ss.botStateLabel == "Returning")           stateColor = { 1.0f, 0.8f, 0.2f, 1.f };
    else if (ss.botStateLabel == "Dead")                stateColor = { 1.0f, 0.3f, 0.3f, 1.f };
    const ImU32 stateU32 = ImGui::ColorConvertFloat4ToU32(stateColor);

    // Tab bar
    static const char* kTabs[] = { "Bot", "Skills", "Settings" };
    static int s_tab = 0;
    const float availW = ImGui::GetContentRegionAvail().x;
    const float availH = ImGui::GetContentRegionAvail().y;
    const float bodyH  = availH - K_TAB_H - ImGui::GetStyle().ItemSpacing.y;
    SROTabBar("##bwt_tabs", kTabs, 3, &s_tab, 90.f);

    if (s_tab == 0)
    {
        const float sp     = ImGui::GetStyle().ItemSpacing.x;
        const float sp2    = ImGui::GetStyle().ItemSpacing.y;
        const float leftW  = availW * 0.40f - sp * 0.5f;
        const float rightW = availW - leftW - sp;

        
        ImGui::BeginChild("##bwt_left", { leftW, bodyH }, false,
                          ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
        {
            const float rowW = ImGui::GetContentRegionAvail().x;
            ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "STATUS");
            ImGui::Spacing();

            char killsBuf[32], distBuf[32], targetBuf[32];
            snprintf(killsBuf,  sizeof(killsBuf),  "%d",   ss.sessionKills);
            snprintf(distBuf,   sizeof(distBuf),   "%.1f m", ss.distanceToTarget);
            if (ss.lastTargetUid)
                snprintf(targetBuf, sizeof(targetBuf), "%d", ss.lastTargetUid);
            else
                snprintf(targetBuf, sizeof(targetBuf), "None");

            SROStatusRow("State",    stateStr,  rowW, stateU32);
            ImGui::Dummy({ 0.f, 2.f });
            SROStatusRow("Region",   ss.curRegionName.empty() ? "-" : ss.regionName, rowW);
            ImGui::Dummy({ 0.f, 2.f });
            SROStatusRow("Kills",    killsBuf,  rowW);
            ImGui::Dummy({ 0.f, 2.f });
            SROStatusRow("Distance", distBuf,   rowW);
            ImGui::Dummy({ 0.f, 2.f });
            SROStatusRow("Target",   targetBuf, rowW);

            ImGui::Spacing();
            ImGui::Spacing();

            const bool botRunning = !ss.botStateLabel.empty() && ss.botStateLabel != "Idle";
            const float halfW = (ImGui::GetContentRegionAvail().x - sp) * 0.5f;
            if (SROButton("##bwt_start", "Start", halfW, 28.f, botRunning))
                NetActions::SendStartBotRequest(walkX, walkY, walkZ, walkR, regionID);
            ImGui::SameLine();
            if (SROButton("##bwt_stop", "Stop", halfW, 28.f, !botRunning))
                NetActions::SendStopBotRequest();

            // Trainplace minimap
            {
                static float s_tpZoom       = 0.35f;
                static float s_tpZoomTarget = 0.35f;
                static int   s_charLastX = 0, s_charLastY = 0;
                static float s_charAngle = 0.0f;

                // Textures loaded once when device is first available
                static IDirect3DTexture9* s_locPin       = nullptr;
                static IDirect3DTexture9* s_mmZoomIn     = nullptr;
                static IDirect3DTexture9* s_mmZoomInFoc  = nullptr;
                static IDirect3DTexture9* s_mmZoomInPrs  = nullptr;
                static IDirect3DTexture9* s_mmZoomOut    = nullptr;
                static IDirect3DTexture9* s_mmZoomOutFoc = nullptr;
                static IDirect3DTexture9* s_mmZoomOutPrs = nullptr;
                static IDirect3DTexture9* s_mmMonster    = nullptr;
                static IDirect3DTexture9* s_mmCharacter  = nullptr;
                static float s_mmBtnSz  = 30.f;
                static float s_mmMobSz  = 16.f;
                static float s_mmCharSz = 16.f;
                static bool  s_locInit = false;
                if (!s_locInit && device) {
                    s_locPin       = LoadDDJFromPk2(device, "interface\\worldmap\\wmap_sign_location.ddj");
                    s_mmZoomIn     = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomin.ddj");
                    s_mmZoomInFoc  = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomin_focus.ddj");
                    s_mmZoomInPrs  = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomin_press.ddj");
                    s_mmZoomOut    = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomout.ddj");
                    s_mmZoomOutFoc = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomout_focus.ddj");
                    s_mmZoomOutPrs = LoadDDJFromPk2(device, "interface\\minimap\\mm_zoomout_press.ddj");
                    s_mmMonster    = LoadDDJFromPk2(device, "interface\\minimap\\mm_sign_monster.ddj");
                    s_mmCharacter  = LoadDDJFromPk2(device, "interface\\minimap\\mm_sign_character.ddj");
                    if (s_mmZoomIn) {
                        D3DSURFACE_DESC bd{};
                        if (SUCCEEDED(s_mmZoomIn->GetLevelDesc(0, &bd)) && bd.Width > 0)
                            s_mmBtnSz = (float)bd.Width;
                    }
                    if (s_mmMonster) {
                        D3DSURFACE_DESC md{};
                        if (SUCCEEDED(s_mmMonster->GetLevelDesc(0, &md)) && md.Width > 0)
                            s_mmMobSz = (float)md.Width;
                    }
                    if (s_mmCharacter) {
                        D3DSURFACE_DESC cd{};
                        if (SUCCEEDED(s_mmCharacter->GetLevelDesc(0, &cd)) && cd.Width > 0)
                            s_mmCharSz = (float)cd.Width;
                    }
                    s_locInit = true;
                }

                ImGui::Spacing();
                const float mapW      = ImGui::GetContentRegionAvail().x;
                const float mapFrameH = ImGui::GetContentRegionAvail().y;
                const float K_ZBTN_W  = s_mmBtnSz;

                if (mapFrameH > 40.f) {
                    // Subframe fills all but the zoom button column
                    const float  subW     = mapW - K_ZBTN_W - sp;
                    const ImVec2 sfOrigin = ImGui::GetCursorScreenPos();
                    const bool   mapVis   = SROBeginSubFrame("##tp_map", subW, mapFrameH);
                    if (mapVis) {
                        // Canvas bounds derived from outer frame coords so the map is flush
                        // against the inner edge of all four border strips (K_SF_C each side).
                        const float  canvasW = subW      - 2.f * K_SF_C;
                        const float  canvasH = mapFrameH - 2.f * K_SF_C;

                        // Minimum zoom: 5 tile-widths (960 wu) must cover each canvas half
                        // so the tile grid never reveals black edges.
                        const float  zMin    = (std::max)(canvasW, canvasH) / (2.f * 5.f * 192.f);
                        s_tpZoomTarget = (std::max)(s_tpZoomTarget, zMin);

                        // Mouse-wheel zoom while hovering the map canvas
                        {
                            const float wheel = ImGui::GetIO().MouseWheel;
                            if (wheel != 0.f && ImGui::IsWindowHovered() &&
                                ImGui::IsMouseHoveringRect(
                                    { sfOrigin.x + K_SF_C,        sfOrigin.y + K_SF_C },
                                    { sfOrigin.x + subW - K_SF_C, sfOrigin.y + mapFrameH - K_SF_C }))
                            {
                                s_tpZoomTarget = (std::min)((std::max)(
                                    s_tpZoomTarget * powf(1.35f, wheel), zMin), 2.0f);
                            }
                        }
                        // Exponential smooth approach toward target (~90% in 0.13 s at 60 fps)
                        const float dt = ImGui::GetIO().DeltaTime;
                        s_tpZoom = s_tpZoomTarget + (s_tpZoom - s_tpZoomTarget) * expf(-18.f * dt);
                        s_tpZoom = (std::max)(s_tpZoom, zMin);

                        const ImVec2 cTL = { sfOrigin.x + K_SF_C,        sfOrigin.y + K_SF_C };
                        const ImVec2 cBR = { sfOrigin.x + subW - K_SF_C, sfOrigin.y + mapFrameH - K_SF_C };
                        const ImVec2 ctr = { cTL.x + canvasW * 0.5f,     cTL.y + canvasH * 0.5f };

                        ImDrawList* dl = ImGui::GetWindowDrawList();
                        dl->PushClipRect(cTL, cBR, true);
                        dl->AddRectFilled(cTL, cBR, IM_COL32(10, 11, 16, 255));

                        const bool isDungeon = (regionID & 0x8000) != 0;
                        const int  tpSX = isDungeon
                            ? (int)floorf((float)walkX / 192.f)
                            : (int)floorf((float)walkX / 192.f) + 135;
                        const int  tpSY = isDungeon
                            ? (int)floorf((float)walkY / 192.f)
                            : (int)floorf((float)walkY / 192.f) + 92;

                        auto W2M = [&](int wx, int wy) -> ImVec2 {
                            return { ctr.x + (wx - walkX) * s_tpZoom,
                                     ctr.y - (wy - walkY) * s_tpZoom };
                        };

                        if (!isDungeon) {
                            // Dynamic radius: enough tiles to fill the canvas, capped at 5
                            // (5 sectors guarantee ≥960 wu coverage in every direction).
                            const int rX = (std::min)(
                                (int)ceilf(canvasW * 0.5f / (192.f * s_tpZoom)) + 1, 5);
                            const int rY = (std::min)(
                                (int)ceilf(canvasH * 0.5f / (192.f * s_tpZoom)) + 1, 5);
                            for (int dsy = -rY; dsy <= rY; dsy++) {
                                for (int dsx = -rX; dsx <= rX; dsx++) {
                                    const int sx    = tpSX + dsx, sy = tpSY + dsy;
                                    const int wMinX = (sx - 135) * 192;
                                    const int wMinY = (sy -  92) * 192;
                                    const ImVec2 p1 = W2M(wMinX,       wMinY);
                                    const ImVec2 p2 = W2M(wMinX + 192, wMinY + 192);
                                    const float  sL = (std::min)(p1.x, p2.x);
                                    const float  sR = (std::max)(p1.x, p2.x);
                                    const float  sT = (std::min)(p1.y, p2.y);
                                    const float  sB = (std::max)(p1.y, p2.y);
                                    if (sR < cTL.x || sL > cBR.x || sB < cTL.y || sT > cBR.y) continue;
                                    MinimapTile* tile = GetOrLoadTile(device, sx, sy);
                                    if (tile && tile->texture)
                                        dl->AddImage(ImTextureRef((ImTextureID)(uintptr_t)tile->texture),
                                                     p1, p2, { 0.f, 1.f }, { 1.f, 0.f });
                                }
                            }
                        } else {
                            // Identify dungeon type and floor from regionID
                            const int rID = regionID & 0xFFFF;
                            std::string dgFolder, dgPrefix;
                            int dgFloor = 0;
                            if (rID == 0x8001) {
                                // Stone Cave — floor derived from saved Z position
                                dgFolder = "donwhang";
                                dgPrefix = "dh_a01";
                                dgFloor  = DetectStoneCaveFloor((float)walkZ);
                            } else if (rID >= 0x8004 && rID <= 0x8007) {
                                // Qin-Shi Tomb — B1=0x8007 … B4=0x8004, floor = 8-(rID&0xF)
                                dgFolder = "jinsi";
                                dgPrefix = "qt_a01";
                                dgFloor  = 8 - (rID & 0x0F);
                            }

                            if (!dgFolder.empty()) {
                                const int rX = (std::min)(
                                    (int)ceilf(canvasW * 0.5f / (192.f * s_tpZoom)) + 1, 5);
                                const int rY = (std::min)(
                                    (int)ceilf(canvasH * 0.5f / (192.f * s_tpZoom)) + 1, 5);
                                for (int dsy = -rY; dsy <= rY; dsy++) {
                                    for (int dsx = -rX; dsx <= rX; dsx++) {
                                        const int sx    = tpSX + dsx, sy = tpSY + dsy;
                                        // Dungeon tiles use raw sector coords (no overworld offset)
                                        const int wMinX = sx * 192;
                                        const int wMinY = sy * 192;
                                        const ImVec2 p1 = W2M(wMinX,       wMinY);
                                        const ImVec2 p2 = W2M(wMinX + 192, wMinY + 192);
                                        const float  sL = (std::min)(p1.x, p2.x);
                                        const float  sR = (std::max)(p1.x, p2.x);
                                        const float  sT = (std::min)(p1.y, p2.y);
                                        const float  sB = (std::max)(p1.y, p2.y);
                                        if (sR < cTL.x || sL > cBR.x || sB < cTL.y || sT > cBR.y) continue;
                                        MinimapTile* tile = GetOrLoadTile(device, sx, sy,
                                                                          dgFolder, dgPrefix, dgFloor);
                                        if (tile && tile->texture)
                                            dl->AddImage(ImTextureRef((ImTextureID)(uintptr_t)tile->texture),
                                                         p1, p2, { 0.f, 1.f }, { 1.f, 0.f });
                                    }
                                }
                            } else {
                                // Unknown dungeon — keep text fallback
                                const char*  msg = "Dungeon N/A";
                                const ImVec2 ts  = ImGui::CalcTextSize(msg);
                                dl->AddText({ ctr.x - ts.x * 0.5f, ctr.y - ts.y * 0.5f },
                                            IM_COL32(110, 110, 110, 200), msg);
                            }
                        }

                        // Monster sprites
                        {
                            const int n = ss.nearbyMobCount;
                            const float hs = s_mmMobSz * 0.5f;
                            for (int mi = 0; mi < n; mi++) {
                                const ImVec2 mp = W2M(ss.nearbyMobs[mi].x, ss.nearbyMobs[mi].y);
                                if (mp.x + hs < cTL.x || mp.x - hs > cBR.x ||
                                    mp.y + hs < cTL.y || mp.y - hs > cBR.y) continue;
                                if (s_mmMonster)
                                    dl->AddImage(ImTextureRef((ImTextureID)(uintptr_t)s_mmMonster),
                                                 { mp.x - hs, mp.y - hs }, { mp.x + hs, mp.y + hs });
                                else
                                    dl->AddCircleFilled(mp, 4.f, IM_COL32(220, 40, 40, 200));
                            }
                        }

                        // Location pin
                        if (s_locPin) {
                            D3DSURFACE_DESC desc{};
                            if (SUCCEEDED(s_locPin->GetLevelDesc(0, &desc)) && desc.Height > 0) {
                                const int   nFr = (desc.Width > desc.Height)
                                                  ? (int)(desc.Width / desc.Height) : 1;
                                const float t   = fmodf((float)ImGui::GetTime(), 0.5f) / 0.5f;
                                const int   frm = (int)(t * nFr) % nFr;
                                const float u0  = frm       / (float)nFr;
                                const float u1  = (frm + 1) / (float)nFr;
                                const float pH  = (float)desc.Height;

                                // DROP ME
                                const float pDrop = 6.f;
                                const ImVec2 pTL = { ctr.x - pH * 0.5f, ctr.y - pH + pDrop };
                                const ImVec2 pBR = { ctr.x + pH * 0.5f, ctr.y      + pDrop };
                                dl->AddImage(ImTextureRef((ImTextureID)(uintptr_t)s_locPin),
                                             pTL, pBR, { u0, 0.f }, { u1, 1.f });
                            }
                        } else {
                            const float a = (sinf((float)ImGui::GetTime() * 4.f) + 1.f) * 0.5f;
                            dl->AddCircleFilled(ctr, 5.f, IM_COL32(255, 60, 60, (int)(a * 220.f)));
                        }

                        // Player character sprite
                        if (s_mmCharacter) {
                            const int   cx = ss.WorldX, cy = ss.WorldY;
                            const float dx = (float)(cx - s_charLastX);
                            const float dy = (float)(cy - s_charLastY);
                            if (dx * dx + dy * dy > 1.0f) {
                                s_charAngle = atan2f(-dy, dx); // Y negated: world-north = screen-up
                                s_charLastX = cx;
                                s_charLastY = cy;
                            }
                            const ImVec2 sp = W2M(cx, cy);
                            const float  hs = s_mmCharSz * 0.5f;
                            const float  c  = cosf(s_charAngle), si = sinf(s_charAngle);
                            auto rp = [&](float ox, float oy) -> ImVec2 {
                                return { sp.x + c * ox - si * oy, sp.y + si * ox + c * oy };
                            };
                            dl->AddImageQuad(
                                ImTextureRef((ImTextureID)(uintptr_t)s_mmCharacter),
                                rp(-hs, -hs), rp(hs, -hs), rp(hs, hs), rp(-hs, hs));
                        }

                        dl->PopClipRect();
                        ImGui::Dummy({ canvasW, canvasH });
                    }
                    SROEndSubFrame();

                    // 3-state DDJ zoom buttons (normal / hover / pressed)
                    auto DDJBtn = [&](const char* id,
                                      IDirect3DTexture9* norm,
                                      IDirect3DTexture9* foc,
                                      IDirect3DTexture9* prs,
                                      float sz) -> bool
                    {
                        const ImVec2 pos = ImGui::GetCursorScreenPos();
                        ImGui::InvisibleButton(id, { sz, sz });
                        const bool clicked = ImGui::IsItemClicked();
                        const bool hov     = ImGui::IsItemHovered();
                        const bool act     = ImGui::IsItemActive() && hov;
                        IDirect3DTexture9* tex = act ? prs : (hov ? foc : norm);
                        if (!tex) tex = norm;
                        if (tex)
                            ImGui::GetWindowDrawList()->AddImage(
                                ImTextureRef((ImTextureID)(uintptr_t)tex),
                                pos, { pos.x + sz, pos.y + sz });
                        return clicked;
                    };

                    // Zoom in
                    ImGui::SetCursorScreenPos({ sfOrigin.x + subW + sp, sfOrigin.y });
                    if (DDJBtn("##tp_zi", s_mmZoomIn, s_mmZoomInFoc, s_mmZoomInPrs, K_ZBTN_W))
                        s_tpZoomTarget = (std::min)(s_tpZoomTarget * 1.35f, 2.0f);

                    // Zoom out
                    const float zMin2 = (std::max)(
                        (mapW - K_ZBTN_W - sp - 2.f * K_SF_C),
                        (mapFrameH              - 2.f * K_SF_C)) / (2.f * 5.f * 192.f);
                    ImGui::SetCursorScreenPos({ sfOrigin.x + subW + sp, sfOrigin.y + K_ZBTN_W + sp2 });
                    if (DDJBtn("##tp_zo", s_mmZoomOut, s_mmZoomOutFoc, s_mmZoomOutPrs, K_ZBTN_W))
                        s_tpZoomTarget = (std::max)(s_tpZoomTarget / 1.35f, zMin2);

                    // Advance layout past the whole minimap row
                    ImGui::SetCursorScreenPos({ sfOrigin.x, sfOrigin.y + mapFrameH });
                    ImGui::Dummy({ mapW, 0.f });
                }
            }
        }
        ImGui::EndChild();

        ImGui::SameLine();

        ImGui::BeginChild("##bwt_right", { rightW, bodyH }, false,
                          ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
        {
            const float cW  = ImGui::GetContentRegionAvail().x;
            const float sp3 = ImGui::GetStyle().ItemSpacing.x;

            // TRAIN PLACE
            ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "TRAIN PLACE");
            ImGui::Spacing();

            // 5 fields: X, Y, Z, R (editable) + Region (read-only LE hex)
            const float fieldW   = (cW - sp3 * 4.f) / 5.f;
            const ImVec2 fieldStart = ImGui::GetCursorScreenPos();

            const char* fids[4] = { "##bwt_x","##bwt_y","##bwt_z","##bwt_r" };
            int*        fvals[4] = { &walkX, &walkY, &walkZ, &walkR };
            for (int fi = 0; fi < 4; fi++) {
                ImGui::SetCursorScreenPos({ fieldStart.x + fi * (fieldW + sp3), fieldStart.y });
                SROInputBarInt(fids[fi], fvals[fi], fieldW, 0, true);
            }

            {
                char rgBuf[16];
                snprintf(rgBuf, sizeof(rgBuf), "0x%04X",
                         (unsigned)(regionID) & 0xFFFFu);
                ImGui::SetCursorScreenPos({ fieldStart.x + 4 * (fieldW + sp3), fieldStart.y });
                SROInputBar("##bwt_reg", rgBuf, sizeof(rgBuf), fieldW,
                            ImGuiInputTextFlags_ReadOnly, true);
            }
            // Advance layout past the 5 bars
            ImGui::SetCursorScreenPos(fieldStart);
            ImGui::Dummy({ cW, 22.f });

            // Field labels centred under each bar
            {
                const char* lbls[5] = { "X","Y","Z","R","Region" };
                const float labelY  = fieldStart.y + 22.f + 2.f;
                ImDrawList* dl = ImGui::GetWindowDrawList();
                const ImU32 dimCol = IM_COL32(140, 130, 110, 255);
                for (int fi = 0; fi < 5; fi++) {
                    const ImVec2 ts = ImGui::CalcTextSize(lbls[fi]);
                    const float  lx = fieldStart.x + fi * (fieldW + sp3) + (fieldW - ts.x) * 0.5f;
                    dl->AddText({ lx, labelY }, dimCol, lbls[fi]);
                }
                ImGui::SetCursorScreenPos({ fieldStart.x, labelY });
                ImGui::Dummy({ cW, ImGui::GetTextLineHeight() + 4.f });
            }

            ImGui::Spacing();

            {
                static constexpr float gcpW  = 180.f;
                const float            barsW = 5.f * fieldW + 4.f * sp3;
                const float            gcpX  = fieldStart.x + (barsW - gcpW) * 0.5f;
                ImGui::SetCursorScreenPos({ gcpX, ImGui::GetCursorScreenPos().y });
                if (SROButton("##bwt_gcp", "Get Current Pos", gcpW, 26.f)) {
                    walkX    = ss.WorldX;
                    walkY    = ss.WorldY;
                    walkZ    = ss.WorldZ;
                    regionID = ss.currentRegionID;
                }
            }

            ImGui::Spacing();
            ImGui::Separator();
            ImGui::Spacing();

            // PRESETS
            ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "PRESETS");
            ImGui::Spacing();

            // Filter bar
            ImGui::TextDisabled("Search Presets");
            SROInputBar("##bwt_filter", presetFilter, sizeof(presetFilter), cW);

            ImGui::Spacing();

            // Preset list — height fills remaining space minus bottom buttons
            const float btnRowH = 26.f + sp2;
            const float nameRowH = savingPreset ? 22.f + sp2 : 0.f;
            const float listH = ImGui::GetContentRegionAvail().y - btnRowH - nameRowH - sp2 * 2.f;

            ImGui::PushStyleColor(ImGuiCol_ChildBg, IM_COL32(12, 14, 20, 200));
            if (ImGui::BeginChild("##bwt_list", { -1.f, listH > 30.f ? listH : 30.f }, false)) {
                const float iW = ImGui::GetContentRegionAvail().x;
                // Rows sit flush — no vertical gap between list items
                ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing,
                                    { ImGui::GetStyle().ItemSpacing.x, 0.f });

                for (int i = 0; i < IM_ARRAYSIZE(kDefPresets); i++) {
                    const DefaultPreset& p = kDefPresets[i];
                    if (presetFilter[0]) {
                        std::string nl = p.name, fl = presetFilter;
                        std::transform(nl.begin(), nl.end(), nl.begin(), ::tolower);
                        std::transform(fl.begin(), fl.end(), fl.begin(), ::tolower);
                        if (nl.find(fl) == std::string::npos) continue;
                    }
                    char lbl[160], lid[16];
                    snprintf(lbl, sizeof(lbl), "[default] %s  (%d, %d, %d)", p.name, p.x, p.y, p.z);
                    snprintf(lid, sizeof(lid), "##dp%d", i);
                    if (SROListItem(lid, lbl, selectedPreset == i, iW))
                        selectedPreset = i;
                    if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0)) {
                        walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r; regionID = p.regionId;
                    }
                }

                for (int i = 0; i < (int)kUsrPresets.size(); i++) {
                    UserPreset& p = kUsrPresets[i];
                    if (presetFilter[0]) {
                        std::string nl = p.name, fl = presetFilter;
                        std::transform(nl.begin(), nl.end(), nl.begin(), ::tolower);
                        std::transform(fl.begin(), fl.end(), fl.begin(), ::tolower);
                        if (nl.find(fl) == std::string::npos) continue;
                    }
                    const int encIdx = -2 - i;
                    char lbl[160], lid[16];
                    snprintf(lbl, sizeof(lbl), "%s  (%d, %d, %d)", p.name, p.x, p.y, p.z);
                    snprintf(lid, sizeof(lid), "##up%d", i);
                    if (SROListItem(lid, lbl, selectedPreset == encIdx, iW))
                        selectedPreset = encIdx;
                    if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0)) {
                        walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r; regionID = p.regionID;
                    }
                }
                ImGui::PopStyleVar();
            }
            ImGui::EndChild();
            ImGui::PopStyleColor();

            ImGui::Spacing();

            // Save-as name entry
            if (savingPreset) {
                const float okW = 46.f;
                ImGui::SetNextItemWidth(ImGui::GetContentRegionAvail().x - okW - sp3);
                const bool entered = ImGui::InputText("##bwt_newname", newPresetName,
                    sizeof(newPresetName),
                    ImGuiInputTextFlags_EnterReturnsTrue | ImGuiInputTextFlags_AutoSelectAll);
                ImGui::SameLine(0.f, sp3);
                const bool ok = SROButton("##bwt_ok", "OK", okW, 22.f) || entered;
                if (ok && newPresetName[0]) {
                    UserPreset np = {};
                    strncpy(np.name, newPresetName, sizeof(np.name) - 1);
                    np.x = walkX; np.y = walkY; np.z = walkZ; np.r = walkR; np.regionID = regionID;
                    kUsrPresets.push_back(np);
                    savingPreset = false; newPresetName[0] = '\0';
                }
                if (ImGui::IsKeyPressed(ImGuiKey_Escape)) {
                    savingPreset = false; newPresetName[0] = '\0';
                }
                ImGui::Spacing();
            }

            // Bottom buttons
            const bool hasSel     = (selectedPreset != -1);
            const bool hasUsrSel  = (selectedPreset < -1);
            const float thirdW    = (ImGui::GetContentRegionAvail().x - sp3 * 2.f) / 3.f;

            if (SROButton("##bwt_use",  "Use Selected",   thirdW, 26.f, !hasSel) && hasSel) {
                if (selectedPreset >= 0) {
                    const DefaultPreset& p = kDefPresets[selectedPreset];
                    walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r; regionID = p.regionId;
                } else {
                    UserPreset& p = kUsrPresets[-2 - selectedPreset];
                    walkX = p.x; walkY = p.y; walkZ = p.z; walkR = p.r; regionID = p.regionID;
                }
            }
            ImGui::SameLine();
            if (SROButton("##bwt_del", "Delete", thirdW, 26.f, !hasUsrSel) && hasUsrSel) {
                const int idx = -2 - selectedPreset;
                kUsrPresets.erase(kUsrPresets.begin() + idx);
                selectedPreset = -1;
            }
            ImGui::SameLine();
            if (SROButton("##bwt_save", "Save as Preset", thirdW, 26.f)) {
                savingPreset = true; newPresetName[0] = '\0';
            }
        }
        ImGui::EndChild();
    }
    else if (s_tab == 1)
    {
        // Lazy-loaded skill-tab textures
        static IDirect3DTexture9* s_stlSlot   = nullptr;  // stl_slot_04.ddj  (exact, no stretch)
        static IDirect3DTexture9* s_skillGlow = nullptr;  // pt_edge_effect.ddj (288x32, 9 frames)
        static IDirect3DTexture9* s_ubNum[10] = {};       // ub_number_0..9.ddj
        static bool s_skTex = false;
        if (!s_skTex && g_iconCache) {
            s_stlSlot   = g_iconCache->Get("interface/stall/stl_slot_04.ddj");
            s_skillGlow = g_iconCache->Get("interface/pet/pt_edge_effect.ddj");
            for (int d = 0; d < 10; d++) {
                char p[64]; snprintf(p, sizeof(p), "interface/underbar/ub_number_%d.ddj", d);
                s_ubNum[d] = g_iconCache->Get(p);
            }
            s_skTex = true;
        }

        static constexpr float K_SLOT  = 38.f;   // stl_slot_04 natural size (assumed square)
        static constexpr float K_ICON  = 30.f;   // icon size centered inside slot
        static constexpr float K_ROW_H = K_SLOT; // list row height = slot height
        static constexpr float K_NUM_W =  7.f;   // ub_number digit render width
        static constexpr float K_NUM_H =  9.f;   // ub_number digit render height
        static constexpr float K_MID_W = 98.f;   // middle controls column width
        static constexpr int   SK_GLOW_FR = 9;
        static constexpr float SK_GLOW_CY = 0.5f;

        static int  s_selPool = -1;
        static int  s_selAtk  = -1;
        static int  s_selBuf  = -1;
        static char s_skFlt[64] = {};

        const float sp  = ImGui::GetStyle().ItemSpacing.x;
        const float sp2 = ImGui::GetStyle().ItemSpacing.y;

        // Glow animation
        const float glowT   = fmodf((float)ImGui::GetTime(), SK_GLOW_CY) / SK_GLOW_CY;
        const int   glowFrm = (int)(glowT * SK_GLOW_FR) % SK_GLOW_FR;
        const float glowU0  = (float)glowFrm / SK_GLOW_FR;
        const float glowU1  = (float)(glowFrm + 1) / SK_GLOW_FR;

        auto ToTI = [](IDirect3DTexture9* t) -> ImTextureRef {
            return ImTextureRef((ImTextureID)(uintptr_t)t);
        };

        // Draw digit number overlay using ub_number DDJs (multi-digit supported)
        auto DrawNum = [&](ImDrawList* dl, ImVec2 pos, int n) {
            char buf[8]; snprintf(buf, sizeof(buf), "%d", n);
            float x = pos.x;
            for (const char* c = buf; *c; ++c) {
                const int d = *c - '0';
                if (d >= 0 && d <= 9 && s_ubNum[d])
                    dl->AddImage(ToTI(s_ubNum[d]), { x, pos.y }, { x + K_NUM_W, pos.y + K_NUM_H });
                x += K_NUM_W + 1.f;
            }
        };

        const float leftW  = availW * 0.44f;
        const float rightW = availW - leftW - K_MID_W - sp * 2.f;
        const float textH  = ImGui::GetTextLineHeight() + sp2;
        // Right column splits bodyH between two labelled frames + a small gap
        const float halfFrH = floorf((bodyH - textH * 2.f - sp2 * 4.f) * 0.5f);

        ImGui::BeginChild("##skl_col", { leftW, bodyH }, false,
            ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

        ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "ALL SKILLS");
        SROInputBar("##skflt", s_skFlt, sizeof(s_skFlt), leftW);
        ImGui::Dummy({ 0.f, 2.f });
        const float poolFrameH = ImGui::GetContentRegionAvail().y;

        if (SROBeginActionFrame("##sk_pool", leftW, poolFrameH)) {
            ImGui::Dummy({ 0.f, (float)K_AF_C });
            ImDrawList* dl = ImGui::GetWindowDrawList();
            const float rowW = ImGui::GetContentRegionAvail().x;

            for (int i = 0; i < (int)ss.availableSkills.size(); i++) {
                const SkillEntry& sk = ss.availableSkills[i];

                if (s_skFlt[0]) {
                    std::string nl = sk.readableName, fl = s_skFlt;
                    std::transform(nl.begin(), nl.end(), nl.begin(), ::tolower);
                    std::transform(fl.begin(), fl.end(), fl.begin(), ::tolower);
                    if (nl.find(fl) == std::string::npos) continue;
                }

                const bool   sel = (s_selPool == i);
                const ImVec2 rTL = ImGui::GetCursorScreenPos();

                // Slot background — exact natural size, never stretched
                if (s_stlSlot)
                    dl->AddImage(ToTI(s_stlSlot), rTL, { rTL.x + K_SLOT, rTL.y + K_SLOT });
                else
                    dl->AddRectFilled(rTL, { rTL.x + K_SLOT, rTL.y + K_SLOT },
                                      IM_COL32(18, 20, 28, 220), 2.f);

                // Icon centered inside slot
                const float  pad  = (K_SLOT - K_ICON) * 0.5f;
                const ImVec2 iTL  = { rTL.x + pad,          rTL.y + pad          };
                const ImVec2 iBR  = { iTL.x + K_ICON,       iTL.y + K_ICON       };
                IDirect3DTexture9* ico = GetSkillIcon(sk.iconFile);
                if (ico) dl->AddImage(ToTI(ico), iTL, iBR);

                // Glow overlay on selection
                if (sel && s_skillGlow)
                    dl->AddImage(ToTI(s_skillGlow), iTL, iBR, { glowU0, 0.f }, { glowU1, 1.f });

                // Skill name
                const ImU32 nameCol = sk.isPassive
                    ? IM_COL32(105, 100, 85, 255) : IM_COL32(210, 200, 160, 255);
                dl->AddText({ rTL.x + K_SLOT + 6.f,
                              rTL.y + (K_ROW_H - ImGui::GetTextLineHeight()) * 0.5f },
                             nameCol, sk.readableName.c_str());

                ImGui::SetCursorScreenPos(rTL);
                char bid[12]; snprintf(bid, sizeof(bid), "##pk%d", i);
                ImGui::InvisibleButton(bid, { rowW, K_ROW_H });
                if (ImGui::IsItemClicked()) {
                    s_selPool = (s_selPool == i) ? -1 : i;
                    s_selAtk  = -1;
                    s_selBuf  = -1;
                    SROSkin_PlayClick();
                }
                ImGui::Dummy({ 0.f, 2.f });
            }

            if (ss.availableSkills.empty()) {
                ImGui::Dummy({ 0.f, 8.f });
                ImGui::TextDisabled("  No skills loaded.");
            }
            ImGui::Dummy({ 0.f, (float)K_AF_C }); // bottom border clearance
        }
        SROEndActionFrame();
        ImGui::EndChild(); // skl_col
        ImGui::SameLine();

        ImGui::BeginChild("##skm_col", { K_MID_W, bodyH }, false,
            ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

        ImGui::Dummy({ 0.f, bodyH * 0.28f });

        const bool hasPool  = (s_selPool >= 0 && s_selPool < (int)ss.availableSkills.size());
        const bool notPassv = hasPool && !ss.availableSkills[s_selPool].isPassive;
        const bool hasAtk   = (s_selAtk >= 0 && s_selAtk < (int)ss.attackSkills.size());
        const bool hasBuf   = (s_selBuf >= 0 && s_selBuf < (int)ss.buffSkills.size());
        const bool hasRight = hasAtk || hasBuf;

        if (SROButton("##skAddA", "Add Attack", K_MID_W, 26.f, !notPassv) && notPassv)
            NetActions::SendSkillAdd(ss.availableSkills[s_selPool].id, false);
        ImGui::Dummy({ 0.f, 3.f });
        if (SROButton("##skAddB", "Add Buff", K_MID_W, 26.f, !notPassv) && notPassv)
            NetActions::SendSkillAdd(ss.availableSkills[s_selPool].id, true);

        ImGui::Dummy({ 0.f, 14.f });

        if (SROButton("##skMvU", "Move Up", K_MID_W, 26.f, !hasRight) && hasRight) {
            if (hasAtk) {
                NetActions::SendSkillMove(ss.attackSkills[s_selAtk].id, -1);
                if (s_selAtk > 0) s_selAtk--;
            } else {
                NetActions::SendSkillMove(ss.buffSkills[s_selBuf].id, -1);
                if (s_selBuf > 0) s_selBuf--;
            }
        }
        ImGui::Dummy({ 0.f, 3.f });
        if (SROButton("##skMvD", "Move Down", K_MID_W, 26.f, !hasRight) && hasRight) {
            if (hasAtk) {
                NetActions::SendSkillMove(ss.attackSkills[s_selAtk].id, 1);
                if (s_selAtk < (int)ss.attackSkills.size() - 1) s_selAtk++;
            } else {
                NetActions::SendSkillMove(ss.buffSkills[s_selBuf].id, 1);
                if (s_selBuf < (int)ss.buffSkills.size() - 1) s_selBuf++;
            }
        }
        ImGui::Dummy({ 0.f, 14.f });

        if (SROButton("##skRem", "Remove", K_MID_W, 26.f, !hasRight) && hasRight) {
            if (hasAtk) { NetActions::SendSkillRemove(ss.attackSkills[s_selAtk].id, false); s_selAtk = -1; }
            else         { NetActions::SendSkillRemove(ss.buffSkills[s_selBuf].id,   true);  s_selBuf = -1; }
        }

        ImGui::EndChild(); // skm_col
        ImGui::SameLine();

        ImGui::BeginChild("##skr_col", { rightW, bodyH }, false,
            ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

        auto DrawIndexedRow = [&](ImDrawList* dl, const SkillEntry& sk,
                                   char prefix, int i, int* pSel) {
            const bool   sel = (*pSel == i);
            const ImVec2 rTL = ImGui::GetCursorScreenPos();
            const float  rowW = ImGui::GetContentRegionAvail().x;

            if (s_stlSlot)
                dl->AddImage(ToTI(s_stlSlot), rTL, { rTL.x + K_SLOT, rTL.y + K_SLOT });
            else
                dl->AddRectFilled(rTL, { rTL.x + K_SLOT, rTL.y + K_SLOT },
                                  IM_COL32(18, 20, 28, 220), 2.f);

            const float  pad = (K_SLOT - K_ICON) * 0.5f;
            const ImVec2 iTL = { rTL.x + pad,    rTL.y + pad    };
            const ImVec2 iBR = { iTL.x + K_ICON, iTL.y + K_ICON };
            IDirect3DTexture9* ico = GetSkillIcon(sk.iconFile);
            if (ico) dl->AddImage(ToTI(ico), iTL, iBR);

            if (sel && s_skillGlow)
                dl->AddImage(ToTI(s_skillGlow), iTL, iBR, { glowU0, 0.f }, { glowU1, 1.f });

            // Index number overlaid top-left of slot
            DrawNum(dl, { rTL.x + 2.f, rTL.y + 2.f }, i + 1);

            dl->AddText({ rTL.x + K_SLOT + 6.f,
                          rTL.y + (K_ROW_H - ImGui::GetTextLineHeight()) * 0.5f },
                        IM_COL32(210, 200, 160, 255), sk.readableName.c_str());

            ImGui::SetCursorScreenPos(rTL);
            char bid[12]; snprintf(bid, sizeof(bid), "##%ck%d", prefix, i);
            ImGui::InvisibleButton(bid, { rowW, K_ROW_H });
            if (ImGui::IsItemClicked()) {
                *pSel     = (sel) ? -1 : i;
                s_selPool = -1;
                if (prefix == 'a') s_selBuf = -1;
                else               s_selAtk = -1;
                SROSkin_PlayClick();
            }
            ImGui::Dummy({ 0.f, 2.f });
        };

        // Attack skills
        ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "ATTACK SKILLS");
        if (SROBeginActionFrame("##sk_atk", rightW, halfFrH)) {
            ImGui::Dummy({ 0.f, (float)K_AF_C });
            ImDrawList* dl = ImGui::GetWindowDrawList();
            for (int i = 0; i < (int)ss.attackSkills.size(); i++)
                DrawIndexedRow(dl, ss.attackSkills[i], 'a', i, &s_selAtk);
            if (ss.attackSkills.empty()) {
                ImGui::Dummy({ 0.f, 8.f });
                ImGui::TextDisabled("  (empty — add from skill list)");
            }
            ImGui::Dummy({ 0.f, (float)K_AF_C }); // bottom border clearance
        }
        SROEndActionFrame();

        ImGui::Dummy({ 0.f, sp2 });

        // Buff skills
        ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "BUFF SKILLS");
        const float bufFrameH = ImGui::GetContentRegionAvail().y;
        if (SROBeginActionFrame("##sk_buf", rightW, bufFrameH)) {
            ImGui::Dummy({ 0.f, (float)K_AF_C });
            ImDrawList* dl = ImGui::GetWindowDrawList();
            for (int i = 0; i < (int)ss.buffSkills.size(); i++)
                DrawIndexedRow(dl, ss.buffSkills[i], 'b', i, &s_selBuf);
            if (ss.buffSkills.empty()) {
                ImGui::Dummy({ 0.f, 8.f });
                ImGui::TextDisabled("  (empty — add from skill list)");
            }
            ImGui::Dummy({ 0.f, (float)K_AF_C }); // bottom border clearance
        }
        SROEndActionFrame();

        ImGui::EndChild(); // skr_col
    }
    else if (s_tab == 2)
    {
        static int settingsCat = 0;
        static const char* kCats[] = {
            "Auto Potion", "Town Supplies", "Maintenance", "Combat", "Looting & Buffs"
        };

        const float sp2      = ImGui::GetStyle().ItemSpacing.y;
        const float sp       = ImGui::GetStyle().ItemSpacing.x;
        const float saveBtnH = 28.f;
        const float contentH = bodyH - saveBtnH - sp2 * 2.f;
        const float listW    = 150.f;
        const float detailW  = availW - listW - sp;
        const ImVec2 topLeft = ImGui::GetCursorScreenPos();

        // Category list
        ImGui::BeginChild("##bws_cats", { listW, contentH }, false,
                          ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
        for (int i = 0; i < IM_ARRAYSIZE(kCats); i++) {
            char cid[16]; snprintf(cid, sizeof(cid), "##bwcat%d", i);
            if (SROListItem(cid, kCats[i], settingsCat == i, listW, 28.f))
                settingsCat = i;
            if (i < IM_ARRAYSIZE(kCats) - 1) ImGui::Dummy({ 0.f, 1.f });
        }
        ImGui::EndChild();

        ImGui::SameLine();

        // Detail panel
        if (SROBeginActionFrame("##bws_detail", detailW, contentH)) {
            ImGui::Spacing();
            const float lCol = 130.f; // label column width

            ImGui::Dummy({ 0.f, (float)K_AF_C }); // clear top border strip

            // label (drawlist) + SROInputBarInt on same visual row
            auto SettingRow = [&](const char* lbl, const char* rowId, int* val, float iw = 80.f) {
                const ImVec2 p = ImGui::GetCursorScreenPos();
                ImGui::GetWindowDrawList()->AddText(
                    { p.x + 16.f, p.y + (22.f - ImGui::GetTextLineHeight()) * 0.5f },
                    IM_COL32(175, 160, 120, 255), lbl);
                ImGui::SetCursorScreenPos({ p.x + 16.f + lCol, p.y });
                SROInputBarInt(rowId, val, iw);
                ImGui::SetCursorScreenPos(p);
                ImGui::Dummy({ 16.f + lCol + iw, 22.f });
            };

            // label (drawlist) + SROCombo on same visual row
            auto ComboRow = [&](const char* lbl, const char* rowId, int* val,
                                const char* const* items, int cnt, float cw = 120.f) {
                const ImVec2 p = ImGui::GetCursorScreenPos();
                ImGui::GetWindowDrawList()->AddText(
                    { p.x + 16.f, p.y + (22.f - ImGui::GetTextLineHeight()) * 0.5f },
                    IM_COL32(175, 160, 120, 255), lbl);
                ImGui::SetCursorScreenPos({ p.x + 16.f + lCol, p.y });
                SROCombo(rowId, val, items, cnt, cw, 22.f);
                ImGui::SetCursorScreenPos(p);
                ImGui::Dummy({ 16.f + lCol + cw, 22.f });
            };

            // checkbox + sub-rows for town supply items
            auto BuyRow = [&](const char* name, const char* bid,
                               bool* buy, int* refill, int* thresh,
                               const char* const* typeItems, int typeCount, int* typeVal) {
                bool b = *buy;
                if (SROCheckbox(bid, name, &b)) *buy = b;
                if (*buy) {
                    char rfid[48], thid[48], tyid[48];
                    snprintf(rfid, sizeof(rfid), "%s_rf", bid);
                    snprintf(thid, sizeof(thid), "%s_th", bid);
                    snprintf(tyid, sizeof(tyid), "%s_ty", bid);
                    SettingRow("Refill to:", rfid, refill, 60.f);
                    SettingRow("Min stock:", thid, thresh, 60.f);
                    if (typeItems) ComboRow("Type:", tyid, typeVal, typeItems, typeCount);
                    ImGui::Spacing();
                }
            };

            if (settingsCat == 0)
            {
                ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "AUTO POTION");
                ImGui::Separator(); ImGui::Spacing();

                bool hp = ss.botSettings.AutoPotion.AutoUseHP;
                if (SROCheckbox("##bs_hp", "Use HP Potion", &hp)) ss.botSettings.AutoPotion.AutoUseHP = hp;
                if (hp) {
                    SettingRow("HP Threshold %:", "##bs_hpt", &ss.botSettings.AutoPotion.HPPotHealthThreshold);
                    SettingRow("HP Delay (ms):",  "##bs_hpd", &ss.botSettings.AutoPotion.HPDelay);
                }
                ImGui::Spacing();

                bool mp = ss.botSettings.AutoPotion.AutoUseMP;
                if (SROCheckbox("##bs_mp", "Use MP Potion", &mp)) ss.botSettings.AutoPotion.AutoUseMP = mp;
                if (mp) {
                    SettingRow("MP Threshold %:", "##bs_mpt", &ss.botSettings.AutoPotion.MPPotManaThreshold);
                    SettingRow("MP Delay (ms):",  "##bs_mpd", &ss.botSettings.AutoPotion.MPDelay);
                }
                ImGui::Spacing();

                bool vig = ss.botSettings.AutoPotion.UseVigorPotions;
                if (SROCheckbox("##bs_vig", "Use Vigor Potions", &vig)) ss.botSettings.AutoPotion.UseVigorPotions = vig;
                if (vig) {
                    SettingRow("Vigor Threshold %:", "##bs_vigt", &ss.botSettings.AutoPotion.VigorHPMPThreshold);
                    bool pref = ss.botSettings.AutoPotion.PreferVigorFirst;
                    if (SROCheckbox("##bs_vpref", "  Prioritize Vigor", &pref))
                        ss.botSettings.AutoPotion.PreferVigorFirst = pref;
                }
                ImGui::Spacing(); ImGui::Separator(); ImGui::Spacing();

                bool pills = ss.botSettings.AutoPotion.AutoUseContPills;
                if (SROCheckbox("##bs_pills", "Auto Universal Pills", &pills))
                    ss.botSettings.AutoPotion.AutoUseContPills = pills;
                bool purif = ss.botSettings.AutoPotion.AutoUsePurifPills;
                if (SROCheckbox("##bs_purif", "Auto Purification Pills", &purif))
                    ss.botSettings.AutoPotion.AutoUsePurifPills = purif;
                ImGui::Spacing(); ImGui::Separator(); ImGui::Spacing();

                bool petHeal = ss.botSettings.AutoPotion.HealPets;
                if (SROCheckbox("##bs_peth", "Heal Pets", &petHeal))
                    ss.botSettings.AutoPotion.HealPets = petHeal;
                if (petHeal)
                    SettingRow("Pet HP Threshold %:", "##bs_petht", &ss.botSettings.AutoPotion.HealPetHPThreshold);
            }
            else if (settingsCat == 1)
            {
                ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "TOWN SUPPLIES");
                ImGui::Separator(); ImGui::Spacing();

                BuyRow("HP Potions",         "##br_hp",
                       &ss.botSettings.Consumables.BuyHpPotions,
                       &ss.botSettings.Consumables.HpPotionRefillAmount,
                       &ss.botSettings.Consumables.HpPotionReturnThreshold,
                       PotionTypes, 6, (int*)&ss.botSettings.Consumables.HPType);
                BuyRow("MP Potions",         "##br_mp",
                       &ss.botSettings.Consumables.BuyMpPotions,
                       &ss.botSettings.Consumables.MpPotionRefillAmount,
                       &ss.botSettings.Consumables.MpPotionReturnThreshold,
                       PotionTypes, 6, (int*)&ss.botSettings.Consumables.MPType);
                BuyRow("Vigor Potions",       "##br_vig",
                       &ss.botSettings.Consumables.BuyVigorPotions,
                       &ss.botSettings.Consumables.VigorPotionRefillAmount,
                       &ss.botSettings.Consumables.VigorPotionReturnThreshold,
                       nullptr, 0, nullptr);
                BuyRow("Universal Pills",     "##br_upill",
                       &ss.botSettings.Consumables.BuyUniversalPills,
                       &ss.botSettings.Consumables.UniversalPillsRefillAmount,
                       &ss.botSettings.Consumables.UniversalPillsReturnThreshold,
                       UniPillTypes, 4, (int*)&ss.botSettings.Consumables.UniPillType);
                BuyRow("Purification Pills",  "##br_ppill",
                       &ss.botSettings.Consumables.BuyPurifPills,
                       &ss.botSettings.Consumables.PurifPillsRefillAmount,
                       &ss.botSettings.Consumables.PurifPillsReturnThreshold,
                       PurifPillTypes, 4, (int*)&ss.botSettings.Consumables.PurificationPillType);
                BuyRow("Speed Drugs",         "##br_spd",
                       &ss.botSettings.Consumables.BuySpeedDrugs,
                       &ss.botSettings.Consumables.SpeedDrugsRefillAmount,
                       &ss.botSettings.Consumables.SpeedDrugsReturnThreshold,
                       SpeedDrugTypes, 2, (int*)&ss.botSettings.Consumables.DrugType);
                BuyRow("Ammo / Arrows",       "##br_ammo",
                       &ss.botSettings.Consumables.BuyAmmo,
                       &ss.botSettings.Consumables.AmmoRefillAmount,
                       &ss.botSettings.Consumables.AmmoReturnThreshold,
                       AmmoTypes, 2, (int*)&ss.botSettings.Consumables.AmmoType);

                ImGui::Separator(); ImGui::Spacing();

                bool buyScrolls = ss.botSettings.Consumables.BuyReturnScrolls;
                if (SROCheckbox("##br_ret", "Buy Return Scrolls", &buyScrolls))
                    ss.botSettings.Consumables.BuyReturnScrolls = buyScrolls;
                if (buyScrolls)
                    SettingRow("Count:", "##br_retc", &ss.botSettings.Consumables.ReturnScrollRefillAmount, 60.f);
            }
            else if (settingsCat == 2)
            {
                ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "MAINTENANCE");
                ImGui::Separator(); ImGui::Spacing();

                ImGui::TextDisabled("CITY REPAIR");
                ImGui::Spacing();

                bool repair = ss.botSettings.Maintenance.RepairWeapon;
                if (SROCheckbox("##bs_rep", "Repair at Blacksmith", &repair))
                    ss.botSettings.Maintenance.RepairWeapon = repair;
                if (repair)
                    SettingRow("Durability % Trip:", "##bs_repd", &ss.botSettings.Maintenance.RepairDurabilityThreshold);

                ImGui::Spacing(); ImGui::Separator(); ImGui::Spacing();
                ImGui::TextDisabled("EMERGENCY RECALLS");
                ImGui::Spacing();

                bool retDead = ss.botSettings.BackTownMonitor.ReturnIfDead;
                if (SROCheckbox("##bs_retd", "Return to Town if Dead", &retDead))
                    ss.botSettings.BackTownMonitor.ReturnIfDead = retDead;

                bool retInv = ss.botSettings.BackTownMonitor.ReturnIfInventoryFull;
                if (SROCheckbox("##bs_reti", "Return if Inventory Full", &retInv))
                    ss.botSettings.BackTownMonitor.ReturnIfInventoryFull = retInv;
            }
            else if (settingsCat == 3)
            {
                ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "COMBAT");
                ImGui::Separator(); ImGui::Spacing();

                bool ignDP = ss.botSettings.Attack.IgnoreDimensionPillars;
                if (SROCheckbox("##bs_idp", "Ignore Dimension Pillars", &ignDP))
                    ss.botSettings.Attack.IgnoreDimensionPillars = ignDP;

                ImGui::Spacing(); ImGui::Separator(); ImGui::Spacing();
                ImGui::TextDisabled("BERSERK MODES");
                ImGui::Spacing();

                bool zerkNow = ss.botSettings.Attack.UseZerkRightAwayWhenFull;
                if (SROCheckbox("##bs_zin", "Zerk Instantly when Full", &zerkNow))
                    ss.botSettings.Attack.UseZerkRightAwayWhenFull = zerkNow;

                if (!zerkNow) {
                    bool zng = ss.botSettings.Attack.UseZerkOnNormalGiants;
                    if (SROCheckbox("##bs_zng", "Zerk on Normal Giants", &zng))
                        ss.botSettings.Attack.UseZerkOnNormalGiants = zng;
                    bool zpm = ss.botSettings.Attack.UseZerkOnPartyMobs;
                    if (SROCheckbox("##bs_zpm", "Zerk on Party Mobs", &zpm))
                        ss.botSettings.Attack.UseZerkOnPartyMobs = zpm;
                    bool zpg = ss.botSettings.Attack.UseZerkOnPartyGiants;
                    if (SROCheckbox("##bs_zpg", "Zerk on Party Giants", &zpg))
                        ss.botSettings.Attack.UseZerkOnPartyGiants = zpg;
                    bool zu = ss.botSettings.Attack.UseZerkOnUniques;
                    if (SROCheckbox("##bs_zu", "Zerk on Uniques", &zu))
                        ss.botSettings.Attack.UseZerkOnUniques = zu;
                    bool zsw = ss.botSettings.Attack.UseZerkIfNMobsAttackingSimulataneously;
                    if (SROCheckbox("##bs_zsw", "Zerk if Swarmed", &zsw))
                        ss.botSettings.Attack.UseZerkIfNMobsAttackingSimulataneously = zsw;
                    if (zsw)
                        SettingRow("Mob Count:", "##bs_zswc", &ss.botSettings.Attack.ZerkMobCount, 60.f);
                }
            }
            else if (settingsCat == 4)
            {
                ImGui::TextColored({ 0.7f, 0.65f, 0.45f, 1.f }, "LOOTING & BUFFS");
                ImGui::Separator(); ImGui::Spacing();

                bool pg = ss.botSettings.Pickup.PickGold;
                if (SROCheckbox("##bs_pg", "Pick Gold", &pg)) ss.botSettings.Pickup.PickGold = pg;
                bool pa = ss.botSettings.Pickup.PickAll;
                if (SROCheckbox("##bs_pa", "Loot Everything", &pa)) ss.botSettings.Pickup.PickAll = pa;
                bool pam = ss.botSettings.Pickup.PickAmmoIfAmountLowerThan;
                if (SROCheckbox("##bs_pam", "Pick Ammo if Low", &pam))
                    ss.botSettings.Pickup.PickAmmoIfAmountLowerThan = pam;

                ImGui::Spacing(); ImGui::Separator(); ImGui::Spacing();
                ImGui::TextDisabled("WALK BUFFING");
                ImGui::Spacing();

                bool spd = ss.botSettings.Autowalker.CastSpeedBuffWhileWalking;
                if (SROCheckbox("##bs_spd", "Speed Buffs While Walking", &spd))
                    ss.botSettings.Autowalker.CastSpeedBuffWhileWalking = spd;
                bool nzz = ss.botSettings.Autowalker.CastNoiseBuffWhileWalking;
                if (SROCheckbox("##bs_nzz", "Noise Buffs While Walking", &nzz))
                    ss.botSettings.Autowalker.CastNoiseBuffWhileWalking = nzz;
            }

            ImGui::Spacing();
            ImGui::Spacing(); // Intentional.
        }
        SROEndActionFrame();

        // Save button — fixed width, bottom right
        static constexpr float saveW = 200.f;
        const float saveY = topLeft.y + contentH + sp2;
        ImGui::SetCursorScreenPos({ topLeft.x + availW - saveW, saveY });
        if (SROButton("##bws_save", "Save & Apply", saveW, saveBtnH))
            NetActions::SendSaveBotSettings(ss.botSettings);
    }

    EndSROWindow();
}

static void RenderSettings() {
    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));

    if (!BeginSROWindow("##settingswnd", "Settings", &showSettingsWindow,
            ImVec2(360.f, 440.f), ImVec2(-1.f, -1.f),
            ImVec2(280.f, 300.f), ImVec2(500.f, 700.f)))
        return;

    struct Bind { const char* id; const char* label; int* key; };
    static const Bind kBinds[] = {
        { "##kb0", "Session Stats", &Settings::showSessionStatsKey },
        { "##kb1", "Admin Tools",   &Settings::showAdminToolsKey   },
        { "##kb2", "Settings",      &Settings::showSettingsKey     },
        { "##kb3", "Achievements",  &Settings::showAchKey          },
        { "##kb4", "Bot Window",    &Settings::showBotWindow       },
    };

    // KEYBINDS
    ImGui::TextColored(ImVec4(0.7f, 0.65f, 0.45f, 1.f), "KEYBINDS");
    ImGui::Spacing();
    for (const auto& b : kBinds) {
        if (SROKeybind(b.id, b.label, b.key))
            Settings::Save();
        ImGui::Spacing();
    }

    ImGui::Separator();
    ImGui::Spacing();

    //TOGGLES
    ImGui::TextColored(ImVec4(0.7f, 0.65f, 0.45f, 1.f), "TOGGLES");
    ImGui::Spacing();

    bool kf = Settings::keepFocused;
    if (SROCheckbox("##cfg_kf", "Keep Focus", &kf)) {
        Settings::keepFocused = kf;
        Settings::Save();
    }
    ImGui::Spacing();

    bool fps = Settings::showFPSCounter;
    if (SROCheckbox("##cfg_fps", "Show FPS Counter", &fps)) {
        Settings::showFPSCounter = fps;
        Settings::Save();
    }
    ImGui::Spacing();

    bool wm = Settings::showWatermark;
    if (SROCheckbox("##cfg_wm", "Show Watermark", &wm)) {
        Settings::showWatermark = wm;
        Settings::Save();
    }

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // DEV TOOLS
    ImGui::TextColored(ImVec4(0.7f, 0.65f, 0.45f, 1.f), "DEV TOOLS");
    ImGui::Spacing();
    if (SROButton("##cfg_skintest",
            showSkinTest ? "Close Skin Test" : "Open Skin Test",
            ImGui::GetContentRegionAvail().x, 24.f))
        showSkinTest = !showSkinTest;

    EndSROWindow();
}

static void RenderSkinTest()
{
    if (!BeginSROWindow("##skintest", "Element Test", &showSkinTest,
                        { 440.f, 430.f }, { 320.f, 290.f },
                        { 280.f, 260.f }, { 800.f, 700.f }))
        return;

    auto& log = GetLogger();

    static int s_tab = 0;
    static int s_c1  = 0, s_c2 = 0, s_c3 = 0;

    static const char* kTabs[]  = { "Elements", "Scroll", "Info" };
    static const char* kItems1[] = { "Player",  "Pet"                      };
    static const char* kItems2[] = { "By Type", "By Name", "Logical"       };
    static const char* kItems3[] = { "Normal",  "Party",   "Guild", "All"  };

    const float availW = ImGui::GetContentRegionAvail().x;
    const float availH = ImGui::GetContentRegionAvail().y;
    const float tabW   = availW / 3.f;
    const float subH   = availH - K_TAB_H - ImGui::GetStyle().ItemSpacing.y;

    SROTabBar("##et_tabs", kTabs, 3, &s_tab, tabW);

    // Elements
    if (s_tab == 0) {
        if (SROBeginSubFrame("##sf_elem", availW, subH)) {
            const float iW  = ImGui::GetContentRegionAvail().x;
            const float sp  = ImGui::GetStyle().ItemSpacing.x;
            const float cW  = (iW - sp) * 0.5f;
            const float bW  = (iW - sp * 2.f) / 3.f;

            ImGui::Dummy({ 0.f, 2.f });
            ImGui::TextDisabled("Combos");
            ImGui::Spacing();
            SROCombo("##ec1", &s_c1, kItems1, 2, cW, 22.f);
            ImGui::SameLine();
            SROCombo("##ec2", &s_c2, kItems2, 3, cW, 22.f);

            ImGui::Dummy({ 0.f, 6.f });
            ImGui::Separator();
            ImGui::Dummy({ 0.f, 6.f });
            ImGui::TextDisabled("Buttons");
            ImGui::Spacing();

            if (SROButton("##eb1", "Action A", bW, 24.f))
                log.Info("SkinTest", "Action A clicked");
            ImGui::SameLine();
            if (SROButton("##eb2", "Action B", bW, 24.f))
                log.Info("SkinTest", "Action B clicked");
            ImGui::SameLine();
            if (SROButton("##eb3", "Action C", bW, 24.f))
                log.Info("SkinTest", "Action C clicked");

            ImGui::Dummy({ 0.f, 6.f });
            ImGui::Separator();
            ImGui::Dummy({ 0.f, 6.f });
            ImGui::TextDisabled("Wide combo");
            ImGui::Spacing();
            SROCombo("##ec3", &s_c3, kItems3, 4, iW, 22.f);

            ImGui::Dummy({ 0.f, 8.f });
            ImGui::TextColored(ImVec4(0.8f, 0.7f, 0.3f, 1.f),
                "Selected: %s / %s / %s",
                kItems1[s_c1], kItems2[s_c2], kItems3[s_c3]);
        }
        SROEndSubFrame();
    }
    // Scroll
    else if (s_tab == 1) {
        ImGui::TextDisabled("Uses the SROWindow built-in scrollbar — resize to test.");
        ImGui::Spacing();
        for (int i = 1; i <= 25; ++i)
            ImGui::TextDisabled("Row %02d — scroll test content row", i);
    }
    // Info
    else {
        if (SROBeginSubFrame("##sf_info", availW, subH)) {
            ImGui::Dummy({ 0.f, 2.f });
            ImGui::TextDisabled("SROSkinWindow element library:");
            ImGui::Spacing();
            ImGui::BulletText("BeginSROWindow / EndSROWindow");
            ImGui::BulletText("SROTabBar  (long + short variants)");
            ImGui::BulletText("SROBeginSubFrame / SROEndSubFrame");
            ImGui::BulletText("SROCombo");
            ImGui::BulletText("SROButton  (+ disabled variant)");
            ImGui::BulletText("SROKeybind  (click box to remap)");
            ImGui::BulletText("SROCheckbox  (+ disabled variant)");
            ImGui::BulletText("Scrollbar  (built into EndSROWindow)");
            ImGui::Dummy({ 0.f, 6.f });
            ImGui::Separator();
            ImGui::Dummy({ 0.f, 6.f });
            ImGui::TextDisabled("DDJ source paths:");
            ImGui::Spacing();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.55f, 0.75f, 1.f, 1.f));
            ImGui::TextWrapped("interface\\option\\opt_long_tab_*.ddj");
            ImGui::TextWrapped("interface\\option\\opt_short_tab_*.ddj");
            ImGui::TextWrapped("interface\\option\\opt_key*.ddj");
            ImGui::TextWrapped("interface\\frame\\frame_sub_*.ddj");
            ImGui::TextWrapped("interface\\ifcommon\\com_button*.ddj");
            ImGui::TextWrapped("interface\\ifcommon\\com_checkbutton_*.ddj");
            ImGui::PopStyleColor();
        }
        SROEndSubFrame();
    }

    EndSROWindow();
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
            ReadClientVersion();
            g_dataPk2.Open(clientDir + L"\\data.pk2");
        }
        g_iconCache = new IconCache(device, g_pk2);
        SROSkin_Init(g_iconCache);
        SROSkin_InitSound(&g_dataPk2, params.hFocusWindow);
        g_rewardWindow.iconSource = g_iconCache;
        g_soxOverlay.texture = g_iconCache->Get("icon/item/etc/icon_edge_rare.ddj");
        initialized = true;
    }
    
    if (!ImGui::GetIO().WantTextInput && !SROSkin_IsCapturingKey())
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
    if (Settings::showWatermark) {
        const std::string wm = (g_clientVersion.empty() ? "V?.???" : "V1." + g_clientVersion)
                             + " BETA - @Dewwta";
        RenderWatermark(wm.c_str());
    }
    if (Settings::showFPSCounter) RenderFPS();

    if (showSessionStatsWindow) RenderSessionStats();
    if (showAdminToolsWindow)   RenderAdminTools();
    if (showSettingsWindow)     RenderSettings();
    if (showBotWindow)          RenderBotWindow(device);
    if (showSkinTest)           RenderSkinTest();
    if (showLogsWindow)         RenderLogsWindow();

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
        if (io.WantCaptureMouse && msg >= WM_MOUSEFIRST && msg <= 0x020E)
            return 0;
        // 0x0100-0x0109 = WM_KEYDOWN..WM_UNICHAR (incl. WM_SYSKEY*/WM_SYSCHAR)
        if (io.WantCaptureKeyboard && msg >= WM_KEYFIRST && msg <= 0x0109)
            return 0;
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