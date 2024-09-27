/**
 * BotsX for Soldat 2, based on SnailBots by RetiredSnail
 * - Make bots rek people?
 *
 * Soldat 2 Community License:
 *
 * You are allowed to redistribute or use the software in source or binary form
 * with or without modifications provided that you share your modifications
 * with the Soldat 2 community in source form. 
 **/

using UnityEngine;
using Teal;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Diagnostics;
//using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Random = System.Random;

// ReSharper disable once CheckNamespace
public class SnailBotsX: MonoBehaviour
{
    private class PriorityQueue<T> {
        private List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();

        public int Count {
            get { return elements.Count; }
        }

        public void Enqueue(T item, float priority) {
            elements.Add(new KeyValuePair<T, float>(item, priority));
        }

        public T Dequeue() {
            int bestIndex = 0;

            for (int i = 0; i < elements.Count; i++) {
                if (elements[i].Value < elements[bestIndex].Value) {
                    bestIndex = i;
                }
            }

            T bestItem = elements[bestIndex].Key;
            elements.RemoveAt(bestIndex);
            return bestItem;
        }

        public bool Contains(T item) {
            return elements.Exists(x => x.Key.Equals(item));
        }
    }
    public class NavX {
        static readonly float sqrt2 = (float)Math.Sqrt(2);
        [Flags]
        public enum Dirs {
            Zero = 0,
            Up = 1 << 0,
            Right = 1 << 1,
            Down = 1 << 2,
            Left = 1 << 3,
            UpRight = 1 << 4,
            DownRight = 1 << 5,
            DownLeft = 1 << 6,
            UpLeft = 1 << 7
        }

        public static Vector2 DirToVec(Dirs dir)
        {
            switch (dir)
            {
                case Dirs.Up:
                    return Vector2.up;
                case Dirs.Right:
                    return Vector2.right;
                case Dirs.Down:
                    return Vector2.down;
                case Dirs.Left:
                    return Vector2.left;
                case Dirs.UpRight:
                    return Vector2.up + Vector2.right;
                case Dirs.DownRight:
                    return Vector2.down + Vector2.right;
                case Dirs.DownLeft:
                    return Vector2.down + Vector2.left;
                case Dirs.UpLeft:
                    return Vector2.up + Vector2.left;
                case Dirs.Zero:
                default:
                    return Vector2.zero;
            }
        }

        public static (int, int) DirToXY(Dirs dir)
        {
            switch (dir)
            {
                case Dirs.Up:
                    return (0, 1);
                case Dirs.Right:
                    return (1, 0);
                case Dirs.Down:
                    return (0, -1);
                case Dirs.Left:
                    return (-1, 0);
                case Dirs.UpRight:
                    return (1, 1);
                case Dirs.DownRight:
                    return (1, -1);
                case Dirs.DownLeft:
                    return (-1, -1);
                case Dirs.UpLeft:
                    return (-1, 1);
                case Dirs.Zero:
                default:
                    return (0, 0);
            }
        }
        
        public static float DirToDist(Dirs dir)
        {
            switch (dir)
            {
                case Dirs.Up:
                case Dirs.Right:
                case Dirs.Down:
                case Dirs.Left:
                    return 1f;
                case Dirs.UpRight:
                case Dirs.DownRight:
                case Dirs.DownLeft:
                case Dirs.UpLeft:
                    return sqrt2;
                case Dirs.Zero:
                default:
                    return float.PositiveInfinity;
            }
        }
        
        public static Dirs XYToDir(int x, int y)
        {
            if (x == 0)
            {
                if (y > 0)
                    return Dirs.Up;
                return Dirs.Down;
            }
            if (x > 0)
            {
                if (y > 0)
                    return Dirs.UpRight;
                else if (y == 0)
                    return Dirs.Right;
                return Dirs.DownRight;
            }
            if (y > 0)
                return Dirs.UpLeft;
            else if (y == 0)
                return Dirs.Left;
            return Dirs.DownLeft;
        }
        public class NavPoint {
            public int xi;
            public int yi;
            public Vector2 pos;
            public Dirs dirs;
            public List<(NavPoint, float)> neighbors;

            public NavPoint(int x, int y, Vector2 newPos) {
                xi = x;
                yi = y;
                pos = newPos;
                dirs = Dirs.Zero;
            }

            public override string ToString()
            {
	            return "P(" + xi + "," + yi + ")";
            } 
        }
        public static bool Ready = false;
        public static int PointsX = -1;
        public static int PointsY = -1;
        public static float PointTargetScale = 2.0f;

        private static float Heuristic(NavPoint a, NavPoint b)
        {
            return Vector2.Distance(a.pos, b.pos);
        }
        public static NavPoint[,] Grid = null;
        public static List<NavPoint> AllPoints = null;

        public static LayerMask PathMask;
        
        
        static List<NavPoint> GetNeigbors(NavPoint start) {
            var res = new List<NavPoint>();
            int x = start.xi;
            int y = start.yi;
            foreach (Dirs value in Enum.GetValues(typeof(Dirs)))
            {
                if (start.dirs.HasFlag(value))
                {
                    var (xs, ys) = DirToXY(value);
                    NavPoint neighbor = GetPoint(x + xs, y + ys);
                    if (neighbor != null)
                    {
                        res.Add(neighbor);
                    }
                }
            }

            return res;
        }
        
        static List<(NavPoint, float)> GetNeigborsDist(NavPoint start) {
            var res = new List<(NavPoint, float)>();
            int x = start.xi;
            int y = start.yi;
            foreach (Dirs value in Enum.GetValues(typeof(Dirs)))
            {
                if (start.dirs.HasFlag(value))
                {
                    var (xs, ys) = DirToXY(value);
                    NavPoint neighbor = GetPoint(x + xs, y + ys);
                    if (neighbor != null)
                    {
                        res.Add((neighbor, DirToDist(value)));
                    }
                }
            }

            return res;
        }

        public static Vector2 PointsCoordPos(int x, int y)
        {
            Map map = Map.Get;
            Bounds bounds = map.bounds;
            return (Vector2)bounds.min + new Vector2(x * PointTargetScale, y * PointTargetScale);
        }
        
        public static NavPoint GetPoint(int x, int y)
        {
            if (x < 0 || y < 0 || x >= PointsX || y >= PointsY)
                return null;
            return Grid[x, y];
        }

        public static NavPoint GetNearestPoint(Vector2 vec)
        {
            NavPoint result = null;
            float bestDistance = float.PositiveInfinity;
            int px = Math.Min((int)Math.Floor(vec.x / PointTargetScale), PointsX-1);
            int py = Math.Min((int)Math.Floor(vec.y / PointTargetScale), PointsY-1);
            for (int xs = 0; xs <= 1; xs++)
            {
                for (int ys = 0; ys <= 1; ys++)
                {
                    NavPoint cur = GetPoint(px + xs, py + ys);
                    if (cur != null)
                    {
                        Vector2 delta = cur.pos - vec;
                        float curDistance = delta.sqrMagnitude;
                        if (curDistance > bestDistance)
                            continue;
                        
                        result = cur;
                        bestDistance = curDistance;
                    }
                }
            }

            if (result != null) return result;
            
            // TODO: replace with smarter expansion algorithm
            for (int x = 0; x < Grid.GetLength(0); x++)
            {
                for (int y = 0; y < Grid.GetLength(1); y++)
                {
                    NavPoint cur = GetPoint(x, y);
                    if (cur != null)
                    {
                        Vector2 delta = cur.pos - vec;
                        float curDistance = delta.sqrMagnitude;
                        if (curDistance > bestDistance)
                            continue;
                    
                        result = cur;
                        bestDistance = curDistance;
                    }
                }
            }
            return result;
        }

        public static NavPoint GetNearestVisiblePoint(Vector2 vec)
        {
            NavPoint result = null;
            float bestDistance = float.PositiveInfinity;
            int px = Math.Min((int)Math.Floor(vec.x / PointTargetScale), PointsX-1);
            int py = Math.Min((int)Math.Floor(vec.y / PointTargetScale), PointsY-1);
            var rayResults = new RaycastHit2D[8];
            for (int xs = 0; xs <= 1; xs++)
            {
                for (int ys = 0; ys <= 1; ys++)
                {
                    NavPoint cur = GetPoint(px + xs, py + ys);
                    if (cur != null)
                    {
                        Vector2 delta = cur.pos - vec;
                        float curDistance = delta.sqrMagnitude;
                        if (curDistance > bestDistance)
                            continue;
                        
                        int hits = Physics2D.RaycastNonAlloc(cur.pos, -delta, rayResults, delta.magnitude, PathMask);
                        if (hits > 0)
                            continue;
                        
                        result = cur;
                        bestDistance = curDistance;
                    }
                }
            }

            // if this fails, we're screwed anyway, just get the nearest one. i'm not tracing allat
            return result ?? GetNearestPoint(vec);
        }

