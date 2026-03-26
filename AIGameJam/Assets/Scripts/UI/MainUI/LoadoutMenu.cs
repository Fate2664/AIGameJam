using System;
using System.Collections.Generic;
using Nova;
using NovaSamples.UIControls;
using UnityEngine;

[Serializable]
public class LoadoutMenu : MonoBehaviour
{
    private static readonly List<LoadoutItemDefinition> EmptyLoadout = new();

    public static LoadoutMenu Instance;

    public UIBlock Root = null;
    public GridManager GridManager = null;
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
    }

    private void Start()
    {
        currentBudget = StartingBudget;

        LoadoutList.AddDataBinder<LoadoutItemDefinition, LoadoutItemVisuals>(BindLoadoutItem);
        Root.AddGestureHandler<Gesture.OnHover, LoadoutItemVisuals>(HandleItemHover);
        Root.AddGestureHandler<Gesture.OnUnhover, LoadoutItemVisuals>(HandleItemUnhover);
        Root.AddGestureHandler<Gesture.OnPress, LoadoutItemVisuals>(HandleItemPress);
        Root.AddGestureHandler<Gesture.OnRelease, LoadoutItemVisuals>(HandleItemRelease);
        Root.AddGestureHandler<Gesture.OnDrag, LoadoutItemVisuals>(HandleItemDrag);
        Root.AddGestureHandler<Gesture.OnCancel, LoadoutItemVisuals>(HandleItemCancel);
        Root.AddGestureHandler<Gesture.OnClick, LoadoutItemVisuals>(HandleItemClick);

        if (GridManager != null)
        {
            GridManager.SelectedRotationChanged += HandleSelectedRotationChanged;
        }

        LoadoutList.SetDataSource(CurrentLoadout);
        HideDragPreview();
        RefreshBudgetVisual();
        RefreshVisibleItems();
    }

    private void OnDisable()
    {
        if (GridManager != null)
        {
            GridManager.SelectedRotationChanged -= HandleSelectedRotationChanged;
        }

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
        if (draggedItem == null || GridManager == null || GridManager.SelectedPlaceablePrefab == null || !CanAfford(draggedItem))
        {
            return false;
        }

        if (!GridManager.TryGetHoveredCell(out CellVisuals hoveredCell))
        {
            return false;
        }

        if (!GridManager.CanPlaceSelectedAt(hoveredCell))
        {
            return false;
        }

        bool placed = GridManager.TryPlaceSelected(hoveredCell);
        if (!placed)
        {
            return false;
        }

        if (EnforceBudget)
        {
            currentBudget = Mathf.Max(0, currentBudget - draggedItem.Cost);
            RefreshBudgetVisual();
            BudgetChanged?.Invoke(currentBudget);
        }

        RefreshVisibleItems();
        ValidateSelectedItem();
        ItemPlaced?.Invoke(draggedItem, hoveredCell);
        return true;
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

        if (GridManager != null)
        {
            GridManager.SetSelectedPlaceable(selectedItem, true);
        }

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
        DragPreviewRoot.transform.rotation = GridManager != null ? GridManager.SelectedRotation : Quaternion.identity;

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
        if (GridManager != null)
        {
            GridManager.SetSelectedPlaceable(selectedItem, allowClickPlacement);
        }
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
            if (!isDragging && GridManager != null)
            {
                GridManager.ClearSelectedPlaceable();
            }

            return;
        }

        if (!isDragging && GridManager != null)
        {
            GridManager.SetSelectedPlaceable(selectedItem, true);
        }
    }

    private void HandleSelectedRotationChanged(float rotationDegrees)
    {
        if (!isDragging || DragPreviewRoot == null)
        {
            return;
        }

        DragPreviewRoot.transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
    }
}
