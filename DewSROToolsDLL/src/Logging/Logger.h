#pragma once
#include <iostream>
#include <Windows.h>
#include <vector>
#include <mutex>

class Logger
{
public:
	void Alloc();
	void ToggleState();
	void SetState(bool enabled);
	void Info(std::string loc, std::string msg);
	void Warn(std::string loc, std::string msg);
	void Err(std::string loc, std::string msg);
	void Dbg(std::string loc, std::string msg);
private:
	void WriteToFile(const char* level, const std::string& loc, const std::string& msg);
	FILE* m_logFile = nullptr;
	bool m_state = false;
	bool m_isAlloced = false;
	std::vector<std::string> m_logs;
	HWND m_consoleHwnd;
	HANDLE m_consoleHandle;
	std::mutex m_logMutex;
	const WORD DEFAULT_COLOR =
		FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY;
};

Logger& GetLogger();