        public static bool CheckHits(int inHits, RaycastHit2D[] rayResults)
        {
            for (int i = 0; i < inHits; i++)
            {
                var collision = rayResults[i];
                ProtoshapeEdit pe = collision.collider.GetComponent<ProtoshapeEdit>();
                if (pe)
                {
                    var shapeType = pe.mapData.type;
                    if (shapeType == ProtoshapeEdit.ColliderType.Solid ||
                        shapeType == ProtoshapeEdit.ColliderType.PlayersCollide ||
                        shapeType == ProtoshapeEdit.ColliderType.Deadly)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public static void InitializeGlobal() {
            Map map = Map.Get;
            Bounds bounds = map.bounds;
            float mapW = bounds.size.x;
            float mapH = bounds.size.y;
            PathMask = LayerMask.GetMask(new string[]{"Gostek","Ground"});
            PointsX = (int)(mapW / PointTargetScale);
            PointsY = (int)(mapH / PointTargetScale);
            
            var colliderResults = new Collider2D[32];
            var rayResults = new RaycastHit2D[32];
            
            Grid = new NavPoint[PointsX, PointsY];
            // Create NavPoints
            for (int x = 0; x < PointsX; x++) {
                for (int y = 0; y < PointsY; y++)
                {
                    Vector2 targetPoint = PointsCoordPos(x, y);
                    int hits = Physics2D.OverlapPointNonAlloc(targetPoint, colliderResults, PathMask);
                    if (hits == 0)
                    {
                        Grid[x, y] = new NavPoint(x, y, targetPoint);
                    }
                    // Maybe TODO: if we're near the edge of a wall, try to nudge the point out of it if possible
                }
            }
            
            // Create NavPoint neighbor links
            for (int x = 0; x < PointsX; x++) {
                for (int y = 0; y < PointsY; y++)
                {
                    NavPoint point = Grid[x, y];
                    if (point == null)
                        continue;
                    // Manually unrolled neighbor traces
                    { // Right
                        NavPoint neigbor = GetPoint(x + 1, y);
                        if (neigbor != null)
                        {
                            Vector2 delta = neigbor.pos - point.pos;
                            int hits = Physics2D.RaycastNonAlloc(point.pos, delta.normalized, rayResults, delta.magnitude);
                            if (CheckHits(hits, rayResults))
                            {
                                point.dirs |= Dirs.Right;
                                neigbor.dirs |= Dirs.Left;
                            }
                        }
                    }
                    { // UpRight
                        NavPoint neigbor = GetPoint(x + 1, y + 1);
                        if (neigbor != null)
                        {
                            Vector2 delta = neigbor.pos - point.pos;
                            int hits = Physics2D.RaycastNonAlloc(point.pos, delta.normalized, rayResults, delta.magnitude);
                            if (CheckHits(hits, rayResults))
                            {
                                point.dirs |= Dirs.UpRight;
                                neigbor.dirs |= Dirs.DownLeft;
                            }
                        }
                    }
                    { // Up
                        NavPoint neigbor = GetPoint(x, y + 1);
                        if (neigbor != null)
                        {
                            Vector2 delta = neigbor.pos - point.pos;
                            int hits = Physics2D.RaycastNonAlloc(point.pos, delta.normalized, rayResults, delta.magnitude);
                            if (CheckHits(hits, rayResults))
                            {
                                point.dirs |= Dirs.Up;
                                neigbor.dirs |= Dirs.Down;
                            }
                        }
                    }
                    { // UpLeft
                        NavPoint neigbor = GetPoint(x - 1, y + 1);
                        if (neigbor != null)
                        {
                            Vector2 delta = neigbor.pos - point.pos;
                            int hits = Physics2D.RaycastNonAlloc(point.pos, delta.normalized, rayResults, delta.magnitude);
                            if (CheckHits(hits, rayResults))
                            {
                                point.dirs |= Dirs.UpLeft;
                                neigbor.dirs |= Dirs.DownRight;
                            }
                        }
                    }
                }
            }
			
            AllPoints = new List<NavPoint>();
            // Remove points with 0 neighbors
            for (int x = 0; x < PointsX; x++)
            {
                for (int y = 0; y < PointsY; y++)
                {
                    NavPoint point = Grid[x, y];
                    if (point != null)
                    {
	                    if (point.dirs == Dirs.Zero)
	                    {
		                    Grid[x, y] = null;
	                    }
	                    else
	                    {
		                    point.neighbors = GetNeigborsDist(point);
		                    AllPoints.Add(point);
	                    }
                    }
                }
            }
            
            // Collect all player spawn points
            List<Vector2> spawnPoints = new List<Vector2>();
            foreach (Respawn res in Respawn._list)
            {
                if (!res)
                    continue;
                if (res.respawnPrefab != "Gostek")
                    continue;
                spawnPoints.Add(res.transform.position);
            }

            // Flood fill from player spawns
            HashSet<NavPoint> reachablePoints = new HashSet<NavPoint>();
            Queue<NavPoint> queue = new Queue<NavPoint>();

            // Initialize the queue with the nearest points to the spawn points
            foreach (Vector2 spawnPoint in spawnPoints)
            {
                NavPoint startPoint = GetNearestPoint(spawnPoint);
                if (startPoint != null)
                {
                    queue.Enqueue(startPoint);
                    reachablePoints.Add(startPoint);
                }
            }

            // Perform the flood fill
            while (queue.Count > 0)
            {
                NavPoint current = queue.Dequeue();
                foreach (var (neighbor, dist) in GetNeigborsDist(current))
                {
                    if (!reachablePoints.Contains(neighbor))
                    {
                        reachablePoints.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Remove all points that are not reachable
            for (int x = 0; x < PointsX; x++)
            {
                for (int y = 0; y < PointsY; y++)
                {
                    NavPoint point = Grid[x, y];
                    if (point != null && !reachablePoints.Contains(point))
                    {
                        Grid[x, y] = null;
                    }
                }
            }

            // Rebuild the AllPoints list
            AllPoints.Clear();
            for (int x = 0; x < PointsX; x++)
            {
                for (int y = 0; y < PointsY; y++)
                {
                    NavPoint point = Grid[x, y];
                    if (point != null)
                    {
                        point.neighbors = GetNeigborsDist(point);
                        AllPoints.Add(point);
                    }
                }
            }

            Ready = true;
        }

        public static void ResetGlobal()
        { 
            Ready = false;
            PointsX = -1;
            PointsY = -1; 
            //PointTargetScale = 10f;
            Grid = null;
            AllPoints = null;
        }

        public static NavPoint GetRandomPoint()
        {
	        var rand = new Random();
	        return AllPoints[rand.Next(AllPoints.Count)];
        }

        public static List<NavPoint> AStar(NavPoint start, NavPoint goal)
        {
            var cameFrom = new Dictionary<NavPoint, NavPoint>();
            var gScore = new Dictionary<NavPoint, float> { [start] = 0 };
            var fScore = new Dictionary<NavPoint, float> { [start] = Heuristic(start, goal) };
            PriorityQueue<NavPoint> openSet = new PriorityQueue<NavPoint>();
            openSet.Enqueue(start, fScore[start]);

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }
                foreach (var (neighbor, dist) in current.neighbors)
                {
                    var tentativeGScore = gScore[current] + dist;
                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Enqueue(neighbor, fScore[neighbor]);
                        }
                    }
                }
            }

            return null; // No path found
        }

        private static List<NavPoint> ReconstructPath(Dictionary<NavPoint, NavPoint> cameFrom, NavPoint current)
        {
            var totalPath = new List<NavPoint> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                totalPath.Insert(0, current);
            }
            return totalPath;
        }
    }
	
