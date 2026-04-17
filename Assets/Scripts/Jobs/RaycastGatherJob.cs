using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Thread.Jobs
{
    /// <summary>
    /// Gathers the results of the batched raycasts.
    /// Updates the exposure map and draws debug lines to visualize the outcome.
    /// </summary>
    [BurstCompile]
    public struct RaycastGatherJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RaycastCommand> Commands;
        [ReadOnly] public NativeArray<RaycastHit> Results;

        // Allows us to write to specific indices in parallel without unity complaining about race conditions
        [NativeDisableParallelForRestriction] public NativeArray<bool> ExposureMap;

        public int TimeSliceBaseIndex;

        public void Execute(int index)
        {
            int globalIndex = index + TimeSliceBaseIndex;

            // If distance <= 0, the raycast didn't hit anything (the tile is exposed to the actor)
            bool exposed = (Results[index].distance <= 0.0f);
            ExposureMap[globalIndex] = exposed;

#if UNITY_EDITOR
            // Visual Debugging
            var command = Commands[index];
            Color color = exposed ? Color.red : Color.green;
            Debug.DrawLine(command.from, command.from + command.direction * command.distance, color);
#endif
        }
    }
}
