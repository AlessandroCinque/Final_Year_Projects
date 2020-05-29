using System.Collections;
using System.Collections.Generic;
using UnityEngine;


    public class TerrainChunck
    {
    const float colliderGenerationDistanceThreshold = 5;
    public event System.Action<TerrainChunck, bool> onVisibilityChanged;
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

        float maxViewDst;
        HeightMap_Settings heightMapSettings;
        MeshSettings meshSettings;
        Transform viewer;
        public TerrainChunck(Vector2 coord, HeightMap_Settings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLeveles, int colliderLODIndex, Transform parent, Transform viewer, Material material)
        {
            this.coord = coord;
            this.detailLeveles = detailLeveles;
            this.colliderLODIndex = colliderLODIndex;
            this.heightMapSettings = heightMapSettings;
            this.meshSettings = meshSettings;
            this.viewer = viewer;

            sampleCentre = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
            Vector2 position = coord * meshSettings.meshWorldSize;
            bounds = new Bounds(position, Vector2.one * meshSettings.meshWorldSize);

            meshObject = new GameObject("Terrain Chunck");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();

            meshRenderer.material = material;

            meshObject.transform.position = new Vector3(position.x, 0, position.y);

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
                    lodMeshes[i].updateCallback += UpdateCollisionMesh;
                }
            }

            maxViewDst = detailLeveles[detailLeveles.Length-1].visibleDstThreshold;
            
        }

        public void Load()
        {
        ThreadedDataRequester.RequestData(() => HeightMap_Generator.GenerateHeightMap(meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, sampleCentre), OnHeightMapReceived);

        }
        void OnHeightMapReceived(object heightMapObject)
        {
            this.heightMap = (HeightMap)heightMapObject;
            heightMapReceived = true;

            UpdateTerrainChunk();
        }
        Vector2 viewerPosition
        {
            get { return new Vector2(viewer.position.x, viewer.position.z); }
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
                            lodMesh.RequestMesh(heightMap, meshSettings);
                        }
                    }


                }

                if (wasVisible != visible)
                {
                    SetVisible(visible);
                    if (onVisibilityChanged != null)
                    {
                        onVisibilityChanged(this, visible);
                    }
                }

            }

        }
        public void UpdateCollisionMesh()
        {
            if (!hasSetCollider)
            {


                float sqrtDstFromViewerToEdge = bounds.SqrDistance(viewerPosition);
                if (sqrtDstFromViewerToEdge < detailLeveles[colliderLODIndex].sqrVisibleDistanceThreshold)
                {
                    if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
                    {
                        lodMeshes[colliderLODIndex].RequestMesh(heightMap, meshSettings);
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
        void OnMeshDataReceived(object meshData)
        {
            mesh = ((MeshData)meshData).CreateMesh();
            hasMesh = true;
            updateCallback();
        }
        public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings)
        {
            hasRequestedMesh = true;
            ThreadedDataRequester.RequestData(() => Mesh_Generator1.GenerateTerrainMesh(heightMap.values, meshSettings, lod), OnMeshDataReceived);
        }
    }

