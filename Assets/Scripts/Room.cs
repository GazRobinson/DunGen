using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Room : MonoBehaviour
{
    public Vector3 Position;
    public Vector3 Size;
    public Bounds bounds;
    public Bounds trueBounds;
    public float Radius = 0.0f;
    float buffer = 1.0f;

    public bool StartRoom = false;
    public bool BossRoom = false;

    public HashSet<Room> connections = new HashSet<Room>();
    private GameObject[,] tiles;

    public void Init(float min, float max, float buffer, Vector3 position)
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        mesh.triangles = mesh.triangles.Reverse().ToArray();
        mesh.RecalculateNormals();

        this.buffer = buffer;
        float a = Random.Range(min, max);
        float sqr = Mathf.Sqrt(a);
        float w = Mathf.Round(Mathf.Max(4, sqr + Random.Range(-sqr/2, sqr/2))); //Mathf.Round(Mathf.Max(4, (a / (Random.Range(2, 6)))));
        float d = Mathf.Round(Mathf.Max(4, a / w));
        if(Random.value < 0.5f)
        {
            float t = d;
            d = w;
            w = t;
        }

        Position = position;
        Size = new Vector3(w, 3.0f, d);

        Position.x = Mathf.Round(Position.x) + (Size.x % 2 > 0 ? 0.5f : 0.0f);
        Position.z = Mathf.Round(Position.z) + (Size.z % 2 > 0 ? 0.5f : 0.0f);

        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position;
        Radius = Mathf.Max(Size.x * 0.5f, Size.z * 0.5f);
    }

    public void MakeStart()
    {
        Size = new Vector3(6.0f, 3.0f, 6.0f);

        Position.x = Mathf.Round(Position.x) + (Size.x % 2 > 0 ? 0.5f : 0.0f);
        Position.z = Mathf.Round(Position.z) + (Size.z % 2 > 0 ? 0.5f : 0.0f);

        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position;
        Radius = Mathf.Max(Size.x * 0.5f, Size.z * 0.5f);

        gameObject.name = "Start";
        GetComponent<Renderer>().material.SetColor("_Color", Color.green);
    }

    public void MakeBoss()
    {
        Size = new Vector3(10.0f, 3.0f, 10.0f);

        Position.x = Mathf.Round(Position.x) + (Size.x % 2 > 0 ? 0.5f : 0.0f);
        Position.z = Mathf.Round(Position.z) + (Size.z % 2 > 0 ? 0.5f : 0.0f);

        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position;
        Radius = Mathf.Max(Size.x * 0.5f, Size.z * 0.5f);

        gameObject.name = "End";
        GetComponent<Renderer>().material.SetColor("_Color", Color.red);
    }

    public void Nudge(Vector3 dist)
    {
        Position += dist;
        Position.x = Mathf.Round(Position.x) + (Size.x % 2 > 0 ? 0.5f : 0.0f);
        Position.z = Mathf.Round(Position.z) + (Size.z % 2 > 0 ? 0.5f : 0.0f);

        transform.position = Position;
        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
    }

    public void BuildRoom(ref RoomTiles tileFabs)
    {
        tiles = new GameObject[Mathf.RoundToInt(Size.x), Mathf.RoundToInt(Size.z)];
        transform.localScale = Vector3.one;
        Vector3 origin = transform.position - (Size / 2.0f);
        for(int y = 0; y < tiles.GetLength(1); y++)
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
            {
                if (IsEdge(x, y))
                {
                    if (IsCorner(x, y))
                    {
                        tiles[x, y] = Instantiate<GameObject>(tileFabs.Corner, origin + new Vector3(x,0.0f,y), Quaternion.identity, transform);
                    }
                    else
                    {
                        tiles[x, y] = Instantiate<GameObject>(tileFabs.Wall, origin + new Vector3(x, 0.0f, y), Quaternion.identity, transform);
                        int facing = FindFacing(x, y);
                        tiles[x, y].transform.localRotation = Quaternion.AngleAxis(facing * 90.0f, Vector3.up);
                    }
                }
                else
                {
                    tiles[x, y] = Instantiate<GameObject>(tileFabs.Floor[Random.value < tileFabs.FloorDetailChance ? 1 : 0], origin + new Vector3(x, 0.0f, y), Quaternion.identity, transform);
                    tiles[x, y].transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 4) * 90.0f, Vector3.up);
                    if (Random.value < tileFabs.FloorRockChance)
                    {
                        GameObject rock = Instantiate<GameObject>(tileFabs.Rocks, tiles[x, y].transform, false);
                        rock.transform.localRotation = Quaternion.AngleAxis(Random.Range(0, 4) * 90.0f, Vector3.up);
                    }
                }
                tiles[x, y].isStatic = true;
            }
        }
        GetComponent<MeshRenderer>().enabled = false;
    }

    bool IsEdge(int x, int z)
    {
        return x == 0 || x == Mathf.FloorToInt(Size.x - 1) || z == 0 || z == Mathf.FloorToInt(Size.z - 1);
    }
    bool IsCorner(int x, int z)
    {
        return (x == 0 || x == Mathf.FloorToInt(Size.x - 1)) && (z == 0 || z == Mathf.FloorToInt(Size.z - 1));
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="x"></param>
    /// <param name="z"></param>
    /// <returns>0, 1, 2, 3 = Up, Right, Down, Left</returns>
    int FindFacing(int x, int z)
    {
        if (z == Mathf.FloorToInt(Size.z - 1))
            return 2;
        if (z == 0)
            return 0;

        if (x == Mathf.FloorToInt(Size.x - 1))
            return 3;
        if (x == 0)
            return 1;
        return 0;
    }

    public static bool QuickOverlap(Room a, Room b)
    {
        return (Vector3.SqrMagnitude(b.Position - a.Position) < Mathf.Pow(a.Radius + b.Radius, 2));
    }
}