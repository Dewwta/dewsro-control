#include "SROSkinWindow.h"
#include <algorithm>
#include <cstdlib>
#include <unordered_map>
#include <unordered_set>
#include <string>
#include <vector>
#include <dsound.h>
#pragma comment(lib, "dsound.lib")

// Natural DDJ pixel dimensions
static constexpr float K_BORDER  = 16.f;   // left/right border strip width
static constexpr float K_TITLE   = 40.f;   // top frame strip height
static constexpr float K_BOT     = 16.f;   // bottom frame strip height
static constexpr float K_TILE    = 128.f;  // bg tile natural size
static constexpr float K_SCR_W   = 16.f;   // scrollbar element width
static constexpr float K_SCR_H   = 16.f;   // scrollbar element height

// Title text center as fraction of K_TITLE.
// 0.35 places the center at row ~12.6 of the 36px mid_up
static constexpr float K_TITLE_Y = 0.35f;
// K_TAB_H, K_TABS_H, K_SF_C defined in SROSkinWindow.h

// Cached textures
static IconCache*         s_cache  = nullptr;
static bool               s_ready  = false;
static IDirect3DTexture9* s_TL = nullptr, *s_TC = nullptr, *s_TR = nullptr;
static IDirect3DTexture9* s_ML = nullptr,                  *s_MR = nullptr;
static IDirect3DTexture9* s_BL = nullptr, *s_BC = nullptr, *s_BR = nullptr;
static IDirect3DTexture9* s_bg    = nullptr;
static IDirect3DTexture9* s_sBtn  = nullptr, *s_sBtnF = nullptr, *s_sBtnP = nullptr;
static IDirect3DTexture9* s_sBar  = nullptr;
static IDirect3DTexture9* s_closeN = nullptr, *s_closeF = nullptr, *s_closeP = nullptr;

static IDirect3DTexture9* s_arrowU = nullptr, *s_arrowUF = nullptr, *s_arrowUP = nullptr;
static IDirect3DTexture9* s_arrowD = nullptr, *s_arrowDF = nullptr, *s_arrowDP = nullptr;
// SROCombo + SROButton textures
static IDirect3DTexture9* s_comboBox  = nullptr;
static IDirect3DTexture9* s_cbArrowN  = nullptr, *s_cbArrowF  = nullptr, *s_cbArrowP  = nullptr;
static IDirect3DTexture9* s_btnN      = nullptr, *s_btnF      = nullptr, *s_btnP      = nullptr, *s_btnDis = nullptr;
// Tab textures
static IDirect3DTexture9* s_tabLOn  = nullptr, *s_tabLOff = nullptr, *s_tabLDis = nullptr;
static IDirect3DTexture9* s_tabSOn  = nullptr, *s_tabSOff = nullptr, *s_tabSDis = nullptr;
// Sub-frame 8-piece border (frame_sub_*)
static IDirect3DTexture9* s_sfTL = nullptr, *s_sfTC = nullptr, *s_sfTR = nullptr;
static IDirect3DTexture9* s_sfML = nullptr,                    *s_sfMR = nullptr;
static IDirect3DTexture9* s_sfBL = nullptr, *s_sfBC = nullptr, *s_sfBR = nullptr;
// SROKeybind + SROCheckbox textures
static IDirect3DTexture9* s_optKey    = nullptr;  // opt_key.ddj       156×28
static IDirect3DTexture9* s_optKeySel = nullptr;  // opt_key_select.ddj 56×16
static IDirect3DTexture9* s_cbOff     = nullptr;  // com_checkbutton_off.ddj     16×16
static IDirect3DTexture9* s_cbOn      = nullptr;  // com_checkbutton_on.ddj      16×16
static IDirect3DTexture9* s_cbDis     = nullptr;  // com_checkbutton_on_disable.ddj 16×16
// SROInputBar / SROListItem textures
static IDirect3DTexture9* s_bar01L    = nullptr;  // com_bar01_left.ddj
static IDirect3DTexture9* s_bar01M    = nullptr;  // com_bar01_mid.ddj
static IDirect3DTexture9* s_bar01R    = nullptr;  // com_bar01_right.ddj
static IDirect3DTexture9* s_bar01selL = nullptr;  // com_bar01select_left.ddj
static IDirect3DTexture9* s_bar01selM = nullptr;  // com_bar01select_mid.ddj
static IDirect3DTexture9* s_bar01selR = nullptr;  // com_bar01select_right.ddj
// SROStatusRow textures
static IDirect3DTexture9* s_strSlot   = nullptr;  // str_slot_01.ddj
// SROActionFrame textures (frame_g_action_*)
static IDirect3DTexture9* s_afTL = nullptr, *s_afTC = nullptr, *s_afTR = nullptr;
static IDirect3DTexture9* s_afML = nullptr,                    *s_afMR = nullptr;
static IDirect3DTexture9* s_afBL = nullptr, *s_afBC = nullptr, *s_afBR = nullptr;
static IDirect3DTexture9* s_bgTileA = nullptr; // com_bg_tile_a.ddj (action frame bg)

// DirectSound playback for open/close sounds
static IDirectSound8*      s_ds       = nullptr;
static IDirectSoundBuffer* s_bufOpen  = nullptr;
static IDirectSoundBuffer* s_bufClose = nullptr;
static IDirectSoundBuffer* s_bufBtnA  = nullptr;
static IDirectSoundBuffer* s_bufBtnB  = nullptr;

struct WavView {
    const WAVEFORMATEX* fmt  = nullptr;
    const uint8_t*      data = nullptr;
    DWORD               size = 0;
};

static WavView ParseWAV(const std::vector<uint8_t>& wav) {
    WavView v;
    if (wav.size() < 12) return v;
    const uint8_t* p   = wav.data();
    const uint8_t* end = p + wav.size();
    if (memcmp(p, "RIFF", 4) != 0 || memcmp(p + 8, "WAVE", 4) != 0) return v;
    p += 12;
    while (p + 8 <= end) {
        DWORD chunkSize;
        memcpy(&chunkSize, p + 4, 4);
        if (memcmp(p, "fmt ", 4) == 0 && chunkSize >= 16)
            v.fmt = reinterpret_cast<const WAVEFORMATEX*>(p + 8);
        else if (memcmp(p, "data", 4) == 0 && p + 8 + chunkSize <= end) {
            v.data = p + 8;
            v.size = chunkSize;
        }
        p += 8 + ((chunkSize + 1u) & ~1u);
    }
    return v;
}

static IDirectSoundBuffer* MakeDSBuffer(const std::vector<uint8_t>& wav) {
    if (!s_ds || wav.empty()) return nullptr;
    WavView v = ParseWAV(wav);
    if (!v.fmt || !v.data || v.size == 0) return nullptr;

    DSBUFFERDESC desc = {};
    desc.dwSize        = sizeof(desc);
    desc.dwFlags       = DSBCAPS_STATIC | DSBCAPS_GLOBALFOCUS |
                         DSBCAPS_CTRLVOLUME | DSBCAPS_LOCSOFTWARE;
    desc.dwBufferBytes = v.size;
    desc.lpwfxFormat   = const_cast<WAVEFORMATEX*>(v.fmt);

    IDirectSoundBuffer* buf = nullptr;
    if (FAILED(s_ds->CreateSoundBuffer(&desc, &buf, nullptr))) return nullptr;

    void* p1 = nullptr; DWORD b1 = 0;
    void* p2 = nullptr; DWORD b2 = 0;
    if (SUCCEEDED(buf->Lock(0, v.size, &p1, &b1, &p2, &b2, 0))) {
        if (p1) memcpy(p1, v.data, b1);
        if (p2) memcpy(p2, v.data + b1, b2);
        buf->Unlock(p1, b1, p2, b2);
        return buf;
    }
    buf->Release();
    return nullptr;
}

static void PlaySROSnd(IDirectSoundBuffer* buf) {
    if (!buf) return;
    buf->SetCurrentPosition(0);
    buf->Play(0, 0, 0);
}

