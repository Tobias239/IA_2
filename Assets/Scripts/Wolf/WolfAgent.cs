using System;
using System.Collections.Generic;
using UnityEngine;

public class WolfAgent : Agent
{
    public static event Action<WolfAgent, Vector3> OnWolfHowl;

    [Header("Movement")]
    [SerializeField] private float _patrolSpeed;
    [SerializeField] private float _chaseSpeed;
    [SerializeField] private float _investigateSpeed;
    [SerializeField] private float _maxSteering;

    [Header("Perception (FOV + Line of Sight)")]
    [SerializeField] private float _viewRadius;
    [SerializeField, Range(1f, 360f)] private float _viewAngle;
    [SerializeField] private float _eyeHeight;
    [SerializeField] private LayerMask _hunterLayer;
    [SerializeField] private LayerMask _obstacleMask;

    [Header("Capture & Alert")]
    [SerializeField] private float _captureRadius = 1.35f;
    [SerializeField] private float _howlCooldown = 2f;

    [Header("Patrol & Investigation")]
    [SerializeField] private List<Transform> _waypoints = new();
    [SerializeField] private float _waypointReachThreshold;
    [SerializeField] private float _waypointWaitTime;
    [SerializeField] private float _investigateRadius;
    [SerializeField] private float _investigateSearchDuration;
    [SerializeField] private float _repathInterval;

    public StateMachine StateMachine { get; private set; }
    public WolfPatrolState PatrolState { get; private set; }
    public WolfChaseState ChaseState { get; private set; }
    public WolfInvestigateState InvestigateState { get; private set; }
    public HunterAgent TargetHunter { get; private set; }

    public Vector3 LastKnownHunterPos { get; private set; }
    public bool HasLastKnownPos { get; private set; }

    public float PatrolSpeed => _patrolSpeed;
    public float ChaseSpeed => _chaseSpeed;
    public float InvestigateSpeed => _investigateSpeed;
    public float MaxSteering => _maxSteering;
    public float ViewRadius => _viewRadius;
    public float ViewAngle => _viewAngle;
    public float CaptureRadius => _captureRadius;
    public float WaypointReachThreshold => _waypointReachThreshold;
    public float WaypointWaitTime => _waypointWaitTime;
    public float InvestigateRadius => _investigateRadius;
    public float InvestigateSearchDuration => _investigateSearchDuration;
    public float RepathInterval => _repathInterval;
    public List<Transform> Waypoints => _waypoints;

    private Vector3 _currentVelocity;
    private float _howlTimer;
    private bool _hasDetectedHunter;
    private Color _gizmoColor = Color.cyan;

    private void Awake()
    {
        StateMachine = new StateMachine();
        PatrolState = new WolfPatrolState(this);
        ChaseState = new WolfChaseState(this);
        InvestigateState = new WolfInvestigateState(this);
    }

    private void Start()
    {
        if (Graph._current != null && Graph._current.GetAllGridNodes.Count == 0)
        Graph._current.GenerateGrid();

        Node startNode = GetClosestSafeNavigableNode(transform.position);
        if (startNode != null) Reposition(startNode);

        StateMachine.Initialize(PatrolState);
    }

    private void OnEnable()
    {
        OnPathCompleted += HandlePathCompleted;
        OnWolfHowl += HandleOtherWolfHowl;
    }

    protected override void OnDisable()
    {
        OnPathCompleted -= HandlePathCompleted;
        OnWolfHowl -= HandleOtherWolfHowl;
        base.OnDisable();
    }

    private void Update()
    {
        if (_howlTimer > 0f) _howlTimer -= Time.deltaTime;

        StateMachine.Update();
        UpdateDetectionFeedback();
    }

    private void HandlePathCompleted() => StopPathMovement();

