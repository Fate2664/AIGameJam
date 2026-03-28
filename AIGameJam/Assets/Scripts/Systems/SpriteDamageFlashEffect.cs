using System;
using System.Collections;
using UnityEngine;

public sealed class SpriteDamageFlashEffect
{
    private readonly MonoBehaviour host;
    private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private Color[] defaultColors = Array.Empty<Color>();
    private Coroutine flashCoroutine;

    public SpriteDamageFlashEffect(MonoBehaviour host)
    {
        this.host = host;
    }

    public void CacheRenderers(SpriteRenderer[] renderers)
    {
        spriteRenderers = renderers ?? Array.Empty<SpriteRenderer>();
        defaultColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            defaultColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }
    }

    public void Play(Color flashColor, float duration, float flashSpeed)
    {
        if (host == null || spriteRenderers.Length == 0)
        {
            return;
        }

        Stop();
        flashCoroutine = host.StartCoroutine(FlashRoutine(flashColor, Mathf.Max(0f, duration), Mathf.Max(0.01f, flashSpeed)));
    }

    public void Stop()
    {
        if (host != null && flashCoroutine != null)
        {
            host.StopCoroutine(flashCoroutine);
        }

        flashCoroutine = null;
        RestoreDefaultColors();
    }

    private IEnumerator FlashRoutine(Color flashColor, float duration, float flashSpeed)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            SetRendererColors(flashColor);
            yield return new WaitForSeconds(flashSpeed);
            RestoreDefaultColors();
            yield return new WaitForSeconds(flashSpeed);
            elapsed += flashSpeed * 2f;
        }

        RestoreDefaultColors();
        flashCoroutine = null;
    }

    private void SetRendererColors(Color color)
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = color;
            }
        }
    }

    private void RestoreDefaultColors()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && i < defaultColors.Length)
            {
                spriteRenderers[i].color = defaultColors[i];
            }
        }
    }
}
