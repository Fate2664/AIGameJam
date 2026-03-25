using System;
using System.Collections.Generic;
using Nova;
using UnityEngine;

public interface IGridPlaceable
{
    void OnPlaced(GridManager gridManager, PlotVisuals plot);
    void OnRemoved(GridManager gridManager, PlotVisuals plot);
}

public class GridManager : MonoBehaviour
{
    [SerializeField] private UIBlock root = null;
    [SerializeField] private GameInput gameInput = null;
    [SerializeField] private bool autoCollectPlots = true;
    [SerializeField] private bool allowReplacingPlacedItems = false;
    [SerializeField] private bool allowRemovingPlacedItems = true;
    [SerializeField] private List<ItemView> plotViews = new();
    [SerializeField] private GameObject selectedPlaceablePrefab = null;

    private readonly List<PlotVisuals> plots = new();
    private readonly Dictionary<PlotVisuals, PlacedItemRecord> placedItems = new();
    private bool gesturesRegistered;

    public event Action<PlotVisuals> PlotClicked;
    public event Action<PlotVisuals, GameObject> PlotItemPlaced;
    public event Action<PlotVisuals, GameObject> PlotItemRemoved;

    public IReadOnlyList<PlotVisuals> Plots => plots;
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
        if (autoCollectPlots)
        {
            CollectPlots();
        }
        else
        {
            RebuildPlotCache();
        }

        RefreshAllPlots();
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

