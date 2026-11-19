using UnityEngine;

public class WolfChaseState : IState
{
    private readonly WolfAgent _wolf;
    private float _repathTimer;

    public WolfChaseState(WolfAgent wolf)
    {
        _wolf = wolf;
    }

    public void Enter()
    {
        _wolf.StopAgentMovement();
        _repathTimer = 0f;

        if (_wolf.TargetHunter != null) _wolf.SetLastKnownHunterPosition(_wolf.TargetHunter.transform.position);
    }

    public void UpdateState()
    {
        if (_wolf.TargetHunter != null && _wolf.IsHunterInSafeZone(_wolf.TargetHunter))
        {
            _wolf.ClearTargetHunter();
            _wolf.ClearLastKnownHunterPosition();
            _wolf.StateMachine.ChangeState(_wolf.PatrolState);
            return;
        }

        if (_wolf.TryDetectHunter(out HunterAgent seenHunter))
        {
            _wolf.SetTargetHunter(seenHunter);
            _wolf.TriggerHowlAlert(seenHunter.transform.position);

            float distToHunter = Vector3.Distance(_wolf.transform.position, seenHunter.transform.position);
            if (distToHunter < _wolf.CaptureRadius)
            {
                _wolf.StopAgentMovement();
                seenHunter.Die();
                _wolf.ClearTargetHunter();
                _wolf.ClearLastKnownHunterPosition();
                _wolf.StateMachine.ChangeState(_wolf.PatrolState);
                return;
            }

            if (_wolf.HasLineOfSightTo(seenHunter.transform))
            _wolf.MoveDirectlyTowardSafePos(seenHunter.transform.position, _wolf.ChaseSpeed);
            else
            {
                _repathTimer -= Time.deltaTime;
                if (!_wolf.IsFollowingPath || _repathTimer <= 0f)
                {
                    _repathTimer = _wolf.RepathInterval;
                    if (!_wolf.TryNavigateToSafePosition(seenHunter.transform.position, _wolf.ChaseSpeed))
                    _wolf.MoveDirectlyTowardSafePos(seenHunter.transform.position, _wolf.ChaseSpeed);
                }
            }

            return;
        }

        if (_wolf.HasLastKnownPos)
        {
            _wolf.ClearTargetHunter();
            _wolf.StateMachine.ChangeState(_wolf.InvestigateState);
            return;
        }

        _wolf.ClearTargetHunter();
        _wolf.StateMachine.ChangeState(_wolf.PatrolState);
    }

    public void Exit()
    {
        _wolf.StopAgentMovement();
        _repathTimer = 0f;
    }
}