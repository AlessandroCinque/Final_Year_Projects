using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{
    public Color[] baseColor;
    [Range(0,1)]
    public float[] baseStartHeights;

    float savedMinHeight;
    float savedMaxHeight;
    public void ApplyToMaterial(Material material)
    {
        material.SetInt("baseColourCount", baseColor.Length);
        material.SetColorArray("baseColours", baseColor);
        material.SetFloatArray("baseStartHeights", baseStartHeights);
        UpdateMeshHeight(material,savedMinHeight,savedMaxHeight);
    }
    public void UpdateMeshHeight(Material material, float minHeight, float maxHeight)
    {
        savedMinHeight = minHeight;
        savedMaxHeight = maxHeight;
        material.SetFloat("minHeight", minHeight);
        material.SetFloat("maxHeight", maxHeight);
    }
}
