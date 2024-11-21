using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class Tree<T>
{
    private NodeList<T> nodeSet;

    public Tree() { root = null; }
    public Tree(T _root) { root = new GNode<T>(_root); }
    public Tree(NodeList<T> nodeSet)
    {
        if (nodeSet == null)
            this.nodeSet = new NodeList<T>();
        else
            this.nodeSet = nodeSet;
    }

    private GNode<T> root;

    public GNode<T> Root { get { return root; } }
    public void SetRoot(T _Root)
    {
        root = (GNode < T > )nodeSet.FindByValue(_Root);
    }


}

public class Graph<T>
{
    private GNode<T> Root = null;
    private NodeList<T> nodeSet;
    private Tree<T> minimumTree = new Tree<T>();

    Graph<T> tree = null;

    public Graph() : this(null) { }
    public Graph(NodeList<T> nodeSet)
    {
        if (nodeSet == null)
            this.nodeSet = new NodeList<T>();
        else
            this.nodeSet = nodeSet;
    }


    public GNode<T> AddNode(GNode<T> node)
    {
        // adds a node to the graph
        nodeSet.Add(node);
        return node;
    }

    public GNode<T> AddNode(T value)
    {
        // adds a node to the graph
        return AddNode(new GNode<T>(value));
    }
    public void AddRootNode(GNode<T> node)
    {
        // adds a node to the graph
        nodeSet.Add(node);
        Root = (GNode<T>)nodeSet[0];
    }
    public void AddRootNode(T value)
    {
        // adds a node to the graph
        AddNode(new GNode<T>(value));
        Root = (GNode<T>)nodeSet[0];
    }

    public void AddDirectedEdge(GNode<T> from, GNode<T> to, int cost)
    {
        from.Neighbors.Add(to);
        from.Costs.Add(cost);
    }

    public void AddUndirectedEdge(T from, T to, int cost)
    {
        GNode<T> fromNode = (GNode<T>)nodeSet.FindByValue(from);
        GNode<T> toNode = (GNode<T>)nodeSet.FindByValue(to);
        
        AddUndirectedEdge(fromNode, toNode, cost);
    }

    public void AddUndirectedEdge(GNode<T> from, GNode<T> to, int cost)
    {
        from.Neighbors.Add(to);
        from.Costs.Add(cost);

        to.Neighbors.Add(from);
        to.Costs.Add(cost);
    }

    public bool Contains(T value)
    {
        return nodeSet.FindByValue(value) != null;
    }

    public bool Remove(T value)
    {
        // first remove the node from the nodeset
        GNode<T> nodeToRemove = (GNode<T>)nodeSet.FindByValue(value);
        if (nodeToRemove == null)
            // node wasn't found
            return false;

        // otherwise, the node was found
        nodeSet.Remove(nodeToRemove);

        // enumerate through each node in the nodeSet, removing edges to this node
        foreach (GNode<T> gnode in nodeSet)
        {
            int index = gnode.Neighbors.IndexOf(nodeToRemove);
            if (index != -1)
            {
                // remove the reference to the node and associated cost
                gnode.Neighbors.RemoveAt(index);
                gnode.Costs.RemoveAt(index);
            }
        }

        return true;
    }

    public NodeList<T> Nodes
    {
        get
        {
            return nodeSet;
        }
    }

    public int Count
    {
        get { return nodeSet.Count; }
    }


