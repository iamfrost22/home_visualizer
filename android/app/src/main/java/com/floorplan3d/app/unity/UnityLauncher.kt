package com.floorplan3d.app.unity

import android.app.Activity
import android.widget.FrameLayout
import android.util.Log

/**
 * UnityLauncher
 * ─────────────
 * Thin wrapper around "Unity as a Library" (UnityPlayer).
 *
 * Unity as a Library flow:
 *   1. Export from Unity: File → Build Settings → check "Export as Google Android Project"
 *      OR use File → Build Settings → "Build" to produce a .AAR (unityLibrary module).
 *   2. Copy the exported module (or the .aar) into android/app/libs.
 *   3. Un-comment the implementation(fileTree…) line in app/build.gradle.kts.
 *   4. The class com.unity3d.player.UnityPlayer becomes available.
 *
 * Until the Unity export is done we guard every call with a try/catch so the
 * Android app still compiles and the rest of the UI is visible/testable.
 */
object UnityLauncher {

    private const val TAG         = "UnityLauncher"
    private const val BRIDGE_OBJ  = "UnityBridge"   // matches GameObject name in Unity scene

    private var unityPlayer: Any? = null   // typed as Any to avoid compile error before AAR is added

    /**
     * Attach the Unity player view into [container] and pass the server URL
     * to Unity so it can configure ARFloorplanScanner at runtime.
     */
    fun init(activity: Activity, container: FrameLayout, serverUrl: String) {
        try {
            val playerClass = Class.forName("com.unity3d.player.UnityPlayer")
            val player = playerClass
                .getConstructor(android.content.Context::class.java)
                .newInstance(activity)
            unityPlayer = player

            // Add Unity's rendering surface to our container
            val view = playerClass.getMethod("getView").invoke(player)
            container.addView(view as android.view.View)

            // Tell Unity which server URL to use
            sendMessage("SetServerUrl", serverUrl)

            Log.i(TAG, "Unity player initialised, server=$serverUrl")
        } catch (e: ClassNotFoundException) {
            Log.w(TAG, "UnityPlayer not found — Unity AAR not yet added to libs/. " +
                       "Export Unity project and add the AAR to continue.")
        } catch (e: Exception) {
            Log.e(TAG, "Unity init error: ${e.message}", e)
        }
    }

    /** Send the scan trigger to Unity's UnityBridge GameObject. */
    fun triggerScan() = sendMessage("TriggerScan", "")

    /** Update the server URL inside Unity at runtime. */
    fun setServerUrl(url: String) = sendMessage("SetServerUrl", url)

    /**
     * Release Unity resources. Call from Activity.onDestroy().
     */
    fun destroy() {
        try {
            unityPlayer?.let {
                it.javaClass.getMethod("destroy").invoke(it)
            }
        } catch (e: Exception) {
            Log.e(TAG, "Unity destroy error: ${e.message}")
        }
        unityPlayer = null
    }

    // ── Private helper ────────────────────────────────────────────────────────

    private fun sendMessage(method: String, param: String) {
        try {
            val playerClass = Class.forName("com.unity3d.player.UnityPlayer")
            playerClass.getMethod("UnitySendMessage", String::class.java, String::class.java, String::class.java)
                .invoke(null, BRIDGE_OBJ, method, param)
            Log.d(TAG, "UnitySendMessage → $BRIDGE_OBJ.$method($param)")
        } catch (e: ClassNotFoundException) {
            Log.w(TAG, "UnitySendMessage skipped – UnityPlayer not yet available.")
        } catch (e: Exception) {
            Log.e(TAG, "UnitySendMessage error: ${e.message}", e)
        }
    }
}
