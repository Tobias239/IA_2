using UnityEngine;

public class HunterAttackState : IState
{
    private readonly HunterAgent _hunter;

    public HunterAttackState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter() => _hunter.StateLogic.EnterAttack();

    public void UpdateState()
    {
        if (_hunter.DetectWolf(out Transform wolf))
        {
            _hunter.ThreatWolf = wolf;
            _hunter.ClearTargetBoid();
            _hunter.StateMachine.ChangeState(_hunter.FleeState);
            return;
        }

        if (_hunter.HasRequiredGathers)
        {
            _hunter.ClearTargetBoid();
            _hunter.StateMachine.ChangeState(_hunter.ExitState);
            return;
        }

        if (_hunter.TargetBoid == null)
        {
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
            return;
        }

        if (!_hunter.IsBoidAlive(_hunter.TargetBoid))
        {
            if (_hunter.IsBoidAvailableForGather(_hunter.TargetBoid))
            {
                _hunter.StateMachine.ChangeState(_hunter.GatherState);
                return;
            }

            _hunter.ClearTargetBoid();
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
            return;
        }

        if (!_hunter.IsTargetBoidValidForAttack())
        {
            _hunter.ClearTargetBoid();
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
            return;
        }

        _hunter.StateLogic.TickAttack();

        if (_hunter.TargetBoid != null && !_hunter.IsBoidAlive(_hunter.TargetBoid) &&
        _hunter.IsBoidAvailableForGather(_hunter.TargetBoid))
        {
            _hunter.StateMachine.ChangeState(_hunter.GatherState);
            return;
        }

        if (_hunter.TargetBoid == null)
        _hunter.StateMachine.ChangeState(_hunter.SearchState);
    }

    public void Exit() => _hunter.StateLogic.ExitAttack();
}