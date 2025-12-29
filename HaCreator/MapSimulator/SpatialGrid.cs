using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator
{
    /// <summary>
    /// Spatial partitioning grid for efficient object culling.
    /// Divides the map into cells and tracks which objects are in each cell.
    /// </summary>
    /// <typeparam name="T">Type of objects to store</typeparam>
    public class SpatialGrid<T> where T : class
    {
        private readonly int _cellSize;
        private readonly int _gridWidth;
        private readonly int _gridHeight;
        private readonly int _offsetX;
        private readonly int _offsetY;
        private readonly List<T>[] _cells;
        private readonly HashSet<T> _allObjects;

        // Reusable list for query results to avoid allocations
        private readonly List<T> _queryResults;
        private readonly HashSet<T> _querySet;

        /// <summary>
        /// Creates a new spatial grid
        /// </summary>
        /// <param name="mapBounds">The bounds of the map</param>
        /// <param name="cellSize">Size of each cell (default 512px)</param>
        public SpatialGrid(Rectangle mapBounds, int cellSize = 512)
        {
            _cellSize = cellSize;
            _offsetX = mapBounds.X;
            _offsetY = mapBounds.Y;

            // Calculate grid dimensions (add 1 to handle edge cases)
            _gridWidth = (mapBounds.Width / cellSize) + 2;
            _gridHeight = (mapBounds.Height / cellSize) + 2;

            // Initialize cells
            int totalCells = _gridWidth * _gridHeight;
            _cells = new List<T>[totalCells];
            for (int i = 0; i < totalCells; i++)
            {
                _cells[i] = new List<T>(4); // Most cells will have few objects
            }

            _allObjects = new HashSet<T>();
            _queryResults = new List<T>(64);
            _querySet = new HashSet<T>();
        }

        /// <summary>
        /// Gets the cell index for a world position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCellIndex(int worldX, int worldY)
        {
            int cellX = (worldX - _offsetX) / _cellSize;
            int cellY = (worldY - _offsetY) / _cellSize;

            // Clamp to valid range
            cellX = Math.Clamp(cellX, 0, _gridWidth - 1);
            cellY = Math.Clamp(cellY, 0, _gridHeight - 1);

            return cellY * _gridWidth + cellX;
        }

        /// <summary>
        /// Gets cell coordinates for a world position
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int cellX, int cellY) GetCellCoords(int worldX, int worldY)
        {
            int cellX = (worldX - _offsetX) / _cellSize;
            int cellY = (worldY - _offsetY) / _cellSize;
            return (Math.Clamp(cellX, 0, _gridWidth - 1), Math.Clamp(cellY, 0, _gridHeight - 1));
        }

        /// <summary>
        /// Adds an object at the specified position
        /// </summary>
        public void Add(T obj, int x, int y)
        {
            if (obj == null || _allObjects.Contains(obj))
                return;

            int cellIndex = GetCellIndex(x, y);
            _cells[cellIndex].Add(obj);
            _allObjects.Add(obj);
        }

        /// <summary>
        /// Adds an object that spans a rectangular area
        /// </summary>
        public void Add(T obj, Rectangle bounds)
        {
            if (obj == null || _allObjects.Contains(obj))
                return;

            var (startCellX, startCellY) = GetCellCoords(bounds.Left, bounds.Top);
            var (endCellX, endCellY) = GetCellCoords(bounds.Right, bounds.Bottom);

            for (int cy = startCellY; cy <= endCellY; cy++)
            {
                for (int cx = startCellX; cx <= endCellX; cx++)
                {
                    int cellIndex = cy * _gridWidth + cx;
                    _cells[cellIndex].Add(obj);
                }
            }

            _allObjects.Add(obj);
        }

        /// <summary>
        /// Removes an object from all cells
        /// </summary>
        public void Remove(T obj)
        {
            if (obj == null || !_allObjects.Contains(obj))
                return;

            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].Remove(obj);
            }

            _allObjects.Remove(obj);
        }

        /// <summary>
        /// Updates an object's position (for moving objects like mobs/NPCs)
        /// </summary>
        public void Update(T obj, int oldX, int oldY, int newX, int newY)
        {
            int oldIndex = GetCellIndex(oldX, oldY);
            int newIndex = GetCellIndex(newX, newY);

            if (oldIndex != newIndex)
            {
                _cells[oldIndex].Remove(obj);
                _cells[newIndex].Add(obj);
            }
        }

        /// <summary>
        /// Queries all objects visible within the specified view rectangle.
        /// Returns a reusable list - do not store the reference.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<T> Query(Rectangle viewBounds)
        {
            _queryResults.Clear();
            _querySet.Clear();

            var (startCellX, startCellY) = GetCellCoords(viewBounds.Left, viewBounds.Top);
            var (endCellX, endCellY) = GetCellCoords(viewBounds.Right, viewBounds.Bottom);

            for (int cy = startCellY; cy <= endCellY; cy++)
            {
                for (int cx = startCellX; cx <= endCellX; cx++)
                {
                    int cellIndex = cy * _gridWidth + cx;
                    List<T> cell = _cells[cellIndex];

                    for (int i = 0; i < cell.Count; i++)
                    {
                        T obj = cell[i];
                        // Use HashSet to avoid duplicates (objects spanning multiple cells)
                        if (_querySet.Add(obj))
                        {
                            _queryResults.Add(obj);
                        }
                    }
                }
            }

            return _queryResults;
        }

        /// <summary>
        /// Queries objects and copies them to the provided array.
        /// Returns the number of objects found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int QueryToArray(Rectangle viewBounds, T[] outputArray)
        {
            _querySet.Clear();
            int count = 0;

            var (startCellX, startCellY) = GetCellCoords(viewBounds.Left, viewBounds.Top);
            var (endCellX, endCellY) = GetCellCoords(viewBounds.Right, viewBounds.Bottom);

            for (int cy = startCellY; cy <= endCellY; cy++)
            {
                for (int cx = startCellX; cx <= endCellX; cx++)
                {
                    int cellIndex = cy * _gridWidth + cx;
                    List<T> cell = _cells[cellIndex];

                    for (int i = 0; i < cell.Count; i++)
                    {
                        T obj = cell[i];
                        if (_querySet.Add(obj) && count < outputArray.Length)
                        {
                            outputArray[count++] = obj;
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Gets all objects in the grid
        /// </summary>
        public IEnumerable<T> GetAll() => _allObjects;

        /// <summary>
        /// Gets the total number of objects in the grid
        /// </summary>
        public int Count => _allObjects.Count;

        /// <summary>
        /// Clears all objects from the grid
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i].Clear();
            }
            _allObjects.Clear();
        }

        /// <summary>
        /// Gets grid statistics for debugging
        /// </summary>
        public (int totalCells, int occupiedCells, int maxObjectsPerCell) GetStats()
        {
            int occupied = 0;
            int maxObjects = 0;

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Count > 0)
                {
                    occupied++;
                    maxObjects = Math.Max(maxObjects, _cells[i].Count);
                }
            }

            return (_cells.Length, occupied, maxObjects);
        }
    }
}
