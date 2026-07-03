#pragma once
#include <d3d9.h>
#include "imgui.h"
#include "pk2/IconCache.h"
#include "pk2/Pk2Reader.h"

// Call once after IconCache is first created (from the Present hook).
void SROSkin_Init(IconCache* cache);

// Call once after g_dataPk2 is opened; creates DirectSound device and loads UI sound buffers.
// hwnd must be the game's main window (used for DS cooperative level).
void SROSkin_InitSound(Pk2Reader* dataPk2, HWND hwnd);

// Call once per frame, immediately after ImGui::NewFrame().
// Detects windows that disappeared (plays close sound) and rolls the visibility set.
void SROSkin_NewFrame();

// Returns true while a SROKeybind widget is listening for a key press.
// Gate all global hotkey checks behind this to prevent input passthrough.
bool SROSkin_IsCapturingKey();

// Plays one of the two UI click sounds (uibutton_a/b.wav), randomized like the game.
// SROButton/SROTabBar/SROListItem call this automatically; call it manually for
// custom click targets (icon grids, invisible buttons, etc.).
void SROSkin_PlayClick();

// Begin a SRO-styled skinned window.
// Returns true if content should be rendered; always call EndSROWindow() in that case.
// default_pos {-1,-1} = don't set a first-use position.
bool BeginSROWindow(const char* id,
                    const char* title,
                    bool*       open,
                    ImVec2      default_size = { 380.f, 400.f },
                    ImVec2      default_pos  = {  -1.f,  -1.f },
                    ImVec2      size_min     = { 200.f, 150.f },
                    ImVec2      size_max     = { 900.f, 900.f },
                    bool        resizable    = true);

void EndSROWindow();

// SRO-styled combo box. Draws a mall_box_02 background + rotated right-arrow button.
// Opens a dark popup with blue hover highlight. Returns true when selection changes.
bool SROCombo(const char* id, int* selectedIdx, const char* const* items, int itemCount,
              float width = 120.f, float height = 22.f);

// SRO-styled push button using com_button.ddj. Returns true on click.
// Pass disabled=true to render com_button_disable.ddj with no interaction.
bool SROButton(const char* id, const char* label, float width = 80.f, float height = 22.f, bool disabled = false);

// SRO-styled keybind row using opt_key.ddj (156x28) + opt_key_select.ddj (56x16).
// Stretches to fill available width. Key label in the left box; click to enter
// listening mode; next keypress (ESC = cancel) updates *vkCode. Returns true on change.
bool SROKeybind(const char* id, const char* label, int* vkCode);

// SRO-styled checkbox using com_checkbutton_*.ddj (16x16). Label drawn to the right.
// Pass disabled=true to render the disabled variant with no interaction.
// Returns true when *value changes.
bool SROCheckbox(const char* id, const char* label, bool* value, bool disabled = false);

// 3-piece styled input bar (com_bar01_*.ddj). Wraps ImGui::InputText; background is
// transparent so the bar texture shows through. Returns true on text change.
bool SROInputBar(const char* id, char* buf, size_t bufSize,
                 float width = 0.f, ImGuiInputTextFlags flags = 0, bool center = false);

// Integer variant of SROInputBar using InputScalar. Returns true on value change.
bool SROInputBarInt(const char* id, int* value,
                    float width = 0.f, ImGuiInputTextFlags flags = 0, bool center = false);

// List row using com_bar01_*.ddj; shows com_bar01select_*.ddj overlay when selected.
// height=0 defaults to K_BAR_H (22). Returns true if clicked.
bool SROListItem(const char* id, const char* text, bool selected, float width = 0.f, float height = 0.f);

// Label+value row using str_slot_01.ddj. Label on left (~38%), value in dark box right.
// No interaction. valueColor defaults to light blue.
void SROStatusRow(const char* label, const char* value, float width = 0.f,
                  ImU32 valueColor = IM_COL32(180, 210, 255, 255));

// Layout constants for callers computing heights around tabs and sub-frames.
static constexpr float K_TAB_H  = 22.f;  // opt_long_tab render height
static constexpr float K_TABS_H = 16.f;  // opt_short_tab render height
static constexpr float K_SF_C   =  8.f;  // sub-frame border strip width
static constexpr float K_AF_C   =  8.f;  // action frame border strip width
static constexpr float K_BAR_H  = 22.f;  // com_bar01 / list item row height

// SRO-styled horizontal tab strip (opt_long_tab / opt_short_tab DDJs).
// No hover highlight — tabs change on click only. Returns true when selection changes.
bool SROTabBar(const char* id, const char* const* labels, int count,
               int* selected, float tabW = 90.f, bool longTab = true);

// Sub-frame: bordered content box drawn with frame_sub_* DDJs.
// Background and 8-piece border are drawn on the parent draw list; content goes
// inside a BeginChild with WindowPadding equal to the border strip width.
// Always pair with SROEndSubFrame().
bool SROBeginSubFrame(const char* id, float w, float h);
void SROEndSubFrame();

// Action frame: scrollable content box drawn with frame_g_action_* DDJs (no title header).
// Background drawn manually; 8-piece border overlaid on top after EndChild.
// Content can overflow and will scroll. Add Dummy({0,K_AF_C}) at the start for top clearance.
// Always pair with SROEndActionFrame().
bool SROBeginActionFrame(const char* id, float w, float h);
void SROEndActionFrame();
