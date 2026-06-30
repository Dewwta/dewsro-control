// IconCache.h — turns PK2 DDJ icons into cached D3D9 textures for ImGui.
//
// DDJ format: a 20-byte JoyMax wrapper (8 magic + 4 version + 4 data size + 4 flags)
// followed by a standard DDS file. So DDJ->DDS is just "skip 20 bytes". We hand the DDS bytes
// to D3DX, which understands DXT/BC compression natively, and cache the
// resulting IDirect3DTexture9* keyed by archive path. The texture pointer is
// used directly as ImTextureID by ImGui's D3D9 backend.
#pragma once
#include <d3d9.h>
#include <string>
#include <unordered_map>
#include <vector>
#include <cstdint>
#include "Pk2Reader.h"

class IconCache
{
public:

    IconCache(IDirect3DDevice9* device, Pk2Reader& pk2)
        : m_device(device), m_pk2(pk2) {}

    ~IconCache() { Clear(); }

    IDirect3DTexture9* Get(const std::string& archivePath);

    void* GetImTex(const std::string& archivePath)
    {
        return reinterpret_cast<void*>(Get(archivePath));
    }

    // Drop a single cached entry
    void Evict(const std::string& archivePath);

    // Release every cached texture.
    void Clear();

    void OnDeviceLost() {}
    void OnDeviceReset() {}

    size_t Count() const { return m_cache.size(); }

private:
    IDirect3DTexture9* Load(const std::string& archivePath);

    IDirect3DDevice9* m_device;
    Pk2Reader& m_pk2;
    std::unordered_map<std::string, IDirect3DTexture9*> m_cache;
};