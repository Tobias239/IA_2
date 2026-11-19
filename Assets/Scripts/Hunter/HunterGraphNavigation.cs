using System.Collections.Generic;
using UnityEngine;

public class HunterGraphNavigation
{
    private readonly Agent _agent;
    private readonly List<Node> _validHuntingNodes = new();

    private bool _waitingForPath;

    public bool IsWaitingForPath => _waitingForPath;
    public IReadOnlyList<Node> ValidHuntingNodes => _validHuntingNodes;

    public HunterGraphNavigation(Agent agent)
    {
        _agent = agent;
    }

    public void ResetPathState()
    {
        _waitingForPath = false;
        _agent.StopPathMovement();
    }

    public void OnPathFinished() => _waitingForPath = false;

    public void RefreshHuntingNodes(List<Node> configuredNodes, Transform huntingCenter, float huntingRadius)
    {
        _validHuntingNodes.Clear();

        if (Graph._current != null)
        {
            List<Node> floorNodes = Graph._current.GetNodesByZone(ZoneType.Floor, excludeBlocked: true);
            if (floorNodes != null && floorNodes.Count > 0)
            {
                foreach (Node node in floorNodes)
                {
                    if (node != null && !node.Blocked)
                    {
                        if (huntingCenter != null && huntingRadius > 0f)
                        {
                            float dist = Vector3.Distance(node.transform.position, huntingCenter.position);
                            if (dist <= huntingRadius) _validHuntingNodes.Add(node);
                        }
                        else _validHuntingNodes.Add(node);
                    }
                }

                if (_validHuntingNodes.Count == 0)
                {
                    foreach (Node node in floorNodes)
                    {
                        if (node != null && !node.Blocked) _validHuntingNodes.Add(node);
                    }
                }

                if (_validHuntingNodes.Count > 0) return;
            }
        }

        if (configuredNodes != null && configuredNodes.Count > 0)
        {
            foreach (Node node in configuredNodes)
            {
                if (node != null && !node.Blocked) _validHuntingNodes.Add(node);
            }
        }

        if (_validHuntingNodes.Count == 0 && huntingCenter != null && Graph._current != null)
        {
            foreach (Node node in Graph._current.GetAllGridNodes)
            {
                if (node != null && !node.Blocked)
                {
                    float dist = Vector3.Distance(node.transform.position, huntingCenter.position);
                    if (dist <= huntingRadius) _validHuntingNodes.Add(node);
                }
            }
        }
    }

    public Node GetClosestNode(Vector3 worldPos)
    {
        if (Graph._current == null) return null;
        return Graph._current.GetClosestNode(worldPos, excludeBlocked: true);
    }

    public Vector3 ResolveStartSpawnPosition(Transform startZone)
    {
        if (Graph._current != null)
        {
            List<Node> startNodes = Graph._current.GetNodesByZone(ZoneType.Start, excludeBlocked: true);
            if (startNodes != null && startNodes.Count > 0)
            {
                Node chosenNode = startNodes[Random.Range(0, startNodes.Count)];
                if (chosenNode != null) return chosenNode.transform.position;
            }
        }

        if (startZone != null) return startZone.position;

        return _agent.transform.position;
    }

    public bool TryMoveToHuntingGroundNode(List<Node> configuredNodes, Transform huntingCenter, float huntingRadius)
    {
        if (_validHuntingNodes.Count == 0)
        {
            RefreshHuntingNodes(configuredNodes, huntingCenter, huntingRadius);
            if (_validHuntingNodes.Count == 0) return false;
        }

        Node startNode = GetClosestNode(_agent.transform.position);
        if (startNode == null) return false;

        Node targetNode = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Node candidate = _validHuntingNodes[Random.Range(0, _validHuntingNodes.Count)];
            if (candidate != null && !candidate.Blocked && candidate != startNode)
            {
                targetNode = candidate;
                break;
            }
        }

        if (targetNode == null)
        {
            foreach (Node candidate in _validHuntingNodes)
            {
                if (candidate != null && !candidate.Blocked && candidate != startNode)
                {
                    targetNode = candidate;
                    break;
                }
            }
        }

        if (targetNode == null) return false;

        if (_agent.CalculatePath(startNode, targetNode, out List<Node> path) && path != null && path.Count > 0)
        {
            _waitingForPath = true;
            _agent.WalkAlongPath(path);
            return true;
        }

