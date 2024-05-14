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

        public static void drawBox(SpriteBatch b, Vector2 posistion)
        {
            var texture2D = Game1.mouseCursors;
            var position = Game1.GlobalToLocal(Game1.viewport, posistion);
            var sourceRectangle = new Rectangle(194, 388, 16, 16);
            var color = Color.White;
            var rotation = 0f;
            var origin = new Vector2(1f, 4f);
            var scale = 1.2f;
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
        private List<Node> nodes;
        private bool[,] isPassable = new bool[128, 128];

        private IMonitor Monitor;

        private PriorityQueue<Vector2, float> pq;
        private HashSet<Vector2> visited;
        private Dictionary<Vector2, Vector2> cameFrom;
        public Vector2 destination;

        private Stack<Vector2> path;

        private int step;

        public PathFindingHelper(IMonitor im)
        {
            this.nodes = new List<Node>();
            this.Monitor = im;
        }

        public void drawThing(SpriteBatch b)
        {
            foreach (var item in nodes)
            {
                item.draw(b);
            }
            if (this.path is not null)
            {
                foreach (var node in this.path)
                {
                    DrawHelper.drawBox(b, new Rectangle((int)node.X * 64, (int)node.Y * 64, 64, 64));
                }
            }
        }

        public void addNode(Rectangle box)
        {
            var node = new Node(box);
            this.Monitor.Log(String.Format("Manual: {0}", node), LogLevel.Info);
            nodes.Add(node);
        }

        public void loadMap()
        {
            nodes.Clear();
            GameLocation gl = Game1.player.currentLocation;
            for (int i = 0; i < 128; i += 1)
                for (int j = 0; j < 128; j += 1)
                {
                    isPassable[i, j] = gl.isTilePassable(new Vector2(i, j));
                    if (!isPassable[i, j])
                    {
                        nodes.Add(new Node(i * 64, j * 64, 64, 64));
                    }
                }

            foreach (var item in gl.buildings)
            {
                Rectangle box = item.GetBoundingBox();
                var node = new Node(box);
                this.Monitor.Log(String.Format("{0}:{1}", item, node), LogLevel.Info);
                nodes.Add(node);
            }
        }

        public Vector2 toTile(Vector2 position)
        {
            return new Vector2((float)Math.Round(position.X/64f), (float)Math.Round(position.Y/64f));
        }

        public Vector2 changeDes(Vector2 destination)
        {
            GameLocation gl = Game1.player.currentLocation;
            this.destination = destination;
            var destinationTile = toTile(this.destination);

            this.visited = new HashSet<Vector2>();

            this.pq = new PriorityQueue<Vector2, float>();
            this.pq.Enqueue(Game1.player.Tile, 0);

            this.cameFrom = new Dictionary<Vector2, Vector2>();
            var gScore = new Dictionary<Vector2, float>();
            var fScore = new Dictionary<Vector2, float>();

            Vector2 start = Game1.player.Tile;
            Vector2 bestScoreTile = start;
            float bestScore = Vector2.Distance(start, destinationTile);

            gScore.Add(start, 0);
            fScore.Add(start, Vector2.Distance(start, destinationTile));

            while (pq.Count > 0)
            {
                var current = pq.Dequeue();
                if (Vector2.Distance(current, destinationTile) < 1f)
                {
                    return updatePath();
                }

                for (int i = -1; i <= 1; i += 1)
                    for (int j = -1; j <= 1; j += 1)
                    {
                        Vector2 neighbor = current + new Vector2(i, j);
                        if ((i == 0 && j == 0) || !gl.isTilePassable(neighbor))
                            continue;

                        var temp = gScore[current] + Vector2.Distance(current, neighbor);
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
                                fScore[neighbor] = temp + Vector2.Distance(neighbor, destinationTile);
                            }
                        }
                        else
                        {
                            this.cameFrom.Add(neighbor, current);
                            gScore.Add(neighbor, temp);
                            fScore.Add(neighbor, temp + Vector2.Distance(neighbor, destinationTile));
                        }
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            pq.Enqueue(neighbor, fScore[neighbor]);
                            if (bestScore > temp + Vector2.Distance(neighbor, destinationTile))
                            {
                                bestScore = temp + Vector2.Distance(neighbor, destinationTile);
                                bestScoreTile = neighbor;
                            }
                        }
                    }
            }

            this.destination = new Vector2(bestScoreTile.X * 64, bestScoreTile.Y * 64);
            return updatePath();
        }

        public Vector2 updatePath()
        {
            this.path = new Stack<Vector2>();
            Vector2 desTile = toTile(this.destination);
            Vector2 start = Game1.player.Tile;
            Vector2 p = desTile;
            while (Vector2.Distance(this.cameFrom[p], start) > 1f)
            {
                this.path.Push(p);
                p = this.cameFrom[p];
            };
            return this.path.Peek();
        }

        public Vector2 nextPath()
        {
            Vector2 start = toTile(Game1.player.Tile);
            Vector2 p = this.path.Peek();
            if (Vector2.Distance(p, start) > 1f)
            {
                return this.path.Peek();
            }
            this.path.Pop();
            return this.path.Peek();
        }
    }
}