    private void HandleOtherWolfHowl(WolfAgent sourceWolf, Vector3 alertedPos)
    {
        if (sourceWolf == null || sourceWolf == this) return;
        if (!gameObject.activeInHierarchy) return;

        Node alertNode = GetClosestSafeNavigableNode(alertedPos);
        if (alertNode == null || alertNode.Zone == ZoneType.SafeZone) return;

        SetLastKnownHunterPosition(alertNode.transform.position);

        if (StateMachine.CurrentState == PatrolState)
        {
            StateMachine.ChangeState(InvestigateState);
        }
        else if (StateMachine.CurrentState == InvestigateState)
        {
            InvestigateState.NotifyNewAlertPosition(alertNode.transform.position);
        }
    }

    public void SetTargetHunter(HunterAgent hunter)
    {
        TargetHunter = hunter;
        if (hunter != null)
        SetLastKnownHunterPosition(hunter.transform.position);
    }

    public void ClearTargetHunter() => TargetHunter = null;

    public void SetLastKnownHunterPosition(Vector3 pos)
    {
        Node safeNode = GetClosestSafeNavigableNode(pos);
        if (safeNode != null)
        {
            LastKnownHunterPos = safeNode.transform.position;
            HasLastKnownPos = true;
        }
    }

    public void ClearLastKnownHunterPosition()
    {
        HasLastKnownPos = false;
        LastKnownHunterPos = Vector3.zero;
    }

    public void TriggerHowlAlert(Vector3 position)
    {
        if (_howlTimer > 0f) return;

        _howlTimer = _howlCooldown;
        OnWolfHowl?.Invoke(this, position);
    }

