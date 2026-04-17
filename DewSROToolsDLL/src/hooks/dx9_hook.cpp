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

static std::string FormatSeconds(int totalSeconds) {
    int h = totalSeconds / 3600;
    int m = (totalSeconds % 3600) / 60;
    int s = totalSeconds % 60;
    char buf[16];
    snprintf(buf, sizeof(buf), "%02d:%02d:%02d", h, m, s);
    return buf;
}


static bool initialized = false;
static ImFont* g_fontWatermark = nullptr;

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
typedef HRESULT(__stdcall* Present_t)(IDirect3DDevice9*, CONST RECT*, CONST RECT*, HWND, CONST RGNDATA*);
static Present_t oPresent = nullptr;

static bool showPlayerActionsWindow = false;
static bool showSettingsWindow = false;

static bool AnyWindowOpen() {
    return showPlayerActionsWindow || showSettingsWindow;
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
    ImGui::SetNextWindowSize(ImVec2(320, 0), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowPos(ImVec2(20, 20), ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(ImVec2(260, 100), ImVec2(500, 600));

    ImGui::Begin("Player Actions", &showPlayerActionsWindow);

    PlayerState ps = g_bridge.m_state;
    State ss = g_bridge.m_sessionState;
    bool hasPlayer = !ps.charName.empty();

    // ── PLAYER INFO ─────────────────────────────────────
    ImGui::TextDisabled("PLAYER");
    ImGui::Separator();
    ImGui::Spacing();

    if (!hasPlayer) {
        ImGui::TextDisabled("Waiting for session...");
    }
    else {
        float col1 = 90.0f;

        ImGui::Text("Character"); ImGui::SameLine(col1);
        ImGui::TextColored(ImVec4(0.75f, 0.88f, 1.0f, 1.0f), "%s", ps.charName.c_str());

        ImGui::Text("Account"); ImGui::SameLine(col1);
        ImGui::TextColored(ImVec4(0.75f, 0.88f, 1.0f, 1.0f), "%s", ps.accName.c_str());

        ImGui::Text("JID"); ImGui::SameLine(col1);
        ImGui::TextColored(ImVec4(0.75f, 0.88f, 1.0f, 1.0f), "%d", ps.accJID);

        ImGui::Spacing();

        ImGui::Text("Session"); ImGui::SameLine(col1);
        
        int elapsed = 0;
        if (ss.syncTick > 0)
            elapsed = (int)((GetTickCount() - ss.syncTick) / 1000);

        std::string sessionStr = FormatSeconds(ss.sessionSeconds + elapsed);
        ImGui::TextColored(ImVec4(0.6f, 0.9f, 0.6f, 1.0f), "%s%s",
            sessionStr.c_str(),
            ss.isAfk ? "  [AFK]" : "");

        ImGui::Text("Kills"); ImGui::SameLine(col1);
        ImGui::TextColored(ImVec4(0.9f, 0.6f, 0.6f, 1.0f), "%d", ss.sessionKills);
    }

    ImGui::Spacing();

    // ── INVENTORY ───────────────────────────────────────
    ImGui::TextDisabled("INVENTORY");
    ImGui::Separator();
    ImGui::Spacing();
    ImGui::TextDisabled("Sort");
    ImGui::Spacing();

    float btnWidth = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x * 2) / 3.0f;
    if (ImGui::Button("By Type", ImVec2(btnWidth, 28)))
        NetActions::SendSortRequest(SortType::ByType);
    ImGui::SameLine();
    if (ImGui::Button("By Name", ImVec2(btnWidth, 28)))
        NetActions::SendSortRequest(SortType::ByName);
    ImGui::SameLine();
    if (ImGui::Button("Logical", ImVec2(btnWidth, 28)))
        NetActions::SendSortRequest(SortType::Logical);

    ImGui::Spacing();
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
HRESULT __stdcall hkPresent(IDirect3DDevice9* device, CONST RECT* pSrcRect, CONST RECT* pDestRect, HWND hDestWindow, CONST RGNDATA* pDirtyRegion)
{
    if (!initialized)
    {
        Settings::Load();
        D3DDEVICE_CREATION_PARAMETERS params;
        device->GetCreationParameters(&params);
        g_gameHwnd = params.hFocusWindow;
        ImGui::CreateContext();
        SetupImGuiStyle();

        ImGuiIO& io = ImGui::GetIO();
        io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;

        // Load watermark font
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

        initialized = true;
    }

    if (GetAsyncKeyState(Settings::showPlayerActionsKey) & 1) showPlayerActionsWindow = !showPlayerActionsWindow;
    if (GetAsyncKeyState(Settings::showSettingsKey) & 1) showSettingsWindow = !showSettingsWindow;

    ImGui_ImplDX9_NewFrame();
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();

    if (Settings::showWatermark) RenderWatermark("V1.199 BETA - @Dewwta");

    if (Settings::showFPSCounter) RenderFPS();
    
    if (showPlayerActionsWindow)    RenderPlayerActions();
    if (showSettingsWindow) RenderSettings();

    ImGui::EndFrame();
    ImGui::Render();
    ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());

    return oPresent(device, pSrcRect, pDestRect, hDestWindow, pDirtyRegion);
}

HRESULT __stdcall hkReset(IDirect3DDevice9* device, D3DPRESENT_PARAMETERS* pp)
{
    ImGui_ImplDX9_InvalidateDeviceObjects();
    HRESULT hr = oReset(device, pp);
    if (SUCCEEDED(hr))
        ImGui_ImplDX9_CreateDeviceObjects();
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
    //MessageBoxA(nullptr, "Create9 hook installed", "ok", MB_OK);
    log.Info("dx9_hook::init", "Installing login hook");
    InstallLoginHook();
}

// Only for debugging, do not use this in release stupid
void dx9_hook::notify(const char* msg) {
    MessageBoxA(nullptr, msg, "ok", MB_OK);
}

