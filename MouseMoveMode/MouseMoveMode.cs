using System;
using GenericModConfigMenu;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Objects;
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
        public bool EnablePathFinding { get; set; } = true;
        public int PathFindLimit { get; set; } = 500;
        public bool ShowMousePositionHint { get; set; } = false;
    }

    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        private static IMonitor monitor;

        public static ModConfig config;

        public static bool isMovingAutomaticaly = false;
        public static bool isBeingAutoCommand = false;

        // This flag usage is to temprorary break the auto moving 
        public static bool isBeingControl = false;
        public static bool isHoldingMove = false;
        public static bool isHoldingRunButton = false;

        public static NPC pointedNPC = null;
        public IActionHandler actionHandler = new ActionHandlerOld();

        private static Vector2 vector_PlayerToMouse;
        private static Vector2 vector_AutoMove;

        private static Vector2 position_MouseOnScreen;
        private static Vector2 position_Destination;

        private static int tickCount = 15;
        private static int holdCount = 15;

        private static PathFindingHelper pathFindingHelper;

        public static bool isDebugVerbose = false;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Helper.Events.Input.ButtonPressed += this.ButtonPressedMods;
            Helper.Events.Input.ButtonPressed += this.DebugActionHandler;
            Helper.Events.Input.ButtonPressed += this.ExtendedButtonPressedMods;
            Helper.Events.Input.CursorMoved += this.CursorMovedMods;
            Helper.Events.Input.MouseWheelScrolled += this.MouseWheelScrolled;
            Helper.Events.Input.ButtonReleased += this.ButtonReleasedMods;
            Helper.Events.GameLoop.UpdateTicked += this.UpdateTickMods;
            Helper.Events.Player.Warped += this.WarpedMods;
            Helper.Events.Display.Rendered += this.RenderedEvents;
            Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            StartPatching();
            ModEntry.monitor = this.Monitor;
            pathFindingHelper = new PathFindingHelper();

            ModEntry.config = this.Helper.ReadConfig<ModConfig>();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => ModEntry.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(ModEntry.config)
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Path finding",
                tooltip: () => "Enable to Use latest functional for more acuracy control player to the pointed destination location",
                getValue: () => ModEntry.config.EnablePathFinding,
                setValue: value => ModEntry.config.EnablePathFinding = value
            );

            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Path indicator",
                tooltip: () => "Enable to Show path that player are moving",
                getValue: () => ModEntry.config.ShowMousePositionHint,
                setValue: value => ModEntry.config.ShowMousePositionHint = value
            );

            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "Finding limit",
                tooltip: () => "Total of game title will be search through to find the path",
                getValue: () => ModEntry.config.PathFindLimit,
                setValue: value => ModEntry.config.PathFindLimit = value,
                min: 1,
                max: 800
            );

            string[] allows = new string[4];
            allows[0] = "Disable special handling ()";
            allows[1] = "Can be used freely";
            allows[2] = "Left click on player";
            allows[3] = "Use Middle or X1 mouse button";
            configMenu.AddTextOption(
                mod: this.ModManifest,
                name: () => "Weapon special attack",
                tooltip: () => "By default right-click is disable so any player action won't interupt movement. This allow how you want to overide default handling to perform weapon special actack",
                getValue: () => allows[ModEntry.config.WeaponsSpecticalInteractionType],
                setValue: value =>
                {
                    for (int i = 0; i < allows.Length; i++)
                    {
                        if (allows[i] == value)
                            ModEntry.config.WeaponsSpecticalInteractionType = i;
                    }
                },
                allowedValues: allows
            );
        }

        public static IMonitor getMonitor()
        {
            return ModEntry.monitor;
        }

        private void RenderedEvents(object sender, RenderedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!config.RightClickMoveModeDefault)
                return;

            if (config.ShowMousePositionHint)
            {
                if ((Game1.activeClickableMenu == null) && (Game1.CurrentEvent == null))
                {
                    var mouseHelper = ModEntry.position_MouseOnScreen + new Vector2(Game1.viewport.X, Game1.viewport.Y);
                    var mouseBox = Util.toBoxPosition(Util.toTile(mouseHelper));
                    DrawHelper.drawCursorHelper(e.SpriteBatch, mouseBox);
                }

                if (ModEntry.isMovingAutomaticaly && !ModEntry.isHoldingMove && !ModEntry.isBeingControl)
                {
                    pathFindingHelper.drawIndicator(e.SpriteBatch);
                }
            }
        }

        private void UpdateTickMods(object sender, EventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!config.RightClickMoveModeDefault)
                return;

            vector_PlayerToMouse = position_MouseOnScreen + new Vector2(Game1.viewport.X, Game1.viewport.Y) - Game1.player.GetBoundingBox().Center.ToVector2();

            if (!Context.IsPlayerFree)
                return;

            MouseState mouseState = Mouse.GetState();
            switch (mouseState.RightButton)
            {
                case ButtonState.Pressed:
                    if (holdCount < config.HoldTickCount)
                    {
                        ModEntry.isHoldingMove = false;
                        ModEntry.holdCount++;
                    }
                    else
                    {
                        ModEntry.isHoldingMove = true;
                    }
                    break;
                case ButtonState.Released:
                default:
                    if (holdCount >= config.HoldTickCount)
                    {
                        if (ModEntry.isDebugVerbose) ModEntry.getMonitor().Log("Right mouse release, so holding move should stop at the current posistion");

                        ModEntry.isHoldingMove = false;
                        ModEntry.isMovingAutomaticaly = false;
                    }
                    ModEntry.holdCount = 0;
                    break;
            }

            if (ModEntry.isHoldingMove)
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Holding move already start, not thing will break auto move till right mouse release");
                ModEntry.isMovingAutomaticaly = true;

                if (ModEntry.isBeingControl)
                {
                    if (ModEntry.tickCount == 0)
                    {
                        ModEntry.isBeingControl = false;
                        ModEntry.tickCount = 15;
                    }
                    else
                        ModEntry.tickCount--;
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
                if (ModEntry.isMovingAutomaticaly && Game1.player.ActiveObject is StardewValley.Objects.Furniture)
                {
                    if (ModEntry.isDebugVerbose) this.Monitor.Log("Player holding furniture, stop moving");

                    ModEntry.isMovingAutomaticaly = false;
                    Game1.player.Halt();
                }
            }

            if (ModEntry.isHoldingMove)
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("cancelAction");
                actionHandler.cancelAction();
            }
            else
            {
                if (actionHandler.tryDoAction())
                {
                    ModEntry.isMovingAutomaticaly = false;
                }
            }
        }

        private void WarpedMods(object sender, WarpedEventArgs e)
        {
            ModEntry.pathFindingHelper.loadMap();
            ModEntry.isMovingAutomaticaly = false;
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
            switch (currentWeapon.type.Value)
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

        private bool autoMovePrecheck()
        {
            // If player can't move then just let the game handle Right Click
            if (!(Context.IsPlayerFree && Game1.player.CanMove))
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Player isn't free or can't move");

                return false;
            }

            // Let the game handle like default
            if (Game1.player.ActiveObject != null)
                if (Game1.player.ActiveObject is Furniture)
                    return false;

            return true;
        }

        private void handleRightClickToMove(SButton button)
        {
            if (config.ForceMoveButton.IsDown())
            {
                if (ModEntry.isDebugVerbose)
                    this.Monitor.Log("We only moving now, no more fancy interaction", LogLevel.Trace);

                Helper.Input.Suppress(button);
            }

            // If we only need to handler holding move, this is enough
            if (config.HoldingMoveOnly)
            {
                if (ModEntry.isDebugVerbose)
                    this.Monitor.Log("We only holding moving now, no need for complicated code", LogLevel.Trace);

                return;
            }

            ModEntry.position_Destination = new Vector2(Game1.viewport.X, Game1.viewport.Y) + position_MouseOnScreen;
            var desitinationTile = Util.toTile(ModEntry.position_Destination);

            // Like why
            ModEntry.pathFindingHelper.changeDes(Util.toPosition(desitinationTile));

            // This could null, so we know that we won't chasing a NPC when this null
            ModEntry.pointedNPC = Game1.player.currentLocation.isCharacterAtTile(desitinationTile);
            if (ModEntry.isDebugVerbose)
                if (pointedNPC is not null)
                    this.Monitor.Log(String.Format("Found NPC {0} at destination {1}", pointedNPC, position_Destination), LogLevel.Trace);
        }

        /**
         * @brief This contain special handle for weapon
         * @return when it true, it mean we fully handle the button and thus can
         * stop/finish that button handling process
         */
        private bool handleWhenUsingWeapon(SButton button, MeleeWeapon weapon)
        {
            // Check If player can't move
            if (!(Context.IsPlayerFree && Game1.player.CanMove))
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Player isn't free or can't move");

                return false;
            }

            var canActiveSecialMove = true;
            canActiveSecialMove &= !weapon.Name.Contains("Scythe");
            canActiveSecialMove &= SpecialCooldown(weapon) <= 0;
            canActiveSecialMove &= !Game1.player.isRidingHorse();
            // If the Weapon can't perform special attack then we didn't need to
            // do anything
            if (!canActiveSecialMove)
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Player isn't able to perform special attack, we bail out");
                return false;
            }

            var mousePosition = ModEntry.position_MouseOnScreen + new Vector2(Game1.viewport.X, Game1.viewport.Y);
            bool isMouseWithinRadiusOfPlayer = Utility.withinRadiusOfPlayer((int)mousePosition.X, (int)mousePosition.Y, 1, Game1.player);

            if (config.WeaponsSpecticalInteractionType == 1)
            {
                // Let the game handle right click like default
                if (SButton.MouseRight == button)
                    // This let the button to be handle by other function
                    return false;
            }

            if (config.WeaponsSpecticalInteractionType == 2)
            {
                switch (button)
                {
                    // Disable right-click
                    case SButton.MouseRight:
                        Helper.Input.Suppress(button);
                        // Then let the right click to move handle the rest
                        return false;
                    // Mouse middle or mouse X1 could be use for special attack
                    case SButton.MouseMiddle:
                    case SButton.MouseX1:
                        weapon.animateSpecialMove(Game1.player);
                        Helper.Input.Suppress(button);
                        // No need for more handle to this button
                        return true;
                    // Mouse left on the player could be use for special attack
                    case SButton.MouseLeft:
                        if (isMouseWithinRadiusOfPlayer)
                        {
                            weapon.animateSpecialMove(Game1.player);
                            Helper.Input.Suppress(button);
                            // This mean we overide normal left click interaction
                            return true;
                        }
                        // or just let it be normal
                        return false;
                    default:
                        break;
                }
            }

            if (config.WeaponsSpecticalInteractionType == 3)
            {
                switch (button)
                {
                    // Disable right-click
                    case SButton.MouseRight:
                        Helper.Input.Suppress(button);
                        // Let the right click to move handle the rest
                        return false;
                    // Mouse middle or mouse X1 could be use for special attack
                    case SButton.MouseMiddle:
                    case SButton.MouseX1:
                        weapon.animateSpecialMove(Game1.player);
                        Helper.Input.Suppress(button);
                        // No need for more fancy handling
                        return true;
                    default:
                        break;
                }
            }

            return false;
        }

        /**
         * @brief Use F7 to test the action handler
         */
        private void DebugActionHandler(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (isDebugVerbose)
            {
                if (e.Button.CompareTo(SButton.F7) == 0)
                {
                    var target = new Vector2(Game1.viewport.X, Game1.viewport.Y) + position_MouseOnScreen;
                    if (ModEntry.isDebugVerbose)
                        this.Monitor.Log(String.Format("Force do action at {0}", Util.toTile(target)));
                    this.actionHandler.debugDoAction(target);
                }
            }
        }

        /**
         * @brief there is only one button being handle here, by passing it over
         * multiple function, which return if we properly handle it? if it true
         * then we can stop at that point
         */
        private void ButtonPressedMods(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (config.RightClickMoveModeToggleButton.JustPressed())
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Toggle mouse move mode!");

                config.RightClickMoveModeDefault = !config.RightClickMoveModeDefault;
            }

            if (!config.RightClickMoveModeDefault)
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Mouse move mode is currently disable");

                return;
            }

            // This to check if the control input running is enable for the auto
            // movement - movement speed patch handler
            if (e.Button == Game1.options.runButton[0].ToSButton())
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("Start holding run button");

                ModEntry.isHoldingRunButton = true;
                return;
            }

            var isFinishHandling = false;
            var tool = Game1.player.CurrentTool;
            if (tool is not null)
            {
                var isWeapon = tool is MeleeWeapon;
                if (isWeapon)
                {
                    if (ModEntry.isDebugVerbose)
                        this.Monitor.Log("It seem we using weapon here");

                    isFinishHandling = handleWhenUsingWeapon(e.Button, (MeleeWeapon)tool);
                    if (isFinishHandling) return;
                }
            }

            if (e.Button == SButton.MouseRight)
            {
                //if (ModEntry.isDebugVerbose) this.Monitor.Log("Check if we can auto run here?");
                if (autoMovePrecheck())
                {
                    if (ModEntry.isDebugVerbose) this.Monitor.Log("Seem like we can auto run here");

                    ModEntry.isMovingAutomaticaly = true;
                    ModEntry.isBeingControl = false;
                    handleRightClickToMove(e.Button);
                }

                // We also let the game to handle right-click normally
                // If it within one tile randius vs the player
                bool isMouseWithinRadiusOfPlayer = Utility.withinRadiusOfPlayer((int)position_Destination.X, (int)position_Destination.Y, 1, Game1.player);

                if (!isMouseWithinRadiusOfPlayer)
                {
                    if (ModEntry.isDebugVerbose)
                        this.Monitor.Log(String.Format("Mouse target is outside hitbox range, at {0} and have {1} distance from player", position_Destination, vector_PlayerToMouse.Length()), LogLevel.Trace);

                    if (ModEntry.isDebugVerbose)
                        this.Monitor.Log("updateTarget for action handler", LogLevel.Trace);
                    this.actionHandler.updateTarget(ModEntry.position_Destination);
                    // We first will suppress that be havior by disable the right-click
                    // input
                    Helper.Input.Suppress(e.Button);
                }

                return;
            }

            // Our configured force move button should not break movement
            // Any button that not handled till now should break the movement
            // but only for a temprorary time
            if (!config.ForceMoveButton.IsDown())
            {
                ModEntry.isBeingControl = true;

                // When we is holding move, we allowing some acction that will not
                // break holding movement
                // This is for using tool.
                // Example use-case: Kept (hoding) moving and use tool to clear a path
                if (e.Button.IsUseToolButton())
                {
                    // Enough time for perform tool animation finish
                    ModEntry.tickCount = 15;
                    return;
                }
            }
        }

        private void MouseWheelScrolled(object sender, MouseWheelScrolledEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            if (!config.ExtendedModeDefault)
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

            if (!config.RightClickMoveModeDefault)
                return;

            position_MouseOnScreen = Game1.getMousePosition(Game1.uiMode).ToVector2();
        }

        private void ButtonReleasedMods(object sender, ButtonReleasedEventArgs e)
        {
            if (!config.RightClickMoveModeDefault)
                return;

            string button = e.Button.ToString();
            if (e.Button == Game1.options.runButton[0].ToSButton())
            {
                if (ModEntry.isDebugVerbose) this.Monitor.Log("End holding run button");
                ModEntry.isHoldingRunButton = false;
            }
        }

        public static void MoveVectorToCommand()
        {
            if (!ModEntry.isMovingAutomaticaly)
                return;

            if (ModEntry.isHoldingMove)
            {
                vector_AutoMove = vector_PlayerToMouse;

                Game1.player.movementDirections.Clear();
                if (vector_AutoMove.X <= 5 && vector_AutoMove.X >= -5)
                    vector_AutoMove.X = 0;
                if (vector_AutoMove.Y <= 5 && vector_AutoMove.Y >= -5)
                    vector_AutoMove.Y = 0;

                if (vector_AutoMove == new Vector2(0, 0))
                {
                    if (ModEntry.isDebugVerbose)
                    {
                        ModEntry.getMonitor().Log("Mouse too close, holding move stopped", LogLevel.Trace);
                    }
                    ModEntry.isMovingAutomaticaly = false;
                    return;
                }

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

                return;
            }

            // We following the path finding result
            if (ModEntry.isDebugVerbose)
            {
                //ModEntry.getMonitor().Log(String.Format("Follow path finding to {0} with direction {1}", pathFindingHelper.nextPath(), pathFindingHelper.moveDirection()), LogLevel.Trace);
            }

            vector_AutoMove = pathFindingHelper.moveDirection();

            if (vector_AutoMove == new Vector2(0, 0))
            {
                ModEntry.isMovingAutomaticaly = false;
                return;
            }

            // Some time, the destination is unreachable, but we will goes until
            // colision with the grab tiles, then try to facing toward it
            // before stop and perform action if needed
            if (pathFindingHelper.nextPath() is null && Game1.player.isColliding(Game1.player.currentLocation, Util.toTile(pathFindingHelper.originalDestination)))
            {
                if (ModEntry.isDebugVerbose) ModEntry.getMonitor().Log("Colling to grabTile");
                ModEntry.isMovingAutomaticaly = false;

                var facingVector = pathFindingHelper.moveDirection();
                if (facingVector.X > facingVector.Y)
                {
                    if (facingVector.X < 0)
                        Game1.player.SetMovingLeft(true);
                    else
                        Game1.player.SetMovingRight(true);
                }
                else
                {
                    if (facingVector.Y < 0)
                        Game1.player.SetMovingUp(true);
                    else
                        Game1.player.SetMovingDown(true);
                }
                return;
            }

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

        // Prefix Method return will control the base method execution
        // true mean base method will exec, false mean the opposite
        public static bool PrefixMethod_Farmer_HaltPatch()
        {
            // This let Halt work normally
            if (!config.RightClickMoveModeDefault)
                return true;

            // This will prevent any call to Halt which set the player stop
            // movement durring the auto movement
            return !ModEntry.isMovingAutomaticaly || ModEntry.isBeingAutoCommand;
        }

        public static bool PrefixMethod_Farmer_MovePositionPatch()
        {
            if (config.RightClickMoveModeDefault)
            {
                if (!ModEntry.isBeingControl && ModEntry.isMovingAutomaticaly && Context.IsPlayerFree && Game1.player.CanMove)
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
                if (!ModEntry.isBeingControl && Context.IsPlayerFree && Game1.player.CanMove)
                {
                    ModEntry.isBeingAutoCommand = true;
                    MoveVectorToCommand();
                    if (ModEntry.isHoldingRunButton && !Game1.player.canOnlyWalk)
                    {
                        Game1.player.setRunning(!Game1.options.autoRun, false);
                        Game1.player.setMoving(Game1.player.running ? (byte)16 : (byte)48);
                    }
                    else if (!ModEntry.isHoldingRunButton && !Game1.player.canOnlyWalk)
                    {
                        Game1.player.setRunning(Game1.options.autoRun, false);
                        Game1.player.setMoving(Game1.player.running ? (byte)16 : (byte)48);
                    }

                    ModEntry.isBeingAutoCommand = false;
                }
                else
                    isBeingAutoCommand = false;
            }
        }

        public static bool PrefixMethod_Farmer_getMovementSpeedPatch(ref float __result)
        {
            if (config.RightClickMoveModeDefault)
            {
                if (!ModEntry.isBeingControl && Context.IsPlayerFree)
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

        public static bool checkColidingIfMoving()
        {
            Rectangle playerBound = Game1.player.GetBoundingBox();

            var nextPositionX = playerBound.X + (int)Math.Floor(Game1.player.xVelocity);
            var nextPositionY = playerBound.Y - (int)Math.Floor(Game1.player.yVelocity);
            Rectangle nextPositionBound = new Rectangle(nextPositionX, nextPositionY, playerBound.Width, playerBound.Height);

            var nextPositionXCeil = playerBound.X + (int)Math.Ceiling(Game1.player.xVelocity);
            var nextPositionYCeil = playerBound.Y - (int)Math.Ceiling(Game1.player.yVelocity);
            Rectangle nextPositionCeil = new Rectangle(nextPositionXCeil, nextPositionYCeil, playerBound.Width, playerBound.Height);

            Rectangle nextPosition = Rectangle.Union(nextPositionBound, nextPositionCeil);

            return !Game1.player.currentLocation.isCollidingPosition(nextPosition, Game1.viewport, true, -1, false, Game1.player);
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
