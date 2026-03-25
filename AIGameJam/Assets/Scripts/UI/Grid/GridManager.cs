using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

public interface IGridPlaceable
{
    void OnPlaced(GridManager gridManager, CellVisuals cell);
    void OnRemoved(GridManager gridManager, CellVisuals cell);
}

public class GridManager : MonoBehaviour
{
    [SerializeField] private UIBlock root = null;
    [SerializeField] private GameInput gameInput = null;
    [SerializeField] private bool autoCollectCells = true;
    [SerializeField] private bool allowReplacingPlacedItems = false;
    [SerializeField] private bool allowRemovingPlacedItems = true;
    [SerializeField] private List<ItemView> cellViews;
    [SerializeField] private GameObject selectedPlaceablePrefab = null;

    private readonly List<CellVisuals> cells = new();
    private readonly Dictionary<CellVisuals, PlacedItemRecord> placedItems = new();
    private bool gesturesRegistered;

    public event Action<CellVisuals> CellClicked;
    public event Action<CellVisuals, GameObject> CellItemPlaced;
    public event Action<CellVisuals, GameObject> CellItemRemoved;

    public IReadOnlyList<CellVisuals> Cells => cells;
    public GameObject SelectedPlaceablePrefab => selectedPlaceablePrefab;

    private struct PlacedItemRecord
    {
        public GameObject Instance;
        public bool DestroyOnRemove;

        public PlacedItemRecord(GameObject instance, bool destroyOnRemove)
        {
            Instance = instance;
            DestroyOnRemove = destroyOnRemove;
        }
    }

    private void Reset()
    {
        if (root == null)
        {
            root = GetComponent<UIBlock>();
        }
    }

    private void Awake()
    {
        if (autoCollectCells)
        {
            CollectCells();
        }
        else
        {
            RebuildCellCache();
        }

        RefreshAllCells();
    }

    private void Start()
    {
        if (gameInput != null)
        {
            gameInput.EnableActions();
        }

        RegisterGestures();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        if (autoCollectCells)
        {
            CollectCells();
        }
        else
        {
            RebuildCellCache();
        }
    }

    public void CollectCells()
    {
        cellViews.Clear();
        ItemView[] childViews = GetComponentsInChildren<ItemView>(true);
        for (int i = 0; i < childViews.Length; i++)
        {
            if (childViews[i].TryGetVisuals<CellVisuals>(out _))
            {
                cellViews.Add(childViews[i]);
            }
        }

        RebuildCellCache();
    }

    public void SetSelectedPlaceable(GameObject placeablePrefab)
    {
        selectedPlaceablePrefab = placeablePrefab;
        RefreshAllCells();
    }

    public void ClearSelectedPlaceable()
    {
        selectedPlaceablePrefab = null;
        RefreshAllCells();
    }

    public bool RegisterCell(ItemView cellView)
    {
        if (cellView == null || cellViews.Contains(cellView) || !cellView.TryGetVisuals(out CellVisuals cell))
        {
            return false;
        }

        cellViews.Add(cellView);
        cells.Add(cell);
        RefreshCell(cell);
        return true;
    }

    public bool UnregisterCell(ItemView cellView)
    {
        if (cellView == null || !cellView.TryGetVisuals(out CellVisuals cell))
        {
            return false;
        }

        TryRemovePlacedItem(cell);
        cells.Remove(cell);
        return cellViews.Remove(cellView);
    }

    public bool IsOccupied(CellVisuals cell)
    {
        return cell != null && placedItems.ContainsKey(cell);
    }

    public bool TryGetPlacedItem(CellVisuals cell, out GameObject placedItem)
    {
        if (cell == null)
        {
            placedItem = null;
            return false;
        }

        if (placedItems.TryGetValue(cell, out PlacedItemRecord placedItemRecord))
        {
            placedItem = placedItemRecord.Instance;
            return true;
        }

        placedItem = null;
        return false;
    }

    public bool CanPlaceSelectedAt(CellVisuals cell)
    {
        return CanPlace(selectedPlaceablePrefab, cell);
    }

    public bool CanPlace(GameObject placeablePrefab, CellVisuals cell)
    {
        if (placeablePrefab == null || cell == null || !cells.Contains(cell))
        {
            return false;
        }

        if (!IsOccupied(cell))
        {
            return true;
        }

        return allowReplacingPlacedItems;
    }

    public bool TryPlaceSelected(CellVisuals cell)
    {
        return TryPlacePrefab(selectedPlaceablePrefab, cell);
    }

