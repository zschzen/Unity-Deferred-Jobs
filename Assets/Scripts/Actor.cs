using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;
using Thread.Jobs;

namespace Thread.Core
{
    /// <summary>
    /// The Actor class handles raycasting across the grid to build an ExposureMap.
    /// It uses Time Slicing and the C# Job System to offload heavy calculations.
    /// </summary>
    public class Actor : MonoBehaviour
    {
        [Header("References")]
        public Grid grid;
        public NavMeshAgent agent;
        public LayerMask layerMask = -1;

        [Header("Visibility Settings")]
        [Min(0f)] public float raycastHeight = 0.25f;

        [Header("Job Settings")]
        [Range(1, 100)] public int JobBatchSize = 100;
        [Range(1, 100)] public int RaysPerFrame = 10;
        public int TimeSliceBaseIndex = 0;

        // Job Handles
        private JobHandle gatherJobHandle;

        // Native Arrays for Job System
        private NativeArray<RaycastCommand> CommandBuffer;
        private NativeArray<RaycastHit> ResultBuffer;
        private NativeArray<bool> ExposureMap;
        private NativeArray<Vector3> GridPositions;

        private LayerMask visibilityMask;
        private bool hasPendingExposureResults;

        public void SetRaysPerFrame(float raysPerFrameRatio)
        {
            float rays = Mathf.Lerp(1, grid.gridTiles.Count, raysPerFrameRatio);
            RaysPerFrame = Mathf.RoundToInt(rays);
        }

        private void Start()
        {
            grid = FindObjectOfType<Grid>();
            agent ??= GetComponent<NavMeshAgent>();
            visibilityMask = BuildVisibilityMask();

            // Cache grid positions for native access across frames
            GridPositions = new NativeArray<Vector3>(grid.gridTiles.Count, Allocator.Persistent);
            for (int i = 0; i < grid.gridTiles.Count; ++i)
            {
                GridPositions[i] = grid.gridTiles[i].transform.position;
            }

            // Initialize the persistent exposure map
            ExposureMap = new NativeArray<bool>(grid.gridTiles.Count, Allocator.Persistent);

            // Kick off an initial full scan
            UpdateExposureMap(grid.gridTiles.Count);
        }

        private void Update()
        {
            if (grid.gridTiles.Count < 1) return;
            
            Profiler.BeginSample("UpdateExposureMap");
            UpdateExposureMap(RaysPerFrame);
            Profiler.EndSample();
        }

        private void LateUpdate()
        {
            // Simple click-to-move logic for the actor
            if (!Input.GetMouseButtonDown(0)) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, NavMesh.AllAreas)) return;
            if (hit.transform.gameObject.name.Contains("Ground")) return;

            agent.SetDestination(hit.transform.position);
        }

        /// <summary>
        /// Schedules the Raycast and Gathering jobs using Time Slicing.
        /// </summary>
        private void UpdateExposureMap(int numRaysPerTimeSlice = 1)
        {
            // Delay result gathering: wait for the previous frame's jobs to finish.
            gatherJobHandle.Complete();

            if (hasPendingExposureResults && ExposureMap.IsCreated)
            {
                // Publish each completed slice immediately so early indices do not lag behind the rest of the grid.
                grid.SetExposureMap(ExposureMap);
                hasPendingExposureResults = false;
            }

            int numCells = grid.gridTiles.Count;
            if (numCells < 1) return;

            if (TimeSliceBaseIndex < 0)
            {
                TimeSliceBaseIndex = 0;
            }

            // Trim excess ray count if we're near the end of the batch
            int numExcessRays = TimeSliceBaseIndex + numRaysPerTimeSlice - numCells;
            numRaysPerTimeSlice -= Mathf.Max(0, numExcessRays);
            if (numRaysPerTimeSlice < 1) return;

            // Clean up temporary data from the previous slice
            if (CommandBuffer.IsCreated) CommandBuffer.Dispose();
            if (ResultBuffer.IsCreated) ResultBuffer.Dispose();

            // Allocate fresh temporary job data for this frame's slice
            CommandBuffer = new NativeArray<RaycastCommand>(numRaysPerTimeSlice, Allocator.TempJob);
            ResultBuffer = new NativeArray<RaycastHit>(numRaysPerTimeSlice, Allocator.TempJob);

            // 1. Setup Job: Prepares the RaycastCommands
            var setupJob = new RaycastSetupJob
            {
                LayerMask = visibilityMask,
                EyePos = transform.position + Vector3.up * raycastHeight,
                GridPositions = GridPositions,
                RaycastHeight = raycastHeight,
                Commands = CommandBuffer,
                TimeSliceBaseIndex = TimeSliceBaseIndex
            };

            // 2. Gather Job: Analyzes the results
            var gatherJob = new RaycastGatherJob
            {
                Commands = CommandBuffer,
                Results = ResultBuffer,
                ExposureMap = ExposureMap,
                TimeSliceBaseIndex = TimeSliceBaseIndex
            };

            // Schedule the job chain
            JobHandle setupJobHandle = setupJob.Schedule(numRaysPerTimeSlice, JobBatchSize);
            
            JobHandle raycastJobHandle = RaycastCommand.ScheduleBatch(
                CommandBuffer, 
                ResultBuffer, 
                JobBatchSize, 
                setupJobHandle
            );

            gatherJobHandle = gatherJob.Schedule(numRaysPerTimeSlice, JobBatchSize, raycastJobHandle);

            // Advance the time slice index
            TimeSliceBaseIndex += numRaysPerTimeSlice;

            // Check if we've reached the end of the grid
            if (TimeSliceBaseIndex >= numCells)
            {
                TimeSliceBaseIndex = -1; // Signal end of batch
            }

            hasPendingExposureResults = true;

            // Kick off the scheduled jobs so worker threads can begin
            JobHandle.ScheduleBatchedJobs();
        }

        private LayerMask BuildVisibilityMask()
        {
            int maskBits = layerMask.value;

            if (grid != null && grid.gridTilePrefab != null)
            {
                maskBits &= ~(1 << grid.gridTilePrefab.gameObject.layer);
            }

            return maskBits;
        }

        private void OnDestroy()
        {
            // Ensure jobs complete before memory is disposed to prevent memory leaks and crashes
            gatherJobHandle.Complete();

            if (GridPositions.IsCreated) GridPositions.Dispose();
            if (ExposureMap.IsCreated) ExposureMap.Dispose();
            if (CommandBuffer.IsCreated) CommandBuffer.Dispose();
            if (ResultBuffer.IsCreated) ResultBuffer.Dispose();
        }
    }
}
