package com.floorplan3d.app.ui.settings

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Toast
import androidx.fragment.app.Fragment
import com.floorplan3d.app.FloorPlan3DApp
import com.floorplan3d.app.databinding.FragmentSettingsBinding

/**
 * SettingsFragment
 * ────────────────
 * Lets the user set the FastAPI server IP/port so the app can reach
 * the Python backend on the local network.
 *
 * Saved to SharedPreferences via FloorPlan3DApp.prefs.
 */
class SettingsFragment : Fragment() {

    private var _binding: FragmentSettingsBinding? = null
    private val binding get() = _binding!!

    override fun onCreateView(
        inflater: LayoutInflater, container: ViewGroup?,
        savedInstanceState: Bundle?
    ): View {
        _binding = FragmentSettingsBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        // Pre-fill from saved prefs
        binding.etServerUrl.setText(
            (requireActivity().application as FloorPlan3DApp).serverUrl
        )

        binding.btnSaveServer.setOnClickListener {
            val raw = binding.etServerUrl.text?.toString()?.trim() ?: ""
            if (raw.isBlank() || !raw.startsWith("http")) {
                binding.tilServerUrl.error = "Enter a valid URL, e.g. http://192.168.1.50:8000"
                return@setOnClickListener
            }
            binding.tilServerUrl.error = null
            FloorPlan3DApp.prefs.edit()
                .putString(FloorPlan3DApp.PREF_SERVER_URL, raw)
                .apply()
            Toast.makeText(requireContext(), "Server URL saved!", Toast.LENGTH_SHORT).show()
        }
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }
}
