using System.Collections.Generic;

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
                    validNodes.Add(new KeyValuePair<GNode<T>, int>((GNode<T>)closedList[n],i));
                }
                else
                {
                    //Debug.Log("Node is in closed set");
                }
            } 
        }
        if(validNodes.Count > 0)
        {
            int closest = validNodes[0].Value;
            KeyValuePair<GNode<T>, int> closestEdge = validNodes[0];
            for (int i=0; i < validNodes.Count; i++)
            {
                int cost = validNodes[i].Value;  //current.Costs[validNodes[i].Value];
                if(cost < closest)
                {
                    closest = cost;
                    closestEdge = validNodes[i];
                }
            }
            closestEdge.Key.MST_Neighbors.Add((GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            closedSet.Add( (GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            closedList.Add((GNode<T>)closestEdge.Key.Neighbors[closestEdge.Value]);
            //next = (GNode<T>)current.Neighbors[closestEdge.Value];
        }


    }

    public void AddNode(GNode<T> node)
    {
        // adds a node to the graph
        nodeSet.Add(node);
    }

    public void AddNode(T value)
    {
        // adds a node to the graph
        nodeSet.Add(new GNode<T>(value));
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
}