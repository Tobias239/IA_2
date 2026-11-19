using System.Collections.Generic;
using UnityEngine;

public class HunterStateLogic
{
    private readonly HunterAgent _hunter;
    private readonly HunterSteering _steering;
    private readonly HunterGraphNavigation _navigation;

    private readonly List<GameObject> _spawnedObjects = new();

    private float _spawnTimer;
    private float _gatherTimer;
    private float _safeZoneWaitTimer;
    private float _deadTimer;
    private float _repathTimer;
    private float _startHoldTimer;

    private Transform _selectedSafeZone;
    private bool _hasReachedSafeZone;
    private bool _hasReachedHuntingGround;
    private bool _hasStartedHuntingMovement;

    public bool HasCompletedGather { get; private set; }
    public bool HasFinishedSafeZoneWait { get; private set; }
    public bool HasReachedExit { get; private set; }
    public bool IsRespawnReady { get; private set; }

    public HunterStateLogic(HunterAgent hunter, HunterSteering steering, HunterGraphNavigation navigation)
    {
        _hunter = hunter;
        _steering = steering;
        _navigation = navigation;
    }

    public void EnterSearch()
    {
        _hunter.StopMovement();
        _spawnTimer = 0f;
        _startHoldTimer = 0f;
        _hasStartedHuntingMovement = false;
        _hasReachedHuntingGround = IsInsideHuntingGround();
        _navigation.ResetPathState();
        _navigation.RefreshHuntingNodes(_hunter.HuntingGroundNodes, _hunter.HuntingGroundCenter, _hunter.HuntingGroundRadius);

        if (_hunter.StartZoneHoldTime <= 0f)
        {
            _hasStartedHuntingMovement = true;
            _navigation.TryMoveToHuntingGroundNode(_hunter.HuntingGroundNodes, _hunter.HuntingGroundCenter, _hunter.HuntingGroundRadius);
        }
    }

    public void TickSearch()
    {
        if (!_hasReachedHuntingGround && IsInsideHuntingGround()) _hasReachedHuntingGround = true;
        if (IsInsideHuntingGround()) HandleInterestObjectSpawning();

        if (!_hasStartedHuntingMovement)
        {
            _startHoldTimer += Time.deltaTime;
            if (_startHoldTimer >= _hunter.StartZoneHoldTime)
            {
                _hasStartedHuntingMovement = true;
                _navigation.TryMoveToHuntingGroundNode(_hunter.HuntingGroundNodes, _hunter.HuntingGroundCenter, _hunter.HuntingGroundRadius);
            }

            return;
        }

        if (!_navigation.IsWaitingForPath && !_hunter.IsFollowingPath)
        _navigation.TryMoveToHuntingGroundNode(_hunter.HuntingGroundNodes, _hunter.HuntingGroundCenter, _hunter.HuntingGroundRadius);
    }

    public void ExitSearch()
    {
        _startHoldTimer = 0f;
        _hasStartedHuntingMovement = false;
        _hasReachedHuntingGround = false;
        _navigation.ResetPathState();
    }

    public void EnterAttack()
    {
        _hunter.StopMovement();
        _repathTimer = 0f;
    }

    public void TickAttack()
    {
        if (_hunter.TargetBoid == null) return;

        float distanceToTarget = Vector3.Distance(_hunter.transform.position, _hunter.TargetBoid.position);
        bool hasLineOfSight = _hunter.HasLineOfSightTo(_hunter.TargetBoid);
        if (hasLineOfSight && _hunter.IsAttackReady)
        {
            if (distanceToTarget <= _hunter.MeleeAttackRadius)
            {
                _hunter.PerformMeleeAttack(_hunter.TargetBoid);
                _hunter.ResetAttackCooldown();
                if (_hunter.IsBoidAlive(_hunter.TargetBoid)) _hunter.ClearTargetBoid();

                return;
            }

            if (distanceToTarget <= _hunter.RangeAttackRadius)
            {
                _hunter.PerformRangedAttack(_hunter.TargetBoid);
                _hunter.ResetAttackCooldown();
                if (_hunter.IsBoidAlive(_hunter.TargetBoid)) _hunter.ClearTargetBoid();

                return;
            }
        }

        if (!hasLineOfSight)
        {
            _repathTimer -= Time.deltaTime;
            if (!_hunter.IsFollowingPath || _repathTimer <= 0f)
            {
                _repathTimer = 0.35f;
                _navigation.TryMoveTowardWorldPos(_hunter.TargetBoid.position);
            }
        }
        else
        {
            Vector3 pursuitSteering = _steering.CalculatePursuit(_hunter.TargetBoid);
            _hunter.ApplyMovement(pursuitSteering);
        }
    }

    public void ExitAttack()
    {
        _navigation.ResetPathState();
        _repathTimer = 0f;
    }

    public void EnterGather()
    {
        _hunter.StopMovement();
        _repathTimer = 0f;
        _gatherTimer = 0f;
        HasCompletedGather = false;
    }

