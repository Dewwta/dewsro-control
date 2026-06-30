#include "SROSkinWindow.h"
#include <algorithm>
#include <unordered_map>
#include <unordered_set>
#include <string>
#include <vector>
#include <dsound.h>
#pragma comment(lib, "dsound.lib")

// ── Natural DDJ pixel dimensions (confirmed via binary analysis) ──────────
static constexpr float K_BORDER  = 16.f;   // left/right border strip width
static constexpr float K_TITLE   = 40.f;   // top frame strip height
static constexpr float K_BOT     = 16.f;   // bottom frame strip height
static constexpr float K_TILE    = 128.f;  // bg tile natural size
static constexpr float K_SCR_W   = 16.f;   // scrollbar element width
static constexpr float K_SCR_H   = 16.f;   // scrollbar element height
// Title text center as fraction of K_TITLE.
// 0.35 places the center at row ~12.6 of the 36px mid_up — just into the gold crown band.
static constexpr float K_TITLE_Y = 0.35f;

// ── Cached textures (loaded once after SROSkin_Init) ──────────────────────
static IconCache*         s_cache  = nullptr;
static bool               s_ready  = false;
static IDirect3DTexture9* s_TL = nullptr, *s_TC = nullptr, *s_TR = nullptr;
static IDirect3DTexture9* s_ML = nullptr,                  *s_MR = nullptr;
static IDirect3DTexture9* s_BL = nullptr, *s_BC = nullptr, *s_BR = nullptr;
static IDirect3DTexture9* s_bg    = nullptr;
static IDirect3DTexture9* s_sBtn  = nullptr, *s_sBtnF = nullptr, *s_sBtnP = nullptr;
static IDirect3DTexture9* s_sBar  = nullptr;
static IDirect3DTexture9* s_closeN = nullptr, *s_closeF = nullptr, *s_closeP = nullptr;
// Correct scrollbar elements (confirmed from in-game party match window):
//   s_sBtn/F/P = com_scroll_button  → scrubber/thumb (the moving knob)
//   s_sBar                          → track background
//   s_arrowU/D                      → actual up/down arrow buttons (chattingwnd)
static IDirect3DTexture9* s_arrowU = nullptr, *s_arrowUF = nullptr, *s_arrowUP = nullptr;
static IDirect3DTexture9* s_arrowD = nullptr, *s_arrowDF = nullptr, *s_arrowDP = nullptr;
// ── SROCombo + SROButton textures ─────────────────────────────────────────
static IDirect3DTexture9* s_comboBox  = nullptr;
static IDirect3DTexture9* s_cbArrowN  = nullptr, *s_cbArrowF  = nullptr, *s_cbArrowP  = nullptr;
static IDirect3DTexture9* s_btnN      = nullptr, *s_btnF      = nullptr, *s_btnP      = nullptr;

// ── DirectSound playback for open / close sounds ──────────────────────────
// Using DS instead of WinMM/PlaySound so SRC quality matches the game engine.
static IDirectSound8*      s_ds       = nullptr;
static IDirectSoundBuffer* s_bufOpen  = nullptr;
static IDirectSoundBuffer* s_bufClose = nullptr;

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

// ── Per-frame window visibility tracking (for open / close sound) ─────────
// s_visThisFrame is built as windows call BeginSROWindow each frame.
// SROSkin_NewFrame() (called at start of each frame) swaps it into s_visLastFrame.
static std::unordered_set<size_t> s_visThisFrame, s_visLastFrame;

// ── Per-window scroll state, keyed by hash of the window id string ───────
struct SROScrollState {
    float y = 0.f, maxY = 0.f;
    float dragStartMouseY = 0.f, dragStartScrollY = 0.f;
};
static std::unordered_map<size_t, SROScrollState> s_scroll;

// ── Window frame stack (supports up to 4 nested SRO windows) ─────────────
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

// ── Custom title-bar drag state ───────────────────────────────────────────
// We use ImGuiWindowFlags_NoMove so ImGui never sets itself as MovingWindow
// on a title-bar click — that was causing the "latch to cursor on re-open" bug.
// Instead we implement dragging here with SetNextWindowPos.
struct SRODragState { size_t wid = 0; ImVec2 offset = {}; bool active = false; };
static SRODragState s_drag;

