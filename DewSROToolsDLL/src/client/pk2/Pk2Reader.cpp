// Pk2Reader.cpp
#include "Pk2Reader.h"
#include <algorithm>
#include <cstring>

#ifdef _WIN32
#include <windows.h>
#endif


void Pk2Reader::MakeSilkroadKey(uint8_t outKey[8])
{
    static const char     kPassword[] = "169841";          // 6 chars, no NUL
    static const uint8_t  kSalt[10] = { 0x03,0xF8,0xE4,0x44,0x88,0x99,0x3F,0x64,0xFE,0x35 };
    const size_t          plainLen = sizeof(kPassword) - 1; // 6

    // outKey is sized 8 here for convenience; only the first plainLen bytes
    std::memset(outKey, 0, 8);
    for (size_t i = 0; i < plainLen && i < 8; ++i)
        outKey[i] = uint8_t(kPassword[i]) ^ kSalt[i];
    // Result: 32 CE DD 7C BC A8
}

Pk2Reader::~Pk2Reader() { Close(); }

void Pk2Reader::Close()
{
    if (m_file) { std::fclose(m_file); m_file = nullptr; }
}

bool Pk2Reader::Open(const std::wstring& path)
{
    static_assert(sizeof(Header) == 256, "PK2 header must be 256 bytes");
    static_assert(sizeof(Entry) == 128, "PK2 entry must be 128 bytes");
    static_assert(sizeof(Block) == 2560, "PK2 block must be 2560 bytes");

    Close();

#ifdef _WIN32
    if (_wfopen_s(&m_file, path.c_str(), L"rb") != 0 || !m_file)
        return false;
#else
    // Non-Windows fallback for the offline verification harness.
    std::string narrow(path.begin(), path.end());
    m_file = std::fopen(narrow.c_str(), "rb");
    if (!m_file) return false;
#endif

    Header hdr{};
    if (std::fread(&hdr, 1, sizeof(hdr), m_file) != sizeof(hdr)) { Close(); return false; }

    m_encrypted = (hdr.encryption != 0);
    if (m_encrypted) {
        uint8_t key[8];
        MakeSilkroadKey(key);
        m_bf.SetKey(key, 6); // 6-byte effective key
    }
    return true;
}

bool Pk2Reader::ReadBlock(int64_t offset, Block& block)
{
    if (!m_file) return false;
#ifdef _WIN32
    if (_fseeki64(m_file, offset, SEEK_SET) != 0) return false;
#else
    if (fseeko(m_file, (off_t)offset, SEEK_SET) != 0) return false;
#endif
    if (std::fread(&block, 1, sizeof(Block), m_file) != sizeof(Block))
        return false;

    if (m_encrypted)
        m_bf.DecryptEcb(reinterpret_cast<uint8_t*>(&block), sizeof(Block));

    return true;
}

std::string Pk2Reader::EntryName(const Entry& e)
{
    // name is a fixed 81-byte field, NUL-terminated.
    size_t n = 0;
    while (n < sizeof(e.name) && e.name[n] != '\0') ++n;
    return std::string(e.name, e.name + n);
}

std::string Pk2Reader::Normalize(const std::string& path)
{
    std::string s = path;
    for (char& c : s) {
        if (c == '/') c = '\\';
        c = (char)std::tolower((unsigned char)c);
    }
    // strip leading separators
    size_t start = 0;
    while (start < s.size() && s[start] == '\\') ++start;
    return s.substr(start);
}

template <typename Fn>
bool Pk2Reader::WalkFolder(int64_t blockOffset, Fn fn)
{
    int64_t offset = blockOffset;
    while (offset != 0) {
        Block block;
        if (!ReadBlock(offset, block)) return false;

        for (int i = 0; i < 20; ++i) {
            const Entry& e = block.entries[i];
            if (e.type == 0) continue;          // empty slot
            std::string nm = EntryName(e);
            if (nm == "." || nm == "..") continue; // navigators
            if (fn(e, nm)) return true;         // caller asked to stop
        }
        // The 20th entry's nextChain links to a continuation block for this
        offset = block.entries[19].nextChain;
    }
    return false;
}

bool Pk2Reader::FindEntry(const std::string& archivePath, Entry& out)
{
    std::string norm = Normalize(archivePath);
    if (norm.empty()) return false;

    // Split into path components.
    std::vector<std::string> parts;
    size_t pos = 0;
    while (pos < norm.size()) {
        size_t sep = norm.find('\\', pos);
        if (sep == std::string::npos) { parts.push_back(norm.substr(pos)); break; }
        if (sep > pos) parts.push_back(norm.substr(pos, sep - pos));
        pos = sep + 1;
    }
    if (parts.empty()) return false;

    // Root folder's first block begins right after the 256-byte header.
    int64_t curBlock = 256;

    for (size_t pi = 0; pi < parts.size(); ++pi) {
        const std::string& want = parts[pi];
        bool isLast = (pi + 1 == parts.size());
        Entry found{};
        bool got = WalkFolder(curBlock, [&](const Entry& e, const std::string& nm) {
            std::string lower = nm;
            for (char& c : lower) c = (char)std::tolower((unsigned char)c);
            if (lower == want) { found = e; return true; }
            return false;
            });
        if (!got) return false;

        if (isLast) { out = found; return true; }

        // Need to descend, must be a folder.
        if (found.type != 1) return false;
        curBlock = found.position; // folder's child block offset
    }
    return false;
}

bool Pk2Reader::Exists(const std::string& archivePath)
{
    Entry e{};
    return FindEntry(archivePath, e);
}

bool Pk2Reader::ReadFile(const std::string& archivePath, std::vector<uint8_t>& out)
{
    Entry e{};
    if (!FindEntry(archivePath, e)) return false;
    if (e.type != 2) return false; // not a file

    out.resize(e.size);
    if (e.size == 0) return true;

#ifdef _WIN32
    if (_fseeki64(m_file, e.position, SEEK_SET) != 0) return false;
#else
    if (fseeko(m_file, (off_t)e.position, SEEK_SET) != 0) return false;
#endif
    // File payloads are stored plaintext — no decryption needed.
    return std::fread(out.data(), 1, e.size, m_file) == e.size;
}

std::vector<std::string> Pk2Reader::List(const std::string& folderPath)
{
    std::vector<std::string> names;
    int64_t startBlock;

    std::string norm = Normalize(folderPath);
    if (norm.empty()) {
        startBlock = 256; // root
    }
    else {
        Entry e{};
        if (!FindEntry(folderPath, e) || e.type != 1) return names;
        startBlock = e.position;
    }
    WalkFolder(startBlock, [&](const Entry&, const std::string& nm) {
        names.push_back(nm);
        return false;
        });
    return names;
}