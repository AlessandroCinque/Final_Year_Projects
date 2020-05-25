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

    int chunkSize;
    int chunckVisibleInViewDst;

    Dictionary<Vector2, TerrainChunck> terrainChunckDictionary = new Dictionary<Vector2, TerrainChunck>();
    static List<TerrainChunck> terrainChunckVisibleLastUpdate = new List<TerrainChunck>();
    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDst = detailLeveles[detailLeveles.Length - 1].visibleDstThreshold;
        chunkSize = mapGenerator.mapChunckSize - 1;
        chunckVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);
        UpdateVisibleChuncks();
    }
    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z)/mapGenerator.terrainData.uniformScale;
        if (viewerPosition != viewerPositionOld)
        {
            foreach (TerrainChunck chucnk in terrainChunckVisibleLastUpdate)
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
        for (int i = 0; i < terrainChunckVisibleLastUpdate.Count; i++)
        {
            terrainChunckVisibleLastUpdate[i].SetVisible(false);
        }
        terrainChunckVisibleLastUpdate.Clear();

        int currentChunckCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunckCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunckVisibleInViewDst; yOffset <= chunckVisibleInViewDst; yOffset++)
        {
            for (int xOffset = -chunckVisibleInViewDst; xOffset < chunckVisibleInViewDst; xOffset++)
            {
                Vector2 viewedChunckCoord = new Vector2(currentChunckCoordX + xOffset, currentChunckCoordY + yOffset);

                if (terrainChunckDictionary.ContainsKey(viewedChunckCoord))
                {
                    terrainChunckDictionary[viewedChunckCoord].UpdateTerrainChunk();
                 
                }
                else
                {
                    terrainChunckDictionary.Add(viewedChunckCoord, new TerrainChunck(viewedChunckCoord, chunkSize, detailLeveles, colliderLODIndex, transform, mapMaterial));
                }
            }
        }
    }
    public class TerrainChunck
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLeveles;
        LODMesh[] lodMeshes;
        int colliderLODIndex;

        MapData mapData;
        bool mapDataReceived;
        int previousLODIndex = -1;

        bool hasSetCollider;
        public TerrainChunck(Vector2 coord, int size,LODInfo[] detailLeveles,int colliderLODIndex, Transform parent,Material material)
        {
            this.detailLeveles = detailLeveles;
            this.colliderLODIndex = colliderLODIndex;
            position = coord * size;
            bounds = new Bounds(position,Vector2.one*size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunck");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * mapGenerator.terrainData.uniformScale;

            meshObject.transform.parent = parent;

            meshObject.transform.localScale = Vector3.one * mapGenerator.terrainData.uniformScale;
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

            mapGenerator.RequestMapData(position,OnMapDataReceived);
        }


        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (mapDataReceived)
            {


                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
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
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    terrainChunckVisibleLastUpdate.Add(this);
                }
                SetVisible(visible);
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
                        lodMeshes[colliderLODIndex].RequestMesh(mapData);
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
        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData,lod, OnMeshDataReceived);
        }
    }
    //Setting all the different areas a their level of detail, especially the range which they can be seen from!
    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDstThreshold;
        public bool useForCollider;

        public float sqrVisibleDistanceThreshold
        {
            get { return visibleDstThreshold * visibleDstThreshold; }
        }
    }
}

