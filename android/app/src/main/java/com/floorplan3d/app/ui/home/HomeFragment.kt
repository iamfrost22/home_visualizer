package com.floorplan3d.app.ui.home

import android.content.Intent
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import com.floorplan3d.app.databinding.FragmentHomeBinding
import com.floorplan3d.app.ui.scanner.ScannerActivity

/**
 * Home screen fragment.
 * Shows the app hero, a "Scan Floor Plan" CTA button, and recent scan cards.
 * Tapping Scan → launches ScannerActivity (Unity AR surface).
 */
class HomeFragment : Fragment() {

    private var _binding: FragmentHomeBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentHomeBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // ── Scan CTA ───────────────────────────────────────────────────────
        binding.btnScanFloorplan.setOnClickListener {
            startActivity(Intent(requireContext(), ScannerActivity::class.java))
        }

        // ── Animate button on creation ─────────────────────────────────────
        binding.btnScanFloorplan.apply {
            alpha = 0f
            animate().alpha(1f).translationYBy(-20f).setDuration(500).setStartDelay(200).start()
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
