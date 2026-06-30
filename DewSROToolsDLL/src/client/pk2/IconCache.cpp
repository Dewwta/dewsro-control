// IconCache.cpp
#include "IconCache.h"
#include <d3dx9tex.h>   // D3DXCreateTextureFromFileInMemoryEx
#pragma comment(lib, "d3dx9.lib")

// DDJ layout: [0..7] "JMXVDDJ\0" magic, [8..11] version, [12..15] data size,
// [16..19] flags, [20..] raw DDS payload. Total header = 20 bytes.
static const size_t DDJ_HEADER_SIZE = 20;
static const uint32_t DDS_MAGIC = 0x20534444; // 'DDS '

static std::string ToLowerKey(const std::string& s)
{
    std::string k = s;
    for (char& c : k) { if (c == '/') c = '\\'; c = (char)tolower((unsigned char)c); }
    return k;
}

IDirect3DTexture9* IconCache::Get(const std::string& archivePath)
{
    std::string key = ToLowerKey(archivePath);
    auto it = m_cache.find(key);
    if (it != m_cache.end())
        return it->second; // may be nullptr if a prior load failed

    IDirect3DTexture9* tex = Load(archivePath);
    m_cache.emplace(key, tex);
    return tex;
}

IDirect3DTexture9* IconCache::Load(const std::string& archivePath)
{
    if (!m_device) return nullptr;

    std::vector<uint8_t> ddj;
    if (!m_pk2.ReadFile(archivePath, ddj))
        return nullptr;

    if (ddj.size() <= DDJ_HEADER_SIZE)
        return nullptr;

    const uint8_t* ddsData = ddj.data() + DDJ_HEADER_SIZE;
    size_t         ddsSize = ddj.size() - DDJ_HEADER_SIZE;

    if (ddsSize < 4 || *reinterpret_cast<const uint32_t*>(ddsData) != DDS_MAGIC)
    {
        bool found = false;
        for (size_t off = 0; off + 4 <= ddj.size() && off <= 64; ++off)
        {
            if (*reinterpret_cast<const uint32_t*>(ddj.data() + off) == DDS_MAGIC)
            {
                ddsData = ddj.data() + off;
                ddsSize = ddj.size() - off;
                found = true;
                break;
            }
        }
        if (!found) return nullptr;
    }

    IDirect3DTexture9* tex = nullptr;
    HRESULT hr = D3DXCreateTextureFromFileInMemoryEx(
        m_device,
        ddsData, (UINT)ddsSize,
        D3DX_DEFAULT_NONPOW2,   // width  — keep source size
        D3DX_DEFAULT_NONPOW2,   // height
        D3DX_FROM_FILE,         // mip levels from file
        0,                      // usage
        D3DFMT_FROM_FILE,       // keep DXT compression (lighter VRAM)
        D3DPOOL_MANAGED,        // survives device reset, no manual restore
        D3DX_DEFAULT,           // filter
        D3DX_DEFAULT,           // mip filter
        0,                      // no colorkey
        nullptr, nullptr,
        &tex);

    if (FAILED(hr) || !tex)
        return nullptr;

    return tex;
}

void IconCache::Evict(const std::string& archivePath)
{
    std::string key = ToLowerKey(archivePath);
    auto it = m_cache.find(key);
    if (it != m_cache.end())
    {
        if (it->second) it->second->Release();
        m_cache.erase(it);
    }
}

void IconCache::Clear()
{
    for (auto& kv : m_cache)
        if (kv.second) kv.second->Release();
    m_cache.clear();
}