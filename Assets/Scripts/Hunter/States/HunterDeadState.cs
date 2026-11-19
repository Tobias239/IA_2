using UnityEngine;

public class HunterDeadState : IState
{
    private readonly HunterAgent _hunter;

    public HunterDeadState(HunterAgent hunter)
    {
        _hunter = hunter;
    }

    public void Enter()
    {
        _hunter.StateLogic.EnterDead();
    }

    public void UpdateState()
    {
        _hunter.StateLogic.TickDead();

        if (_hunter.StateLogic.IsRespawnReady)
        {
            _hunter.TeleportToStartZone();
            _hunter.StateMachine.ChangeState(_hunter.SearchState);
        }
    }

    public void Exit()
    {
        _hunter.StateLogic.ExitDead();
    }
}