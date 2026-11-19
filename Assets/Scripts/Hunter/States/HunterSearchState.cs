using UnityEngine;

public class HunterSearchState : IState
{
    private readonly HunterAgent _hunter;

    public HunterSearchState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter() => _hunter.StateLogic.EnterSearch();

    public void UpdateState()
    {
        if (_hunter.DetectWolf(out Transform wolf))
        {
            _hunter.ThreatWolf = wolf;
            _hunter.StateMachine.ChangeState(_hunter.FleeState);
            return;
        }

        if (_hunter.HasRequiredGathers)
        {
            _hunter.StateMachine.ChangeState(_hunter.ExitState);
            return;
        }

        if (_hunter.DetectDeadBoid(out Transform deadBoid))
        {
            _hunter.TargetBoid = deadBoid;
            _hunter.StateMachine.ChangeState(_hunter.GatherState);
            return;
        }

        if (_hunter.IsAttackReady && _hunter.DetectLiveBoid(out Transform liveBoid))
        {
            _hunter.TargetBoid = liveBoid;
            _hunter.StateMachine.ChangeState(_hunter.AttackState);
            return;
        }

        _hunter.StateLogic.TickSearch();
    }

    public void Exit() => _hunter.StateLogic.ExitSearch();
}