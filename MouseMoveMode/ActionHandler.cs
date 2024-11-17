using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace MouseMoveMode
{
    interface IActionHandler
    {
        public void cancelAction();
        public void updateTarget(Vector2 target);
        public bool tryDoAction();
        public void debugDoAction(Vector2 target);
        public string toString();
    }

    class ActionHandlerNew : IActionHandler
    {
        public Vector2 target;
        public NPC targetNPC = null;

        public bool isActionableAtDesinationTile = false;
        public bool isDone = false;

        public void debugDoAction(Vector2 target)
        {
            updateTarget(target);
            ModEntry.getMonitor().Log(String.Format("Try do action at tile {0}", Util.toTile(target)), LogLevel.Info);
            ModEntry.getMonitor().Log(String.Format("Full context: {0}", this.toString()), LogLevel.Trace);
            tryDoAction();

            if (!this.isActionableAtDesinationTile)
                return;
            DecompiliedGame1.pressActionButtonMod(Util.toTile(target), forceNonDirectedTile: true);
            this.isActionableAtDesinationTile = false;
            cancelAction();
        }

        public void cancelAction()
        {
            this.isActionableAtDesinationTile = false;
        }

        public void updateTarget(Vector2 target)
        {
            this.target = target;
            this.targetNPC = Game1.player.currentLocation.isCharacterAtTile(Util.toTile(target));
        }

        public bool tryDoAction()
        {
            if (!this.isActionableAtDesinationTile)
                return false;

            // Do action
            var grabTile = Util.toTile(this.target);
            // Maybe we need dismount right
            if (Game1.player.isRidingHorse())
            {
                ModEntry.getMonitor().Log(String.Format("Try do as riding horse", Util.toTile(target)), LogLevel.Info);
                if (Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 2, Game1.player))
                    Game1.player.mount.dismount();
            }

            // Try to check grap tile when player is close enough
            if (Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                ModEntry.getMonitor().Log(String.Format("Try do as within range", Util.toTile(target)), LogLevel.Info);
                TryToCheckGrapTile();
            }

            return false;
        }

        public void TryToCheckGrapTile()
        {
            var grabTile = this.target;
            // This overide all other action interaction
            if (this.targetNPC is not null)
            {
                if (ModEntry.isDebugVerbose)
                    ModEntry.getMonitor().Log(String.Format("Try check NPC {0} at tile {1}", this.targetNPC, grabTile), LogLevel.Info);
                // This updating grabTile as NPC could already moved
                grabTile = this.targetNPC.Tile;
                bool isNPCChecked = Game1.player.currentLocation.checkAction(new xTile.Dimensions.Location((int)grabTile.X, (int)grabTile.Y), Game1.viewport, Game1.player);
                if (isNPCChecked)
                {
                    if (ModEntry.isDebugVerbose)
                        ModEntry.getMonitor().Log(String.Format("Success check NPC {0} at tile {1}", this.targetNPC, grabTile), LogLevel.Info);
                    this.isActionableAtDesinationTile = false;
                }
                // This overide all other behavior
                return;
            }

            // Try to place the item next, It have higher piority
            if (Game1.player.ActiveObject is not null)
                if (isActionableAtDesinationTile && Game1.player.ActiveObject.isPlaceable() && Game1.player.currentLocation.CanItemBePlacedHere(grabTile))
                {
                    if (ModEntry.isDebugVerbose)
                        ModEntry.getMonitor().Log(String.Format("Try placing item at tile {0}", grabTile), LogLevel.Info);
                    var isPlaced = Utility.tryToPlaceItem(Game1.player.currentLocation, Game1.player.ActiveObject, (int)grabTile.X * 64, (int)grabTile.Y * 64);
                    if (isPlaced)
                    {
                        if (ModEntry.isDebugVerbose)
                            ModEntry.getMonitor().Log(String.Format("Success placing item at tile {0}", grabTile), LogLevel.Info);
                        this.isActionableAtDesinationTile = false;
                        return;
                    }
                }

            var gl = Game1.player.currentLocation;
            var funiture = gl.GetFurnitureAt(grabTile);
            if (funiture is not null)
            {
                if (funiture.isActionable(Game1.player))
                {
                    var isFunitureChecked = funiture.checkForAction(Game1.player);
                    if (isFunitureChecked)
                    {
                        if (ModEntry.isDebugVerbose)
                            ModEntry.getMonitor().Log(String.Format("Success checked funiture at tile {0}", grabTile), LogLevel.Info);
                        this.isActionableAtDesinationTile = false;
                        return;
                    }
                }
            }

            var isChecked = !DecompiliedGame1.pressActionButtonMod(grabTile);
            if (isChecked)
            {
                if (ModEntry.isDebugVerbose)
                    ModEntry.getMonitor().Log(String.Format("Success checked item at tile {0}", grabTile), LogLevel.Info);
                this.isActionableAtDesinationTile = false;
            }
        }

        public string toString()
        {
            throw new NotImplementedException();
        }
    }

    class ActionHandlerOld : IActionHandler
    {
        public Vector2 target;
        public NPC targetNPC = null;
        public int actionToolIndex = -1;
        public StardewValley.Object targetObject = null;
        public int isTryToDoActionAtClickedTitle = 0;

        public ActionHandlerOld()
        {
        }

        public void debugDoAction(Vector2 target)
        {
            updateTarget(target);
            ModEntry.getMonitor().Log(String.Format("Try do action at tile {0}", Util.toTile(target)), LogLevel.Info);
            ModEntry.getMonitor().Log(String.Format("Full context: {0}", this.toString()), LogLevel.Trace);
            tryDoAction();
            cancelAction();
        }

        public bool tryDoAction()
        {
            if (isTryToDoActionAtClickedTitle == 0)
                return false;

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
                isTryToDoActionAtClickedTitle = 0;
                //isMovingAutomaticaly = false;
                return true;
            }

            if (isTryToDoActionAtClickedTitle == 1 && this.targetObject != null && (Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player) || Utility.tileWithinRadiusOfPlayer((int)targetObject.TileLocation.X, (int)targetObject.TileLocation.Y, 1, Game1.player)))
            {
                if (this.targetObject.checkForAction(Game1.player))
                {
                    ModEntry.getMonitor().Log(String.Format("Check object directly instead {0}", targetObject.TileLocation));
                    isTryToDoActionAtClickedTitle = 0;
                    //isMovingAutomaticaly = false;
                    return true;
                }
                else
                {
                    if (this.targetObject.name == "Chest")
                    {
                        ModEntry.getMonitor().Log(String.Format("Got Chest type at {0}, we do special handling", targetObject.TileLocation));
                        StardewValley.Objects.Chest chest = (StardewValley.Objects.Chest)this.targetObject;
                        if (chest.playerChest.Value)
                        {
                            chest.GetMutex().RequestLock(delegate
                            {
                                if (chest.SpecialChestType == StardewValley.Objects.Chest.SpecialChestTypes.MiniShippingBin)
                                {
                                    chest.OpenMiniShippingMenu();
                                }
                                else
                                {
                                    chest.frameCounter.Value = 5;
                                    Game1.playSound(chest.fridge.Value ? "doorCreak" : "openChest");
                                    Game1.player.Halt();
                                    Game1.player.freezePause = 1000;
                                }
                            });
                            isTryToDoActionAtClickedTitle = 0;
                            return true;
                        }
                    }
                }
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
                return true;
            }

            if (isTryToDoActionAtClickedTitle == 3 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                int stack = Game1.player.ActiveObject.Stack;
                Utility.tryToPlaceItem(Game1.player.currentLocation, Game1.player.ActiveObject, (int)grabTile.X * 64 + 32, (int)grabTile.Y * 64 + 32);
                if (Game1.player.ActiveObject == null || Game1.player.ActiveObject.Stack < stack || Game1.player.ActiveObject.isPlaceable())
                {
                    isTryToDoActionAtClickedTitle = 0;
                    // isMovingAutomaticaly=false;
                    return true;
                }
            }

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                isTryToDoActionAtClickedTitle = 0;
                return Game1.tryToCheckAt(grabTile, Game1.player);
            }

            if (isTryToDoActionAtClickedTitle == 5 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                isTryToDoActionAtClickedTitle = 0;
                if (!Game1.player.isRidingHorse())
                    return Game1.tryToCheckAt(grabTile, Game1.player);
            }

            return false;
        }

        public void cancelAction()
        {
            isTryToDoActionAtClickedTitle = 0;
        }

        public void updateTarget(Vector2 target)
        {
            this.target = target;
            isTryToDoActionAtClickedTitle = GetActionType(Util.toTile(this.target));
        }

        private int GetActionType(Vector2 grabTile)
        {
            // There is 5 type:
            StardewValley.Object pointedObject = Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y);
            if (pointedObject == null && Game1.player.currentLocation.Objects.ContainsKey(grabTile))
            {
                pointedObject = Game1.player.currentLocation.Objects[grabTile];
            }
            targetNPC = null;
            targetObject = null;
            actionToolIndex = -1;

            // 1 is for Object is 1x1 tile size but with 2x1 hit box (Chess, ...)
            if (pointedObject == null && Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y + 1) != null)
            {
                pointedObject = Game1.player.currentLocation.getObjectAtTile((int)grabTile.X, (int)grabTile.Y + 1);
            }

            if (pointedObject == null)
            {
                if (Game1.player.currentLocation.Objects.ContainsKey(grabTile + Vector2.UnitY))
                    pointedObject = Game1.player.currentLocation.Objects[grabTile];
            }

            if (pointedObject != null && pointedObject.isActionable(Game1.player))
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

        public string toString()
        {
            return String.Format("targetObject:{0}, target:{1}, isTryToDoActionAtClickedTitle:{2}, targetNPC:{3}, actionToolIndex:{4}", this.targetObject, this.target, this.isTryToDoActionAtClickedTitle, this.targetNPC, this.actionToolIndex);
        }
    }
}
