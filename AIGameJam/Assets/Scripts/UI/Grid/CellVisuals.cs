using System;
using DG.Tweening;
using Nova;
using UnityEngine;

[Serializable]
public class CellVisuals : ItemVisuals
{
    public UIBlock2D Background;
    public Transform ItemAnchor;
    public Color DefaultColor = Color.white;
    public Color HoverColor = Color.white;
    public Color PressedColor = Color.gray;
    public Color OccupiedColor = Color.gray;
    public Color InvalidColor = new(0.95f, 0.35f, 0.35f, 1f);
    [Min(0f)] public float ColorTweenDuration = 0.08f;

    private bool isHovered;
    private bool isPressed;
    private bool isOccupied;
    private bool hasPlacementSelection;
    private bool canPlaceSelection = true;
    private bool backgroundVisible = true;

    public Transform PlacementAnchor
    {
        get
        {
            if (ItemAnchor != null)
            {
                return ItemAnchor;
            }

            if (View != null)
            {
                return View.transform;
            }

            return Background != null ? Background.transform : null;
        }
    }
    public bool IsOccupied => isOccupied;
    public bool BackgroundVisible => backgroundVisible;
    public Vector3 WorldPosition => PlacementAnchor != null ? PlacementAnchor.position : Vector3.zero;
    public Vector2 WorldSize
    {
        get
        {
            if (Background == null)
            {
                return Vector2.zero;
            }

            Vector2 localSize = Background.CalculatedSize.XY.Value;
            if (localSize.x <= Mathf.Epsilon || localSize.y <= Mathf.Epsilon)
            {
                Vector3 layoutSize = Background.LayoutSize;
                localSize = new Vector2(layoutSize.x, layoutSize.y);
            }

            Vector3 scale = Background.transform.lossyScale;
            return new Vector2(localSize.x * Mathf.Abs(scale.x), localSize.y * Mathf.Abs(scale.y));
        }
    }

    private void OnEnable()
    {
        RefreshVisual(true);
    }

    public bool ContainsWorldPosition(Vector2 worldPosition, float padding = 0f)
    {
        Vector2 worldSize = WorldSize;
        if (worldSize.x <= Mathf.Epsilon || worldSize.y <= Mathf.Epsilon)
        {
            return false;
        }

        Vector2 extents = (worldSize * 0.5f) + Vector2.one * Mathf.Max(0f, padding);
        Vector2 delta = worldPosition - (Vector2)WorldPosition;
        return Mathf.Abs(delta.x) <= extents.x && Mathf.Abs(delta.y) <= extents.y;
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        if (!hovered)
        {
            isPressed = false;
        }

        RefreshVisual();
    }

    public void ClearInteractionState()
    {
        isHovered = false;
        isPressed = false;
        RefreshVisual();
    }

    public void SetPressed(bool pressed)
    {
        isPressed = pressed;
        RefreshVisual();
    }

    public void SetOccupied(bool occupied)
    {
        isOccupied = occupied;
        RefreshVisual();
    }

    public void SetPlacementPreview(bool hasSelection, bool canPlace)
    {
        hasPlacementSelection = hasSelection;
        canPlaceSelection = canPlace;
        RefreshVisual();
    }

    public void SetBackgroundVisible(bool visible)
    {
        if (backgroundVisible == visible)
        {
            return;
        }

        backgroundVisible = visible;
        RefreshVisual(true);
    }

    private void RefreshVisual(bool immediate = false)
    {
        if (Background == null)
        {
            return;
        }

        Background.Visible = backgroundVisible;
        Color targetColor = ResolveColor();
        DOTween.Kill(Background);

        if (!backgroundVisible)
        {
            return;
        }

        if (immediate || ColorTweenDuration <= 0f)
        {
            Background.Color = targetColor;
            return;
        }

        DOTween.To(() => Background.Color, color => Background.Color = color, targetColor, ColorTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetTarget(Background);
    }

    private Color ResolveColor()
    {
        if (hasPlacementSelection && !canPlaceSelection && (isHovered || isPressed))
        {
            return InvalidColor;
        }

        if (isPressed)
        {
            return PressedColor;
        }

        if (isHovered)
        {
            return HoverColor;
        }

        if (isOccupied)
        {
            return OccupiedColor;
        }

        return DefaultColor;
    }
}
