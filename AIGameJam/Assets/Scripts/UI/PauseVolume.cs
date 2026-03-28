using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PauseVolume
{
    private Volume pVolume;
    private float sat = 0f;
    private float pExposure = 0f;
    private ColorAdjustments colorAdjustments;

    public PauseVolume(Volume volume, float sateration, float postExposure)
    {
        this.sat = sateration;
        this.pExposure = postExposure;
        this.pVolume = volume;
        if (pVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = 0f;
            colorAdjustments.postExposure.value = 0f;
        }
       
    }

    public void ApplyPauseEffect()
    {
        colorAdjustments.postExposure.value = pExposure;
        colorAdjustments.saturation.value = sat;
    }

    public void RemovePauseEffect()
    {
        colorAdjustments.postExposure.value = 0f;
        colorAdjustments.saturation.value = 0f;
    }


}
