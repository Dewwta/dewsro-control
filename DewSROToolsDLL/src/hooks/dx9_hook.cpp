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

static std::string FormatSeconds(int totalSeconds) {
    int h = totalSeconds / 3600;
    int m = (totalSeconds % 3600) / 60;
    int s = totalSeconds % 60;
    char buf[16];
    snprintf(buf, sizeof(buf), "%02d:%02d:%02d", h, m, s);
    return buf;
}

SoxOverlay g_soxOverlay;
static bool initialized = false;
static ImFont* g_fontWatermark = nullptr;
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

typedef HRESULT(__stdcall* Present_t)(IDirect3DDevice9*, CONST RECT*, CONST RECT*, HWND, CONST RGNDATA*);
static Present_t oPresent = nullptr;

static bool showPlayerActionsWindow = false;
static bool showSettingsWindow = false;
static bool showAchWindow = false;
static bool AnyWindowOpen() {
    return showPlayerActionsWindow || showSettingsWindow || g_rewardWindow.isOpen || g_achWindow.isOpen;
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
    // 👇 bottom-left instead of bottom-right
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

static void RenderPlayerActions() {
    ImGui::SetNextWindowSize(ImVec2(300, 0), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(20, 20), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(260, 80), ImVec2(500, 600));
    ImGui::Begin("Player Actions", &showPlayerActionsWindow);
    PlayerState ps = g_bridge.m_state;
    State ss = g_bridge.m_sessionState;
    bool hasPlayer = !ps.charName.empty();
    ImGui::TextDisabled("PLAYER");
    ImGui::Separator();
    ImGui::Spacing();
    if (!hasPlayer)
    {
        ImGui::TextDisabled("Waiting for session...");
    }
    else
    {
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
        Row("Char", "%s", ps.charName.c_str());
        Row("Account", "%s", ps.accName.c_str());
        Row("JID", "%d", ps.accJID);
        Row("Level", "%d", ps.currentLevel);
        ImGui::Spacing();
        int elapsed = (ss.syncTick > 0)
            ? (int)((GetTickCount() - ss.syncTick) / 1000) : 0;
        std::string timeStr = FormatSeconds(ss.sessionSeconds + elapsed);
        ImGui::TextDisabled("Session");
        ImGui::SameLine(labelCol);
        ImGui::TextColored(ImVec4(0.55f, 0.85f, 0.55f, 1.0f), "%s", timeStr.c_str());
        if (ss.isAfk) {
            ImGui::SameLine();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.9f, 0.7f, 0.2f, 1.0f));
            ImGui::Text("[AFK]");
            ImGui::PopStyleColor();
        }

        std::string totalTimeStr = FormatSeconds(ss.totalSeconds + ss.sessionSeconds + elapsed);
        ImGui::TextDisabled("Total");
        ImGui::SameLine(labelCol);
        ImGui::TextColored(ImVec4(0.55f, 0.85f, 0.55f, 1.0f), "%s", totalTimeStr.c_str());

        ImGui::TextDisabled("Kills");
        ImGui::SameLine(labelCol);
        ImGui::TextColored(ImVec4(0.9f, 0.55f, 0.55f, 1.0f), "%d", ss.sessionKills);
        ImGui::TextDisabled("Gold");
        ImGui::SameLine(labelCol);
        char goldBuf[32];
        uint64_t g = ps.gold;
        if (g >= 1000000)
            snprintf(goldBuf, sizeof(goldBuf), "%llu,%03llu,%03llu",
                g / 1000000, (g / 1000) % 1000, g % 1000);
        else if (g >= 1000)
            snprintf(goldBuf, sizeof(goldBuf), "%llu,%03llu",
                g / 1000, g % 1000);
        else
            snprintf(goldBuf, sizeof(goldBuf), "%llu", g);
        ImGui::TextColored(ImVec4(1.0f, 0.85f, 0.3f, 1.0f), "%s", goldBuf);
    }

    ImGui::Spacing();
    ImGui::TextDisabled("INVENTORY");
    ImGui::Separator();
    ImGui::Spacing();

    ImGui::BeginDisabled(!hasPlayer);

    float avail = ImGui::GetContentRegionAvail().x;
    float gap = ImGui::GetStyle().ItemSpacing.x;
    float labelCol = 42.0f;
    float btnW = (avail - labelCol - gap * 3.0f) / 3.0f;

    if (ImGui::BeginTable("SortTable", 4, ImGuiTableFlags_None))
    {
        ImGui::TableSetupColumn("##label", ImGuiTableColumnFlags_WidthFixed, labelCol);
        ImGui::TableSetupColumn("Type", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Name", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Logical", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableHeadersRow();

        // Player row
        ImGui::TableNextRow();
        ImGui::TableSetColumnIndex(0); ImGui::TextDisabled("Player");
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.13f, 0.25f, 0.45f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.18f, 0.35f, 0.60f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.08f, 0.18f, 0.35f, 1.0f));
        ImGui::TableSetColumnIndex(1); if (ImGui::Button("T##ps", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::ByType, SortTarget::Player);
        ImGui::TableSetColumnIndex(2); if (ImGui::Button("N##ps", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::ByName, SortTarget::Player);
        ImGui::TableSetColumnIndex(3); if (ImGui::Button("L##ps", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::Logical, SortTarget::Player);
        ImGui::PopStyleColor(3);

        // Pet row
        ImGui::TableNextRow();
        ImGui::TableSetColumnIndex(0); ImGui::TextDisabled("Pet");
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.13f, 0.38f, 0.25f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.18f, 0.52f, 0.35f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.08f, 0.25f, 0.16f, 1.0f));
        ImGui::TableSetColumnIndex(1); if (ImGui::Button("T##pt", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::ByType, SortTarget::Pet);
        ImGui::TableSetColumnIndex(2); if (ImGui::Button("N##pt", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::ByName, SortTarget::Pet);
        ImGui::TableSetColumnIndex(3); if (ImGui::Button("L##pt", ImVec2(-1, 22))) NetActions::SendSortRequest(SortType::Logical, SortTarget::Pet);
        ImGui::PopStyleColor(3);

        ImGui::EndTable();
    }

    ImGui::EndDisabled();

    if (!g_bridge.unclaimedRewards.empty())
    {
        ImGui::Spacing();
        ImGui::TextDisabled("PENDING REWARDS");
        ImGui::Separator();
        ImGui::Spacing();
        const float pillH = 22.0f;
        const float pillPadX = 10.0f;
        float lineW = ImGui::GetContentRegionAvail().x;
        float cursorX = ImGui::GetCursorPosX();
        float startX = cursorX;
        bool firstOnLine = true;
        for (int lvl : g_bridge.unclaimedRewards)
        {
            char label[16];
            snprintf(label, sizeof(label), "Lv %d", lvl);
            ImVec2 textSz = ImGui::CalcTextSize(label);
            float pillW = textSz.x + pillPadX * 2.0f;
            if (!firstOnLine && (cursorX + pillW > startX + lineW))
            {
                ImGui::NewLine();
                cursorX = startX;
                firstOnLine = true;
            }
            if (!firstOnLine) {
                ImGui::SameLine(0.0f, 4.0f);
                cursorX += pillW + 4.0f;
            }
            else {
                cursorX += pillW;
            }
            firstOnLine = false;
            ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.45f, 0.30f, 0.05f, 1.0f));
            ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.65f, 0.45f, 0.08f, 1.0f));
            ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.30f, 0.20f, 0.03f, 1.0f));
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
    ImGui::End();
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
    ImGui::TextDisabled("MORE COMING SOON");
    ImGui::Separator();
    ImGui::End();
}

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
        g_soxOverlay.Load(device, "icon\\item\\etc\\icon_edge_rare.png");
        ImGui_ImplWin32_Init(params.hFocusWindow);
        ImGui_ImplDX9_Init(device);
        oWndProc = (WNDPROC)GetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC);
        SetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC, (LONG)hkWndProc);
        g_rewardWindow.device = device;
        initialized = true;
    }
    if (GetAsyncKeyState(Settings::showPlayerActionsKey) & 1) showPlayerActionsWindow = !showPlayerActionsWindow;
    if (GetAsyncKeyState(Settings::showSettingsKey) & 1) showSettingsWindow = !showSettingsWindow;
    if (GetAsyncKeyState(Settings::showAchKey) & 1) {
        if (g_achWindow.isOpen) {
            g_achWindow.Close();
        }
        else {
            NetActions::SendAchievementsRequest();
        }
    }
        
    ImGui_ImplDX9_NewFrame();
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();
    if (Settings::showWatermark) RenderWatermark("V1.201 BETA - @Dewwta");
    if (Settings::showFPSCounter) RenderFPS();

    if (showPlayerActionsWindow) RenderPlayerActions();
    if (showSettingsWindow) RenderSettings();
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
    g_rewardWindow.ReleaseIcons();
    g_soxOverlay.Release();
    HRESULT hr = oReset(device, pp);
    if (SUCCEEDED(hr))
    {
        s_deviceLost = false;
        ImGui_ImplDX9_CreateDeviceObjects();
        g_soxOverlay.Load(device, "icon\\item\\etc\\icon_edge_rare.png");
        g_rewardWindow.device = device;
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
}