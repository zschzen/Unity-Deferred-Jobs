using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Thread.Core
{
    /// <summary>
    /// Generates the grid layout, instantiates tiles, and builds the NavMesh.
    /// Also holds the central exposure map data used by other components.
    /// </summary>
    public class Grid : MonoBehaviour
    {
        [Header("Grid Dimensions")]
        public int rows;
        public int columns;
        public float tileSpacing = 1f;

        [Header("Prefabs & Materials")]
        public GridTile gridTilePrefab;
        public GameObject obstaclePrefab;
        public Material whiteTileMaterial;
        public Material blackTileMaterial;

        [Header("Settings")]
        [Tooltip("Probability of a tile spawning an obstacle")]
        public float obstacleProbability = 0.1f;

        // Tracks all generated tiles
        [HideInInspector] public List<GridTile> gridTiles = new List<GridTile>();

        // Tracks the exposure state (true = exposed, false = hidden)
        [HideInInspector] public bool[] exposureMap = Array.Empty<bool>();

        private readonly Color greenColor = new Color(0.68f, 0.96f, 0.64f, 1f);
        private readonly Color redColor = new Color(0.89f, 0.39f, 0.53f, 1f);

        private void Awake()
        {
            ClearGrid();
            CreateGrid();
            CreateGroundQuadAndNavMesh();
        }

        private void Start()
        {
            // Standard performance target for high-refresh testing
            Application.targetFrameRate = 240;
        }

        /// <summary>
        /// Updates the visual state of the grid tiles based on the Job System's results.
        /// Accepts NativeArray directly for efficient memory usage without allocations.
        /// </summary>
        public void SetExposureMap(NativeArray<bool> map)
        {
            if (exposureMap.Length != map.Length)
            {
                exposureMap = new bool[map.Length];
            }

            // NativeArray's CopyTo is much faster than Linq .ToArray()
            map.CopyTo(exposureMap);

            for (int i = 0; i < gridTiles.Count; i++)
            {
                gridTiles[i].SetColor(exposureMap[i] ? redColor : greenColor);
            }
        }

        private void ClearGrid()
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            gridTiles.Clear();
            exposureMap = Array.Empty<bool>();
        }

        private void CreateGrid()
        {
            bool isWhite = true;

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector3 position = new Vector3(x * tileSpacing, 0f, y * tileSpacing);

                    // Chance to spawn an obstacle instead of a walkable tile
                    if (Random.value < obstacleProbability)
                    {
                        Vector3 obstaclePos = new Vector3(x * tileSpacing, 0.25f, y * tileSpacing);
                        Instantiate(obstaclePrefab, obstaclePos, obstaclePrefab.transform.rotation, transform);
                        continue;
                    }

                    // Spawn grid tile
                    var gridTile = Instantiate(gridTilePrefab, position, gridTilePrefab.transform.rotation, transform);
                    gridTile.Initialize(position, isWhite ? whiteTileMaterial : blackTileMaterial);
                    
                    gridTiles.Add(gridTile);
                    isWhite = !isWhite;
                }
                
                // Offset parity per column to create checkered pattern
                isWhite = !isWhite;
            }
        }

        /// <summary>
        /// Procedurally generates a physics quad and builds a NavMesh surface over the grid.
        /// </summary>
        private void CreateGroundQuadAndNavMesh()
        {
            float quadSize = Mathf.Max(rows, columns) * tileSpacing + 0.0125f;
            Vector3 quadPosition = new Vector3((columns - 1) * tileSpacing / 2f, -0.01f, (rows - 1) * tileSpacing / 2f);

            // Construct Ground Mesh
            Mesh mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-quadSize / 2f, 0f, -quadSize / 2f),
                    new Vector3(-quadSize / 2f, 0f, quadSize / 2f),
                    new Vector3(quadSize / 2f, 0f, quadSize / 2f),
                    new Vector3(quadSize / 2f, 0f, -quadSize / 2f)
                },
                triangles = new[] { 0, 1, 2, 2, 3, 0 },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }
            };

            // Initialize Ground GameObject
            GameObject groundQuad = new GameObject("GroundQuad");
            groundQuad.transform.position = quadPosition;
            groundQuad.transform.parent = transform;
            groundQuad.layer = LayerMask.NameToLayer("Ignore Raycast");
            groundQuad.isStatic = true;

            var meshFilter = groundQuad.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;

            var meshRenderer = groundQuad.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Standard")) { color = Color.white };
            
            // Optimize renderer lighting settings
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightmapIndex = -1;

            var meshCollider = groundQuad.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;

            // Generate NavMesh
            var surface = groundQuad.AddComponent<NavMeshSurface>();
            surface.minRegionArea = 0f;
            surface.overrideTileSize = true;
            surface.tileSize = 256;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Children;

            surface.BuildNavMesh();
        }
    }
}
