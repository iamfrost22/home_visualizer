using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// ARFloorplanScanner (Fallback Version without AR Foundation dependencies)
/// ──────────────────────────────────────────────────────────────────────
/// This version removes all AR Foundation imports so it compiles instantly
/// on any Unity version without fighting with the Package Manager.
/// 
/// Instead of placing the floor plan on a detected physical floor, it places
/// it exactly 1.5 meters in front of the camera, floating at eye level.
/// You can still walk around it using your phone!
/// </summary>
public class ARFloorplanScanner : MonoBehaviour
{
    [Header("Server Connection")]
    public string serverUrl = "http://192.168.1.50:8000";

    [Header("References")]
    public FloorplanExtruder extruder;

    [Header("UI Controls")]
    public Text statusText;
    public Button scanButton;
    public Slider scaleSlider;
    public Slider rotationSlider;
    public Image alignmentRect;

    private GameObject _activeModel;
    private float _baseScale = 1f;
    private bool _isScanning;
    private string currentStatus = "Point camera at your floor plan and tap screen to scan.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoStart()
    {
        // Automatically inject the AR Scanner when the app launches without needing to setup the scene manually
        if (FindObjectOfType<ARFloorplanScanner>() != null) return;

        GameObject go = new GameObject("ARFloorplanScanner");
        var scanner = go.AddComponent<ARFloorplanScanner>();
        scanner.extruder = go.AddComponent<FloorplanExtruder>();
        
        if (Camera.main == null) {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
        }
    }

    void Start()
    {
        if (scanButton != null)
            scanButton.onClick.AddListener(TriggerScan);

        SetStatus("Point camera at your floor plan and tap screen to scan.");

        if (scaleSlider != null)
        {
            scaleSlider.minValue = 0.1f;
            scaleSlider.maxValue = 3.0f;
            scaleSlider.value = 1.0f;
            scaleSlider.onValueChanged.AddListener(OnScaleChanged);
        }

        if (rotationSlider != null)
        {
            rotationSlider.minValue = 0f;
            rotationSlider.maxValue = 360f;
            rotationSlider.value = 0f;
            rotationSlider.onValueChanged.AddListener(OnRotationChanged);
        }
    }

    void Update()
    {
        if (!_isScanning && Input.GetMouseButtonDown(0))
        {
            // Ignore touches on the bottom 20% of the screen (in case we tap sliders)
            if (Input.mousePosition.y > Screen.height * 0.2f)
            {
                TriggerScan();
            }
        }
    }

