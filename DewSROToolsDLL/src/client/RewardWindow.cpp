#include "RewardWindow.h"
#include "imgui.h"
#include "../net/DllBridge.h"
#include "../net/NetActions.h"

RewardWindow g_rewardWindow;

static constexpr int   MAX_SLOTS = 8;
static constexpr float ICON_SIZE = 52.0f;
static constexpr float SLOT_PAD = 6.0f;
static constexpr float SLOT_W = ICON_SIZE + SLOT_PAD * 2.0f;
static constexpr float SLOT_H = ICON_SIZE + SLOT_PAD * 2.0f;
static constexpr float SLOT_GAP = 4.0f;
static constexpr float CLAIM_BTN_H = 28.0f;
static constexpr float WIN_PADDING = 16.0f;
static constexpr float WIN_W = WIN_PADDING + MAX_SLOTS * (SLOT_W + SLOT_GAP) - SLOT_GAP + WIN_PADDING;

void RewardWindow::Render() {
    if (!isOpen) return;

    const float WIN_H =
        8.0f              // top spacing
        + 20.0f           // congrats
        + 6.0f
        + 16.0f           // "choose" line
        + 8.0f
        + 1.0f            // separator
        + 8.0f
        + SLOT_H          // icon row (no labels)
        + 10.0f           // spacing before sep
        + 1.0f            // separator
        + 8.0f
        + 20.0f           // selected-name line (always reserved)
        + 6.0f
        + CLAIM_BTN_H     // Claim Reward (shown or greyed)
        + 4.0f
        + CLAIM_BTN_H     // Claim Later  (always)
        + 24.0f;          // bottom pad

    ImGuiIO& io = ImGui::GetIO();
    ImGui::SetNextWindowPos(
        ImVec2(io.DisplaySize.x * 0.5f, io.DisplaySize.y * 0.5f),
        ImGuiCond_Always, ImVec2(0.5f, 0.5f));
    ImGui::SetNextWindowSize(ImVec2(WIN_W, WIN_H), ImGuiCond_Always);
    ImGui::SetNextWindowBgAlpha(0.97f);

    ImGuiWindowFlags flags =
        ImGuiWindowFlags_NoMove |
        ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoCollapse |
        ImGuiWindowFlags_NoSavedSettings |
        ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoScrollWithMouse;

    char title[64];
    snprintf(title, sizeof(title), "Level %d Reward", level);
    if (!ImGui::Begin(title, nullptr, flags)) { ImGui::End(); return; }

    ImDrawList* dl = ImGui::GetWindowDrawList();

    // ── Header ────────────────────────────────────────────────────────────
    ImGui::Spacing();
    ImGui::TextColored(ImVec4(1.0f, 0.85f, 0.3f, 1.0f),
        "Congratulations! You reached level %d!", level);
    ImGui::Spacing();
    ImGui::TextDisabled("Choose one reward:");
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    // ── Slot row ──────────────────────────────────────────────────────────
    const float rowStartY = ImGui::GetCursorScreenPos().y;
    const float rowStartX = ImGui::GetCursorScreenPos().x;

    int clickedIndex = -2; // sentinel: -2 = no click this frame

    for (int i = 0; i < MAX_SLOTS; i++) {
        const bool hasItem = (i < (int)options.size());
        const bool selected = hasItem && (selectedIndex == i);

        const float slotX = rowStartX + i * (SLOT_W + SLOT_GAP);
        ImVec2 slotTL(slotX, rowStartY);
        ImVec2 slotBR(slotX + SLOT_W, rowStartY + SLOT_H);
        ImVec2 iconTL(slotX + SLOT_PAD, rowStartY + SLOT_PAD);

        // Background
        if (selected) {
            dl->AddRectFilled(slotTL, slotBR, IM_COL32(50, 80, 35, 220), 4.0f);
            dl->AddRect(slotTL, slotBR, IM_COL32(90, 210, 50, 255), 4.0f, 0, 2.0f);
        }
        else {
            dl->AddRectFilled(slotTL, slotBR, IM_COL32(25, 30, 45, 200), 4.0f);
            dl->AddRect(slotTL, slotBR, IM_COL32(60, 70, 90, 180), 4.0f, 0, 1.0f);
        }

        // Icon / placeholder
        if (hasItem) {
            IDirect3DTexture9* tex = GetIcon(options[i].icon);
            SealType seal = GetSealType(options[i].code);

            if (tex)
                dl->AddImage((ImTextureID)tex,
                    iconTL, ImVec2(iconTL.x + ICON_SIZE, iconTL.y + ICON_SIZE));
            else
                dl->AddRectFilled(iconTL,
                    ImVec2(iconTL.x + ICON_SIZE, iconTL.y + ICON_SIZE),
                    IM_COL32(40, 50, 70, 255), 3.0f);

            if (seal != SealType::None)
                g_soxOverlay.Render(iconTL, ImVec2(ICON_SIZE, ICON_SIZE));
        }
        else {
            const char* glyph = u8"\u2205";
            ImVec2 gs = ImGui::CalcTextSize(glyph);
            dl->AddText(
                ImVec2(slotTL.x + (SLOT_W - gs.x) * 0.5f,
                    slotTL.y + (SLOT_H - gs.y) * 0.5f),
                IM_COL32(55, 60, 75, 200), glyph);
        }

        // Click/hover button
        ImGui::SetCursorScreenPos(slotTL);
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0, 0, 0, 0));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(1, 1, 1, hasItem ? 0.07f : 0.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(1, 1, 1, hasItem ? 0.14f : 0.0f));
        char btnId[32]; snprintf(btnId, sizeof(btnId), "##s%d", i);
        bool clicked = ImGui::Button(btnId, ImVec2(SLOT_W, SLOT_H));
        ImGui::PopStyleColor(3);

        // Record the click index — don't mutate selectedIndex yet
        if (clicked && hasItem)
            clickedIndex = i;

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

    // apply click mutation
    if (clickedIndex >= 0)
        selectedIndex = (selectedIndex == clickedIndex) ? -1 : clickedIndex;

    // recompute after mutation
    const bool hasSelection = (selectedIndex >= 0 && selectedIndex < (int)options.size());

    // Advance cursor past slot row
    ImGui::SetCursorScreenPos(ImVec2(rowStartX, rowStartY + SLOT_H));

    // Bottom Bar
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    if (hasSelection) {
        const char* selName = options[selectedIndex].name.empty()
            ? options[selectedIndex].code.c_str()
            : options[selectedIndex].name.c_str();
        if (options[selectedIndex].plus > 0)
            ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 1.0f),
                "Selected: %s (+%d)", selName, options[selectedIndex].plus);
        else
            ImGui::TextColored(ImVec4(0.4f, 1.0f, 0.4f, 1.0f),
                "Selected: %s", selName);
    }
    else {
        ImGui::TextDisabled(" "); // blank placeholder
    }

    ImGui::Spacing();

    const float btnW = (WIN_W - WIN_PADDING * 2.0f - 4.0f) * 0.5f;

    // Claim Reward — only enabled when something is selected
    if (!hasSelection) {
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.10f, 0.30f, 0.10f, 0.4f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.10f, 0.30f, 0.10f, 0.4f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.10f, 0.30f, 0.10f, 0.4f));
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 0.5f));
        ImGui::Button("Claim Reward", ImVec2(btnW, CLAIM_BTN_H)); // non-interactive visually
        ImGui::PopStyleColor(4);
    }
    else {
        ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.15f, 0.50f, 0.15f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.20f, 0.65f, 0.20f, 1.0f));
        ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.10f, 0.35f, 0.10f, 1.0f));
        if (ImGui::Button("Claim Reward", ImVec2(btnW, CLAIM_BTN_H))) {
            const auto& sel = options[selectedIndex];
            NetActions::SendRewardClaim(level, sel.code, sel.qty, sel.plus);
            Close();
        }
        ImGui::PopStyleColor(3);
    }

    ImGui::SameLine(0.0f, 4.0f);

    ImGui::PushStyleColor(ImGuiCol_Button, ImVec4(0.30f, 0.12f, 0.12f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_ButtonHovered, ImVec4(0.45f, 0.18f, 0.18f, 1.0f));
    ImGui::PushStyleColor(ImGuiCol_ButtonActive, ImVec4(0.20f, 0.08f, 0.08f, 1.0f));
    if (ImGui::Button("Claim Later", ImVec2(-1.0f, CLAIM_BTN_H)))
        Close();
    ImGui::PopStyleColor(3);

    ImGui::End();
}