    public bool TryPlacePrefab(GameObject placeablePrefab, CellVisuals cell)
    {
        if (!CanPlace(placeablePrefab, cell))
        {
            return false;
        }

        if (IsOccupied(cell))
        {
            TryRemovePlacedItem(cell);
        }

        GameObject instance = Instantiate(placeablePrefab, cell.PlacementAnchor, false);
        HandlePlacement(cell, instance, true);
        return true;
    }

    public bool TryPlaceExisting(GameObject placeableInstance, CellVisuals cell, bool destroyOnRemove = false)
    {
        if (placeableInstance == null || cell == null || !cells.Contains(cell))
        {
            return false;
        }

        if (IsOccupied(cell))
        {
            if (!allowReplacingPlacedItems)
            {
                return false;
            }

            TryRemovePlacedItem(cell);
        }

        placeableInstance.transform.SetParent(cell.PlacementAnchor, false);
        HandlePlacement(cell, placeableInstance, destroyOnRemove);
        return true;
    }

    public bool TryRemovePlacedItem(CellVisuals cell)
    {
        if (cell == null || !placedItems.TryGetValue(cell, out PlacedItemRecord placedItemRecord))
        {
            return false;
        }

        placedItems.Remove(cell);
        GameObject placedItem = placedItemRecord.Instance;

        IGridPlaceable placeableHandler = FindPlaceableHandler(placedItem);
        if (placeableHandler != null)
        {
            placeableHandler.OnRemoved(this, cell);
        }

        CellItemRemoved?.Invoke(cell, placedItem);

        if (placedItem != null)
        {
            if (placedItemRecord.DestroyOnRemove)
            {
                Destroy(placedItem);
            }
            else
            {
                placedItem.transform.SetParent(null, false);
            }
        }

        RefreshCell(cell);
        return true;
    }

    private void RegisterGestures()
    {
        if (gesturesRegistered || root == null)
        {
            return;
        }

        root.AddGestureHandler<Gesture.OnHover, CellVisuals>(HandleCellHover);
        root.AddGestureHandler<Gesture.OnUnhover, CellVisuals>(HandleCellUnhover);
        root.AddGestureHandler<Gesture.OnPress, CellVisuals>(HandleCellPress);
        root.AddGestureHandler<Gesture.OnRelease, CellVisuals>(HandleCellRelease);
        root.AddGestureHandler<Gesture.OnClick, CellVisuals>(HandleCellClick);
        gesturesRegistered = true;
    }

    private void HandleCellHover(Gesture.OnHover evt, CellVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetHovered(true);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandleCellUnhover(Gesture.OnUnhover evt, CellVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(false);
        target.SetHovered(false);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandleCellPress(Gesture.OnPress evt, CellVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(true);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandleCellRelease(Gesture.OnRelease evt, CellVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(false);
    }

    private void HandleCellClick(Gesture.OnClick evt, CellVisuals target)
    {
        if (target == null)
        {
            return;
        }

        CellClicked?.Invoke(target);

        if (selectedPlaceablePrefab != null)
        {
            TryPlaceSelected(target);
            return;
        }

        if (allowRemovingPlacedItems)
        {
            TryRemovePlacedItem(target);
        }
    }

    private void HandlePlacement(CellVisuals cell, GameObject placedItem, bool destroyOnRemove)
    {
        placedItems[cell] = new PlacedItemRecord(placedItem, destroyOnRemove);

        IGridPlaceable placeableHandler = FindPlaceableHandler(placedItem);
        if (placeableHandler != null)
        {
            placeableHandler.OnPlaced(this, cell);
        }

        CellItemPlaced?.Invoke(cell, placedItem);
        RefreshCell(cell);
    }

    private void RefreshAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            RefreshCell(cells[i]);
        }
    }

    private void RefreshCell(CellVisuals cell)
    {
        if (cell == null)
        {
            return;
        }

        cell.SetOccupied(IsOccupied(cell));
        cell.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(cell));
    }

    private void RebuildCellCache()
    {
        cells.Clear();

        for (int i = 0; i < cellViews.Count; i++)
        {
            ItemView cellView = cellViews[i];
            if (cellView != null && cellView.TryGetVisuals(out CellVisuals cell))
            {
                cells.Add(cell);
            }
        }
    }

    private static IGridPlaceable FindPlaceableHandler(GameObject placedItem)
    {
        if (placedItem == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = placedItem.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IGridPlaceable placeableHandler)
            {
                return placeableHandler;
            }
        }

        return null;
    }
}
