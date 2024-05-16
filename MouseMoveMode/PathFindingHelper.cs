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
        private List<DrawableNode> visitedNodes = new List<DrawableNode>();
        private Stack<DrawableNode> pathNodes = new Stack<DrawableNode>();

        private IMonitor Monitor;
        private float microTileDelta = 0.0001f;
        private float microPositionDelta = 35f;

        private PriorityQueue<Vector2, float> pq = new PriorityQueue<Vector2, float>();
        private HashSet<Vector2> visited = new HashSet<Vector2>();
        private Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        private Dictionary<Vector2, float> gScore = new Dictionary<Vector2, float>();
        private Dictionary<Vector2, float> fScore = new Dictionary<Vector2, float>();

        private Vector2 destination = new Vector2(0, 0);
        private Vector2 destinationTile = new Vector2(0, 0);
        private Stack<Vector2> path = new Stack<Vector2>();

        public bool useBetter = false;
        public bool debugCheckedTile = false;
        public bool debugPassable = false;

        private int step;

        public void flushCache()
        {
            this.pq.Clear();
            this.visited.Clear();
            this.cameFrom.Clear();
            this.gScore.Clear();
            this.fScore.Clear();
            this.path.Clear();

            this.visitedNodes.Clear();
            this.pathNodes.Clear();
        }

        public PathFindingHelper(IMonitor im, bool useBetter = false, bool debugCheckedTile = false, bool debugPassable = false)
        {
            this.Monitor = im;
            this.useBetter = useBetter;
            this.debugCheckedTile = debugCheckedTile;
            this.debugPassable = debugPassable;
        }

        public void drawVisitedNodes(SpriteBatch b)
        {
            foreach (var node in this.visitedNodes)
                node.draw(b, Color.Aqua);
        }

        public void drawPath(SpriteBatch b)
        {
            foreach (var node in this.pathNodes)
                node.draw(b);
        }

        public void drawThing(SpriteBatch b)
        {
            if (this.debugCheckedTile)
                this.drawVisitedNodes(b);

            if (this.debugPassable)
                Util.drawPassable(b);

            this.drawPath(b);
        }

        public void loadMap()
        {
            this.flushCache();
            Util.flushCache();
        }

        public void changeDes(Vector2 destination)
        {
            this.flushCache();
            Util.flushCache();
            aStarPathFinding(destination);
        }

        public void aStarPathFinding(Vector2 destination)
        {
            GameLocation gl = Game1.player.currentLocation;
            this.destination = destination;

            // This preventing error when changing or rounding destinaltion multiple times
            this.destinationTile = Util.toTile(this.destination);
            this.pq.Enqueue(Game1.player.Tile, 0);

            Vector2 start = Game1.player.Tile;
            Vector2 bestScoreTile = start;
            float bestScore = Vector2.Distance(Game1.player.Position, this.destination);

            gScore.Add(start, 0);
            fScore.Add(start, Vector2.Distance(Game1.player.Position, this.destination));

            // I just too dumb so let limit the time we try to find best past
            // we have a quite small available click screen so it fine as long
            // as the limit can match the total screen tile
            int limit = 100;
            while (pq.Count > 0 && limit > 0)
            {
                limit -= 1;
                var current = pq.Dequeue();
                if (Vector2.Distance(this.destination, Util.toPosition(current)) < this.microPositionDelta)
                {
                    this.destinationTile = current;
                    //this.Monitor.Log("Found path!", LogLevel.Info);
                    updatePath();
                    return;
                }

                // Being extra cautious here, as this is the true end for a_star
                if (Vector2.Distance(current, this.destinationTile) < this.microTileDelta)
                {
                    this.destinationTile = current;
                    //this.Monitor.Log("Found path!", LogLevel.Info);
                    updatePath();
                    return;
                }

                List<Vector2> neighborList = new List<Vector2>();
                for (int i = -1; i <= 1; i += 1)
                    for (int j = -1; j <= 1; j += 1)
                    {
                        if (i == 0 && j == 0)
                            continue;
                        Vector2 neighbor = current + new Vector2(i, j);
                        // tile isn't passable or already visited
                        if (!Util.isTilePassable(neighbor) || visited.Contains(neighbor))
                            continue;

                        // diagonal special case handling, just don't do it if there is a blockage
                        if (Vector2.Distance(current, neighbor) > 1.2f)
                        {
                            if (!Util.isTilePassable(current.X, neighbor.Y) || !Util.isTilePassable(neighbor.X, current.Y))
                            {
                                continue;
                            }
                        }

                        // horse riding will make player bigger, which can't go up and down into 1 tile gap. Skip anything like this
                        // ?-any, O-passable, X-unpassable, P-current tile
                        //
                        // ?O?    OOX    OO?    XOO    ?OO
                        // XPX    XP?    XP?    ?PX    ?PX
                        // ?O?    OO?    OOX    ?OO    XOO
                        if (Game1.player.isRidingHorse())
                        {
                            if (!Util.isTilePassable(neighbor.X - 1, neighbor.Y))
                            {
                                bool check = false;
                                check = check || !Util.isTilePassable(neighbor.X + 1, neighbor.Y - 1);
                                check = check || !Util.isTilePassable(neighbor.X + 1, neighbor.Y);
                                check = check || !Util.isTilePassable(neighbor.X + 1, neighbor.Y + 1);
                                if (check)
                                    continue;
                            }

                            if (!Util.isTilePassable(neighbor.X + 1, neighbor.Y))
                            {
                                bool check = false;
                                check = check || !Util.isTilePassable(neighbor.X - 1, neighbor.Y - 1);
                                check = check || !Util.isTilePassable(neighbor.X - 1, neighbor.Y + 1);
                                if (check)
                                    continue;
                            }
                        }

                        // Pass all checked, this tile could be consider to be use
                        neighborList.Add(neighbor);
                    }

                foreach (var neighbor in neighborList)
                {
                    visited.Add(neighbor);
                    this.visitedNodes.Add(new DrawableNode(Util.toBoxPosition(neighbor)));

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
            updatePath();
        }

        public void updatePath()
        {
            this.path.Clear();
            this.pathNodes.Push(new DrawableNode(this.destination));
            Vector2 startTile = Game1.player.Tile;
            Vector2 pointerTile = this.destinationTile;
            while (Vector2.Distance(pointerTile, startTile) > this.microTileDelta)
            {
                var traceBackPosition = this.addPadding(pointerTile);
                //this.Monitor.Log("Path: " + traceBackPosition, LogLevel.Info);
                this.path.Push(traceBackPosition);

                this.pathNodes.Push(new DrawableNode(traceBackPosition));
                pointerTile = this.cameFrom[pointerTile];
            }

            // Could be needed, but player already consider standing on this tile
            // it for extra protection that player can still stuck for any reason
            this.path.Push(Util.toPosition(startTile));
            this.pathNodes.Push(new DrawableNode(startTile));
        }

        public Vector2 addPadding(Vector2 positionTile)
        {
            Rectangle box = Game1.player.GetBoundingBox();
            var padX = 32;
            var padY = 32;

            // blockage on left side by any mean
            bool checkLeft = !Util.isTilePassable(positionTile.X - 1, positionTile.Y);
            checkLeft = checkLeft || !Util.isTilePassable(positionTile.X - 1, positionTile.Y - 1);
            checkLeft = checkLeft || !Util.isTilePassable(positionTile.X - 1, positionTile.Y + 1);

            // blockage on right side by any mean
            bool checkRight = !Util.isTilePassable(positionTile.X + 1, positionTile.Y);
            checkRight = checkRight || !Util.isTilePassable(positionTile.X + 1, positionTile.Y - 1);
            checkRight = checkRight || !Util.isTilePassable(positionTile.X + 1, positionTile.Y + 1);

            if (checkLeft ^ checkRight)
            {
                if (checkLeft)
                {
                    //this.Monitor.Log("Left blockage", LogLevel.Info);
                    padX = box.Width;
                }
                if (checkRight)
                {
                    //this.Monitor.Log("Right blockage", LogLevel.Info);
                    padX = 64 - box.Width;
                }
            }

            // blockage on top side by any mean
            bool checkTop = !Util.isTilePassable(positionTile.X - 1, positionTile.Y - 1);
            checkTop = checkTop || !Util.isTilePassable(positionTile.X, positionTile.Y - 1);
            checkTop = checkTop || !Util.isTilePassable(positionTile.X + 1, positionTile.Y - 1);

            // blockage on bottom side by any mean
            bool checkBottom = !Util.isTilePassable(positionTile.X - 1, positionTile.Y + 1);
            checkBottom = checkBottom || !Util.isTilePassable(positionTile.X, positionTile.Y + 1);
            checkBottom = checkBottom || !Util.isTilePassable(positionTile.X + 1, positionTile.Y + 1);

            if (checkTop ^ checkBottom)
            {
                if (checkTop)
                {
                    //this.Monitor.Log("Top blockage", LogLevel.Info);
                    padY = box.Height;
                }
                if (checkBottom)
                {
                    //this.Monitor.Log("Bottom blockage", LogLevel.Info);
                    padY = 64 - box.Height;
                }
            }
            var res = Util.toPosition(positionTile, padX, padY);
            return res;
        }

        public Nullable<Vector2> nextPath()
        {
            if (this.path.Count == 0)
                return null;

            Vector2 start = Game1.player.GetBoundingBox().Center.ToVector2();
            Vector2 next = this.path.Peek();
            if (Vector2.Distance(start, next) > this.microPositionDelta)
                return this.path.Peek();
            this.path.Pop();
            this.pathNodes.Pop();
            return this.nextPath();
        }

        public Vector2 moveDirection()
        {
            var optionalNext = this.nextPath();
            if (optionalNext is null)
            {
                return new Vector2(0, 0);
            }
            Vector2 next = optionalNext.Value;
            Vector2 player = Game1.player.GetBoundingBox().Center.ToVector2();

            Vector2 direction = Vector2.Subtract(next, player);
            return direction;
        }
    }
}