	public class SnailBot {
		const int IDX_RIGHT = 0;
		const int IDX_LEFT = 1;
		const int IDX_DOWN = 2;
		const int IDX_UP = 3;
		const int IDX_DOWN_RIGHT = 4;
		const int IDX_DOWN_LEFT = 5;
		const int IDX_UP_RIGHT = 6;
		const int IDX_UP_LEFT = 7;
		const int IDX_UP_LEFT2 = 8;
		const int IDX_UP_RIGHT2 = 9;
		const int IDX_DOWN_LEFT2 = 10;
		const int IDX_DOWN_RIGHT2 = 11;
		
		
		const int IDX_GOSTEK_JET = 12;
		const int IDX_GOSTEK_JUMP = 13;
		const int IDX_GOSTEK_CAN_ROLL = 14;
		const int IDX_GOSTEK_JUMP_TIME = 15;
		const int IDX_GOSTEK_GROUNDED = 16;
		const int IDX_GOSTEK_SUPERMAN = 17;
		const int IDX_GOSTEK_VELX = 18;
		const int IDX_GOSTEK_VELY = 19;
		
		const int IDX_GOSTEK_SLOPE_LEFT = 20;
		const int IDX_GOSTEK_SLOPE_RIGHT = 21;
		
		
		const int IDX_OUT_JUMP = 0;
		const int IDX_OUT_SMAN = 1;
		const int IDX_OUT_JETS = 2;
		const int IDX_OUT_HAXS = 3;
		const int IDX_OUT_AIMX = 4;
		const int IDX_OUT_AIMY = 5;
		const int IDX_OUT_FIRE = 6;
		
		public float[] values = new float[22];
		public float[] outputs = new float[7];
		
		
		// Basic vectors!
		private Vector2 V_RIGHT = new Vector2(1, 0);
		private Vector2 V_LEFT = new Vector2(-1, 0);
		private Vector2 V_UP = new Vector2(0, 1);
		private Vector2 V_DOWN = new Vector2(0, -1);
		
		// More vectors!
		private Vector2 V_DOWN_RIGHT = new Vector2(1, -1);
		private Vector2 V_UP_RIGHT = new Vector2(1, 1);
		private Vector2 V_DOWN_LEFT = new Vector2(-1, -1);
		private Vector2 V_UP_LEFT = new Vector2(-1, 1);
		
		// And more
		private Vector2 V_DOWN_RIGHT2 = new Vector2(2, -1);
		private Vector2 V_DOWN_LEFT2 = new Vector2(-2, -1);
		private Vector2 V_UP_LEFT2 = new Vector2(-2, 1);
		private Vector2 V_UP_RIGHT2 = new Vector2(2, 1);
		
		public string[] valueNames = new string[22];
		public string[] outputNames = new string[7];
		
		private RaycastHit2D[] raycastResults = new RaycastHit2D[2];
		
		private Vector2[] raycastVectors = new Vector2[12];
		
		private bool wasJumping = false;
		private Stopwatch sw = new Stopwatch();
		
		public Player plrToControl = null;
		private Player plrToKill = null;
		private Player lastPlrToKill = null;
		
		public Flag ourFlag = null;
		public Flag theirFlag = null;
		
		const int SMODE_DEATHMATCH = 0;
		const int SMODE_GET_THEIR_FLAG = 1;
		
		private int currentMode = -1;
		
		public Vector3 navFinalTarget = Vector3.zero;
		public NavX.NavPoint navTargetWaypoint = null;
		public NavX.NavPoint navCurrentWaypoint = null;
		public NavX.NavPoint navNextWaypoint = null;
		public List<NavX.NavPoint> navCurrentPath = null;
		
		public void SetNavTarget(Vector2 pos, Vector2 newTarget) {
			if (newTarget.Equals(navFinalTarget))
				return;
			navFinalTarget = newTarget;
			NavX.NavPoint newTargetWaypoint = NavX.GetNearestVisiblePoint(newTarget);
			if (newTargetWaypoint == navTargetWaypoint)
				return;
			navTargetWaypoint = newTargetWaypoint;
			navCurrentWaypoint = NavX.GetNearestVisiblePoint(pos);
			navCurrentPath = NavX.AStar(navCurrentWaypoint, navTargetWaypoint);
			if (navCurrentPath != null && navCurrentPath.Count > 1) {
				navCurrentPath.RemoveAt(0); // start waypoint
				navNextWaypoint = navCurrentPath[0];
				navCurrentPath.RemoveAt(0); // next waypoint
			} else {
				navNextWaypoint = null;
			}
		}
		
		public void ResetNav() {
			//navFinalTarget = Vector3.zero;
			navTargetWaypoint = null;
			navCurrentWaypoint = null;
			navNextWaypoint = null;
			navCurrentPath = null;
			navFinalTarget = NULLVEC;
		}
		
		public Vector3 GetWalkTarget(Vector2 pos) {
			if (navNextWaypoint == null || navCurrentPath == null || navCurrentPath.Count == 0) // go go go!
				return navFinalTarget;
			Vector2 playerPos = pos;
			Vector2 currentWpPos = navCurrentWaypoint.pos;
			Vector2 nextWpPos = navNextWaypoint.pos;
			float currentWpDistSqr = ((Vector2)(currentWpPos - playerPos)).sqrMagnitude;
			float nextWpDistSqr = ((Vector2)(nextWpPos - playerPos)).sqrMagnitude;
			if (currentWpDistSqr > nextWpDistSqr) {
				navCurrentWaypoint = navNextWaypoint;
				navNextWaypoint = navCurrentPath[0];
				navCurrentPath.RemoveAt(0);
				return GetWalkTarget(pos);
			}
			
			// Funnel
			var currentTarget = nextWpPos;
			var rayResults = new RaycastHit2D[32];

			{
				var delta = (Vector2)navFinalTarget - playerPos;
				int hits = Physics2D.RaycastNonAlloc(playerPos, delta.normalized, rayResults, delta.magnitude);
				if (NavX.CheckHits(hits, rayResults))
				{
					return navFinalTarget;
				}
			}
			
			for (int i = 0; i < Math.Min(navCurrentPath.Count, 16); i++)
			{
				NavX.NavPoint targ = navCurrentPath[i];
				if (targ == null)
					return currentTarget;

				var delta = targ.pos - playerPos;
				int hits = Physics2D.RaycastNonAlloc(playerPos, delta.normalized, rayResults, delta.magnitude);
				if (NavX.CheckHits(hits, rayResults))
				{
					currentTarget = targ.pos;
				}
			}

			return currentTarget;
		}

		//public Vector2 mouseTarget = Vector2.zero;
		public Vector2 mouseCurrent = Vector2.zero;
		public float mouseSpeed = 0.75f;
		public float mouseDiv = 8f;

		public void MoveMouse(Vector2 target)
		{
			var delta = target - mouseCurrent;
			var deltaLen = delta.magnitude;
			if (deltaLen < mouseSpeed)
			{
				mouseCurrent = target;
				return;
			}
			var mouseMoveLen = Math.Max(Math.Min(mouseSpeed, deltaLen), deltaLen / mouseDiv);
			var mouseMovement = delta.normalized * mouseMoveLen;
			mouseCurrent += mouseMovement;
		}
		
		private bool isDeathmatch = false;
		private NavX.NavPoint wpDeathmatch = null;
		private Stopwatch wpDmWatch = new Stopwatch();
		
		private static int snailBotIdx = 0;
		
		public static void Reset() {
			snailBotIdx = 0;
		}
		
		private static void GiveCoolAppearance(Player player) {
			player.props["skin color"] = new Color(1f,1f,1f).ToHex();
			player.props["hair color"] = new Color(1f,0f,1f).ToHex();
			player.controlled.GetComponent<GostekAppearance>().ForceRefresh();
		}
		
		private void FindFlags() {
			Flag ourFlag_ = null;
			Flag theirFlag_ = null;
		
			foreach(Flag flag in Flag._flags) {
				if(flag.team.Number == plrToControl.GetTeam())
					ourFlag_ = flag;
				else
					theirFlag_ = flag;
			}
			
			ourFlag = ourFlag_;
			theirFlag = theirFlag_;
		}
	
