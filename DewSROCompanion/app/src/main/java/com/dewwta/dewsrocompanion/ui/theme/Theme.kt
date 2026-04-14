package com.dewwta.dewsrocompanion.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val VSROColorScheme = darkColorScheme(
    primary           = GoldLight,
    onPrimary         = BgBase,
    primaryContainer  = BgRaised,
    onPrimaryContainer = GoldBright,

    secondary         = GoldBase,
    onSecondary       = BgBase,
    secondaryContainer = BgSurface,
    onSecondaryContainer = TextBase,

    tertiary          = SteelBright,
    onTertiary        = BgBase,
    tertiaryContainer = SteelDark,
    onTertiaryContainer = SteelBright,

    error             = CrimsonBright,
    onError           = BgBase,
    errorContainer    = CrimsonDark,
    onErrorContainer  = CrimsonBright,

    background        = BgBase,
    onBackground      = TextBright,

    surface           = BgSurface,
    onSurface         = TextBright,
    surfaceVariant    = BgRaised,
    onSurfaceVariant  = TextBase,

    outline           = BorderGold,
    outlineVariant    = BorderDark,

    inverseSurface     = TextBright,
    inverseOnSurface   = BgBase,
    inversePrimary     = GoldDim,

    scrim             = BgDeep,
)

@Composable
fun DewSROCompanionTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = VSROColorScheme,
        typography  = VSROTypography,
        content     = content,
    )
}
