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

    class ActionHandler : IActionHandler
    {
        public bool isVerbose = false;
        public Vector2 target;
        public NPC targetNPC = null;
        public int actionToolIndex = -1;
        public StardewValley.Object targetObject = null;
        StardewValley.Buildings.Building targetBuilding = null;
        StardewValley.Buildings.ShippingBin targetShippingBin = null;
        StardewValley.TerrainFeatures.TerrainFeature targetTerrainFeatures = null;
        StardewValley.TerrainFeatures.LargeTerrainFeature targetLargeTerrainFeatures = null;
        public int isTryToDoActionAtClickedTitle = 0;

        public ActionHandler(bool isVerbose = false)
        {
            this.isVerbose = isVerbose;
        }

        public void debugDoAction(Vector2 target)
        {
            updateTarget(target);
            if (this.isVerbose)
            {
                ModEntry.getMonitor().Log(String.Format("Try do action at tile {0}, full context: {1}", Util.toTile(target), this.toString()), LogLevel.Trace);
            }
            tryDoAction();
            cancelAction();
        }

        private void shipItem(Item i, Farmer who)
        {
            if (i != null)
            {
                who.removeItemFromInventory(i);
                Game1.getFarm().getShippingBin(who).Add(i);
                this.targetShippingBin.showShipment(i, playThrowSound: false);
                Game1.getFarm().lastItemShipped = i;
                if (Game1.player.ActiveItem == null)
                {
                    Game1.player.showNotCarrying();
                    Game1.player.Halt();
                }
            }
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
                    if (this.isVerbose)
                        ModEntry.getMonitor().Log(String.Format("Check object directly instead {0}", targetObject.TileLocation));
                    isTryToDoActionAtClickedTitle = 0;
                    //isMovingAutomaticaly = false;
                    return true;
                }
                else
                {
                    if (this.targetObject.name == "Chest")
                    {
                        if (this.isVerbose)
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

                if (this.isVerbose)
                    ModEntry.getMonitor().Log(String.Format("Got Special object that return false when try to check!"));
                isTryToDoActionAtClickedTitle = 0;
                //isMovingAutomaticaly = false;
                return true;
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

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player) && targetTerrainFeatures != null && targetTerrainFeatures.performUseAction(grabTile))
            {
                if (this.isVerbose)
                    ModEntry.getMonitor().Log(String.Format("Got TerrainFeatures type: {0}, we do special handling at grapTile: {1}", targetTerrainFeatures, grabTile));
                isTryToDoActionAtClickedTitle = 0;
                return true;
            }

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player) && targetLargeTerrainFeatures != null && targetLargeTerrainFeatures.performUseAction(grabTile))
            {
                if (this.isVerbose)
                    ModEntry.getMonitor().Log(String.Format("Got LargeTerrainFeatures type: {0}, we do special handling at grapTile: {1}", targetLargeTerrainFeatures, grabTile));
                isTryToDoActionAtClickedTitle = 0;
                return true;
            }

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player) && targetBuilding != null)
            {
                if (targetBuilding.doAction(grabTile, Game1.player))
                {
                    if (this.isVerbose)
                        ModEntry.getMonitor().Log(String.Format("Got Building type: {0}, we do special handling at grapTile: {1}", targetBuilding, grabTile));
                    isTryToDoActionAtClickedTitle = 0;
                    return true;
                }

                // Again special handling for ShippingBin
                if (targetBuilding is StardewValley.Buildings.ShippingBin)
                {
                    targetShippingBin = (StardewValley.Buildings.ShippingBin)targetBuilding;
                    StardewValley.Buildings.ShippingBin bin = (StardewValley.Buildings.ShippingBin)targetBuilding;
                    StardewValley.Menus.ItemGrabMenu itemGrabMenu = new StardewValley.Menus.ItemGrabMenu(null, reverseGrab: true, showReceivingMenu: false, Utility.highlightShippableObjects, shipItem, "", null, snapToBottom: true, canBeExitedWithKey: true, playRightClickSound: false, allowRightClick: true, showOrganizeButton: false, 0, null, -1, this);
                    itemGrabMenu.initializeUpperRightCloseButton();
                    itemGrabMenu.setBackgroundTransparency(b: false);
                    itemGrabMenu.setDestroyItemOnClick(b: true);
                    itemGrabMenu.initializeShippingBin();
                    Game1.activeClickableMenu = itemGrabMenu;
                    if (Game1.player.IsLocalPlayer)
                    {
                        Game1.playSound("shwip");
                    }
                    if (Game1.player.FacingDirection == 1)
                    {
                        Game1.player.Halt();
                    }
                    Game1.player.showCarrying();
                    isTryToDoActionAtClickedTitle = 0;
                    return true;
                }
            }

            if (isTryToDoActionAtClickedTitle == 4 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                if (Game1.tryToCheckAt(grabTile, Game1.player))
                {
                    if (this.isVerbose)
                        ModEntry.getMonitor().Log(String.Format("Just checked at {0}", grabTile), LogLevel.Trace);
                }
                else
                {
                    if (this.isVerbose)
                        ModEntry.getMonitor().Log(String.Format("Just failed checked at {0}", grabTile), LogLevel.Trace);
                }
                isTryToDoActionAtClickedTitle = 0;
                return true;
            }

            if (isTryToDoActionAtClickedTitle == 5 && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 1, Game1.player))
            {
                if (!Game1.player.isRidingHorse() && Game1.tryToCheckAt(grabTile, Game1.player))
                {
                    isTryToDoActionAtClickedTitle = 0;
                    return true;
                }
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
            targetBuilding = null;
            targetTerrainFeatures = null;
            targetLargeTerrainFeatures = null;
            targetShippingBin = null;
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
                    {
                        targetTerrainFeatures = v.Value;
                        return 4;
                    }

                    // Anything actionable really
                    if (v.Value.isActionable())
                    {
                        targetTerrainFeatures = v.Value;
                        return 4;
                    }
                }
            }

            if (Game1.player.currentLocation.largeTerrainFeatures != null)
            {
                foreach (var f in Game1.player.currentLocation.largeTerrainFeatures)
                {
                    if (f.getBoundingBox().Intersects(new Rectangle((int)grabTile.X * 64, (int)grabTile.Y * 64, 64, 64)))
                    {
                        targetLargeTerrainFeatures = f;
                        return 4;
                    }
                }
            }

            // 4 - extend with building actionable tiles building even
            var l = Game1.player.currentLocation;
            var building = l.getBuildingAt(grabTile);
            if (building is not null && building.isActionableTile((int)grabTile.X, (int)grabTile.Y, Game1.player))
            {
                this.targetBuilding = building;
                return 4;
            }

            // 4 - Seem like shipping bin isn't actionable, this directly handle it
            if (building is StardewValley.Buildings.ShippingBin)
            {
                this.targetBuilding = building;
                return 4;
            }

            if (Game1.currentLocation.isActionableTile((int)grabTile.X, (int)grabTile.Y, Game1.player))
            {
                return 4;
            }

            return 0;
        }

        public string toString()
        {
            return String.Format("targetObject:{0}, target:{1}, isTryToDoActionAtClickedTitle:{2}, targetNPC:{3}, actionToolIndex:{4}, targetBuilding:{5}, targetTerrainFeatures:{6}, targetLargeTerrainFeatures:{7}", this.targetObject, this.target, this.isTryToDoActionAtClickedTitle, this.targetNPC, this.actionToolIndex, this.targetBuilding, this.targetTerrainFeatures, this.targetLargeTerrainFeatures);
        }
    }
}
