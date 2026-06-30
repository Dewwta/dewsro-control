#pragma once
#include <cstdint>
#include <string>
#include <vector>
#include <unordered_map>
#include <cstdio>
#include "Blowfish.h"

class Pk2Reader
{
public:
    Pk2Reader() = default;
    ~Pk2Reader();

    bool Open(const std::wstring& path);
    void Close();
    bool IsOpen() const { return m_file != nullptr; }

    // Read an entire file from the archive by its in-archive path.
    // Returns false if not found. 'out' is resized to the file size.
    bool ReadFile(const std::string& archivePath, std::vector<uint8_t>& out);

    // true if a file exists at the given path.
    bool Exists(const std::string& archivePath);

    // List immediate children (files+folders) of a folder path ("" = root).
    // Returns names only. Useful for debugging / enumerating icon folders.
    std::vector<std::string> List(const std::string& folderPath);

    // The standard Silkroad PK2 key derivation. Exposed so callers can see /
    // override it. The ASCII base key is "169841" run through a small mix
    // against the well-known JoyMax constant.
    static void MakeSilkroadKey(uint8_t outKey[8]);

private:
    // On-disk structures (packed). Sizes are fixed by the format.
#pragma pack(push, 1)
    struct Header
    {
        char     name[30];        // "JoyMax File Manager!\n" + padding
        uint8_t  version[4];
        uint8_t  encryption;      // 1 = entries encrypted
        uint8_t  verifySig[16];   // checksum of "Joymax Pak File" encrypted
        uint8_t  reserved[205];
    };                            // total 256 bytes

    struct Entry
    {
        uint8_t  type;            // 0 = empty, 1 = folder, 2 = file
        char     name[81];
        uint64_t accessTime;
        uint64_t createTime;
        uint64_t modifyTime;
        int64_t  position;        
        uint32_t size;            // file size in bytes
        int64_t  nextChain;       // offset of next chained block (0 if none)
        uint8_t  padding[2];
    };                            // total 128 bytes
#pragma pack(pop)

    struct Block { Entry entries[20]; }; // 20 * 128 = 2560 bytes

    // Resolve a path to its Entry. Returns true and fills 'out' if found.
    bool FindEntry(const std::string& archivePath, Entry& out);

    // Read+ decrypt the 2560-byte block at file offset 'offset'.
    bool ReadBlock(int64_t offset, Block& block);

    // fn returns true to stop early. Returns true if stopped early.
    template <typename Fn>
    bool WalkFolder(int64_t blockOffset, Fn fn);

    static std::string Normalize(const std::string& path); // lowercase, '/'->'\\'
    static std::string EntryName(const Entry& e);

    FILE* m_file = nullptr;
    Blowfish m_bf;
    bool     m_encrypted = false;
};