using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    const float viewerMoveThresholdForChunckUpdate = 25f;
    // because in this way is faster. By default for get the distance unity does the square root
    const float sqrViewerMoveThresholdForChunckUpdate = viewerMoveThresholdForChunckUpdate * viewerMoveThresholdForChunckUpdate;
    
    public int colliderLODIndex;
    public LODInfo[] detailLeveles;
    
    public MeshSettings meshSettings;
    public HeightMap_Settings heightMapSettings;
    public TextureData textureSettings;

    public Transform viewer;
    public Material mapMaterial;

    public Vector2 viewerPosition;
    Vector2 viewerPositionOld;

    float meshWorldSize;
    int chunckVisibleInViewDst;

    Dictionary<Vector2, TerrainChunck> terrainChunckDictionary = new Dictionary<Vector2, TerrainChunck>();
    List<TerrainChunck> visibleTerrainChuncks = new List<TerrainChunck>();
    private void Start()
    {
        textureSettings.ApplyToMaterial(mapMaterial);
        textureSettings.UpdateMeshHeight(mapMaterial, heightMapSettings.minHeight, heightMapSettings.maxHeight);

        float maxViewDst = detailLeveles[detailLeveles.Length - 1].visibleDstThreshold;
        meshWorldSize = meshSettings.meshWorldSize;
        chunckVisibleInViewDst = Mathf.RoundToInt(maxViewDst / meshWorldSize);
        UpdateVisibleChuncks();
    }
    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z);
        if (viewerPosition != viewerPositionOld)
        {
            foreach (TerrainChunck chucnk in visibleTerrainChuncks)
            {
                chucnk.UpdateCollisionMesh();
            }
           
        }

        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunckUpdate )
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChuncks();
        }
    }
    void UpdateVisibleChuncks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();

        //Cause we are now removing stuff from this list is better to iterate in the inverse way for avoid indexing errors
        for (int i = visibleTerrainChuncks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChuncks[i].coord);
            visibleTerrainChuncks[i].UpdateTerrainChunk();
        }

        int currentChunckCoordX = Mathf.RoundToInt(viewerPosition.x / meshWorldSize);
        int currentChunckCoordY = Mathf.RoundToInt(viewerPosition.y / meshWorldSize);

        for (int yOffset = -chunckVisibleInViewDst; yOffset <= chunckVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunckVisibleInViewDst; xOffset < chunckVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunckCoord = new Vector2(currentChunckCoordX + xOffset, currentChunckCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunckCoord))
                {
                    if (terrainChunckDictionary.ContainsKey(viewedChunckCoord))
                    {
                        terrainChunckDictionary[viewedChunckCoord].UpdateTerrainChunk();

                    }
                    else
                    {
                        TerrainChunck newChunk = new TerrainChunck(viewedChunckCoord, heightMapSettings,meshSettings, detailLeveles, colliderLODIndex, transform,viewer ,mapMaterial);
                        terrainChunckDictionary.Add(viewedChunckCoord, newChunk);
                        newChunk.onVisibilityChanged += OnTerrainChunkVisibilityChanged;
                        newChunk.Load();
                    }

                }
                
            }
        }
    }
    void OnTerrainChunkVisibilityChanged(TerrainChunck chunk, bool isVisible )
    {
        if (isVisible)
        {
            visibleTerrainChuncks.Add(chunk);
        }
        else
        {
            visibleTerrainChuncks.Remove(chunk);
        }
    }
    //Setting all the different areas a their level of detail, especially the range which they can be seen from!
    
}

[System.Serializable]
public struct LODInfo
{
    [Range(0, MeshSettings.numSupportedLODs - 1)]
    public int lod;
    public float visibleDstThreshold;

    public float sqrVisibleDistanceThreshold
    {
        get { return visibleDstThreshold * visibleDstThreshold; }
    }
}