    void OnGUI()
    {
        int w = Screen.width;
        int h = Screen.height;
        
        GUIStyle style = new GUIStyle();
        style.fontSize = Mathf.RoundToInt(h * 0.03f);
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperCenter;

        // Draw Status at the top
        GUI.Label(new Rect(0, h * 0.05f, w, h * 0.1f), currentStatus, style);

        // If scanning is complete, draw sliders
        if (_activeModel != null)
        {
            GUI.Label(new Rect(10, h * 0.8f, w, h * 0.05f), "Scale", style);
            _baseScale = GUI.HorizontalSlider(new Rect(10, h * 0.85f, w - 20, h * 0.05f), _baseScale, 0.1f, 3f);
            OnScaleChanged(_baseScale);

            GUI.Label(new Rect(10, h * 0.9f, w, h * 0.05f), "Rotation", style);
            float rot = _activeModel.transform.localEulerAngles.y;
            rot = GUI.HorizontalSlider(new Rect(10, h * 0.95f, w - 20, h * 0.05f), rot, 0f, 360f);
            OnRotationChanged(rot);
        }
        else if (!_isScanning) // If not scanning, draw the targeting reticle
        {
            int cropSize = (int)(Mathf.Min(w, h) * 0.75f);
            int ox = (w - cropSize) / 2;
            int oy = (h - cropSize) / 2;
            
            // Draw a semi-transparent box
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.Box(new Rect(ox, oy, cropSize, cropSize), "");
            GUI.color = Color.white;
            
            GUIStyle hugeStyle = new GUIStyle();
            hugeStyle.fontSize = Mathf.RoundToInt(h * 0.05f);
            hugeStyle.normal.textColor = Color.green;
            hugeStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, h * 0.7f, w, h * 0.1f), "TAP ANYWHERE TO SCAN", hugeStyle);
        }
    }

    public void TriggerScan()
    {
        if (!_isScanning) StartCoroutine(CaptureAndUpload());
    }

    public void ScanFromAndroid(string unused)
    {
        TriggerScan();
    }

    private IEnumerator CaptureAndUpload()
    {
        _isScanning = true;
        SetStatus("Capturing frame…");

        yield return new WaitForEndOfFrame();

        int screenW = Screen.width;
        int screenH = Screen.height;

        int cropSize = (int)(Mathf.Min(screenW, screenH) * 0.75f);
        if (alignmentRect != null)
            cropSize = Mathf.RoundToInt(alignmentRect.rectTransform.rect.width);
        cropSize = Mathf.Max(cropSize, 64);

        int ox = (screenW - cropSize) / 2;
        int oy = (screenH - cropSize) / 2;

        Texture2D full = new Texture2D(screenW, screenH, TextureFormat.RGB24, false);
        full.ReadPixels(new Rect(0, 0, screenW, screenH), 0, 0);
        full.Apply();

        Texture2D crop = new Texture2D(cropSize, cropSize, TextureFormat.RGB24, false);
        crop.SetPixels(full.GetPixels(ox, oy, cropSize, cropSize));
        crop.Apply();

        byte[] png = crop.EncodeToPNG();
        Destroy(full);
        Destroy(crop);

        SetStatus("Running AI inference…");

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", png, "floorplan.png", "image/png");

        using (UnityWebRequest req = UnityWebRequest.Post(serverUrl + "/extract", form))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.ProtocolError)
            {
                SetStatus("Error: " + req.error);
                Debug.LogError("[ARFloorplanScanner] " + req.error);
                _isScanning = false;
                yield break;
            }

            SetStatus("Building 3-D model…");
            string json = req.downloadHandler.text;

            if (_activeModel != null)
                Destroy(_activeModel);

            if (extruder != null)
            {
                _activeModel = extruder.Extrude(json);
                if (_activeModel != null)
                    PlaceModelInFrontOfCamera(_activeModel);
            }

            SetStatus("Done! Use sliders to adjust size and rotation.");
        }

        _isScanning = false;
    }

    private void PlaceModelInFrontOfCamera(GameObject model)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Place 1.5 meters directly in front of the camera
            Vector3 pos = cam.transform.position + cam.transform.forward * 1.5f;
            
            // Lower it slightly so it doesn't block the exact center of vision
            pos.y -= 0.5f;

            model.transform.position = pos;

            // Make it face the camera
            Vector3 look = cam.transform.position - pos; 
            look.y = 0;
            if (look != Vector3.zero)
            {
                model.transform.rotation = Quaternion.LookRotation(look);
            }
        }
        else
        {
            // Absolute fallback
            model.transform.position = new Vector3(0, 0, 1.5f);
        }

        _baseScale = model.transform.localScale.x;

        if (scaleSlider != null) scaleSlider.value = 1f;
        if (rotationSlider != null) rotationSlider.value = 0f;
    }

    private void OnScaleChanged(float v)
    {
        if (_activeModel != null)
            _activeModel.transform.localScale = Vector3.one * (_baseScale * v);
    }

    private void OnRotationChanged(float v)
    {
        if (_activeModel != null)
        {
            Vector3 e = _activeModel.transform.localEulerAngles;
            e.y = v;
            _activeModel.transform.localEulerAngles = e;
        }
    }

    private void SetStatus(string msg)
    {
        currentStatus = msg;
        if (statusText != null) statusText.text = msg;
        Debug.Log("[ARFloorplanScanner] " + msg);
    }
}
