using UnityEngine;

public class DestroyCurrency : MonoBehaviour
{
    [SerializeField] private CurrencyPickup currencyPickup = null;

    private void Reset()
    {
        if (currencyPickup == null)
        {
            currencyPickup = GetComponent<CurrencyPickup>();
        }
    }

    private void Update()
    {
        if (currencyPickup != null && currencyPickup.IsCollected)
        {
            Destroy(gameObject);
        }
    }
}
