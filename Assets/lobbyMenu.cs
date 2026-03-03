// lobbyMenu.cs
// No extra plugins: uses built-in Unity UI (Canvas/Button/Text) + EventSystem.
// Attach to an empty GameObject in your scene. Press Play to auto-build the menu.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class lobbyMenu : MonoBehaviour
{
    [Header("World Space")]
    public bool worldSpace = true;
    public Vector2 canvasSize = new Vector2(1600, 1000);
    [Tooltip("0.001 means 1000px = ~1 meter in world space.")]
    public float worldScale = 0.001f;

    [Tooltip("If set, menu will face this target (usually Main Camera).")]
    public Transform faceTarget;
    public Vector3 worldPosition = new Vector3(0f, 1.5f, 2f);

    [Header("Buttons")]
    public Vector2 buttonSize = new Vector2(1200, 140);
    public float buttonSpacing = 40f;
    [Range(0f, 1f)] public float backgroundAlpha = 0.20f; // subtle fill
    [Range(0f, 1f)] public float borderAlpha = 0.95f;     // white border
    [Range(0f, 1f)] public float textAlpha = 0.95f;

    public int fontSize = 48;

    private GameObject _root;

    private void Start()
    {
        EnsureEventSystem();
        BuildMenu();
        PositionMenu();
    }

    private void LateUpdate()
    {
        // Keep facing camera (yaw only)
        if (worldSpace)
        {
            if (faceTarget == null && Camera.main != null) faceTarget = Camera.main.transform;
            if (faceTarget != null && _root != null)
            {
                Vector3 dir = faceTarget.position - _root.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    _root.transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            }
        }
    }

    private void BuildMenu()
    {
        // Clear previous build (if any)
        if (_root != null)
        {
            Destroy(_root);
        }

        _root = new GameObject("LobbyMenuCanvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = worldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;

        _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        _root.AddComponent<GraphicRaycaster>();

        var canvasRT = _root.GetComponent<RectTransform>();
        canvasRT.sizeDelta = canvasSize;
        canvasRT.localScale = Vector3.one * (worldSpace ? worldScale : 1f);

        // Container with vertical layout
        var container = new GameObject("MenuContainer", typeof(RectTransform));
        container.transform.SetParent(_root.transform, false);

        var containerRT = container.GetComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.pivot = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = Vector2.zero;

        var vlg = container.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = buttonSpacing;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var fitter = container.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Buttons
        CreateButton(container.transform, "Create Lobby", OnCreateLobby);
        CreateButton(container.transform, "Join Lobby", OnJoinLobby);
        CreateButton(container.transform, "Options", OnOptions);
        CreateButton(container.transform, "Quit", OnQuit);
    }

    private void PositionMenu()
    {
        if (_root == null) return;

        if (worldSpace)
        {
            _root.transform.position = worldPosition;

            // Default face target
            if (faceTarget == null && Camera.main != null)
                faceTarget = Camera.main.transform;
        }
    }

    private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Btn_{label.Replace(" ", "")}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = buttonSize;

        // Background
        var bg = go.AddComponent<Image>();
        bg.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0f, 0f, 0f, backgroundAlpha);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = buttonSize.x;
        le.preferredHeight = buttonSize.y;

        // Border (outline only)
        var borderGO = new GameObject("Border", typeof(RectTransform));
        borderGO.transform.SetParent(go.transform, false);

        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        var border = borderGO.AddComponent<Image>();
        border.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        border.type = Image.Type.Sliced;
        border.fillCenter = false;         // important: border only
        border.raycastTarget = false;
        border.color = new Color(1f, 1f, 1f, borderAlpha);

        // Text
        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        var txt = textGO.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = fontSize;
        txt.color = new Color(1f, 1f, 1f, textAlpha);
        txt.raycastTarget = false;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    // --- Button actions (replace with your real logic) ---
    private void OnCreateLobby() => Debug.Log("Create Lobby clicked");
    private void OnJoinLobby() => Debug.Log("Join Lobby clicked");
    private void OnOptions() => Debug.Log("Options clicked");
    private void OnQuit()
    {
        Debug.Log("Quit clicked");
        Application.Quit();
    }
}