package com.dewwta.dewsrocompanion.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusDirection
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.dewwta.dewsrocompanion.ui.components.ErrorBanner
import com.dewwta.dewsrocompanion.ui.components.GoldDivider
import com.dewwta.dewsrocompanion.ui.components.SroButton
import com.dewwta.dewsrocompanion.ui.components.SroTextField
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.AuthState
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel

@Composable
fun LoginScreen(
    authViewModel: AuthViewModel,
    onLoginSuccess: () -> Unit,
    onSettingsClick: () -> Unit = {},
) {
    val loginState by authViewModel.loginState.collectAsStateWithLifecycle()
    val serverUrl  by authViewModel.serverUrl.collectAsStateWithLifecycle()

    var username     by remember { mutableStateOf("") }
    var password     by remember { mutableStateOf("") }
    var showPassword by remember { mutableStateOf(false) }
    val focusManager = LocalFocusManager.current

    LaunchedEffect(loginState) {
        if (loginState is AuthState.Success) onLoginSuccess()
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(BgBase)
            .imePadding(),
    ) {
        // ── Settings gear — top-right ─────────────────────────────────────
        IconButton(
            onClick = onSettingsClick,
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(8.dp),
        ) {
            Icon(
                imageVector = Icons.Filled.Settings,
                contentDescription = "Settings",
                tint = TextMuted,
                modifier = Modifier.size(22.dp),
            )
        }

        Column(
            modifier = Modifier
                .fillMaxWidth(0.88f)
                .align(Alignment.Center)
                .verticalScroll(rememberScrollState()),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Spacer(Modifier.height(48.dp))

            // ── Logo ───────────────────────────────────────────────────────
            Text("⚔", fontSize = 42.sp, color = GoldBright, textAlign = TextAlign.Center)
            Spacer(Modifier.height(12.dp))
            Text(
                text = "SRO COMPANION",
                fontSize = 22.sp,
                fontWeight = FontWeight.Bold,
                fontFamily = FontFamily.Serif,
                letterSpacing = 3.sp,
                color = GoldBright,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = "Server Control Interface",
                fontSize = 11.sp,
                letterSpacing = 1.4.sp,
                color = TextMuted,
                textAlign = TextAlign.Center,
            )

            // Show active server URL as a subtle hint
            if (!serverUrl.isNullOrBlank()) {
                Spacer(Modifier.height(6.dp))
                Text(
                    text = serverUrl!!,
                    fontSize = 10.sp,
                    fontFamily = FontFamily.Monospace,
                    color = TextMuted.copy(alpha = 0.6f),
                    textAlign = TextAlign.Center,
                )
            }

            Spacer(Modifier.height(32.dp))
            GoldDivider()
            Spacer(Modifier.height(28.dp))

            // ── Fields ─────────────────────────────────────────────────────
            SroTextField(
                value = username,
                onValueChange = { username = it },
                label = "Username",
                enabled = loginState !is AuthState.Loading,
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Text,
                    imeAction    = ImeAction.Next,
                ),
                keyboardActions = KeyboardActions(
                    onNext = { focusManager.moveFocus(FocusDirection.Down) }
                ),
            )
            Spacer(Modifier.height(14.dp))

            SroTextField(
                value = password,
                onValueChange = { password = it },
                label = "Password",
                enabled = loginState !is AuthState.Loading,
                visualTransformation = if (showPassword) VisualTransformation.None
                    else PasswordVisualTransformation(),
                trailingIcon = {
                    IconButton(onClick = { showPassword = !showPassword }) {
                        Icon(
                            imageVector = if (showPassword) Icons.Filled.VisibilityOff
                                else Icons.Filled.Visibility,
                            contentDescription = if (showPassword) "Hide" else "Show",
                            tint = TextMuted,
                            modifier = Modifier.size(20.dp),
                        )
                    }
                },
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Password,
                    imeAction    = ImeAction.Done,
                ),
                keyboardActions = KeyboardActions(
                    onDone = {
                        focusManager.clearFocus()
                        authViewModel.login(username, password)
                    }
                ),
            )

            if (loginState is AuthState.Error) {
                Spacer(Modifier.height(16.dp))
                ErrorBanner((loginState as AuthState.Error).message)
            }

            Spacer(Modifier.height(22.dp))

            SroButton(
                text    = "Sign In",
                onClick = {
                    focusManager.clearFocus()
                    authViewModel.login(username, password)
                },
                loading  = loginState is AuthState.Loading,
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(Modifier.height(24.dp))

            Text(
                text = "Access restricted to registered members.",
                fontSize = 11.sp,
                color = TextMuted,
                letterSpacing = 0.3.sp,
                textAlign = TextAlign.Center,
            )

            Spacer(Modifier.height(48.dp))
        }
    }
}
