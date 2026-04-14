package com.dewwta.dewsrocompanion.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.dewwta.dewsrocompanion.ui.components.GoldDivider
import com.dewwta.dewsrocompanion.ui.components.SroSectionHeader
import com.dewwta.dewsrocompanion.ui.components.StatCard
import com.dewwta.dewsrocompanion.ui.components.StatCardVariant
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.GreenBright
import com.dewwta.dewsrocompanion.ui.theme.SteelBright
import com.dewwta.dewsrocompanion.ui.theme.TextBase
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.width
import androidx.compose.ui.Alignment
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.res.painterResource
import androidx.compose.foundation.layout.size
import com.dewwta.dewsrocompanion.R
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.res.imageResource

@Composable
fun DashboardScreen(authViewModel: AuthViewModel) {
    val username  by authViewModel.username.collectAsStateWithLifecycle()
    val nickname  by authViewModel.nickname.collectAsStateWithLifecycle()
    val silk      by authViewModel.silk.collectAsStateWithLifecycle()
    val authority by authViewModel.authority.collectAsStateWithLifecycle()

    LaunchedEffect(Unit) {
        authViewModel.loadSilk()
        authViewModel.loadMe()
    }

    val displayName = nickname?.takeIf { it.isNotBlank() } ?: username ?: "Adventurer"
    val isAdmin = authority != 3

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BgBase)
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 20.dp),
    ) {
        Spacer(Modifier.height(28.dp))

        // ── Greeting ───────────────────────────────────────────────────────
        Text(
            text = "Welcome back,",
            fontSize = 12.sp,
            letterSpacing = 0.8.sp,
            color = TextMuted,
        )
        Text(
            text = displayName,
            fontSize = 26.sp,
            fontWeight = FontWeight.Bold,
            fontFamily = FontFamily.Serif,
            letterSpacing = 1.sp,
            color = GoldBright,
        )
        if (isAdmin) {
            Text(
                text = "▲ ADMINISTRATOR",
                fontSize = 9.sp,
                letterSpacing = 1.6.sp,
                fontWeight = FontWeight.SemiBold,
                color = GoldBright.copy(alpha = 0.7f),
            )
        }

        Spacer(Modifier.height(20.dp))
        GoldDivider()
        Spacer(Modifier.height(20.dp))

        // ── Stat cards ─────────────────────────────────────────────────────
        SroSectionHeader("Account Overview")
        Spacer(Modifier.height(10.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            StatCard(
                label    = "Silk",
                value    = if (silk != null) "%,d".format(silk) else "—",
                variant  = StatCardVariant.Gold,
                modifier = Modifier.weight(1f),
            )
            StatCard(
                label    = "Role",
                value    = if (isAdmin) "Admin" else "User",
                variant  = if (isAdmin) StatCardVariant.Steel else StatCardVariant.Green,
                modifier = Modifier.weight(1f),
            )
        }

        Spacer(Modifier.height(28.dp))
        GoldDivider()
        Spacer(Modifier.height(20.dp))

        // ── Quick action tiles ─────────────────────────────────────────────
        SroSectionHeader("Quick Actions")
        Spacer(Modifier.height(10.dp))

        ControlTile(
            "My Characters",
            GoldBright
        ) {
            Text(
                text = "View stats, inventory & status",
                fontSize = 12.sp,
                color = TextBase,
                letterSpacing = 0.2.sp,
            )
        }
        Spacer(Modifier.height(10.dp))
        ControlTile(
            "Silk Balance",
            GoldLight
        ) {
            if (silk != null) {
                Row(verticalAlignment = Alignment.CenterVertically) {

                    val silkBitmap = ImageBitmap.imageResource(R.drawable.silk)
                    Image(
                        bitmap = silkBitmap,
                        contentDescription = "Silk",
                        modifier = Modifier.size(14.dp),
                        filterQuality = FilterQuality.None
                    )

                    Spacer(Modifier.width(4.dp))

                    Text(
                        text = "%,d available".format(silk),
                        fontSize = 12.sp,
                        color = TextBase,
                        letterSpacing = 0.2.sp,
                    )
                }
            } else {
                Text(
                    text = "Loading…",
                    fontSize = 12.sp,
                    color = TextBase,
                )
            }
        }
        Spacer(Modifier.height(10.dp))
        ControlTile(
            "Server Status",
            GreenBright
        ) {
            Text(
                text = "Proxy online — players connected",
                fontSize = 12.sp,
                color = TextBase,
                letterSpacing = 0.2.sp,
            )
        }
        if (isAdmin) {
            Spacer(Modifier.height(10.dp))

            ControlTile(
                "Admin Panel",
                SteelBright
            ) {
                Text(
                    text = "Live sessions & server controls",
                    fontSize = 12.sp,
                    color = TextBase,
                    letterSpacing = 0.2.sp,
                )
            }
        }

        Spacer(Modifier.height(32.dp))
    }
}

@Composable
private fun ControlTile(
    title: String,
    accent: Color,
    subtitle: @Composable () -> Unit
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(BgSurface, RoundedCornerShape(4.dp))
            .drawBehind {
                drawLine(
                    color = accent,
                    start = Offset(0f, 0f),
                    end = Offset(0f, size.height),
                    strokeWidth = 6f,
                )
            }
            .padding(horizontal = 16.dp, vertical = 12.dp),
    ) {
        Text(
            text = title,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold,
            fontFamily = FontFamily.Serif,
            letterSpacing = 0.5.sp,
            color = TextBright,
        )

        Spacer(Modifier.height(2.dp))

        subtitle()
    }
}
