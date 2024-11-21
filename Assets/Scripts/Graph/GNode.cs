using System.Collections.Generic;

public class Node<T>
{
    // Private member-variables
    private T data;
    private NodeList<T> neighbors = null;
    private NodeList<T> mstNeighbors = null;

    public Node() { }
    public Node(T data) : this(data, null) { }
    public Node(T data, NodeList<T> neighbors)
    {
        this.data = data;
        this.neighbors = neighbors;
    }

    public T Value
    {
        get
        {
            return data;
        }
        set
        {
            data = value;
        }
    }

    public NodeList<T> Neighbors
    {
        get
        {
            return neighbors;
        }
        set
        {
            neighbors = value;
        }
    }
    public NodeList<T> MST_Neighbors
    {
        get
        {
            return mstNeighbors;
        }
        set
        {
            mstNeighbors = value;
        }
    }
}

public class GNode<T> : Node<T>
{
    private List<int> costs;

    public GNode() : base() { }
    public GNode(T value) : base(value) { }
    public GNode(T value, NodeList<T> neighbors) : base(value, neighbors) { }

    new public NodeList<T> Neighbors
    {
        get
        {
            if (base.Neighbors == null)
                base.Neighbors = new NodeList<T>();

            return base.Neighbors;
        }
    }
    new public NodeList<T> MST_Neighbors
    {
        get
        {
            if (base.MST_Neighbors == null)
                base.MST_Neighbors = new NodeList<T>();

            return base.MST_Neighbors;
        }
    }
    public GNode<T> GetNearest()
    {
        int nearest = int.MaxValue;
        int nearestIndex = -1;
        for (int i = 0; i < costs.Count; i++)
        {
            if (costs[i] < nearest)
            {
                nearest = costs[i];
                nearestIndex = i;
            }
        }
        if (nearestIndex > -1)
        {            
            return (GNode<T>)Neighbors[nearestIndex];
        }
        else
        {
            return null;
        }
    }

    public List<int> Costs
    {
        get
        {
            if (costs == null)
                costs = new List<int>();

            return costs;
        }
    }
}