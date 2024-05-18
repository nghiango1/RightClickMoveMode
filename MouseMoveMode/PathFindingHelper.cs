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
        // Logic control
        public bool debugVisitedTile = false;
        public bool debugVerbose = true;
        public bool debugLineToTiles = true;

        public bool doPathSkipping = true;

        private IMonitor Monitor;
        private List<DrawableNode> visitedNodes = new List<DrawableNode>();
        private Stack<DrawableNode> pathNodes = new Stack<DrawableNode>();
        private List<DrawableNode> lineToTileNodes = new List<DrawableNode>();
        private DrawableNode targetNode = null;

        private float microTileDelta = 0.0001f;
        private float microPositionDelta = 25f;

        private PriorityQueue<Vector2, float> pq = new PriorityQueue<Vector2, float>();
        private HashSet<Vector2> visited = new HashSet<Vector2>();
        private Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        private Dictionary<Vector2, float> gScore = new Dictionary<Vector2, float>();
        private Dictionary<Vector2, float> fScore = new Dictionary<Vector2, float>();

        private Vector2 destination = new Vector2(0, 0);
        private Vector2 destinationTile = new Vector2(0, 0);
        private Stack<Vector2> path = new Stack<Vector2>();

        public static bool isBestScoreFront { get; private set; }
        public Vector2 bestNext { get; private set; }

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

        public PathFindingHelper()
        {
            this.Monitor = ModEntry.getMonitor();
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
            if (targetNode is not null)
                targetNode.draw(b, Color.Red);

        }

        public void drawIndicator(SpriteBatch b)
        {
            if (this.debugVisitedTile)
                this.drawVisitedNodes(b);

            if (Util.debugPassable)
                Util.drawPassable(b);

            if (this.debugLineToTiles)
            {
                foreach (var node in this.lineToTileNodes)
                    node.draw(b);
            }

            this.drawPath(b);
        }

        public void loadMap()
        {
            this.flushCache();
            Util.flushCache();
        }

        public void changeDes(Vector2 destination)
        {
            if (this.debugVerbose)
            {
                this.Monitor.Log(String.Format("Change destination from {0} to {1}, tile value {2} to {3}", this.destination, destination, Util.toTile(this.destination), Util.toTile(destination)), LogLevel.Info);
            }

            this.flushCache();
            Util.flushCache();
            targetNode = new DrawableNode(destination);
            aStarPathFinding(destination);

            if (this.debugLineToTiles)
            {
                if (this.debugVerbose)
                {
                    this.Monitor.Log(String.Format("Try draw line from tile {0} to {1}", getPlayerStandingTile(), Util.toTile(this.destination)), LogLevel.Info);
                }
                this.lineToTileNodes.Clear();
                List<Vector2> line = lineToTiles(getPlayerStandingTile(), Util.toTile(this.destination));
                for (var i = 0; i < line.Count; i += 1)
                {
                    var tile = Util.fixFragtionTile(line[i]);
                    var node = new DrawableNode(Util.toBoxPosition(tile));
                    if (i >= 1)
                    {
                        var prev = Util.fixFragtionTile(line[i - 1]);
                        if (!isValidMovement(prev, tile))
                        {
                            node.color = Color.Red;
                        }
                    }
                    if (this.debugVerbose)
                    {
                        this.Monitor.Log(String.Format("Line to tiles {0} (pre-fixed: {1}) or {2}", tile, line[i], this.addPadding(tile)), LogLevel.Info);
                    }
                    this.lineToTileNodes.Add(node);
                }
            }
        }

        /**
         * @brief Path-finding scaled down destination tile locatiton
         */
        public Vector2 getCurrentDestinationTile()
        {
            return Util.toTile(this.destination);
        }

        private bool isValidMovement(Vector2 current, int i, int j)
        {
            Vector2 neighbor = new Vector2(current.X + i, current.Y + j);
            return isValidMovement(current, neighbor);
        }

        /**
         * @brief the neighbor should only be a tile away from the current one
         */
        private bool isValidMovement(Vector2 current, Vector2 neighbor)
        {
            var gl = Game1.player.currentLocation;
            var i = neighbor.X - current.X;
            var j = neighbor.Y - current.X;

            if (i == 0 && j == 0)
                return false;
            // tile isn't passable 
            if (!Util.isTilePassable(neighbor) || !Util.isTilePassable(current))
                return false;

            // diagonal special case handling, just don't do it if there is a blockage
            if (Vector2.Distance(current, neighbor) > 1.2f)
            {
                if (!Util.isTilePassable(current.X, neighbor.Y) || !Util.isTilePassable(neighbor.X, current.Y))
                {
                    return false;
                }
            }

            // horse riding will make player bigger, which can't go up and down into 1 tile gap. Skip anything like this unless we goes through fence gate
            // ?-any, O-passable, X-unpassable, P-current tile (which isn't gate)
            // This isn't ok
            // ?O?    OOX    OO?    XOO    ?OO
            // XPX    XP?    XP?    ?PX    ?PX
            // ?O?    OO?    OOX    ?OO    XOO
            if (Game1.player.isRidingHorse())
            {
                bool neighborIsGate = gl.getObjectAtTile((int)neighbor.X, (int)neighbor.Y) is Fence;
                if (neighborIsGate)
                {
                    if (this.debugVerbose)
                    {
                        this.Monitor.Log("Found gate", LogLevel.Info);
                        this.Monitor.Log(String.Format("From {0} to {1}", current, neighbor));
                    }
                }

                bool checkBlockage = false;
                if (!Util.isTilePassable(neighbor.X - 1, neighbor.Y))
                {
                    checkBlockage = checkBlockage || (!Util.isTilePassable(neighbor.X + 1, neighbor.Y - 1));
                    checkBlockage = checkBlockage || (!Util.isTilePassable(neighbor.X + 1, neighbor.Y));
                    checkBlockage = checkBlockage || (!Util.isTilePassable(neighbor.X + 1, neighbor.Y + 1));
                }

                if (!Util.isTilePassable(neighbor.X + 1, neighbor.Y))
                {
                    checkBlockage = checkBlockage || (!Util.isTilePassable(neighbor.X - 1, neighbor.Y - 1));
                    checkBlockage = checkBlockage || (!Util.isTilePassable(neighbor.X - 1, neighbor.Y + 1));
                }

                // We can squeze through gate when ringind horse ONLY WHEN MOVE UP AND DOWN
                bool squezeToGate = (neighbor.X == current.X) && neighborIsGate;
                if (squezeToGate)
                {
                    if (this.debugVerbose)
                        this.Monitor.Log("Seem like we going through gate now");
                }

                if (checkBlockage && !squezeToGate)
                    return false;
            }

            return true;
        }

        public void aStarPathFinding(Vector2 destination)
        {
            GameLocation gl = Game1.player.currentLocation;
            this.destination = destination;

            // This preventing error when changing or rounding destination multiple times
            this.destinationTile = Util.toTile(this.destination);
            this.pq.Enqueue(getPlayerStandingTile(), 0);

            Vector2 start = getPlayerStandingTile();
            // This also favor consider player in front of destination
            isBestScoreFront = false;
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

                // Being extra cautious here, as this is the true end for a_star
                if (Vector2.Distance(current, this.destinationTile) < this.microTileDelta)
                {
                    this.destinationTile = current;
                    if (this.debugVerbose)
                        this.Monitor.Log("Found path!", LogLevel.Info);
                    updatePath();
                    return;
                }

                List<Vector2> neighborList = new List<Vector2>();
                for (int i = -1; i <= 1; i += 1)
                    for (int j = -1; j <= 1; j += 1)
                    {
                        Vector2 neighbor = new Vector2(current.X + i, current.Y + j);
                        if (visited.Contains(neighbor))
                        {
                            continue;
                        }
                        if (isValidMovement(current, neighbor))
                        {
                            // Pass all checked, this tile could be consider to be use
                            neighborList.Add(neighbor);
                        }
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

                    // We favor player to be infront of the destination, this make the path found more reliable-ish
                    // Again, within-limit of the effective reach for the object. I just hard code 1 tiles here atm
                    if (neighbor.X == this.destinationTile.X && (neighbor.Y - this.destinationTile.Y >= 0) && (neighbor.Y - this.destinationTile.Y) < 1.1f)
                    {
                        if (!isBestScoreFront)
                        {
                            bestScore = Vector2.Distance(Util.toPosition(neighbor), this.destination);
                            bestScoreTile = neighbor;
                        }
                        isBestScoreFront = true;
                        if (isBestScoreFront && (bestScore > Vector2.Distance(Util.toPosition(neighbor), this.destination)))
                        {
                            bestScore = Vector2.Distance(Util.toPosition(neighbor), this.destination);
                            bestScoreTile = neighbor;
                        }
                    }
                    else
                    {
                        if (!isBestScoreFront && (bestScore > Vector2.Distance(Util.toPosition(neighbor), this.destination)))
                        {
                            bestScore = Vector2.Distance(Util.toPosition(neighbor), this.destination);
                            bestScoreTile = neighbor;
                        }
                    }
                }
            }

            // Destination tile is unreach-able (or no path found within limit),
            // so we goes to the closest tile or the in-front tile
            this.destinationTile = bestScoreTile;
            updatePath();
        }

        public void updatePath()
        {
            this.path.Clear();
            // We may not add destination here, as it could be un reachable
            if (this.cameFrom.ContainsKey(Util.toTile(this.destination)))
            {
                this.path.Push(this.destination);
                this.pathNodes.Push(new DrawableNode(this.destination));
            }

            Vector2 startTile = getPlayerStandingTile();
            Vector2 pointerTile = this.destinationTile;
            while (Vector2.Distance(pointerTile, startTile) > this.microTileDelta)
            {
                var traceBackPosition = this.addPadding(pointerTile);
                if (this.debugVerbose)
                    this.Monitor.Log("Path: " + traceBackPosition, LogLevel.Info);
                this.path.Push(traceBackPosition);

                this.pathNodes.Push(new DrawableNode(traceBackPosition));
                pointerTile = this.cameFrom[pointerTile];
            }

            // Could be needed, but player already consider standing on this tile
            // it for extra protection that player can still stuck for any reason
            this.path.Push(this.addPadding(startTile));
            this.pathNodes.Push(new DrawableNode(startTile));

            if (doPathSkipping)
            {
                // This allowing us to be in unstuckable position when begin to
                // move from the start tile
                bestNext = this.path.Peek();
            }
        }

        public Vector2 addPadding(Vector2 positionTile)
        {
            Rectangle box = Game1.player.GetBoundingBox();
            var padX = 32;
            var padY = 32;
            var microRounding = 16;

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
                    if (this.debugVerbose)
                        this.Monitor.Log("Left blockage", LogLevel.Info);
                    padX = (box.Width / 2 + microRounding);
                }
                if (checkRight)
                {
                    if (this.debugVerbose)
                        this.Monitor.Log("Right blockage", LogLevel.Info);
                    padX = 64 - (box.Width / 2 + microRounding);
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
                    if (this.debugVerbose)
                        this.Monitor.Log("Top blockage", LogLevel.Info);
                    padY = (box.Height / 2 + microRounding);
                }
                if (checkBottom)
                {
                    if (this.debugVerbose)
                        this.Monitor.Log("Bottom blockage", LogLevel.Info);
                    padY = 64 - (box.Height / 2 + microRounding);
                }
            }
            var res = Util.toPosition(positionTile, padX, padY);
            return res;
        }

        /**
         * @brief From A to B have some tile in between, it is as close to be
         * a line as you wanted, it from A -> B for the tiles result. 
         *
         * @param A line from tileA
         * @param B to tileB
         */
        public List<Vector2> lineToTiles(Vector2 A, Vector2 B)
        {
            var lineTilesX = new List<Vector2>();

            var stepX = B.X - A.X;
            if (stepX > 0) stepX = 1;
            if (stepX < 0) stepX = -1;

            var stepY = B.Y - A.Y;
            if (stepY > 0) stepY = 1;
            if (stepY < 0) stepY = -1;

            if (A.X != B.X)
            {
                // We are sure that it work on xAxis now
                var line = (B.Y - A.Y) / (B.X - A.X);
                var constant = A.Y - line * A.X;

                var i = A.X;
                while (i != B.X)
                {
                    var xAxis = i;
                    var yAxis = line * i + constant;
                    lineTilesX.Add(new Vector2(xAxis, yAxis));
                    i += stepX;
                }
            }

            var lineTilesY = new List<Vector2>();
            if (A.Y != B.Y)
            {
                var line = (B.X - A.X) / (B.Y - A.Y);
                var constant = A.X - line * A.Y;

                var j = A.Y;
                while (j != B.Y)
                {
                    var yAxis = j;
                    var xAxis = line * j + constant;
                    lineTilesY.Add(new Vector2(xAxis, yAxis));
                    j += stepY;
                }
            }

            // Seeing who do the job better, which just mean there is more node
            if (lineTilesX.Count > lineTilesY.Count)
                return lineTilesX;

            return lineTilesY;
        }


        /**
         * @brief This scall thing back to tile base and check every movement
         * is valid or not, skip path expect to be call in when calculating the
         * next node in path to goes to
         */
        public void findBestNext()
        {
            // Only try to skip if we have some thing right
            if (this.path.Count == 1)
            {
                this.bestNext = this.path.Peek();
                return;
            }

            // So we can alway start at the player position (other wise we need
            // to use a general start point)
            // Normally we are expected to go to the next node in found path
            // Which are both real position (not really ideal, so we scale them
            // back down to tile position)
            Vector2 start = Game1.player.GetBoundingBox().Center.ToVector2();
            Vector2 startTile = getPlayerStandingTile();
            Vector2 next = this.path.Peek();


            // We expect to find a best node that direct movement from the player
            // location to that node (straight movement) is a valid movement
            // Which mean every tile along the way in between need to be a valid
            // movement
            float bestLength = Vector2.Distance(start, next);
            bestNext = this.path.Peek();

            foreach (var skipping in this.path)
            {
                var skippingTile = Util.toTile(skipping);
                // The line goes from skipping tile to start tile
                var lineTiles = lineToTiles(skippingTile, startTile);
                var isBlocked = false;
                for (var i = 0; i < lineTiles.Count; i += 1)
                {
                    var tile = Util.fixFragtionTile(lineTiles[i]);
                    var prev = startTile;
                    if (i > 0)
                        prev = Util.fixFragtionTile(lineTiles[i - 1]);
                    // But we going from start, so this need to walk backward
                    if (!isValidMovement(tile, prev))
                    {
                        isBlocked = true;
                        break;
                    }
                }

                if (isBlocked)
                    continue;

                if (bestLength < Vector2.Distance(start, skipping))
                {
                    bestLength = Vector2.Distance(start, skipping);
                    for (var i = 0; i < lineTiles.Count; i += 1)
                    {
                        var tile = Util.fixFragtionTile(lineTiles[i]);
                        // This show us back the short cut we found
                        if (debugLineToTiles)
                        {
                            this.lineToTileNodes.Clear();
                            var node = new DrawableNode(Util.toBoxPosition(tile));
                            var prev = startTile;
                            if (i > 0)
                                prev = Util.fixFragtionTile(lineTiles[i - 1]);

                            // But we going from start, so this need to walk backward
                            if (!isValidMovement(tile, prev))
                            {
                                node.color = Color.Red;
                            }

                            this.lineToTileNodes.Add(node);
                        }
                    }

                    bestNext = skipping;

                    if (this.debugVerbose)
                    {
                        this.Monitor.Log("Found our new best " + bestNext, LogLevel.Info);
                    }

                }
            }
        }

        public void skipingPath()
        {
            // Extra here so we not goes to inf loop
            var limit = 100;
            while (this.path.Peek() != bestNext)
            {
                if (this.debugVerbose)
                    this.Monitor.Log(String.Format("Skip {0}, we go to {1} driectly", this.path.Peek(), bestNext), LogLevel.Info);
                this.path.Pop();
                this.pathNodes.Pop();

                // How can this happend, we can't skip everything
                if (this.path is null)
                    break;
                // Limit should be enough
                limit -= 1;
                if (limit < 0)
                {
                    if (this.debugVerbose)
                        this.Monitor.Log("This seem stuck, we breakout", LogLevel.Info);
                    break;
                }
            }
        }

        public void addLineToPath(Vector2 startTile, Vector2 destinationTile)
        {
            // Re-add all imadiate point
            var bestLine = lineToTiles(bestNext, startTile);
            for (var i = 0; i < bestLine.Count; i++)
            {
                var tile = Util.fixFragtionTile(bestLine[i]);
                var position = this.addPadding(tile);
                this.path.Push(position);
                this.pathNodes.Push(new DrawableNode(this.addPadding(position)));
            }
        }

        /**
         * @brief This just give the next path if player has reach the current
         * path node
         */
        public Nullable<Vector2> nextPath()
        {
            if (this.path.Count == 0)
                return null;

            Vector2 start = Game1.player.GetBoundingBox().Center.ToVector2();

            // Seem suck flowing stupid path, we can use math and skip the most
            // of them
            if (doPathSkipping)
            {
                // When we reach the bestNext path
                if ((Vector2.Distance(start, bestNext) < this.microPositionDelta))
                {
                    if (debugLineToTiles)
                        this.lineToTileNodes.Clear();
                    // we start find the next one, or it might as well at the
                    // destination, we also haven't clear the current path yet
                    // so the top of stack should also be bestNext
                    findBestNext();
                    // and update the path imediately
                    skipingPath();
                }

                // The update also consider if we reach the next best path also
                // this will put the following path to the end
                if ((Vector2.Distance(start, bestNext) < this.microPositionDelta))
                {
                    if (this.debugVerbose)
                    {
                        this.Monitor.Log("Finish to follow skipping path", LogLevel.Info);
                    }
                    this.path.Pop();
                    this.pathNodes.Pop();
                }
                if (this.path.Count == 0)
                    return null;
                return this.path.Peek();
            }
            else
            {
                Vector2 next = this.path.Peek();

                if (Vector2.Distance(start, next) > this.microPositionDelta)
                    return this.path.Peek();
                this.path.Pop();
                this.pathNodes.Pop();
                return this.nextPath();
            }
        }

        public Vector2 moveDirection()
        {
            var optionalNext = this.nextPath();
            Vector2 next = this.destination;
            if (optionalNext is not null)
                next = optionalNext.Value;
            Vector2 player = Game1.player.GetBoundingBox().Center.ToVector2();
            Vector2 direction = Vector2.Subtract(next, player);

            // When destination is reach-able, and we found the path lead to it
            if (Util.isTilePassable(Util.toTile(this.destination)))
            {
                if (Util.toTile(this.destination) == this.destinationTile)
                {
                    // Which mean moving will end when we reach the destination tile
                    // which also mean we finish all node inside path
                    if (optionalNext is null)
                    {
                        if (this.debugVerbose)
                        {
                            this.Monitor.Log("Destination tile seem reachable, We found the right path too", LogLevel.Info);
                        }
                        return new Vector2(0, 0);
                    }
                }

                // We can't found path to it, though it worth just try to reach
                // destination after we finish all node inside path
                if (optionalNext is null)
                {
                    if (this.debugVerbose)
                    {
                        this.Monitor.Log("Destination tile seem unreachable, as we haven't found the path to it yet", LogLevel.Info);
                    }
                    return direction;
                }
            }

            // So if destination is unreachable, player will kept moving till
            // we got stuck colliding with the destination
            if (optionalNext is null)
            {
                if (!Game1.player.isColliding(Game1.player.currentLocation, Util.toTile(this.destination)))
                {
                    return direction;
                }
                else
                {
                    if (this.debugVerbose)
                    {
                        this.Monitor.Log("Destination tile is unreachable, but we have collided to it!", LogLevel.Info);
                    }
                    return new Vector2(0, 0);
                }
            }

            // We reach the end
            return direction;
        }

        public static Vector2 getPlayerStandingTile()
        {
            return Util.toTile(Game1.player.GetBoundingBox().Center.ToVector2());
        }
    }
}