// Per-frame window visibility tracking
static std::unordered_set<size_t> s_visThisFrame, s_visLastFrame;

// scroll state, keyed by hash of the window id string
struct SROScrollState {
    float y = 0.f, maxY = 0.f;
    float dragStartMouseY = 0.f, dragStartScrollY = 0.f;
    bool  draggingThumb = false;
};
static std::unordered_map<size_t, SROScrollState> s_scroll;
static std::unordered_map<size_t, SROScrollState> s_afScroll;

// Window frame stack
struct SROFrame {
    ImVec2 p, sz;
    size_t id;   // std::hash of the id string passed to BeginSROWindow
    bool*  pOpen;
    float   bodyTop, bodyBot;
    float   scrX0,   scrX1;
    float   trackTop, trackH;
};
static constexpr int MAX_DEPTH = 4;
static SROFrame s_stack[MAX_DEPTH];
static int      s_depth = 0;

struct SRODragState { size_t wid = 0; ImVec2 offset = {}; bool active = false; };
static SRODragState s_drag;

static inline ImTextureRef TI(IDirect3DTexture9* t) {
    return ImTextureRef((ImTextureID)(uintptr_t)t);
}

static void EnsureLoaded()
{
    if (s_ready || !s_cache) return;
    auto F = [](const char* piece) -> IDirect3DTexture9* {
        char buf[256];
        snprintf(buf, sizeof(buf), "interface\\frame\\sframe_wnd_%s.ddj", piece);
        return s_cache->Get(buf);
    };
    auto C = [](const char* path) -> IDirect3DTexture9* {
        return s_cache->Get(path);
    };
    s_TL = F("left_up");    s_TC = F("mid_up");    s_TR = F("right_up");
    s_ML = F("left_side");                          s_MR = F("right_side");
    s_BL = F("left_down");  s_BC = F("mid_down");  s_BR = F("right_down");
    s_bg    = C("interface\\ifcommon\\bg_tile\\com_bg_tile_b.ddj");
    s_sBtn   = C("interface\\ifcommon\\com_scroll_button.ddj");
    s_sBtnF  = C("interface\\ifcommon\\com_scroll_button_focus.ddj");
    s_sBtnP  = C("interface\\ifcommon\\com_scroll_button_press.ddj");
    s_sBar   = C("interface\\ifcommon\\com_scroll_bar.ddj");
    s_closeN = C("interface\\ifcommon\\com_windowclose.ddj");
    s_closeF = C("interface\\ifcommon\\com_windowclose_focus.ddj");
    s_closeP = C("interface\\ifcommon\\com_windowclose_press.ddj");
    s_arrowU  = C("interface\\chattingwnd\\chat_arrow_up.ddj");
    s_arrowUF = C("interface\\chattingwnd\\chat_arrow_up_focus.ddj");
    s_arrowUP = C("interface\\chattingwnd\\chat_arrow_up_press.ddj");
    s_arrowD  = C("interface\\chattingwnd\\chat_arrow_down.ddj");
    s_arrowDF = C("interface\\chattingwnd\\chat_arrow_down_focus.ddj");
    s_arrowDP = C("interface\\chattingwnd\\chat_arrow_down_press.ddj");
    s_comboBox  = C("interface\\mall\\mall_box_02.ddj");
    s_cbArrowN  = C("interface\\ifcommon\\com_qst_rightarrow_button.ddj");
    s_cbArrowF  = C("interface\\ifcommon\\com_qst_rightarrow_button_focus.ddj");
    s_cbArrowP  = C("interface\\ifcommon\\com_qst_rightarrow_button_press.ddj");
    s_btnN   = C("interface\\ifcommon\\com_button.ddj");
    s_btnF   = C("interface\\ifcommon\\com_button_focus.ddj");
    s_btnP   = C("interface\\ifcommon\\com_button_press.ddj");
    s_btnDis = C("interface\\ifcommon\\com_button_disable.ddj");
    // Tabs (option window)
    s_tabLOn  = C("interface\\option\\opt_long_tab_on.ddj");
    s_tabLOff = C("interface\\option\\opt_long_tab_off.ddj");
    s_tabLDis = C("interface\\option\\opt_long_tab_disabled.ddj");
    s_tabSOn  = C("interface\\option\\opt_short_tab_on.ddj");
    s_tabSOff = C("interface\\option\\opt_short_tab_off.ddj");
    s_tabSDis = C("interface\\option\\opt_short_tab_disabled.ddj");
    // Sub-frame 8-piece border (frame_sub_left_up)
    auto SF = [](const char* p) -> IDirect3DTexture9* {
        char buf[256];
        snprintf(buf, sizeof(buf), "interface\\frame\\frame_sub_%s.ddj", p);
        return s_cache->Get(buf);
    };
    s_sfTL = SF("left_up");   s_sfTC = SF("mid_up");   s_sfTR = SF("right_up");
    s_sfML = SF("left_side");                           s_sfMR = SF("right_side");
    s_sfBL = SF("left_down"); s_sfBC = SF("mid_down"); s_sfBR = SF("right_down");
    s_optKey    = C("interface\\option\\opt_key.ddj");
    s_optKeySel = C("interface\\option\\opt_key_select.ddj");
    s_cbOff  = C("interface\\ifcommon\\com_checkbutton_off.ddj");
    s_cbOn   = C("interface\\ifcommon\\com_checkbutton_on.ddj");
    s_cbDis  = C("interface\\ifcommon\\com_checkbutton_on_disable.ddj");
    s_bar01L    = C("interface\\ifcommon\\com_bar01_left.ddj");
    s_bar01M    = C("interface\\ifcommon\\com_bar01_mid.ddj");
    s_bar01R    = C("interface\\ifcommon\\com_bar01_right.ddj");
    s_bar01selL = C("interface\\ifcommon\\ifcommon\\com_bar01select_left.ddj");
    s_bar01selM = C("interface\\ifcommon\\ifcommon\\com_bar01select_mid.ddj");
    s_bar01selR = C("interface\\ifcommon\\ifcommon\\com_bar01select_right.ddj");
    s_strSlot   = C("interface\\store\\str_slot_01.ddj");
    auto AF = [](const char* p) -> IDirect3DTexture9* {
        char buf[256];
        snprintf(buf, sizeof(buf), "interface\\frame\\frame_g_action_%s.ddj", p);
        return s_cache->Get(buf);
    };
    s_afTL = AF("left_up");   s_afTC = AF("mid_up");   s_afTR = AF("right_up");
    s_afML = AF("left_side");                           s_afMR = AF("right_side");
    s_afBL = AF("left_down"); s_afBC = AF("mid_down");  s_afBR = AF("right_down");
    s_bgTileA = C("interface\\ifcommon\\bg_tile\\com_bg_tile_a.ddj");
    s_ready = true;
}

void SROSkin_Init(IconCache* cache)
{
    s_cache = cache;
    s_ready = false;  // force a reload with the new cache
}

void SROSkin_InitSound(Pk2Reader* dataPk2, HWND hwnd)
{
    if (!dataPk2 || !dataPk2->IsOpen()) return;
    if (FAILED(DirectSoundCreate8(nullptr, &s_ds, nullptr))) return;
 
    s_ds->SetCooperativeLevel(hwnd, DSSCL_NORMAL);

    std::vector<uint8_t> wavOpen, wavClose, wavBtnA, wavBtnB;
    dataPk2->ReadFile("prim\\snd\\ui\\uiwinopen.wav",  wavOpen);
    dataPk2->ReadFile("prim\\snd\\ui\\uiwinclose.wav", wavClose);
    dataPk2->ReadFile("prim\\snd\\ui\\uibutton_a.wav", wavBtnA);
    dataPk2->ReadFile("prim\\snd\\ui\\uibutton_b.wav", wavBtnB);
    s_bufOpen  = MakeDSBuffer(wavOpen);
    s_bufClose = MakeDSBuffer(wavClose);
    s_bufBtnA  = MakeDSBuffer(wavBtnA);
    s_bufBtnB  = MakeDSBuffer(wavBtnB);
}

