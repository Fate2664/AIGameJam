using System;
using DG.Tweening;
using Nova;
using UnityEngine;

[Serializable]
public class PlotVisuals : ItemVisuals
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

    private void OnEnable()
    {
        RefreshVisual(true);
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

    private void RefreshVisual(bool immediate = false)
    {
        if (Background == null)
        {
            return;
        }

        Color targetColor = ResolveColor();
        DOTween.Kill(Background);

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
