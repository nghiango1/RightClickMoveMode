using StardewValley;
using StardewModdingAPI;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MouseMoveMode
{
    class PathFindingHelper
    {
        private List<DrawableNode> nodes = new List<DrawableNode>();

        private IMonitor Monitor;
        private float microDelta = 0.0001f;

        private PriorityQueue<Vector2, float> pq = new PriorityQueue<Vector2, float>();
        private HashSet<Vector2> visited = new HashSet<Vector2>();
        private Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        public Vector2 destination;
        private Vector2 destinationTile;
        private Stack<Vector2> path;

        public bool useBetter = false;
        public bool debugCheckedTile = false;
        public bool debugPassable = false;

        private int step;

        public PathFindingHelper(IMonitor im, bool useBetter = false, bool debugCheckedTile = false, bool debugPassable = true)
        {
            this.Monitor = im;
            this.useBetter = useBetter;
            this.debugCheckedTile = debugCheckedTile;
            this.debugPassable = debugPassable;
        }

        public void drawThing(SpriteBatch b)
        {
            if (this.debugCheckedTile)
            {
                foreach (var node in this.nodes)
                {
                    node.draw(b);
                }
            }

            if (this.debugPassable)
            {
                Util.drawPassable(b);
            }

            if (this.path is not null)
            {
                foreach (var node in this.path)
                {
                    DrawHelper.drawBox(b, Util.toPosition(node));
                }
            }
        }

        public void addNode(Rectangle box)
        {
            var node = new DrawableNode(box);
            this.Monitor.Log(String.Format("Manual: {0}", node), LogLevel.Info);
            nodes.Add(node);
        }

        public void loadMap()
        {
            this.nodes.Clear();
            Util.flushCache();
        }

        public Vector2 changeDes(Vector2 destination)
        {
            return aStarPathFinding(destination);
        }

        public Vector2 aStarPathFinding(Vector2 destination)
        {
            GameLocation gl = Game1.player.currentLocation;
            this.destination = destination;
            this.destinationTile = Util.toTile(this.destination);

            this.visited.Clear();

            this.pq.Clear();
            this.pq.Enqueue(Game1.player.Tile, 0);

            this.cameFrom.Clear();
            var gScore = new Dictionary<Vector2, float>();
            var fScore = new Dictionary<Vector2, float>();

            Vector2 start = Game1.player.Tile;
            Vector2 bestScoreTile = start;
            float bestScore = Vector2.Distance(Game1.player.Position, this.destination);

            gScore.Add(start, 0);
            fScore.Add(start, Vector2.Distance(Game1.player.Position, this.destination));
            this.nodes.Clear();

            // I just too dumb so let limit the time we try to find best past
            // we have a quite small available click screen so it fine as long
            // as the limit can match the total screen tile
            int limit = 100;
            while (pq.Count > 0 && limit > 0)
            {
                limit -= 1;
                var current = pq.Dequeue();
                if (Vector2.Distance(Util.toPosition(current), this.destination) < 33f)
                {
                    this.destinationTile = current;
                    //this.Monitor.Log("Found path!", LogLevel.Info);
                    return updatePath();
                }

                if (Vector2.Distance(current, this.destinationTile) < this.microDelta)
                {
                    this.destinationTile = current;
                    //this.Monitor.Log("Found path!", LogLevel.Info);
                    return updatePath();
                }

                for (int i = -1; i <= 1; i += 1)
                    for (int j = -1; j <= 1; j += 1)
                    {
                        Vector2 neighbor = current + new Vector2(i, j);
                        if ((i == 0 && j == 0) || !Util.isTilePassable(neighbor) || visited.Contains(neighbor))
                            continue;
                        // diagonal special case handling, just don't do it if there is a blockage
                        if (Vector2.Distance(current, neighbor) > 1.2f)
                        {
                            if (!Util.isTilePassable(current.X, neighbor.Y) || !Util.isTilePassable(neighbor.X, current.Y))
                            {
                                continue;
                            }
                        }

                        // horse riding will make player bigger, which can't go up and down into 1 tile gap
                        if (Game1.player.isRidingHorse())
                        {
                            if (!Util.isTilePassable(neighbor.X - 1, neighbor.Y))
                            {
                                bool check = false;
                                for (int z = -1; z <= 1; z += 1)
                                {
                                    if (!Util.isTilePassable(neighbor.X + 1, neighbor.Y + z))
                                    {
                                        check = true;
                                        break;
                                    }
                                }
                                if (check)
                                    continue;
                            }

                            if (!Util.isTilePassable(neighbor.X + 1, neighbor.Y))
                            {
                                bool check = false;
                                for (int z = -1; z <= 1; z += 1)
                                {
                                    if (!Util.isTilePassable(neighbor.X - 1, neighbor.Y + z))
                                    {
                                        check = true;
                                        break;
                                    }
                                }
                                if (check)
                                    continue;
                            }
                        }

                        visited.Add(neighbor);
                        this.nodes.Add(new DrawableNode((int)neighbor.X * 64, (int)neighbor.Y * 64, 64, 64));
                        var temp = gScore[current] + Vector2.Distance(Util.toPosition(current), Util.toPosition(neighbor));
                        if (gScore.ContainsKey(neighbor))
                        {
                            if (gScore[neighbor] < temp)
                            {
                                continue;
                            }
                            else
                            {
                                this.cameFrom[neighbor] = current;
                                gScore[neighbor] = temp;
                                fScore[neighbor] = temp + (float)Math.Pow(Vector2.Distance(Util.toPosition(neighbor), this.destination), 2);
                            }
                        }
                        else
                        {
                            this.cameFrom.Add(neighbor, current);
                            gScore.Add(neighbor, temp);
                            fScore.Add(neighbor, temp + (float)Math.Pow(Vector2.Distance(Util.toPosition(neighbor), this.destination), 2));
                        }

                        pq.Enqueue(neighbor, fScore[neighbor]);
                        if (bestScore > Vector2.Distance(Util.toPosition(neighbor), this.destination))
                        {
                            bestScore = Vector2.Distance(Util.toPosition(neighbor), this.destination);
                            bestScoreTile = neighbor;
                        }
                    }
            }

            this.destinationTile = bestScoreTile;
            return updatePath();
        }

        public Vector2 updatePath()
        {
            this.path = new Stack<Vector2>();
            Vector2 start = Game1.player.Tile;
            Vector2 p = this.destinationTile;
            while (Vector2.Distance(p, start) > this.microDelta)
            {
                //this.Monitor.Log("Path: " + p, LogLevel.Info);
                this.path.Push(p);
                p = this.cameFrom[p];
            }
            this.path.Push(start);
            return this.path.Peek();
        }

        public Vector2 nextPath()
        {
            Vector2 start = Game1.player.Tile;
            if (this.path is null)
                return start;
            if (this.path.Count > 0)
            {
                Vector2 p = this.path.Peek();
                if (Vector2.Distance(p, start) > this.microDelta)
                {

                    var paddingX = 31f;
                    // Sometime we want to go into the middle so horse can getin 2 tile gap
                    if (Game1.player.isRidingHorse())
                    {
                        if (Game1.player.Tile.Y == p.Y)
                        {
                            paddingX = 63f;
                            if (Util.isTilePassable(new Vector2(p.X + 1, p.Y)))
                            {
                                paddingX = 1f;
                            }
                        }
                    }
                    return Util.toPosition(p, paddingX: paddingX);
                }
                this.path.Pop();
                return nextPath();
            }
            else
                return this.destination;
        }
    }
}