		public SnailBot(Player player, Flag ourFlag, Flag theirFlag) {
			
			this.plrToControl = player;
			
			player.ping = -1;
			//snailBotIdx++;
			
			this.ourFlag = ourFlag;
			this.theirFlag = theirFlag;
			
			if(Map.GetLevelName().StartsWith("dm_"))
				isDeathmatch = true;
			
			outputNames[IDX_OUT_HAXS] = "HAXS";
			outputNames[IDX_OUT_JUMP] = "JUMP";
			outputNames[IDX_OUT_JETS] = "JETS";
			outputNames[IDX_OUT_SMAN] = "SMAN";
			outputNames[IDX_OUT_AIMX] = "AIMX";
			outputNames[IDX_OUT_AIMY] = "AIMY";
			outputNames[IDX_OUT_FIRE] = "FIRE";
			
			raycastVectors[IDX_RIGHT] = V_RIGHT;
			raycastVectors[IDX_LEFT] = V_LEFT;
			raycastVectors[IDX_UP] = V_UP;
			raycastVectors[IDX_DOWN] = V_DOWN;
			raycastVectors[IDX_DOWN_LEFT] = V_DOWN_LEFT;
			raycastVectors[IDX_DOWN_RIGHT] = V_DOWN_RIGHT;
			raycastVectors[IDX_UP_LEFT] = V_UP_LEFT;
			raycastVectors[IDX_UP_RIGHT] = V_UP_RIGHT;
			raycastVectors[IDX_UP_LEFT2] = V_UP_LEFT2;
			raycastVectors[IDX_UP_RIGHT2] = V_UP_RIGHT2;
			raycastVectors[IDX_DOWN_LEFT2] = V_DOWN_LEFT2;
			raycastVectors[IDX_DOWN_RIGHT2] = V_DOWN_RIGHT2;
			
			for(int i = 0; i < raycastVectors.Length; i++)
				raycastVectors[i] = raycastVectors[i].normalized;
			
			valueNames[IDX_RIGHT] = "RGHT";
			valueNames[IDX_LEFT] =  "LEFT";
			valueNames[IDX_UP] =    "UP__";
			valueNames[IDX_DOWN] =  "DOWN";
			valueNames[IDX_DOWN_RIGHT] = "DWRG";
			valueNames[IDX_UP_RIGHT] = "UPRG";
			valueNames[IDX_UP_LEFT] = "UPLF";
			valueNames[IDX_DOWN_LEFT] = "DWLF";
			
			valueNames[IDX_GOSTEK_JET] = "JET_";
			valueNames[IDX_GOSTEK_JUMP] = "JUMP";
			valueNames[IDX_GOSTEK_CAN_ROLL] = "CANR";
			valueNames[IDX_GOSTEK_JUMP_TIME] = "JMPT";
			valueNames[IDX_GOSTEK_GROUNDED] = "GRND";
			valueNames[IDX_GOSTEK_SUPERMAN] = "SMAN";
			
			valueNames[IDX_DOWN_LEFT2] = "DLF2";
			valueNames[IDX_DOWN_RIGHT2] = "DRG2";
			valueNames[IDX_UP_LEFT2] = "ULF2";
			valueNames[IDX_UP_RIGHT2] = "URG2";
			
			valueNames[IDX_GOSTEK_VELX] = "VEL_X";
			valueNames[IDX_GOSTEK_VELY] = "VEL_Y";
			
			valueNames[IDX_GOSTEK_SLOPE_LEFT] = "SLPL";
			valueNames[IDX_GOSTEK_SLOPE_RIGHT] = "SLPR";
		}
		
		private float RayCastDistance(Vector2 origin, Vector2 dir, float distance) {
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, distance, Map.Get.groundLayer);
			if(hits > 0)
				return raycastResults[0].distance;
			return distance+1;
		}
		
		private bool RayCastHit(Vector2 origin, Vector2 target) {
			Vector2 dir = target - origin;
			float distance = dir.magnitude;
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, distance, Map.Get.groundLayer);
			return hits > 0;
		}
		
		private Vector2? RayCastPoint(Vector2 origin, Vector2 dir) {
			int hits = Physics2D.RaycastNonAlloc(origin, dir, raycastResults, 500, Map.Get.groundLayer);
			if(hits > 0)
				return (Vector2)raycastResults[0].point;
			return null;
		}


		private HumanBrain humanBrain;
		private GostekBot gostekBot;
		private bool brainInited;
		public void SetupDecision()
		{
			try
			{
				if (brainInited && humanBrain && gostekBot && gostekBot.brain?.globals != null && gostekBot.brain.list.Count > 0)
					return;
				
				var player = plrToControl;
				if (player.IsDead() || !player.controlled || !player.controlled.gameObject)
					return;
			
				if (!humanBrain)
				{
					brainInited = false;
					humanBrain = player.controlled.gameObject.GetComponent<HumanBrain>();
					if (!humanBrain)
						return;
				}

				if (!gostekBot)
				{
					brainInited = false;
					gostekBot = player.controlled.gameObject.GetComponent<GostekBot>();
					if (!gostekBot)
						return;
				}

				if (gostekBot.brain?.globals == null)
				{
					brainInited = false;
					return;
				}

				if (gostekBot.brain.list.Count > 0)
				{
					brainInited = true;
					return;
				}
				
				var dec = new BotsXDecision(gostekBot.brain, humanBrain);
				gostekBot.brain.AddDecision("BotsXDecision", dec);
				brainInited = true;
			}
			catch (Exception e)
			{
				Debug.Log("[BotsX.SetupDecision] Caught exception of type " + e.GetType());
			}
		}
		
		public void FixedUpdate() {
			if(plrToControl == null)
				return;
			
			if(plrToControl.controlled == null)
				return;
			
			if(plrToControl.controlled.dead)
				return;
			
			
			
			try {
				ScanEnvironment();
			} catch(Exception ex) {
				GameChat.ChatOrLog("// EXCEPTION DURING SCANENV. LINE REACHED := " + lineReached.ToString());
			}
			
			try {
				TakeControl();
			} catch(Exception ex) {
				GameChat.ChatOrLog("// EXCEPTION DURING TAKECTRL. LINE REACHED := " + lineReached.ToString());
				GameChat.ChatOrLog("//  Msg: " + ex.Message);
			}
		}
		
		private int doJumpFor = 0;
		private int goLeftFor = 0;
		private int stuckTimer = 0;
		private int goRightFor = 0;
		private int doJetFor = 0;
		private bool doCannonBall = false;
		private int waitSuperman = 0;
		private float killTargetDistance = 0f;
		private System.Random rand = new System.Random();
		
		
		private float RandomFloat() {
			double v = rand.NextDouble();
			if(rand.NextDouble() < 0.5)
				v *= -1;
			return (float)v;
		}
		
		private Vector2 RandomDirection() {
			return new Vector2(RandomFloat(), RandomFloat()).normalized;
		}
		
		bool didFire = false;
		
		
		private Vector2 altPos = new Vector2(0,0);
		private Vector2 lastPosition = new Vector2(0,0);
		private bool waitForJets = false;
		private Vector2 NULLVEC = new Vector2(0,0);
		
		//public StandardNavigate pather = null;
		//public StandardBrain brain = null;
		
