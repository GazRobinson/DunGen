//#define DEBUGGING

using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Collections;
using UnityEditor;
using UnityEngine;
using Diag = System.Diagnostics;
using Extensions;

public enum Direction
{
    NONE,
    UP,
    DOWN,
    LEFT,
    RIGHT
}

[System.Serializable]
public class RoomDetails
{
    public float MinArea = 4;
    public float MaxArea = 30;

    public float Buffer = 0.1f;

}

[System.Serializable]
public class DunGenParams
{
    public int RoomsToGenerate = 100;
    public RoomDetails roomDefinition;

    public int MaxDistIterations = 1000;

    public Material corridorMat;
    public float NudgeMultiplier = 0.5f;

    public bool BuildRooms = true;
    public bool LinkRooms = true;
    public bool Pathfind = true;
}

[System.Serializable]
public class DebugStuff
{
    public bool drawConnections = true;
    public bool drawPath = true;
    public bool drawPathfinder = true;
    public bool drawBounds = true;
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
    List<System.Tuple<Room, Room>> MST_Path = null;


    private Room start = null;
    private Room boss = null;
    private Room currentRoom = null;
    private Graph<Room> roomGraph;
    private Bounds fullBounds = new Bounds();

    //Debug
    private Diag.Stopwatch stopwatch;

    private float stepTime = 0.03f;

