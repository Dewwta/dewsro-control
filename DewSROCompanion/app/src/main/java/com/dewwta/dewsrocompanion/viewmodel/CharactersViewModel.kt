package com.dewwta.dewsrocompanion.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.dewwta.dewsrocompanion.data.model.CharacterWithState
import com.dewwta.dewsrocompanion.data.network.RetrofitClient
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed class CharactersState {
    object Idle    : CharactersState()
    object Loading : CharactersState()
    data class Success(val characters: List<CharacterWithState>) : CharactersState()
    data class Error(val message: String) : CharactersState()
}

class CharactersViewModel : ViewModel() {

    private val _state = MutableStateFlow<CharactersState>(CharactersState.Idle)
    val state: StateFlow<CharactersState> = _state.asStateFlow()

    fun load() {
        viewModelScope.launch {
            _state.value = CharactersState.Loading
            try {
                val resp = RetrofitClient.service.getMyCharacters()
                _state.value = if (resp.isSuccessful)
                    CharactersState.Success(resp.body() ?: emptyList())
                else
                    CharactersState.Error("Failed to load characters (${resp.code()})")
            } catch (e: Exception) {
                _state.value = CharactersState.Error("Connection failed: ${e.message}")
            }
        }
    }
}
