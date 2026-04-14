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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.SnackbarDuration
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.dewwta.dewsrocompanion.ui.components.ErrorBanner
import com.dewwta.dewsrocompanion.ui.components.GoldDivider
import com.dewwta.dewsrocompanion.ui.components.SroButton
import com.dewwta.dewsrocompanion.ui.components.SroDangerButton
import com.dewwta.dewsrocompanion.ui.components.SroSectionHeader
import com.dewwta.dewsrocompanion.ui.components.SroTextField
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.BgRaised
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.BorderGold
import com.dewwta.dewsrocompanion.ui.theme.BorderMid
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.TextBase
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.AuthState
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel

@Composable
fun ProfileScreen(
    authViewModel: AuthViewModel,
    onLogout: () -> Unit,
) {
    val me        by authViewModel.me.collectAsStateWithLifecycle()
    val username  by authViewModel.username.collectAsStateWithLifecycle()
    val silk      by authViewModel.silk.collectAsStateWithLifecycle()
    val authority by authViewModel.authority.collectAsStateWithLifecycle()
    val profileState by authViewModel.profileState.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }

    // Pre-fill editable fields from live /me data
    var nickname by remember(me) { mutableStateOf(me?.nickname ?: "") }
    var email    by remember(me) { mutableStateOf(me?.email    ?: "") }

    LaunchedEffect(Unit) {
        authViewModel.loadMe()
        authViewModel.loadSilk()
    }

    LaunchedEffect(profileState) {
        when (profileState) {
            is AuthState.Success -> {
                snackbarHostState.showSnackbar(
                    message  = "Profile updated.",
                    duration = SnackbarDuration.Short,
                )
                authViewModel.resetProfileState()
            }
            is AuthState.Error -> {
                snackbarHostState.showSnackbar(
                    message  = (profileState as AuthState.Error).message,
                    duration = SnackbarDuration.Short,
                )
                authViewModel.resetProfileState()
            }
            else -> {}
        }
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(BgBase),
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp),
        ) {
            Spacer(Modifier.height(28.dp))

            // ── Avatar row ─────────────────────────────────────────────────
            Row(verticalAlignment = Alignment.CenterVertically) {
                // Placeholder avatar circle with initials
                Box(
                    contentAlignment = Alignment.Center,
                    modifier = Modifier
                        .size(56.dp)
                        .background(BgRaised, CircleShape)
                        .border(2.dp, BorderGold, CircleShape),
                ) {
                    Text(
                        text = (username ?: "?").first().uppercaseChar().toString(),
                        fontSize = 22.sp,
                        fontWeight = FontWeight.Bold,
                        fontFamily = FontFamily.Serif,
                        color = GoldBright,
                    )
                }
                Spacer(Modifier.width(16.dp))
                Column {
                    Text(
                        text = username ?: "—",
                        fontSize = 18.sp,
                        fontWeight = FontWeight.SemiBold,
                        fontFamily = FontFamily.Serif,
                        color = TextBright,
                        letterSpacing = 0.5.sp,
                    )
                    Text(
                        text = if (authority != 3) "Administrator" else "Member",
                        fontSize = 11.sp,
                        letterSpacing = 0.6.sp,
                        color = if (authority != 3) GoldLight else TextMuted,
                    )
                }
            }

            Spacer(Modifier.height(20.dp))

            // ── Silk info card ─────────────────────────────────────────────
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier
                    .fillMaxWidth()
                    .background(BgSurface, RoundedCornerShape(4.dp))
                    .border(1.dp, BorderMid, RoundedCornerShape(4.dp))
                    .padding(horizontal = 16.dp, vertical = 12.dp),
            ) {
                Text("♦", fontSize = 18.sp, color = GoldBright)
                Spacer(Modifier.width(10.dp))
                Column {
                    Text(
                        text = "SILK BALANCE",
                        fontSize = 10.sp,
                        letterSpacing = 1.2.sp,
                        color = TextMuted,
                    )
                    Text(
                        text = if (silk != null) "%,d".format(silk) else "—",
                        fontSize = 20.sp,
                        fontWeight = FontWeight.SemiBold,
                        color = GoldBright,
                    )
                }
            }

            Spacer(Modifier.height(24.dp))
            GoldDivider()
            Spacer(Modifier.height(20.dp))

            // ── Edit profile ───────────────────────────────────────────────
            SroSectionHeader("Edit Profile")
            Spacer(Modifier.height(12.dp))

            SroTextField(
                value = nickname,
                onValueChange = { nickname = it },
                label = "Nickname",
                enabled = profileState !is AuthState.Loading,
            )
            Spacer(Modifier.height(12.dp))
            SroTextField(
                value = email,
                onValueChange = { email = it },
                label = "Email",
                enabled = profileState !is AuthState.Loading,
            )

            if (profileState is AuthState.Error) {
                Spacer(Modifier.height(12.dp))
                ErrorBanner((profileState as AuthState.Error).message)
            }

            Spacer(Modifier.height(16.dp))
            SroButton(
                text = "Save Changes",
                onClick = { authViewModel.updateProfile(nickname, email, me?.sex) },
                loading = profileState is AuthState.Loading,
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(Modifier.height(28.dp))
            HorizontalDivider(color = BorderMid)
            Spacer(Modifier.height(20.dp))

            // ── Danger zone ────────────────────────────────────────────────
            SroSectionHeader("Account")
            Spacer(Modifier.height(12.dp))

            InfoRow("Username", username ?: "—")
            Spacer(Modifier.height(6.dp))
            InfoRow("Role", if (authority != 3) "Administrator" else "Member")

            Spacer(Modifier.height(20.dp))

            SroDangerButton(
                text = "Sign Out",
                onClick = {
                    authViewModel.logout()
                    onLogout()
                },
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(Modifier.height(40.dp))
        }

        // Snackbar anchored at bottom
        SnackbarHost(
            hostState = snackbarHostState,
            modifier  = Modifier.align(Alignment.BottomCenter),
        )
    }
}

@Composable
private fun InfoRow(label: String, value: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 2.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text     = label,
            fontSize = 12.sp,
            color    = TextMuted,
            letterSpacing = 0.4.sp,
            modifier = Modifier.width(90.dp),
        )
        Text(
            text  = value,
            fontSize = 13.sp,
            color = TextBase,
            fontWeight = FontWeight.Medium,
        )
    }
}