    /// <summary>
    /// This should be done outside of the class to create a new Graph which only has the minimum span.
    /// </summary>
    /// <param name="start"></param>
    public void BuildMinimumTree(T start)
    {
        List<GNode<T>> closedList = new List<GNode<T>>();
        HashSet<GNode<T>> closedSet = new HashSet<GNode<T>>();

        GNode<T> startNode = (GNode<T>)nodeSet.FindByValue(start);
        tree = new Graph<T>();
        minimumTree = new Tree<T>(startNode.Value);
        tree.AddNode(startNode.Value);


        closedSet.Add(startNode);
        closedList.Add(startNode);

        //Node that we're coming from and index of neighbour that is valid
        List<KeyValuePair<GNode<T>, int>> validNodes = new List<KeyValuePair<GNode<T>, int>>();
        for (int n = 0; n < closedList.Count; n++)
        {
            for (int i = 0; i < closedList[n].Neighbors.Count; i++)
            {
                if (!closedSet.Contains((GNode<T>)closedList[n].Neighbors[i]))
                {
                    validNodes.Add(new KeyValuePair<GNode<T>, int>((GNode<T>)closedList[n], i));
                }
                else
                {
                    //Debug.Log("Node is in closed set");
                }
            }
        }
        if (validNodes.Count > 0)
        {
            int closest = validNodes[0].Value;
            KeyValuePair<GNode<T>, int> closestEdge = validNodes[0];
            for (int i = 0; i < validNodes.Count; i++)
            {
                int cost = validNodes[i].Value;  //current.Costs[validNodes[i].Value];
                if (cost < closest)
                {
                    closest = cost;
                    closestEdge = validNodes[i];
                }
            }
            closestEdge.Key.MST_Neighbors.Add((GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            closedSet.Add((GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            closedList.Add((GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            //next = (GNode<T>)current.Neighbors[closestEdge.Value];
        }
    }

    public static Graph<T> BuildMST(Graph<T> graph, T root)
    {
        bool doing = true;
        int cycles = 0;
        Graph<T> tree = new Graph<T>();
        tree.AddRootNode(root);

        List<Tuple<Node<T>, int>> validPaths = new List<Tuple<Node<T>, int>>();
        while (doing && cycles < 1000)
        {
            validPaths.Clear();
            foreach (Node<T> treeNode in tree.Nodes)
            {
                Node<T> graphNode = graph.nodeSet.FindByValue(treeNode.Value);
                Debug.Assert(graphNode != null, $"Could not find '{graphNode.Value}'");
                Debug.Assert(graphNode.Neighbors != null, $"Node '{graphNode.Value}' has null neighbours.");
                for (int i = 0; i < graphNode.Neighbors.Count; i++)
                {
                    //Check to see if the neighbour is already in the tree
                    //Mayb add a hash set to deal with this easier
                    Node<T> neighbour = tree.Nodes.FindByValue(graphNode.Neighbors[i].Value);
                    if (neighbour == null)
                    {
                        validPaths.Add(new Tuple<Node<T>, int>(graphNode, i));
                    }
                    else
                    {
                        //Node is already in tree
                    }
                }
            }
            if (validPaths.Count > 0)
            {
                int closestScore = int.MaxValue;
                Tuple<Node<T>, int> best = null;
                foreach (Tuple<Node<T>, int> edge in validPaths)
                {
                    if (((GNode<T>)edge.Item1).Costs[edge.Item2] < closestScore)
                    {
                        closestScore = ((GNode<T>)edge.Item1).Costs[edge.Item2];
                        best = edge;
                    }
                }

                if (best != null)
                {
                    GNode<T> newNode = tree.AddNode(best.Item1.Neighbors[best.Item2].Value);
                    tree.AddUndirectedEdge((GNode<T>)tree.nodeSet.FindByValue(best.Item1.Value), newNode, closestScore);
                }
            }
            else
            {
                //We're done!
                Debug.Log($"Complete in {cycles} cycles!");
                doing = false;
            }
            cycles++;
        }

        return tree;
    }
    public static List<Tuple<T,T>> TraverseMST(Graph<T> MST)
    {
        List<Tuple<T, T>> path = new List<Tuple<T, T>>();
        Stack<Node<T>> openSet = new Stack<Node<T>>(); //Replace with priority queue
        HashSet<T> visited = new HashSet<T>();
        Node<T> current = null;

        Debug.Assert(MST.Root != null);

        openSet.Push(MST.Root);
        int cycles = 0;
        while (openSet.Count > 0 && cycles < (MST.nodeSet.Count* MST.nodeSet.Count))
        {
            current = openSet.Pop();
            Debug.Assert(current != null);
            Debug.Assert(current.Neighbors != null, $"Node '{current.Value}' has null neighbours.");
            if (current.Neighbors.Count > 0)
            {
                //TODO: Go through these based on Cost
                for (int i = 0; i < current.Neighbors.Count; i++)
                {
                    if (!visited.Contains(current.Neighbors[i].Value))
                    {
                        openSet.Push(current.Neighbors[i]);
                        path.Add(new Tuple<T, T>(current.Value, current.Neighbors[i].Value));
                    }
                }
            }
            visited.Add(current.Value);
            cycles++;
        }



        return path;
    }

}
