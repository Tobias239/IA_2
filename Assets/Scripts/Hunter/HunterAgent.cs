using System.Collections.Generic;
using UnityEngine;

public class HunterAgent : Agent
{
    [Header("General & Perception")]
    [SerializeField] private float _maxSpeed;
    [SerializeField] private float _maxSteering;
    [SerializeField] private float _visionRadius;
    [SerializeField] private float _eyeHeight;
    [SerializeField] private LayerMask _boidLayer;
    [SerializeField] private LayerMask _wolfLayer;
    [SerializeField] private LayerMask _obstacleMask;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float _avoidanceRadius;
    [SerializeField] private float _avoidanceLookAhead;
    [SerializeField] private float _avoidanceWeight;

    [Header("Search State")]
    [SerializeField] private Transform _huntingGroundCenter;
    [SerializeField] private float _huntingGroundRadius;
    [SerializeField] private List<Node> _huntingGroundNodes = new();
    [SerializeField] private GameObject _interestObjectPrefab;
    [SerializeField] private float _spawnInterval;
    [SerializeField] private int _maxActiveInterestObjects;

    [Header("Attack State")]
    [SerializeField] private int _attackDamage;
    [SerializeField] private float _timeBetweenAttacks;
    [SerializeField] private float _meleeAttackRadius;
    [SerializeField] private float _rangeAttackRadius;

    [Header("Gather State")]
    [SerializeField] private int _requiredGathers;
    [SerializeField] private float _gatherDuration;
    [SerializeField] private float _gatherReachMargin;

    [Header("Flee State")]
    [SerializeField] private List<Transform> _safeZones = new();
    [SerializeField] private float _safeZoneWaitTime;
    [SerializeField] private float _zoneArrivalThreshold;

    [Header("Exit & Respawn")]
    [SerializeField] private Transform _startZone;
    [SerializeField] private float _startZoneHoldTime;
    [SerializeField] private Transform _exitZone;
    [SerializeField] private float _exitArrivalThreshold;
    [SerializeField] private float _respawnTime;

    public StateMachine StateMachine { get; private set; }
    public HunterSearchState SearchState { get; private set; }
    public HunterAttackState AttackState { get; private set; }
    public HunterGatherState GatherState { get; private set; }
    public HunterFleeState FleeState { get; private set; }
    public HunterExitState ExitState { get; private set; }
    public HunterDeadState DeadState { get; private set; }

    public HunterSteering Steering { get; private set; }
    public HunterGraphNavigation Navigation { get; private set; }
    public HunterStateLogic StateLogic { get; private set; }

    public Transform TargetBoid { get; set; }
    public Transform ThreatWolf { get; set; }
    public bool IsInSafeZone { get; private set; }

    public int RequiredGathers => _requiredGathers;
    public int CurrentGathers { get; private set; }
    public bool HasRequiredGathers => CurrentGathers >= _requiredGathers;
    public bool IsAttackReady => _attackCooldownTimer <= 0f;

    public float GatherDuration => _gatherDuration;
    public float GatherReachMargin => _gatherReachMargin;
    public float SafeZoneWaitTime => _safeZoneWaitTime;
    public float ZoneArrivalThreshold => _zoneArrivalThreshold;
    public float ExitArrivalThreshold => _exitArrivalThreshold;
    public float RespawnTime => _respawnTime;
    public float StartZoneHoldTime => _startZoneHoldTime;
    public float VisionRadius => _visionRadius;
    public float MeleeAttackRadius => _meleeAttackRadius;
    public float RangeAttackRadius => _rangeAttackRadius;
    public float SpawnInterval => _spawnInterval;
    public int MaxActiveInterestObjects => _maxActiveInterestObjects;
    public GameObject InterestObjectPrefab => _interestObjectPrefab;
    public float HuntingGroundRadius => _huntingGroundRadius;

    public Transform StartZone => _startZone;
    public Transform HuntingGroundCenter => _huntingGroundCenter;
    public List<Node> HuntingGroundNodes => _huntingGroundNodes;
    public List<Transform> SafeZones => _safeZones;
    public Transform ExitZone => _exitZone;

