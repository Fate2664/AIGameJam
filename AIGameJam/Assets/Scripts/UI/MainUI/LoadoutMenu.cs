using System;
using System.Collections.Generic;
using Nova;
using NovaSamples.UIControls;
using UnityEngine;

[Serializable]
public class LoadoutMenu : MonoBehaviour
{
    private static readonly List<LoadoutItemDefinition> EmptyLoadout = new();
    private const int UiSortGroupOrder = 500;
    private const int UiSortGroupRenderQueue = 4000;
    private const float RootPlaneDistanceFromCamera = 1f;
    private const string ResetGridButtonObjectName = "ResetGridButton";

    public static LoadoutMenu Instance;

    public UIBlock Root = null;
    public GridManager GridManager = null;
    [SerializeField] private List<GridManager> additionalGridManagers = new();
    public ListView LoadoutList = null;
    public LoadoutCollection Loadout = null;

    [Header("Drag Preview")]
    public UIBlock2D DragPreviewRoot = null;
    public UIBlock2D DragPreviewImage = null;
    public TextBlock DragPreviewCostText = null;

    [Header("Budget")]
    public bool EnforceBudget = false;
    [Min(0)] public int StartingBudget = 0;
    public TextBlock BudgetText = null;
    public string BudgetFormat = "{0}";

    private int currentBudget;
    private LoadoutItemVisuals draggedVisual;
    private LoadoutItemDefinition draggedItem;
    private LoadoutItemDefinition selectedItem;
    private bool isDragging;
    private readonly List<GridManager> managedGridManagers = new();
    private int lastGridResetFrame = -1;
    private Button resetGridButton;
    private bool resetGridRequestPending;

    public event Action<LoadoutItemDefinition> DragStarted;
    public event Action<LoadoutItemDefinition> DragCanceled;
    public event Action<LoadoutItemDefinition, CellVisuals> ItemPlaced;
    public event Action<int> BudgetChanged;

