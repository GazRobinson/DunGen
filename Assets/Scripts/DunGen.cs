using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[System.Serializable]
public class RoomDetails
{
    public float MinArea = 4;
    public float MaxArea = 30;

    [Range(1.01f, 2.0f)]
    public float Buffer = 1.1f;

}

[System.Serializable]
public class DunGenParams
{
    public int RoomsToGenerate = 100;
    public RoomDetails roomDefinition;

    public int MaxDistIterations = 1000;

    public Material corridorMat;
    public float NudgeMultiplier = 0.5f;
}

[System.Serializable]
public class DebugStuff
{
    public bool drawConnections = true;
    public bool drawPath = true;
    public bool drawPathfinder = true;
    public bool verbose = true;
    public bool slow = true;
    public bool vSlow = true;
}

[System.Serializable]
public class Assets
{
    public GameObject chest;
    public GameObject smallKey;
    public GameObject bigKey;
    public GameObject smallLock;
    public GameObject bigLock;
    public GameObject skull;
}
struct Connection
{
    public Room A;
    public Room B;
}


[System.Serializable]
public class RoomTiles
{
    [Range(0.0f, 1.0f)]
    public float FloorDetailChance = 0.1f;
    [Range(0.0f, 1.0f)]
    public float FloorRockChance = 0.05f;

    public GameObject[] Floor;
    public GameObject Rocks;
    public GameObject Wall;
    public GameObject Corner;
    public GameObject Door;
}

public class DunGen : MonoBehaviour
{
    public DunGenParams args;
    public Assets mapAssets;
    public RoomTiles mapTiles;
    public DebugStuff debugStuff;
    public List<Room> rooms = new List<Room>();

    public Room roomFab = null;
    List<Room> path = null;


    private Room start = null;
    private Room boss = null;
    private Room currentRoom = null;
    private Graph<Room> roomGraph;
    IEnumerator Gen()
    {
        CreateRooms();
        yield return StartCoroutine(DistributeRooms());
        yield return StartCoroutine(StartAndEnd());
        yield return StartCoroutine(DistributeRooms());
        yield return StartCoroutine(LinkRooms());
        yield return StartCoroutine(ConnectRooms());
        yield return StartCoroutine(BuildRooms());
        yield return StartCoroutine(Pathfind());
    }

    void CreateRooms()
    {
        for(int i =0; i < args.RoomsToGenerate; i++)
        {
            float radius = Mathf.Sqrt(args.RoomsToGenerate) * Mathf.Sqrt(args.roomDefinition.MinArea);
            Vector2 p = Random.insideUnitCircle* radius * 0.5f;
            Vector3 pos = new Vector3(p.x, 0.0f, p.y);
            rooms.Add(Instantiate<Room>(roomFab));
            rooms.Last().Init(args.roomDefinition.MinArea, args.roomDefinition.MaxArea, args.roomDefinition.Buffer, pos);
            rooms.Last().gameObject.name = "Room " + i;
        }
    }

    IEnumerator DistributeRooms()
    {
        yield return new WaitForSeconds(0.1f);
        int i = 0;
        int moves = 0;
        while (i < args.MaxDistIterations)
        {
            for (int j = 0; j < rooms.Count; j++)
            {
                for (int k = 0; k < rooms.Count; k++)
                {
                    if (j == k)
                        continue;
                    if (rooms[j].bounds.Intersects(rooms[k].bounds))
                    {
                        Bounds b = new Bounds();
                        b.SetMinMax(Vector3.Max(rooms[j].bounds.min, rooms[k].bounds.min),
                            Vector3.Min(rooms[j].bounds.max, rooms[k].bounds.max));
                        Vector3 s = b.size;
                        s.y = 0.0f;
                        s.x = rooms[j].Position.x < rooms[k].Position.x ? -s.x : s.x;
                        s.z = rooms[j].Position.z < rooms[k].Position.z ? -s.z : s.z;
                        if (Mathf.Abs(s.x) < Mathf.Abs(s.z))
                        {
                            s.z = 0.0f;
                        }
                        else
                        {
                            s.x = 0.0f;
                        }
                        //s.z = Random.value > 0.5f ? -s.z : s.z;
                        b.size = s;
                        if (s.magnitude > 1.0f)
                        {
                            rooms[j].Nudge(b.size * args.NudgeMultiplier);
                            rooms[k].Nudge(-b.size  * args.NudgeMultiplier);
                            moves++;
                        }
                    }
                }
                if (debugStuff.vSlow)
                    yield return new WaitForEndOfFrame();
            }
            if(debugStuff.slow)
                yield return new WaitForSeconds(0.025f);
            i++;
            if(debugStuff.verbose)
                Debug.Log("Moves: " + moves);
            if (moves == 0)
            {
                Debug.Log("Done early");
                break;
            }
            else
            {
                moves = 0;
            }
        }

        Debug.Log("Complete");
    }

