package com.dewwta.dewsrocompanion.navigation

import androidx.compose.animation.AnimatedContentTransitionScope
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import com.dewwta.dewsrocompanion.ui.screens.CharactersScreen
import com.dewwta.dewsrocompanion.ui.screens.DashboardScreen
import com.dewwta.dewsrocompanion.ui.screens.LoginScreen
import com.dewwta.dewsrocompanion.ui.screens.ProfileScreen
import com.dewwta.dewsrocompanion.ui.screens.SettingsScreen
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel

sealed class Screen(val route: String) {
    object Login      : Screen("login")
    object Dashboard  : Screen("dashboard")
    object Characters : Screen("characters")
    object Profile    : Screen("profile")
    object Settings   : Screen("settings")
}

@Composable
fun AppNavGraph(
    navController: NavHostController,
    authViewModel: AuthViewModel = viewModel(),
) {
    val token     by authViewModel.token.collectAsStateWithLifecycle()
    val serverUrl by authViewModel.serverUrl.collectAsStateWithLifecycle()

    // Restore persisted token + URL into Retrofit on cold start
    LaunchedEffect(token)     { token?.let     { authViewModel.restoreToken(it) } }
    LaunchedEffect(serverUrl) { serverUrl?.let { authViewModel.restoreUrl(it)   } }

    val startDestination = if (!token.isNullOrEmpty()) Screen.Dashboard.route else Screen.Login.route

    NavHost(
        navController       = navController,
        startDestination    = startDestination,
        modifier            = Modifier.fillMaxSize().background(BgBase),
        enterTransition     = { fadeIn(tween(200)) + slideIntoContainer(AnimatedContentTransitionScope.SlideDirection.Start, tween(200)) },
        exitTransition      = { fadeOut(tween(160)) },
        popEnterTransition  = { fadeIn(tween(200)) + slideIntoContainer(AnimatedContentTransitionScope.SlideDirection.End, tween(200)) },
        popExitTransition   = { fadeOut(tween(160)) },
    ) {
        composable(Screen.Login.route) {
            LoginScreen(
                authViewModel   = authViewModel,
                onLoginSuccess  = {
                    navController.navigate(Screen.Dashboard.route) {
                        popUpTo(Screen.Login.route) { inclusive = true }
                    }
                },
                onSettingsClick = {
                    navController.navigate(Screen.Settings.route) {
                        launchSingleTop = true
                    }
                },
            )
        }
        composable(Screen.Dashboard.route) {
            MainScaffold(navController = navController, authViewModel = authViewModel) {
                DashboardScreen(authViewModel = authViewModel)
            }
        }
        composable(Screen.Characters.route) {
            MainScaffold(navController = navController, authViewModel = authViewModel) {
                CharactersScreen()
            }
        }
        composable(Screen.Profile.route) {
            MainScaffold(navController = navController, authViewModel = authViewModel) {
                ProfileScreen(
                    authViewModel = authViewModel,
                    onLogout = {
                        navController.navigate(Screen.Login.route) {
                            popUpTo(0) { inclusive = true }
                        }
                    },
                )
            }
        }
        composable(Screen.Settings.route) {
            MainScaffold(navController = navController, authViewModel = authViewModel) {
                SettingsScreen(authViewModel = authViewModel)
            }
        }
    }
}
