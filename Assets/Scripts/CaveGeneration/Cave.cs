using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System;

public class Cave : MonoBehaviour
{
    [HideInInspector]
    public MapTile[,] map;

    [Header("Map Params")]
    public int width = 20;
    public int height = 20;

    [Header("Generation Parms")]
    [Range(0f, 1f)]
    public float fillPercent;
    [Range(0, 5)]
    public int smoothSteps = 5;
    public int smoothMin;
    public Matrix filterMatrix;
    public int wallRegionThresholdSize = 50;
    public int roomRegionThresholdSize = 50;
    public ConnectionType connectionType = ConnectionType.Closest;
    [Range(0f, 1f)]
    public float connectionProbability = 1f;
    public float maxRoomDistance = 20f;
    public int radius = 2;

    public enum MapTile
    {
        Empty,
        Wall
    }

    public enum ConnectionType
    {
        Closest,
        ZigZag
    }

    void Start()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        radius = Mathf.Max(1, radius);
        //GenerateMap();
    }

    int smoothCount = 0;
    bool regions = false;
    bool removedSmallRegions = false;
    bool connectedRooms = false;

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            map = new MapTile[width, height];
            smoothCount = 0;
            regions = false;
            removedSmallRegions = false;
            connectedRooms = false;
            FillMap();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            if (smoothCount < smoothSteps)
            {
                SmoothMap();
                smoothCount++;
            }
            else if (!regions)
            {
                drawRegions = true;
                GetAllRegions();
                regions = true;
            }
            else if (!removedSmallRegions)
            {
                drawRegions = false;
                RemoveSmallRegions(wallRegions, wallRegionThresholdSize, MapTile.Empty);
                RemoveSmallRegions(roomRegions, roomRegionThresholdSize, MapTile.Wall);
                removedSmallRegions = true;
            }
            else if (!connectedRooms)
            {
                Connect();
                connectedRooms = true;
            }
        }

    }

    void GenerateMap()
    {
        map = new MapTile[width, height];
        FillMap();
        for (int i = 0; i < smoothSteps; i++)
            SmoothMap();
        GetAllRegions();
        RemoveSmallRegions(wallRegions, wallRegionThresholdSize, MapTile.Empty);
        RemoveSmallRegions(roomRegions, roomRegionThresholdSize, MapTile.Wall);
    }

    void FillMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    map[x, y] = MapTile.Wall;
                else
                    map[x, y] = Random.value < fillPercent ? MapTile.Wall : MapTile.Empty;
            }
        }
    }


    void SmoothMap()
    {
        int convolution;
        int bounds = (filterMatrix.size - 1) / 2;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                convolution = 0;
                for (int i = x - bounds; i <= x + bounds; i++)
                {
                    for (int j = y - bounds; j <= y + bounds; j++)
                    {
                        if (i < 0 || j < 0 || i >= width || j >= height)
                            convolution++;
                        else
                            convolution += ((map[i, j] == MapTile.Empty) ? 0 : 1) * filterMatrix.At(bounds + x - i, bounds + y - j);
                    }
                }

                if (convolution > smoothMin)
                    map[x, y] = MapTile.Wall;
                else if (convolution < smoothMin)
                    map[x, y] = MapTile.Empty;
            }
        }
    }

    struct Coord
    {
        public int tileX, tileY;

        public Coord(int _tileX, int _tileY)
        {
            tileX = _tileX;
            tileY = _tileY;
        }

        public int SqrDistance(Coord other)
        {
            int x = other.tileX - this.tileX;
            int y = other.tileY - this.tileY;
            return x * x + y * y;
        }

        public int Dot(Coord other)
        {
            return this.tileX * other.tileX + this.tileY * other.tileY;
        }
    }

    class Region
    {
        public List<Coord> tiles = null;
        public List<Coord> edgeTiles = null;
        public Color color;
        public bool connected = false;
        public bool connectedToMain = false;
        private Vector2 _center;

        public Vector2 center
        {
            get
            {
                return _center;
            }
        }

        public void Begin()
        {
            if (tiles == null)
                tiles = new List<Coord>();
            else
                tiles.Clear();
            if (edgeTiles == null)
                edgeTiles = new List<Coord>();
            else
                edgeTiles.Clear();

            _center = Vector2.zero;
            color = Random.ColorHSV(0f, 1f);
        }

        public void AddTile(Coord coord, bool isEdge = false)
        {
            tiles.Add(coord);
            if (isEdge)
                edgeTiles.Add(coord);
            _center.x += coord.tileX;
            _center.y += coord.tileY;
        }

        public void End()
        {
            _center = _center / tiles.Count;
        }

        public void Connect(Region other)
        {
            this.connected = other.connected = true;
            this.connectedToMain = this.connectedToMain | other.connectedToMain;
            other.connectedToMain = this.connectedToMain | other.connectedToMain;
        }
    }

    Region GetRegionAt(int startX, int startY, ref int[,] mapFlags, MapTile? checkEdgeTile = null)
    {
        Region region = new Region();
        MapTile type = map[startX, startY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY));
        mapFlags[startX, startY] = 1;

        region.Begin();
        while (queue.Count > 0)
        {
            bool isEdge = false;
            Coord tile = queue.Dequeue();

            if (tile.tileX - 1 >= 0 && mapFlags[tile.tileX - 1, tile.tileY] != 1)
            {
                if (map[tile.tileX - 1, tile.tileY] == type)
                {
                    queue.Enqueue(new Coord(tile.tileX - 1, tile.tileY));
                    mapFlags[tile.tileX - 1, tile.tileY] = 1;
                }
                else if (checkEdgeTile != null && map[tile.tileX - 1, tile.tileY] == checkEdgeTile)
                    isEdge = true;
            }
            if (tile.tileY - 1 >= 0 && mapFlags[tile.tileX, tile.tileY - 1] != 1)
            {
                if (map[tile.tileX, tile.tileY - 1] == type)
                {
                    queue.Enqueue(new Coord(tile.tileX, tile.tileY - 1));
                    mapFlags[tile.tileX, tile.tileY - 1] = 1;
                }
                else if (checkEdgeTile != null && map[tile.tileX, tile.tileY - 1] == checkEdgeTile)
                    isEdge = true;
            }
            if (tile.tileX + 1 < width && mapFlags[tile.tileX + 1, tile.tileY] != 1)
            {
                if (map[tile.tileX + 1, tile.tileY] == type)
                {
                    queue.Enqueue(new Coord(tile.tileX + 1, tile.tileY));
                    mapFlags[tile.tileX + 1, tile.tileY] = 1;
                }
                else if (checkEdgeTile != null && map[tile.tileX + 1, tile.tileY] == checkEdgeTile)
                    isEdge = true;
            }
            if (tile.tileY + 1 < height && mapFlags[tile.tileX, tile.tileY + 1] != 1)
            {
                if (map[tile.tileX, tile.tileY + 1] == type)
                {
                    queue.Enqueue(new Coord(tile.tileX, tile.tileY + 1));
                    mapFlags[tile.tileX, tile.tileY + 1] = 1;
                }
                else if (checkEdgeTile != null && map[tile.tileX, tile.tileY + 1] == checkEdgeTile)
                    isEdge = true;
            }

            region.AddTile(tile, isEdge);
        }

        region.End();

        return region;
    }

    List<Region> GetRegions(MapTile type, MapTile? checkEdgeTile = null)
    {
        List<Region> regions = new List<Region>();
        int[,] mapFlags = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (map[x, y] == type && mapFlags[x, y] == 0)
                    regions.Add(GetRegionAt(x, y, ref mapFlags, checkEdgeTile));
            }
        }

        return regions;
    }

    private List<Region> wallRegions;
    private List<Region> roomRegions;

    void GetAllRegions()
    {
        wallRegions = GetRegions(MapTile.Wall);
        roomRegions = GetRegions(MapTile.Empty, MapTile.Wall);
    }

    void RemoveSmallRegions(List<Region> regions, int thresholdSize, MapTile fill)
    {
        int i = 0;
        while (i < regions.Count)
        {
            Region region = regions[i];
            if (region.tiles.Count < thresholdSize)
            {
                FillRegion(region.tiles, fill);
                regions.RemoveAt(i);
            }
            else
                i++;
        }
    }

    void FillRegion(List<Coord> region, MapTile type)
    {
        foreach (Coord coord in region)
            map[coord.tileX, coord.tileY] = type;
    }

    List<Region> ConnectClosest()
    {
        List<Region> order = new List<Region>();

        Region roomA, roomB;

        while (roomRegions.Count > 0)
        {
            roomA = roomRegions[0];
            roomB = roomRegions[0];
            roomA.connectedToMain = true;
            bool quit = false;
            while (!quit)
            {
                order.Add(roomA);
                roomRegions.Remove(roomA);

                quit = true;
                float minSqrDistance = float.PositiveInfinity;
                for (int j = 0; j < roomRegions.Count; j++)
                {
                    Region room = roomRegions[j];
                    if (roomA == room)
                        continue;
                    float distance = Vector2.SqrMagnitude(roomA.center - room.center);
                    if (distance < minSqrDistance)//&& !room.connectedToMain)
                    {
                        minSqrDistance = distance;
                        roomB = room;
                        quit = false;
                    }
                }
                if (!quit)
                {
                    roomA.Connect(roomB);
                    roomA = roomB;
                }
            }
        }
        roomRegions = order;
        return order;
    }

    List<Region> ConnectZigZag()
    {
        List<Region> order = roomRegions;

        order.Sort((a, b) => Mathf.CeilToInt(a.center.y - b.center.y));

        return order;
    }

    void Connect()
    {
        List<Region> connectionOrder;
        switch (connectionType)
        {
            case ConnectionType.Closest:
                connectionOrder = ConnectClosest();
                break;
            case ConnectionType.ZigZag:
                connectionOrder = ConnectZigZag();
                break;
            default:
                connectionOrder = roomRegions;
                break;
        }


        Region roomA;
        Region roomB;
        Coord roomAedge;
        Coord roomBedge;

        for (int i = 0; i < connectionOrder.Count - 1; i++)
        {
            if (Random.value > connectionProbability)
                continue;

            roomA = connectionOrder[i];
            roomB = connectionOrder[i + 1];

            if (roomA.edgeTiles.Count == 0 || roomB.edgeTiles.Count == 0)
                continue;

            roomAedge = roomA.edgeTiles[0];
            roomBedge = roomB.edgeTiles[0];
            float minSqrDistance = roomAedge.SqrDistance(roomBedge);
            float minAngle = Mathf.Atan2(roomAedge.tileY - roomBedge.tileY, roomAedge.tileX - roomBedge.tileX);

            foreach (Coord coordA in roomA.edgeTiles)
            {
                foreach (Coord coordB in roomB.edgeTiles)
                {
                    int sqrDistance = coordA.SqrDistance(coordB);
                    float angle = Mathf.Atan2(Mathf.Abs(coordA.tileY - coordB.tileY), coordA.tileX - coordB.tileX);
                    if (sqrDistance < minSqrDistance)
                    {
                        if (connectionType == ConnectionType.ZigZag)
                        {
                            if (angle < minAngle)
                            {
                                minSqrDistance = sqrDistance;
                                minAngle = angle;
                                roomAedge = coordA;
                                roomBedge = coordB;
                            }
                        }
                        else
                        {
                            minSqrDistance = sqrDistance;
                            roomAedge = coordA;
                            roomBedge = coordB;
                        }

                            
                    }
                }
            }
            if (roomAedge.SqrDistance(roomBedge) < maxRoomDistance * maxRoomDistance)
            {
                roomA.Connect(roomB);
                CreatePassage(roomAedge, roomBedge, radius, MapTile.Empty);
            }
        }
    }

    void CreatePassage(Coord tileA, Coord tileB, int radius, MapTile tile)
    {
        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord c in line)
            DrawCircle(c, radius, tile);
    }

    void DrawCircle(Coord c, int r, MapTile tile)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                if (x * x + y * y <= r * r)
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                    {
                        map[drawX, drawY] = tile;
                    }
                }
            }
        }
    }

    List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y));

            if (inverted)
            {
                y += step;
            }
            else {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    bool drawRegions = false;

    void OnDrawGizmos()
    {
        if (map != null)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Gizmos.color = map[x, y] == MapTile.Empty ? Color.white : Color.black;
                    Vector3 position = new Vector3(-width / 2f + x * 1f, -height / 2f + y * 1f, 0f);
                    Gizmos.DrawCube(position, Vector3.one);
                }
            }

            if (!drawRegions)
                return;

            if (wallRegions != null)
                foreach (Region region in wallRegions)
                {
                    Gizmos.color = region.color;
                    foreach (Coord coord in region.tiles)
                    {
                        Vector3 position = new Vector3(-width / 2f + coord.tileX * 1f, -height / 2f + coord.tileY * 1f, -1f);
                        Gizmos.DrawCube(position, Vector3.one);
                    }
                }

            if (roomRegions != null)
                foreach (Region region in roomRegions)
                {
                    Gizmos.color = region.color;
                    foreach (Coord coord in region.tiles)
                    {
                        Vector3 position = new Vector3(-width / 2f + coord.tileX * 1f, -height / 2f + coord.tileY * 1f, -1f);
                        Gizmos.DrawCube(position, Vector3.one);
                    }
                }
        }
    }

}