void SROSkin_PlayClick()
{
    // Randomize between the two click sounds
    IDirectSoundBuffer* pick = (std::rand() & 1) ? s_bufBtnA : s_bufBtnB;
    if (!pick) pick = s_bufBtnA ? s_bufBtnA : s_bufBtnB; // fall back if one failed to load
    PlaySROSnd(pick);
}

void SROSkin_NewFrame()
{
    // Any window in last frame's set that isn't in this frame's set just closed.
    for (size_t wid : s_visLastFrame)
        if (s_visThisFrame.find(wid) == s_visThisFrame.end())
            PlaySROSnd(s_bufClose);

    s_visLastFrame = std::move(s_visThisFrame);
    s_visThisFrame.clear();
}

bool BeginSROWindow(const char* id, const char* title, bool* open,
                    ImVec2 default_size, ImVec2 default_pos,
                    ImVec2 size_min,     ImVec2 size_max,    bool resizable)
{
    EnsureLoaded();

    const size_t thisWid = std::hash<std::string>{}(id);

    bool applyingDrag = false;
    if (s_drag.active && s_drag.wid == thisWid) {
        if (ImGui::IsMouseDown(ImGuiMouseButton_Left)) {
            const ImVec2 mp = ImGui::GetIO().MousePos;
            ImGui::SetNextWindowPos({ mp.x - s_drag.offset.x, mp.y - s_drag.offset.y });
            applyingDrag = true;
        } else {
            s_drag.active = false;
        }
    }
    if (!applyingDrag && default_pos.x >= 0.f)
        ImGui::SetNextWindowPos(default_pos, ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSize(default_size, ImGuiCond_FirstUseEver);
    if (resizable)
        ImGui::SetNextWindowSizeConstraints(size_min, size_max);

    ImGui::PushStyleColor(ImGuiCol_WindowBg,      ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_Border,        ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_TitleBg,       ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_TitleBgActive, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding,
        ImVec2(K_BORDER + 4.f, K_TITLE + 4.f));

    ImGuiWindowFlags wflags = ImGuiWindowFlags_NoTitleBar        |
                              ImGuiWindowFlags_NoScrollbar       |
                              ImGuiWindowFlags_NoScrollWithMouse |
                              ImGuiWindowFlags_NoCollapse        |
                              ImGuiWindowFlags_NoMove;
    if (!resizable) wflags |= ImGuiWindowFlags_NoResize;

    bool vis = ImGui::Begin(id, open, wflags); // NoMove: custom drag; stops ImGui treating close-click as drag-start
    ImGui::PopStyleColor(4);
    ImGui::PopStyleVar();

    if (!vis || s_depth >= MAX_DEPTH) { ImGui::End(); return false; }

    SROFrame& fr = s_stack[s_depth++];
    fr.p     = ImGui::GetWindowPos();
    fr.sz    = ImGui::GetWindowSize();
    fr.id    = thisWid;
    fr.pOpen = open;

    if (s_visLastFrame.find(fr.id) == s_visLastFrame.end())
        PlaySROSnd(s_bufOpen);
    s_visThisFrame.insert(fr.id);

    const float px = fr.p.x, py = fr.p.y, pw = fr.sz.x, ph = fr.sz.y;
    ImDrawList* dl = ImGui::GetWindowDrawList();

    // Base fill
    dl->AddRectFilled({ px, py }, { px+pw, py+ph }, IM_COL32(18, 17, 22, 255));

    // Frame pieces render on top and cover the edges, eliminating seams.
    {
        float bx0=px, bx1=px+pw, by0=py+K_TITLE, by1=py+ph-K_BOT;
        if (s_bg && bx1 > bx0 && by1 > by0)
            for (float ty = by0; ty < by1; ty += K_TILE)
                for (float tx = bx0; tx < bx1; tx += K_TILE) {
                    float tw = (std::min)(tx+K_TILE, bx1) - tx;
                    float th = (std::min)(ty+K_TILE, by1) - ty;
                    dl->AddImage(TI(s_bg), { tx,ty }, { tx+tw,ty+th },
                        { 0,0 }, { tw/K_TILE,th/K_TILE });
                }
        else
            dl->AddRectFilled({ bx0,by0 }, { bx1,by1 }, IM_COL32(50, 52, 68, 255));
    }

    // Frame pieces
    auto I = [&](IDirect3DTexture9* t, ImVec2 a, ImVec2 b) {
        if (t) dl->AddImage(TI(t), a, b);
    };
    //Corners
    I(s_TL, { px,             py              }, { px+K_BORDER,   py+K_TITLE });
    I(s_TR, { px+pw-K_BORDER, py              }, { px+pw,         py+K_TITLE });
    I(s_BL, { px,             py+ph-K_BOT     }, { px+K_BORDER,   py+ph      });
    I(s_BR, { px+pw-K_BORDER, py+ph-K_BOT     }, { px+pw,         py+ph      });
    // Top/bottom strips
    const float hl = px+K_BORDER, hr = px+pw-K_BORDER;
    if (hr > hl) {
        I(s_TC, { hl, py          }, { hr, py+K_TITLE });
        I(s_BC, { hl, py+ph-K_BOT }, { hr, py+ph      });
    }
    // Side strips
    const float vt = py+K_TITLE, vb = py+ph-K_BOT;
    if (vb > vt) {
        I(s_ML, { px,             vt }, { px+K_BORDER, vb });
        I(s_MR, { px+pw-K_BORDER, vt }, { px+pw,       vb });
    }

    // Title text
    {
        const float midY = py + K_TITLE * K_TITLE_Y;
        ImVec2 tSz = ImGui::CalcTextSize(title);
        dl->AddText({ px + pw*0.5f - tSz.x*0.5f, midY - tSz.y*0.5f },
            IM_COL32(255, 220, 150, 255), title);
    }

    // Close button
    if (open) {
        const float BSZ = K_SCR_W;  // 16px — same width as scrollbar column
        ImVec2 bl{ px + pw - K_BORDER - K_SCR_W,
                   py + K_TITLE * K_TITLE_Y - BSZ * 0.5f };
        ImVec2 br{ bl.x + BSZ, bl.y + BSZ };
        bool chov = ImGui::IsMouseHoveringRect(bl, br, false);
        bool cprs = chov && ImGui::IsMouseDown(ImGuiMouseButton_Left);
        if (chov && ImGui::IsMouseClicked(ImGuiMouseButton_Left))
            *open = false;
        IDirect3DTexture9* ct = cprs ? s_closeP : chov ? s_closeF : s_closeN;
        if (ct)
            dl->AddImage(TI(ct), bl, br);
        else {
            ImVec2 xSz = ImGui::CalcTextSize("x");
            dl->AddText({ bl.x + BSZ*0.5f - xSz.x*0.5f, bl.y + BSZ*0.5f - xSz.y*0.5f },
                chov ? IM_COL32(255, 200, 100, 255) : IM_COL32(200, 160, 80, 200), "x");
        }
    }

    // Title-bar drag detection
    if (!s_drag.active) {
        const ImVec2 dragMin{ px + K_BORDER,              py          };
        const ImVec2 dragMax{ px + pw - K_BORDER - K_SCR_W, py + K_TITLE };
        if (ImGui::IsMouseHoveringRect(dragMin, dragMax, false)
            && ImGui::IsMouseClicked(ImGuiMouseButton_Left)) {
            s_drag.active = true;
            s_drag.wid    = fr.id;
            s_drag.offset = { ImGui::GetIO().MousePos.x - px,
                              ImGui::GetIO().MousePos.y - py };
        }
    }

    // Scrollbar geometry
    fr.bodyTop  = py + K_TITLE;
    fr.bodyBot  = py + ph - K_BOT;
    fr.scrX1    = px + pw - K_BORDER;       // left edge of right frame strip
    fr.scrX0    = fr.scrX1 - K_SCR_W;       // scrollbar sits just left of strip
    const float upB = fr.bodyTop + K_SCR_H;
    const float dnT = fr.bodyBot  - K_SCR_H;
    fr.trackTop = upB;
    fr.trackH   = dnT - upB;

    SROScrollState& sc = s_scroll[fr.id];

    const float bodyW = (sc.maxY > 2.f) ? -K_SCR_W : 0.f;

    // Content child window
    ImGui::SetNextWindowScroll(ImVec2(0.f, sc.y));
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0, 0, 0, 0));
    ImGui::BeginChild("##sro_body", ImVec2(bodyW, 0), false,
        ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleColor();

    return true;
}

