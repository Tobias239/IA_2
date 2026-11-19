using UnityEngine;

public class AgentPhysics : MonoBehaviour, IVelocityProvider
{
    public Vector3 Velocity => _velocity;
    protected Vector3 _velocity;
}