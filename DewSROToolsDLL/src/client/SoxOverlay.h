#pragma once
#include <Windows.h>
#include <string>
#include <vector>
#include "../Logging/Logger.h"
#include <map>
#include <d3d9.h>
#include <d3dx9.h>
#include "imgui.h"

struct SoxOverlay
{
    IDirect3DTexture9* texture = nullptr;
    int frameCount = 32;
    int cols = 8; 
    int rows = 4; 
    int currentFrame = 0;
    DWORD lastTick = 0;
    int frameMs = 50; 

    void Load(IDirect3DDevice9* device, const char* path) {
        HRESULT hr = D3DXCreateTextureFromFileA(device, path, &texture);
        auto& log = GetLogger();
        if (SUCCEEDED(hr))
            log.Info("SoxOverlay::Load", std::string("Loaded: ") + path);
        else
            log.Err("SoxOverlay::Load", std::string("Failed to load: ") + path + " hr=" + std::to_string(hr));
    }

    void Release() {
        if (texture) { texture->Release(); texture = nullptr; }
    }

    void Update() {
        DWORD now = GetTickCount();
        if (now - lastTick >= (DWORD)frameMs) {
            currentFrame = (currentFrame + 1) % frameCount;
            lastTick = now;
        }
    }

    // call this after ImGui::Image or invisible button for the icon
    void Render(ImVec2 pos, ImVec2 size) {
        auto& log = GetLogger();

        if (!texture) return;
        Update();

        float frameW = 1.0f / cols;
        float frameH = 1.0f / rows;
        int col = currentFrame % cols;
        int row = currentFrame / cols;

        ImVec2 uv0(col * frameW, row * frameH);
        ImVec2 uv1(col * frameW + frameW, row * frameH + frameH);

        ImGui::GetForegroundDrawList()->AddImage(
            (ImTextureID)texture,
            pos,
            ImVec2(pos.x + size.x, pos.y + size.y), // ← no * 2
            uv0, uv1,
            IM_COL32(255, 255, 255, 255)
        );
        
    }
};

extern SoxOverlay g_soxOverlay;