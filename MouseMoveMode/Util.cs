using StardewValley;
using System;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace MouseMoveMode
{
    class Util
    {
        private static HashSet<Vector2> cacheCantPassable = new HashSet<Vector2>();
        private static List<DrawableNode> nonPassableNodes = new List<DrawableNode>();

        public static void flushCache()
        {
            cacheCantPassable.Clear();
            nonPassableNodes.Clear();
        }

        public static void drawPassable(SpriteBatch b)
        {
            foreach (var node in nonPassableNodes)
            {
                node.draw(b, color: Color.Gray);
            }
        }

        /**
         * @brief This use tile (a scale down 1/64 from game true position)
         *
         * @param x tile value in X-axis
         * @param y tile value in Y-axis
         * @return if the current tile is passable
         */
        public static bool isTilePassable(float x, float y, bool useBetter = true)
        {
            return _isTilePassable(new Vector2(x, y), useBetter);
        }

        /**
         * @brief This use tile (a scale down 1/64 from game true position)
         *
         * @param tile value
         * @param useBetter (Optional) use new different way to check isTilePassable
         * @return if the current tile is passable
         */
        public static bool isTilePassable(Vector2 tile, bool useBetter = true)
        {
            return _isTilePassable(tile, useBetter);
        }

        /**
         * @brief Function routing
         */
        private static bool _isTilePassable(Vector2 tile, bool useBetter)
        {
            if (useBetter)
                return _isTilePassableBetter(tile);
            else
                return _isTilePassableOld(tile);
        }

        /**
         * @brief VonLoewe implementation for isTilePassable
         */
        private static bool _isTilePassableBetter(Vector2 tile)
        {
            const CollisionMask collisionMask = CollisionMask.Buildings | CollisionMask.Furniture | CollisionMask.Objects |
                                    CollisionMask.TerrainFeatures | CollisionMask.LocationSpecific;
            var l = Game1.player.currentLocation;
            if (l.isTilePassable(tile) && !l.IsTileOccupiedBy(tile, collisionMask))
                return true;
            nonPassableNodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
            return false;
        }

        /**
         * @brief My old implementation
         */
        private static bool _isTilePassableOld(Vector2 tile)
        {
            if (cacheCantPassable.Contains(tile))
            {
                return false;
            }

            GameLocation gl = Game1.player.currentLocation;
            if (!gl.isTilePassable(tile))
            {
                //this.Monitor.Log("Found unpassable tile from current location at " + tile, LogLevel.Info);
                cacheCantPassable.Add(tile);
                nonPassableNodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
                return false;
            }

            foreach (var item in gl.buildings)
            {
                Rectangle box = item.GetBoundingBox();
                if (!box.Contains(toBoxPosition(tile)))
                    continue;
                if (!item.isTilePassable(tile))
                {
                    cacheCantPassable.Add(tile);
                    nonPassableNodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
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
                //cacheCantPassable.Add(tile) = false;
                //nodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
                return false;
            }

            foreach (var item in gl.largeTerrainFeatures)
            {
                if (item.isPassable())
                {
                    continue;
                }
                if (item.getBoundingBox().Contains(toBoxPosition(tile)))
                {
                    continue;
                }
                //this.Monitor.Log("Found unpassable large terran feature " + item + " at " + tile, LogLevel.Info);
                cacheCantPassable.Add(tile);
                nonPassableNodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
                return false;
            }

            if (gl.Objects.ContainsKey(tile))
            {
                var item = gl.Objects[tile];
                if (!item.isPassable())
                {
                    // Object like stone etc should also consider breakable
                    //this.Monitor.Log("Found unpassable object" + item, LogLevel.Info);
                    //cacheIsPassable[tile] = false;
                    //nodes.Add(new DrawableNode(Util.toBoxPosition(tile)));
                    return false;
                }
            }

            return true;
        }

        /**
         * @brief Convert a tile position (128x128) to true game position, which is a multiple of 64 compare to original value
         *
         * @param tile position vector
         * @param paddingX (Optional) default to be the middle of the tile
         * @param paddingY (Optional) default to be the middle of the tile
         * @return true positional vector
         */
        public static Vector2 toPosition(Vector2 tile, float paddingX = 31f, float paddingY = 31f)
        {
            return new Vector2((float)Math.Round(tile.X * 64f) + paddingX, (float)Math.Round(tile.Y * 64f) + paddingY);
        }

        /**
         * @brief Convert a tile position (128x128) to true game position, which is a multiple of 64 compare to original value
         *
         * @param tile position vector
         * @return true positional vector
         */
        public static Rectangle toBoxPosition(Vector2 tile)
        {
            return new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
        }

        /**
         * @brief Convert a in-game position to a 128x128 tile map, which is a downscale of 64 times compare to original value
         *
         * @param position in true value
         * @return down scale tile position
         */
        public static Vector2 toTile(Vector2 position)
        {
            return new Vector2((float)Math.Round(position.X / 64f), (float)Math.Round(position.Y / 64f));
        }

        /**
         * @brief Convert a in-game rectangle into a list of tile on 128x128 block map
         *
         * @param box usually a boundingBox or renderBox of in-game object
         * @return list of tile corresponed to the input
         */
        public static List<Vector2> toTiles(Rectangle box)
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

    }
}
