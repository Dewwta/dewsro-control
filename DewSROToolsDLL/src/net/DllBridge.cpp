#define _WINSOCKAPI_
#include <winsock2.h>
#include <ws2tcpip.h>
#include <Windows.h>
#include "DllBridge.h"
#include <iostream>
#include "Logging/Logger.h"

DllBridge g_bridge;

// reinterpret the opaque pointer back to SOCKET
#define SOCK ((SOCKET)(size_t)m_socket)

void DllBridge::SetIdentity(const std::string& username) {
    m_username = username;
}

void DllBridge::Reconnect(const std::string& username) {
    m_connected = false;
    m_started.store(false);

    SetIdentity(username);
    Connect();
}

void DllBridge::Connect() {
    if (m_username.empty()) return;
    if (m_started.exchange(true)) return;
    std::thread([this]() { RunLoop(); }).detach();
}

void DllBridge::Send(const std::string& msg) {
    if (!m_socket) return;
    std::string line = msg + "\n";
    if (send((SOCKET)(size_t)m_socket, line.c_str(), (int)line.size(), 0) == SOCKET_ERROR) {
        m_connected = false;
        std::cout << "Error connecting client to proxy socket" << std::endl;
    }
}

void DllBridge::RunLoop() {
    Sleep(3000);
    
    auto& log = GetLogger();
    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    while (true) {
        SOCKET s = socket(AF_INET, SOCK_STREAM, 0);
        m_socket = (void*)(size_t)s;

        hostent* host = (hostent*)gethostbyname(BRIDGE_HOST);
        if (!host) {
            closesocket(s);
            m_socket = nullptr;
            Sleep(2000);
            continue;
        }

        sockaddr_in addr{};
        addr.sin_family = AF_INET;
        addr.sin_port = htons(BRIDGE_PORT);
        addr.sin_addr = *(in_addr*)host->h_addr_list[0];

        if (connect(s, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
            log.Err("DllBridge::RunLoop", "connect() failed, WSAError=" + std::to_string(WSAGetLastError()));
            closesocket(s);
            m_socket = nullptr;
            Sleep(2000);
            continue;
        }
        DWORD timeout = 1000; // 1000ms = 1 second
        setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));
        // this would also send the password in prod, not right now. im way too lazy
        Send("{\"type\":\"auth\",\"user\":\"" + m_username + "\"}");
        log.Info("DllBridge::RunLoop", "Sent user auth, waiting for ack...");
        m_connected = true;
        

        char buf[4096];
        std::string accum;
        DWORD lastHeartbeat = GetTickCount();
        while (m_connected) {
            DWORD currentTick = GetTickCount();

            // heartbeat
            if (currentTick - lastHeartbeat > 30000) {
                Send("{\"type\":\"ping\"}");
                lastHeartbeat = currentTick;
                log.Info("DllBridge::Heartbeat", "Hearbeat sent");
            }

            int bytes = recv(s, buf, sizeof(buf) - 1, 0);

            if (bytes > 0) {
                buf[bytes] = '\0';
                accum += buf;

                size_t pos;
                while ((pos = accum.find('\n')) != std::string::npos) {
                    std::string line = accum.substr(0, pos);
                    accum.erase(0, pos + 1);
                    if (!line.empty()) Dispatch(line);
                }
            }
            else if (bytes == 0) {
                // Proxy closed the connection
                log.Err("DllBridge::Parse", "Client closed the connection");
                m_connected = false;
                
            }
            else {
                // bytes are negative
                int err = WSAGetLastError();
                if (err != WSAETIMEDOUT) {
                    // it a real error -WSAECONNRESET-, kill the loop to reconnect

                    m_connected = false;
                }
                // if its WSAETIMEDOUT, do nothing
            }
        }

        closesocket(s);
        m_socket = nullptr;
        m_connected = false;
        Sleep(2000);
    }
}

std::string DllBridge::ExtractStr(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":\"";
    auto s = json.find(search);
    if (s == std::string::npos) return "";
    s += search.size();
    auto e = json.find("\"", s);
    return json.substr(s, e - s);
}

int DllBridge::ExtractInt(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":";
    auto s = json.find(search);
    if (s == std::string::npos) return 0;
    s += search.size();
    try {
        return std::stoi(json.substr(s));
    }
    catch (...) {
        return 0;
    }
}


uint64_t DllBridge::ExtractUint64(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":";
    auto s = json.find(search);
    if (s == std::string::npos) return 0;

    s += search.size();

    // Find where the number ends (usually a comma or closing brace)
    auto e = json.find_first_of(",}", s);
    std::string valueStr = json.substr(s, e - s);

    try {
        return std::stoull(valueStr);
    }
    catch (...) {
        return 0;
    }
}


void DllBridge::Dispatch(const std::string& json) {
    std::string type = ExtractStr(json, "type");
    auto& log = GetLogger();
    log.Dbg("DllBridge::Dispatch", "type='" + type + "' json=" + json);
    auto it = m_handlers.find(type);
    if (it != m_handlers.end())
        it->second(json);
    else
        log.Warn("DllBridge::Dispatch", "No handler for type: " + type);
}

void DllBridge::RegisterHandler(const std::string& type, BridgeHandler handler) {
    m_handlers[type] = handler;
}


void DllBridge::ClearSession() {
    m_state = PlayerState{};
    m_sessionState = State{};
    unclaimedRewards.clear();
}
