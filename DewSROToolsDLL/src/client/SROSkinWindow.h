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

// Begin a SRO-styled skinned window.
// Returns true if content should be rendered; always call EndSROWindow() in that case.
// default_pos {-1,-1} = don't set a first-use position.
bool BeginSROWindow(const char* id,
                    const char* title,
                    bool*       open,
                    ImVec2      default_size = { 380.f, 400.f },
                    ImVec2      default_pos  = {  -1.f,  -1.f },
                    ImVec2      size_min     = { 200.f, 150.f },
                    ImVec2      size_max     = { 900.f, 900.f });

void EndSROWindow();

// SRO-styled combo box. Draws a mall_box_02 background + rotated right-arrow button.
// Opens a dark popup with blue hover highlight. Returns true when selection changes.
bool SROCombo(const char* id, int* selectedIdx, const char* const* items, int itemCount,
              float width = 120.f, float height = 22.f);

// SRO-styled push button using com_button.ddj. Returns true on click.
bool SROButton(const char* id, const char* label, float width = 80.f, float height = 22.f);