    IEnumerator StartAndEnd()
    {
        yield return new WaitForEndOfFrame();
        start = null;
        boss = null;

        float minZ = 0.0f;
        float maxZ = 0.0f;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].Position.z < minZ)
            {
                minZ = rooms[i].Position.z;
                start = rooms[i];
            }
            if (rooms[i].Position.z > maxZ)
            {
                maxZ = rooms[i].Position.z;
                boss = rooms[i];
            }
        }

        start?.MakeStart();
        boss?.MakeBoss();
        if (boss != null)
        {
            Transform t = Instantiate<GameObject>(mapAssets.skull, boss.transform).transform;
            t.localPosition = Vector3.zero;
            t.LookAt(t.position + Vector3.back);
        }
        if (debugStuff.slow)
            yield return new WaitForSeconds(0.5f);
    }

    IEnumerator LinkRooms()
    {
        roomGraph = new Graph<Room>();
        for (int i = 0; i < rooms.Count; i++)
        {
            roomGraph.AddNode(rooms[i]);
        }

            yield return new WaitForEndOfFrame();
        List<Room> nearRooms = new List<Room>(rooms);
        for (int i = 0; i < rooms.Count; i++)
        {
            nearRooms = new List<Room>(rooms);
            nearRooms.Remove(rooms[i]);
            try
            {
                nearRooms.Sort((a, b) => Vector3.Distance(rooms[i].Position, a.Position) > Vector3.Distance(rooms[i].Position, b.Position) ? 1 : -1);
            }
            catch(System.Exception e)
            {
                Debug.LogException(e);
            }
            List<Room> validRooms = new List<Room>();
            int cons = Random.Range(2, 4);
            for (int j = 0; j < cons; j++)
            {
                Vector3 dir = nearRooms[j].Position - rooms[i].Position;
                float dist = dir.magnitude;
                dir.Normalize();
                RaycastHit[] info = Physics.RaycastAll(rooms[i].Position, dir, dist);
                if (info.Length > 0)
                {
                    bool valid = true;
                    foreach(RaycastHit hit in info)
                    {
                        if(hit.collider.gameObject == rooms[i].gameObject || hit.collider.gameObject == nearRooms[j].gameObject)
                        {
                            continue;
                        }
                        else
                        {
                            if(debugStuff.verbose)
                                Debug.LogWarning("Intersection! Discarding connection");
                            valid = false;
                        }
                    }
                    if (valid)
                    {
                        validRooms.Add(nearRooms[j]);
                    }
                }
                else
                {
                    validRooms.Add(nearRooms[j]);
                }
            }
            foreach (Room rm in validRooms)
            {
                roomGraph.AddUndirectedEdge(rooms[i], rm, Mathf.FloorToInt(Vector3.Distance(rooms[i].Position, rm.Position)));
                rooms[i].connections.Add(rm);
                rm.connections.Add(rooms[i]);
            }

            if (debugStuff.slow)
                yield return new WaitForSeconds(0.05f);
        }
    }
    IEnumerator ConnectRooms()
    {
        yield return new WaitForEndOfFrame();
        HashSet<Connection> connections = new HashSet<Connection>();

        for (int i = 0; i < rooms.Count; i++)
        {
            Room a = rooms[i];
            if(a.connections.Count > 0)
            {
                List<Vector3> path;
                foreach(Room b in a.connections)
                {
                    Connection c1;
                    c1.A = a;
                    c1.B = b;
                    Connection c2;
                    c2.B = a;
                    c2.A = b;
                    if (connections.Contains(c1) || connections.Contains(c2))
                    {
                        if (debugStuff.verbose)
                            Debug.Log("Discarding duplicate");
                        continue;
                    }
                    else
                    {
                        connections.Add(c1);

                        path = GetPath(a.Position, b.Position);

                        GameObject go = new GameObject("Connection", typeof(LineRenderer));
                        go.transform.parent = transform;
                        LineRenderer lr = go.GetComponent<LineRenderer>();
                        lr.numCornerVertices = 0;
                        lr.numCapVertices = 0;
                        lr.startWidth = 0.5f;
                        lr.positionCount = path.Count;
                        path[0] = a.trueBounds.ClosestPoint(path[1]);
                        path[path.Count-1] = b.trueBounds.ClosestPoint(path[path.Count - 2]);
                        for (int p= 0; p < path.Count; p++)
                        {
                            lr.SetPosition(p, path[p]);
                        }
                        
                        lr.sharedMaterial = args.corridorMat;
                    }
                }
            }

            if (debugStuff.slow)
                yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator BuildRooms()
    {
        yield return new WaitForEndOfFrame();
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].BuildRoom(ref mapTiles);
            if(debugStuff.slow)
                yield return new WaitForSeconds(0.15f);
        }
    }

    IEnumerator Pathfind()
    {        
        yield return new WaitForEndOfFrame();
        Room goal = boss;
        float d(Room rm, Room g) { return Vector3.Distance(rm.Position, g.Position); }
        float h(Room rm) { return d(rm, goal); }

        List<Room> openSet = new List<Room>();
        openSet.Add(start);

        Dictionary<Room, float> gScore = new Dictionary<Room, float>();
        gScore.Add(start, 0);

        Dictionary<Room, float> fScore = new Dictionary<Room, float>();
        fScore.Add(start, h(start));

        Dictionary<Room, Room> cameFrom = new Dictionary<Room, Room>();

        path = new List<Room>();
        bool success = false;
        while (openSet.Count > 0)
        {
            Room current = openSet.First();
            for (int i = 1; i < openSet.Count; i++)
            {
                if (fScore[openSet[i]] < fScore[current])
                {
                    current = openSet[i];
                }
            }
            //if(current == goal)
            
                path = new List<Room>();
                Room head = current;
                path.Add(head);
                while(head != null)
                {
                    if (cameFrom.ContainsKey(head))
                    {
                        head = cameFrom[head];
                        path.Insert(0,head);
                    }
                    else
                    {
                    head = null;
                    }
                }
                if (current == goal)
                {
                    Debug.Log("Complete");
                    success = true;
                    break;
                }
            
            currentRoom = current;
            openSet.Remove(current);

            foreach(Room neighbour in current.connections)
            {
                float tentative_gScore = gScore[current] + d(current, neighbour);
                float currentNeighbourScore = gScore.GetValueOrDefault(neighbour, Mathf.Infinity);
                if (tentative_gScore < currentNeighbourScore)
                {
                    cameFrom[neighbour] = current;
                    gScore[neighbour] = tentative_gScore;
                    fScore[neighbour] = tentative_gScore + h(neighbour);
                    if (!openSet.Contains(neighbour))
                    {
                        openSet.Add(neighbour);
                    }
                }
            }
            if (debugStuff.slow)
                yield return new WaitForSeconds(0.2f);
        }

        if (!success)
        {
            Debug.LogWarning("Failed pathfinding?!");

        }


    }
    List<Vector3> GetPath(Vector3 start, Vector3 end, bool doY = false)
    {
        float xChange = end.x - start.x;
        float yChange = end.y - start.y;
        float zChange = end.z - start.z;

        List<Vector3> a = new List<Vector3>();

        Vector3 p;
        if (xChange > zChange)
        {
            p = start; a.Add(p);
            p.x += xChange; a.Add(p);
            if (doY)
            {
                p.y += yChange; a.Add(p);
            }
            p.z += zChange; a.Add(p);

        }
        else
        {
            p = start; a.Add(p);
            p.z += zChange; a.Add(p);
            if (doY)
            {
                p.y += yChange; a.Add(p);
            }
            p.x += xChange; a.Add(p);
        }

        return a;
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(Gen());
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        if (debugStuff.drawConnections && roomGraph != null)
        {
            /*foreach (Room rm in rooms)
            {
                if (rm.connections.Count > 0)
                {
                    foreach (Room rm2 in rm.connections)
                    {
                        Gizmos.DrawLine(rm.Position, rm2.Position);
                    }
                }
            }*/
            for(int i=0; i < roomGraph.Nodes.Count; i++)
            {
                if (roomGraph.Nodes[i].Neighbors != null && roomGraph.Nodes[i].Neighbors.Count > 0)
                {
                    for (int j=0; j < roomGraph.Nodes[i].Neighbors.Count; j++)
                    {
                        Gizmos.DrawLine(roomGraph.Nodes[i].Value.Position, roomGraph.Nodes[i].Neighbors[j].Value.Position);
                    }
                }
            }
        }
        if(debugStuff.drawPath && path != null && path.Count > 1)
        {
            Gizmos.color = Color.red;
            for (int i =0; i < path.Count -1; i++)
            {
                Gizmos.DrawLine(path[i].Position, path[i+1].Position);
            }
        }

        if(debugStuff.drawPathfinder && currentRoom != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(currentRoom.Position, 1.0f);
        }
    }
}
