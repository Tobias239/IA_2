using UnityEngine;

public class WolfPatrolState : IState
{
    private readonly WolfAgent _wolf;
    private int _currentWaypointIndex;
    private float _waitTimer;
    private float _repathTimer;
    private bool _isWaiting;

    public WolfPatrolState(WolfAgent wolf)
    {
        _wolf = wolf;
    }

    public void Enter()
    {
        _wolf.StopAgentMovement();
        _waitTimer = 0f;
        _repathTimer = 0f;
        _isWaiting = false;
        MoveToCurrentWaypoint();
    }

    public void UpdateState()
    {
        if (_wolf.TryDetectHunter(out HunterAgent hunter))
        {
            _wolf.SetTargetHunter(hunter);
            _wolf.TriggerHowlAlert(hunter.transform.position);
            _wolf.StateMachine.ChangeState(_wolf.ChaseState);
            return;
        }

        if (_wolf.Waypoints == null || _wolf.Waypoints.Count == 0) return;

        Transform wp = GetValidWaypoint(_currentWaypointIndex);
        if (wp == null)
        {
            AdvanceWaypointIndex();
            return;
        }

        if (_isWaiting)
        {
            _wolf.StopAgentMovement();
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= _wolf.WaypointWaitTime)
            {
                _isWaiting = false;
                _waitTimer = 0f;
                AdvanceWaypointIndex();
                MoveToCurrentWaypoint();
            }
            
            return;
        }

        float dist = Vector3.Distance(_wolf.transform.position, wp.position);
        if (dist <= _wolf.WaypointReachThreshold)
        {
            _isWaiting = true;
            _waitTimer = 0f;
            _wolf.StopAgentMovement();
            return;
        }

        _repathTimer -= Time.deltaTime;
        if (!_wolf.IsFollowingPath && _repathTimer <= 0f)
        {
            _repathTimer = _wolf.RepathInterval;
            if (!_wolf.TryNavigateToSafePosition(wp.position, _wolf.PatrolSpeed))
            _wolf.MoveDirectlyTowardSafePos(wp.position, _wolf.PatrolSpeed);
        }
    }

    public void Exit()
    {
        _wolf.StopAgentMovement();
        _isWaiting = false;
        _waitTimer = 0f;
        _repathTimer = 0f;
    }

    private void MoveToCurrentWaypoint()
    {
        Transform wp = GetValidWaypoint(_currentWaypointIndex);
        if (wp == null) return;

        _repathTimer = _wolf.RepathInterval;
        if (!_wolf.TryNavigateToSafePosition(wp.position, _wolf.PatrolSpeed))
        _wolf.MoveDirectlyTowardSafePos(wp.position, _wolf.PatrolSpeed);
    }

    private Transform GetValidWaypoint(int index)
    {
        if (_wolf.Waypoints == null || _wolf.Waypoints.Count == 0) return null;
        if (index < 0 || index >= _wolf.Waypoints.Count) return null;

        Transform wp = _wolf.Waypoints[index];
        if (wp == null) return null;

        Node wpNode = _wolf.GetClosestSafeNavigableNode(wp.position);
        if (wpNode == null || wpNode.Zone == ZoneType.SafeZone) return null;

        return wp;
    }

    private void AdvanceWaypointIndex()
    {
        if (_wolf.Waypoints == null || _wolf.Waypoints.Count == 0) return;
        _currentWaypointIndex = (_currentWaypointIndex + 1) % _wolf.Waypoints.Count;
    }
}