        return false;
    }

    public bool TryMoveTowardWorldPos(Vector3 targetWorldPos)
    {
        Node startNode = GetClosestNode(_agent.transform.position);
        Node endNode = GetClosestNode(targetWorldPos);
        if (startNode == null || endNode == null || startNode == endNode) return false;

        if (_agent.CalculatePath(startNode, endNode, out List<Node> path) && path != null && path.Count > 0)
        {
            _waitingForPath = true;
            _agent.WalkAlongPath(path);
            return true;
        }

        return false;
    }

    public bool TryMoveToClosestSafeZone(
        List<Transform> safeZones,
        float arrivalThreshold,
        out Transform chosenZone,
        out bool alreadyArrived)
    {
        chosenZone = null;
        alreadyArrived = false;

        Node startNode = GetClosestNode(_agent.transform.position);
        if (startNode == null) return false;

        List<Node> bestPath = null;
        int bestPathLength = int.MaxValue;
        Node bestTargetNode = null;
        Transform bestZoneTransform = null;

        if (Graph._current != null)
        {
            List<Node> safeNodes = Graph._current.GetNodesByZone(ZoneType.SafeZone, excludeBlocked: true);
            if (safeNodes != null && safeNodes.Count > 0)
            {
                if (startNode.Zone == ZoneType.SafeZone)
                {
                    alreadyArrived = true;
                    chosenZone = startNode.transform;
                    return true;
                }

                foreach (Node safeNode in safeNodes)
                {
                    if (safeNode == null || safeNode.Blocked) continue;

                    if (_agent.CalculatePath(startNode, safeNode, out List<Node> path) && path != null && path.Count > 0)
                    {
                        if (path.Count < bestPathLength)
                        {
                            bestPathLength = path.Count;
                            bestPath = path;
                            bestTargetNode = safeNode;
                        }
                    }
                }

                if (bestPath != null && bestTargetNode != null)
                {
                    chosenZone = bestTargetNode.transform;
                    _waitingForPath = true;
                    _agent.WalkAlongPath(bestPath);
                    return true;
                }
            }
        }

        if (safeZones == null || safeZones.Count == 0) return false;

        Transform fallbackClosestZone = null;
        float fallbackMinDist = float.MaxValue;

        foreach (Transform zone in safeZones)
        {
            if (zone == null) continue;

            float euclidDist = Vector3.Distance(_agent.transform.position, zone.position);
            if (euclidDist < fallbackMinDist)
            {
                fallbackMinDist = euclidDist;
                fallbackClosestZone = zone;
            }

            Node zoneNode = GetClosestNode(zone.position);
            if (zoneNode != null)
            {
                if (_agent.CalculatePath(startNode, zoneNode, out List<Node> path) && path != null && path.Count > 0)
                {
                    if (path.Count < bestPathLength)
                    {
                        bestPathLength = path.Count;
                        bestPath = path;
                        bestZoneTransform = zone;
                    }
                }
            }
        }

        if (bestZoneTransform != null && bestPath != null)
        {
            chosenZone = bestZoneTransform;
            _waitingForPath = true;
            _agent.WalkAlongPath(bestPath);
            return true;
        }

        if (fallbackClosestZone != null)
        {
            chosenZone = fallbackClosestZone;
            if (Vector3.Distance(_agent.transform.position, fallbackClosestZone.position) <= arrivalThreshold * 1.5f)
            {
                alreadyArrived = true;
                return true;
            }

            Node fallbackNode = GetClosestNode(fallbackClosestZone.position);
            if (fallbackNode != null && _agent.CalculatePath(startNode, fallbackNode, out List<Node> fallbackPath) && fallbackPath != null && fallbackPath.Count > 0)
            {
                _waitingForPath = true;
                _agent.WalkAlongPath(fallbackPath);
                return true;
            }
        }

        return false;
    }

    public bool TryMoveToExit(Transform exitZone, out bool alreadyAtExit)
    {
        alreadyAtExit = false;
        Node startNode = GetClosestNode(_agent.transform.position);
        if (startNode == null) return false;

        Node exitNode = ResolveExitNode(startNode, exitZone, out List<Node> precomputedPath);
        if (exitNode == null) return false;

        if (startNode == exitNode)
        {
            alreadyAtExit = true;
            return true;
        }

        List<Node> path = precomputedPath;
        if (path == null || path.Count == 0)
        {
            if (!_agent.CalculatePath(startNode, exitNode, out path) || path == null || path.Count == 0) return false;
        }

        _waitingForPath = true;
        _agent.WalkAlongPath(path);
        return true;
    }

    private Node ResolveExitNode(Node startNode, Transform exitZone, out List<Node> bestPath)
    {
        bestPath = null;

        if (Graph._current != null)
        {
            List<Node> endNodes = Graph._current.GetNodesByZone(ZoneType.End, excludeBlocked: true);
            if (endNodes != null && endNodes.Count > 0)
            {
                if (endNodes.Contains(startNode))
                {
                    bestPath = new List<Node> { startNode };
                    return startNode;
                }

                Node bestNode = null;
                int bestPathLength = int.MaxValue;

                foreach (Node candidate in endNodes)
                {
                    if (candidate == null || candidate.Blocked) continue;
                    if (_agent.CalculatePath(startNode, candidate, out List<Node> candidatePath) &&
                        candidatePath != null && candidatePath.Count > 0)
                    {
                        if (candidatePath.Count < bestPathLength)
                        {
                            bestPathLength = candidatePath.Count;
                            bestPath = candidatePath;
                            bestNode = candidate;
                        }
                    }
                }

                if (bestNode != null) return bestNode;

                Node closestFallback = null;
                float bestDistSqr = float.MaxValue;
                foreach (Node node in endNodes)
                {
                    if (node == null || node.Blocked) continue;
                    float distSqr = (node.transform.position - _agent.transform.position).sqrMagnitude;
                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        closestFallback = node;
                    }
                }

                if (closestFallback != null) return closestFallback;
            }
        }

        if (exitZone != null) return GetClosestNode(exitZone.position);

        return null;
    }
}