		public static int lineReached = -1;
		private void TakeControl() {
			
			Controls c = plrToControl.controlled;
			StandardObject mystdobj = c.GetComponent<StandardObject>();
			
			if(c == null)
				return;
			
			lineReached = 1;
			
			GostekMovement gm = c.GetComponent<GostekMovement>();
			
			if(gm.v.superman && gm.v.grounded) {
				//GameChat.ChatOrLog("SUPERMAN ON GROUND!!!");
				c.SetKey(Key.Jump, pressed: true);
				return; // leave superman if grounded :(
			} else if (gm.v.slide) {
				c.SetKey(Key.Superman, pressed: false);
				return; // don't slide :(
			}
			
			lineReached = 2;
			
			float hAxis = 0.0f;
			bool amIFlagger = false;
			Vector2 cpos = (Vector2)c.transform.position;
			if (!gm.v.superman)
			{
				cpos.y -= gm.capsule.size.y / 2f;
			}
			Vector3 navTargetPosition = NULLVEC;
			Vector2 gotoPosition = NULLVEC;

			if (navCurrentWaypoint != null && (navCurrentWaypoint.pos - cpos).sqrMagnitude > 9)
			{
				ResetNav();
			}
			
			if(!isDeathmatch) {
				/*if(theirFlag == null || ourFlag == null) {
					FindFlags();
					return; // find flags first then
				}*/
				// i don't trust that
				foreach(Flag flag in Flag._flags) {
					if(flag.team.Number == plrToControl.GetTeam())
						ourFlag = flag;
					else
						theirFlag = flag;
				}

				if (!ourFlag || !theirFlag)
					return;
				
				lineReached = 9001;
				
				navTargetPosition = theirFlag.transform.position;
				
				if(theirFlag.carried) {
					Player holder = GetFlagHolderPlayer(theirFlag);
					if(holder == plrToControl) {
						amIFlagger = true;
					}
				}
				lineReached = 9002;

				if (amIFlagger)
				{
					navTargetPosition = ourFlag.basePoint;
				}
				else
				{
					if (ourFlag.inbase)
					{
						navTargetPosition = theirFlag.transform.position;
					}
					else
					{
						Vector2 ourDelta = (Vector2)ourFlag.transform.position - cpos;
						Vector2 theirDelta = (Vector2)theirFlag.transform.position - cpos;
						if (theirFlag.carried || theirDelta.magnitude > ourDelta.magnitude)
							navTargetPosition = ourFlag.transform.position;
						else
							navTargetPosition = theirFlag.transform.position;
					}
				}
				lineReached = 9003;
				
				navTargetPosition.y += 1f;
			} else {
				if(wpDeathmatch != null)
					navTargetPosition = wpDeathmatch.pos;
			}
			lineReached = 9004;
			
			gotoPosition = navTargetPosition;
			Vector3 gotoDelta = gotoPosition - cpos;
			float gotoDistSqr = gotoDelta.sqrMagnitude;
			bool canSeePathTarget = RayCastHit(cpos, gotoPosition);
			lineReached = 9005;
			
			// do smarter pathing if we can't see or aren't close to the target
			//if (!canSeePathTarget || gotoDistSqr > (50*50)) {
			if (true) {
				lineReached = 9006;
				if (!navTargetPosition.Equals(NULLVEC)) {
					lineReached = 9007;
					SetNavTarget(cpos, navTargetPosition);
					lineReached = 9008;
					
					if (true) {//if (navCurrentConnection != null) {
						gotoPosition = GetWalkTarget(cpos);
						lineReached = 9009;
						gotoDelta = gotoPosition - cpos;
						gotoDistSqr = gotoDelta.sqrMagnitude;
						canSeePathTarget = RayCastHit(cpos, gotoPosition);
						lineReached = 9010;
					}
				}
			}
			lineReached = 9011;
			
			lineReached = 3;
			
			
			bool canSeeKillTarget = false;
			
			/*
			if(gotoPosition.x < cpos.x) {
				hAxis = -1.0f;
			} else {
				hAxis = 1.0f;
			}
			*/
			
			lineReached = 101;
			
			
			/*if(canSeePathTarget) {
				// Ok so we can't see our goto position?
				// but can we see some waypoint or whatever?
				
				lineReached = 102;
				
				if(Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 4) {
					if (goLeftFor <= 0 && goRightFor <= 0) {
						if(rand.NextDouble() < 0.5) {
							goLeftFor = 90;
							doJumpFor = 10;
						} else {
							goRightFor = 90;
							doJumpFor = 10;
						}
					}
					if(amIFlagger && stuckTimer > 120) {
						c.SetKey(Key.DropFlag, pressed: true);
					}
					stuckTimer++;
				} else {
					stuckTimer = 0;
					lineReached = 500;
					//WaypointX wp = WaypointX.FindVisibleWaypointX(cpos, gotoPosition, hAxis);
					if(wp != null)
						gotoPosition = wp.point;
					lineReached = 100;
				}
			}*/
			
			lineReached = 4;

			
			if(plrToKill) {
				if(plrToKill.controlled) {
					Controls k = plrToKill.controlled;
					
					GostekMovement gm2 = k.GetComponent<GostekMovement>();
					Vector2 overAim = new Vector2(1, 2).normalized;
					
					Vector2 kpos = (Vector2)k.transform.position;

					
					if (kpos.x < cpos.x) {
						overAim.x *= -1;
					}
					
					Vector2 toKillTarget = kpos - cpos;
					
					// Adjust for target velocity?
					int adjustmentFactor = 8 + rand.Next(4);
					int f = (int)(toKillTarget.magnitude / 6.0f);
					if(f <= 0)
						f = 1;
					adjustmentFactor /= f;
					if(adjustmentFactor <= 0)
						adjustmentFactor = 1;
					
					kpos += gm2.v.velocity / adjustmentFactor;
					
					lineReached = 5;
					
					float overAimFactor = toKillTarget.magnitude / 12.0f;
					
					killTargetDistance = RayCastDistance(cpos, toKillTarget, 500f);
					if(killTargetDistance > 60)
						killTargetDistance = -1;
					
					if(amIFlagger)
						if(killTargetDistance > 40)
							killTargetDistance = -1; // only shoot close enemies if we are flagger
					
					lineReached = 6;
					
					if(killTargetDistance > toKillTarget.magnitude) {
						canSeeKillTarget = true;
						//ControlsExtensions.SetAimWorld(c, kpos + overAim * overAimFactor);
						var mouseTarget = (kpos + overAim * overAimFactor) - cpos;
						MoveMouse(mouseTarget);
						c.SetAimWorld(cpos + mouseCurrent);
						didFire = false;
						if (Vector2.Distance(mouseTarget, mouseCurrent) < 2f)
						{
							// stop the bots from shooting while they're inside invincibility
							if(plrToControl.GetTimeSinceRespawn() > (handicapsInvincibleAfterSpawnDuration + ((mystdobj.Health > handicapsSpawnHealth) ? 1.5f : 0f))) {
								GostekWeapon gw = c.GetComponent<GostekWeapon>();
								if(gw && gw.weapon) {
									if(gw.weapon.IsReady()) {
										c.SetKey(Key.Fire1, pressed: true);
										//c.gameObject.AddTag("StopHandicap"); // does not actually work on the server :) nice
									}
								}
								didFire = true;
							}
						}
					} else {
						c.SetKey(Key.Fire1, pressed: false);
					}
					
					lineReached = 7;
				}
			}
			
			lineReached = 8;
			
			/*if (waitForJets) {
				if (c.GetComponent<GostekJets>().jetsAmount <= 0.9 && values[IDX_GOSTEK_GROUNDED] >= 0.99) {
					return;
				}
				else {
					waitForJets = false;
				}
			}*/
			
			if(gotoPosition.x < cpos.x) {
				hAxis = -1.0f;
			} else {
				hAxis = 1.0f;
			}
			
			if(!canSeeKillTarget) {
				if (gotoPosition.Equals(NULLVEC)/* || gm.v.superman*/) {
					c.SetAimWorld(cpos + new Vector2(hAxis, 0));
					//MoveMouse((kpos + overAim * overAimFactor) - cpos);
					//c.SetAimWorld(cpos + mouseCurrent);
				} else {
					Vector2 delta = gotoDelta;
					if (delta.magnitude > 40) {
						delta = delta.normalized * 40;
					}
					//c.SetAimWorld(cpos + delta);
					MoveMouse(delta);
					c.SetAimWorld(cpos + mouseCurrent);
				}
			}
			
			lineReached = 9;
			
			/*
			if((cpos - kpos).magnitude <= 6)
				c.SetKey(Key.Fire1, pressed: true);*/
			
			int _idx_down_dir, _idx_dir, _idx_gostek_slope_dir, _idx_up_dir2;
			
			if(hAxis < 0.0f) {
				_idx_dir = IDX_LEFT;
				_idx_down_dir = IDX_DOWN_LEFT;
				_idx_gostek_slope_dir = IDX_GOSTEK_SLOPE_LEFT;
				_idx_up_dir2 = IDX_UP_LEFT2;
				
			} else {
				_idx_dir = IDX_RIGHT;
				_idx_down_dir = IDX_DOWN_RIGHT;
				_idx_gostek_slope_dir = IDX_GOSTEK_SLOPE_RIGHT;
				_idx_up_dir2 = IDX_UP_RIGHT2;
			}
			
			lineReached = 10;
			
			// But can we move to the left?
			if (values[_idx_down_dir] > 0.01)
				c.SetAxis(Axis.H, hAxis);
			
			if (goLeftFor > 0) {
				goLeftFor--;
				c.SetAxis(Axis.H, -1.5f); // cheat :D
			} else if (goRightFor > 0) {
				goRightFor--;
				c.SetAxis(Axis.H, 1.5f); // also cheat
			}
			
			if(doJumpFor > 0) {
				c.SetKey(Key.Jump, pressed: true);
				doJumpFor--;
			} else {
				if(doCannonBall) {
					c.SetKey(Key.Jets, pressed: true);
					doJetFor = 2;
					if(values[_idx_gostek_slope_dir] <= 0.01)
						doJetFor = 45;
					doCannonBall = false;
					waitSuperman = 5;
				} else if(doJetFor > 0) {
					c.SetKey(Key.Jets, pressed: true);
					doJetFor--;
				} else {
					if (values[_idx_gostek_slope_dir] > 0.02 && values[IDX_GOSTEK_GROUNDED] <= 0.1) {
						if(values[IDX_GOSTEK_SUPERMAN] <= 0.1 && values[IDX_DOWN] > 0.05) {
							if(waitSuperman == 0)
								c.SetKey(Key.Superman, pressed: true);
							else
								waitSuperman--;
						}
						if(values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
							c.SetKey(Key.Jets, pressed: true);
						}
					}
					
					if(values[IDX_GOSTEK_SUPERMAN] >= 0.99 && values[IDX_DOWN] <= 0.05) {
						c.SetKey(Key.Superman, pressed: true); 
					}
					
					if (values[_idx_gostek_slope_dir] < 0.01 && values[IDX_GOSTEK_GROUNDED] >= 0.99) {
						doJumpFor = 2;
						c.SetKey(Key.Jump, pressed: true);
					}
				}
			}
			
			lineReached = 11;
			
			if (values[IDX_GOSTEK_GROUNDED] >= 0.99 && Math.Abs(values[IDX_GOSTEK_VELX ]) > 0.2 && values[_idx_gostek_slope_dir] >= 0.005 && values[_idx_up_dir2] >= 0.05) {
				c.SetKey(Key.Jump, pressed: true);
				doJumpFor = 2;
				doCannonBall = true;
			}
			
			if (values[IDX_DOWN] >= 0.99 && values[IDX_GOSTEK_VELY] <= 0.12)
				c.SetKey(Key.Jets, pressed: true);
			
			if(doJumpFor == 0) {
				if (Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 10) {
					// Ok, so we are under your gotoPosition?
					if (cpos.y < gotoPosition.y) {
						if (Math.Abs((double)cpos.x - (double)gotoPosition.x) <= 1)
							c.SetAxis(Axis.H, 0.0f); // stop then I guess
						
						if(c.GetComponent<GostekJets>().jetsAmount < 0.2f) {
							waitForJets = true;
						}
						
						if (values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
							c.SetKey(Key.Superman, pressed: true); // exit superman
						} else {
							c.SetKey(Key.Superman, pressed: false);
						}
						
						if (values[IDX_GOSTEK_GROUNDED] >= 0.99) {
							c.SetKey(Key.Jump, pressed: true);
							doJumpFor = 10;
							doCannonBall = true;
						} else {
							c.SetKey(Key.Jets, pressed: true);
						}
					}
				}
			}
			
			lineReached = 12;
			
			if (values[IDX_GOSTEK_GROUNDED] >= 0.99 && values[IDX_GOSTEK_SUPERMAN] >= 0.99) {
				c.SetKey(Key.Superman, pressed: true); // leave superman if on ground.
			}
			if (gm.v.superman)
			{
				var aimWorld = c.GetAimWorld();
				var aimDelta = aimWorld - cpos;
				var aimingRight = aimDelta.x > 0;
				var targetRight = gotoDelta.x > 0;
				var matchX = gm.v.supermanJetInverted ? (aimingRight != targetRight) : (aimingRight == targetRight);
				var targetDown = gotoDelta.normalized.y < -0.33;
				var targetUp = gotoDelta.normalized.y > 0.33;
				var aimingDown = aimDelta.y < -0.33;
				var aimingUp = aimDelta.y > 0.33;
				var failDown = (gm.v.supermanJetInverted ? aimingDown : aimingUp) && targetDown;
				var failUp = (gm.v.supermanJetInverted ? aimingUp : aimingDown) && targetUp;
				//var aimingRight = aimDelta.x > 0;
				//var targetRight = gotoDelta.x > 0;
				if (!matchX || failUp || failDown)
					c.SetKey(Key.Superman, pressed: true);
				if (failDown)
					doJetFor = 0;
			}
			
			if (gm.v.grounded && gm.v.crouch)
			{
				c.SetKey(Key.Crouch, pressed: false);
				c.SetKey(Key.Jets, pressed: false);
				c.SetKey(Key.Jump, pressed: true);
			}

			var gj = c.GetComponent<GostekJets>();
			
			if (gj.jetsAmount <= 0)
			{
				waitForJets = true;
			}
			
			if (waitForJets) {
				if (gj.jetsAmount <= 0.9) {
					c.SetKey(Key.Jets, pressed: false);
				}
				else {
					waitForJets = false;
				}
			}
			
			lineReached = 13;
		}
		
