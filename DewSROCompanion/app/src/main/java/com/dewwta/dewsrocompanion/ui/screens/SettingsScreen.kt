package com.dewwta.dewsrocompanion.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.dewwta.dewsrocompanion.data.network.RetrofitClient
import com.dewwta.dewsrocompanion.ui.components.GoldDivider
import com.dewwta.dewsrocompanion.ui.components.SroButton
import com.dewwta.dewsrocompanion.ui.components.SroSectionHeader
import com.dewwta.dewsrocompanion.ui.components.SroTextField
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.BorderMid
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.GreenBright
import com.dewwta.dewsrocompanion.ui.theme.TextBase
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel

@Composable
fun SettingsScreen(authViewModel: AuthViewModel) {
    val storedUrl by authViewModel.serverUrl.collectAsStateWithLifecycle()
    val focusManager = LocalFocusManager.current

    // Local edit field — pre-filled from the stored value (or default)
    var urlInput by remember(storedUrl) {
        mutableStateOf(storedUrl ?: RetrofitClient.DEFAULT_BASE_URL)
    }
    var saved by remember { mutableStateOf(false) }

    // Reset "saved" tick after 2 seconds
    LaunchedEffect(saved) {
        if (saved) {
            kotlinx.coroutines.delay(2000)
            saved = false
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BgBase)
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp),
    ) {
        Spacer(Modifier.height(28.dp))

        Text(
            text = "SETTINGS",
            fontSize = 13.sp,
            fontWeight = FontWeight.SemiBold,
            fontFamily = FontFamily.Serif,
            letterSpacing = 2.sp,
            color = GoldLight,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = "App configuration",
            fontSize = 12.sp,
            color = TextMuted,
            letterSpacing = 0.3.sp,
        )

        Spacer(Modifier.height(20.dp))
        GoldDivider()
        Spacer(Modifier.height(20.dp))

        // ── Server connection ───────────────────────────────────────────────
        SroSectionHeader("Server Connection")
        Spacer(Modifier.height(12.dp))

        // Info card
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(BgSurface, RoundedCornerShape(4.dp))
                .border(1.dp, BorderMid, RoundedCornerShape(4.dp))
                .padding(horizontal = 14.dp, vertical = 10.dp),
        ) {
            Text(
                text = "Enter the full HTTPS base URL of your VSRO Control API server.",
                fontSize = 12.sp,
                color = TextBase,
                lineHeight = 18.sp,
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = "Example: https://192.168.1.10:5086/",
                fontSize = 11.sp,
                color = TextMuted,
                fontFamily = FontFamily.Monospace,
            )
        }

        Spacer(Modifier.height(14.dp))

        SroTextField(
            value = urlInput,
            onValueChange = {
                urlInput = it
                saved = false
            },
            label = "Server URL",
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Uri,
                imeAction    = ImeAction.Done,
            ),
            keyboardActions = KeyboardActions(
                onDone = {
                    focusManager.clearFocus()
                    if (urlInput.isNotBlank()) {
                        authViewModel.saveServerUrl(urlInput.trim())
                        saved = true
                    }
                }
            ),
        )

        Spacer(Modifier.height(14.dp))

        Row(verticalAlignment = Alignment.CenterVertically) {
            SroButton(
                text = "Save URL",
                onClick = {
                    focusManager.clearFocus()
                    if (urlInput.isNotBlank()) {
                        authViewModel.saveServerUrl(urlInput.trim())
                        saved = true
                    }
                },
                modifier = Modifier.weight(1f),
            )

            if (saved) {
                Spacer(Modifier.width(12.dp))
                Icon(
                    imageVector = Icons.Filled.CheckCircle,
                    contentDescription = "Saved",
                    tint = GreenBright,
                    modifier = Modifier.size(22.dp),
                )
            }
        }

        Spacer(Modifier.height(20.dp))

        // Show what's currently active in Retrofit
        val activeUrl = RetrofitClient.currentBaseUrl()
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(BgSurface, RoundedCornerShape(4.dp))
                .border(1.dp, BorderMid, RoundedCornerShape(4.dp))
                .padding(horizontal = 14.dp, vertical = 10.dp),
        ) {
            Text(
                text = "ACTIVE URL",
                fontSize = 9.sp,
                letterSpacing = 1.2.sp,
                color = TextMuted,
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = activeUrl,
                fontSize = 12.sp,
                fontFamily = FontFamily.Monospace,
                color = GoldBright,
            )
        }

        Spacer(Modifier.height(32.dp))
    }
}