void EndSROWindow()
{
    if (s_depth <= 0) return;
    SROFrame& fr = s_stack[--s_depth];

    SROScrollState& sc = s_scroll[fr.id];
    sc.maxY = ImGui::GetScrollMaxY();
    sc.y    = ImGui::GetScrollY();
    ImGui::EndChild();

    // Shared geometry used by both interaction and visuals.
    const float upT = fr.bodyTop,       upB = fr.bodyTop + K_SCR_H;
    const float dnB = fr.bodyBot,       dnT = fr.bodyBot - K_SCR_H;
    const bool scrollable = sc.maxY > 2.f;

    // Thumb position — only meaningful when scrollable.
    float thumbTop = fr.trackTop;
    if (scrollable && fr.trackH > K_SCR_H && sc.maxY > 0.f)
        thumbTop = fr.trackTop + (sc.y / sc.maxY) * (fr.trackH - K_SCR_H);
    thumbTop = (std::max)(fr.trackTop, (std::min)(fr.trackTop + fr.trackH - K_SCR_H, thumbTop));

    // Interaction — only when there is scrollable content.
    bool upH = false, upA = false, dnH = false, dnA = false, thH = false, thA = false;
    if (scrollable)
    {
        ImGui::PushClipRect({ fr.scrX0, fr.bodyTop },
                            { fr.scrX1, fr.bodyBot }, false);

        ImGui::SetCursorScreenPos({ fr.scrX0, upT });
        ImGui::InvisibleButton("##sro_up", { K_SCR_W, K_SCR_H });
        upH = ImGui::IsItemHovered(); upA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ fr.scrX0, dnT });
        ImGui::InvisibleButton("##sro_dn", { K_SCR_W, K_SCR_H });
        dnH = ImGui::IsItemHovered(); dnA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ fr.scrX0, thumbTop });
        ImGui::InvisibleButton("##sro_thumb", { K_SCR_W, K_SCR_H });
        thH = ImGui::IsItemHovered();

        if (ImGui::IsItemClicked()) {
            sc.draggingThumb    = true;
            sc.dragStartMouseY  = ImGui::GetIO().MousePos.y;
            sc.dragStartScrollY = sc.y;
        }
        if (!ImGui::IsMouseDown(ImGuiMouseButton_Left))
            sc.draggingThumb = false;
        thA = sc.draggingThumb;

        ImGui::SetCursorScreenPos({ fr.scrX0, fr.trackTop });
        ImGui::InvisibleButton("##sro_track", { K_SCR_W, fr.trackH });
        bool trackClicked = ImGui::IsItemClicked();

        ImGui::PopClipRect();

        if (upA) sc.y -= 2.f;
        if (dnA) sc.y += 2.f;
        if (trackClicked && !thA) {
            float frac = (ImGui::GetIO().MousePos.y - fr.trackTop) / fr.trackH;
            sc.y = frac * sc.maxY;
        }
        if (thA && fr.trackH > K_SCR_H) {
            float mouseDelta = ImGui::GetIO().MousePos.y - sc.dragStartMouseY;
            sc.y = sc.dragStartScrollY + mouseDelta * sc.maxY / (fr.trackH - K_SCR_H);
        }

        const float wheel = ImGui::GetIO().MouseWheel;
        if (wheel != 0.f && ImGui::IsWindowHovered(ImGuiHoveredFlags_ChildWindows |
                                                    ImGuiHoveredFlags_AllowWhenBlockedByActiveItem))
            sc.y -= wheel * ImGui::GetTextLineHeightWithSpacing() * 3.f;

        sc.y = (std::max)(0.f, (std::min)(sc.maxY, sc.y));
    }

    if (scrollable)
    {
        ImDrawList* sdl = ImGui::GetWindowDrawList();

        if (s_sBar)
            sdl->AddImage(TI(s_sBar),
                { fr.scrX0, fr.trackTop }, { fr.scrX1, dnT });
        else
            sdl->AddRectFilled({ fr.scrX0, fr.trackTop }, { fr.scrX1, dnT },
                IM_COL32(10, 9, 14, 220));

        if (s_arrowU)
            sdl->AddImage(TI(upA ? s_arrowUP : upH ? s_arrowUF : s_arrowU),
                { fr.scrX0, upT }, { fr.scrX1, upB });
        else
            sdl->AddRectFilled({ fr.scrX0, upT }, { fr.scrX1, upB },
                upA ? IM_COL32(120,100,50,255) : IM_COL32(60,50,30,200));

        if (s_arrowD)
            sdl->AddImage(TI(dnA ? s_arrowDP : dnH ? s_arrowDF : s_arrowD),
                { fr.scrX0, dnT }, { fr.scrX1, dnB });
        else
            sdl->AddRectFilled({ fr.scrX0, dnT }, { fr.scrX1, dnB },
                dnA ? IM_COL32(120,100,50,255) : IM_COL32(60,50,30,200));

        if (s_sBtn)
            sdl->AddImage(TI(thA ? s_sBtnP : thH ? s_sBtnF : s_sBtn),
                { fr.scrX0, thumbTop }, { fr.scrX1, thumbTop + K_SCR_H });
        else
            sdl->AddRectFilled({ fr.scrX0, thumbTop },
                               { fr.scrX1, thumbTop + K_SCR_H },
                thA ? IM_COL32(200,170,100,230)
                    : thH ? IM_COL32(170,145,85,200)
                          : IM_COL32(130,110,65,180));
    }

    ImGui::End();
}

