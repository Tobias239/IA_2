using UnityEngine;

public class HunterSteering
{
    private readonly Transform _transform;
    private readonly float _maxSpeed;
    private readonly float _maxSteering;
    private readonly float _eyeHeight;
    private readonly float _avoidanceRadius;
    private readonly float _avoidanceLookAhead;
    private readonly float _avoidanceWeight;
    private readonly LayerMask _obstacleMask;

    public Vector3 Velocity { get; private set; }

    public HunterSteering(
        Transform transform,
        float maxSpeed,
        float maxSteering,
        float eyeHeight,
        float avoidanceRadius,
        float avoidanceLookAhead,
        float avoidanceWeight,
        LayerMask obstacleMask)
    {
        _transform = transform;
        _maxSpeed = maxSpeed;
        _maxSteering = maxSteering;
        _eyeHeight = eyeHeight;
        _avoidanceRadius = avoidanceRadius;
        _avoidanceLookAhead = avoidanceLookAhead;
        _avoidanceWeight = avoidanceWeight;
        _obstacleMask = obstacleMask;
    }

    public void ResetVelocity() => Velocity = Vector3.zero;

    public void ApplyMovement(Vector3 steering)
    {
        Vector3 avoidanceSteering = CalculateAvoidance();
        Velocity += steering + avoidanceSteering;
        Velocity = Vector3.ClampMagnitude(Velocity, _maxSpeed);
        _transform.position += Velocity * Time.deltaTime;
        if (Velocity != Vector3.zero) _transform.forward = Velocity;
    }

    public Vector3 CalculateAvoidance()
    {
        if (_obstacleMask.value == 0) return Vector3.zero;

        Vector3 origin = _transform.position + Vector3.up * (_eyeHeight * 0.5f);
        Vector3 forward = _transform.forward.sqrMagnitude > 0.001f ? _transform.forward.normalized : Vector3.forward;
        Vector3 avoidanceForce = Vector3.zero;

        if (Physics.SphereCast(origin, _avoidanceRadius, forward, out RaycastHit frontHit, _avoidanceLookAhead, _obstacleMask))
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(forward, frontHit.normal).normalized;
            if (slideDir.sqrMagnitude < 0.01f)
            slideDir = Vector3.Cross(Vector3.up, frontHit.normal).normalized;

            float closeness = 1f - Mathf.Clamp01(frontHit.distance / _avoidanceLookAhead);
            Vector3 desiredAvoid = (frontHit.normal * 0.7f + slideDir * 0.3f).normalized * (_maxSpeed * _avoidanceWeight * closeness);
            avoidanceForce += CalculateSteering(desiredAvoid);
        }

        float sideLookAhead = _avoidanceLookAhead * 0.8f;
        Vector3 leftDir = Quaternion.AngleAxis(-25f, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(25f, Vector3.up) * forward;

        if (Physics.Raycast(origin, leftDir, out RaycastHit leftHit, sideLookAhead, _obstacleMask))
        {
            float closeness = 1f - Mathf.Clamp01(leftHit.distance / sideLookAhead);
            Vector3 steerLeft = Vector3.Cross(Vector3.up, leftHit.normal).normalized * (_maxSpeed * _avoidanceWeight * 0.65f * closeness);
            avoidanceForce += CalculateSteering(steerLeft);
        }

        if (Physics.Raycast(origin, rightDir, out RaycastHit rightHit, sideLookAhead, _obstacleMask))
        {
            float closeness = 1f - Mathf.Clamp01(rightHit.distance / sideLookAhead);
            Vector3 steerRight = Vector3.Cross(rightHit.normal, Vector3.up).normalized * (_maxSpeed * _avoidanceWeight * 0.65f * closeness);
            avoidanceForce += CalculateSteering(steerRight);
        }

        avoidanceForce.y = 0f;
        return avoidanceForce;
    }

    public Vector3 CalculateArrive(Vector3 target)
    {
        Vector3 direction = target - _transform.position;
        float distance = direction.magnitude;
        if (distance < 0.1f) return Vector3.zero;

        float slowingDistance = 3f;
        float targetSpeed = _maxSpeed * (distance / slowingDistance);
        float desiredSpeed = Mathf.Min(targetSpeed, _maxSpeed);
        Vector3 desired = direction.normalized * desiredSpeed;
        return CalculateSteering(desired);
    }

    public Vector3 CalculatePursuit(Transform target)
    {
        Vector3 direction = target.position - _transform.position;
        float distance = direction.magnitude;
        Vector3 targetVelocity = target.TryGetComponent<IVelocityProvider>(out var provider) ? provider.Velocity : Vector3.zero;
        float prediction = distance / (_maxSpeed + targetVelocity.magnitude);
        Vector3 futurePosition = target.position + targetVelocity * prediction;
        Vector3 desired = (futurePosition - _transform.position).normalized * _maxSpeed;
        return CalculateSteering(desired);
    }

    private Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - Velocity;
        steering = Vector3.ClampMagnitude(steering, _maxSteering * Time.deltaTime);
        return steering;
    }
}