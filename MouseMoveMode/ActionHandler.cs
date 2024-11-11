using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace MouseMoveMode
{
    class ActionHandler
    {
        public Vector2 target;
        public bool isDone = false;

        public void UpdateTarget(Vector2 target)
        {
            this.target = target;
        }

        public void doAction()
        {
            // Do action
        }

        public bool isTargetInActionableRange()
        {
            // Check ActionableRange
            return true;
        }
    }

    class ActionHandlerOld
    {
        public Vector2 target;
        public NPC targetNPC = null;
        public int actionToolIndex = -1;
        public StardewValley.Object targetObject = null;
        public int isTryToDoActionAtClickedTitle = 0;

        public ActionHandlerOld()
        {
        }

        public void tryDoAction()
        {
            if (isTryToDoActionAtClickedTitle == 0)
                return;

            var grabTile = Util.toTile(this.target);
            if (Game1.player.isRidingHorse())
            {
                if ((isTryToDoActionAtClickedTitle == 2) && Utility.tileWithinRadiusOfPlayer(targetNPC.TilePoint.X, targetNPC.TilePoint.Y, 2, Game1.player))
                    Game1.player.mount.dismount();
                else if (isTryToDoActionAtClickedTitle != 0 && isTryToDoActionAtClickedTitle != 5 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 2, Game1.player))
                    Game1.player.mount.dismount();
            }

            if (isTryToDoActionAtClickedTitle == 1 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player) && Game1.tryToCheckAt(grabTile, Game1.player))
            {
                ModEntry.getMonitor().Log("Check 1");
                isTryToDoActionAtClickedTitle = 0;
                //isMovingAutomaticaly = false;
            }

            if ((isTryToDoActionAtClickedTitle == 2 || isTryToDoActionAtClickedTitle == 3) && (Game1.player.CurrentToolIndex != actionToolIndex))
            {
                isTryToDoActionAtClickedTitle = 0;
            }

            if (isTryToDoActionAtClickedTitle == 3 && (Game1.player.ActiveObject == null))
            {
                isTryToDoActionAtClickedTitle = 0;
            }

            if (isTryToDoActionAtClickedTitle == 2 && Utility.tileWithinRadiusOfPlayer(targetNPC.TilePoint.X, targetNPC.TilePoint.Y, 1, Game1.player) && Game1.tryToCheckAt(targetNPC.Tile, Game1.player))
            {
                isTryToDoActionAtClickedTitle = 0;
                //isMovingAutomaticaly = false;
            }

            if (isTryToDoActionAtClickedTitle == 3 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                int stack = Game1.player.ActiveObject.Stack;
                Utility.tryToPlaceItem(Game1.player.currentLocation, Game1.player.ActiveObject, (int)grabTile.X * 64 + 32, (int)grabTile.Y * 64 + 32);
                if (Game1.player.ActiveObject == null || Game1.player.ActiveObject.Stack < stack || Game1.player.ActiveObject.isPlaceable())
                {
                    isTryToDoActionAtClickedTitle = 0;
                    // isMovingAutomaticaly=false;
                }
            }

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                Game1.tryToCheckAt(grabTile, Game1.player);
                isTryToDoActionAtClickedTitle = 0;
            }

            if (isTryToDoActionAtClickedTitle == 5 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                if (!Game1.player.isRidingHorse())
                    Game1.tryToCheckAt(grabTile, Game1.player);
                isTryToDoActionAtClickedTitle = 0;
            }
        }

        public void cancelAction()
        {
            isTryToDoActionAtClickedTitle = 0;
        }

        public void updateTarget(Vector2 target)
        {
            this.target = target;
            isTryToDoActionAtClickedTitle = GetActionType(ref this.target);
        }

        private int GetActionType(ref Vector2 grabTile)
        {
            // There is 5 type:
            StardewValley.Object pointedObject = Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y);
            targetNPC = null;
            targetObject = null;
            actionToolIndex = -1;

            // 1 is for Object is 1x1 tile size but with 2x1 hit box (Chess, ...)
            if (pointedObject == null && Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y + 1) != null)
            {
                grabTile.Y += 1;
                pointedObject = Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y);
                targetObject = pointedObject;
            }

            if (pointedObject != null && pointedObject.Type != null && (pointedObject.IsSpawnedObject || (pointedObject.Type.Equals("Crafting")
                && pointedObject.Type.Equals("interactive"))))
            {
                targetObject = pointedObject;
                return 1;
            }

            // 2 is for NPC
            var pointedNPC = Game1.player.currentLocation.isCharacterAtTile(grabTile);
            if (pointedNPC == null)
                pointedNPC = Game1.player.currentLocation.isCharacterAtTile(grabTile + new Vector2(0f, 1f));

            if (pointedNPC != null && !pointedNPC.IsMonster)
            {
                actionToolIndex = Game1.player.CurrentToolIndex;
                targetNPC = pointedNPC;
                return 2;
            }

            // 3 to handle Fence, Seed, ... thaat placeable
            if (Game1.player.ActiveObject != null && Game1.player.ActiveObject.isPlaceable())
            {
                actionToolIndex = Game1.player.CurrentToolIndex;
                return 3;
            }

            // 4 to handle terrainFeatures (some has hitbox that unreachable and have to change)
            foreach (var v in Game1.player.currentLocation.terrainFeatures.Pairs)
            {
                if (v.Value.getBoundingBox().Intersects(new Rectangle((int)grabTile.X * 64, (int)grabTile.Y * 64, 64, 64)))
                {
                    if ((v.Value is Grass) || (v.Value is HoeDirt dirt && !dirt.readyForHarvest()))
                    { }
                    else
                        return 4;
                }
            }

            if (Game1.player.currentLocation.largeTerrainFeatures != null)
            {
                foreach (var f in Game1.player.currentLocation.largeTerrainFeatures)
                {
                    if (f.getBoundingBox().Intersects(new Rectangle((int)grabTile.X * 64, (int)grabTile.Y * 64, 64, 64)))
                    {
                        return 4;
                    }
                }
            }

            if (Game1.isActionAtCurrentCursorTile || Game1.isInspectionAtCurrentCursorTile)
            {
                if (!Game1.currentLocation.isActionableTile((int)grabTile.X, (int)grabTile.Y, Game1.player))
                    if (Game1.currentLocation.isActionableTile((int)grabTile.X, (int)grabTile.Y + 1, Game1.player))
                        grabTile.Y += 1;
                return 1;
            }

            // 5 is Unknown, try to grap at pointed place 
            return 5;
        }
    }
}
