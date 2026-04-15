#pragma once
#include <string>
#include <thread>
#include <atomic>
#include <functional>
#include <unordered_map>


#define BRIDGE_HOST "REDACTED"
#define BRIDGE_PORT 9001

struct PlayerState
{
    std::string accName;
    std::string charName;
    int accJID = 0;
    int charId = 0;
    bool IsAfk = false;
    int ZerkLevel = 0;
    int strength = 0, intelligence = 0;
    int hp = 0, mp = 0;
    int sessionKills = 0;
    int unusedStatPoints = 0;
    int currentLevel = 0;
    uint64_t gold = 0;
};

using BridgeHandler = std::function<void(const std::string& json)>;
class DllBridge
{
public:
    void RegisterHandler(const std::string& type, BridgeHandler handler);
    std::string m_username;
    std::atomic<bool> m_connected{ false };
    PlayerState m_state;
    std::function<void(const std::string& type, const std::string& json)> OnEvent;

    void SetIdentity(const std::string& username);
    void Connect();
    void Send(const std::string& msg);
    std::string ExtractStr(const std::string& json, const std::string& key);
    int ExtractInt(const std::string& json, const std::string& key);

private:
    void* m_socket = nullptr; // opaque
    void RunLoop();
    
    void Dispatch(const std::string& json);
    std::unordered_map<std::string, BridgeHandler> m_handlers;
};

extern DllBridge g_bridge;