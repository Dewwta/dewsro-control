#pragma once

#include "../libs/ClientLib/src/ICPlayer.h"

class PlayerInterface
{
public:
	std::n_wstring GetCharacterName() const;
	int GetCurrentLevel() const;
	short GetSTR() const;
	short GetINT() const;
	short GetStatPoints() const;

	// Jobs
	std::n_wstring GetJobAliasName() const;
	int GetCurrentJobEXP() const;
	
	// Admin (Game Master)
	bool IsGM() const;
};