using DG.Tweening;
using Nova;
using UnityEngine;

public class IndicatorManager : MonoBehaviour
{
    [SerializeField] private UIBlock2D indicator;
    [SerializeField] private float scaleDuration = 0.5f;
    
    
    void Start()
    {
        indicator.transform.localScale = Vector3.zero;
        indicator.transform.DOLocalMoveY(1.2f, 1.0f).SetLoops(-1,  LoopType.Yoyo).From(0.9f).SetEase(Ease.InOutQuad);
    }

    public void ShowIndictor()
    {
        indicator.transform.DOScale(Vector3.one, scaleDuration).SetEase(Ease.OutCubic);
    }

    public void HideIndictor()
    {
        indicator.transform.DOScale(Vector3.zero, scaleDuration).SetEase(Ease.OutCubic);
    }

    public void Destroy()
    {
        DOTween.Kill(this);
    }
}
