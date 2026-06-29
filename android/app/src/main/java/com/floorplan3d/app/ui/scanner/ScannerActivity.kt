package com.floorplan3d.app.ui.scanner

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.view.View
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.floorplan3d.app.FloorPlan3DApp
import com.floorplan3d.app.databinding.ActivityScannerBinding
import com.floorplan3d.app.unity.UnityCallback
import com.floorplan3d.app.unity.UnityLauncher

/**
 * ScannerActivity
 * ───────────────
 * Full-screen activity that:
 *  1. Requests CAMERA permission if not yet granted.
 *  2. Embeds the Unity AR player via UnityLauncher (Unity as a Library).
 *  3. Provides a floating "Scan" FAB that triggers scanning through
 *     the Unity bridge (UnitySendMessage → ARFloorplanScanner.ScanFromAndroid).
 *  4. Listens for results via UnityCallback static callbacks and shows a
 *     success / error toast.
 */
class ScannerActivity : AppCompatActivity() {

    private lateinit var binding: ActivityScannerBinding

    private val cameraPermissionLauncher =
        registerForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
            if (granted) initUnity()
            else {
                Toast.makeText(this, "Camera permission is required to scan floor plans.", Toast.LENGTH_LONG).show()
                finish()
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivityScannerBinding.inflate(layoutInflater)
        setContentView(binding.root)

        // ── Close button ───────────────────────────────────────────────────
        binding.btnClose.setOnClickListener { finish() }

        // ── FAB: trigger Unity scan ────────────────────────────────────────
        binding.fabScan.setOnClickListener {
            binding.fabScan.isEnabled = false
            binding.scanProgress.visibility = View.VISIBLE
            binding.tvStatus.text = "Scanning…"
            UnityLauncher.triggerScan()
        }

        // ── Register Unity result callbacks ────────────────────────────────
        UnityCallback.onComplete = { json ->
            runOnUiThread {
                binding.fabScan.isEnabled = true
                binding.scanProgress.visibility = View.GONE
                binding.tvStatus.text = "3-D model ready!"
            }
        }
        UnityCallback.onError = { error ->
            runOnUiThread {
                binding.fabScan.isEnabled = true
                binding.scanProgress.visibility = View.GONE
                binding.tvStatus.text = "Error: $error"
                Toast.makeText(this, error, Toast.LENGTH_LONG).show()
            }
        }

        // ── Permission check ───────────────────────────────────────────────
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.CAMERA)
            == PackageManager.PERMISSION_GRANTED
        ) {
            initUnity()
        } else {
            cameraPermissionLauncher.launch(Manifest.permission.CAMERA)
        }
    }

    private fun initUnity() {
        val serverUrl = (application as FloorPlan3DApp).serverUrl
        UnityLauncher.init(this, binding.unityContainer, serverUrl)
    }

    override fun onDestroy() {
        super.onDestroy()
        UnityLauncher.destroy()
        UnityCallback.onComplete = null
        UnityCallback.onError    = null
    }
}
