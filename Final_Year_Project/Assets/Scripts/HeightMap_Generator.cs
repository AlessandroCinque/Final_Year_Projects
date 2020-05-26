using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class HeightMap_Generator
{
    public static HeightMap GenerateHeightMap(int width,int height, HeightMapSettings settings)
    {

    }
}
public struct HeightMap
{
    public readonly float[,] values;
    public readonly float minValue;
    public readonly float maxValue;
    public HeightMap(float[,] values, float minValue, float maxValue)
    {
        this.values = values;
        this.minValue = minValue;
        this.maxValue = maxValue;
    }
}