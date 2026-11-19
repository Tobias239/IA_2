using System;
using System.Collections.Generic;
using UnityEngine;

public class Graph : MonoBehaviour
{
    public static Graph _current { get; private set; }

    [Header("Grid configuration")]
    [SerializeField] private Node _nodePrefab;
    [SerializeField] private int _gridWidth = 10;
    [SerializeField] private int _gridHeight = 10;
    [SerializeField] private float _nodeOffset = 1f;
    [SerializeField] private bool _allowDiagonalMovement = false;

    [Header("Obstacle layer mask")]
    [SerializeField] private LayerMask _obstacleLayerMask;

    [Header("Zone layer masks")]
    [SerializeField] private LayerMask _floorLayerMask;
    [SerializeField] private LayerMask _startLayerMask;
    [SerializeField] private LayerMask _endLayerMask;
    [SerializeField] private LayerMask _safeZoneLayerMask;
    [SerializeField] private float _zoneCheckRadius = 0.45f;

    public event Action<Node, bool> OnNodeBlockedStateChanged;

    private Node[,] _grid;
    private readonly List<Node> _subscribedNodes = new();
    private readonly Dictionary<Node, Action<Node, bool>> _blockedHandlers = new();

    public int Width => _gridWidth;
    public int Height => _gridHeight;
    public float NodeOffset => _nodeOffset;
    public IReadOnlyList<Node> GetAllGridNodes => _subscribedNodes;

    private void Awake()
    {
        _current = this;
        GenerateGrid();
    }

    private void OnDestroy()
    {
        if (_current == this) _current = null;
        UnsubscribeAllNodes();
    }

    public void GenerateGrid()
    {
        UnsubscribeAllNodes();
        ClearExistingChildren();

        if (_nodePrefab == null || _gridWidth <= 0 || _gridHeight <= 0) return;

        _grid = new Node[_gridWidth, _gridHeight];
        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Vector3 worldPos = transform.position + new Vector3(x * _nodeOffset, 0f, y * _nodeOffset);
                Node node = Instantiate(_nodePrefab, worldPos, Quaternion.identity, transform);
                node.name = $"Node_{x}_{y}";
                node.Setup(new Vector2Int(x, y), _obstacleLayerMask);
                node.DetectZone(_floorLayerMask, _startLayerMask, _endLayerMask, _safeZoneLayerMask, _zoneCheckRadius);

                Action<Node, bool> handler = (n, isBlocked) => HandleNodeBlockedChanged(n, isBlocked);
                node.OnBlockedChanged += handler;
                _blockedHandlers[node] = handler;

                _subscribedNodes.Add(node);
                _grid[x, y] = node;
            }
        }
    }

    public void RefreshAllZones()
    {
        if (_grid == null) return;

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Node node = _grid[x, y];
                if (node != null)
                {
                    node.CheckForObstacles();
                    node.DetectZone(_floorLayerMask, _startLayerMask, _endLayerMask,
                        _safeZoneLayerMask, _zoneCheckRadius);
                }
            }
        }
    }

    public Node GetNode(int x, int y)
    {
        if (_grid == null || x < 0 || x >= _gridWidth || y < 0 || y >= _gridHeight) return null;

        return _grid[x, y];
    }

    public Node GetClosestNode(Vector3 worldPosition, bool excludeBlocked = true)
    {
        if (_grid == null) return null;

        Node closest = null;
        float bestDistanceSqr = float.MaxValue;

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Node candidate = _grid[x, y];
                if (candidate == null) continue;
                if (excludeBlocked && candidate.Blocked) continue;

                float distSqr = (candidate.transform.position - worldPosition).sqrMagnitude;
                if (distSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distSqr;
                    closest = candidate;
                }
            }
        }

        return closest;
    }

    public List<Node> GetNodesByZone(ZoneType zone, bool excludeBlocked = true)
    {
        List<Node> matchingNodes = new();
        if (_grid == null || zone == ZoneType.None) return matchingNodes;

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Node node = _grid[x, y];

                if (node == null) continue;
                if (excludeBlocked && node.Blocked) continue;
                if (node.Zone == zone) matchingNodes.Add(node);
            }
        }

        return matchingNodes;
    }

    public List<Node> GetNodesInLayer(LayerMask zoneLayerMask, bool excludeBlocked = true)
    {
        List<Node> matchingNodes = new();
        if (_grid == null || zoneLayerMask.value == 0) return matchingNodes;

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                Node node = _grid[x, y];

                if (node == null) continue;
                if (excludeBlocked && node.Blocked) continue;
                if (node.IsInZone(zoneLayerMask, _zoneCheckRadius))
                    matchingNodes.Add(node);
            }
        }

        return matchingNodes;
    }

    public List<Node> GetNeighbors(Node node, bool includeBlocked = false) =>
        GetNeighbours(node, includeBlocked);

    public List<Node> GetNeighbours(Node node, bool includeBlocked = false)
    {
        List<Node> neighbours = new();
        if (_grid == null || node == null) return neighbours;

        int baseX = node.GridPosition.x;
        int baseY = node.GridPosition.y;

        Vector2Int[] cardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        foreach (Vector2Int dir in cardinalDirections)
        {
            Node candidate = GetNode(baseX + dir.x, baseY + dir.y);
            if (candidate != null && (includeBlocked || !candidate.Blocked)) neighbours.Add(candidate);
        }

        if (_allowDiagonalMovement)
        {
            Vector2Int[] diagonalDirections =
            {
                new(1, 1),
                new(1, -1),
                new(-1, 1),
                new(-1, -1)
            };

            foreach (Vector2Int dir in diagonalDirections)
            {
                Node candidate = GetNode(baseX + dir.x, baseY + dir.y);
                if (candidate == null || (!includeBlocked && candidate.Blocked)) continue;

                Node horiz = GetNode(baseX + dir.x, baseY);
                Node vert = GetNode(baseX, baseY + dir.y);
                bool horizBlocked = horiz == null || horiz.Blocked;
                bool vertBlocked = vert == null || vert.Blocked;

                if (includeBlocked || (!horizBlocked && !vertBlocked)) neighbours.Add(candidate);
            }
        }

        return neighbours;
    }

    public void ResetAllNodes()
    {
        if (_grid == null) return;

        for (int x = 0; x < _gridWidth; x++)
        {
            for (int y = 0; y < _gridHeight; y++)
            {
                _grid[x, y]?.ResetPathfindingData();
            }
        }
    }

    private void HandleNodeBlockedChanged(Node node, bool isBlocked)
    {
        OnNodeBlockedStateChanged?.Invoke(node, isBlocked);
    }

    private void UnsubscribeAllNodes()
    {
        foreach (Node node in _subscribedNodes)
        {
            if (node != null && _blockedHandlers.TryGetValue(node, out var handler))
            {
                node.OnBlockedChanged -= handler;
            }
        }

        _blockedHandlers.Clear();
        _subscribedNodes.Clear();
    }

    private void ClearExistingChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}