using UnityEngine;

public class HunterGatherState : IState
{
    private readonly HunterAgent _hunter;

    public HunterGatherState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter() => _hunter.StateLogic.EnterGather();

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

        if (!_hunter.IsBoidAvailableForGather(_hunter.TargetBoid))
        {
            _hunter.ClearTargetBoid();
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
            return;
        }

        _hunter.StateLogic.TickGather();

        if (_hunter.StateLogic.HasCompletedGather)
        {
            if (_hunter.HasRequiredGathers) _hunter.StateMachine.ChangeState(_hunter.ExitState);
            else _hunter.StateMachine.ChangeState(_hunter.SearchState);
        }
    }

    public void Exit() => _hunter.StateLogic.ExitGather();
}