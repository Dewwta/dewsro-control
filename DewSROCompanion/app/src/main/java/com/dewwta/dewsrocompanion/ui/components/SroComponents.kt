package com.dewwta.dewsrocompanion.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.BgRaised
import com.dewwta.dewsrocompanion.ui.theme.BorderGold
import com.dewwta.dewsrocompanion.ui.theme.BorderMid
import com.dewwta.dewsrocompanion.ui.theme.CrimsonDark
import com.dewwta.dewsrocompanion.ui.theme.CrimsonLight
import com.dewwta.dewsrocompanion.ui.theme.GoldBase
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.TextBase
import com.dewwta.dewsrocompanion.ui.theme.TextBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted

private val SroShape = RoundedCornerShape(4.dp)

// ── Primary gold button ───────────────────────────────────────────────────────
@Composable
fun SroButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    loading: Boolean = false,
    enabled: Boolean = true,
) {
    Button(
        onClick = onClick,
        enabled = enabled && !loading,
        shape = SroShape,
        colors = ButtonDefaults.buttonColors(
            containerColor         = Color(0x388B5E1C),
            contentColor           = GoldLight,
            disabledContainerColor = Color(0x228B5E1C),
            disabledContentColor   = GoldLight.copy(alpha = 0.4f),
        ),
        border = androidx.compose.foundation.BorderStroke(
            width = 1.dp,
            color = if (enabled && !loading) BorderGold else BorderMid,
        ),
        modifier = modifier.height(44.dp),
    ) {
        if (loading) {
            CircularProgressIndicator(
                color = GoldBright,
                strokeWidth = 2.dp,
                modifier = Modifier
                    .height(18.dp)
                    .padding(horizontal = 4.dp),
            )
        } else {
            Text(
                text = text.uppercase(),
                fontSize = 12.sp,
                fontWeight = FontWeight.SemiBold,
                letterSpacing = 1.sp,
                color = GoldLight,
            )
        }
    }
}

// ── Danger button ─────────────────────────────────────────────────────────────
@Composable
fun SroDangerButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    loading: Boolean = false,
    enabled: Boolean = true,
) {
    Button(
        onClick = onClick,
        enabled = enabled && !loading,
        shape = SroShape,
        colors = ButtonDefaults.buttonColors(
            containerColor         = Color(0x385C1010),
            contentColor           = CrimsonLight,
            disabledContainerColor = Color(0x225C1010),
            disabledContentColor   = CrimsonLight.copy(alpha = 0.4f),
        ),
        border = androidx.compose.foundation.BorderStroke(
            width = 1.dp,
            color = if (enabled && !loading) CrimsonDark else BorderMid,
        ),
        modifier = modifier.height(44.dp),
    ) {
        Text(
            text = text.uppercase(),
            fontSize = 12.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 1.sp,
        )
    }
}

// ── Themed text input ─────────────────────────────────────────────────────────
@Composable
fun SroTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    trailingIcon: @Composable (() -> Unit)? = null,
    visualTransformation: VisualTransformation = VisualTransformation.None,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    singleLine: Boolean = true,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = {
            Text(
                text = label.uppercase(),
                fontSize = 10.sp,
                letterSpacing = 0.8.sp,
                color = TextMuted,
            )
        },
        singleLine = singleLine,
        enabled = enabled,
        trailingIcon = trailingIcon,
        visualTransformation = visualTransformation,
        keyboardOptions = keyboardOptions,
        keyboardActions = keyboardActions,
        shape = SroShape,
        colors = OutlinedTextFieldDefaults.colors(
            focusedBorderColor       = GoldBase,
            unfocusedBorderColor     = BorderMid,
            focusedLabelColor        = GoldBase,
            unfocusedLabelColor      = TextMuted,
            focusedTextColor         = TextBright,
            unfocusedTextColor       = TextBase,
            cursorColor              = GoldBright,
            focusedContainerColor    = BgRaised,
            unfocusedContainerColor  = BgBase,
            disabledContainerColor   = BgBase,
            disabledTextColor        = TextMuted,
            disabledBorderColor      = BorderMid,
            disabledLabelColor       = TextMuted,
        ),
        modifier = modifier.fillMaxWidth(),
    )
}

// ── Section divider with title ────────────────────────────────────────────────
@Composable
fun SroSectionHeader(title: String, modifier: Modifier = Modifier) {
    Text(
        text = title.uppercase(),
        fontSize = 10.sp,
        fontWeight = FontWeight.SemiBold,
        letterSpacing = 1.4.sp,
        color = TextMuted,
        modifier = modifier.padding(vertical = 4.dp),
    )
}

// ── Gold ornamental divider ───────────────────────────────────────────────────
@Composable
fun GoldDivider(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(1.dp)
            .background(BorderGold),
    )
}

// ── Error banner ──────────────────────────────────────────────────────────────
@Composable
fun ErrorBanner(message: String, modifier: Modifier = Modifier) {
    Box(
        contentAlignment = Alignment.Center,
        modifier = modifier
            .fillMaxWidth()
            .clip(SroShape)
            .border(1.dp, CrimsonDark, SroShape)
            .background(Color(0x335C1010))
            .padding(horizontal = 14.dp, vertical = 10.dp),
    ) {
        Text(
            text = message,
            color = CrimsonLight,
            fontSize = 13.sp,
            letterSpacing = 0.3.sp,
        )
    }
}
