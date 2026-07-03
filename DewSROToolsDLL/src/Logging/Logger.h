#pragma once
#include <Windows.h>
#include <deque>
#include <vector>
#include <mutex>
#include <string>

// level: 0=INFO 1=WARN 2=ERR 3=DBG
struct LogEntry
{
	int         level = 0;
	std::string loc;
	std::string msg;
};

class Logger
{
public:
	static constexpr size_t K_MAX_ENTRIES = 4000;

	// Opens the on-disk log file.
	void Init();

	void Info(std::string loc, std::string msg);
	void Warn(std::string loc, std::string msg);
	void Err(std::string loc, std::string msg);
	void Dbg(std::string loc, std::string msg);

	std::mutex& Mutex() { return m_logMutex; }
	const std::deque<LogEntry>& Entries() const { return m_entries; }
	void Clear();

private:
	void Append(int level, const char* levelName,
	            const std::string& loc, const std::string& msg);

	FILE* m_logFile = nullptr;
	std::deque<LogEntry> m_entries;
	std::mutex m_logMutex;
};

Logger& GetLogger();