    private readonly List<GameObject> _activeInterestObjects = new();
    private float _attackCooldownTimer;
    private Renderer[] _renderers;
    private Collider[] _colls;
    private Color _gizmoColor = Color.green;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _colls = GetComponentsInChildren<Collider>();

        _attackCooldownTimer = 0f;
        CurrentGathers = 0;

        Steering = new HunterSteering(transform, _maxSpeed, _maxSteering, _eyeHeight,
        _avoidanceRadius, _avoidanceLookAhead, _avoidanceWeight, _obstacleMask);

        Navigation = new HunterGraphNavigation(this);
        StateLogic = new HunterStateLogic(this, Steering, Navigation);

        StateMachine = new StateMachine();
        SearchState = new HunterSearchState(this);
        AttackState = new HunterAttackState(this);
        GatherState = new HunterGatherState(this);
        FleeState = new HunterFleeState(this);
        ExitState = new HunterExitState(this);
        DeadState = new HunterDeadState(this);
    }

    private void Start()
    {
        if (Graph._current != null && Graph._current.GetAllGridNodes.Count == 0) Graph._current.GenerateGrid();
        if (_startZone != null) Reposition(_startZone.position);
        else if (Graph._current != null)
        {
            List<Node> startNodes = Graph._current.GetNodesByZone(ZoneType.Start, excludeBlocked: true);
            if (startNodes != null && startNodes.Count > 0) Reposition(startNodes[0]);
        }

        StateMachine.Initialize(SearchState);
    }

    private void OnEnable() => OnPathCompleted += HandlePathCompleted;
    
    protected override void OnDisable()
    {
        OnPathCompleted -= HandlePathCompleted;
        base.OnDisable();
    }

    private void HandlePathCompleted() => Navigation.OnPathFinished();

    private void Update()
    {
        if (_attackCooldownTimer > 0f) _attackCooldownTimer -= Time.deltaTime;

        StateMachine.Update();
        UpdateGizmoColor();
    }

    public void SpawnInterestObjectAt(Vector3 position)
    {
        if (_interestObjectPrefab == null) return;

        for (int i = _activeInterestObjects.Count - 1; i >= 0; i--)
        {
            if (_activeInterestObjects[i] == null) _activeInterestObjects.RemoveAt(i);
        }

        if (_activeInterestObjects.Count >= _maxActiveInterestObjects) return;

        Vector3 spawnPos = position;
        spawnPos.y = transform.position.y;

        GameObject obj = Instantiate(_interestObjectPrefab, spawnPos, Quaternion.identity);
        _activeInterestObjects.Add(obj);
    }

    public void SetInSafeZone(bool inSafeZone) => IsInSafeZone = inSafeZone;
    public void AddGatherCount() => CurrentGathers++;
    public void ResetGathers() => CurrentGathers = 0;
    public void ResetAttackCooldown() => _attackCooldownTimer = _timeBetweenAttacks;
    public void ClearTargetBoid() => TargetBoid = null;
    public void ClearThreatWolf() => ThreatWolf = null;

    public void TeleportToStartZone()
    {
        StopMovement();
        ClearTargetBoid();
        ClearThreatWolf();
        SetInSafeZone(false);

        if (_startZone != null)
            Reposition(_startZone.position);
        else if (Graph._current != null)
        {
            List<Node> startNodes = Graph._current.GetNodesByZone(ZoneType.Start, excludeBlocked: true);
            if (startNodes != null && startNodes.Count > 0) Reposition(startNodes[0]);
        }

        ResetGathers();
    }

    public void ApplyMovement(Vector3 steering)
    {
        StopPathMovement();
        Steering.ApplyMovement(steering);
        _velocity = Steering.Velocity;
    }

    public void StopMovement()
    {
        StopPathMovement();
        Steering.ResetVelocity();
        _velocity = Vector3.zero;
    }

    public bool DetectWolf(out Transform wolfTransform)
    {
        wolfTransform = null;
        if (_wolfLayer.value == 0) return false;

        Collider[] hits = Physics.OverlapSphere(transform.position, _visionRadius, _wolfLayer);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!HasLineOfSightTo(hit.transform)) continue;

            float dist = Vector3.SqrMagnitude(transform.position - hit.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                wolfTransform = hit.transform;
            }
        }

        return wolfTransform != null;
    }

    public bool HasLineOfSightTo(Transform target)
    {
        if (target == null) return false;
        if (_obstacleMask.value == 0) return true;

        Vector3 eyePos = transform.position + Vector3.up * _eyeHeight;
        Vector3 targetPos = target.position + Vector3.up * _eyeHeight;
        Vector3 direction = targetPos - eyePos;
        float distance = direction.magnitude;
        if (distance <= 0.001f) return true;

        if (Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance, _obstacleMask))
        return hit.transform == target || hit.transform.IsChildOf(target);

        return true;
    }

    public bool DetectLiveBoid(out Transform boidTransform)
    {
        boidTransform = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, _visionRadius, _boidLayer);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<BoidAgent>(out var boid) && boid.CurrentHealth > 0 && HasLineOfSightTo(boid.transform))
            {
                float dist = Vector3.SqrMagnitude(transform.position - boid.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    boidTransform = boid.transform;
                }
            }
        }

        return boidTransform != null;
    }

    public bool DetectDeadBoid(out Transform boidTransform)
    {
        boidTransform = null;
        Collider[] hits = Physics.OverlapSphere(transform.position, _visionRadius, _boidLayer);
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<BoidAgent>(out var boid) && boid.CurrentHealth <= 0 && HasLineOfSightTo(boid.transform))
            {
                float dist = Vector3.SqrMagnitude(transform.position - boid.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    boidTransform = boid.transform;
                }
            }
        }

        return boidTransform != null;
    }

    public bool IsBoidAlive(Transform boidTarget)
    {
        if (boidTarget != null && boidTarget.TryGetComponent<BoidAgent>(out var boid))
        return boid.CurrentHealth > 0;

        return false;
    }

    public bool IsTargetBoidValidForAttack()
    {
        if (TargetBoid == null) return false;
        if (!IsBoidAlive(TargetBoid)) return false;
        return Vector3.Distance(transform.position, TargetBoid.position) <= _visionRadius;
    }

    public bool IsBoidAvailableForGather(Transform boidTarget)
    {
        if (boidTarget == null) return false;
        if (!boidTarget.gameObject.activeInHierarchy) return false;
        if (!boidTarget.TryGetComponent<BoidAgent>(out var boid)) return false;

        return boid.CurrentHealth <= 0 && boid.StateMachine.CurrentState != boid.RespawnState;
    }

    public void PerformMeleeAttack(Transform boidTarget)
    {
        if (boidTarget != null && boidTarget.TryGetComponent<BoidAgent>(out var boid))
        boid.TakeDamage(_attackDamage);
    }

    public void PerformRangedAttack(Transform boidTarget)
    {
        if (boidTarget != null && boidTarget.TryGetComponent<BoidAgent>(out var boid))
        boid.TakeDamage(_attackDamage);
    }

    public void CollectTargetBoid(Transform boidTarget)
    {
        if (boidTarget == null) return;
        if (boidTarget.TryGetComponent<BoidAgent>(out var boid))
        boid.StateMachine.ChangeState(boid.RespawnState);
    }

    public void Die()
    {
        if (StateMachine.CurrentState == DeadState) return;

        ClearTargetBoid();
        ClearThreatWolf();
        SetInSafeZone(false);
        StopMovement();
        StateMachine.ChangeState(DeadState);
    }

    public void SetAgentVisible(bool visible)
    {
        if (_renderers != null)
        {
            foreach (var r in _renderers)
            {
                if (r != null) r.enabled = visible;
            }
        }

        if (_colls != null)
        {
            foreach (var c in _colls)
            {
                if (c != null) c.enabled = visible;
            }
        }
    }

    private void UpdateGizmoColor()
    {
        if (DetectWolf(out _)) _gizmoColor = Color.magenta;
        else if (DetectLiveBoid(out _)) _gizmoColor = Color.red;
        else if (DetectDeadBoid(out _)) _gizmoColor = Color.yellow;
        else _gizmoColor = Color.green;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _gizmoColor;
        Gizmos.DrawWireSphere(transform.position, _visionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _meleeAttackRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, _rangeAttackRadius);
    }
}