#pragma once
#include <string>
#include <thread>
#include <atomic>
#include <functional>
#include <unordered_map>


#define BRIDGE_HOST "REDACTED"
#define BRIDGE_PORT 9001

struct OverlayState
{
    std::string charName;
    int hp = 0, maxHp = 0;
    int mp = 0, maxMp = 0;
    uint64_t gold = 0;
    bool valid = false;
};

using BridgeHandler = std::function<void(const std::string& json)>;
class DllBridge
{
public:
    void RegisterHandler(const std::string& type, BridgeHandler handler);
    std::string m_username;
    std::atomic<bool> m_connected{ false };
    OverlayState m_state;
    std::function<void(const std::string& type, const std::string& json)> OnEvent;

    void SetIdentity(const std::string& username);
    void Connect();
    void Send(const std::string& msg);

private:
    void* m_socket = nullptr; // opaque
    void RunLoop();
    std::string ExtractStr(const std::string& json, const std::string& key);
    int ExtractInt(const std::string& json, const std::string& key);
    void Dispatch(const std::string& json);
    std::unordered_map<std::string, BridgeHandler> m_handlers;
};

extern DllBridge g_bridge;