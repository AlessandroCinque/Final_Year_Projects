using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    const float viewerMoveThresholdForChunckUpdate = 25f;
    // because in this way is faster. By default for get the distance unity does the square root
    const float sqrViewerMoveThresholdForChunckUpdate = viewerMoveThresholdForChunckUpdate * viewerMoveThresholdForChunckUpdate;
    const float colliderGenerationDistanceThreshold = 5;
    public int colliderLODIndex;
    public LODInfo[] detailLeveles;

    public static float maxViewDst;
    
    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    static MapGenerator mapGenerator;

    float meshWorldSize;
    int chunckVisibleInViewDst;

    Dictionary<Vector2, TerrainChunck> terrainChunckDictionary = new Dictionary<Vector2, TerrainChunck>();
    static List<TerrainChunck> visibleTerrainChuncks = new List<TerrainChunck>();
    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDst = detailLeveles[detailLeveles.Length - 1].visibleDstThreshold;
        meshWorldSize = mapGenerator.meshSettings.meshWorldSize;
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
                chucnk.UpdateCollsionMesh();
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
                        terrainChunckDictionary.Add(viewedChunckCoord, new TerrainChunck(viewedChunckCoord, meshWorldSize, detailLeveles, colliderLODIndex, transform, mapMaterial));
                    }

                }
                
            }
        }
    }
    public class TerrainChunck
    {
        public Vector2 coord;

        GameObject meshObject;
        Vector2 sampleCentre;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLeveles;
        LODMesh[] lodMeshes;
        int colliderLODIndex;

        HeightMap heightMap;
        bool heightMapReceived;
        int previousLODIndex = -1;

        bool hasSetCollider;
        public TerrainChunck(Vector2 coord, float meshWorldSize,LODInfo[] detailLeveles,int colliderLODIndex, Transform parent,Material material)
        {
            this.coord = coord;
            this.detailLeveles = detailLeveles;
            this.colliderLODIndex = colliderLODIndex;
            sampleCentre = coord * meshWorldSize / mapGenerator.meshSettings.meshScale;
            Vector2 position = coord * meshWorldSize;
            bounds = new Bounds(position, Vector2.one*meshWorldSize);

            meshObject = new GameObject("Terrain Chunck");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshRenderer.material = material;

            meshObject.transform.position = new Vector3(position.x,0,position.y);

            meshObject.transform.parent = parent;

            //
            SetVisible(false);

            lodMeshes = new LODMesh[detailLeveles.Length];
            for (int i = 0; i < detailLeveles.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLeveles[i].lod);
                lodMeshes[i].updateCallback += UpdateTerrainChunk;
                if (i == colliderLODIndex)
                {
                    lodMeshes[i].updateCallback += UpdateCollsionMesh;
                }
            }

            mapGenerator.RequestHeightMap(sampleCentre,OnHeightMapReceived);
        }


        void OnHeightMapReceived(HeightMap heightMap)
        {
            this.heightMap = heightMap;
            heightMapReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (heightMapReceived)
            {


                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool wasVisible = IsVisible();
                bool visible = viewerDstFromNearestEdge <= maxViewDst;

                if (visible)
                {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLeveles.Length - 1; i++)
                    {
                        if (viewerDstFromNearestEdge > detailLeveles[i].visibleDstThreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(heightMap);
                        }
                    }

                    
                }

                if (wasVisible != visible)
                {
                    if (visible)
                    {
                        visibleTerrainChuncks.Add(this);
                    }
                    else
                    {
                        visibleTerrainChuncks.Remove(this);
                    }
                    SetVisible(visible);
                }
                
            }
            
        }
        public void UpdateCollsionMesh()
        {
            if (!hasSetCollider)
            {


                float sqrtDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);
                if (sqrtDstFromViewerToEdge < detailLeveles[colliderLODIndex].sqrVisibleDistanceThreshold)
                {
                    if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
                    {
                        lodMeshes[colliderLODIndex].RequestMesh(heightMap);
                    }
                }

                if (sqrtDstFromViewerToEdge < (colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold))
                {
                    if (lodMeshes[colliderLODIndex].hasMesh)
                    {
                        meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                        hasSetCollider = true;

                    }
                }
            }
        }
        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }
        public bool IsVisible()
        {
            return meshObject.activeSelf;
        }
    }
    //Level Of Detail Mesh (LODMesh)
    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        public event System.Action updateCallback;
        public LODMesh(int lod)
        {
            this.lod = lod;
        }
        void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallback();
        }
        public void RequestMesh(HeightMap heightMap)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(heightMap,lod, OnMeshDataReceived);
        }
    }
    //Setting all the different areas a their level of detail, especially the range which they can be seen from!
    [System.Serializable]
    public struct LODInfo
    {
        [Range(0, MeshSettings.numSupportedLODs -1)]
        public int lod;
        public float visibleDstThreshold;

        public float sqrVisibleDistanceThreshold
        {
            get { return visibleDstThreshold * visibleDstThreshold; }
        }
    }
}

