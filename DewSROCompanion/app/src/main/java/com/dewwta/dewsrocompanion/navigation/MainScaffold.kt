package com.dewwta.dewsrocompanion.navigation

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.SportsKabaddi
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp
import androidx.navigation.NavHostController
import androidx.navigation.compose.currentBackStackEntryAsState
import com.dewwta.dewsrocompanion.ui.theme.BgBase
import com.dewwta.dewsrocompanion.ui.theme.BgSurface
import com.dewwta.dewsrocompanion.ui.theme.GoldBright
import com.dewwta.dewsrocompanion.ui.theme.GoldLight
import com.dewwta.dewsrocompanion.ui.theme.TextMuted
import com.dewwta.dewsrocompanion.viewmodel.AuthViewModel

data class NavItem(
    val label: String,
    val icon: ImageVector,
    val route: String,
)

val navItems = listOf(
    NavItem("Home",       Icons.Filled.Dashboard,     Screen.Dashboard.route),
    NavItem("Characters", Icons.Filled.SportsKabaddi, Screen.Characters.route),
    NavItem("Profile",    Icons.Filled.Person,         Screen.Profile.route),
    NavItem("Settings",   Icons.Filled.Settings,       Screen.Settings.route),
)

@Composable
fun MainScaffold(
    navController: NavHostController,
    authViewModel: AuthViewModel,
    content: @Composable () -> Unit,
) {
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route

    Scaffold(
        containerColor = BgBase,
        bottomBar = {
            NavigationBar(containerColor = BgSurface) {
                navItems.forEach { item ->
                    val selected = currentRoute == item.route
                    NavigationBarItem(
                        selected = selected,
                        onClick = {
                            if (currentRoute != item.route) {
                                navController.navigate(item.route) {
                                    popUpTo(Screen.Dashboard.route) { saveState = true }
                                    launchSingleTop = true
                                    restoreState    = true
                                }
                            }
                        },
                        icon = {
                            Icon(imageVector = item.icon, contentDescription = item.label)
                        },
                        label = {
                            Text(
                                text = item.label.uppercase(),
                                fontSize = 8.sp,
                                fontWeight = FontWeight.Medium,
                                letterSpacing = 0.8.sp,
                            )
                        },
                        colors = NavigationBarItemDefaults.colors(
                            selectedIconColor   = GoldBright,
                            selectedTextColor   = GoldLight,
                            unselectedIconColor = TextMuted,
                            unselectedTextColor = TextMuted,
                            indicatorColor      = BgSurface,
                        ),
                    )
                }
            }
        }
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(BgBase)
                .padding(innerPadding),
        ) {
            content()
        }
    }
}
