#include "Logger.h"

void Logger::Alloc() {
	if (m_isAlloced) return;
	AllocConsole();

	HANDLE hIn = GetStdHandle(STD_INPUT_HANDLE);
	DWORD mode;
	GetConsoleMode(hIn, &mode);
	mode &= ~ENABLE_QUICK_EDIT_MODE;
	mode &= ~ENABLE_INSERT_MODE;
	SetConsoleMode(hIn, mode);

	m_consoleHwnd = GetConsoleWindow();
	m_consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);

	FILE* f;
	freopen_s(&f, "CONOUT$", "w", stdout);
	freopen_s(&f, "CONOUT$", "w", stderr);
	freopen_s(&f, "CONIN$", "r", stdin);

	m_isAlloced = true;
}

void Logger::ToggleState() {
	if (!m_consoleHwnd) return;
	m_state = !m_state;
	ShowWindow(m_consoleHwnd, m_state ? SW_SHOW : SW_HIDE);
}

void Logger::SetState(bool enabled) {
	if (!m_consoleHwnd) return;
	m_state = enabled;
	ShowWindow(m_consoleHwnd, m_state ? SW_SHOW : SW_HIDE);
}

//Unsure but most my loggers are sync with a lock object, might be needed here because of socket operations logging?
void Logger::Info(std::string loc, std::string msg) {
	std::lock_guard<std::mutex> lock(m_logMutex);

	SetConsoleTextAttribute(m_consoleHandle,
		FOREGROUND_BLUE | FOREGROUND_GREEN | FOREGROUND_INTENSITY);

	std::cout << "[" << loc << "] " << msg << std::endl;

	SetConsoleTextAttribute(m_consoleHandle, DEFAULT_COLOR);
}

void Logger::Warn(std::string loc, std::string msg) {
	std::lock_guard<std::mutex> lock(m_logMutex);

	SetConsoleTextAttribute(m_consoleHandle,
		FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY);

	std::cout << "[" << loc << "] " << msg << std::endl;

	SetConsoleTextAttribute(m_consoleHandle, DEFAULT_COLOR);
}

void Logger::Err(std::string loc, std::string msg) {
	std::lock_guard<std::mutex> lock(m_logMutex);

	SetConsoleTextAttribute(m_consoleHandle,
		FOREGROUND_RED | FOREGROUND_INTENSITY);

	std::cout << "[" << loc << "] " << msg << std::endl;

	SetConsoleTextAttribute(m_consoleHandle, DEFAULT_COLOR);
}

void Logger::Dbg(std::string loc, std::string msg) {
	std::lock_guard<std::mutex> lock(m_logMutex);

	SetConsoleTextAttribute(m_consoleHandle,
		FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);

	std::cout << "[" << loc << "] " << msg << std::endl;

	SetConsoleTextAttribute(m_consoleHandle, DEFAULT_COLOR);
}

Logger& GetLogger() {
	static Logger instance; // constructed once, thread-safe in C++11+
	return instance;
}