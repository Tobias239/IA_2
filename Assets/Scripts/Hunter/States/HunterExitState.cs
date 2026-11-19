using UnityEngine;

public class HunterExitState : IState
{
    private readonly HunterAgent _hunter;

    public HunterExitState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter()
    {
        _hunter.StateLogic.EnterExit();
    }

    public void UpdateState()
    {
        _hunter.StateLogic.TickExit();

        if (_hunter.StateLogic.HasReachedExit)
        {
            _hunter.TeleportToStartZone();
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
        }
    }

    public void Exit()
    {
        _hunter.StateLogic.ExitExit();
    }
}