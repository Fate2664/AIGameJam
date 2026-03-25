using System;
using DG.Tweening;
using Nova;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class DialogueVisuals
{
    public UIBlock2D Background;
    public UIBlock2D Icon;
    public UIBlock2D RightArrow;
    public TextBlock DialogueText;
    public TextBlock NameText;
    public float PopinDuration = 0.35f;
    public event UnityAction OnRightArrowPressed;
    
    private bool EventHandlersRegistered = false;
    
    public void InitializeGestureHandlers()
    {
        if (!EventHandlersRegistered)
        {
            RightArrow.AddGestureHandler<Gesture.OnClick>(HandleRightArrowPressed);
            EventHandlersRegistered = true;    
        }
        
    }

    private void HandleRightArrowPressed(Gesture.OnClick evt)
    {
        OnRightArrowPressed.Invoke();
    }

    public void Show()
    {
        Background.Visible = true;
        Background.transform.DOScale(Vector3.one, PopinDuration).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        Background.transform.DOScale(Vector3.zero, PopinDuration).SetEase(Ease.InBack).OnComplete(() =>
        {
            Background.Visible = false;
        });
    }
}
