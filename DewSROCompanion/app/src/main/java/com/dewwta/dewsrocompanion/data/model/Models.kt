package com.dewwta.dewsrocompanion.data.model

data class LoginRequest(
    val username: String,
    val password: String,
)

data class UserDto(
    val jid: Int,
    val username: String?,
    val nickname: String?,
    val email: String?,
    val sex: String?,
    val authority: Int,
)

data class LoginResponse(
    val jwt: String,
    val user: UserDto,
)

data class CharacterSnapshotItem(
    val itemID: Int,
    val codeName: String,
    val stack: Int,
    val maxStack: Int,
)

data class CharacterSnapshot(
    val characterName: String,
    val characterID: Long,
    val jid: Int,
    val savedAt: String,
    val level: Int,
    val currentHP: Long,
    val currentMP: Long,
    val zerkLevel: Int,
    val unusedStatPoints: Int,
    val gold: Long,
    val skillPoints: Long,
    val equipment: Map<String, CharacterSnapshotItem>,
    val slots: Map<String, CharacterSnapshotItem>,
    val pets: Map<String, Map<String, CharacterSnapshotItem>>,
)

data class CharacterWithState(
    val login: String,
    val charName: String,
    val silkOwn: Int,
    val silkGift: Int,
    val jid: Int,
    val charID: Long,
    val isOnline: Boolean,
    val lastKnownState: CharacterSnapshot?,
)

data class UpdateProfileRequest(
    val nickname: String?,
    val email: String?,
    val sex: String?,
)

data class SilkResponse(
    val silk: Int,
)

data class MessageResponse(
    val message: String,
)
