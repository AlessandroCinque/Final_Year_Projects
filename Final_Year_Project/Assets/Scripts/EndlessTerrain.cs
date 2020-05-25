using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{
    const float viewerMoveThresholdForChunckUpdate = 25f;
    // because in this way is faster. By default for get the distance unity does the square root
    const float sqrViewerMoveThresholdForChunckUpdate = viewerMoveThresholdForChunckUpdate * viewerMoveThresholdForChunckUpdate;
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
                    terrainChunckDictionary.Add(viewedChunckCoord, new TerrainChunck(viewedChunckCoord, chunkSize, detailLeveles,transform, mapMaterial));
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
        LODMesh collisionLODMesh;

        MapData mapData;
        bool mapDataReceived;
        int previousLODIndex = -1;
        public TerrainChunck(Vector2 coord, int size,LODInfo[] detailLeveles, Transform parent,Material material)
        {
            this.detailLeveles = detailLeveles;
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
                lodMeshes[i] = new LODMesh(detailLeveles[i].lod,UpdateTerrainChunk);
                if (detailLeveles[i].useForCollider)
                {
                    collisionLODMesh = lodMeshes[i];
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

                    if (lodIndex == 0)
                    {
                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else if(!collisionLODMesh.hasRequestedMesh)
                        {
                            collisionLODMesh.RequestMesh(mapData);
                        }
                    }
                    terrainChunckVisibleLastUpdate.Add(this);
                }
                SetVisible(visible);
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
        System.Action updateCallback;
        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
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
    }
}