    public void TickGather()
    {
        HasCompletedGather = false;
        if (!_hunter.IsBoidAvailableForGather(_hunter.TargetBoid)) return;

        float distanceToTarget = Vector3.Distance(_hunter.transform.position, _hunter.TargetBoid.position);
        float reachDistance = _hunter.MeleeAttackRadius + _hunter.GatherReachMargin;

        if (distanceToTarget <= reachDistance)
        {
            _hunter.StopMovement();
            _gatherTimer += Time.deltaTime;

            if (_gatherTimer >= _hunter.GatherDuration)
            {
                Transform collectedBoid = _hunter.TargetBoid;
                Vector3 dropPos = collectedBoid.position;
                _hunter.ClearTargetBoid();
                _gatherTimer = 0f;
                _hunter.CollectTargetBoid(collectedBoid);
                _hunter.AddGatherCount();
                _hunter.SpawnInterestObjectAt(dropPos);
                HasCompletedGather = true;
            }

            return;
        }

        _gatherTimer = 0f;

        if (!_hunter.HasLineOfSightTo(_hunter.TargetBoid))
        {
            _repathTimer -= Time.deltaTime;
            if (!_hunter.IsFollowingPath || _repathTimer <= 0f)
            {
                _repathTimer = 0.35f;
                _navigation.TryMoveTowardWorldPos(_hunter.TargetBoid.position);
            }
        }
        else
        {
            Vector3 arriveSteering = _steering.CalculateArrive(_hunter.TargetBoid.position);
            _hunter.ApplyMovement(arriveSteering);
        }
    }

    public void ExitGather()
    {
        _navigation.ResetPathState();
        _repathTimer = 0f;
        _gatherTimer = 0f;
        HasCompletedGather = false;
    }

    public void EnterFlee()
    {
        _hunter.StopMovement();
        _hunter.ClearTargetBoid();
        _hunter.SetInSafeZone(false);
        HasFinishedSafeZoneWait = false;
        _safeZoneWaitTimer = 0f;
        _hasReachedSafeZone = false;
        _selectedSafeZone = null;
        _navigation.ResetPathState();

        if (IsInsideSafeZone())
        {
            ArriveAtSafeZone();
            return;
        }

        if (_navigation.TryMoveToClosestSafeZone(_hunter.SafeZones, _hunter.ZoneArrivalThreshold, out Transform zone, out bool arrived))
        {
            _selectedSafeZone = zone;
            if (arrived) ArriveAtSafeZone();
        }
    }

    public void TickFlee()
    {
        if (!_hasReachedSafeZone)
        {
            if (IsInsideSafeZone())
            {
                ArriveAtSafeZone();
                return;
            }

            if (_selectedSafeZone == null)
            {
                if (_navigation.TryMoveToClosestSafeZone(_hunter.SafeZones, _hunter.ZoneArrivalThreshold, out Transform zone, out bool arrived))
                {
                    _selectedSafeZone = zone;
                    if (arrived) ArriveAtSafeZone();
                }

                return;
            }

            float distToZone = Vector3.Distance(_hunter.transform.position, _selectedSafeZone.position);
            if (distToZone <= _hunter.ZoneArrivalThreshold)
            {
                ArriveAtSafeZone();
                return;
            }

            if (!_hunter.IsFollowingPath && !_navigation.IsWaitingForPath)
            {
                if (distToZone <= _hunter.ZoneArrivalThreshold * 1.5f) ArriveAtSafeZone();
                else if (_navigation.TryMoveToClosestSafeZone(_hunter.SafeZones, _hunter.ZoneArrivalThreshold, out Transform nextZone, out bool arrived))
                {
                    _selectedSafeZone = nextZone;
                    if (arrived) ArriveAtSafeZone();
                }
            }

            return;
        }

        _hunter.StopMovement();
        _safeZoneWaitTimer += Time.deltaTime;
        if (_safeZoneWaitTimer >= _hunter.SafeZoneWaitTime) HasFinishedSafeZoneWait = true;
    }

    public void ExitFlee()
    {
        _hunter.SetInSafeZone(false);
        HasFinishedSafeZoneWait = false;
        _hasReachedSafeZone = false;
        _safeZoneWaitTimer = 0f;
        _selectedSafeZone = null;
        _navigation.ResetPathState();
    }

    public void EnterExit()
    {
        _hunter.StopMovement();
        _hunter.ClearTargetBoid();
        HasReachedExit = false;
        _repathTimer = 0f;
        _navigation.ResetPathState();

        if (IsInsideExitZone())
        {
            HasReachedExit = true;
            return;
        }

        if (_navigation.TryMoveToExit(_hunter.ExitZone, out bool alreadyAtExit) && alreadyAtExit) HasReachedExit = true;
    }

