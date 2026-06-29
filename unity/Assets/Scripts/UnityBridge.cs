using System;
using UnityEngine;

/// <summary>
/// UnityBridge
/// ───────────
/// Sits on the "UnityBridge" GameObject in the scene.
/// Android Studio calls:
///   UnityPlayer.UnitySendMessage("UnityBridge", "TriggerScan",  "")
///   UnityPlayer.UnitySendMessage("UnityBridge", "SetServerUrl", "http://192.168.1.50:8000")
///
/// Unity sends results back to Android by calling a static Java method:
///   com.floorplan3d.app.UnityCallback.onScanComplete(String json)
///   com.floorplan3d.app.UnityCallback.onScanError(String error)
/// </summary>
public class UnityBridge : MonoBehaviour
{
    private const string ANDROID_CALLBACK_CLASS = "com.floorplan3d.app.UnityCallback";

    [SerializeField] private ARFloorplanScanner scanner;

    void Awake()
    {
        if (scanner == null)
            scanner = FindObjectOfType<ARFloorplanScanner>();
    }

    // ── Messages from Android Studio ──────────────────────────────────────────

    /// <summary>Start a scan. Called via UnitySendMessage from Android.</summary>
    public void TriggerScan(string unused)
    {
        if (scanner != null)
            scanner.ScanFromAndroid(unused);
        else
            Debug.LogError("[UnityBridge] ARFloorplanScanner not found.");
    }

    /// <summary>Update server URL at runtime (e.g. from Android settings screen).</summary>
    public void SetServerUrl(string url)
    {
        if (scanner != null && !string.IsNullOrEmpty(url))
        {
            scanner.serverUrl = url;
            Debug.Log("[UnityBridge] Server URL updated to: " + url);
        }
    }

    // ── Callbacks back to Android Studio ─────────────────────────────────────

    /// <summary>
    /// Call this from ARFloorplanScanner after successful extrusion to notify Android.
    /// </summary>
    public static void NotifyAndroidSuccess(string resultJson)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass callbackClass = new AndroidJavaClass(ANDROID_CALLBACK_CLASS))
            {
                callbackClass.CallStatic("onScanComplete", resultJson);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UnityBridge] Could not call Android callback: " + e.Message);
        }
#endif
        Debug.Log("[UnityBridge] Scan complete, result length=" + resultJson?.Length);
    }

    /// <summary>
    /// Call this from ARFloorplanScanner on error to notify Android.
    /// </summary>
    public static void NotifyAndroidError(string error)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass callbackClass = new AndroidJavaClass(ANDROID_CALLBACK_CLASS))
            {
                callbackClass.CallStatic("onScanError", error);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UnityBridge] Could not call Android error callback: " + e.Message);
        }
#endif
        Debug.LogError("[UnityBridge] Scan error: " + error);
    }
}
