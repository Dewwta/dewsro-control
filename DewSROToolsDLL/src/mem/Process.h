#pragma once
#include <Windows.h>
#include <algorithm>
#include <string>

static BOOL WriteProcessBytes(HANDLE hProcess, DWORD destAddress, LPVOID patch, DWORD numBytes)
{
	DWORD oldProtect = 0;
	DWORD bytesRet = 0;
	BOOL status = TRUE;	

	if (!VirtualProtectEx(hProcess, UlongToPtr(destAddress), numBytes, PAGE_EXECUTE_READWRITE, &oldProtect))
		return FALSE;


	if (!WriteProcessMemory(hProcess, UlongToPtr(destAddress), patch, numBytes, &bytesRet))
		status = FALSE;

	if (bytesRet != numBytes)
		status = FALSE;

	
	if (!VirtualProtectEx(hProcess, UlongToPtr(destAddress), numBytes, oldProtect, &oldProtect))
		status = FALSE;


	if (!FlushInstructionCache(hProcess, UlongToPtr(destAddress), numBytes))
		status = FALSE;

	return status;
}


static BOOL ReadProcessBytes(HANDLE hProcess, DWORD destAddress, LPVOID buffer, DWORD numBytes)
{
	DWORD oldProtect = 0;	
	DWORD bytesRet = 0;		
	BOOL status = TRUE;		

	if (!VirtualProtectEx(hProcess, UlongToPtr(destAddress), numBytes, PAGE_READONLY, &oldProtect))
		return FALSE;

	if (!ReadProcessMemory(hProcess, UlongToPtr(destAddress), buffer, numBytes, &bytesRet))
		status = FALSE;

	if (bytesRet != numBytes)
		status = FALSE;

	if (!VirtualProtectEx(hProcess, UlongToPtr(destAddress), numBytes, oldProtect, &oldProtect))
		status = FALSE;

	return status;
}

template<typename T>
static BOOL WriteMemoryValue(DWORD DestAddress, T Value)
{
	BYTE bytes[sizeof(T)];
	std::copy(static_cast<const BYTE*>(static_cast<const void*>(&Value)), static_cast<const BYTE*>(static_cast<const void*>(&Value)) + sizeof(Value), bytes);
	return WriteProcessBytes(GetCurrentProcess(), DestAddress, bytes, sizeof(T));
}


template<typename T>
static BOOL ReadMemoryValue(DWORD DestAddress, T& Value)
{
	static BYTE bytes[sizeof(T)];
	BOOL result = ReadProcessBytes(GetCurrentProcess(), DestAddress, bytes, sizeof(T));
	if (result)
		Value = reinterpret_cast<T&>(bytes);
	return result;
}

static BOOL WriteMemoryString(DWORD DestAddress, const char* Value)
{
	auto valueLength = strlen(Value) + 1;
	char* charPtr = new char[valueLength];
	strcpy_s(charPtr, valueLength, Value);
	BYTE* bytes = reinterpret_cast<BYTE*>(const_cast<char*>(charPtr));
	return WriteProcessBytes(GetCurrentProcess(), DestAddress, bytes, valueLength);
}

static std::string ReadMemoryString(DWORD DestAddress)
{
	uint32_t charPtr;
	if (ReadMemoryValue<uint32_t>(DestAddress, charPtr))
		return std::string((char*)charPtr);
	return std::string();
}