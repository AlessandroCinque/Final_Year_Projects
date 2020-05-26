using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class MeshSettings : UpdatableData
{
    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlastShadedChunkSizes = 3;
    public static readonly int[] supportedChunckSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 };
 



    public float meshScale = 2.5f;
    public bool useFlatShading;

    [Range(0, numSupportedChunkSizes - 1)]
    public int chunckSizeIndex;

    [Range(0, numSupportedFlastShadedChunkSizes - 1)]
    public int flastshadedchunckSizeIndex;

    // number of vertices when LOD is 0 + 2 extra vertices that are out of the final mesh
    public int numVertsPerLine
    {
        get
        {
            return supportedChunckSizes[(useFlatShading) ? flastshadedchunckSizeIndex : chunckSizeIndex] + 1;
        
        }
    }
    public float meshWorldSize
    {
        get { return (numVertsPerLine - 3) * meshScale; }
    }
}
