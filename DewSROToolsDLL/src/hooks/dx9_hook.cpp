#include "dx9_hook.h"
#include "imgui.h"
#include "imgui_impl_dx9.h"
#include "imgui_impl_win32.h"
#include <d3d9.h>
#include <MinHook.h>
#include "../net/NetActions.h"
#include "../Settings.h"
#include "../net/DllBridge.h"
#include "../net/LoginHook.h"
#include "../Interfaces/PlayerInterface.h";

static PlayerInterface g_player;

static bool initialized = false;
extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
typedef HRESULT(__stdcall* Present_t)(IDirect3DDevice9*, CONST RECT*, CONST RECT*, HWND, CONST RGNDATA*);
static Present_t oPresent = nullptr;
static bool showToolsWindow = false; // Test window

typedef IDirect3D9* (__stdcall* Direct3DCreate9_t)(UINT);
static Direct3DCreate9_t oCreate = nullptr;

typedef HRESULT(__stdcall* CreateDevice_t)(IDirect3D9*, UINT, D3DDEVTYPE, HWND, DWORD, D3DPRESENT_PARAMETERS*, IDirect3DDevice9**);
static CreateDevice_t oCreateDevice = nullptr;

typedef HRESULT(__stdcall* Reset_t)(IDirect3DDevice9*, D3DPRESENT_PARAMETERS*);
static Reset_t oReset = nullptr;

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

    float paddingX = 10.0f;  // distance from right edge
    float paddingY = 10.0f;  // distance from bottom edge
    ImVec2 textSize = ImGui::CalcTextSize(text);

    // Account for window padding on both sides
    float windowWidth = textSize.x + style.WindowPadding.x * 2;
    float windowHeight = textSize.y + style.WindowPadding.y * 2;

    ImVec2 pos = ImVec2(
        io.DisplaySize.x - windowWidth - paddingX,
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
    ImGui::Begin("##watermark", nullptr, flags);
    ImGui::TextColored(ImVec4(1.0f, 0.4f, 0.4f, 0.8f), text);
    ImGui::End();
    ImGui::PopStyleVar();
}
static void RenderTools() {
    ImGui::SetNextWindowSize(ImVec2(320, 0), ImGuiCond_Always);
    ImGui::SetNextWindowPos(ImVec2(20, 20), ImGuiCond_FirstUseEver);

    int currentLevel = 0;
    short availablePoints = 0;

    if (g_pMyPlayerObj)
    {
        currentLevel = g_player.GetCurrentLevel();
        availablePoints = g_player.GetStatPoints();
    }

    ImGui::Begin("VSRO Tools", &showToolsWindow);

    // DEBUG
    ImGui::TextDisabled("DEBUG / PLAYER TEST");
    ImGui::Separator();
    ImGui::Text("Player pointer: %s", g_pMyPlayerObj ? "VALID" : "NULL");
    if (g_pMyPlayerObj)
    {
        ImGui::Text("Level: %d", currentLevel);
        ImGui::Text("Stat Points: %d", availablePoints);
    }
    else
    {
        ImGui::TextColored(ImVec4(1, 0, 0, 1), "Player not loaded yet");
    }

    ImGui::Spacing();
    ImGui::Separator();

    // INVENTORY
    ImGui::TextDisabled("INVENTORY");
    ImGui::Separator();
    ImGui::Spacing();
    ImGui::Text("Sort");
    ImGui::Spacing();

    float btnWidth = (ImGui::GetContentRegionAvail().x - ImGui::GetStyle().ItemSpacing.x) / 2.0f;
    if (ImGui::Button("By Type", ImVec2(btnWidth, 28)))
        NetActions::SendSortRequest(SortType::ByType);
    ImGui::SameLine();
    if (ImGui::Button("By Name", ImVec2(btnWidth, 28)))
        NetActions::SendSortRequest(SortType::ByName);

    ImGui::Spacing();

    // GENERAL
    ImGui::TextDisabled("GENERAL");
    ImGui::Separator();
    ImGui::Spacing();
    bool kf = Settings::keepFocused;
    if (ImGui::Checkbox("Keep Focus", &kf))
    {
        Settings::keepFocused = kf;
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

        ImGui::CreateContext();
        SetupImGuiStyle();
        ImGui::GetIO().ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange;
        ImGui_ImplWin32_Init(params.hFocusWindow);
        ImGui_ImplDX9_Init(device);

        oWndProc = (WNDPROC)GetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC);
        SetWindowLongPtrA(params.hFocusWindow, GWLP_WNDPROC, (LONG)hkWndProc);

        g_bridge.SetIdentity("dewwta"); // or pull from settings/login hook
        g_bridge.Connect();


        initialized = true;
    }

    if (GetAsyncKeyState('Z') & 1)
        showToolsWindow = !showToolsWindow;

    ImGui_ImplDX9_NewFrame();
    ImGui_ImplWin32_NewFrame();
    ImGui::NewFrame();

    RenderWatermark("V1.199 BETA - @Dewwta");  // always visible

    if (showToolsWindow)
        RenderTools();

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
HRESULT __stdcall hkCreateDevice(IDirect3D9* d3d, UINT adapter, D3DDEVTYPE devType, HWND hwnd, DWORD flags, D3DPRESENT_PARAMETERS* pp, IDirect3DDevice9** outDevice)
{
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
    if (showToolsWindow) {
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
    HMODULE d3d9 = GetModuleHandleA("d3d9.dll");
    void* create9addr = GetProcAddress(d3d9, "Direct3DCreate9");

    MH_Initialize();
    MH_CreateHook(create9addr, hkDirect3DCreate9,
        reinterpret_cast<void**>(&oCreate));
    MH_EnableHook(create9addr);
    //MessageBoxA(nullptr, "Create9 hook installed", "ok", MB_OK);
    InstallLoginHook();
}

void dx9_hook::notify(const char* msg) {
    MessageBoxA(nullptr, msg, "ok", MB_OK);
}