        if (autoCollectPlots)
        {
            CollectPlots();
        }
        else
        {
            RebuildPlotCache();
        }
    }

    public void CollectPlots()
    {
        plotViews.Clear();
        ItemView[] childViews = GetComponentsInChildren<ItemView>(true);
        for (int i = 0; i < childViews.Length; i++)
        {
            if (childViews[i].TryGetVisuals<PlotVisuals>(out _))
            {
                plotViews.Add(childViews[i]);
            }
        }

        RebuildPlotCache();
    }

    public void SetSelectedPlaceable(GameObject placeablePrefab)
    {
        selectedPlaceablePrefab = placeablePrefab;
        RefreshAllPlots();
    }

    public void ClearSelectedPlaceable()
    {
        selectedPlaceablePrefab = null;
        RefreshAllPlots();
    }

    public bool RegisterPlot(ItemView plotView)
    {
        if (plotView == null || plotViews.Contains(plotView) || !plotView.TryGetVisuals(out PlotVisuals plot))
        {
            return false;
        }

        plotViews.Add(plotView);
        plots.Add(plot);
        RefreshPlot(plot);
        return true;
    }

    public bool UnregisterPlot(ItemView plotView)
    {
        if (plotView == null || !plotView.TryGetVisuals(out PlotVisuals plot))
        {
            return false;
        }

        TryRemovePlacedItem(plot);
        plots.Remove(plot);
        return plotViews.Remove(plotView);
    }

    public bool IsOccupied(PlotVisuals plot)
    {
        return plot != null && placedItems.ContainsKey(plot);
    }

    public bool TryGetPlacedItem(PlotVisuals plot, out GameObject placedItem)
    {
        if (plot == null)
        {
            placedItem = null;
            return false;
        }

        if (placedItems.TryGetValue(plot, out PlacedItemRecord placedItemRecord))
        {
            placedItem = placedItemRecord.Instance;
            return true;
        }

        placedItem = null;
        return false;
    }

    public bool CanPlaceSelectedAt(PlotVisuals plot)
    {
        return CanPlace(selectedPlaceablePrefab, plot);
    }

    public bool CanPlace(GameObject placeablePrefab, PlotVisuals plot)
    {
        if (placeablePrefab == null || plot == null || !plots.Contains(plot))
        {
            return false;
        }

        if (!IsOccupied(plot))
        {
            return true;
        }

        return allowReplacingPlacedItems;
    }

    public bool TryPlaceSelected(PlotVisuals plot)
    {
        return TryPlacePrefab(selectedPlaceablePrefab, plot);
    }

    public bool TryPlacePrefab(GameObject placeablePrefab, PlotVisuals plot)
    {
        if (!CanPlace(placeablePrefab, plot))
        {
            return false;
        }

        if (IsOccupied(plot))
        {
            TryRemovePlacedItem(plot);
        }

        GameObject instance = Instantiate(placeablePrefab, plot.PlacementAnchor, false);
        ResetLocalTransform(instance.transform);
        HandlePlacement(plot, instance, true);
        return true;
    }

    public bool TryPlaceExisting(GameObject placeableInstance, PlotVisuals plot, bool destroyOnRemove = false)
    {
        if (placeableInstance == null || plot == null || !plots.Contains(plot))
        {
            return false;
        }

        if (IsOccupied(plot))
        {
            if (!allowReplacingPlacedItems)
            {
                return false;
            }

            TryRemovePlacedItem(plot);
        }

        placeableInstance.transform.SetParent(plot.PlacementAnchor, false);
        ResetLocalTransform(placeableInstance.transform);
        HandlePlacement(plot, placeableInstance, destroyOnRemove);
        return true;
    }

    public bool TryRemovePlacedItem(PlotVisuals plot)
    {
        if (plot == null || !placedItems.TryGetValue(plot, out PlacedItemRecord placedItemRecord))
        {
            return false;
        }

        placedItems.Remove(plot);
        GameObject placedItem = placedItemRecord.Instance;

        IGridPlaceable placeableHandler = FindPlaceableHandler(placedItem);
        if (placeableHandler != null)
        {
            placeableHandler.OnRemoved(this, plot);
        }

        PlotItemRemoved?.Invoke(plot, placedItem);

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

        RefreshPlot(plot);
        return true;
    }

    private void RegisterGestures()
    {
        if (gesturesRegistered || root == null)
        {
            return;
        }

        root.AddGestureHandler<Gesture.OnHover, PlotVisuals>(HandlePlotHover);
        root.AddGestureHandler<Gesture.OnUnhover, PlotVisuals>(HandlePlotUnhover);
        root.AddGestureHandler<Gesture.OnPress, PlotVisuals>(HandlePlotPress);
        root.AddGestureHandler<Gesture.OnRelease, PlotVisuals>(HandlePlotRelease);
        root.AddGestureHandler<Gesture.OnClick, PlotVisuals>(HandlePlotClick);
        gesturesRegistered = true;
    }

    private void HandlePlotHover(Gesture.OnHover evt, PlotVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetHovered(true);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandlePlotUnhover(Gesture.OnUnhover evt, PlotVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(false);
        target.SetHovered(false);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandlePlotPress(Gesture.OnPress evt, PlotVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(true);
        target.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(target));
    }

    private void HandlePlotRelease(Gesture.OnRelease evt, PlotVisuals target)
    {
        if (target == null)
        {
            return;
        }

        target.SetPressed(false);
    }

    private void HandlePlotClick(Gesture.OnClick evt, PlotVisuals target)
    {
        if (target == null)
        {
            return;
        }

        PlotClicked?.Invoke(target);

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

    private void HandlePlacement(PlotVisuals plot, GameObject placedItem, bool destroyOnRemove)
    {
        placedItems[plot] = new PlacedItemRecord(placedItem, destroyOnRemove);

        IGridPlaceable placeableHandler = FindPlaceableHandler(placedItem);
        if (placeableHandler != null)
        {
            placeableHandler.OnPlaced(this, plot);
        }

        PlotItemPlaced?.Invoke(plot, placedItem);
        RefreshPlot(plot);
    }

    private void RefreshAllPlots()
    {
        for (int i = 0; i < plots.Count; i++)
        {
            RefreshPlot(plots[i]);
        }
    }

    private void RefreshPlot(PlotVisuals plot)
    {
        if (plot == null)
        {
            return;
        }

        plot.SetOccupied(IsOccupied(plot));
        plot.SetPlacementPreview(selectedPlaceablePrefab != null, CanPlaceSelectedAt(plot));
    }

    private void RebuildPlotCache()
    {
        plots.Clear();

        for (int i = 0; i < plotViews.Count; i++)
        {
            ItemView plotView = plotViews[i];
            if (plotView != null && plotView.TryGetVisuals(out PlotVisuals plot))
            {
                plots.Add(plot);
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

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }
}
