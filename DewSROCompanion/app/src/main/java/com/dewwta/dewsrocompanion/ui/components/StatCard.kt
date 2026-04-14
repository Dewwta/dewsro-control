package com.dewwta.dewsrocompanion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.BorderDark
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted

enum class StatCardVariant { Gold, Green, Red, Steel }

@Composable
fun StatCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier,
    variant: StatCardVariant = StatCardVariant.Gold,
) {
    val accentColor = when (variant) {
        StatCardVariant.Gold  -> GoldLight
        StatCardVariant.Green -> Color(0xFF5AB038)
        StatCardVariant.Red   -> Color(0xFFB03030)
        StatCardVariant.Steel -> Color(0xFF4A9AC0)
    }

    val shape = RoundedCornerShape(4.dp)

    Column(
        modifier = modifier
            .clip(shape)
            .background(BgSurface)
            .border(1.dp, BorderDark, shape)
            .drawBehind {
                // 2px top accent line, like the Svelte site's stat cards
                drawLine(
                    color = accentColor,
                    start = Offset(0f, 0f),
                    end   = Offset(size.width, 0f),
                    strokeWidth = 4f,
                )
            }
            .padding(horizontal = 14.dp, vertical = 12.dp),
    ) {
        Text(
            text = label.uppercase(),
            fontSize = 10.sp,
            fontWeight = FontWeight.Medium,
            letterSpacing = 0.8.sp,
            color = TextMuted,
        )
        Spacer(Modifier.height(4.dp))
        Text(
            text = value,
            fontSize = 22.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 0.sp,
            color = accentColor,
        )
    }
}
