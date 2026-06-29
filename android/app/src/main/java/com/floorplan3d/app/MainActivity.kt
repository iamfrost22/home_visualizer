package com.floorplan3d.app

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            FloorPlanTheme {
                MainScreen(
                    onScanClicked = {
                        val intent = Intent(this@MainActivity, com.unity3d.player.UnityPlayerGameActivity::class.java)
                        startActivity(intent)
                    }
                )
            }
        }
    }
}

@Composable
fun FloorPlanTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = lightColorScheme(
            background = Color(0xFFF1D1B5), // 10% accent color as background or inverse? The user requested: 60% #FFEAA7, 30% #99582A, 10% #F1D1B5
            primary = Color(0xFF99582A),
            surface = Color(0xFFFFEAA7)
        ),
        content = content
    )
}

@Composable
fun MainScreen(onScanClicked: () -> Unit) {
    // 60% #FFEAA7, 30% #99582A, 10% #F1D1B5
    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xFFFFEAA7)), // 60% background
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            modifier = Modifier
                .padding(32.dp)
                .clip(RoundedCornerShape(24.dp))
                .background(Color.White.copy(alpha = 0.4f)) // Glassmorphism effect
                .padding(48.dp)
        ) {
            Text(
                text = "Floorplan AR",
                fontSize = 32.sp,
                fontWeight = FontWeight.Bold,
                color = Color(0xFF99582A) // 30% color for main text
            )
            
            Spacer(modifier = Modifier.height(16.dp))
            
            Text(
                text = "Scan and extrude 3D models from 2D floorplans seamlessly.",
                fontSize = 16.sp,
                color = Color(0xFF99582A).copy(alpha = 0.8f),
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )
            
            Spacer(modifier = Modifier.height(48.dp))
            
            Button(
                onClick = onScanClicked,
                colors = ButtonDefaults.buttonColors(
                    containerColor = Color(0xFFF1D1B5), // 10% accent color
                    contentColor = Color(0xFF99582A)
                ),
                shape = RoundedCornerShape(16.dp),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(56.dp)
            ) {
                Text(
                    text = "START SCANNING",
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}
