package com.dewwta.dewsrocompanion.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.SentimentDissatisfied
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import com.dewwta.dewsrocompanion.ui.components.CharacterCard
import com.dewwta.dewsrocompanion.ui.components.GoldDivider
import com.dewwta.dewsrocompanion.ui.components.SroButton
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.CrimsonLight
import com.dewwta.dewsrocompanion.ui.theme.GoldBase
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.CharactersState
import com.dewwta.dewsrocompanion.viewmodel.CharactersViewModel

@Composable
fun CharactersScreen(
    vm: CharactersViewModel = viewModel(),
) {
    val state by vm.state.collectAsStateWithLifecycle()

    LaunchedEffect(Unit) { vm.load() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(BgBase),
    ) {
        // ── Page header ────────────────────────────────────────────────────
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(com.dewwta.dewsrocompanion.ui.theme.BgSurface)
                .padding(horizontal = 20.dp, vertical = 16.dp),
        ) {
            Text(
                text = "MY CHARACTERS",
                fontSize = 13.sp,
                fontWeight = FontWeight.SemiBold,
                fontFamily = FontFamily.Serif,
                letterSpacing = 2.sp,
                color = com.dewwta.dewsrocompanion.ui.theme.GoldLight,
                modifier = Modifier.align(Alignment.CenterStart),
            )
            IconButton(
                onClick = { vm.load() },
                modifier = Modifier
                    .align(Alignment.CenterEnd)
                    .size(32.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Refresh,
                    contentDescription = "Refresh",
                    tint = TextMuted,
                    modifier = Modifier.size(18.dp),
                )
            }
        }
        GoldDivider()

        when (val s = state) {
            is CharactersState.Loading -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        CircularProgressIndicator(
                            color = GoldBase,
                            strokeWidth = 2.dp,
                            modifier = Modifier.size(36.dp),
                        )
                        Spacer(Modifier.height(12.dp))
                        Text("Loading characters…", color = TextMuted, fontSize = 13.sp)
                    }
                }
            }

            is CharactersState.Success -> {
                if (s.characters.isEmpty()) {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        Column(
                            horizontalAlignment = Alignment.CenterHorizontally,
                            verticalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(
                                imageVector = Icons.Filled.SentimentDissatisfied,
                                contentDescription = null,
                                tint = TextMuted,
                                modifier = Modifier.size(40.dp),
                            )
                            Text(
                                text = "No characters found.",
                                color = TextMuted,
                                fontSize = 14.sp,
                            )
                        }
                    }
                } else {
                    LazyColumn(
                        verticalArrangement = Arrangement.spacedBy(10.dp),
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(horizontal = 16.dp, vertical = 16.dp),
                    ) {
                        items(s.characters, key = { it.charID }) { char ->
                            CharacterCard(character = char)
                        }
                    }
                }
            }

            is CharactersState.Error -> {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        modifier = Modifier.padding(24.dp),
                    ) {
                        Text(
                            text = s.message,
                            color = CrimsonLight,
                            fontSize = 13.sp,
                            textAlign = TextAlign.Center,
                        )
                        Spacer(Modifier.height(16.dp))
                        SroButton(text = "Retry", onClick = { vm.load() })
                    }
                }
            }

            else -> {}
        }
    }
}
