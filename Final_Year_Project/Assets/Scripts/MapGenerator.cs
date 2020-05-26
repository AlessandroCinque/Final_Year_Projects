using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, Mesh,FalloffMap };
    public DrawMode drawMode;

    public MeshSettings meshSettings;
    public HeightMap_Settings heightMapSettings;
    public TextureData textureData;

    public Material terrainMaterial;

  


    [Range(0, Mesh_Generator1.numSupportedLODs - 1)]
    public int levelPreviewLOD;

    public bool autoUpdate;
    
    float[,] fallOffMap;
    Queue<MapThreadInfo<HeightMap>> heightMapThreadInfoQueue = new Queue<MapThreadInfo<HeightMap>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    void Awake()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeight(terrainMaterial, meshSettings.minHeight, meshSettings.maxHeight);
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
 
    public void DrawMapInEditor()
    {
        textureData.UpdateMeshHeight(terrainMaterial, meshSettings.minHeight, meshSettings.maxHeight);
        HeightMap heightMap = GenerateHeightMap(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(heightMap.values));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(Mesh_Generator1.GenerateTerrainMesh(heightMap.values, meshSettings.meshHeightMultiplier, meshSettings.meshHeightCurve, levelPreviewLOD, meshSettings.useFlatShading));
        }
        else if (drawMode == DrawMode.FalloffMap)
        {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunckSize)));
        }
    }

    public void RequestHeightMap(Vector2 centre,Action<HeightMap> callback)
    {
        //textureData.UpdateMeshHeight(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        ThreadStart threadStart = delegate
        {
            HeightMapThread(centre,callback);
        };
        new Thread(threadStart).Start();
    }
    void HeightMapThread(Vector2 centre, Action<HeightMap> callback)
    {
        HeightMap heightMap = GenerateHeightMap(centre);
        // Preventing any other threads to execute this code while another thread is already executing it!!!!
        lock (heightMapThreadInfoQueue)
        {
            heightMapThreadInfoQueue.Enqueue(new MapThreadInfo<HeightMap>(callback, heightMap));
        }
        
    }

    public void RequestMeshData(HeightMap heightMap,int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(heightMap, lod, callback);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(HeightMap heightMap, int lod, Action<MeshData> callback)
    {
        MeshData meshData = Mesh_Generator1.GenerateTerrainMesh(heightMap.values, meshSettings.meshHeightMultiplier, meshSettings.meshHeightCurve,lod, meshSettings.useFlatShading);
        // Preventing any other threads to execute this code while another thread is already executing it!!!!
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }

    }
    private void Update()
    {
        if (heightMapThreadInfoQueue.Count> 0)
        {
            for (int i = 0; i < heightMapThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<HeightMap> threadInfo = heightMapThreadInfoQueue.Dequeue();
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


    private void OnValidate()
    {
        if (meshSettings!=null)
        {
            //If I am not subscribet this will make nothing happen
            meshSettings.OnValuesUpdated -= OnValuesUpdated;
            // but if I am already susbscribed the line above would limit the subscription to one 
            meshSettings.OnValuesUpdated += OnValuesUpdated;
        }
        if (heightMapSettings != null)
        {
            heightMapSettings.OnValuesUpdated -= OnValuesUpdated;
            heightMapSettings.OnValuesUpdated += OnValuesUpdated;
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

