using UnityEngine;
using Unity.Collections;
using UnityEngine.AI;
using Unity.Jobs;
using Thread.Jobs;

namespace Thread.Core
{
    /// <summary>
    /// The Runner class uses the generated ExposureMap to hide from the Actor.
    /// It queries the Job System to find the nearest safe tile.
    /// </summary>
    public class Runner : MonoBehaviour
    {
        [Header("Settings")]
        public float speed = 2f;
        public float detectionRadius = 10f;

        [Header("References")]
        private Grid grid;
        private Actor actor;
        private NavMeshAgent agent;

        // Cached positions for Job System efficiency
        private NativeArray<Vector3> gridPosNative;

        private void Start()
        {
            actor = FindObjectOfType<Actor>();
            grid = FindObjectOfType<Grid>();
            agent = GetComponent<NavMeshAgent>();

            // Cache grid positions persistently
            var gridPos = grid.gridTiles.ConvertAll(tile => tile.transform.position).ToArray();
            gridPosNative = new NativeArray<Vector3>(gridPos, Allocator.Persistent);
        }

        private void LateUpdate()
        {
            RunToNearestNotExposedTile();
        }

        private void RunToNearestNotExposedTile()
        {
            Vector3 nearestTile = FindNearestNotExposedTile();
            if (nearestTile != Vector3.zero)
            {
                agent.SetDestination(nearestTile);
            }
        }

        /// <summary>
        /// Schedules a Job to find the nearest non-exposed tile.
        /// Demonstrates synchronous Job execution using .Complete().
        /// </summary>
        private Vector3 FindNearestNotExposedTile()
        {
            // Validate the exposure map matches our grid points
            if (grid == null || grid.exposureMap == null || grid.exposureMap.Length != gridPosNative.Length) 
                return Vector3.zero;

            // Prepare temporary data for the Job
            var exposureMapNative = new NativeArray<bool>(grid.exposureMap, Allocator.TempJob);
            var nearestTileNative = new NativeArray<Vector3>(1, Allocator.TempJob);
            nearestTileNative[0] = Vector3.zero;

            var job = new FindNearestNotExposedTileJob
            {
                gridPos = gridPosNative,
                exposureMap = exposureMapNative,
                runnerPos = transform.position,
                nearestTile = nearestTileNative
            };

            // Run synchronously since we need the result this frame
            job.Schedule().Complete();

            Vector3 nearestTile = nearestTileNative[0];

            // Dispose temporary memory
            exposureMapNative.Dispose();
            nearestTileNative.Dispose();

            return nearestTile;
        }
        
        private void OnDestroy()
        {
            if (gridPosNative.IsCreated) 
            {
                gridPosNative.Dispose();
            }
        }
    }
}
