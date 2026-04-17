#include "patcher.h"
#include "Logging/Logger.h"
#pragma warning(disable:4996)
#include <Windows.h>
#include "mem/Process.h"
#pragma warning(disable:4244)

void Patcher::PatchAll() {
	auto& log = GetLogger();

	uint8_t maxLevel = 120;
	WriteMemoryValue<uint8_t>(0x008A99A2 + 2, maxLevel); // Level up limit
	WriteMemoryValue<uint8_t>(0x0069C7C8 + 1, maxLevel); // Mastery limit
	WriteMemoryValue<uint8_t>(0x0073AFAE + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x0073B013 + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x0073B030 + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x0073FA4C + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x0073FAAF + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x0073FACC + 1, maxLevel); // Party Match
	WriteMemoryValue<uint8_t>(0x009448B1 + 6, maxLevel); // Skill limit
	log.Info("Patcher::PatchAll", "Patched max level");

	uint32_t masteries_CH = 550;
	WriteMemoryValue<uint32_t>(0x006A51BC + 1, masteries_CH);
	WriteMemoryValue<uint32_t>(0x006AA4C3 + 1, masteries_CH);
	log.Info("Patcher::PatchAll", "Patched max CH mastery level");

	uint32_t masteries_EU = 220;
	WriteMemoryValue<uint32_t>(0x006A5197 + 1, masteries_EU);
	WriteMemoryValue<uint32_t>(0x006A51A2 + 1, masteries_EU);
	WriteMemoryValue<uint32_t>(0x006AA498 + 1, masteries_EU);
	WriteMemoryValue<uint32_t>(0x006AA4A3 + 1, masteries_EU);
	log.Info("Patcher::PatchAll", "Patched max EU mastery level");

	WriteMemoryValue<uint8_t>(0x0077B4F3 + 2, 0);
	log.Info("Patcher::PatchAll", "Patched zoom limit check");

	uint8_t level = 20;
	WriteMemoryValue<uint8_t>(0x00645688 + 1, level); // Show message
	WriteMemoryValue<uint8_t>(0x00797E21 + 1, level); // Unlock action
	log.Info("Patcher::PatchAll", "Patched resurrection max level");

	// Background sight range
	WriteMemoryValue<float>(0x00DE4C5C, 2500.0f); // 0
	WriteMemoryValue<float>(0x00DE34C0, 3500.0f); // 1
	WriteMemoryValue<float>(0x00DE4C58, 4500.0f); // 2
	WriteMemoryValue<float>(0x00DE4C54, 5500.0f); // 3
	WriteMemoryValue<float>(0x00DE4C50, 6500.0f); // 4
	log.Info("Patcher::PatchAll", "Patched background sight range");

}