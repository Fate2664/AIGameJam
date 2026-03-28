using System;
using System.Collections.Generic;
using Nova;
using NovaSamples.UIControls;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class DefenseAreaUiSwitchController : MonoBehaviour
{
    private const int ButtonSortGroupOrder = 550;
    private const int ButtonSortGroupRenderQueue = 4000;
    private const float ButtonPlaneDistanceFromCamera = 1f;
    private const int GameOverCanvasSortingOrder = 1000;
    private static readonly Color GameOverBackdropColor = new(0f, 0f, 0f, 0.92f);
    private static readonly Color GameOverTitleColor = new(1f, 0.35f, 0.35f, 1f);
    private static readonly Color GameOverButtonColor = new(1f, 1f, 1f, 0.96f);
    private static readonly Color GameOverButtonTextColor = new(0.16f, 0.16f, 0.16f, 1f);

    [Serializable]
    private sealed class AreaButtonBinding
    {
        public string buttonObjectName = string.Empty;
        public string areaObjectName = string.Empty;
        public Button button = null;
        public DefenseAreaController area = null;
        public ButtonVisuals visuals = null;

        [NonSerialized] public UnityAction clickHandler;
        [NonSerialized] public Action<DefenseAreaController, TowerStats, float> areaDamagedHandler;
        [NonSerialized] public Action<TowerStats> towerDepletedHandler;
        [NonSerialized] public Coroutine flashRoutine;
        [NonSerialized] public bool colorsCached;
        [NonSerialized] public Color defaultColor;
        [NonSerialized] public Color hoveredColor;
        [NonSerialized] public Color pressedColor;
    }

    [SerializeField] private Transform mainUiRoot = null;
    [SerializeField] private bool captureOffsetFromNearestArea = true;
    [SerializeField] private List<AreaButtonBinding> areaButtons = new();
    [Header("Damage Alert")]
    [SerializeField] private Color damageAlertColor = new(0.95f, 0.25f, 0.25f, 1f);
    [SerializeField] [Min(0.05f)] private float damageAlertDuration = 1.2f;
    [SerializeField] [Min(0.1f)] private float damageAlertPulseFrequency = 6f;

    private Vector3 mainUiAreaOffset = Vector3.zero;
    private bool offsetCaptured;
    private bool gameOverShown;
    private GameObject gameOverOverlay;

    private void Reset()
    {
        if (mainUiRoot == null)
        {
            mainUiRoot = transform;
        }

        EnsureDefaultBindings();
        ResolveBindings();
    }

    private void Awake()
    {
        if (mainUiRoot == null)
        {
            mainUiRoot = transform;
        }

        EnsureDefaultBindings();
        ResolveBindings();
        CaptureOffsetIfNeeded();
    }

    private void OnEnable()
    {
        ResolveBindings();
        RegisterHandlers();
    }

    private void OnDisable()
    {
        UnregisterHandlers();
    }

    private void OnDestroy()
    {
        if (gameOverShown)
        {
            Time.timeScale = 1f;
        }
    }

    private void OnValidate()
    {
        if (mainUiRoot == null)
        {
            mainUiRoot = transform;
        }

        EnsureDefaultBindings();
    }

    public void SwitchToPlantDefence()
    {
        SwitchToArea("PlantDefence");
    }

    public void SwitchToIceDefence()
    {
        SwitchToArea("IceDefence");
    }

    public void SwitchToDesertDefence()
    {
        SwitchToArea("DesertDefence");
    }

    public void SwitchToArea(string areaObjectName)
    {
        if (string.IsNullOrWhiteSpace(areaObjectName))
        {
            return;
        }

        EnsureDefaultBindings();
        ResolveBindings();

        for (int i = 0; i < areaButtons.Count; i++)
        {
            AreaButtonBinding binding = areaButtons[i];
            if (binding == null)
            {
                continue;
            }

            if (!string.Equals(binding.areaObjectName, areaObjectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SwitchToArea(binding.area);
            return;
        }

        GameObject areaObject = GameObject.Find(areaObjectName);
        DefenseAreaController area = areaObject != null ? areaObject.GetComponent<DefenseAreaController>() : null;
        SwitchToArea(area);
    }

    public void SwitchToArea(DefenseAreaController area)
    {
        if (mainUiRoot == null || area == null)
        {
            return;
        }

        CaptureOffsetIfNeeded();
        mainUiRoot.position = area.FocusPoint.position + mainUiAreaOffset;
    }

    private void EnsureDefaultBindings()
    {
        if (areaButtons.Count > 0)
        {
            return;
        }

        areaButtons.Add(CreateBinding("PlantTowerButton", "PlantDefence"));
        areaButtons.Add(CreateBinding("IceTowerButton", "IceDefence"));
        areaButtons.Add(CreateBinding("DesertTowerButton", "DesertDefence"));
    }

    private void ResolveBindings()
    {
        for (int i = 0; i < areaButtons.Count; i++)
        {
            AreaButtonBinding binding = areaButtons[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.button == null)
            {
                binding.button = FindButton(binding.buttonObjectName);
            }

            ConfigureButtonSorting(binding.button);
            ConfigureButtonDepth(binding.button);

            if (binding.visuals == null && binding.button != null)
            {
                ItemView itemView = binding.button.GetComponent<ItemView>();
                if (itemView != null)
                {
                    itemView.TryGetVisuals(out binding.visuals);
                }
            }

            if (binding.area == null)
            {
                binding.area = FindArea(binding.areaObjectName);
            }
        }
    }

    private void RegisterHandlers()
    {
        UnregisterHandlers();

        for (int i = 0; i < areaButtons.Count; i++)
        {
            AreaButtonBinding binding = areaButtons[i];
            if (binding?.button == null || binding.area == null)
            {
                continue;
            }

            AreaButtonBinding capturedBinding = binding;
            binding.clickHandler = () => SwitchToArea(capturedBinding.area);
            binding.button.OnClicked.AddListener(binding.clickHandler);
            binding.areaDamagedHandler = (area, damagedTower, damageAmount) => HandleAreaDamaged(capturedBinding);
            binding.area.AreaDamaged += binding.areaDamagedHandler;

            TowerStats towerStats = binding.area.TowerStats;
            if (towerStats != null)
            {
                binding.towerDepletedHandler = tower => HandleTowerDepleted(capturedBinding, tower);
                towerStats.Depleted += binding.towerDepletedHandler;
            }
        }
    }

    private void UnregisterHandlers()
    {
        for (int i = 0; i < areaButtons.Count; i++)
        {
            AreaButtonBinding binding = areaButtons[i];
            if (binding == null)
            {
                continue;
            }

            if (binding.button != null && binding.clickHandler != null)
            {
                binding.button.OnClicked.RemoveListener(binding.clickHandler);
                binding.clickHandler = null;
            }

            if (binding.area != null && binding.areaDamagedHandler != null)
            {
                binding.area.AreaDamaged -= binding.areaDamagedHandler;
                binding.areaDamagedHandler = null;
            }

            TowerStats towerStats = binding.area != null ? binding.area.TowerStats : null;
            if (towerStats != null && binding.towerDepletedHandler != null)
            {
                towerStats.Depleted -= binding.towerDepletedHandler;
                binding.towerDepletedHandler = null;
            }

            StopDamageFlash(binding);
        }
    }

    private void CaptureOffsetIfNeeded()
    {
        if (offsetCaptured || mainUiRoot == null || !captureOffsetFromNearestArea)
        {
            return;
        }

        DefenseAreaController nearestArea = ResolveNearestArea(mainUiRoot.position);
        if (nearestArea != null)
        {
            mainUiAreaOffset = mainUiRoot.position - nearestArea.FocusPoint.position;
        }

        offsetCaptured = true;
    }

    private DefenseAreaController ResolveNearestArea(Vector3 position)
    {
        DefenseAreaController nearestArea = null;
        float nearestDistanceSquared = float.MaxValue;

        for (int i = 0; i < areaButtons.Count; i++)
        {
            DefenseAreaController area = areaButtons[i]?.area;
            if (area == null)
            {
                continue;
            }

            float distanceSquared = (area.FocusPoint.position - position).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestArea = area;
        }

        return nearestArea ?? DefenseAreaController.FindClosest(position);
    }

    private Button FindButton(string buttonObjectName)
    {
        if (string.IsNullOrWhiteSpace(buttonObjectName))
        {
            return null;
        }

        Button[] buttons = mainUiRoot != null
            ? mainUiRoot.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || !string.Equals(button.name, buttonObjectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return button;
        }

        return null;
    }

    private static DefenseAreaController FindArea(string areaObjectName)
    {
        if (string.IsNullOrWhiteSpace(areaObjectName))
        {
            return null;
        }

        GameObject areaObject = GameObject.Find(areaObjectName);
        if (areaObject != null)
        {
            return areaObject.GetComponent<DefenseAreaController>();
        }

        DefenseAreaController[] areas = FindObjectsByType<DefenseAreaController>(FindObjectsSortMode.None);
        for (int i = 0; i < areas.Length; i++)
        {
            DefenseAreaController area = areas[i];
            if (area == null || !string.Equals(area.name, areaObjectName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return area;
        }

        return null;
    }

    private static AreaButtonBinding CreateBinding(string buttonObjectName, string areaObjectName)
    {
        return new AreaButtonBinding
        {
            buttonObjectName = buttonObjectName,
            areaObjectName = areaObjectName,
        };
    }

    private static void ConfigureButtonSorting(Button button)
    {
        if (button == null)
        {
            return;
        }

        UIBlock rootBlock = button.GetComponent<UIBlock>();
        if (rootBlock == null)
        {
            return;
        }

        SortGroup sortGroup = rootBlock.GetComponent<SortGroup>();
        if (sortGroup == null)
        {
            sortGroup = rootBlock.gameObject.AddComponent<SortGroup>();
        }

        sortGroup.SortingOrder = ButtonSortGroupOrder;
        sortGroup.RenderQueue = ButtonSortGroupRenderQueue;
        sortGroup.RenderOverOpaqueGeometry = true;
    }

    private void ConfigureButtonDepth(Button button)
    {
        if (button == null)
        {
            return;
        }

        Camera uiCamera = ResolveUiCamera();
        Transform buttonTransform = button.transform;
        Transform parent = buttonTransform.parent;
        if (uiCamera == null || parent == null)
        {
            return;
        }

        Vector3 desiredWorldPosition = buttonTransform.position;
        desiredWorldPosition.z = uiCamera.transform.position.z + ButtonPlaneDistanceFromCamera;

        Vector3 desiredLocalPosition = parent.InverseTransformPoint(desiredWorldPosition);
        buttonTransform.localPosition = new Vector3(
            buttonTransform.localPosition.x,
            buttonTransform.localPosition.y,
            desiredLocalPosition.z);
    }

    private Camera ResolveUiCamera()
    {
        if (mainUiRoot != null)
        {
            Camera childCamera = mainUiRoot.GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                return childCamera;
            }
        }

        return Camera.main;
    }

    private void HandleAreaDamaged(AreaButtonBinding binding)
    {
        if (binding?.button == null || binding.visuals == null)
        {
            return;
        }

        CacheButtonColors(binding);
        StopDamageFlash(binding);
        binding.flashRoutine = StartCoroutine(FlashAreaButton(binding));
    }

    private void HandleTowerDepleted(AreaButtonBinding binding, TowerStats towerStats)
    {
        if (gameOverShown)
        {
            return;
        }

        StopDamageFlash(binding);
        ShowGameOverOverlay();
    }

    private void ShowGameOverOverlay()
    {
        if (gameOverShown)
        {
            return;
        }

        gameOverShown = true;
        EnsureEventSystem();
        gameOverOverlay = CreateGameOverOverlay();
        Time.timeScale = 0f;
    }

    private GameObject CreateGameOverOverlay()
    {
        GameObject canvasObject = new("TowerGameOverOverlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = GameOverCanvasSortingOrder;

        UnityEngine.UI.CanvasScaler canvasScaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
        canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject backdrop = new("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<UnityEngine.UI.Image>().color = GameOverBackdropColor;

        CreateOverlayText(
            canvasObject.transform,
            "GameOverText",
            "GAME OVER",
            new Vector2(0f, 120f),
            new Vector2(900f, 140f),
            84f,
            GameOverTitleColor,
            FontStyles.Bold);

        GameObject buttonObject = new("RestartButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonObject.transform.SetParent(canvasObject.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(340f, 100f);

        UnityEngine.UI.Image buttonImage = buttonObject.GetComponent<UnityEngine.UI.Image>();
        buttonImage.color = GameOverButtonColor;

        UnityEngine.UI.Button restartButton = buttonObject.GetComponent<UnityEngine.UI.Button>();
        restartButton.targetGraphic = buttonImage;
        restartButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        restartButton.onClick.AddListener(RestartGame);

        CreateOverlayText(
            buttonObject.transform,
            "RestartLabel",
            "Restart",
            Vector2.zero,
            Vector2.zero,
            42f,
            GameOverButtonTextColor,
            FontStyles.Bold,
            stretchToParent: true);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(buttonObject);
        }

        return canvasObject;
    }

    private static TextMeshProUGUI CreateOverlayText(
        Transform parent,
        string objectName,
        string text,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        float fontSize,
        Color color,
        FontStyles fontStyle,
        bool stretchToParent = false)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        if (stretchToParent)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }
        else
        {
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = anchoredPosition;
            textRect.sizeDelta = sizeDelta;
        }

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }

        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    private System.Collections.IEnumerator FlashAreaButton(AreaButtonBinding binding)
    {
        UIBlock target = binding.visuals.TransitionTarget;
        if (target == null)
        {
            binding.flashRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < damageAlertDuration)
        {
            float pulse = (Mathf.Sin(elapsed * damageAlertPulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            binding.visuals.DefaultColor = Color.Lerp(binding.defaultColor, damageAlertColor, pulse);
            binding.visuals.HoveredColor = Color.Lerp(binding.hoveredColor, damageAlertColor, pulse);
            binding.visuals.PressedColor = Color.Lerp(binding.pressedColor, damageAlertColor, pulse);
            target.Color = Color.Lerp(binding.defaultColor, damageAlertColor, pulse);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        RestoreButtonColors(binding);
        binding.flashRoutine = null;
    }

    private void CacheButtonColors(AreaButtonBinding binding)
    {
        if (binding == null || binding.visuals == null || binding.colorsCached)
        {
            return;
        }

        binding.defaultColor = binding.visuals.DefaultColor;
        binding.hoveredColor = binding.visuals.HoveredColor;
        binding.pressedColor = binding.visuals.PressedColor;
        binding.colorsCached = true;
    }

    private void StopDamageFlash(AreaButtonBinding binding)
    {
        if (binding == null || binding.flashRoutine == null)
        {
            return;
        }

        StopCoroutine(binding.flashRoutine);
        binding.flashRoutine = null;
        RestoreButtonColors(binding);
    }

    private static void RestoreButtonColors(AreaButtonBinding binding)
    {
        if (binding == null || binding.visuals == null || !binding.colorsCached)
        {
            return;
        }

        binding.visuals.DefaultColor = binding.defaultColor;
        binding.visuals.HoveredColor = binding.hoveredColor;
        binding.visuals.PressedColor = binding.pressedColor;

        UIBlock target = binding.visuals.TransitionTarget;
        if (target != null)
        {
            target.Color = binding.defaultColor;
        }
    }
}