// ─────────────────────────────────────────────────────────────────────────

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
    s_btnN = C("interface\\ifcommon\\com_button.ddj");
    s_btnF = C("interface\\ifcommon\\com_button_focus.ddj");
    s_btnP = C("interface\\ifcommon\\com_button_press.ddj");
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
    // DSSCL_NORMAL: don't claim exclusive control — the game's own DS device stays intact.
    s_ds->SetCooperativeLevel(hwnd, DSSCL_NORMAL);

    std::vector<uint8_t> wavOpen, wavClose;
    dataPk2->ReadFile("prim\\snd\\ui\\uiwinopen.wav",  wavOpen);
    dataPk2->ReadFile("prim\\snd\\ui\\uiwinclose.wav", wavClose);
    s_bufOpen  = MakeDSBuffer(wavOpen);
    s_bufClose = MakeDSBuffer(wavClose);
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
                    ImVec2 size_min,     ImVec2 size_max)
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
    ImGui::SetNextWindowSize(default_size,   ImGuiCond_FirstUseEver);
    ImGui::SetNextWindowSizeConstraints(size_min, size_max);

    ImGui::PushStyleColor(ImGuiCol_WindowBg,      ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_Border,        ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_TitleBg,       ImVec4(0, 0, 0, 0));
    ImGui::PushStyleColor(ImGuiCol_TitleBgActive, ImVec4(0, 0, 0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding,
        ImVec2(K_BORDER + 4.f, K_TITLE + 4.f));

    bool vis = ImGui::Begin(id, open,
        ImGuiWindowFlags_NoTitleBar        |
        ImGuiWindowFlags_NoScrollbar       |
        ImGuiWindowFlags_NoScrollWithMouse |
        ImGuiWindowFlags_NoCollapse        |
        ImGuiWindowFlags_NoMove);           // custom drag below; NoMove stops ImGui from
                                            // treating a close-click as a drag-start
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
    // Corners
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

    // Content child window
    ImGui::SetNextWindowScroll(ImVec2(0.f, sc.y));
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0, 0, 0, 0));
    ImGui::BeginChild("##sro_body", ImVec2(-K_SCR_W, 0), false,
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

    // Scrollbar interaction
    // Threshold avoids flicker when content barely overflows (maxY near 0).
    if (sc.maxY > 2.f)
    {
        const float upT = fr.bodyTop, upB = fr.bodyTop + K_SCR_H;
        const float dnB = fr.bodyBot,  dnT = fr.bodyBot  - K_SCR_H;

        // thumb is always K_SCR_H tall,
        // slides within the track rather than scaling with content length.
        const float thumbH = K_SCR_H;
        float thumbTop = fr.trackTop;
        if (fr.trackH > K_SCR_H && sc.maxY > 0.f)
            thumbTop = fr.trackTop + (sc.y / sc.maxY) * (fr.trackH - K_SCR_H);
        thumbTop = (std::max)(fr.trackTop, (std::min)(fr.trackTop + fr.trackH - K_SCR_H, thumbTop));

        ImGui::PushClipRect({ fr.scrX0, fr.bodyTop },
                            { fr.scrX1, fr.bodyBot }, false);

        ImGui::SetCursorScreenPos({ fr.scrX0, upT });
        ImGui::InvisibleButton("##sro_up", { K_SCR_W, K_SCR_H });
        bool upH = ImGui::IsItemHovered(), upA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ fr.scrX0, dnT });
        ImGui::InvisibleButton("##sro_dn", { K_SCR_W, K_SCR_H });
        bool dnH = ImGui::IsItemHovered(), dnA = ImGui::IsItemActive();

        ImGui::SetCursorScreenPos({ fr.scrX0, fr.trackTop });
        ImGui::InvisibleButton("##sro_track", { K_SCR_W, fr.trackH });
        bool trackClicked = ImGui::IsItemClicked();

        ImGui::SetCursorScreenPos({ fr.scrX0, thumbTop });
        ImGui::InvisibleButton("##sro_thumb", { K_SCR_W, K_SCR_H });
        bool thH = ImGui::IsItemHovered(), thA = ImGui::IsItemActive();

        if (ImGui::IsItemClicked()) {
            sc.dragStartMouseY  = ImGui::GetIO().MousePos.y;
            sc.dragStartScrollY = sc.y;
        }

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
        sc.y = (std::max)(0.f, (std::min)(sc.maxY, sc.y));

        // Scrollbar visuals (foreground = above child window)
        ImDrawList* sdl = ImGui::GetForegroundDrawList();

        // Track background
        if (s_sBar)
            sdl->AddImage(TI(s_sBar),
                { fr.scrX0, fr.trackTop }, { fr.scrX1, dnT });
        else
            sdl->AddRectFilled({ fr.scrX0, fr.trackTop }, { fr.scrX1, dnT },
                IM_COL32(10, 9, 14, 220));
        // Up arrow
        if (s_arrowU)
            sdl->AddImage(TI(upA ? s_arrowUP : upH ? s_arrowUF : s_arrowU),
                { fr.scrX0, upT }, { fr.scrX1, upB });
        else
            sdl->AddRectFilled({ fr.scrX0, upT }, { fr.scrX1, upB },
                upA ? IM_COL32(120,100,50,255) : IM_COL32(60,50,30,200));
        // Down arrow
        if (s_arrowD)
            sdl->AddImage(TI(dnA ? s_arrowDP : dnH ? s_arrowDF : s_arrowD),
                { fr.scrX0, dnT }, { fr.scrX1, dnB });
        else
            sdl->AddRectFilled({ fr.scrX0, dnT }, { fr.scrX1, dnB },
                dnA ? IM_COL32(120,100,50,255) : IM_COL32(60,50,30,200));
        // Thumb / scrubber
        if (s_sBtn)
            sdl->AddImage(TI(thA ? s_sBtnP : thH ? s_sBtnF : s_sBtn),
                { fr.scrX0, thumbTop }, { fr.scrX1, thumbTop + thumbH });
        else
            sdl->AddRectFilled({ fr.scrX0, thumbTop },
                               { fr.scrX1, thumbTop + thumbH },
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

    // Position popup flush below the combo box on the frame it opens
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

bool SROButton(const char* id, const char* label, float width, float height)
{
    EnsureLoaded();
    const ImVec2 pos = ImGui::GetCursorScreenPos();
    ImDrawList*  dl  = ImGui::GetWindowDrawList();

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
    return clicked;
}
