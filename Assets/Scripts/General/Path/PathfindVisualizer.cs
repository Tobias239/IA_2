using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathfindVisualizer : MonoBehaviour
{
    private enum PathfindingMethod { BFS, Dijkstra, GreedyBFS, AStar }

    [SerializeField] private Graph _graph;
    [SerializeField] private Agent _agent;
    [SerializeField] private PathfindingMethod _currentPathfindingMethod;
    [SerializeField] private Pathfinding.HeuristicMethod _currentHeuristicMethod;
    [SerializeField] private float _stepDelay = 0.1f;
    [SerializeField] private float _pathStepDelay = 0.01f;

    private Node _startNode;
    private Node _endNode;
    private Coroutine _paintRoutine;

    private IEnumerator Start()
    {
        yield return null;
        SubscribeNodes();
    }

    private void OnDisable()
    {
        UnsubscribeNodes();
    }

    private void SubscribeNodes()
    {
        if (_graph == null) return;
        foreach (Node node in _graph.GetAllGridNodes)
        {
            if (node != null)
            {
                node.OnNodeClicked += NodeClicked;
                node.OnNodeRightClicked += NodeRightClicked;
            }
        }
    }

    private void UnsubscribeNodes()
    {
        if (_graph == null) return;
        foreach (Node node in _graph.GetAllGridNodes)
        {
            if (node != null)
            {
                node.OnNodeClicked -= NodeClicked;
                node.OnNodeRightClicked -= NodeRightClicked;
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) ExecutePathfindVisualizer();
    }

    public void NodeClicked(Node node)
    {
        _startNode = HandleNodeClicked(node, _startNode);
        if (_startNode != null)
        {
            _startNode.ChangeColor(Color.green);
            if (_startNode == _endNode) _endNode = null;
        }
    }

    public void NodeRightClicked(Node node)
    {
        _endNode = HandleNodeClicked(node, _endNode);
        if (_endNode != null)
        {
            _endNode.ChangeColor(Color.red);
            if (_startNode == _endNode) _startNode = null;
        }
    }

    private Node HandleNodeClicked(Node node, Node current)
    {
        if (current != null)
        {
            current.ChangeColor(Color.white);
            if (current == node) return null;
        }
        return node;
    }

    private void ExecutePathfindVisualizer()
    {
        if (_startNode == null || _endNode == null) return;

        List<Node> visited = new();
        List<Node> path = _currentPathfindingMethod switch
        {
            PathfindingMethod.BFS       => Pathfinding.BreadthFirstSearch(_startNode, _endNode, visited),
            PathfindingMethod.Dijkstra  => Pathfinding.Dijkstra(_startNode, _endNode, visited),
            PathfindingMethod.GreedyBFS => Pathfinding.GreedyBFS(_startNode, _endNode, _currentHeuristicMethod, visited),
            PathfindingMethod.AStar     => Pathfinding.AStar(_startNode, _endNode, _currentHeuristicMethod, visited), _ => new()
        };

        LogResult(visited, path);

        foreach (Node item in _graph.GetAllGridNodes)
        {
            if (item == _startNode || item == _endNode) continue;
            item.ChangeColor(Color.white);
        }

        if (_paintRoutine != null) StopCoroutine(_paintRoutine);
        
        _paintRoutine = StartCoroutine(PaintSearch(visited, path));
    }

    private void ExecuteAgent(List<Node> path)
    {
        if (_agent == null || _startNode == null || path == null || path.Count == 0) return;

        _agent.Reposition(_startNode);
        _agent.WalkAlongPath(path);
    }

    private void LogResult(List<Node> visited, List<Node> path)
    {
        int pathCost = 0;
        for (int i = 1; i < path.Count; i++)
        pathCost += path[i].Cost;

        Debug.Log($"{_currentPathfindingMethod} | Nodos explorados: {visited.Count} | Pasos del camino: {Mathf.Max(path.Count - 1, 0)} | Costo del camino: {pathCost}");
    }

    private IEnumerator PaintSearch(List<Node> visited, List<Node> path)
    {
        PaintStartAndEnd();

        foreach (Node node in visited)
        {
            if (node == _startNode || node == _endNode) continue;

            node.ChangeColor(Color.blue);
            yield return new WaitForSeconds(_stepDelay);
            node.ChangeColor(Color.gray);
        }

        for (int i = 0; i < path.Count; i++)
        {
            path[i].ChangeColor(Color.Lerp(Color.green, Color.red, (float)i / path.Count));
            yield return new WaitForSeconds(_pathStepDelay);
        }

        PaintStartAndEnd();
        ExecuteAgent(path);
    }

    private void PaintStartAndEnd()
    {
        if (_startNode != null) _startNode.ChangeColor(Color.green);
        if (_endNode != null) _endNode.ChangeColor(Color.red);
    }

    private void OnValidate()
    {
        if (_graph == null) _graph = FindAnyObjectByType<Graph>();
        if (_agent == null) _agent = FindAnyObjectByType<Agent>();
    }
}