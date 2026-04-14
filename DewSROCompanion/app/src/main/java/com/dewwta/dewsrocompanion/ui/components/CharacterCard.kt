package com.dewwta.dewsrocompanion.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.dewwta.dewsrocompanion.data.model.CharacterWithState
import com.dewwta.dewsrocompanion.ui.theme.BgRaised
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.BorderDark
import com.dewwta.dewsrocompanion.ui.theme.BorderGold
import com.dewwta.dewsrocompanion.ui.theme.GoldBase
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.GreenBright
import com.dewwta.dewsrocompanion.ui.theme.TextBase
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted

private val CardShape = RoundedCornerShape(4.dp)

@Composable
fun CharacterCard(character: CharacterWithState, modifier: Modifier = Modifier) {
    var expanded by remember { mutableStateOf(false) }

    val onlineColor  = GreenBright
    val offlineColor = Color(0xFFB03030)
    val statusColor  = if (character.isOnline) onlineColor else offlineColor
    val leftBarColor = if (character.isOnline) onlineColor else Color(0xFF5C1010)

    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(CardShape)
            .background(BgSurface)
            .border(1.dp, BorderDark, CardShape)
            .drawBehind {
                // Left status bar — same as Svelte module cards
                drawLine(
                    color = leftBarColor,
                    start = Offset(0f, 0f),
                    end   = Offset(0f, size.height),
                    strokeWidth = 6f,
                )
            }
            .clickable { expanded = !expanded },
    ) {
        // ── Header row ───────────────────────────────────────────────────────
        Row(
            verticalAlignment = Alignment.CenterVertically,
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
        ) {
            // Online dot
            Box(
                modifier = Modifier
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(statusColor),
            )
            Spacer(Modifier.width(10.dp))

            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = character.charName,
                    fontSize = 16.sp,
                    fontWeight = FontWeight.SemiBold,
                    fontFamily = FontFamily.Serif,
                    color = TextBright,
                    letterSpacing = 0.6.sp,
                )
                Text(
                    text = if (character.isOnline) "ONLINE" else "OFFLINE",
                    fontSize = 9.sp,
                    fontWeight = FontWeight.Medium,
                    letterSpacing = 1.2.sp,
                    color = statusColor,
                )
            }

            // Silk chip
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier
                    .clip(RoundedCornerShape(2.dp))
                    .background(BgRaised)
                    .border(1.dp, BorderGold, RoundedCornerShape(2.dp))
                    .padding(horizontal = 8.dp, vertical = 4.dp),
            ) {
                Text(
                    text = "Lvl.",
                    fontSize = 10.sp,
                    color = GoldBase,
                )
                Spacer(Modifier.width(4.dp))
                if (character.lastKnownState?.level != null) {

                }
                Text(
                    text = character.lastKnownState?.level?.let { "%,d".format(it) } ?: "N/A",
                    fontSize = 12.sp,
                    fontWeight = FontWeight.SemiBold,
                    color = GoldLight,
                )
            }

            Spacer(Modifier.width(8.dp))
            Icon(
                imageVector = if (expanded) Icons.Filled.ExpandLess else Icons.Filled.ExpandMore,
                contentDescription = if (expanded) "Collapse" else "Expand",
                tint = TextMuted,
                modifier = Modifier.size(20.dp),
            )
        }

        // ── Expandable stats section ──────────────────────────────────────────
        AnimatedVisibility(
            visible = expanded,
            enter   = expandVertically(),
            exit    = shrinkVertically(),
        ) {
            Column {
                HorizontalDivider(color = BorderDark)

                val snap = character.lastKnownState
                if (snap != null) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(BgRaised)
                            .padding(horizontal = 16.dp, vertical = 12.dp),
                    ) {
                        Text(
                            text = "LAST KNOWN STATE",
                            fontSize = 9.sp,
                            fontWeight = FontWeight.Medium,
                            letterSpacing = 1.4.sp,
                            color = TextMuted,
                        )
                        Spacer(Modifier.height(10.dp))

                        // Stats grid  (2 columns)
                        val stats = listOf(
                            "LEVEL"   to snap.level.toString(),
                            "HP"      to "%,d".format(snap.currentHP),
                            "MP"      to "%,d".format(snap.currentMP),
                            "GOLD"    to "%,d".format(snap.gold),
                            "SP"      to "%,d".format(snap.skillPoints),
                            "STAT PTS" to snap.unusedStatPoints.toString(),
                        )

                        for (i in stats.indices step 2) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(12.dp),
                            ) {
                                SnapStatRow(stats[i].first, stats[i].second, Modifier.weight(1f))
                                if (i + 1 < stats.size)
                                    SnapStatRow(stats[i + 1].first, stats[i + 1].second, Modifier.weight(1f))
                            }
                            Spacer(Modifier.height(6.dp))
                        }

                        Spacer(Modifier.height(4.dp))
                        Text(
                            text = "Saved: ${snap.savedAt.take(19).replace("T", " ")} UTC",
                            fontSize = 10.sp,
                            color = TextMuted,
                            letterSpacing = 0.3.sp,
                        )
                    }
                } else {
                    Box(
                        contentAlignment = Alignment.Center,
                        modifier = Modifier
                            .fillMaxWidth()
                            .background(BgRaised)
                            .padding(20.dp),
                    ) {
                        Text(
                            text = "No snapshot data yet.",
                            color = TextMuted,
                            fontSize = 13.sp,
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SnapStatRow(label: String, value: String, modifier: Modifier = Modifier) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = modifier,
    ) {
        Text(
            text = label,
            fontSize = 10.sp,
            letterSpacing = 0.6.sp,
            color = TextMuted,
            modifier = Modifier.width(60.dp),
        )
        Text(
            text = value,
            fontSize = 13.sp,
            fontWeight = FontWeight.SemiBold,
            color = GoldBright,
        )
    }
}
