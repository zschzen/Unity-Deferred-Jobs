using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Thread.Jobs
{
    /// <summary>
    /// Prepares RaycastCommands for the Job System.
    /// Burst compiler optimizes the struct for native code execution.
    /// </summary>
    [BurstCompile]
    public struct RaycastSetupJob : IJobParallelFor
    {
        public Vector3 EyePos;
        public float RaycastHeight;

        [ReadOnly] public NativeArray<Vector3> GridPositions;
        [ReadOnly] public LayerMask LayerMask;

        [WriteOnly] public NativeArray<RaycastCommand> Commands;

        public int TimeSliceBaseIndex;

        public void Execute(int index)
        {
            // Global index handles time slicing offset
            int globalIndex = index + TimeSliceBaseIndex;

            Vector3 cellCenter = GridPositions[globalIndex] + Vector3.up * RaycastHeight;
            Vector3 directionVector = cellCenter - EyePos;
            float distance = directionVector.magnitude;

            // Prepare a command for the RaycastCommand.ScheduleBatch
            Commands[index] = new RaycastCommand(
                EyePos, 
                directionVector.normalized, 
                new QueryParameters(LayerMask), 
                distance
            );
        }
    }
}
