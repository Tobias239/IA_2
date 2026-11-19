using UnityEngine;

public class WolfInvestigateState : IState
{
    private readonly WolfAgent _wolf;
    private Vector3 _investigateCenter;
    private float _searchTimer;
    private float _repathTimer;
    private bool _hasReachedAlertPoint;

    public WolfInvestigateState(WolfAgent wolf)
    {
        _wolf = wolf;
    }

    public void Enter()
    {
        _wolf.StopAgentMovement();
        _searchTimer = 0f;
        _repathTimer = 0f;
        _hasReachedAlertPoint = false;

        if (_wolf.HasLastKnownPos)
        {
            Node safeTarget = _wolf.GetClosestSafeNavigableNode(_wolf.LastKnownHunterPos);
            if (safeTarget != null && safeTarget.Zone != ZoneType.SafeZone)
            {
                _investigateCenter = safeTarget.transform.position;
                _wolf.TryNavigateToSafePosition(_investigateCenter, _wolf.InvestigateSpeed);
                return;
            }
        }

        _wolf.ClearLastKnownHunterPosition();
        _wolf.StateMachine.ChangeState(_wolf.PatrolState);
    }

    public void NotifyNewAlertPosition(Vector3 newPos)
    {
        Node safeNode = _wolf.GetClosestSafeNavigableNode(newPos);
        if (safeNode == null || safeNode.Zone == ZoneType.SafeZone) return;

        _wolf.SetLastKnownHunterPosition(safeNode.transform.position);
        _investigateCenter = safeNode.transform.position;
        _hasReachedAlertPoint = false;
        _searchTimer = 0f;
        _repathTimer = 0f;

        if (!_wolf.TryNavigateToSafePosition(_investigateCenter, _wolf.InvestigateSpeed))
        _wolf.MoveDirectlyTowardSafePos(_investigateCenter, _wolf.InvestigateSpeed);
    }

    public void UpdateState()
    {
        if (_wolf.TryDetectHunter(out HunterAgent detectedHunter))
        {
            _wolf.SetTargetHunter(detectedHunter);
            _wolf.TriggerHowlAlert(detectedHunter.transform.position);
            _wolf.StateMachine.ChangeState(_wolf.ChaseState);
            return;
        }

        if (!_hasReachedAlertPoint)
        {
            float distToCenter = Vector3.Distance(_wolf.transform.position, _investigateCenter);
            if (distToCenter <= _wolf.WaypointReachThreshold * 1.25f)
            {
                _hasReachedAlertPoint = true;
                _wolf.StopAgentMovement();
                PickNextSearchPoint();
                return;
            }

            _repathTimer -= Time.deltaTime;
            if (!_wolf.IsFollowingPath && _repathTimer <= 0f)
            {
                _repathTimer = _wolf.RepathInterval;
                if (!_wolf.TryNavigateToSafePosition(_investigateCenter, _wolf.InvestigateSpeed))
                _wolf.MoveDirectlyTowardSafePos(_investigateCenter, _wolf.InvestigateSpeed);
            }
            return;
        }

        _searchTimer += Time.deltaTime;
        if (_searchTimer >= _wolf.InvestigateSearchDuration)
        {
            _wolf.ClearLastKnownHunterPosition();
            _wolf.StateMachine.ChangeState(_wolf.PatrolState);
            return;
        }

        _repathTimer -= Time.deltaTime;
        if (!_wolf.IsFollowingPath && _repathTimer <= 0f)
        {
            _repathTimer = _wolf.RepathInterval;
            PickNextSearchPoint();
        }
    }

    public void Exit()
    {
        _wolf.StopAgentMovement();
        _searchTimer = 0f;
        _repathTimer = 0f;
        _hasReachedAlertPoint = false;
    }

    private void PickNextSearchPoint()
    {
        Node searchNode = _wolf.GetRandomSafeNavigableNodeNear(_investigateCenter, _wolf.InvestigateRadius);
        if (searchNode == null || searchNode.Zone == ZoneType.SafeZone) return;

        if (!_wolf.TryNavigateToSafePosition(searchNode.transform.position, _wolf.InvestigateSpeed))
        _wolf.MoveDirectlyTowardSafePos(searchNode.transform.position, _wolf.InvestigateSpeed);
    }
}