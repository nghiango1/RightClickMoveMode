using StardewValley;
using StardewModdingAPI;
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MouseMoveMode
{
    class DrawHelper
    {
        /**
         * @brief Draw a box in the current game location
         *
         * @param b can access via Event Rendered input `e.SpriteBatch`
         * @param boxPosistion A rectangle tobe draw on screen
         */
        public static void drawRedBox(SpriteBatch b, Rectangle boxPosistion)
        {
            // This have full texture2D
            var texture2D = Game1.mouseCursors;
            var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(boxPosistion.X, boxPosistion.Y));
            // The size of rectangle that will contain the sprite texture for green tilte appreared when you
            // powering the tool
            var sourceRectangle = new Rectangle(194, 388, 16, 16);
            var color = Color.Red;
            var rotation = 0f;
            // Start at top-left
            var origin = new Vector2(0f, 0f);
            var scale = new Vector2(boxPosistion.Width / 64f * 4f, boxPosistion.Height / 64f * 4f);
            var effect = SpriteEffects.None;
            // Same layer with tool effective range indicator (green tilte appeared when you powering the tool)
            var layerDepth = 0.01f;

            b.Draw(texture2D, position, sourceRectangle, color, rotation, origin, scale, effect, layerDepth);
        }

        /**
         * @brief Draw a mini box point in the current game location
         *
         * @param b can access via Event Rendered input `e.SpriteBatch`
         * @param posistion X,Y point to be draw on screen
         */
        public static void drawBox(SpriteBatch b, Vector2 posistion)
        {
            var texture2D = Game1.mouseCursors;
            var position = Game1.GlobalToLocal(Game1.viewport, posistion);
            var sourceRectangle = new Rectangle(194, 388, 16, 16);
            var color = Color.White;
            var rotation = 0f;
            var origin = new Vector2(1f, 4f);
            var scale = 1.5f;
            var effect = SpriteEffects.None;
            // Same layer with tool effective range indicator (green tilte appeared when you powering the tool)
            var layerDepth = 0.01f;

            b.Draw(texture2D, position, sourceRectangle, color, rotation, origin, scale, effect, layerDepth);
        }

        /**
         * @brief Draw a mini box point in the current game location
         *
         * @param b can access via Event Rendered input `e.SpriteBatch`
         * @param posistion X,Y point to be draw on screen
         */
        public static void drawBox(SpriteBatch b, Vector2 posistion, Color color)
        {
            var texture2D = Game1.mouseCursors;
            var position = Game1.GlobalToLocal(Game1.viewport, posistion);
            var sourceRectangle = new Rectangle(194, 388, 16, 16);
            var rotation = 0f;
            var origin = new Vector2(1f, 4f);
            var scale = 1.5f;
            var effect = SpriteEffects.None;
            // Same layer with tool effective range indicator (green tilte appeared when you powering the tool)
            var layerDepth = 0.01f;

            b.Draw(texture2D, position, sourceRectangle, color, rotation, origin, scale, effect, layerDepth);
        }

        /**
         * @brief This allow drawing box exactly along the rectangle position
         *
         * @param b 
         * @param boxPosistion 
         * @return 
         */
        public static void drawBox(SpriteBatch b, Rectangle boxPosistion)
        {
            // This have full texture2D
            var texture2D = Game1.mouseCursors;
            var position = Game1.GlobalToLocal(Game1.viewport, new Vector2(boxPosistion.X, boxPosistion.Y));
            // The size of rectangle that will contain the sprite texture for green tilte appreared when you
            // powering the tool
            var sourceRectangle = new Rectangle(194, 388, 16, 16);
            var color = Color.White;
            var rotation = 0f;
            // Start at top-left
            var origin = new Vector2(0f, 0f);
            var scale = new Vector2(boxPosistion.Width / 64f * 4f, boxPosistion.Height / 64f * 4f);
            var effect = SpriteEffects.None;
            // Same layer with tool effective range indicator (green tilte appeared when you powering the tool)
            var layerDepth = 0.01f;

            b.Draw(texture2D, position, sourceRectangle, color, rotation, origin, scale, effect, layerDepth);
        }
    }

    /**
     * @brief This contain a set of rectangle that can be print to screen
     */
    class Node
    {
        public Rectangle boundingBox;

        public Node(Rectangle box)
        {
            this.boundingBox = box;
        }

        public Node(int x, int y, int width, int height)
        {
            this.boundingBox = new Rectangle(x, y, width, height);
        }

        public void draw(SpriteBatch b)
        {
            DrawHelper.drawRedBox(b, this.boundingBox);
        }

        public override String ToString()
        {
            var x = this.boundingBox.X;
            var y = this.boundingBox.Y;
            var w = this.boundingBox.Width;
            var h = this.boundingBox.Height;
            return String.Format("x: {0}, y: {1}, w: {2}, h: {3}", x, y, w, h);
        }
    }

    class PathFindingHelper
    {
        private List<Node> nodes = new List<Node>();

        private IMonitor Monitor;
        private float microDelta = 0.0001f;
        private Dictionary<Vector2, bool> cacheIsPassable = new Dictionary<Vector2, bool>();

        private PriorityQueue<Vector2, float> pq = new PriorityQueue<Vector2, float>();
        private HashSet<Vector2> visited = new HashSet<Vector2>();
        private Dictionary<Vector2, Vector2> cameFrom = new Dictionary<Vector2, Vector2>();
        public Vector2 destination;
        private Vector2 destinationTile;
        private Stack<Vector2> path;

        private int step;

        public PathFindingHelper(IMonitor im)
        {
            this.Monitor = im;
        }

        public void drawThing(SpriteBatch b)
        {
            //foreach (var node in this.nodes)
            //{
            //    node.draw(b);
            //}
            //foreach (var cached in this.cacheIsPassable)
            //{
            //    if (cached.Value)
            //    {
            //        continue;
            //    }
            //    DrawHelper.drawRedBox(b, new Rectangle((int)cached.Key.X * 64, (int)cached.Key.Y * 64, 64, 64));
            //}

            if (this.path is not null)
            {
                foreach (var node in this.path)
                {
                    DrawHelper.drawBox(b, toPosition(node));
                }
            }
        }

        public void addNode(Rectangle box)
        {
            var node = new Node(box);
            this.Monitor.Log(String.Format("Manual: {0}", node), LogLevel.Info);
            nodes.Add(node);
        }

        /**
         * @brief This use tile (a scale down 1/64 from game true position)
         *
         * @param x tile value in X-axis
         * @param y tile value in Y-axis
         * @return if the current tile is passable
         */
        public bool isTilePassable(float x, float y)
        {
            return isTilePassable(new Vector2(x, y));
        }

        public bool isTilePassable(Vector2 tile)
        {
            if (this.cacheIsPassable.ContainsKey(tile))
            {
                return this.cacheIsPassable[tile];
            }

            GameLocation gl = Game1.player.currentLocation;
            if (!gl.isTilePassable(tile))
            {
                //this.Monitor.Log("Found unpassable tile from current location at " + tile, LogLevel.Info);
                this.cacheIsPassable[tile] = false;
                return false;
            }

            foreach (var item in gl.buildings)
            {
                Rectangle box = item.GetBoundingBox();
                if (!item.isTilePassable(tile))
                {
                    this.cacheIsPassable[tile] = false;
                    //this.Monitor.Log("Found unpassable building " + item + " at tile " + tile, LogLevel.Info);
                    return false;
                }
            }

            foreach (var items in gl.terrainFeatures)
            {
                if (!items.ContainsKey(tile))
                {
                    continue;
                }
                if (items[tile].isPassable())
                {
                    continue;
                }
                //Tree can be cutdown, thus caching should not be consider
                //this.Monitor.Log("Found unpassable terrain feature " + items[tile], LogLevel.Info);
                //this.cacheIsPassable[tile] = false;
                return false;
            }

            foreach (var item in gl.largeTerrainFeatures)
            {
                if (item.isPassable())
                {
                    continue;
                }
                if (!toTiles(item.getBoundingBox()).Contains(tile))
                {
                    continue;
                }
                //this.Monitor.Log("Found unpassable large terran feature " + item + " at " + tile, LogLevel.Info);
                this.cacheIsPassable[tile] = false;
                return false;
            }

            if (gl.Objects.ContainsKey(tile))
            {
                var item = gl.Objects[tile];
                if (!item.isPassable())
                {
                    // Object like stone etc should also consider breakable
                    //this.Monitor.Log("Found unpassable object" + item, LogLevel.Info);
                    //this.cacheIsPassable[tile] = false;
                    return false;
                }
            }


            this.cacheIsPassable[tile] = true;
            return true;
        }

        public void loadMap()
        {
            this.nodes.Clear();
            this.cacheIsPassable.Clear();
        }

        public Vector2 toPosition(Vector2 tile)
        {
            return new Vector2((float)Math.Round(tile.X * 64f) + 31f, (float)Math.Round(tile.Y * 64f) + 31f);
        }

        public Vector2 toTile(Vector2 position)
        {
            return new Vector2((float)Math.Round(position.X / 64f), (float)Math.Round(position.Y / 64f));
        }

        public List<Vector2> toTiles(Rectangle box)
        {
            var res = new List<Vector2>();
            var x = (int)Math.Round(box.X / 64f);
            var y = (int)Math.Round(box.Y / 64f);
            var w = (int)Math.Round(box.Width / 64f);
            var h = (int)Math.Round(box.Height / 64f);
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++)
                {
                    res.Add(new Vector2(i, j));
                }
            return res;
        }

        public Vector2 changeDes(Vector2 destination)
        {
            GameLocation gl = Game1.player.currentLocation;
            this.destination = destination;
            this.destinationTile = toTile(this.destination);

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
                if (Vector2.Distance(this.toPosition(current), this.destination) < 33f)
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
                        if ((i == 0 && j == 0) || !this.isTilePassable(neighbor) || visited.Contains(neighbor))
                            continue;
                        // diagonal special case handling, just don't do it if there is a blockage
                        if (Vector2.Distance(current, neighbor) > 1.2f)
                        {
                            if (!this.isTilePassable(current.X, neighbor.Y) || !this.isTilePassable(neighbor.X, current.Y))
                            {
                                continue;
                            }
                        }

                        visited.Add(neighbor);
                        this.nodes.Add(new Node((int)neighbor.X * 64, (int)neighbor.Y * 64, 64, 64));
                        var temp = gScore[current] + Vector2.Distance(this.toPosition(current), this.toPosition(neighbor));
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
                                fScore[neighbor] = temp + (float)Math.Pow(Vector2.Distance(this.toPosition(neighbor), this.destination), 2);
                            }
                        }
                        else
                        {
                            this.cameFrom.Add(neighbor, current);
                            gScore.Add(neighbor, temp);
                            fScore.Add(neighbor, temp + (float)Math.Pow(Vector2.Distance(this.toPosition(neighbor), this.destination), 2));
                        }

                        pq.Enqueue(neighbor, fScore[neighbor]);
                        if (bestScore > Vector2.Distance(this.toPosition(neighbor), this.destination))
                        {
                            bestScore = Vector2.Distance(this.toPosition(neighbor), this.destination);
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
                    return this.toPosition(p);
                }
                this.path.Pop();
                return nextPath();
            }
            else
                return this.destination;
        }
    }
}
