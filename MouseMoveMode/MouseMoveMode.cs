using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace MouseMoveMode
{
    public class ModConfig
    {
        public bool RightClickMoveModeDefault { get; set; } = true;
        public KeybindList RightClickMoveModeToggleButton { get; set; } = KeybindList.Parse("F6");
        public KeybindList ForceMoveButton { get; set; } = KeybindList.Parse("Space");
        public int HoldTickCount { get; set; } = 15;
        public bool HoldingMoveOnly { get; set; } = false;
        public int WeaponsSpecticalInteractionType { get; set; } = 2;
        public bool ExtendedModeDefault { get; set; } = true;
        public float MouseWhellingZoomStep = 0.25f;
        public float MouseWhellingMaxZoom = Options.maxZoom;
        public float MouseWhellingMinZoom = Options.minZoom;
        public KeybindList FullScreenKeybindShortcut { get; set; } = KeybindList.Parse("RightAlt + Enter");
    }

    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private static IMonitor monitor;

        public static ModConfig config;
        public static float hitboxRadius = 64f * 2;
        public static float baseHitboxRadius = 64f * 2;

        public static bool isMovingAutomaticaly = false;
        public static bool isBeingAutoCommand = false;
        public static bool isMouseOutsiteHitBox = false;
        public static bool isBeingControl = false;
        public static bool isHoldingMove = false;
        public static bool isActionableAtDesinationTile;

        public static bool isHoldingRunButton = false;

        private static Vector2 grabTile;
        public static NPC pointedNPC = null;
        public static Vector2 pointedTile;

        private static Vector2 vector_PlayerToMouse;
        private static Vector2 vector_AutoMove;

        private static Vector2 position_MouseOnScreen;
        private static Vector2 position_Destination;


        private static int tickCount = 15;
        private static int holdCount = 15;

        private static PathFindingHelper pathFindingHelper;

        public static bool isDebugVerbose = true;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Helper.Events.Input.ButtonPressed += this.ButtonPressedMods;
            Helper.Events.Input.ButtonPressed += this.ExtendedButtonPressedMods;
            Helper.Events.Input.CursorMoved += this.CursorMovedMods;
            Helper.Events.Input.MouseWheelScrolled += this.MouseWheelScrolled;
            Helper.Events.Input.ButtonReleased += this.ButtonReleasedMods;
            Helper.Events.GameLoop.UpdateTicked += this.UpdateTickMods;
            Helper.Events.Player.Warped += this.WarpedMods;
            Helper.Events.Display.Rendered += this.RenderedEvents;

            StartPatching();
            ModEntry.monitor = this.Monitor;
            pathFindingHelper = new PathFindingHelper();

            ModEntry.config = this.Helper.ReadConfig<ModConfig>();
        }

        public static IMonitor getMonitor()
        {
            return ModEntry.monitor;
        }

        private void RenderedEvents(object sender, RenderedEventArgs e)
        {
            if (config.RightClickMoveModeDefault)
                return;

            if (isMovingAutomaticaly && !isHoldingMove)
            {
                pathFindingHelper.drawIndicator(e.SpriteBatch);
            }
        }

        private void UpdateTickMods(object sender, EventArgs e)
        {
            bool flag = Context.IsWorldReady;

            if (!config.RightClickMoveModeDefault)
                return;
            if (!Context.IsWorldReady)
                return;

            hitboxRadius = baseHitboxRadius;

            if (Game1.player.ActiveObject != null)
            {
                if (Game1.player.ActiveObject.isPlaceable())
                {
                    hitboxRadius = baseHitboxRadius * 1.5f;
                }
            }
            vector_PlayerToMouse = position_MouseOnScreen + new Vector2(Game1.viewport.X, Game1.viewport.Y) - Game1.player.GetBoundingBox().Center.ToVector2();

            if (!Context.IsPlayerFree)
                return;

            MouseState mouseState = Mouse.GetState();
            switch (mouseState.RightButton)
            {
                case ButtonState.Pressed:
                    if (holdCount < config.HoldTickCount)
                    {
                        isHoldingMove = false;
                        holdCount++;
                    }
                    else
                    {
                        isHoldingMove = true;
                        isActionableAtDesinationTile = false;
                    }
                    break;
                default:
                    if (holdCount >= config.HoldTickCount)
                    {
                        isHoldingMove = false;
                        isMovingAutomaticaly = false;
                    }
                    holdCount = 0;
                    break;
            }
            if (isHoldingMove)
            {
                isMovingAutomaticaly = true;

                if (isBeingControl)
                {
                    if (tickCount == 0)
                    {
                        isBeingControl = false;
                        tickCount = 15;
                    }
                    else
                        tickCount--;
                }
            }
            else
            {
                // Update destination realtime when we try to reach an NPC (who is moving) 
                if (pointedNPC != null)
                {
                    position_Destination = pointedNPC.getStandingPosition();
                    // This reducing the need to findPath on every tick, which could make player stuck in one place
                    // because new path could overiding old one
                    if (pathFindingHelper.getCurrentDestinationTile() != Util.toTile(position_Destination))
                    {
                        pathFindingHelper.changeDes(position_Destination);
                    }
                }
            }

            if (Game1.player.ActiveObject != null)
            {
                // Player will stand still to place funiture item
                if (isMovingAutomaticaly && Game1.player.ActiveObject is StardewValley.Objects.Furniture)
                {
                    isMovingAutomaticaly = false;
                    Game1.player.Halt();
                }
            }
        }

        private void WarpedMods(object sender, WarpedEventArgs e)
        {
            pathFindingHelper.loadMap();
            isMovingAutomaticaly = false;
            // There are location that player's new position (after warp) is too close to new warp
            // This prevent warp back to back
            if (e.OldLocation is StardewValley.Locations.Town && e.NewLocation is StardewValley.Locations.Mountain)
            {
                Game1.player.Position += new Vector2(0f, -10f);
            }
            if (e.OldLocation is StardewValley.Farm && e.NewLocation.Name == "Backwoods")
            {
                Game1.player.Position += new Vector2(0f, -10f);
            }
        }

        private int SpecialCooldown(MeleeWeapon currentWeapon)
        {
            switch (currentWeapon.type)
            {
                case MeleeWeapon.defenseSword:
                    return MeleeWeapon.defenseCooldown;
                case MeleeWeapon.dagger:
                    return MeleeWeapon.daggerCooldown;
                case MeleeWeapon.club:
                    return MeleeWeapon.clubCooldown;
                case MeleeWeapon.stabbingSword:
                    return MeleeWeapon.attackSwordCooldown;
                default:
                    return 0;
            }
        }

        private void ExtendedButtonPressedMods(object sender, ButtonPressedEventArgs e)
        {
            if (!config.ExtendedModeDefault)
                return;

            if (config.FullScreenKeybindShortcut.JustPressed())
            {
                if (Game1.options.isCurrentlyWindowedBorderless() || Game1.options.isCurrentlyFullscreen())
                    Game1.options.setWindowedOption("Windowed");
                else
                {
                    Game1.options.setWindowedOption("Windowed Borderless");
                }
                Game1.exitActiveMenu();
            }
        }

        private void ButtonPressedMods(object sender, ButtonPressedEventArgs e)
        {
            string button = e.Button.ToString();

            if (!Context.IsWorldReady)
                return;

            if (config.RightClickMoveModeToggleButton.JustPressed())
            {
                config.RightClickMoveModeDefault = !config.RightClickMoveModeDefault;
            }

            if (!config.RightClickMoveModeDefault)
                return;

            if (e.Button == Game1.options.runButton[0].ToSButton())
            {
                isHoldingRunButton = true;
            }

            bool mouseRightIsDown = button == "MouseRight" && Context.IsPlayerFree;
            bool isMouseOutsiteHitBox = vector_PlayerToMouse.Length().CompareTo(hitboxRadius) > 0;

            if (Game1.player.ActiveObject != null)
            {
                if (Game1.player.ActiveObject is Furniture)
                {
                    mouseRightIsDown = false;
                }
            }

            if (Game1.player.CurrentTool != null && Game1.player.CurrentTool is MeleeWeapon weapon && !Game1.player.CurrentTool.Name.Contains("Scythe") && SpecialCooldown(weapon) <= 0 && Context.IsPlayerFree && Game1.player.CanMove)
            {
                if (config.WeaponsSpecticalInteractionType == 1)
                {
                    mouseRightIsDown = false;
                    if (isMouseOutsiteHitBox && button == "MouseRight" && !Game1.player.isRidingHorse())
                    {
                        weapon.animateSpecialMove(Game1.player);
                        Helper.Input.Suppress(e.Button);
                    }
                }
                else if (config.WeaponsSpecticalInteractionType == 2)
                {
                    if (button == "MouseRight")
                    {
                        Helper.Input.Suppress(e.Button);
                        isMouseOutsiteHitBox = true;
                    }
                    if ((button == "MouseMiddle" || button == "MouseX1") && !Game1.player.isRidingHorse())
                    {
                        weapon.animateSpecialMove(Game1.player);
                        Helper.Input.Suppress(e.Button);
                    }
                    if (button == "MouseLeft" && vector_PlayerToMouse.Length().CompareTo(hitboxRadius) < 0 && !Game1.player.isRidingHorse())
                    {
                        if (vector_PlayerToMouse.Y < 32f)
                        {
                            weapon.animateSpecialMove(Game1.player);
                            Helper.Input.Suppress(e.Button);
                        }
                    }
                }
                else if (config.WeaponsSpecticalInteractionType == 3)
                {
                    if (button == "MouseRight")
                    {
                        Helper.Input.Suppress(e.Button);
                        isMouseOutsiteHitBox = true;
                    }
                    if ((button == "MouseMiddle" || button == "MouseX1") && !Game1.player.isRidingHorse())
                    {
                        weapon.animateSpecialMove(Game1.player);
                        Helper.Input.Suppress(e.Button);
                    }
                }
            }


            if (mouseRightIsDown)
            {
                if (!config.HoldingMoveOnly)
                {
                    position_Destination = new Vector2(Game1.viewport.X, Game1.viewport.Y) + position_MouseOnScreen;
                    pathFindingHelper.changeDes(position_Destination);

                    // This help where we decided where to perform action
                    grabTile = Util.toTile(position_Destination);

                    // This could null, so we know that we not chasing a NPC when this null
                    pointedNPC = Game1.player.currentLocation.isCharacterAtTile(grabTile);

                    isMovingAutomaticaly = true;
                    isBeingControl = false;
                }

                if (config.ForceMoveButton.IsDown())
                {
                    if (isDebugVerbose)
                        this.Monitor.Log("We only moving now, no more fancy interaction", LogLevel.Info);
                    Helper.Input.Suppress(e.Button);
                }
                else if (isMouseOutsiteHitBox)
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Mouse target is outside hitbox range, at {0} and have {1} distance from player", position_Destination, vector_PlayerToMouse.Length()), LogLevel.Info);
                    Helper.Input.Suppress(e.Button);

                    isActionableAtDesinationTile = checkActionableTile();
                }
                else if (!isMouseOutsiteHitBox)
                {
                    isActionableAtDesinationTile = false;
                }
            }
            else
            {
                if (e.Button.IsUseToolButton())
                {
                    // Enough time for perform tool animation finish
                    tickCount = 15;
                }
                else
                    tickCount = 0;
                if (!config.ForceMoveButton.IsDown())
                    isBeingControl = true;
            }
        }

        private bool checkActionableTile()
        {
            // There is 5 type:
            // This is for NPC
            if (pointedNPC is not null)
            {
                if (isDebugVerbose)
                    this.Monitor.Log(String.Format("Found NPC {0} at {1}", pointedNPC, pointedNPC.Tile), LogLevel.Info);
                return true;
            }

            var gl = Game1.player.currentLocation;
            if (gl.Objects.ContainsKey(grabTile))
            {
                var pointedObject = gl.Objects[grabTile];
                if (pointedObject.isActionable(Game1.player))
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Found {0} at the pointed place", pointedObject), LogLevel.Info);
                    return true;
                }
            }

            // This is for Object is 1x1 tile size but with 2x1 hit box (aka Chess, ...)
            var bellowTile = new Vector2((int)grabTile.X, (int)grabTile.Y + 1);
            if (gl.Objects.ContainsKey(bellowTile))
            {
                var bellowObject = gl.Objects[bellowTile];
                if (bellowObject.IsSpawnedObject || (bellowObject.Type.Equals("Crafting") && bellowObject.Type.Equals("interactive")))
                {
                    if (bellowObject.isActionable(Game1.player))
                    {
                        grabTile = bellowTile;
                        if (isDebugVerbose)
                            this.Monitor.Log(String.Format("Found chest? {0} thing at the bellow of pointed place", bellowObject), LogLevel.Info);
                        return true;
                    }
                }
            }

            // This to handle Fence, Seed, ... that placeable
            if (Game1.player.ActiveObject is not null)
            {
                if (Game1.player.ActiveObject.isPlaceable())
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Found active item {0} to be place-able, we will place thing at the pointed place", Game1.player.ActiveItem), LogLevel.Info);
                    return true;
                }
            }

            // This handle terrainFeatures
            foreach (var items in Game1.player.currentLocation.terrainFeatures)
            {
                if (!items.ContainsKey(grabTile))
                    continue;

                var terrainFeature = items[grabTile];

                if (!terrainFeature.isActionable())
                    continue;

                if ((terrainFeature is Grass) || (terrainFeature is HoeDirt dirt && !dirt.readyForHarvest()))
                {
                    if (isDebugVerbose)
                    {
                        this.Monitor.Log(String.Format("Found needed special handler {0}! Which mean we skip", terrainFeature), LogLevel.Info);
                    }
                }
                else
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Found actionable {0} at the pointed place", terrainFeature), LogLevel.Info);
                    return true;
                }
            }

            var large = gl.getLargeTerrainFeatureAt((int)grabTile.X, (int)grabTile.Y);
            if (large is not null)
            {
                if (large.isActionable())
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Found {0} at the pointed place", large), LogLevel.Info);
                    return true;
                }
            }

            var building = gl.getBuildingAt(grabTile);
            if (building is not null)
            {
                if (building.isActionableTile((int)grabTile.X, (int)grabTile.Y, Game1.player))
                {
                    if (isDebugVerbose)
                        this.Monitor.Log(String.Format("Found actionable tile of {0}", building), LogLevel.Info);
                    return true;
                }
            }

            // Don't know, just hope this to work
            if (gl.isActionableTile((int)grabTile.X, (int)grabTile.Y, Game1.player))
            {
                if (isDebugVerbose)
                    this.Monitor.Log("Can't found any thing at the pointed place, but we try anyway", LogLevel.Info);
                return true;
            }

            return false;
        }


        private void MouseWheelScrolled(object sender, MouseWheelScrolledEventArgs e)
        {
            bool flag = Context.IsWorldReady;

            if (!config.ExtendedModeDefault)
                return;
            if (!Context.IsWorldReady)
                return;

            // MouseState mouseState = Mouse.GetState();
            // mouseState.w

            if (this.Helper.Input.IsDown(SButton.LeftControl) || this.Helper.Input.IsDown(SButton.RightControl))
            {
                if (e.OldValue < e.NewValue)
                {
                    if (Game1.options.zoomLevel <= config.MouseWhellingMaxZoom)
                        Game1.options.desiredBaseZoomLevel += config.MouseWhellingZoomStep;
                    Game1.exitActiveMenu();
                    if (!(Game1.player.UsingTool && (Game1.player.CurrentTool == null || !(Game1.player.CurrentTool is FishingRod fishingRod) || (!fishingRod.isReeling && !fishingRod.pullingOutOfWater))))
                    {
                        Game1.player.CurrentToolIndex += 1;
                    }
                }
                else if (e.OldValue > e.NewValue)
                {
                    if (Game1.options.zoomLevel >= config.MouseWhellingMinZoom)
                        Game1.options.desiredBaseZoomLevel -= config.MouseWhellingZoomStep;
                    Game1.exitActiveMenu();
                    if (!(Game1.player.UsingTool && (Game1.player.CurrentTool == null || !(Game1.player.CurrentTool is FishingRod fishingRod) || (!fishingRod.isReeling && !fishingRod.pullingOutOfWater))))
                    {
                        Game1.player.CurrentToolIndex -= 1;
                    }
                }
            }
        }

        private void CursorMovedMods(object sender, CursorMovedEventArgs e)
        {
            // Check if game is fully loaded or not
            if (!Context.IsWorldReady)
                return;

            if (config.RightClickMoveModeDefault)
                position_MouseOnScreen = Game1.getMousePosition(Game1.uiMode).ToVector2();
        }

        private void ButtonReleasedMods(object sender, ButtonReleasedEventArgs e)
        {
            string button = e.Button.ToString();

            if (config.RightClickMoveModeDefault)
            {
                if (e.Button == Game1.options.runButton[0].ToSButton())
                {
                    isHoldingRunButton = false;
                }
            }
        }

        /**
         * @brief This try to perform action at the tile, it will stop the moving
         * automatically when we success the action
         *
         * @return 
         */
        public static void TryToCheckGrapTile()
        {
            if (!isActionableAtDesinationTile)
                return;

            // Auto dismount the player first if destination tile is actionable
            if (Game1.player.isRidingHorse())
            {
                if (isActionableAtDesinationTile && Utility.tileWithinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 2, Game1.player))
                    Game1.player.mount.dismount();
                return;
            }

            // This overide all other action interaction
            if (pointedNPC is not null)
            {
                if (isDebugVerbose)
                    ModEntry.getMonitor().Log(String.Format("Try check NPC {0} at tile {1}", pointedNPC, grabTile), LogLevel.Info);
                // This updating grabTile as NPC could already moved
                grabTile = pointedNPC.Tile;
                bool isNPCChecked = Game1.tryToCheckAt(grabTile, Game1.player);
                if (isNPCChecked)
                {
                    if (isDebugVerbose)
                        ModEntry.getMonitor().Log(String.Format("Success check NPC {0} at tile {1}", pointedNPC, grabTile), LogLevel.Info);
                    isActionableAtDesinationTile = false;
                    isMovingAutomaticaly = false;
                }
                // This overide all other behavior, and it dangerous
                return;
            }

            // Try to place the item next instead, I think it have higher piority
            if (Game1.player.ActiveObject is not null)
                if (isActionableAtDesinationTile && Game1.player.ActiveObject.isPlaceable())
                {
                    if (isDebugVerbose)
                        ModEntry.getMonitor().Log(String.Format("Try placing item at tile {0}", grabTile), LogLevel.Info);
                    var isPlaced = Utility.tryToPlaceItem(Game1.player.currentLocation, Game1.player.ActiveObject, (int)grabTile.X * 64 + 32, (int)grabTile.Y * 64 + 32);
                    if (isPlaced)
                    {
                        if (isDebugVerbose)
                            ModEntry.getMonitor().Log(String.Format("Success placing item at tile {0}", grabTile), LogLevel.Info);
                        isActionableAtDesinationTile = false;
                        isMovingAutomaticaly = false;
                        return;
                    }
                }

            // After that, try to perform an action directly on destination tile
            if (isDebugVerbose)
                ModEntry.getMonitor().Log(String.Format("Try checked at tile {0}", grabTile), LogLevel.Info);
            var isChecked = Game1.tryToCheckAt(pathFindingHelper.getCurrentDestinationTile(), Game1.player);
            if (isChecked)
            {
                if (isDebugVerbose)
                    ModEntry.getMonitor().Log(String.Format("Success checked item at tile {0}", grabTile), LogLevel.Info);
                isActionableAtDesinationTile = false;
                isMovingAutomaticaly = false;
            }
        }

        public static void MoveVectorToCommand()
        {
            bool flag = isMovingAutomaticaly;

            if (!flag)
                return;

            if (isHoldingMove)
            {
                vector_AutoMove = vector_PlayerToMouse;

                Game1.player.movementDirections.Clear();
                if (vector_AutoMove.X <= 5 && vector_AutoMove.X >= -5)
                    vector_AutoMove.X = 0;
                if (vector_AutoMove.Y <= 5 && vector_AutoMove.Y >= -5)
                    vector_AutoMove.Y = 0;

                if (vector_AutoMove == new Vector2(0, 0))
                {
                    isMovingAutomaticaly = false;
                    return;
                }
            }
            else
            {
                // we following the path finding result
                if (isDebugVerbose)
                {
                    ModEntry.getMonitor().Log(String.Format("Follow path finding to {0} with direction {1}", pathFindingHelper.nextPath(), pathFindingHelper.moveDirection()), LogLevel.Info);
                }

                vector_AutoMove = pathFindingHelper.moveDirection();

            }

            // Try to check grap tile when player is close enough
            if (Utility.withinRadiusOfPlayer((int)grabTile.X, (int)grabTile.Y, 2, Game1.player) && isActionableAtDesinationTile)
            {
                TryToCheckGrapTile();
            }

            // Or if the player is facing into it
            // ????

            if (vector_AutoMove == new Vector2(0, 0))
            {
                isMovingAutomaticaly = false;
                return;
            }
            else
            {
                if (vector_AutoMove.Length() > 1f)
                    vector_AutoMove.Normalize();
                if (vector_AutoMove.X > 0)
                    Game1.player.SetMovingRight(true);
                else
                    Game1.player.SetMovingLeft(true);

                if (vector_AutoMove.Y > 0)
                    Game1.player.SetMovingDown(true);
                else
                    Game1.player.SetMovingUp(true);

            }

        }

        public static void StartPatching()
        {
            var newHarmony = new Harmony("ylsama.RightClickMoveMode");

            var farmer_Halt_Info = AccessTools.Method(typeof(Farmer), "Halt");
            var farmer_Halt_PrefixPatch = AccessTools.Method(typeof(ModEntry), "PrefixMethod_Farmer_HaltPatch");
            newHarmony.Patch(farmer_Halt_Info, new HarmonyMethod(farmer_Halt_PrefixPatch));

            var farmer_getMovementSpeed_Info = AccessTools.Method(typeof(Farmer), "getMovementSpeed");
            var farmer_getMovementSpeed_PrefixPatch = AccessTools.Method(typeof(ModEntry), "PrefixMethod_Farmer_getMovementSpeedPatch");
            newHarmony.Patch(farmer_getMovementSpeed_Info, new HarmonyMethod(farmer_getMovementSpeed_PrefixPatch));

            var farmer_MovePosition_Info = AccessTools.Method(typeof(Farmer), "MovePosition", new Type[] { typeof(GameTime), typeof(xTile.Dimensions.Rectangle), typeof(GameLocation) });
            var farmer_MovePosition_PrefixPatch = AccessTools.Method(typeof(ModEntry), "PrefixMethod_Farmer_MovePositionPatch");
            newHarmony.Patch(farmer_MovePosition_Info, new HarmonyMethod(farmer_MovePosition_PrefixPatch));

            var game1_UpdateControlInput_Info = AccessTools.Method(typeof(Game1), "UpdateControlInput", new Type[] { typeof(GameTime) });
            var game1_UpdateControlInput_PostfixPatch = AccessTools.Method(typeof(ModEntry), "PostfixMethod_Game1_UpdateControlInputPatch");
            newHarmony.Patch(game1_UpdateControlInput_Info, null, new HarmonyMethod(game1_UpdateControlInput_PostfixPatch));
        }

        public static bool PrefixMethod_Farmer_HaltPatch()
        {
            // Prefix Method return will control the base method execution
            // true mean base method will exec, false mean the opposite
            if (config.RightClickMoveModeDefault)
            {
                return !isMovingAutomaticaly || isBeingAutoCommand;
            }
            return true;
        }

        public static bool PrefixMethod_Farmer_MovePositionPatch()
        {
            if (config.RightClickMoveModeDefault)
            {
                if (!isBeingControl && isMovingAutomaticaly && Context.IsPlayerFree && Game1.player.CanMove)
                {
                    MovePosition(Game1.currentGameTime, Game1.viewport, Game1.player.currentLocation);
                    return false;
                }
            }
            return true;
        }

        public static void PostfixMethod_Game1_UpdateControlInputPatch()
        {
            if (config.RightClickMoveModeDefault)
            {
                if (!isBeingControl && Context.IsPlayerFree && Game1.player.CanMove)
                {
                    isBeingAutoCommand = true;
                    MoveVectorToCommand();
                    if (isHoldingRunButton && !Game1.player.canOnlyWalk)
                    {
                        Game1.player.setRunning(!Game1.options.autoRun, false);
                        Game1.player.setMoving(Game1.player.running ? (byte)16 : (byte)48);
                    }
                    else if (!isHoldingRunButton && !Game1.player.canOnlyWalk)
                    {
                        Game1.player.setRunning(Game1.options.autoRun, false);
                        Game1.player.setMoving(Game1.player.running ? (byte)16 : (byte)48);
                    }

                    isBeingAutoCommand = false;
                }
                else
                    isBeingAutoCommand = false;
            }
        }

        public static bool PrefixMethod_Farmer_getMovementSpeedPatch(ref float __result)
        {
            if (config.RightClickMoveModeDefault)
            {
                if (!isBeingControl && Context.IsPlayerFree)
                {
                    if (Game1.player.UsingTool && Game1.player.canStrafeForToolUse())
                    {
                        __result = 2f;
                        return false;
                    }
                    if (Game1.CurrentEvent == null || Game1.CurrentEvent.playerControlSequence)
                    {
                        Game1.player.movementMultiplier = 0.066f;
                        float movementSpeed2 = 1f;
                        movementSpeed2 = ((!Game1.player.isRidingHorse()) ? Math.Max(1f, ((float)Game1.player.speed + (Game1.eventUp ? 0f : (Game1.player.addedSpeed + Game1.player.temporarySpeedBuff))) * Game1.player.movementMultiplier * (float)Game1.currentGameTime.ElapsedGameTime.Milliseconds) : Math.Max(1f, ((float)Game1.player.speed + (Game1.eventUp ? 0f : (Game1.player.addedSpeed + 4.6f + (Game1.player.mount.ateCarrotToday ? 0.4f : 0f) + ((Game1.player.stats.Get("Book_Horse") != 0) ? 0.5f : 0f)))) * Game1.player.movementMultiplier * (float)Game1.currentGameTime.ElapsedGameTime.Milliseconds));
                        // This isn't needed
                        // if (Game1.player.movementDirections.Count > 1)
                        // {
                        //     movementSpeed2 *= 0.707f;
                        // }
                        if (Game1.CurrentEvent == null && Game1.player.hasBuff("19"))
                        {
                            movementSpeed2 = 0f;
                        }
                        __result = movementSpeed2;
                        return false;
                    }
                    float movementSpeed = Math.Max(1f, (float)Game1.player.speed + (Game1.eventUp ? ((float)Math.Max(0, Game1.CurrentEvent.farmerAddedSpeed - 2)) : (Game1.player.addedSpeed + (Game1.player.isRidingHorse() ? 5f : Game1.player.temporarySpeedBuff))));
                    if (Game1.player.movementDirections.Count > 1)
                    {
                        movementSpeed = Math.Max(1, (int)Math.Sqrt(2f * (movementSpeed * movementSpeed)) / 2);
                    }
                    __result = movementSpeed;
                    return false;
                }
            }
            return true;
        }

        public static void MovePosition(GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation)
        {
            if (Game1.player.IsSitting()) { return; }

            if (Game1.CurrentEvent == null || Game1.CurrentEvent.playerControlSequence)
            {
                if (Game1.shouldTimePass() && Game1.player.temporarilyInvincible)
                {
                    if (Game1.player.temporaryInvincibilityTimer < 0)
                    {
                        Game1.player.currentTemporaryInvincibilityDuration = 1200;
                    }
                    Game1.player.temporaryInvincibilityTimer += time.ElapsedGameTime.Milliseconds;
                    if (Game1.player.temporaryInvincibilityTimer > Game1.player.currentTemporaryInvincibilityDuration)
                    {
                        Game1.player.temporarilyInvincible = false;
                        Game1.player.temporaryInvincibilityTimer = 0;
                    }
                }
            }
            else if (Game1.player.temporarilyInvincible)
            {
                Game1.player.temporarilyInvincible = false;
                Game1.player.temporaryInvincibilityTimer = 0;
            }

            if (Game1.activeClickableMenu != null)
            {
                if (Game1.CurrentEvent == null)
                {
                    return;
                }
                if (Game1.CurrentEvent.playerControlSequence)
                {
                    return;
                }
            }

            if (Game1.player.isRafting)
            {
                Game1.player.moveRaft(currentLocation, time);
                return;
            }

            if (Game1.player.xVelocity != 0f || Game1.player.yVelocity != 0f)
            {
                if (double.IsNaN((double)Game1.player.xVelocity) || double.IsNaN((double)Game1.player.yVelocity))
                {
                    Game1.player.xVelocity = 0f;
                    Game1.player.yVelocity = 0f;
                }

                Rectangle bounds = Game1.player.GetBoundingBox();
                Rectangle value = new Microsoft.Xna.Framework.Rectangle(bounds.X + (int)Math.Floor(Game1.player.xVelocity), bounds.Y - (int)Math.Floor(Game1.player.yVelocity), bounds.Width, bounds.Height);
                Rectangle nextPositionCeil = new Microsoft.Xna.Framework.Rectangle(bounds.X + (int)Math.Ceiling(Game1.player.xVelocity), bounds.Y - (int)Math.Ceiling(Game1.player.yVelocity), bounds.Width, bounds.Height);
                Rectangle nextPosition = Microsoft.Xna.Framework.Rectangle.Union(value, nextPositionCeil);

                if (!currentLocation.isCollidingPosition(nextPosition, viewport, true, -1, false, Game1.player))
                {
                    Game1.player.position.X += Game1.player.xVelocity;
                    Game1.player.position.Y -= Game1.player.yVelocity;
                    Game1.player.xVelocity -= Game1.player.xVelocity / 16f;
                    Game1.player.yVelocity -= Game1.player.yVelocity / 16f;
                    if (Math.Abs(Game1.player.xVelocity) <= 0.05f)
                    {
                        Game1.player.xVelocity = 0f;
                    }
                    if (Math.Abs(Game1.player.yVelocity) <= 0.05f)
                    {
                        Game1.player.yVelocity = 0f;
                    }
                }
                else
                {
                    Game1.player.xVelocity -= Game1.player.xVelocity / 16f;
                    Game1.player.yVelocity -= Game1.player.yVelocity / 16f;
                    if (Math.Abs(Game1.player.xVelocity) <= 0.05f)
                    {
                        Game1.player.xVelocity = 0f;
                    }
                    if (Math.Abs(Game1.player.yVelocity) <= 0.05f)
                    {
                        Game1.player.yVelocity = 0f;
                    }
                }
            }

            if (Game1.player.CanMove || Game1.eventUp || Game1.player.controller != null || Game1.player.canStrafeForToolUse())
            {
                Game1.player.TemporaryPassableTiles.ClearNonIntersecting(Game1.player.GetBoundingBox());
                float movementSpeed = Game1.player.getMovementSpeed();
                Game1.player.temporarySpeedBuff = 0f;

                if (Game1.player.movementDirections.Contains(0))
                    TryMoveDrection(time, viewport, currentLocation, FaceDirection.UP);

                if (Game1.player.movementDirections.Contains(2))
                    TryMoveDrection(time, viewport, currentLocation, FaceDirection.DOWN);

                if (Game1.player.movementDirections.Contains(1))
                    TryMoveDrection(time, viewport, currentLocation, FaceDirection.RIGHT);

                if (Game1.player.movementDirections.Contains(3))
                    TryMoveDrection(time, viewport, currentLocation, FaceDirection.LEFT);

                if (Game1.player.movementDirections.Count == 2)
                {
                    if (Math.Abs(vector_AutoMove.Y / vector_AutoMove.X).CompareTo(0.45f) < 0)
                    {
                        Game1.player.SetMovingDown(false);
                        Game1.player.SetMovingUp(false);
                    }
                    else if (Math.Abs(vector_AutoMove.Y) > Math.Sin(Math.PI / 3))
                    {
                        Game1.player.SetMovingRight(false);
                        Game1.player.SetMovingLeft(false);
                    }
                }
                return;
            }

            if (Game1.player.movementDirections.Count > 0 && !Game1.player.UsingTool)
            {
                Game1.player.FarmerSprite.intervalModifier = 1f - (Game1.player.running ? 0.0255f : 0.025f) * (Math.Max(1f, ((float)Game1.player.speed + (Game1.eventUp ? 0f : ((float)(int)Game1.player.addedSpeed + (Game1.player.isRidingHorse() ? 4.6f : 0f)))) * Game1.player.movementMultiplier * (float)Game1.currentGameTime.ElapsedGameTime.Milliseconds) * 1.25f);
            }
            else
            {
                Game1.player.FarmerSprite.intervalModifier = 1f;
            }

            if (currentLocation != null && currentLocation.isFarmerCollidingWithAnyCharacter())
            {
                Game1.player.TemporaryPassableTiles.Add(new Microsoft.Xna.Framework.Rectangle(Game1.player.TilePoint.X * 64, Game1.player.TilePoint.Y * 64, 64, 64));
            }
        }

        public static FaceDirection RightDirection(FaceDirection faceDirection)
        {
            switch (faceDirection)
            {
                case FaceDirection.UP:
                    return FaceDirection.RIGHT;
                case FaceDirection.RIGHT:
                    return FaceDirection.DOWN;
                case FaceDirection.DOWN:
                    return FaceDirection.LEFT;
                case FaceDirection.LEFT:
                    return FaceDirection.UP;
                default:
                    return FaceDirection.DOWN;
            }
        }

        public static FaceDirection LeftDirection(FaceDirection faceDirection)
        {
            switch (faceDirection)
            {
                case FaceDirection.UP:
                    return FaceDirection.LEFT;
                case FaceDirection.RIGHT:
                    return FaceDirection.UP;
                case FaceDirection.DOWN:
                    return FaceDirection.RIGHT;
                case FaceDirection.LEFT:
                    return FaceDirection.DOWN;
                default:
                    return FaceDirection.DOWN;
            }
        }

        public static void TryMoveDrection(GameTime time, xTile.Dimensions.Rectangle viewport, GameLocation currentLocation, FaceDirection faceDirection)
        {
            Warp warp = Game1.currentLocation.isCollidingWithWarp(Game1.player.nextPosition(((int)faceDirection)), Game1.player);
            if (warp != null && Game1.player.IsLocalPlayer)
            {
                Game1.player.warpFarmer(warp);
                return;
            }
            float movementSpeed = Game1.player.getMovementSpeed();
            if (Game1.player.movementDirections.Contains((int)faceDirection))
            {
                Rectangle nextPos = Game1.player.nextPosition((int)faceDirection);

                if (!currentLocation.isCollidingPosition(nextPos, viewport, true, 0, false, Game1.player))
                {
                    if (faceDirection == FaceDirection.UP || faceDirection == FaceDirection.DOWN)
                        Game1.player.position.Y += movementSpeed * vector_AutoMove.Y;
                    else
                        Game1.player.position.X += movementSpeed * vector_AutoMove.X;

                    Game1.player.behaviorOnMovement((int)faceDirection);
                }
                else
                {
                    nextPos = Game1.player.nextPositionHalf((int)faceDirection);

                    if (!currentLocation.isCollidingPosition(nextPos, viewport, true, 0, false, Game1.player))
                    {

                        if (faceDirection == FaceDirection.UP || faceDirection == FaceDirection.DOWN)
                            Game1.player.position.Y += movementSpeed * vector_AutoMove.Y / 2f;
                        else
                            Game1.player.position.X += movementSpeed * vector_AutoMove.X / 2f;

                        Game1.player.behaviorOnMovement((int)faceDirection);
                    }
                    else if (Game1.player.movementDirections.Count == 1)
                    {
                        Rectangle tmp = Game1.player.nextPosition((int)faceDirection);
                        tmp.Width /= 4;
                        bool leftCorner = currentLocation.isCollidingPosition(tmp, viewport, true, 0, false, Game1.player);
                        tmp.X += tmp.Width * 3;
                        bool rightCorner = currentLocation.isCollidingPosition(tmp, viewport, true, 0, false, Game1.player);
                        if (leftCorner && !rightCorner && !currentLocation.isCollidingPosition(Game1.player.nextPosition((int)LeftDirection(faceDirection)), viewport, true, 0, false, Game1.player))
                        {
                            if (faceDirection == FaceDirection.UP || faceDirection == FaceDirection.DOWN)
                                Game1.player.position.X += (float)Game1.player.speed * ((float)time.ElapsedGameTime.Milliseconds / 64f);
                            else
                                Game1.player.position.Y += (float)Game1.player.speed * ((float)time.ElapsedGameTime.Milliseconds / 64f);
                        }
                        else if (rightCorner && !leftCorner && !currentLocation.isCollidingPosition(Game1.player.nextPosition((int)RightDirection(faceDirection)), viewport, true, 0, false, Game1.player))
                        {
                            if (faceDirection == FaceDirection.UP || faceDirection == FaceDirection.DOWN)
                                Game1.player.position.X -= (float)Game1.player.speed * ((float)time.ElapsedGameTime.Milliseconds / 64f);
                            else
                                Game1.player.position.Y -= (float)Game1.player.speed * ((float)time.ElapsedGameTime.Milliseconds / 64f);
                        }
                    }
                }
            }
        }
    }
}