    IEnumerator Gen()
    {
        Diag.Stopwatch GenWatch = Diag.Stopwatch.StartNew();
        if (Diag.Stopwatch.IsHighResolution)
            Debug.Log("Using High Res stopwatch");
        stopwatch = Diag.Stopwatch.StartNew();
        CreateRooms();
        Debug.Log($"Create Rooms in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();


        yield return StartCoroutine(DistributeRooms());
        Debug.Log($"Distribute Rooms in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();

        yield return StartCoroutine(StartAndEnd());
        Debug.Log($"Start and End in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();

        yield return StartCoroutine(DistributeRooms());
        Debug.Log($"Re-Distribute Rooms in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();

        for (int i = 0; i < rooms.Count; i++)
        {
            fullBounds.Encapsulate(rooms[i].bounds);
        }
        Camera.main.orthographicSize = Mathf.Max(fullBounds.extents.x, fullBounds.extents.z) * 1.1f;
        Camera.main.transform.position = new Vector3(fullBounds.center.x, 20.0f, fullBounds.center.z);
        Debug.Log($"Complete. Bounds are {fullBounds.size}");


        if (args.BuildRooms)
        {
            yield return StartCoroutine(BuildRooms());
            Debug.Log($"Build Rooms in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();
        }
        if (args.LinkRooms)
        {
            yield return StartCoroutine(LinkRooms());
            Debug.Log($"Link Rooms in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();
            //yield return StartCoroutine(ConnectRooms()); //This was for when I used LineRenderers. Could come back
        }
        if (args.Pathfind)
        {
            yield return StartCoroutine(Pathfind());
            Debug.Log($"Pathfind in: {stopwatch.ElapsedMilliseconds}ms"); stopwatch.Restart();
        }
        Debug.Log($"Total Generation in: {GenWatch.ElapsedMilliseconds}ms");
        GenWatch.Stop();
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
        yield return null;
        int i = 0;
        int moves = 0;
        int skipped = 0;
        int comparisons = 0;
        Bounds b = new Bounds();
        int rmCount = rooms.Count;
#if DEBUGGING
        Diag.Stopwatch iterationTimer = new Diag.Stopwatch();
        Diag.Stopwatch totalIterationTimer = new Diag.Stopwatch();
        Diag.Stopwatch boundsTime = new Diag.Stopwatch();
        Diag.Stopwatch nudgeTime = new Diag.Stopwatch();
#endif
        List<Room> movingList = new List<Room>(rooms);
        List<Room> nextMovingList = new List<Room>();
        while (movingList.Count > 0 && i < args.MaxDistIterations)
        {
#if DEBUGGING
            iterationTimer.Restart();
            totalIterationTimer.Start();
#endif
            for (int j = 0; j < movingList.Count; j++)
            {
                Room current = movingList[j];
                Room other = null;
                for (int k = 0; k < rmCount; k++)
                {
                    other = rooms[k];
                    if (current == other/*j == k*/)
                        continue;
                    comparisons++;
                    if (Room.QuickerOverlap(ref current, ref other)) 
                    {
                        bool intersects = current.bounds.Intersects(other.bounds);
                        if (intersects)
                        {
#if DEBUGGING
    nudgeTime.Start();
#endif
                            b.SetMinMax(Vector3.Max(current.bounds.min, other.bounds.min),
                                Vector3.Min(current.bounds.max, other.bounds.max));
                            Vector3 s = b.size;
                            s.y = 0.0f;
                            s.x = current.Position.x < other.Position.x ? -s.x : s.x;
                            s.z = current.Position.z < other.Position.z ? -s.z : s.z;
                            if (Mathf.Abs(s.x) < Mathf.Abs(s.z))
                                s.z = 0.0f;
                            else
                                s.x = 0.0f;
                            //s.z = Random.value > 0.5f ? -s.z : s.z;
                            b.size = s;
                            if (s.sqrMagnitude > 0.1f)
                            {
                                current.Nudge(b.size * args.NudgeMultiplier);
                                other.Nudge(-b.size * args.NudgeMultiplier);
                                nextMovingList.Add(current);
                                nextMovingList.Add(other);
                                moves++;
                            }
#if DEBUGGING
                            nudgeTime.Stop();
#endif
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                if (debugStuff.vSlow)
                    yield return new WaitForEndOfFrame();
            }

#if DEBUGGING
            totalIterationTimer.Stop();
            iterationTimer.Stop();
            Debug.Log($"Time: {iterationTimer.ElapsedMilliseconds}. Moves: {moves}. List Length for next = {movingList.Count}");
#endif
            movingList = nextMovingList.Distinct().ToList();
            nextMovingList.Clear();
            if(debugStuff.slow)
                yield return new WaitForSeconds(0.025f);
            i++;

            if (moves == 0)
            {
                Debug.Log("Done early in " + i + " iterations");
                break;
            }
            else
            {
                moves = 0;
            }
        }
        /*for (int j = 0; j < rmCount; j++)
        {
            rooms[j].Square();
        }*/

#if DEBUGGING
        Debug.Log($"Comparisons = {comparisons}, Skips = {skipped} ");
        Debug.Log($"Time spent checking intersection = {boundsTime.ElapsedMilliseconds}\nTime spent nudging = {nudgeTime.ElapsedMilliseconds}\n" +
            $"Totla time spent iterating = {totalIterationTimer.ElapsedMilliseconds}");
#endif
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
        int removals = 0;
        roomGraph = new Graph<Room>();
        int c = rooms.Count;
        for (int i = 0; i < c; i++)
        {
            roomGraph.AddNode(rooms[i]);
        }

        yield return new WaitForEndOfFrame();
        List<Room> nearRooms = new List<Room>(rooms);
        for (int i = 0; i < rooms.Count; i++)
        {
            Room current = rooms[i];
            if (!current.gameObject.activeSelf)
            {
                continue;
            }
            nearRooms = new List<Room>(rooms);
            nearRooms.Remove(current);
            try
            {
                nearRooms.Sort((a, b) => Vector3.Distance(current.Position, a.Position) > Vector3.Distance(current.Position, b.Position) ? 1 : -1);
            }
            catch(System.Exception e)
            {
                Debug.LogException(e);
            }

            List<Room> validRooms = new List<Room>();
            int maxConnections = Random.Range(1, 5);
            int connections = 0;
            int index = 0;
            HashSet<Direction> taken = new HashSet<Direction>();
            while (connections < maxConnections && index < nearRooms.Count)
            {
                Room other = nearRooms[index];
                index++;
                if (!other.gameObject.activeSelf)
                {
                    continue;
                }
                if (!current.bufferBound.Intersects(other.bufferBound))
                {
                    continue;
                }
                Direction adjacency = Room.GetAdjacency(current, other);
                //TODO: also need to check that there is overlap NOT JUST adjacency
                if(adjacency == Direction.NONE)
                {
                    continue;
                }
                if (taken.Contains(adjacency))
                {
                    continue;
                }
                taken.Add(adjacency);
                validRooms.Add(other);
                /*Vector3 dir = other.Position - current.Position;
                float dist = dir.magnitude;
                dir.Normalize();
                RaycastHit[] info = Physics.RaycastAll(current.Position, dir, dist);
                if (info.Length > 0)
                {
                    bool valid = true;
                    foreach(RaycastHit hit in info)
                    {
                        if(hit.collider.gameObject == current.gameObject || hit.collider.gameObject == nearRooms[j].gameObject)
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
                        validRooms.Add(other);
                    }
                }
                else
                {
                    validRooms.Add(other);
                }*/
            }
            if (validRooms.Count > 0)
            {
                foreach (Room rm in validRooms)
                {
                    roomGraph.AddUndirectedEdge(current, rm, Mathf.FloorToInt(Vector3.Distance(current.Position, rm.Position)));
                    current.connections.Add(rm);
                    rm.connections.Add(current);
                }
            }
            else
            {
                if(current == start)
                {
                    Debug.LogError("The start room is not connected to anything!");
                }
                else
                {
                    removals++;
                    current.gameObject.SetActive(false);
                    roomGraph.Remove(current);
                }
            }
            if (debugStuff.slow)
                yield return new WaitForSeconds(0.05f);
        }
        Debug.Log($"Removals = {removals}");
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
                        /*
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
                        
                        lr.sharedMaterial = args.corridorMat;*/
                    }
                }
            }

            if (debugStuff.slow)
                yield return new WaitForSeconds(stepTime);
        }
    }

    IEnumerator BuildRooms()
    {
        yield return new WaitForEndOfFrame();
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].BuildRoom(ref mapTiles);
            if(debugStuff.slow)
                yield return new WaitForSeconds(stepTime);
        }
    }

    IEnumerator Pathfind()
    {        
        yield return new WaitForEndOfFrame();
        Diag.Stopwatch graphTimer = Diag.Stopwatch.StartNew();
        Graph<Room> MST = Graph<Room>.BuildMST(roomGraph, start);

        Debug.Log($"Build MST in: {graphTimer.ElapsedMilliseconds}ms"); graphTimer.Restart();
        //yield return new WaitForEndOfFrame();
        //yield return new WaitForSeconds(2.0f);

        MST_Path = Graph<Room>.TraverseMST(MST);

        Debug.Log($"Traverse MST in: {graphTimer.ElapsedMilliseconds}ms"); graphTimer.Stop();
        yield return new WaitForSeconds(0.5f);

        /*Room goal = boss;
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
        */

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
        stepTime = 2.0f / args.RoomsToGenerate;
        StartCoroutine(Gen());
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDrawGizmos()
    {
        /*if(debugStuff.drawPath && path != null && path.Count > 1)
        {
            Gizmos.color = Color.red;
            for (int i =0; i < path.Count -1; i++)
            {
                Gizmos.DrawLine(path[i].Position, path[i+1].Position);
            }
        }*/

        /*if (debugStuff.drawPathfinder && currentRoom != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(currentRoom.Position, 1.0f);
        }*/
        if (debugStuff.drawBounds)
        {
            /*for (int i = 0; i < rooms.Count; i++)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawCube(rooms[i].bounds.center, rooms[i].bounds.size);
            }*/
            for (int i = 0; i < rooms.Count; i++)
            {
                Color c = Color.yellow;
                c.a = 0.5f;
                Gizmos.DrawWireCube(rooms[i].bufferBound.center, rooms[i].bufferBound.size);
            }
            for (int i = 0; i < rooms.Count; i++)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(rooms[i].bounds.center, rooms[i].bounds.size);
            }
            if (fullBounds != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(fullBounds.center, fullBounds.size);
            }
        }

        if (debugStuff.drawConnections && roomGraph != null)
        {
            Color c = Color.green;
            if(debugStuff.drawPathfinder)
                c.a = 0.5f;
            Gizmos.color = c;
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
            for (int i = 0; i < roomGraph.Nodes.Count; i++)
            {
                if (roomGraph.Nodes[i].Neighbors != null && roomGraph.Nodes[i].Neighbors.Count > 0)
                {
                    for (int j = 0; j < roomGraph.Nodes[i].Neighbors.Count; j++)
                    {
                        Gizmos.DrawLine(roomGraph.Nodes[i].Value.Position, roomGraph.Nodes[i].Neighbors[j].Value.Position);
                    }
                }
            }
        }

        if (debugStuff.drawPathfinder && MST_Path != null && MST_Path.Count > 1)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < MST_Path.Count; i++)
            {
                Gizmos.DrawLine(MST_Path[i].Item1.Position, MST_Path[i].Item2.Position);
            }
        }
    }
}