    public bool TryDetectHunter(out HunterAgent detectedHunter)
    {
        detectedHunter = null;
        if (_hunterLayer.value == 0) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, _viewRadius, _hunterLayer);
        float closestDistSqr = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null) continue;

            Transform candidate = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform;
            if (!candidate.TryGetComponent<HunterAgent>(out var hunter)) continue;
            if (!hunter.gameObject.activeInHierarchy) continue;
            if (IsHunterInSafeZone(hunter)) continue;

            Vector3 flatToTarget = hunter.transform.position - transform.position;
            flatToTarget.y = 0f;
            float flatDist = flatToTarget.magnitude;
            if (flatDist > _viewRadius) continue;

            Vector3 forwardFlat = transform.forward;
            forwardFlat.y = 0f;
            if (forwardFlat.sqrMagnitude > 0.0001f && flatToTarget.sqrMagnitude > 0.0001f)
            {
                float angle = Vector3.Angle(forwardFlat.normalized, flatToTarget.normalized);
                if (angle > _viewAngle * 0.5f) continue;
            }

            if (!HasLineOfSightTo(hunter.transform)) continue;

            float distSqr = (transform.position - hunter.transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                detectedHunter = hunter;
            }
        }

        return detectedHunter != null;
    }

    public bool IsHunterInSafeZone(HunterAgent hunter)
    {
        if (hunter == null) return true;
        if (hunter.IsInSafeZone) return true;
        if (Graph._current != null)
        {
            Node hunterNode = Graph._current.GetClosestNode(hunter.transform.position, excludeBlocked: false);
            if (hunterNode != null && hunterNode.Zone == ZoneType.SafeZone) return true;
        }

        return false;
    }

    public bool HasLineOfSightTo(Transform target)
    {
        if (target == null) return false;

        Vector3 origin = transform.position + Vector3.up * _eyeHeight;
        Vector3 dest = target.position + Vector3.up * _eyeHeight;
        Vector3 dir = dest - origin;
        float dist = dir.magnitude;
        if (dist <= 0.001f) return true;
        if (_obstacleMask.value == 0) return true;
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, _obstacleMask))
        return hit.transform == target || hit.transform.IsChildOf(target);

        return true;
    }

    public bool TryNavigateToSafePosition(Vector3 worldPos, float speed)
    {
        if (Graph._current == null) return false;

        Node startNode = GetClosestSafeNavigableNode(transform.position);
        Node endNode = GetClosestSafeNavigableNode(worldPos);
        if (startNode == null || endNode == null) return false;
        if (startNode.Zone == ZoneType.SafeZone || endNode.Zone == ZoneType.SafeZone) return false;
        if (startNode == endNode) return false;

        if (CalculatePath(startNode, endNode, out List<Node> path) && path != null && path.Count > 0)
        {
            if (PathContainsSafeZone(path)) return false;

            WalkAlongPath(path);
            return true;
        }

        return false;
    }

    public void MoveDirectlyTowardSafePos(Vector3 targetPos, float speed)
    {
        Node destNode = GetClosestSafeNavigableNode(targetPos);
        if (destNode != null && destNode.Zone == ZoneType.SafeZone)
        {
            StopAgentMovement();
            return;
        }

        StopPathMovement();

        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.001f)
        {
            _currentVelocity = Vector3.zero;
            _velocity = Vector3.zero;
            return;
        }

        Vector3 desired = toTarget.normalized * speed;
        Vector3 steering = Vector3.ClampMagnitude(desired - _currentVelocity, _maxSteering * Time.deltaTime);
        _currentVelocity = Vector3.ClampMagnitude(_currentVelocity + steering, speed);

        Vector3 nextPos = transform.position + _currentVelocity * Time.deltaTime;
        Node nextNode = GetClosestSafeNavigableNode(nextPos);
        if (nextNode != null && nextNode.Zone == ZoneType.SafeZone)
        {
            StopAgentMovement();
            return;
        }

        transform.position = nextPos;
        if (_currentVelocity.sqrMagnitude > 0.0001f)
        transform.forward = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z).normalized;

        _velocity = _currentVelocity;
    }

    public void StopAgentMovement()
    {
        StopPathMovement();
        _currentVelocity = Vector3.zero;
        _velocity = Vector3.zero;
    }

    public Node GetClosestSafeNavigableNode(Vector3 worldPos)
    {
        if (Graph._current == null) return null;

        Node closest = null;
        float bestDistSqr = float.MaxValue;

        foreach (Node node in Graph._current.GetAllGridNodes)
        {
            if (node == null || node.Blocked || node.Zone == ZoneType.SafeZone) continue;

            float distSqr = (node.transform.position - worldPos).sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                closest = node;
            }
        }

        return closest;
    }

    public Node GetRandomSafeNavigableNodeNear(Vector3 center, float radius)
    {
        if (Graph._current == null) return null;

        List<Node> candidates = new();
        foreach (Node node in Graph._current.GetAllGridNodes)
        {
            if (node == null || node.Blocked || node.Zone == ZoneType.SafeZone) continue;

            float dist = Vector3.Distance(node.transform.position, center);
            if (dist <= radius) candidates.Add(node);
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private bool PathContainsSafeZone(List<Node> path)
    {
        for (int i = 0; i < path.Count; i++)
        {
            if (path[i] != null && path[i].Zone == ZoneType.SafeZone) return true;
        }

        return false;
    }

    private void UpdateDetectionFeedback()
    {
        _hasDetectedHunter = TryDetectHunter(out _);

        if (StateMachine.CurrentState == ChaseState) _gizmoColor = Color.red;
        else if (StateMachine.CurrentState == InvestigateState) _gizmoColor = Color.yellow;
        else if (_hasDetectedHunter) _gizmoColor = new Color(1f, 0.45f, 0f);
        else _gizmoColor = Color.cyan;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _gizmoColor;
        Gizmos.DrawWireSphere(transform.position, _viewRadius);

        Vector3 forwardFlat = transform.forward;
        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude > 0.0001f)
        {
            forwardFlat.Normalize();
            float halfAngle = _viewAngle * 0.5f;
            Vector3 leftDir = Quaternion.AngleAxis(-halfAngle, Vector3.up) * forwardFlat;
            Vector3 rightDir = Quaternion.AngleAxis(halfAngle, Vector3.up) * forwardFlat;

            Gizmos.color = new Color(1f, 0.85f, 0f);
            Gizmos.DrawLine(transform.position + Vector3.up * _eyeHeight, transform.position + Vector3.up * _eyeHeight + leftDir * _viewRadius);
            Gizmos.DrawLine(transform.position + Vector3.up * _eyeHeight, transform.position + Vector3.up * _eyeHeight + rightDir * _viewRadius);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _captureRadius);
    }
}