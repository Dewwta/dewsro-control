package com.dewwta.dewsrocompanion.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.dewwta.dewsrocompanion.data.TokenStore
import com.dewwta.dewsrocompanion.data.model.LoginRequest
import com.dewwta.dewsrocompanion.data.model.UpdateProfileRequest
import com.dewwta.dewsrocompanion.data.model.UserDto
import com.dewwta.dewsrocompanion.data.network.RetrofitClient
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

sealed class AuthState {
    object Idle    : AuthState()
    object Loading : AuthState()
    object Success : AuthState()
    data class Error(val message: String) : AuthState()
}

class AuthViewModel(app: Application) : AndroidViewModel(app) {

    private val ctx get() = getApplication<Application>()

    val token: StateFlow<String?> = TokenStore.tokenFlow(ctx)
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    val username: StateFlow<String?> = TokenStore.usernameFlow(ctx)
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    val nickname: StateFlow<String?> = TokenStore.nicknameFlow(ctx)
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    val authority: StateFlow<Int> = TokenStore.authorityFlow(ctx)
        .stateIn(viewModelScope, SharingStarted.Eagerly, 3)

    // Persisted server URL — starts null until DataStore emits
    val serverUrl: StateFlow<String?> = TokenStore.serverUrlFlow(ctx)
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    private val _loginState = MutableStateFlow<AuthState>(AuthState.Idle)
    val loginState: StateFlow<AuthState> = _loginState.asStateFlow()

    private val _silk = MutableStateFlow<Int?>(null)
    val silk: StateFlow<Int?> = _silk.asStateFlow()

    private val _profileState = MutableStateFlow<AuthState>(AuthState.Idle)
    val profileState: StateFlow<AuthState> = _profileState.asStateFlow()

    private val _me = MutableStateFlow<UserDto?>(null)
    val me: StateFlow<UserDto?> = _me.asStateFlow()

    // ── URL management ────────────────────────────────────────────────────────

    /** Apply the stored URL into RetrofitClient on cold start. */
    fun restoreUrl(url: String) = RetrofitClient.setBaseUrl(url)

    /** Persist a new URL and immediately apply it. */
    fun saveServerUrl(url: String) {
        viewModelScope.launch {
            TokenStore.saveServerUrl(ctx, url)
            RetrofitClient.setBaseUrl(url)
        }
    }

    // ── Auth ──────────────────────────────────────────────────────────────────

    fun restoreToken(token: String) = RetrofitClient.setToken(token)

    fun login(username: String, password: String) {
        if (username.isBlank() || password.isBlank()) {
            _loginState.value = AuthState.Error("Username and password are required")
            return
        }
        viewModelScope.launch {
            _loginState.value = AuthState.Loading
            try {
                val resp = RetrofitClient.service.login(LoginRequest(username.trim(), password))
                if (resp.isSuccessful) {
                    val body = resp.body()!!
                    RetrofitClient.setToken(body.jwt)
                    TokenStore.save(
                        ctx,
                        token     = body.jwt,
                        username  = body.user.username ?: username,
                        nickname  = body.user.nickname,
                        jid       = body.user.jid,
                        authority = body.user.authority,
                    )
                    _loginState.value = AuthState.Success
                } else {
                    _loginState.value = AuthState.Error("Invalid username or password")
                }
            } catch (e: Exception) {
                _loginState.value = AuthState.Error("Connection failed: ${e.message}")
            }
        }
    }

    fun logout() {
        viewModelScope.launch {
            RetrofitClient.setToken(null)
            TokenStore.clearAuth(ctx)
            _loginState.value = AuthState.Idle
            _silk.value       = null
            _me.value         = null
        }
    }

    // ── Remote data ───────────────────────────────────────────────────────────

    fun loadMe() {
        viewModelScope.launch {
            try {
                val resp = RetrofitClient.service.getMe()
                if (resp.isSuccessful) _me.value = resp.body()
            } catch (_: Exception) {}
        }
    }

    fun loadSilk() {
        viewModelScope.launch {
            try {
                val resp = RetrofitClient.service.getSilk()
                if (resp.isSuccessful) _silk.value = resp.body()?.silk
            } catch (_: Exception) {}
        }
    }

    fun updateProfile(nickname: String?, email: String?, sex: String?) {
        viewModelScope.launch {
            _profileState.value = AuthState.Loading
            try {
                val resp = RetrofitClient.service.updateProfile(
                    UpdateProfileRequest(
                        nickname = nickname?.takeIf { it.isNotBlank() },
                        email    = email?.takeIf { it.isNotBlank() },
                        sex      = sex?.takeIf { it.isNotBlank() },
                    )
                )
                if (resp.isSuccessful) {
                    _profileState.value = AuthState.Success
                    loadMe()
                } else {
                    _profileState.value = AuthState.Error("Update failed")
                }
            } catch (e: Exception) {
                _profileState.value = AuthState.Error("Connection failed: ${e.message}")
            }
        }
    }

    fun resetProfileState() { _profileState.value = AuthState.Idle }
}
