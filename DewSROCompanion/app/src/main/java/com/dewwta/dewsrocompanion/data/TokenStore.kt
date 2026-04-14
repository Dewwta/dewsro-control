package com.dewwta.dewsrocompanion.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "sro_prefs")

object TokenStore {

    private val KEY_TOKEN      = stringPreferencesKey("jwt_token")
    private val KEY_USERNAME   = stringPreferencesKey("username")
    private val KEY_NICKNAME   = stringPreferencesKey("nickname")
    private val KEY_JID        = stringPreferencesKey("jid")
    private val KEY_AUTHORITY  = stringPreferencesKey("authority")
    private val KEY_SERVER_URL = stringPreferencesKey("server_url")

    fun tokenFlow(context: Context): Flow<String?> =
        context.dataStore.data.map { it[KEY_TOKEN] }

    fun usernameFlow(context: Context): Flow<String?> =
        context.dataStore.data.map { it[KEY_USERNAME] }

    fun nicknameFlow(context: Context): Flow<String?> =
        context.dataStore.data.map { it[KEY_NICKNAME]?.takeIf { v -> v.isNotBlank() } }

    fun authorityFlow(context: Context): Flow<Int> =
        context.dataStore.data.map { it[KEY_AUTHORITY]?.toIntOrNull() ?: 3 }

    fun serverUrlFlow(context: Context): Flow<String?> =
        context.dataStore.data.map { it[KEY_SERVER_URL]?.takeIf { v -> v.isNotBlank() } }

    suspend fun save(
        context: Context,
        token: String,
        username: String,
        nickname: String?,
        jid: Int,
        authority: Int,
    ) {
        context.dataStore.edit { prefs ->
            prefs[KEY_TOKEN]     = token
            prefs[KEY_USERNAME]  = username
            prefs[KEY_NICKNAME]  = nickname ?: ""
            prefs[KEY_JID]       = jid.toString()
            prefs[KEY_AUTHORITY] = authority.toString()
        }
    }

    suspend fun saveServerUrl(context: Context, url: String) {
        context.dataStore.edit { prefs ->
            prefs[KEY_SERVER_URL] = url.trimEnd('/')
        }
    }

    /** Clears auth data but preserves the server URL setting. */
    suspend fun clearAuth(context: Context) {
        context.dataStore.edit { prefs ->
            prefs.remove(KEY_TOKEN)
            prefs.remove(KEY_USERNAME)
            prefs.remove(KEY_NICKNAME)
            prefs.remove(KEY_JID)
            prefs.remove(KEY_AUTHORITY)
        }
    }
}