    public int CurrentBudget => currentBudget;
    private IList<LoadoutItemDefinition> CurrentLoadout => Loadout != null && Loadout.Items != null ? Loadout.Items : EmptyLoadout;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ConfigureUiSorting();
        RebuildManagedGridManagers();
    }

    private void Start()
    {
        ConfigureUiSorting();
        currentBudget = StartingBudget;

        LoadoutList.AddDataBinder<LoadoutItemDefinition, LoadoutItemVisuals>(BindLoadoutItem);
        Root.AddGestureHandler<Gesture.OnHover, LoadoutItemVisuals>(HandleItemHover);
        Root.AddGestureHandler<Gesture.OnUnhover, LoadoutItemVisuals>(HandleItemUnhover);
        Root.AddGestureHandler<Gesture.OnPress, LoadoutItemVisuals>(HandleItemPress);
        Root.AddGestureHandler<Gesture.OnRelease, LoadoutItemVisuals>(HandleItemRelease);
        Root.AddGestureHandler<Gesture.OnDrag, LoadoutItemVisuals>(HandleItemDrag);
        Root.AddGestureHandler<Gesture.OnCancel, LoadoutItemVisuals>(HandleItemCancel);
        Root.AddGestureHandler<Gesture.OnClick, LoadoutItemVisuals>(HandleItemClick);

        SubscribeToGridManagers();
        BindResetGridButton();

        LoadoutList.SetDataSource(CurrentLoadout);
        HideDragPreview();
        RefreshBudgetVisual();
        RefreshVisibleItems();
    }

    private void OnDisable()
    {
        UnsubscribeFromGridManagers();
        UnbindResetGridButton();

        CancelActiveDrag();
    }

    public void SetBudget(int value)
    {
        currentBudget = Mathf.Max(0, value);
        RefreshBudgetVisual();
        RefreshVisibleItems();
        ValidateSelectedItem();
        BudgetChanged?.Invoke(currentBudget);
    }

    public void AddBudget(int amount)
    {
        SetBudget(currentBudget + amount);
    }

    public bool CanAfford(LoadoutItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        return !EnforceBudget || currentBudget >= item.Cost;
    }

    public void Refresh()
    {
        LoadoutList.SetDataSource(CurrentLoadout);
        RefreshBudgetVisual();
        RefreshVisibleItems();
    }

    public void HandleResetGridButtonPressed()
    {
        if (!resetGridRequestPending)
        {
            return;
        }

        resetGridRequestPending = false;

        if (lastGridResetFrame == Time.frameCount)
        {
            return;
        }

        lastGridResetFrame = Time.frameCount;
        CancelActiveDrag();
        RebuildManagedGridManagers();

        int refundedAmount = 0;
        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager == null)
            {
                continue;
            }

            refundedAmount += managedGridManager.ClearPlacedItemsAndCalculateRefund();
        }

        if (EnforceBudget && refundedAmount > 0)
        {
            AddBudget(refundedAmount);
            return;
        }

        RefreshVisibleItems();
        ValidateSelectedItem();
    }

    private void HandleItemHover(Gesture.OnHover evt, LoadoutItemVisuals target)
    {
        if (target == null || target == draggedVisual)
        {
            return;
        }

        target.SetHovered(true);
    }

    private void HandleItemUnhover(Gesture.OnUnhover evt, LoadoutItemVisuals target)
    {
        if (target == null || target == draggedVisual)
        {
            return;
        }

        target.SetHovered(false);
    }

    private void HandleItemPress(Gesture.OnPress evt, LoadoutItemVisuals target)
    {
        if (!CanDrag(target))
        {
            return;
        }

        SelectItem(target.BoundItem, false);
        target.SetPressed(true);
    }

    private void HandleItemDrag(Gesture.OnDrag evt, LoadoutItemVisuals target)
    {
        if (!CanDrag(target))
        {
            return;
        }

        if (!isDragging)
        {
            BeginDrag(target, evt.PointerPositions.Start);
        }

        UpdateDragPreview(evt.PointerPositions.Current);
    }

    private void HandleItemRelease(Gesture.OnRelease evt, LoadoutItemVisuals target)
    {
        if (target != null)
        {
            target.SetPressed(false);
            if (target != draggedVisual)
            {
                target.SetDragging(false);
            }
        }

        if (!isDragging || target != draggedVisual)
        {
            return;
        }

        TryPlaceDraggedItem();
        EndDrag();
    }

    private void HandleItemCancel(Gesture.OnCancel evt, LoadoutItemVisuals target)
    {
        if (target != null)
        {
            target.SetPressed(false);
        }

        CancelActiveDrag();
    }

    private void HandleItemClick(Gesture.OnClick evt, LoadoutItemVisuals target)
    {
        if (!CanDrag(target))
        {
            return;
        }

        SelectItem(target.BoundItem, true);
    }

    private void BindLoadoutItem(Data.OnBind<LoadoutItemDefinition> evt, LoadoutItemVisuals target, int index)
    {
        target.Bind(evt.UserData, CanAfford(evt.UserData));
    }

    private void BeginDrag(LoadoutItemVisuals target, Vector3 pointerWorldPosition)
    {
        draggedVisual = target;
        draggedItem = target.BoundItem;
        isDragging = true;

        SelectItem(draggedItem, false);
        draggedVisual.SetDragging(true);
        ShowDragPreview(draggedItem, pointerWorldPosition);

        DragStarted?.Invoke(draggedItem);
    }

    private bool TryPlaceDraggedItem()
    {
        if (draggedItem == null || !CanAfford(draggedItem))
        {
            return false;
        }

        if (!TryGetHoveredGridManager(out GridManager hoveredGridManager, out CellVisuals hoveredCell))
        {
            return false;
        }

        if (hoveredGridManager.SelectedPlaceablePrefab == null || !hoveredGridManager.CanPlaceSelectedAt(hoveredCell))
        {
            return false;
        }

        bool placed = hoveredGridManager.TryPlaceSelected(hoveredCell);
        return placed;
    }

    private void EndDrag()
    {
        if (draggedVisual != null)
        {
            draggedVisual.SetDragging(false);
            draggedVisual.SetHovered(false);
            draggedVisual.SetPressed(false);
        }

        HideDragPreview();

        SetSelectedPlaceableOnAllManagers(selectedItem, true);

        draggedVisual = null;
        draggedItem = null;
        isDragging = false;
    }

    private void CancelActiveDrag()
    {
        if (!isDragging)
        {
            return;
        }

        LoadoutItemDefinition canceledItem = draggedItem;
        EndDrag();
        DragCanceled?.Invoke(canceledItem);
    }

    private void RefreshVisibleItems()
    {
        IList<LoadoutItemDefinition> loadoutItems = CurrentLoadout;
        for (int i = 0; i < loadoutItems.Count; i++)
        {
            if (!LoadoutList.TryGetItemView(i, out ItemView itemView))
            {
                continue;
            }

            if (itemView.TryGetVisuals(out LoadoutItemVisuals visuals))
            {
                visuals.SetAffordable(CanAfford(loadoutItems[i]));
            }
        }
    }

    private void RefreshBudgetVisual()
    {
        if (BudgetText == null)
        {
            return;
        }

        BudgetText.Text = string.Format(BudgetFormat, currentBudget);
    }

    private void ShowDragPreview(LoadoutItemDefinition item, Vector3 pointerWorldPosition)
    {
        if (DragPreviewRoot == null)
        {
            return;
        }

        DragPreviewRoot.Visible = true;
        DragPreviewRoot.transform.position = pointerWorldPosition;
        GridManager rotationGridManager = ResolveRotationSourceGridManager();
        DragPreviewRoot.transform.rotation = rotationGridManager != null ? rotationGridManager.SelectedRotation : Quaternion.identity;

        if (DragPreviewImage != null)
        {
            if (item != null && item.Icon != null)
            {
                DragPreviewImage.SetImage(item.Icon);
            }
            else
            {
                DragPreviewImage.ClearImage();
            }
        }

        if (DragPreviewCostText != null)
        {
            DragPreviewCostText.Text = item != null ? item.Cost.ToString() : string.Empty;
        }
    }

    private void UpdateDragPreview(Vector3 pointerWorldPosition)
    {
        if (DragPreviewRoot == null)
        {
            return;
        }

        DragPreviewRoot.transform.position = pointerWorldPosition;
    }

    private void HideDragPreview()
    {
        if (DragPreviewRoot != null)
        {
            DragPreviewRoot.Visible = false;
            DragPreviewRoot.transform.rotation = Quaternion.identity;
        }
    }

    private bool CanDrag(LoadoutItemVisuals target)
    {
        return target != null &&
               target.BoundItem != null &&
               target.BoundItem.PlaceablePrefab != null &&
               CanAfford(target.BoundItem);
    }

    private void SelectItem(LoadoutItemDefinition item, bool allowClickPlacement)
    {
        selectedItem = item;
        SetSelectedPlaceableOnAllManagers(selectedItem, allowClickPlacement);
    }

    private void ValidateSelectedItem()
    {
        if (selectedItem == null)
        {
            return;
        }

        if (!CanAfford(selectedItem) || selectedItem.PlaceablePrefab == null)
        {
            selectedItem = null;
            if (!isDragging)
            {
                ClearSelectedPlaceableOnAllManagers();
            }

            return;
        }

        if (!isDragging)
        {
            SetSelectedPlaceableOnAllManagers(selectedItem, true);
        }
    }

    private void HandleRuntimeResetGridButtonClicked()
    {
        resetGridRequestPending = true;
        HandleResetGridButtonPressed();
    }

    private void HandleSelectedRotationChanged(float rotationDegrees)
    {
        if (!isDragging || DragPreviewRoot == null)
        {
            return;
        }

        DragPreviewRoot.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
    }

    private void RebuildManagedGridManagers()
    {
        managedGridManagers.Clear();
        TryAddManagedGridManager(GridManager);

        for (int i = 0; i < additionalGridManagers.Count; i++)
        {
            TryAddManagedGridManager(additionalGridManagers[i]);
        }

        GridManager[] discoveredGridManagers = FindObjectsByType<GridManager>(FindObjectsSortMode.None);
        for (int i = 0; i < discoveredGridManagers.Length; i++)
        {
            TryAddManagedGridManager(discoveredGridManagers[i]);
        }
    }

    private void SubscribeToGridManagers()
    {
        RebuildManagedGridManagers();

        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager == null)
            {
                continue;
            }

            managedGridManager.SelectedRotationChanged += HandleSelectedRotationChanged;
            managedGridManager.CellItemPlaced += HandleGridItemPlaced;
        }
    }

    private void UnsubscribeFromGridManagers()
    {
        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager == null)
            {
                continue;
            }

            managedGridManager.SelectedRotationChanged -= HandleSelectedRotationChanged;
            managedGridManager.CellItemPlaced -= HandleGridItemPlaced;
        }
    }

    private void SetSelectedPlaceableOnAllManagers(LoadoutItemDefinition item, bool allowClickPlacement)
    {
        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager != null)
            {
                managedGridManager.SetSelectedPlaceable(item, allowClickPlacement);
            }
        }
    }

    private void ClearSelectedPlaceableOnAllManagers()
    {
        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager != null)
            {
                managedGridManager.ClearSelectedPlaceable();
            }
        }
    }

    private bool TryGetHoveredGridManager(out GridManager hoveredGridManager, out CellVisuals hoveredCell)
    {
        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            GridManager managedGridManager = managedGridManagers[i];
            if (managedGridManager == null || !managedGridManager.TryGetHoveredCell(out hoveredCell))
            {
                continue;
            }

            hoveredGridManager = managedGridManager;
            return true;
        }

        hoveredGridManager = null;
        hoveredCell = null;
        return false;
    }

    private GridManager ResolveRotationSourceGridManager()
    {
        if (GridManager != null)
        {
            return GridManager;
        }

        for (int i = 0; i < managedGridManagers.Count; i++)
        {
            if (managedGridManagers[i] != null)
            {
                return managedGridManagers[i];
            }
        }

        return null;
    }

    private void TryAddManagedGridManager(GridManager managedGridManager)
    {
        if (managedGridManager == null || managedGridManagers.Contains(managedGridManager))
        {
            return;
        }

        managedGridManagers.Add(managedGridManager);
    }

    private void BindResetGridButton()
    {
        UnbindResetGridButton();

        resetGridButton = FindButton(ResetGridButtonObjectName);
        if (resetGridButton != null)
        {
            resetGridButton.OnClicked.AddListener(HandleRuntimeResetGridButtonClicked);
        }
    }

    private void UnbindResetGridButton()
    {
        if (resetGridButton != null)
        {
            resetGridButton.OnClicked.RemoveListener(HandleRuntimeResetGridButtonClicked);
            resetGridButton = null;
        }
    }

    private static Button FindButton(string buttonObjectName)
    {
        if (string.IsNullOrWhiteSpace(buttonObjectName))
        {
            return null;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
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

    private void HandleGridItemPlaced(CellVisuals cell, GameObject placedItem)
    {
        if (cell == null || placedItem == null)
        {
            return;
        }

        LoadoutItemDefinition placedDefinition = draggedItem ?? selectedItem;
        if (placedDefinition == null)
        {
            return;
        }

        if (EnforceBudget)
        {
            currentBudget = Mathf.Max(0, currentBudget - placedDefinition.Cost);
            RefreshBudgetVisual();
            BudgetChanged?.Invoke(currentBudget);
        }

        RefreshVisibleItems();
        ValidateSelectedItem();
        ItemPlaced?.Invoke(placedDefinition, cell);
    }

    private void ConfigureUiSorting()
    {
        ConfigureSortGroup(Root);
        ConfigureWorldSpaceDepth(Root);
    }

    private static void ConfigureSortGroup(UIBlock rootBlock)
    {
        if (rootBlock == null)
        {
            return;
        }

        SortGroup sortGroup = rootBlock.GetComponent<SortGroup>();
        if (sortGroup == null)
        {
            sortGroup = rootBlock.gameObject.AddComponent<SortGroup>();
        }

        sortGroup.SortingOrder = UiSortGroupOrder;
        sortGroup.RenderQueue = UiSortGroupRenderQueue;
        sortGroup.RenderOverOpaqueGeometry = true;
    }

    private void ConfigureWorldSpaceDepth(UIBlock rootBlock)
    {
        if (rootBlock == null)
        {
            return;
        }

        Camera uiCamera = Camera.main;
        if (uiCamera == null)
        {
            uiCamera = FindAnyObjectByType<Camera>();
        }

        Transform rootTransform = rootBlock.transform;
        Transform parent = rootTransform.parent;
        if (uiCamera == null || parent == null)
        {
            return;
        }

        Vector3 desiredWorldPosition = rootTransform.position;
        desiredWorldPosition.z = uiCamera.transform.position.z + RootPlaneDistanceFromCamera;

        Vector3 desiredLocalPosition = parent.InverseTransformPoint(desiredWorldPosition);
        rootTransform.localPosition = new Vector3(
            rootTransform.localPosition.x,
            rootTransform.localPosition.y,
            desiredLocalPosition.z);
    }
}
