using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, Mesh,FalloffMap };
    public DrawMode drawMode;

    public TerrainData terrainData;
    public HeightMap_Settings noiseData;
    public TextureData textureData;

    public Material terrainMaterial;

    [Range(0, Mesh_Generator1.numSupportedChunkSizes - 1)]
    public int chunckSizeIndex;

    [Range(0, Mesh_Generator1.numSupportedFlastShadedChunkSizes - 1)]
    public int flastshadedchunckSizeIndex;


    [Range(0, Mesh_Generator1.numSupportedLODs - 1)]
    public int levelPreviewLOD;

    public bool autoUpdate;
    
    float[,] fallOffMap;
    Queue<MapThreadInfo<HeightMap>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<HeightMap>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    void Awake()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }
    void OnValuesUpdated()
    {
        if (!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    void OnTextureValuesUpdated()
    {
        textureData.ApplyToMaterial(terrainMaterial);
    }
    public int mapChunckSize
    {
        get
        {
            if (terrainData.useFlatShading)
            {
                return Mesh_Generator1.supportedFlastShadedChunckSizes[flastshadedchunckSizeIndex]-1;
            }
            else
            {
                return Mesh_Generator1.supportedChunckSizes[chunckSizeIndex] -1;
            }
        }
    }
    public void DrawMapInEditor()
    {
        textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        HeightMap mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.values));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(Mesh_Generator1.GenerateTerrainMesh(mapData.values, terrainData.meshHeightMultiplier, terrainData.meshHeightCurve, levelPreviewLOD, terrainData.useFlatShading));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunckSize)));
        }
    }

    public void RequestMapData(Vector2 centre,Action<HeightMap> callback)
    {
        //textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        ThreadStart threadStart = delegate
        {
            MapDataThread(centre,callback);
        };
        new Thread(threadStart).Start();
    }
    void MapDataThread(Vector2 centre, Action<HeightMap> callback)
    {
        HeightMap mapData = GenerateMapData(centre);
        // Preventing any other threads to execute this code while another thread is already executing it!!!!
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<HeightMap>(callback, mapData));
        }
        
    }

    public void RequestMeshData(HeightMap mapData,int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(HeightMap mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData = Mesh_Generator1.GenerateTerrainMesh(mapData.values, terrainData.meshHeightMultiplier, terrainData.meshHeightCurve,lod, terrainData.useFlatShading);
        // Preventing any other threads to execute this code while another thread is already executing it!!!!
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }

    }
    private void Update()
    {
        if (mapDataThreadInfoQueue.Count> 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<HeightMap> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    HeightMap GenerateMapData(Vector2 centre)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunckSize + 2, mapChunckSize + 2, noiseData.seed, noiseData.noiseScale, noiseData.octaves, noiseData.persistance, noiseData.lacunarity, centre + noiseData.offset, noiseData.normalizeMode);
        if (terrainData.useFalloff)
        {
            if (fallOffMap == null)
            {
                fallOffMap = FalloffGenerator.GenerateFalloffMap(mapChunckSize + 2);
            }
            for (int y = 0; y < mapChunckSize +2; y++)
            {
                for (int x = 0; x < mapChunckSize + 2; x++)
                {
                    if (terrainData.useFalloff)
                    {
                        noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - fallOffMap[x, y]);
                    }

                }
            }
          
        }
        
        return new HeightMap(noiseMap);
        
    }
    private void OnValidate()
    {
        if (terrainData!=null)
        {
            //If I am not subscribet this will make nothing happen
            terrainData.OnValuesUpdated -= OnValuesUpdated;
            // but if I am already susbscribed the line above would limit the subscription to one 
            terrainData.OnValuesUpdated += OnValuesUpdated;
        }
        if (noiseData != null)
        {
            noiseData.OnValuesUpdated -= OnValuesUpdated;
            noiseData.OnValuesUpdated += OnValuesUpdated;
        }
        if (textureData != null)
        {
            textureData.OnValuesUpdated -= OnTextureValuesUpdated;
            textureData.OnValuesUpdated += OnTextureValuesUpdated;
        }
    }
    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

