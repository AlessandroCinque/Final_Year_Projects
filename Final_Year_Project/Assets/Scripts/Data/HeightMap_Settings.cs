using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class HeightMap_Settings : UpdatableData
{
    public NoiseSettings noiseSettings;


    public bool useFalloff;

    public float heightMultiplier;
    public AnimationCurve heightCurve;
    public float minHeight
    {
        get
        {
            return heightMultiplier * heightCurve.Evaluate(0);
        }
    }

    public float maxHeight
    {
        get
        {
            return heightMultiplier * heightCurve.Evaluate(1);
        }
    }
#if UNITY_EDITOR
    protected override void OnValidate()
    {
        noiseSettings.ValidateValues();
        //Just for make sure it gets call when also it gets called in UpdatableData
        base.OnValidate();
    }
#endif
}