bool SROCombo(const char* id, int* selectedIdx, const char* const* items, int itemCount,
              float width, float height)
{
    EnsureLoaded();
    static constexpr float CB_ARROW_W = 18.f;
    const float boxW = width - CB_ARROW_W;

    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

    // Box background
    if (s_comboBox)
        dl->AddImage(TI(s_comboBox), pos, { pos.x + boxW, pos.y + height });
    else
        dl->AddRectFilled(pos, { pos.x + boxW, pos.y + height }, IM_COL32(10, 10, 16, 220));

    // Selected text centered in box
    if (*selectedIdx >= 0 && *selectedIdx < itemCount) {
        ImVec2 tsz = ImGui::CalcTextSize(items[*selectedIdx]);
        dl->AddText({ pos.x + boxW * 0.5f - tsz.x * 0.5f,
                      pos.y + height * 0.5f - tsz.y * 0.5f },
            IM_COL32(255, 255, 255, 255), items[*selectedIdx]);
    }

    // Arrow button
    const ImVec2 ab0{ pos.x + boxW, pos.y };
    const ImVec2 ab1{ pos.x + width, pos.y + height };
    const bool arrowHov = ImGui::IsMouseHoveringRect(ab0, ab1, false);
    const bool arrowPrs = arrowHov && ImGui::IsMouseDown(ImGuiMouseButton_Left);
    IDirect3DTexture9* arrTex = arrowPrs ? s_cbArrowP : arrowHov ? s_cbArrowF : s_cbArrowN;
    if (arrTex)
        dl->AddImageQuad(TI(arrTex),
            ab0,               { ab1.x, ab0.y },
            ab1,               { ab0.x, ab1.y },
            { 0,1 }, { 0,0 }, { 1,0 }, { 1,1 });
    else
        dl->AddRectFilled(ab0, ab1,
            arrowPrs ? IM_COL32(60, 90, 160, 255) : IM_COL32(40, 60, 110, 200));

    // Invisible button over full combo area handles click detection
    ImGui::InvisibleButton(id, { width, height });
    if (ImGui::IsItemClicked())
        ImGui::OpenPopup(id);

    // Position popup below the combo box on the frame it opens
    bool changed = false;
    if (ImGui::IsPopupOpen(id)) {
        ImGui::SetNextWindowPos({ pos.x, pos.y + height }, ImGuiCond_Always);
        ImGui::SetNextWindowSize({ width, 0 },              ImGuiCond_Always);
    }
    ImGui::PushStyleColor(ImGuiCol_PopupBg,       ImVec4(0.05f, 0.05f, 0.08f, 0.96f));
    ImGui::PushStyleColor(ImGuiCol_HeaderHovered, ImVec4(0.18f, 0.35f, 0.70f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_HeaderActive,  ImVec4(0.13f, 0.27f, 0.55f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_Header,        ImVec4(0.10f, 0.20f, 0.50f, 0.8f));
    ImGui::PushStyleColor(ImGuiCol_Text,          ImVec4(1.0f,  1.0f,  1.0f,  1.0f));
    if (ImGui::BeginPopup(id)) {
        for (int i = 0; i < itemCount; ++i) {
            if (ImGui::Selectable(items[i], i == *selectedIdx)) {
                *selectedIdx = i;
                changed = true;
            }
        }
        ImGui::EndPopup();
    }
    ImGui::PopStyleColor(5);
    return changed;
}

bool SROButton(const char* id, const char* label, float width, float height, bool disabled)
{
    EnsureLoaded();
    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

    if (disabled) {
        ImGui::Dummy({ width, height });
        if (s_btnDis)
            dl->AddImage(TI(s_btnDis), pos, { pos.x + width, pos.y + height });
        else
            dl->AddRectFilled(pos, { pos.x + width, pos.y + height }, IM_COL32(30, 30, 30, 180));
        const ImVec2 tsz = ImGui::CalcTextSize(label);
        dl->AddText({ pos.x + width * 0.5f - tsz.x * 0.5f,
                      pos.y + height * 0.5f - tsz.y * 0.5f },
            IM_COL32(130, 110, 70, 160), label);
        return false;
    }

    ImGui::InvisibleButton(id, { width, height });
    const bool hov     = ImGui::IsItemHovered();
    const bool prs     = ImGui::IsItemActive();
    const bool clicked = ImGui::IsItemClicked();

    IDirect3DTexture9* btex = prs ? s_btnP : hov ? s_btnF : s_btnN;
    if (btex)
        dl->AddImage(TI(btex), pos, { pos.x + width, pos.y + height });
    else {
        const ImVec4 c = prs  ? ImVec4(0.50f, 0.35f, 0.05f, 1.f)
                        : hov ? ImVec4(0.65f, 0.47f, 0.08f, 1.f)
                              : ImVec4(0.45f, 0.32f, 0.05f, 1.f);
        dl->AddRectFilled(pos, { pos.x + width, pos.y + height },
            ImGui::ColorConvertFloat4ToU32(c));
    }
    const ImVec2 tsz = ImGui::CalcTextSize(label);
    dl->AddText({ pos.x + width * 0.5f - tsz.x * 0.5f,
                  pos.y + height * 0.5f - tsz.y * 0.5f },
        IM_COL32(255, 220, 150, 255), label);
    if (clicked) SROSkin_PlayClick();
    return clicked;
}

// SROTabBar
bool SROTabBar(const char* id, const char* const* labels, int count,
               int* selected, float tabW, bool longTab)
{
    EnsureLoaded();
    const float tabH = longTab ? K_TAB_H : K_TABS_H;
    bool changed = false;

    ImGui::PushID(id);
    for (int i = 0; i < count; ++i) {
        if (i > 0) ImGui::SameLine(0.f, 0.f);
        const ImVec2 pos = ImGui::GetCursorScreenPos();
        ImGui::InvisibleButton(labels[i], { tabW, tabH });
        if (ImGui::IsItemClicked() && i != *selected) {
            *selected = i;
            changed   = true;
            SROSkin_PlayClick();
        }
        const bool            on  = (i == *selected);
        IDirect3DTexture9*    tex = on ? (longTab ? s_tabLOn  : s_tabSOn)
                                       : (longTab ? s_tabLOff : s_tabSOff);
        ImDrawList* dl = ImGui::GetWindowDrawList();
        if (tex)
            dl->AddImage(TI(tex), pos, { pos.x + tabW, pos.y + tabH });
        else
            dl->AddRectFilled(pos, { pos.x + tabW, pos.y + tabH },
                on ? IM_COL32(90, 70, 20, 240) : IM_COL32(35, 30, 12, 200));

        const ImVec2 tsz = ImGui::CalcTextSize(labels[i]);
        dl->AddText(
            { pos.x + tabW * 0.5f - tsz.x * 0.5f,
              pos.y + tabH * 0.5f - tsz.y * 0.5f },
            on ? IM_COL32(255, 220, 150, 255) : IM_COL32(180, 170, 145, 255),
            labels[i]);
    }
    ImGui::PopID();
    return changed;
}

// SROBeginSubFrame / SROEndSubFrame

struct SROSubState { ImVec2 tl; float w, h; };
static SROSubState s_sub;

bool SROBeginSubFrame(const char* id, float w, float h)
{
    EnsureLoaded();
    const ImVec2 tl = ImGui::GetCursorScreenPos();
    s_sub = { tl, w, h };

    // Background fills behind the child window content.
    ImDrawList* dl = ImGui::GetWindowDrawList();
    dl->AddRectFilled(tl, { tl.x + w, tl.y + h }, IM_COL32(10, 10, 18, 235));

    const float inset = 6.f;
    ImGui::SetCursorScreenPos({ tl.x + inset, tl.y });
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.f, 0.f, 0.f, 0.f));
    const bool r = ImGui::BeginChild(id, { w - inset * 2.f, h }, ImGuiChildFlags_None,
                                     ImGuiWindowFlags_NoScrollbar |
                                     ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleColor();
    return r;
}

void SROEndSubFrame()
{
    ImGui::EndChild();

    // 8-piece border drawn on top of the content (parent draw list, after EndChild).
    ImDrawList*   dl = ImGui::GetWindowDrawList();
    const ImVec2& tl = s_sub.tl;
    const float   w  = s_sub.w, h = s_sub.h, c = K_SF_C;

    if (s_sfTL) dl->AddImage(TI(s_sfTL), tl,                          { tl.x + c,     tl.y + c     });
    if (s_sfTC) dl->AddImage(TI(s_sfTC), { tl.x + c,     tl.y     },  { tl.x + w - c, tl.y + c     });
    if (s_sfTR) dl->AddImage(TI(s_sfTR), { tl.x + w - c, tl.y     },  { tl.x + w,     tl.y + c     });
    if (s_sfML) dl->AddImage(TI(s_sfML), { tl.x,         tl.y + c },  { tl.x + c,     tl.y + h - c });
    if (s_sfMR) dl->AddImage(TI(s_sfMR), { tl.x + w - c, tl.y + c },  { tl.x + w,     tl.y + h - c });
    if (s_sfBL) dl->AddImage(TI(s_sfBL), { tl.x,         tl.y + h - c }, { tl.x + c,     tl.y + h });
    if (s_sfBC) dl->AddImage(TI(s_sfBC), { tl.x + c,     tl.y + h - c }, { tl.x + w - c, tl.y + h });
    if (s_sfBR) dl->AddImage(TI(s_sfBR), { tl.x + w - c, tl.y + h - c }, { tl.x + w,     tl.y + h });
}

//VK short label
static std::string VkToLabel(int vk)
{
    if (vk >= VK_F1 && vk <= VK_F12)
        return "F" + std::to_string(vk - VK_F1 + 1);
    switch (vk) {
    case VK_INSERT:   return "INS";   case VK_DELETE:   return "DEL";
    case VK_HOME:     return "HOME";  case VK_END:      return "END";
    case VK_PRIOR:    return "PGUP";  case VK_NEXT:     return "PGDN";
    case VK_SHIFT:    return "SHFT";  case VK_CONTROL:  return "CTRL";
    case VK_MENU:     return "ALT";   case VK_ESCAPE:   return "ESC";
    case VK_SPACE:    return "SPC";   case VK_RETURN:   return "ENT";
    case VK_TAB:      return "TAB";   case VK_BACK:     return "BKSP";
    case VK_LSHIFT:   return "LSHFT"; case VK_RSHIFT:   return "RSHFT";
    case VK_LCONTROL: return "LCTRL"; case VK_RCONTROL: return "RCTRL";
    case VK_LMENU:    return "LALT";  case VK_RMENU:    return "RALT";
    case VK_LWIN:     return "LWIN";  case VK_RWIN:     return "RWIN";
    case VK_LBUTTON:  return "M1";    case VK_RBUTTON:  return "M2";
    case VK_MBUTTON:  return "M3";    case VK_XBUTTON1: return "M4";
    case VK_XBUTTON2: return "M5";
    case VK_OEM_MINUS: return "-";    case VK_OEM_PLUS:  return "=";
    }
    if ((vk >= 'A' && vk <= 'Z') || (vk >= '0' && vk <= '9'))
        return std::string(1, static_cast<char>(vk));
    char buf[8];
    snprintf(buf, sizeof(buf), "?%02X", vk);
    return buf;
}

// SROKeybind
static ImGuiID s_kbListening = 0;

bool SROSkin_IsCapturingKey() { return s_kbListening != 0; }

bool SROKeybind(const char* id, const char* label, int* vkCode)
{
    EnsureLoaded();

    // Natural DDJ dimension
    static constexpr float KB_W  = 156.f;  // opt_key.ddj natural width
    static constexpr float KB_H  = 28.f;   // opt_key.ddj natural height
    static constexpr float KB_BX = 4.f;    // key box x offset in natural coords
    static constexpr float KB_BY = 6.f;    // key box y offset in natural coords
    static constexpr float KB_BW = 56.f;   // key box width  (= opt_key_select width)
    static constexpr float KB_BH = 16.f;   // key box height (= opt_key_select height)

    const float   availW = ImGui::GetContentRegionAvail().x;
    const float   barW   = (availW > 8.f) ? availW : KB_W;
    const float   scaleX = barW / KB_W;
    const ImVec2  pos    = ImGui::GetCursorScreenPos();
    ImDrawList*   dl     = ImGui::GetWindowDrawList();

    // Background bar
    if (s_optKey)
        dl->AddImage(TI(s_optKey), pos, { pos.x + barW, pos.y + KB_H });
    else
        dl->AddRectFilled(pos, { pos.x + barW, pos.y + KB_H }, IM_COL32(20, 22, 30, 220));

    // Key box screen coords
    const float bx0 = pos.x + KB_BX * scaleX;
    const float by0 = pos.y + KB_BY;
    const float bx1 = bx0   + KB_BW * scaleX;
    const float by1 = by0   + KB_BH;

    const ImGuiID wid       = ImGui::GetID(id);
    const bool    listening = (s_kbListening == wid);

    // Select indicator overlay when listening
    if (listening && s_optKeySel)
        dl->AddImage(TI(s_optKeySel), { bx0, by0 }, { bx1, by1 });

    // Key label centered in key box
    const std::string keyStr = VkToLabel(*vkCode);
    const ImVec2      keySz  = ImGui::CalcTextSize(keyStr.c_str());
    dl->AddText(
        { bx0 + (bx1 - bx0 - keySz.x) * 0.5f,
          by0 + (KB_BH - keySz.y) * 0.5f },
        listening ? IM_COL32(255, 230, 100, 255) : IM_COL32(240, 240, 240, 255),
        keyStr.c_str());

    // Action label right of key box, vertically centered in bar
    dl->AddText(
        { bx1 + 10.f, pos.y + (KB_H - ImGui::GetTextLineHeight()) * 0.5f },
        IM_COL32(220, 200, 160, 255), label);

    // InvisibleButton
    ImGui::SetCursorScreenPos(pos);
    ImGui::InvisibleButton(id, { barW, KB_H });

    // activate listening when the key box was clicked
    const ImVec2 mp         = ImGui::GetMousePos();
    const bool   boxHovered = ImGui::IsItemHovered() &&
                              mp.x >= bx0 && mp.x <= bx1 &&
                              mp.y >= by0 && mp.y <= by1;
    const bool   boxClicked = boxHovered && ImGui::IsItemClicked();

    static bool s_prevKeyState[256] = {};
    if (boxClicked) {
        s_kbListening = wid;
        for (int vk = 0; vk < 256; vk++)
            s_prevKeyState[vk] = (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    // Click outside the key box cancels listening
    if (listening && ImGui::IsMouseClicked(ImGuiMouseButton_Left) && !boxClicked)
        s_kbListening = 0;

    bool changed = false;
    // Skip scanning on the activation frame; detect rising edges only
    if (s_kbListening == wid && !boxClicked) {
        for (int vk = 1; vk < 256; vk++) {
            const bool nowDown  = (GetAsyncKeyState(vk) & 0x8000) != 0;
            const bool newPress = nowDown && !s_prevKeyState[vk];
            s_prevKeyState[vk]  = nowDown;
            if (!newPress) continue;
            if (vk == VK_ESCAPE) {
                s_kbListening = 0;
            } else {
                *vkCode       = vk;
                s_kbListening = 0;
                changed       = true;
            }
            break;
        }
    }

    return changed;
}

// SROCheckbox
bool SROCheckbox(const char* id, const char* label, bool* value, bool disabled)
{
    EnsureLoaded();

    static constexpr float CB_SZ = 16.f;

    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

    IDirect3DTexture9* tex = disabled ? s_cbDis : (*value ? s_cbOn : s_cbOff);

    if (tex)
        dl->AddImage(TI(tex), pos, { pos.x + CB_SZ, pos.y + CB_SZ });
    else {
        dl->AddRectFilled(pos, { pos.x + CB_SZ, pos.y + CB_SZ }, IM_COL32(20, 20, 20, 220));
        if (*value)
            dl->AddLine({ pos.x + 3.f, pos.y + 8.f }, { pos.x + 7.f,  pos.y + 13.f }, IM_COL32(200, 180, 80, 255), 2.f);
        if (*value)
            dl->AddLine({ pos.x + 7.f, pos.y + 13.f }, { pos.x + 13.f, pos.y + 4.f }, IM_COL32(200, 180, 80, 255), 2.f);
    }

    bool clicked = false;
    ImGui::SetCursorScreenPos(pos);
    if (!disabled) {
        ImGui::InvisibleButton(id, { CB_SZ, CB_SZ });
        if (ImGui::IsItemClicked()) {
            *value  = !*value;
            clicked = true;
        }
    } else {
        ImGui::Dummy({ CB_SZ, CB_SZ });
    }

    // Label right of checkbox, vertically centered
    ImGui::SameLine();
    ImGui::SetCursorScreenPos(
        { ImGui::GetCursorScreenPos().x,
          pos.y + (CB_SZ - ImGui::GetTextLineHeight()) * 0.5f });
    if (disabled)
        ImGui::TextDisabled("%s", label);
    else
        ImGui::TextUnformatted(label);

    return clicked;
}

// Bar-01 draw helper

static constexpr float B01_CAP = 9.f;
static constexpr float B01_H   = 22.f;
static constexpr float SR_H        = 22.f;
static constexpr float SR_LBL_FRAC = 0.38f;

static void DrawBar01(ImDrawList* dl, ImVec2 tl, float w, float h, bool selected)
{
    const float cap = B01_CAP;
    const float mw  = (w - cap * 2.f > 0.f) ? w - cap * 2.f : 0.f;
    if (s_bar01L) dl->AddImage(TI(s_bar01L), tl,                              { tl.x + cap,      tl.y + h });
    if (s_bar01M) dl->AddImage(TI(s_bar01M), { tl.x + cap, tl.y },            { tl.x + cap + mw, tl.y + h });
    if (s_bar01R) dl->AddImage(TI(s_bar01R), { tl.x + cap + mw, tl.y },       { tl.x + w,        tl.y + h });
    if (!s_bar01L)
        dl->AddRectFilled(tl, { tl.x + w, tl.y + h }, IM_COL32(15, 18, 28, 220));
    if (selected) {
        if (s_bar01selL) dl->AddImage(TI(s_bar01selL), tl,                              { tl.x + cap,      tl.y + h });
        if (s_bar01selM) dl->AddImage(TI(s_bar01selM), { tl.x + cap, tl.y },            { tl.x + cap + mw, tl.y + h });
        if (s_bar01selR) dl->AddImage(TI(s_bar01selR), { tl.x + cap + mw, tl.y },       { tl.x + w,        tl.y + h });
        if (!s_bar01selL)
            dl->AddRectFilled(tl, { tl.x + w, tl.y + h }, IM_COL32(80, 100, 160, 120));
    }
}

// SROInputBar
bool SROInputBar(const char* id, char* buf, size_t bufSize, float width, ImGuiInputTextFlags flags, bool center)
{
    EnsureLoaded();
    const float  w   = (width > 0.f) ? width : ImGui::GetContentRegionAvail().x;
    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

    DrawBar01(dl, pos, w, B01_H, false);

    const float padX   = B01_CAP + 2.f;
    const float innerW = w - padX * 2.f;
    const float textY  = pos.y + (B01_H - ImGui::GetTextLineHeight()) * 0.5f;

    float startX = pos.x + padX;
    float inputW = innerW;
    if (center && buf[0]) {
        const float textW  = ImGui::CalcTextSize(buf).x;
        const float offset = (std::max)(0.f, (innerW - textW) * 0.5f);
        startX += offset;
        inputW  = (pos.x + w - padX) - startX;
    }

    ImGui::SetCursorScreenPos({ startX, textY });
    ImGui::SetNextItemWidth(inputW);

    ImGui::PushStyleColor(ImGuiCol_FrameBg,        ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_FrameBgHovered, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_FrameBgActive,  ImVec4(0, 0, 0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0, 0));
    const bool changed = ImGui::InputText(id, buf, bufSize, flags);
    ImGui::PopStyleVar();
    ImGui::PopStyleColor(3);

    if (ImGui::GetCursorScreenPos().y < pos.y + B01_H)
        ImGui::SetCursorScreenPos({ pos.x, pos.y + B01_H });

    return changed;
}

// SROInputBarInt

bool SROInputBarInt(const char* id, int* value, float width, ImGuiInputTextFlags flags, bool center)
{
    EnsureLoaded();
    const float  w   = (width > 0.f) ? width : ImGui::GetContentRegionAvail().x;
    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

    DrawBar01(dl, pos, w, B01_H, false);

    const float padX   = B01_CAP + 2.f;
    const float innerW = w - padX * 2.f;
    const float textY  = pos.y + (B01_H - ImGui::GetTextLineHeight()) * 0.5f;

    float startX = pos.x + padX;
    float inputW = innerW;
    if (center) {
        char tmp[32]; snprintf(tmp, sizeof(tmp), "%d", *value);
        const float textW  = ImGui::CalcTextSize(tmp).x;
        const float offset = (std::max)(0.f, (innerW - textW) * 0.5f);
        startX += offset;
        inputW  = (pos.x + w - padX) - startX;
    }

    ImGui::SetCursorScreenPos({ startX, textY });
    ImGui::SetNextItemWidth(inputW);

    ImGui::PushStyleColor(ImGuiCol_FrameBg,        ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_FrameBgHovered, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_FrameBgActive,  ImVec4(0, 0, 0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(0, 0));
    const bool changed = ImGui::InputScalar(id, ImGuiDataType_S32, value, nullptr, nullptr, "%d", flags);
    ImGui::PopStyleVar();
    ImGui::PopStyleColor(3);

    if (ImGui::GetCursorScreenPos().y < pos.y + B01_H)
        ImGui::SetCursorScreenPos({ pos.x, pos.y + B01_H });

    return changed;
}

// SROListItem

bool SROListItem(const char* id, const char* text, bool selected, float width, float height)
{
    EnsureLoaded();
    const float  w     = (width  > 0.f) ? width  : ImGui::GetContentRegionAvail().x;
    const float  itemH = (height > 0.f) ? height : B01_H;
    const ImVec2 pos   = ImGui::GetCursorScreenPos();
    ImDrawList*  dl    = ImGui::GetWindowDrawList();

    DrawBar01(dl, pos, w, itemH, selected);

    const float lineH = ImGui::GetTextLineHeight();
    const float textY = pos.y + (itemH - lineH) * 0.5f;
    dl->PushClipRect({ pos.x + B01_CAP, pos.y }, { pos.x + w - B01_CAP, pos.y + itemH }, true);
    dl->AddText({ pos.x + B01_CAP + 4.f, textY },
        selected ? IM_COL32(255, 222, 100, 255) : IM_COL32(220, 210, 185, 255), text);
    dl->PopClipRect();

    ImGui::SetCursorScreenPos(pos);
    ImGui::InvisibleButton(id, { w, itemH });
    const bool liClicked = ImGui::IsItemClicked();
    if (liClicked) SROSkin_PlayClick();
    return liClicked;
}

// SROStatusRow

void SROStatusRow(const char* label, const char* value, float width, ImU32 valueColor)
{
    EnsureLoaded();
    const float  w      = (width > 0.f) ? width : ImGui::GetContentRegionAvail().x;
    const ImVec2 pos    = ImGui::GetCursorScreenPos();
    ImDrawList*  dl     = ImGui::GetWindowDrawList();
    const float  splitX = pos.x + w * SR_LBL_FRAC;

    if (s_strSlot)
        dl->AddImage(TI(s_strSlot), pos, { pos.x + w, pos.y + SR_H });
    else {
        dl->AddRectFilled(pos,               { splitX, pos.y + SR_H }, IM_COL32(55, 48, 32, 220));
        dl->AddRectFilled({ splitX, pos.y }, { pos.x + w, pos.y + SR_H }, IM_COL32(12, 14, 20, 220));
    }

    const float lineH = ImGui::GetTextLineHeight();
    const float textY = pos.y + (SR_H - lineH) * 0.5f;

    // Label — 3 extra px left padding vs value side
    dl->PushClipRect({ pos.x + 7.f, pos.y }, { splitX - 2.f, pos.y + SR_H }, true);
    dl->AddText({ pos.x + 7.f, textY }, IM_COL32(210, 185, 130, 255), label);
    dl->PopClipRect();

    // Value — left-aligned with consistent 8 px inset
    dl->PushClipRect({ splitX + 2.f, pos.y }, { pos.x + w - 4.f, pos.y + SR_H }, true);
    dl->AddText({ splitX + 8.f, textY }, valueColor, value);
    dl->PopClipRect();

    ImGui::Dummy({ w, SR_H });
}

// SROActionFrame

struct SROAFState { ImVec2 tl; float w, h; size_t scrollKey; };
static SROAFState s_af;

bool SROBeginActionFrame(const char* id, float w, float h)
{
    EnsureLoaded();
    const ImVec2    tl        = ImGui::GetCursorScreenPos();
    const size_t    scrollKey = std::hash<std::string>{}(id);
    SROScrollState& sc        = s_afScroll[scrollKey];
    s_af = { tl, w, h, scrollKey };

    // Tiled background (com_bg_tile_a.ddj, 128×128)
    ImDrawList* dl = ImGui::GetWindowDrawList();
    if (s_bgTileA) {
        for (float ty = tl.y; ty < tl.y + h; ty += 128.f)
            for (float tx = tl.x; tx < tl.x + w; tx += 128.f) {
                const float tw = (std::min)(tx + 128.f, tl.x + w) - tx;
                const float th = (std::min)(ty + 128.f, tl.y + h) - ty;
                dl->AddImage(TI(s_bgTileA), { tx, ty }, { tx + tw, ty + th },
                             { 0.f, 0.f }, { tw / 128.f, th / 128.f });
            }
    } else {
        dl->AddRectFilled(tl, { tl.x + w, tl.y + h }, IM_COL32(20, 22, 30, 240));
    }

    const float c      = K_AF_C;
    const float childW = w - c * 2.f - K_SCR_W;

    ImGui::SetNextWindowScroll({ 0.f, sc.y });
    ImGui::SetCursorScreenPos({ tl.x + c, tl.y });
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.f, 0.f, 0.f, 0.f));
    const bool r = ImGui::BeginChild(id, { childW, h }, ImGuiChildFlags_None,
                                     ImGuiWindowFlags_NoScrollbar |
                                     ImGuiWindowFlags_NoScrollWithMouse);
    ImGui::PopStyleColor();
    // Clip content to the inner safe zone so items never bleed behind the border strips
    if (r)
        ImGui::PushClipRect({ tl.x + c, tl.y + c },
                            { tl.x + c + childW, tl.y + h - c }, true);
    return r;
}

void SROEndActionFrame()
{
    SROScrollState& sc = s_afScroll[s_af.scrollKey];
    sc.maxY = ImGui::GetScrollMaxY();
    sc.y    = ImGui::GetScrollY();
    ImGui::PopClipRect();
    ImGui::EndChild();

    const ImVec2& tl = s_af.tl;
    const float w = s_af.w, h = s_af.h, c = K_AF_C;
    const bool scrollable = sc.maxY > 2.f;

    // Scrollbar geometry
    const float scrX0    = tl.x + w - c - K_SCR_W;
    const float scrX1    = tl.x + w - c;
    const float upT      = tl.y + c;               // below top border strip
    const float upB      = upT + K_SCR_H;
    const float dnB      = tl.y + h - c;           // above bottom border strip
    const float dnT      = dnB - K_SCR_H;
    const float trackTop = upB;
    const float trackH   = dnT - upB;

    float thumbTop = trackTop;
    if (scrollable && trackH > K_SCR_H && sc.maxY > 0.f)
        thumbTop = trackTop + (sc.y / sc.maxY) * (trackH - K_SCR_H);
    thumbTop = (std::max)(trackTop, (std::min)(trackTop + trackH - K_SCR_H, thumbTop));

    // Scrollbar interaction
    bool upH = false, upA = false, dnH = false, dnA = false, thH = false, thA = false;
    if (scrollable)
    {
        ImGui::PushID((int)s_af.scrollKey);  // prevent ID collision when multiple frames share a parent
        ImGui::PushClipRect({ scrX0, tl.y }, { scrX1, tl.y + h }, false);

        ImGui::SetCursorScreenPos({ scrX0, upT });
        ImGui::InvisibleButton("##af_up", { K_SCR_W, K_SCR_H });
        upH = ImGui::IsItemHovered(); upA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ scrX0, dnT });
        ImGui::InvisibleButton("##af_dn", { K_SCR_W, K_SCR_H });
        dnH = ImGui::IsItemHovered(); dnA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ scrX0, thumbTop });
        ImGui::InvisibleButton("##af_thumb", { K_SCR_W, K_SCR_H });
        thH = ImGui::IsItemHovered();
        if (ImGui::IsItemClicked()) {
            sc.draggingThumb    = true;
            sc.dragStartMouseY  = ImGui::GetIO().MousePos.y;
            sc.dragStartScrollY = sc.y;
        }
        if (!ImGui::IsMouseDown(ImGuiMouseButton_Left)) sc.draggingThumb = false;
        thA = sc.draggingThumb;

        ImGui::SetCursorScreenPos({ scrX0, trackTop });
        ImGui::InvisibleButton("##af_track", { K_SCR_W, trackH });
        const bool trackClicked = ImGui::IsItemClicked();

        ImGui::PopClipRect();
        ImGui::PopID();

        if (upA) sc.y -= 2.f;
        if (dnA) sc.y += 2.f;
        if (trackClicked && !thA) {
            const float frac = (ImGui::GetIO().MousePos.y - trackTop) / trackH;
            sc.y = frac * sc.maxY;
        }
        if (thA && trackH > K_SCR_H) {
            const float delta = ImGui::GetIO().MousePos.y - sc.dragStartMouseY;
            sc.y = sc.dragStartScrollY + delta * sc.maxY / (trackH - K_SCR_H);
        }

        const float wheel = ImGui::GetIO().MouseWheel;
        if (wheel != 0.f && ImGui::IsMouseHoveringRect(
                { tl.x, tl.y }, { tl.x + w, tl.y + h }, false))
            sc.y -= wheel * ImGui::GetTextLineHeightWithSpacing() * 3.f;

        sc.y = (std::max)(0.f, (std::min)(sc.maxY, sc.y));
    }

    ImDrawList* dl = ImGui::GetWindowDrawList();

    // Scrollbar visuals
    if (scrollable)
    {
        if (s_sBar)
            dl->AddImage(TI(s_sBar), { scrX0, trackTop }, { scrX1, dnT });
        else
            dl->AddRectFilled({ scrX0, trackTop }, { scrX1, dnT }, IM_COL32(10, 9, 14, 220));

        if (s_arrowU)
            dl->AddImage(TI(upA ? s_arrowUP : upH ? s_arrowUF : s_arrowU),
                         { scrX0, upT }, { scrX1, upB });
        else
            dl->AddRectFilled({ scrX0, upT }, { scrX1, upB },
                upA ? IM_COL32(120, 100, 50, 255) : IM_COL32(60, 50, 30, 200));

        if (s_arrowD)
            dl->AddImage(TI(dnA ? s_arrowDP : dnH ? s_arrowDF : s_arrowD),
                         { scrX0, dnT }, { scrX1, dnB });
        else
            dl->AddRectFilled({ scrX0, dnT }, { scrX1, dnB },
                dnA ? IM_COL32(120, 100, 50, 255) : IM_COL32(60, 50, 30, 200));

        if (s_sBtn)
            dl->AddImage(TI(thA ? s_sBtnP : thH ? s_sBtnF : s_sBtn),
                         { scrX0, thumbTop }, { scrX1, thumbTop + K_SCR_H });
        else
            dl->AddRectFilled({ scrX0, thumbTop }, { scrX1, thumbTop + K_SCR_H },
                thA ? IM_COL32(180, 150, 60, 255) : IM_COL32(100, 80, 30, 220));
    }

    // 8-piece border on top
    if (s_afTL) dl->AddImage(TI(s_afTL), tl,                               { tl.x + c,     tl.y + c     });
    if (s_afTC) dl->AddImage(TI(s_afTC), { tl.x + c,     tl.y      },      { tl.x + w - c, tl.y + c     });
    if (s_afTR) dl->AddImage(TI(s_afTR), { tl.x + w - c, tl.y      },      { tl.x + w,     tl.y + c     });
    if (s_afML) dl->AddImage(TI(s_afML), { tl.x,         tl.y + c  },      { tl.x + c,     tl.y + h - c });
    if (s_afMR) dl->AddImage(TI(s_afMR), { tl.x + w - c, tl.y + c  },      { tl.x + w,     tl.y + h - c });
    if (s_afBL) dl->AddImage(TI(s_afBL), { tl.x,         tl.y + h - c },   { tl.x + c,     tl.y + h     });
    if (s_afBC) dl->AddImage(TI(s_afBC), { tl.x + c,     tl.y + h - c },   { tl.x + w - c, tl.y + h     });
    if (s_afBR) dl->AddImage(TI(s_afBR), { tl.x + w - c, tl.y + h - c },   { tl.x + w,     tl.y + h     });

    
    ImGui::SetCursorScreenPos({ tl.x, tl.y + h });
    ImGui::Dummy({ w, 0.f });
}
