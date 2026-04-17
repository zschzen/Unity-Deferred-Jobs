using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Thread.Jobs
{
    /// <summary>
    /// Finds the nearest tile that is not exposed to the actor.
    /// We use IJob (instead of IJobParallelFor) because we are performing a global reduction (finding a single minimum).
    /// </summary>
    [BurstCompile]
    public struct FindNearestNotExposedTileJob : IJob
    {
        [ReadOnly] public NativeArray<Vector3> gridPos;
        [ReadOnly] public NativeArray<bool> exposureMap;
        
        public Vector3 runnerPos;
        
        // Single element array to store the result of the reduction
        public NativeArray<Vector3> nearestTile;

        public void Execute()
        {
            Vector3 nearest = Vector3.zero;
            float nearestDistanceSq = float.MaxValue;
            int length = gridPos.Length;

            for (int i = 0; i < length; i++)
            {
                // Skip exposed tiles
                if (exposureMap[i]) continue;

                Vector3 tilePos = gridPos[i];
                float distanceSq = (tilePos - runnerPos).sqrMagnitude;
                
                // Track the closest tile
                if (distanceSq < nearestDistanceSq)
                {
                    nearest = tilePos;
                    nearestDistanceSq = distanceSq;
                }
            }

            // Write result back
            nearestTile[0] = nearest;
        }
    }
}
