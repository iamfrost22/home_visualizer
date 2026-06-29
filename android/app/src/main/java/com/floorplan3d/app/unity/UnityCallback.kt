package com.floorplan3d.app.unity

/**
 * UnityCallback
 * ─────────────
 * Static bridge that Unity's UnityBridge.cs calls into via AndroidJavaClass.
 * C# side:   callbackClass.CallStatic("onScanComplete", json)
 * Kotlin side: lambdas are invoked on the Unity thread → caller must switch to main thread.
 */
object UnityCallback {

    /** Called by Unity when segmentation + extrusion succeeded. */
    @JvmStatic
    var onComplete: ((String) -> Unit)? = null

    /** Called by Unity when an error occurred. */
    @JvmStatic
    var onError: ((String) -> Unit)? = null

    // ── JVM static methods called by AndroidJavaClass.CallStatic ─────────────

    @JvmStatic
    fun onScanComplete(json: String) {
        onComplete?.invoke(json)
    }

    @JvmStatic
    fun onScanError(error: String) {
        onError?.invoke(error)
    }
}
