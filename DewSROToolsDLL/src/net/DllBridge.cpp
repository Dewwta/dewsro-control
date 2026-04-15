#define _WINSOCKAPI_
#include <winsock2.h>
#include <ws2tcpip.h>
#include <Windows.h>
#include "DllBridge.h"
#include <iostream>

DllBridge g_bridge;

// reinterpret the opaque pointer back to SOCKET
#define SOCK ((SOCKET)(size_t)m_socket)

void DllBridge::SetIdentity(const std::string& username) {
    m_username = username;
}

void DllBridge::Connect() {
    if (m_username.empty() || m_connected) return;
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
            closesocket(s);
            m_socket = nullptr;
            Sleep(2000);
            continue;
        }
        DWORD timeout = 1000; // 1000ms = 1 second
        setsockopt(s, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));

        Send("{\"type\":\"auth\",\"user\":\"" + m_username + "\"}");
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
                // Proxy closed the connection gracefully
                m_connected = false;
            }
            else {
                // bytes are negative
                int err = WSAGetLastError();
                if (err != WSAETIMEDOUT) {
                    // It's a real error (WSAECONNRESET), kill the loop to reconnect
                    m_connected = false;
                }
                // If it IS WSAETIMEDOUT, do nothing
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
    return std::stoi(json.substr(s));
}

void DllBridge::Dispatch(const std::string& json) {
    std::string type = ExtractStr(json, "type");
    auto it = m_handlers.find(type);
    if (it != m_handlers.end())
        it->second(json);
}

void DllBridge::RegisterHandler(const std::string& type, BridgeHandler handler) {
    m_handlers[type] = handler;
}
