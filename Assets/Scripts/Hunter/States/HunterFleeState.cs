using UnityEngine;

public class HunterFleeState : IState
{
    private readonly HunterAgent _hunter;

    public HunterFleeState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter()
    {
        _hunter.StateLogic.EnterFlee();
    }

    public void UpdateState()
    {
        _hunter.StateLogic.TickFlee();

        if (_hunter.StateLogic.HasFinishedSafeZoneWait)
        {
            _hunter.ClearThreatWolf();
            if (_hunter.HasRequiredGathers)
            _hunter.StateMachine.ChangeState(_hunter.ExitState);
            else
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
        }
    }

    public void Exit()
    {
        _hunter.StateLogic.ExitFlee();
    }
}