    public void TickExit()
    {
        if (IsInsideExitZone())
        {
            _hunter.StopMovement();
            HasReachedExit = true;
            return;
        }

        if (!_hunter.IsFollowingPath && !_navigation.IsWaitingForPath)
        {
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = 0.4f;
                if (_navigation.TryMoveToExit(_hunter.ExitZone, out bool alreadyAtExit) && alreadyAtExit)
                {
                    _hunter.StopMovement();
                    HasReachedExit = true;
                }
            }
        }
    }

    public void ExitExit()
    {
        _navigation.ResetPathState();
        _repathTimer = 0f;
        HasReachedExit = false;
    }

    public void EnterDead()
    {
        _deadTimer = 0f;
        IsRespawnReady = false;
        _hunter.ClearTargetBoid();
        _hunter.ClearThreatWolf();
        _hunter.SetInSafeZone(false);
        _hunter.StopMovement();
        _navigation.ResetPathState();
        _hunter.SetAgentVisible(false);
    }

    public void TickDead()
    {
        _deadTimer += Time.deltaTime;
        _hunter.StopMovement();
        if (_deadTimer >= _hunter.RespawnTime) IsRespawnReady = true;
    }

    public void ExitDead()
    {
        _deadTimer = 0f;
        IsRespawnReady = false;
        _hunter.SetAgentVisible(true);
    }

    private void ArriveAtSafeZone()
    {
        _hasReachedSafeZone = true;
        _hunter.SetInSafeZone(true);
        _safeZoneWaitTimer = 0f;
        _navigation.ResetPathState();
        _hunter.StopMovement();
    }

    private bool IsInsideExitZone()
    {
        if (Graph._current != null)
        {
            Node currentNode = _navigation.GetClosestNode(_hunter.transform.position);
            if (currentNode != null && currentNode.Zone == ZoneType.End)
            {
                float distToNode = Vector3.Distance(_hunter.transform.position, currentNode.transform.position);
                if (distToNode <= Mathf.Max(Graph._current.NodeOffset * 0.75f, _hunter.ExitArrivalThreshold)) return true;
            }
        }

        if (_hunter.ExitZone != null)
        {
            float distToExit = Vector3.Distance(_hunter.transform.position, _hunter.ExitZone.position);
            if (distToExit <= _hunter.ExitArrivalThreshold) return true;
        }

        return false;
    }

    private bool IsInsideSafeZone()
    {
        if (Graph._current != null)
        {
            Node currentNode = _navigation.GetClosestNode(_hunter.transform.position);
            if (currentNode != null && currentNode.Zone == ZoneType.SafeZone)
            {
                float distToNode = Vector3.Distance(_hunter.transform.position, currentNode.transform.position);
                if (distToNode <= Graph._current.NodeOffset * 0.75f) return true;
            }
        }

        if (_selectedSafeZone != null)
        {
            float distToZone = Vector3.Distance(_hunter.transform.position, _selectedSafeZone.position);
            if (distToZone <= _hunter.ZoneArrivalThreshold) return true;
        }

        return false;
    }

    private bool IsInsideHuntingGround()
    {
        if (Graph._current != null)
        {
            Node currentNode = _navigation.GetClosestNode(_hunter.transform.position);
            if (currentNode != null && currentNode.Zone == ZoneType.Floor)
            {
                float distToNode = Vector3.Distance(_hunter.transform.position, currentNode.transform.position);
                if (distToNode <= Graph._current.NodeOffset * 0.75f) return true;
            }
        }

        if (_hunter.HuntingGroundCenter != null)
        {
            float dist = Vector3.Distance(_hunter.transform.position, _hunter.HuntingGroundCenter.position);
            if (dist <= _hunter.HuntingGroundRadius) return true;
        }

        foreach (Node node in _navigation.ValidHuntingNodes)
        {
            if (node == null) continue;
            if (Vector3.Distance(_hunter.transform.position, node.transform.position) <= 1.5f) return true;
        }

        return false;
    }

    private void HandleInterestObjectSpawning()
    {
        if (_hunter.InterestObjectPrefab == null || _hunter.SpawnInterval <= 0f) return;

        for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (_spawnedObjects[i] == null) _spawnedObjects.RemoveAt(i);
        }

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _hunter.SpawnInterval)
        {
            _spawnTimer = 0f;

            if (_spawnedObjects.Count < _hunter.MaxActiveInterestObjects)
            {
                Vector3 spawnPos = GetRandomSpawnPosition();
                GameObject obj = Object.Instantiate(_hunter.InterestObjectPrefab, spawnPos, Quaternion.identity);
                _spawnedObjects.Add(obj);
            }
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (_navigation.ValidHuntingNodes.Count > 0)
        {
            Node randomNode = _navigation.ValidHuntingNodes[Random.Range(0, _navigation.ValidHuntingNodes.Count)];
            return randomNode.transform.position;
        }

        if (_hunter.HuntingGroundCenter != null)
        {
            Vector2 randomCircle = Random.insideUnitCircle * _hunter.HuntingGroundRadius;
            return _hunter.HuntingGroundCenter.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        return _hunter.transform.position;
    }
}