		private void ScanEnvironment() {
			
			lineReached = -1;
			
			Controls targetControls = plrToControl.controlled;
			GostekMovement gm = targetControls.GetComponent<GostekMovement>();
			lineReached = -200;
			Vector2 origin = (Vector2)targetControls.transform.position;
			Vector2 currentTargetPos = Vector2.zero;
			float currentTargetDist = float.PositiveInfinity;
			
			lineReached = -201;
			
			lastPlrToKill = plrToKill;
			if (lastPlrToKill is null || lastPlrToKill.IsDead()) {
				plrToKill = null;
			} else {
				lineReached = -202;
				plrToKill = lastPlrToKill;
				currentTargetPos = (Vector2)plrToKill.controlled.transform.position;
				currentTargetDist = Vector2.Distance(origin, currentTargetPos);
				lineReached = -203;
			}
			
			lineReached = -177;
			
			foreach(Player player in Players.Get.GetAlive()) {
				if (player == plrToControl)
					continue;
				
				if(!player)
					continue;
				
				if(!player.controlled)
					continue;
				
				if(player.GetTeam() == plrToControl.GetTeam())
					if(!isDeathmatch)
						continue;
				
				// does the player have spawnprotection?
				StandardObject stdobj = player.controlled.GetComponent<StandardObject>();
				if (player.GetTimeSinceRespawn() < (handicapsInvincibleAfterSpawnDuration + ((stdobj.Health > handicapsSpawnHealth) ? 1.5f : 0f))) {
					if (!player.controlled.gameObject.HasTag("StopHandicap")) {
						continue;
					}
				}
				
				lineReached = -109;
				
				// Is the player close enough for us to care?
				Vector2 targetPos = (Vector2)player.controlled.transform.position;
				float targetDist = Vector2.Distance(origin, targetPos);
				if (plrToKill == lastPlrToKill) {
					if (targetDist > currentTargetDist * 0.75f) // target sticking
						continue;
				} else {
					if (targetDist > currentTargetDist) // pick closest
						continue;
				}
				
				// But can we see this player?
				if(RayCastHit(origin, targetPos))
					continue;
				
				plrToKill = player;
				currentTargetPos = targetPos;
				currentTargetDist = targetDist;
			}
			
			lineReached = -403;
			
			if(isDeathmatch) {
				if(wpDeathmatch == null) {
					lineReached = -408;
					wpDeathmatch = NavX.GetRandomPoint();
					lineReached = -409;
					wpDmWatch.Reset();
					wpDmWatch.Start();
					lineReached = -455;
				} else {
					lineReached = -410;
					if(wpDmWatch.ElapsedMilliseconds >= 3000) {
						wpDmWatch.Stop();
						wpDmWatch.Reset();
						wpDmWatch.Start();
						wpDeathmatch = NavX.GetRandomPoint();
					}
					lineReached = -444;
				}
			} else {
				wpDeathmatch = null;
			}
			
			lineReached = -999;
			
			for(int i = 0; i < raycastVectors.Length; i++) {
				float distance = RayCastDistance(origin, raycastVectors[i], 50f);
				distance /= 50f; // scale it to 1..0
				values[i] = (float)Math.Round((double)distance, 4);
			}
			
			if(gm.v.jet)
				values[IDX_GOSTEK_JET] = 1.0f;
			else
				values[IDX_GOSTEK_JET] = 0.0f;
			
			if(gm.v.jump)
				values[IDX_GOSTEK_JUMP] = 1.0f;
			else
				values[IDX_GOSTEK_JUMP] = 0.0f;
			
			if(gm.v.canRoll)
				values[IDX_GOSTEK_CAN_ROLL] = 1.0f;
			else
				values[IDX_GOSTEK_CAN_ROLL] = 0.0f;
			
			
			if(gm.v.jump && wasJumping)
				values[IDX_GOSTEK_JUMP_TIME] = (float)Math.Round((double)sw.ElapsedMilliseconds/(2000.0),4);
			else if (gm.v.jump && !wasJumping) {
				sw.Reset();
				sw.Start();
				wasJumping = true;
				values[IDX_GOSTEK_JUMP_TIME] = 0.0f;
			} else if (!gm.v.jump && wasJumping) {
				sw.Stop();
				wasJumping = false;
			}
			
			if(!gm.v.jump)
				values[IDX_GOSTEK_JUMP_TIME] = -1.0f;
			
			if(gm.v.grounded)
				values[IDX_GOSTEK_GROUNDED] = 1.0f;
			else
				values[IDX_GOSTEK_GROUNDED] = 0.0f;
			
			if(targetControls.IsPressed(Key.Jump))
				outputs[IDX_OUT_JUMP] = 1.0f;
			else
				outputs[IDX_OUT_JUMP] = 0.0f;
			
			if(targetControls.IsPressed(Key.Jets))
				outputs[IDX_OUT_JETS] = 1.0f;
			else
				outputs[IDX_OUT_JETS] = 0.0f;
			
			if(targetControls.IsPressed(Key.Superman))
				outputs[IDX_OUT_SMAN] = 1.0f;
			else
				outputs[IDX_OUT_SMAN] = 0.0f;
			
			if(targetControls.IsPressed(Key.Fire1))
				outputs[IDX_OUT_FIRE] = 1.0f;
			else
				outputs[IDX_OUT_FIRE] = 0.0f;
			
			if(gm.v.superman)
				values[IDX_GOSTEK_SUPERMAN] = 1.0f;
			else 
				values[IDX_GOSTEK_SUPERMAN] = 0.0f;
			
			outputs[IDX_OUT_HAXS] = targetControls.GetAxis(Axis.H);
			
			values[IDX_GOSTEK_VELX] = (float)Math.Round((double)(gm.v.velocity.x / 50f), 3);
			values[IDX_GOSTEK_VELY] = (float)Math.Round((double)(gm.v.velocity.y / 50f), 3);
			
			values[IDX_GOSTEK_SLOPE_LEFT] = values[IDX_DOWN_LEFT2] - values[IDX_DOWN_LEFT];
			values[IDX_GOSTEK_SLOPE_RIGHT] = values[IDX_DOWN_RIGHT2] - values[IDX_DOWN_RIGHT];

			Vector2 aimWorld = targetControls.GetAimWorld() - origin;
			outputs[IDX_OUT_AIMX] = (float)Math.Round((double)(aimWorld.x / 50f), 4);
			outputs[IDX_OUT_AIMY] = (float)Math.Round((double)(aimWorld.y / 50f), 4);
		}
	}

	public static float handicapsInvincibleAfterSpawnDuration = 1.5f;
	public static float handicapsSpawnHealth = 1.0f;
	
	private void UpdateVariables() {
		Handicaps handi = (Handicaps)UnityEngine.Object.FindObjectOfType(typeof(Handicaps));
		if (handi) {
			handicapsInvincibleAfterSpawnDuration = handi.invincibleAfterSpawnDuration;
			handicapsSpawnHealth = handi.spawnHealth;
		}
	}
	
	private void Awake() {
		if(!Net.IsServer) {
			Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoinedLocal);
			Eventor.AddListener(Events.Player_Joined, OnPlayerJoinedLocal);
			
			return;
		}
		
		for(int i = 0; i < Global.botNames.Length; i++) {
			string curName = Global.botNames[i];
			if (curName.StartsWith("BOT"))
				continue;
			else
				Global.botNames[i] = "BOT " + curName;
		}
		
		SnailBot.Reset();
		
		//Time.fixedDeltaTime = 1.0f / 60.0f;
		
		GameChat.instance.OnChat.RemoveListener(this.OnChat);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.RemoveListener(Events.Player_Left, OnPlayerLeft);
		Eventor.RemoveListener(Events.Died, OnDied);
		
		NavX.InitializeGlobal();
		
		Initialize();
		
		GameChat.ChatOrLog("INITIALIZED!");
		
		
		GameChat.instance.OnChat.AddListener(this.OnChat);
		Eventor.AddListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.AddListener(Events.Player_Left, OnPlayerLeft);
		Eventor.AddListener(Events.Died, OnDied);
	}
	
	private void ShowFlags() {
		foreach(Flag flag in Flag._flags) {
			GameChat.ChatOrLog("#" + flag.team.Number.ToString() + " at: " + flag.transform.position.ToString() + " base: " + flag.basePoint.ToString());
		}
	}
	
	private void OnDestroy() {
		GameChat.instance.OnChat.RemoveListener(this.OnChat);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoined);
        Eventor.RemoveListener(Events.Player_Left, OnPlayerLeft);
		Eventor.RemoveListener(Events.Died, OnDied);
		Eventor.RemoveListener(Events.Player_Joined, OnPlayerJoinedLocal);
		NavX.ResetGlobal();
	}
	
	private System.Random rand = new System.Random();

	private void RandomBotChatter(string msg) {
		if(!Net.IsServer)
			return;

		SnailBot rsb = snailBots[rand.Next(snailBots.Count)];
		GameChat.instance.ServerChat("<color=#FEFEFE>[" + rsb.plrToControl.nick + "] " + msg + "</color>");
	}

	private void SendBotEmote(Player bot, int index)
	{
		Pump.temp.Clear();
		Pump.temp.WriteUShort(bot.id);
		Pump.temp.WriteInt(index);
		MonoSingleton<GameServer>.Get.Send(Packets.Emoticon, Pump.temp.Pack(), SendFlags.Reliable | SendFlags.Self);
	}

	private int[] killEmojis = {9, 12, 14, 17};
	private int[] deathEmojis = {6, 7, 10, 11, 15, 13};
	private void OnDied(IGameEvent ev) {
		if(!Net.IsServer || snailBots.Count < 1)
			return;
		
		GlobalDieEvent e = ev as GlobalDieEvent;
		var ownerControls = ev.Sender.GetComponent<Controls>();
		if (e.DamagePlayer.IsBot())
		{
			if (rand.NextDouble() < 0.25)
				SendBotEmote(e.DamagePlayer, killEmojis[rand.Next(0, killEmojis.Length)]);
		}
		if (ownerControls && ownerControls.player && ownerControls.IsBot())
		{
			if (rand.NextDouble() < 0.25)
				SendBotEmote(ownerControls.player, deathEmojis[rand.Next(0, deathEmojis.Length)]);
		}
	}
	
	private void OnPlayerJoinedLocal(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot())
			return; // don't care about humans :)
	}
	
	private void OnPlayerJoined(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot()) {
			return; // don't care about humans :)
		}
		
		AddSnailBotForPlayer(gpe.Player);
	}
	
	private void OnPlayerLeft(IGameEvent ev) {
		GlobalPlayerEvent gpe = ev as GlobalPlayerEvent;
		
		if(!gpe.Player.IsBot())
			return; // again we don't care about humans :)
		
		RemoveSnailBotForPlayer(gpe.Player);
		//GameChat.ChatOrLog("BOTS NOW: " + snailBots.Count.ToString());
	}
	
	private void OnChat(Player p, string msg) {
		/*if (msg == "!position") {
			Vector2 pos = Players.Get.GetHumans()[0].controlled.transform.position;
			GameChat.ChatOrLog("Your position is " + pos.ToString() + " " + (Map.Get.scale.x*Map.Get.width).ToString() + "," + Map.Get.height.ToString());
		}
		if (msg.StartsWith("!emo "))
		{
			var sub = msg.Substring(5);
			var parsed = int.Parse(sub);
			foreach (var b in snailBots)
			{
				SendBotEmote(b.plrToControl, parsed);
			}
		}*/
	}
	
	private void DrawDottedLine(Vector2 start, Vector2 end, int count = 20) {
		Vector2 delta = end - start;
		float len = delta.magnitude;
		if (count > len / 10f)
			count = (int)(len / 10f);
		
		float step = 1f / count;
		
		int maxX = Screen.width;
		int maxY = Screen.height;
		
		for (float lerp = 0f; lerp < 1f; lerp += step) {
			Vector2 lerpS = Vector2.Lerp(start, end, lerp);
			if (lerpS.x > -10 && lerpS.y > -10 && lerpS.x < maxX && lerpS.y < maxY)
				GUI.Label(new Rect(lerpS.x - 3, lerpS.y - 22, 20, 28), ".");
		}
	}
	
	public static Vector2 W2S(Vector3 inVec) {
		Vector2 outVec = GameCamera.Get.cam.WorldToScreenPoint(inVec);
		outVec.y = Screen.height - outVec.y;
		return outVec;
	}
	
	public static Player GetFlagHolderPlayer(Flag flag)
	{
		ItemPickup holder = flag.lastholder;
		if (holder)
		{
			return holder.GetComponent<Controls>().player;
		}
		return null;
	}
	
	// Debug overlay
	private void OnGUI()
	{
		var line = 0;
		try
		{
			Player local = Players.Get.GetLocalPlayer();
			//if (!local || !local.controlled)
			if (!local)
				return;
			line = 1;

			GUI.skin.label.fontSize = 22;
			GUI.skin.label.normal.textColor = new Color(1f, 0f, 0f, 1f);

			GameCamera camera = GameCamera.Get;
			line = 2;

			if (local.IsSpectator() && camera && camera.Target)
			{
				GUI.skin.label.normal.textColor = new Color(1f, 0f, 0f, 1f);
				Vector3 pointer = camera.Target.input.GetPointer(Pointer.AimWorld);
				Vector3 player = camera.Target.transform.position;
				line = 3;
				//camera.MoveToPosition(player);
				// outdated pointer, we need to extrapolate it
				//if (pointer == camera.Target.prevInput.GetPointer(Pointer.AimWorld))
				//{
				// how do we do this?
				//}
				Vector2 pointerS = W2S(pointer);
				Vector2 playerS = W2S(player);
				Vector2 deltaStep = pointerS - playerS;
				DrawDottedLine(playerS, pointerS, 30);
				GUI.Label(new Rect(pointerS.x - 7, pointerS.y - 16, 100, 100), "X");
				line = 4;

				if (!Net.IsServer)
					return;

				SnailBot bot = FindSnailBotForPlayer(camera.Target.player);

				{
					GUI.skin.label.normal.textColor = new Color(1f, 1f, 1f, 1f);
					for (var i = 0; i < bot.values.Length; i++)
					{
						var val = bot.values[i];
						var name = bot.valueNames[i];
						GUI.Label(new Rect(100, 40 + i * 30, 500, 500), i + " : " + name + " : " + val);
					}
					for (var i = 0; i < bot.outputs.Length; i++)
					{
						var val = bot.outputs[i];
						var name = bot.outputNames[i];
						GUI.Label(new Rect(300, 40 + i * 30, 500, 500), i + " : " + name + " : " + val);
					}
				}
				
				line = 5;
				if (bot != null)
				{
					if (!bot.navFinalTarget.Equals(Vector3.zero))
					{
						GUI.skin.label.normal.textColor = new Color(1f, 1f, 0f, 1f);
						Vector3 navFinalTargetDir = bot.navFinalTarget - player;
						Vector2 navFinalTargetS = W2S(bot.navFinalTarget);
						Vector2 navFinalTargetS2 = W2S(player + (navFinalTargetDir.normalized * 25));
						DrawDottedLine(playerS, navFinalTargetS, 50);
						line = 6;

						GUI.Label(new Rect(navFinalTargetS.x - 5, navFinalTargetS.y - 10, 100, 100), "T");
						if (bot.navCurrentWaypoint != null)
						{
							GUI.skin.label.normal.textColor = new Color(0f, 0f, 1f, 1f);
							Vector2 navCurrentWaypointS = W2S(bot.navCurrentWaypoint.pos);
							GUI.Label(new Rect(navCurrentWaypointS.x - 5, navCurrentWaypointS.y - 10, 100, 100), "C");
							if (bot.navNextWaypoint != null)
							{
								GUI.skin.label.normal.textColor = new Color(0f, 1f, 1f, 1f);
								Vector3 navCurrentConnPos = bot.navNextWaypoint.pos;
								Vector2 navCurrentConnectionS = W2S(navCurrentConnPos);
								GUI.Label(new Rect(navCurrentConnectionS.x - 5, navCurrentConnectionS.y - 10, 100, 100),
									"N");
								if (bot.navCurrentPath != null && bot.navCurrentPath.Count > 0)
								{
									GUI.skin.label.normal.textColor = new Color(0f, 1f, 0f, 0.5f);
									Vector2 prevConnPosS = navCurrentConnectionS;
									foreach (NavX.NavPoint wpConn in bot.navCurrentPath)
									{
										Vector2 nextConnPosS = W2S(wpConn.pos);
										GUI.Label(new Rect(nextConnPosS.x - 5, nextConnPosS.y - 10, 100, 100), "w");
										DrawDottedLine(prevConnPosS, nextConnPosS, 4);
										prevConnPosS = nextConnPosS;
									}
								}
							}
						}
						line = 7;
					}
				}
			}

			// Map map = Map.Get;
			// int i = 0;
			// Collider2D[] results = new Collider2D[1];
			// foreach (GameObject gameObject in map.combine) {
			// ProtoshapeEdit[] componentsInChildren = gameObject.GetComponentsInChildren<ProtoshapeEdit>();
			// for (int y = 0; y < componentsInChildren.Length; y++)
			// {
			// ProtoshapeEdit shape_edit = componentsInChildren[y];
			// ProtoShape2D shape = shape_edit.ps2d;
			// switch (shape_edit.mapData.type) {
			// case ProtoshapeEdit.ColliderType.Solid:
			// case ProtoshapeEdit.ColliderType.PlayersCollide:
			// case ProtoshapeEdit.ColliderType.Platform:
			// case ProtoshapeEdit.ColliderType.Climbable:
			// foreach (PS2DColliderPoint point in shape.cpoints) {
			// Vector2 wp = point.wPosition;
			// wp += Vector2.up;//point.normal.normalized;
			// Vector2 pt = camera.cam.WorldToScreenPoint(wp);
			// int hits = Physics2D.OverlapPointNonAlloc(wp, results, WaypointX.raycastLayer);
			// if (hits < 1)
			// GUI.skin.label.normal.textColor = new Color(0f, 1f, 0f, 1f);
			// else
			// GUI.skin.label.normal.textColor = new Color(1f, 0f, 0f, 1f);
			// GUI.Label(new Rect(pt.x - 5, Screen.height-pt.y - 20, 100, 100), "p");
			// }
			// break;
			// default:
			// break;
			// }
			// //GUI.Label(new Rect(600, 150 + i, 300, 100), componentsInChildren.Length.ToString());
			// }
			// }
		} catch (System.Exception ex) {
			Debug.Log($"[BotsX.OnGUI] @@@@@ ERROR @@@@@ AT LINE " + line);
			try {
				Debug.Log($"[BotsX.OnGUI] Exception HRESULT " + ex.HResult.ToString("X") + " type " + ex.GetType());
				Debug.Log($"[BotsX.OnGUI] Exception message: " + ex.Message);
				//Debug.Log($"[BotsX.OnGUI] Exception trace: " + ex.StackTrace);
			} catch (System.Exception ex2) {
				Debug.Log($"[BotsX.OnGUI] Another exception occured inside handler, HRESULT " + ex2.HResult.ToString("X") + " type " + ex2.GetType());
			}
		}
	}
	
	
	
	long tick = 1;
	bool stopped = true;
	List<SnailBot> snailBots = new List<SnailBot>();
	
	private void RemoveSnailBotForPlayer(Player player) {
		SnailBot found = null;
		foreach(SnailBot snailBot in snailBots) {
			if (snailBot.plrToControl == player)
				found = snailBot;
		}
		snailBots.Remove(found);
	}
	
	private void AddSnailBotForPlayer(Player player) {
		Debug.Log("Adding SnailBot for " + player);
		Flag hisFlag = null;
		Flag theirFlag = null;
		
		foreach(Flag flag in Flag._flags) {
			if(flag.team.Number == player.GetTeam())
				hisFlag = flag;
			else
				theirFlag = flag;
		}
		
		snailBots.Add(new SnailBot(player, hisFlag, theirFlag));
	}
	
	public SnailBot FindSnailBotForPlayer(Player player) {
		foreach(SnailBot snailBot in snailBots) {
			if (snailBot.plrToControl == player)
				return snailBot;
		}
		return null;
	}

	public static SnailBotsX Instance = null;
	private void Initialize()
	{
		Instance = this;
		UpdateVariables();
		snailBots.Clear();
		foreach(Player player in Players.Get.GetBots()) {
			AddSnailBotForPlayer(player);
		}
		stopped = false;
	}
	
	private void FixedUpdate() {
		//TestCam();

		if (!Net.IsServer)
		{
			return;
		}
		
		if(stopped)
			return;
		
		foreach(SnailBot snailBot in snailBots)
			snailBot.SetupDecision();
	}
}

public class BotsXDecision : HumanBrain.HumanDecision
{
	public BotsXDecision(StandardBrain brain, HumanBrain human)
		: base(brain, human)
	{
	}
	public override void Start()
	{
		base.Start();
	}
	public override float CalculatePriority()
	{
		float p = 1f;
		return p;
	}
	public override bool Do()
	{
		base.Do();
		if (!SnailBotsX.Instance)
			return true;
		var sx = SnailBotsX.Instance;
		var sb = sx.FindSnailBotForPlayer(this.controls.player);
		if (sb == null)
			return true;
		sb.FixedUpdate();
		return true;
	}
}