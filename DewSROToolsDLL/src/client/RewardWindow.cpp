#include "RewardWindow.h"
#include "imgui.h"
#include "SROSkinWindow.h"
#include "../net/DllBridge.h"
#include "../net/NetActions.h"

RewardWindow g_rewardWindow;

static constexpr int   MAX_COLS    = 8;
static constexpr int   MAX_ROWS    = 2;
static constexpr int   MAX_SLOTS   = MAX_COLS * MAX_ROWS;
static constexpr float SLOT_W      = 36.f;
static constexpr float SLOT_H      = 38.f;   // half of 36x76 slot tile (top or bottom half)
static constexpr float SLOT_GAP    = 4.f;
static constexpr float ICON_SIZE   = 32.f;   // centered with 2px border on sides, 3px top/bot
static constexpr float CLAIM_BTN_H = 24.f;
static constexpr int   GLOW_FRAMES = 9;      // pt_edge_effect.ddj is 288x32 = 9 horizontal 32x32 frames
static constexpr float GLOW_CYCLE  = 0.5f;   // seconds per full animation cycle

static inline ImTextureRef ToTI(IDirect3DTexture9* t) {
    return ImTextureRef((ImTextureID)(uintptr_t)t);
}

void RewardWindow::Render() {
    if (!isOpen) return;

    // Center on screen each time the window appears (allows drag after open)
    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_Appearing, ImVec2(0.5f, 0.5f));

    char title[64];
    snprintf(title, sizeof(title), "Level %d Reward", level);

    if (!BeginSROWindow("##rewardwnd", title, &isOpen,
            ImVec2(400.f, 340.f), ImVec2(-1.f, -1.f)))
        return;

    // Slot and glow textures loaded through the shared icon cache
    IDirect3DTexture9* slotTex = iconSource
        ? iconSource->Get("interface\\quick_slot\\qsl_hriz02_slot_tile.ddj") : nullptr;
    IDirect3DTexture9* glowTex = iconSource
        ? iconSource->Get("interface\\pet\\pt_edge_effect.ddj") : nullptr;

    // Digit sprites — interface\item_number\item_number_0..9.ddj
    static IDirect3DTexture9* s_digits[10] = {};
    static float s_numW = 8.f, s_numH = 11.f;
    static bool  s_digLoaded = false;
    if (!s_digLoaded && iconSource) {
        for (int d = 0; d < 10; d++) {
            char p[64]; snprintf(p, sizeof(p), "interface/item_number/item_number_%d.ddj", d);
            s_digits[d] = iconSource->Get(p);
        }
        if (s_digits[0]) {
            D3DSURFACE_DESC desc{};
            if (SUCCEEDED(s_digits[0]->GetLevelDesc(0, &desc)) && desc.Width > 0) {
                s_numW = (float)desc.Width;
                s_numH = (float)desc.Height;
            }
        }
        s_digLoaded = true;
    }

    ImDrawList* dl = ImGui::GetWindowDrawList();

    // Draws n left-to-right from topLeft.
    // Advance by (s_numW - 2) to compensate for transparent side padding in the sprites.
    auto DrawQty = [&](ImVec2 topLeft, int n) {
        char buf[8]; snprintf(buf, sizeof(buf), "%d", n);
        float x = topLeft.x;
        for (const char* c = buf; *c; ++c) {
            const int d = *c - '0';
            if (s_digits[d])
                dl->AddImage(ToTI(s_digits[d]),
                             { x, topLeft.y }, { x + s_numW, topLeft.y + s_numH });
            x += s_numW - 3.f;
        }
    };

    // Header
    ImGui::Spacing();
    ImGui::TextColored(ImVec4(1.0f, 0.85f, 0.3f, 1.0f),
        "Congratulations! You reached level %d!", level);
    ImGui::Spacing();
    ImGui::TextColored(ImVec4(0.0f, 0.85f, 0.9f, 1.0f), "Choose two rewards:");
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // Slot row
    const float rowY = ImGui::GetCursorScreenPos().y;
    const float rowX = ImGui::GetCursorScreenPos().x;

    // pt_edge_effect.ddj is 288x32 = 9 horizontal 32x32 frames
    const float glowT   = fmodf((float)ImGui::GetTime(), GLOW_CYCLE) / GLOW_CYCLE;
    const int   glowFrm = (int)(glowT * GLOW_FRAMES) % GLOW_FRAMES;
    const float glowU0  = glowFrm / (float)GLOW_FRAMES;
    const float glowU1  = (glowFrm + 1) / (float)GLOW_FRAMES;

    int clickedIndex = -2;

    for (int i = 0; i < MAX_SLOTS; i++) {
        const bool hasItem  = (i < (int)options.size());
        const bool selected = hasItem && IsSelected(i);

        const int   slotRow = i / MAX_COLS;
        const int   slotCol = i % MAX_COLS;
        const float slotX   = rowX + slotCol * (SLOT_W + SLOT_GAP);
        const float slotY   = rowY + slotRow * SLOT_H;   // rows stacked with no gap
        const ImVec2  slotTL { slotX,          slotY          };
        const ImVec2  slotBR { slotX + SLOT_W, slotY + SLOT_H };

        // Slot background: row 0 uses top half of 36x76 tile, row 1 uses bottom half.
        const float tileV0 = slotRow == 0 ? 0.f : 0.5f;
        const float tileV1 = slotRow == 0 ? 0.5f : 1.f;
        if (slotTex)
            dl->AddImage(ToTI(slotTex), slotTL, slotBR, {0.f, tileV0}, {1.f, tileV1});
        else {
            dl->AddRectFilled(slotTL, slotBR, IM_COL32(25, 30, 45, 200), 3.f);
            dl->AddRect      (slotTL, slotBR, IM_COL32(60, 70, 90, 180), 3.f, 0, 1.f);
        }

        // Icon centered in slot (32×32 with 2px horizontal border, 3px vertical)
        const ImVec2 iconTL { slotX + (SLOT_W - ICON_SIZE) * 0.5f,
                              slotY + (SLOT_H - ICON_SIZE) * 0.5f };
        const ImVec2 iconBR { iconTL.x + ICON_SIZE, iconTL.y + ICON_SIZE };

        if (hasItem) {
            IDirect3DTexture9* iconTex = GetIcon(options[i].icon);
            SealType seal = GetSealType(options[i].code);

            if (iconTex)
                dl->AddImage(ToTI(iconTex), iconTL, iconBR);
            else
                dl->AddRectFilled(iconTL, iconBR, IM_COL32(40, 50, 70, 255), 2.f);

            // Glow animation overlays the icon on the selected slot
            if (selected && glowTex)
                dl->AddImage(ToTI(glowTex), iconTL, iconBR, {glowU0, 0.f}, {glowU1, 1.f});

            // Quantity badge — top-left of icon, only when qty > 1
            if (options[i].qty > 1)
                DrawQty({ iconTL.x + 1.f, iconTL.y + 2.f }, options[i].qty);

            if (seal != SealType::None)
                g_soxOverlay.Render(iconTL, ImVec2(ICON_SIZE, ICON_SIZE));
        } else {
            const char* glyph = u8"∅";
            ImVec2 gs = ImGui::CalcTextSize(glyph);
            dl->AddText(
                ImVec2(slotTL.x + (SLOT_W - gs.x) * 0.5f,
                       slotTL.y + (SLOT_H - gs.y) * 0.5f),
                IM_COL32(55, 60, 75, 200), glyph);
        }

        // Gold selection ring drawn on top of slot content
        if (selected)
            dl->AddRect(slotTL, slotBR, IM_COL32(200, 180, 80, 220), 2.f, 0, 2.f);

        // Invisible button for click + hover detection
        ImGui::SetCursorScreenPos(slotTL);
        ImGui::PushStyleColor(ImGuiCol_Button,        ImVec4(0, 0, 0, 0));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(1, 1, 1, hasItem ? 0.08f : 0.f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive,  ImVec4(1, 1, 1, hasItem ? 0.16f : 0.f));
        char btnId[32]; snprintf(btnId, sizeof(btnId), "##s%d", i);
        if (ImGui::Button(btnId, ImVec2(SLOT_W, SLOT_H)) && hasItem) {
            clickedIndex = i;
            SROSkin_PlayClick();
        }
        ImGui::PopStyleColor(3);

        if (hasItem && ImGui::IsItemHovered()) {
            ImGui::BeginTooltip();
            const char* name = options[i].name.empty()
                ? options[i].code.c_str() : options[i].name.c_str();
            if (options[i].plus > 0)
                ImGui::Text("%s (+%d)", name, options[i].plus);
            else
                ImGui::Text("%s", name);
            SealType seal = GetSealType(options[i].code);
            if (seal != SealType::None) {
                ImGui::Separator();
                const char* sealName =
                    seal == SealType::Star ? "Seal of Star" :
                    seal == SealType::Moon ? "Seal of Moon" : "Seal of Sun";
                ImVec4 sealColor =
                    seal == SealType::Star ? ImVec4(1.0f, 0.9f, 0.3f, 1.0f) :
                    seal == SealType::Moon ? ImVec4(0.5f, 0.7f, 1.0f, 1.0f) :
                                            ImVec4(1.0f, 0.5f, 0.2f, 1.0f);
                ImGui::TextColored(sealColor, "%s", sealName);
            }
            if (options[i].qty > 1)
                ImGui::TextColored(ImVec4(0.8f, 0.8f, 0.4f, 1.0f), "x%d", options[i].qty);
            ImGui::EndTooltip();
        }
    }

    // Apply selection toggle (two picks; a third click replaces the oldest)
    if (clickedIndex >= 0)
        ToggleSelect(clickedIndex);

    const int  picks    = SelectedCount();
    const bool canClaim = (picks == K_PICKS);

    // Move cursor below the last slot row
    ImGui::SetCursorScreenPos(ImVec2(rowX, rowY + MAX_ROWS * SLOT_H));

    // Bottom section
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // Selection summary
    auto OptLabel = [&](int idx, char* buf, size_t bufSize) {
        const char* nm = options[idx].name.empty()
            ? options[idx].code.c_str() : options[idx].name.c_str();
        if (options[idx].plus > 0)
            snprintf(buf, bufSize, "%s (+%d)", nm, options[idx].plus);
        else if (options[idx].qty > 1)
            snprintf(buf, bufSize, "%s x%d", nm, options[idx].qty);
        else
            snprintf(buf, bufSize, "%s", nm);
    };
    if (picks == 0) {
        ImGui::TextDisabled("Pick two rewards.");
        ImGui::TextDisabled(" ");
    } else {
        char l1[192];
        OptLabel(selected[0], l1, sizeof(l1));
        ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 1.0f), "1. %s", l1);
        if (picks == 2) {
            char l2[192];
            OptLabel(selected[1], l2, sizeof(l2));
            ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 1.0f), "2. %s", l2);
        } else {
            ImGui::TextDisabled("2. Pick one more...");
        }
    }

    ImGui::Spacing();

    const float avail = ImGui::GetContentRegionAvail().x;
    const float btnW  = (avail - 4.f) * 0.5f;

    if (SROButton("##claim_reward", "Claim Rewards", btnW, CLAIM_BTN_H, !canClaim)) {
        const auto& a = options[selected[0]];
        const auto& b = options[selected[1]];
        NetActions::SendRewardClaim(level, a.code, a.qty, a.plus,
                                           b.code, b.qty, b.plus);
        Close();
    }

    ImGui::SameLine(0.f, 4.f);

    if (SROButton("##claim_later", "Claim Later", btnW, CLAIM_BTN_H))
        Close();

    EndSROWindow();
}
