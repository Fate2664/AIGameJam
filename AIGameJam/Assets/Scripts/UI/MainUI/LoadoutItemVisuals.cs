using System;
using DG.Tweening;
using Nova;
using UnityEngine;

[Serializable]
public class LoadoutItemVisuals : ItemVisuals
{
    public UIBlock2D Root;
    public UIBlock2D Background;
    public UIBlock2D ItemImage;
    public TextBlock CostText;

    public Color DefaultColor = Color.white;
    public Color HoveredColor = Color.white;
    public Color PressedColor = Color.gray;
    public Color DraggingColor = new(0.85f, 0.85f, 0.85f, 1f);
    public Color UnavailableColor = new(0.25f, 0.25f, 0.25f, 1f);
    public Color AvailableTextColor = Color.white;
    public Color UnavailableTextColor = new(1f, 1f, 1f, 0.45f);
    public Color AvailableImageTint = Color.white;
    public Color UnavailableImageTint = new(1f, 1f, 1f, 0.45f);
    public float HoverScale = 1.05f;
    public float TweenDuration = 0.12f;

    [NonSerialized] private LoadoutItemDefinition boundItem;
    private bool isHovered;
    private bool isPressed;
    private bool isDragging;
    private bool isAffordable = true;

    public LoadoutItemDefinition BoundItem => boundItem;

    private Transform AnimatedTransform
    {
        get
        {
            if (Root != null)
            {
                return Root.transform;
            }

            if (Background != null)
            {
                return Background.transform;
            }

            return View != null ? View.transform : null;
        }
    }

    public void Bind(LoadoutItemDefinition item, bool affordable)
    {
        boundItem = item;
        isHovered = false;
        isPressed = false;
        isDragging = false;
        isAffordable = affordable;

        if (ItemImage != null)
        {
            if (item != null && item.Icon != null)
            {
                ItemImage.SetImage(item.Icon);
            }
            else
            {
                ItemImage.ClearImage();
            }
        }

        if (CostText != null)
        {
            CostText.Text = item != null ? item.Cost.ToString() : string.Empty;
        }

        ApplyScale(Vector3.one, true);
        RefreshVisual(true);
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        if (!hovered)
        {
            isPressed = false;
        }

        ApplyScale(hovered && !isDragging ? Vector3.one * HoverScale : Vector3.one);
        RefreshVisual();
    }

    public void SetPressed(bool pressed)
    {
        isPressed = pressed;
        RefreshVisual();
    }

    public void SetDragging(bool dragging)
    {
        isDragging = dragging;
        if (dragging)
        {
            isPressed = false;
            isHovered = false;
        }

        ApplyScale(Vector3.one);
        RefreshVisual();
    }

    public void SetAffordable(bool affordable)
    {
        isAffordable = affordable;
        RefreshVisual();
    }

    private void RefreshVisual(bool immediate = false)
    {
        if (Background != null)
        {
            Color backgroundColor = ResolveBackgroundColor();
            DOTween.Kill(Background);

            if (immediate || TweenDuration <= 0f)
            {
                Background.Color = backgroundColor;
            }
            else
            {
                DOTween.To(() => Background.Color, color => Background.Color = color, backgroundColor, TweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetTarget(Background);
            }
        }

        if (ItemImage != null)
        {
            ItemImage.Color = isAffordable ? AvailableImageTint : UnavailableImageTint;
        }

        if (CostText != null)
        {
            CostText.Color = isAffordable ? AvailableTextColor : UnavailableTextColor;
        }
    }

    private Color ResolveBackgroundColor()
    {
        if (!isAffordable)
        {
            return UnavailableColor;
        }

        if (isDragging)
        {
            return DraggingColor;
        }

        if (isPressed)
        {
            return PressedColor;
        }

        if (isHovered)
        {
            return HoveredColor;
        }

        return DefaultColor;
    }

    private void ApplyScale(Vector3 targetScale, bool immediate = false)
    {
        Transform animatedTransform = AnimatedTransform;
        if (animatedTransform == null)
        {
            return;
        }

        animatedTransform.DOKill();
        if (immediate || TweenDuration <= 0f)
        {
            animatedTransform.localScale = targetScale;
            return;
        }

        animatedTransform.DOScale(targetScale, TweenDuration).SetEase(Ease.OutQuad);
    }
}
