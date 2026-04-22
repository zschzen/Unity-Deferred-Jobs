using UnityEngine;

namespace Thread.Core
{
    /// <summary>
    /// Represents a single tile on the grid.
    /// Uses MaterialPropertyBlock to update colors efficiently without breaking batching
    /// or leaking memory through instanced Materials.
    /// </summary>
    public class GridTile : MonoBehaviour
    {
        public MeshRenderer meshRenderer;

        private static Grid grid;
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock propBlock;

        private int gridX;
        private int gridY;

        private void Awake()
        {
            if (!grid)
            {
                grid = FindObjectOfType<Grid>();
            }

            propBlock = new MaterialPropertyBlock();
        }

        public void Initialize(Vector3 position, Material material)
        {
            transform.position = position;
            meshRenderer.material = material; // Initial base material assignment
        }

        private void Start()
        {
            // Precompute grid coordinates for the checkered pattern logic
            gridX = Mathf.RoundToInt(transform.position.x / grid.tileSpacing);
            gridY = Mathf.RoundToInt(transform.position.z / grid.tileSpacing);

            SetColor(Color.white);
        }

        /// <summary>
        /// Updates the tile's color using a MaterialPropertyBlock for performance.
        /// </summary>
        public void SetColor(Color color)
        {
            // Checkered pattern dimming
            bool isEvenTile = ((gridX + gridY) & 1) == 0;
            Color finalColor = isEvenTile ? color : color * 0.85f;

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorPropertyId, finalColor);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }
}
