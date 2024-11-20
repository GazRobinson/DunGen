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
    float buffer = 1.0f;

    public bool StartRoom = false;
    public bool BossRoom = false;

    public HashSet<Room> connections = new HashSet<Room>();
    private GameObject[,] tiles;

    public void Init(float min, float max, float buffer)
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
        mesh.triangles = mesh.triangles.Reverse().ToArray();
        mesh.RecalculateNormals();

        this.buffer = buffer;
        float a = Random.Range(min, max);
        float w = Mathf.Round(Mathf.Max(2, (a / (Random.Range(2, 6)))));
        float d = Mathf.Round(a / w);
        if(Random.value < 0.5f)
        {
            float t = d;
            d = w;
            w = t;
        }

        Position = Vector3.zero;
        Size = new Vector3(w, min, d);
        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position;

        tiles = new GameObject[Mathf.RoundToInt(w), Mathf.RoundToInt(d)];
    }

    public void MakeStart()
    {
        Size = new Vector3(3.0f, 3.0f, 3.0f);
        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position;
        gameObject.name = "Start";
        GetComponent<Renderer>().material.SetColor("_Color", Color.green);
    }

    public void MakeBoss()
    {
        Size = new Vector3(5.0f, 5.0f, 5.0f);
        bounds = new Bounds(Position, Size * buffer);
        trueBounds = new Bounds(Position, Size);
        transform.localScale = Size;
        transform.position = Position; 
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

    void BuildRoom(ref RoomTiles tileFabs)
    {
        for(int y = 0; y < tiles.GetLength(1); y++)
        {
            for (int x = 0; x < tiles.GetLength(0); x++)
            {

            }
        }
    }
}