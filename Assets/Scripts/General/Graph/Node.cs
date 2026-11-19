using System;
using UnityEngine;

public enum ZoneType
{
    None,
    Floor,
    Start,
    End,
    SafeZone
}

public class Node : MonoBehaviour
{
    public Vector2Int GridPosition { get; private set; }
    public Coordinates Coordinates { get; private set; }
    public bool Blocked { get; private set; }
    public ZoneType Zone { get; private set; }

    public int Cost { get; set; } = 1;

    public Node Parent { get; set; }
    public int GCost { get; set; }
    public int HCost { get; set; }
    public int FCost => GCost + HCost;

    public event Action<Node, bool> OnBlockedChanged;
    public event Action<Node> OnNodeClicked;
    public event Action<Node> OnNodeRightClicked;

    [Header("Obstacle detection")]
    [SerializeField] private float _obstacleCheckRadius = 0.4f;

    [Header("Visuals")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _blockedColor = Color.red;
    [SerializeField] private Color _floorColor = new(0.4f, 0.9f, 0.5f);
    [SerializeField] private Color _startColor = new(0.3f, 0.6f, 1f);
    [SerializeField] private Color _endColor = new(1f, 0.7f, 0.3f);
    [SerializeField] private Color _safeZoneColor = new(1f, 0.5f, 0.8f);

    private LayerMask _obstacleLayerMask;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
    }

    public void Setup(Vector2Int gridPosition, LayerMask obstacleLayerMask)
    {
        GridPosition = gridPosition;
        Coordinates = new Coordinates(gridPosition.x, gridPosition.y);
        _obstacleLayerMask = obstacleLayerMask;
        Zone = ZoneType.None;
        ResetPathfindingData();
        CheckForObstacles();
        UpdateVisual();
    }

    public void DetectZone(LayerMask floorLayerMask, LayerMask startLayerMask,
        LayerMask endLayerMask, LayerMask safeZoneLayerMask, float checkRadius = 0.45f)
    {
        if (Blocked)
        {
            Zone = ZoneType.None;
            UpdateVisual();
            return;
        }

        if (IsInZone(startLayerMask, checkRadius)) Zone = ZoneType.Start;
        else if (IsInZone(endLayerMask, checkRadius)) Zone = ZoneType.End;
        else if (IsInZone(safeZoneLayerMask, checkRadius)) Zone = ZoneType.SafeZone;
        else if (IsInZone(floorLayerMask, checkRadius)) Zone = ZoneType.Floor;
        else Zone = ZoneType.None;

        UpdateVisual();
    }

    public bool IsInZone(LayerMask zoneLayerMask, float checkRadius = 0.45f)
    {
        if (zoneLayerMask.value == 0) return false;

        return Physics.CheckSphere(transform.position, checkRadius, zoneLayerMask,
            QueryTriggerInteraction.Collide);
    }

    public void CheckForObstacles()
    {
        if (_obstacleLayerMask.value == 0)
        {
            SetBlocked(false);
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, _obstacleCheckRadius,
            _obstacleLayerMask, QueryTriggerInteraction.Ignore);
        SetBlocked(hits != null && hits.Length > 0);
    }

    public void SetBlocked(bool blocked)
    {
        if (Blocked == blocked)
        {
            UpdateVisual();
            return;
        }

        Blocked = blocked;
        if (Blocked) Zone = ZoneType.None;

        UpdateVisual();
        OnBlockedChanged?.Invoke(this, Blocked);
    }

    public void ResetPathfindingData()
    {
        Parent = null;
        GCost = int.MaxValue;
        HCost = 0;
    }

    public void ChangeColor(Color color)
    {
        if (_renderer == null) return;
        _renderer.material.color = color;
    }

    public void ResetVisual() => UpdateVisual();

    private void UpdateVisual()
    {
        if (_renderer == null) return;

        if (Blocked)
        {
            _renderer.material.color = _blockedColor;
            return;
        }

        _renderer.material.color = Zone switch
        {
            ZoneType.Start => _startColor,
            ZoneType.End => _endColor,
            ZoneType.SafeZone => _safeZoneColor,
            ZoneType.Floor => _floorColor,
            _ => _normalColor
        };
    }

    private void OnMouseDown()
    {
        if (Blocked) return;
        OnNodeClicked?.Invoke(this);
    }

    private void OnMouseOver()
    {
        if (Blocked) return;
        if (Input.GetMouseButtonDown(1))
            OnNodeRightClicked?.Invoke(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Blocked ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, _obstacleCheckRadius);
    }
}