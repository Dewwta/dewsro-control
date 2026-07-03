#include "Logger.h"

void Logger::Init() {
	std::lock_guard<std::mutex> lock(m_logMutex);
	if (m_logFile) return;
	fopen_s(&m_logFile, "C:\\DewSROToolkit.log", "w");
	if (m_logFile) setvbuf(m_logFile, nullptr, _IONBF, 0); // unbuffered
}

void Logger::Append(int level, const char* levelName,
                    const std::string& loc, const std::string& msg) {
	std::lock_guard<std::mutex> lock(m_logMutex);

	if (m_logFile)
		fprintf(m_logFile, "[%s] [%s] %s\n", levelName, loc.c_str(), msg.c_str());

	m_entries.push_back({ level, loc, msg });
	while (m_entries.size() > K_MAX_ENTRIES)
		m_entries.pop_front();
}

void Logger::Clear() {
	std::lock_guard<std::mutex> lock(m_logMutex);
	m_entries.clear();
}

void Logger::Info(std::string loc, std::string msg) { Append(0, "INFO", loc, msg); }
void Logger::Warn(std::string loc, std::string msg) { Append(1, "WARN", loc, msg); }
void Logger::Err(std::string loc, std::string msg)  { Append(2, "ERR",  loc, msg); }
void Logger::Dbg(std::string loc, std::string msg)  { Append(3, "DBG",  loc, msg); }

Logger& GetLogger() {
	static Logger instance;
	return instance;
}
