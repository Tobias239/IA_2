using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 10f;

    private Coroutine _moveRoutine;

    protected Vector3 _velocity;

    public event Action OnPathCompleted;
    public bool IsFollowingPath => _moveRoutine != null;

    protected virtual void OnDisable()
    {
        StopPathMovement();
        _velocity = Vector3.zero;
    }

    public void Reposition(Node targetNode)
    {
        if (targetNode == null) return;

        StopPathMovement();
        _velocity = Vector3.zero;
        transform.position = targetNode.transform.position;
    }

    public void Reposition(Vector3 worldPosition)
    {
        StopPathMovement();
        _velocity = Vector3.zero;
        transform.position = worldPosition;
    }

    public void StopPathMovement()
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }
    }

    public bool CalculatePath(Node startNode, Node endNode, out List<Node> path)
    {
        path = null;
        if (startNode == null || endNode == null) return false;

        path = Pathfinding.AStar(startNode, endNode);
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("No se pudo encontrar un camino para el agente.");
            return false;
        }

        return true;
    }

    public void CalculatePath(Node startNode, Node endNode)
    {
        if (CalculatePath(startNode, endNode, out List<Node> path)) WalkAlongPath(path);
    }

    public void WalkAlongPath(List<Node> path)
    {
        if (path == null || path.Count == 0) return;

        StopPathMovement();
        _velocity = Vector3.zero;
        _moveRoutine = StartCoroutine(MoveAlongPathRoutine(path));
    }

    private IEnumerator MoveAlongPathRoutine(List<Node> path)
    {
        if (path[0] == null)
        {
            _moveRoutine = null;
            OnPathCompleted?.Invoke();
            yield break;
        }

        int startIndex = (path.Count > 1 && Vector3.Distance(transform.position, path[0].transform.position) < 0.1f) ? 1 : 0;

        for (int i = startIndex; i < path.Count; i++)
        {
            if (path[i] == null) continue;

            Vector3 targetPosition = path[i].transform.position;
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                Vector3 toTarget = targetPosition - transform.position;
                Vector3 stepDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
                _velocity = stepDirection * _moveSpeed;

                transform.position = Vector3.MoveTowards(transform.position, targetPosition, _moveSpeed * Time.deltaTime);

                if (stepDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(stepDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
                }

                yield return null;
            }

            transform.position = targetPosition;
        }

        _velocity = Vector3.zero;
        _moveRoutine = null;
        OnPathCompleted?.Invoke();
    }
}