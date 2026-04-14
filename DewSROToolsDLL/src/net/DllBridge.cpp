#define _WINSOCKAPI_
#include <winsock2.h>
#include <ws2tcpip.h>
#include <Windows.h>
#include "DllBridge.h"

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
    send(SOCK, line.c_str(), (int)line.size(), 0);
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

        Send("{\"type\":\"auth\",\"user\":\"" + m_username + "\"}");
        m_connected = true;

        char buf[4096];
        std::string accum;

        while (true) {
            int bytes = recv(s, buf, sizeof(buf) - 1, 0);
            if (bytes <= 0) break;
            buf[bytes] = '\0';
            accum += buf;

            size_t pos;
            while ((pos = accum.find('\n')) != std::string::npos) {
                std::string line = accum.substr(0, pos);
                accum.erase(0, pos + 1);
                if (!line.empty()) Dispatch(line);
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

    if (type == "session_init") {
        m_state.charName = ExtractStr(json, "charName");
        m_state.hp = ExtractInt(json, "hp");
        m_state.maxHp = ExtractInt(json, "maxHp");
        m_state.mp = ExtractInt(json, "mp");
        m_state.maxMp = ExtractInt(json, "maxMp");
        m_state.gold = (uint64_t)ExtractInt(json, "gold");
        m_state.valid = true;
    }
    else if (type == "hp_update") {
        m_state.hp = ExtractInt(json, "hp");
        m_state.maxHp = ExtractInt(json, "maxHp");
    }
    else if (type == "gold_update") {
        m_state.gold = (uint64_t)ExtractInt(json, "gold");
    }
}

// TODO: make handler registry, and make abstractions for sending. 