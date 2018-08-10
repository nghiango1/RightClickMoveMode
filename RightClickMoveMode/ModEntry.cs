//This Version forcus in pathfinding and optimal for following path
//      Which mean, player go by tile, a 32fx32f rectangle
//
//What make me stop complete this ver
//  - Not worth the time invest (Need a lot of test and optimal, this ver can causse the game frezze becus using + alocate too many memory)
//
//  - PathFinding move mode is not comfotable for using (The fact that when using the path finding, I'm not comfotable with the control
//  - PathFinding move mode is Not comfotable for using (may become better if set to Double click or something  instead of defaut using
//  - PathFinding move mode is Not comfotable for using (Feel anoying to use
//  - PathFinding move mode is Not comfotable for using
//  - Not comfotable for using (Honess, this not that great for this game)




using System;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Harmony;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using EpPathFinding;
using EpPathFinding.cs;

namespace RightClickMoveMode
{
    public class ModEntry : Mod
    {
        //private Config Config;

        public const float hitboxRadius = 64f * 2;

        public static bool isRightClickMoveModeOn = true;

        public static bool isUsingEpPathFinding = false; // Set true if you want to use, this not optimal and cause the game to frezze
                                                         //  and was being drop (be me :| ), You can complete if you want



        public static bool isMovingAutomaticaly = false;  //isMovingAutomaticaly True: When the Destination are set/ or when the Holding the Right mouse
                                                          //                        as long as this on the Farmer will moving by him self
                                                          //isMovingAutomaticaly False: When player are at the Destination, and player not Holding the Right mouse
        public static bool isBeingAutoCommand = false;  //isBeingAutoCommand True: When command are being set, this turn to True to make sure no other Threat changing move command -> lead to Farmmer animation such
                                                        //isBeingAutoCommand False: New command can be set and Farmer animation iis still smooth
        public static bool isMouseOutsiteHitBox = false;

        public static bool isBeingControl = false;

        public static bool isHoldingMove = false;

        public static bool pathDone = true; // When path was found, this set to True
                                            // When player at the last node of the path, this is set False

        private static Vector2 vector_PlayerToDestination;  // All 3 vector is Pixel base, not Tile
        private static Vector2 vector_PlayerToMouse;
        private static Vector2 vector_AutoMove; //  This vector define which move command should use

        private static Vector2 position_MouseOnScreen;
        private static Vector2 position_Source; // All 3 Posstion is Pixel, not Tile
        private static Vector2 position_Destination;


        private static int tickCount = 15;
        
        //Variable Path finding with ingame StardewValley.PathFindController.findPathForNPCSchedules() function : Which not take care alot of can't walkthough tile
        public static Stack<Point> path; // The path found store
                                         //  Criitical:: This is the variable that EpPathFinding is using for lower the code needed
        public static Point node; 

        //Flag for debug mode
        public static bool isDebugMode = false;

        //Variable for EpPathFinding
        public EpPathFinding.cs.StaticGrid staticGrid; // Grid Map
        private JumpPointParam iParam;  //  Variable store Flag for path finding(EndNodeUnWalkableTreatment, DiagonalMovement,...  etc)+ store BeginPos and EndPos of the path
        private List<GridPos> pathGridPos; // The path fount store

        public override void Entry(IModHelper helper)
        {
            InputEvents.ButtonPressed += this.InputEvents_ButtonPressed;
            ControlEvents.MouseChanged += this.ControlEvents_MouseChanged;
            InputEvents.ButtonReleased += this.InputEvents_ButtonReleased;
            GameEvents.UpdateTick += this.GameEvents_UpdateTick;
            PlayerEvents.Warped += this.PlayerEvents_Warped;

            LocationEvents.ObjectsChanged += this.LocationEvents_ObjectsChanged;

            HarmonyInstance harmony = HarmonyInstance.Create("RightClickMoveMode.UpdateControlInputPatch");
            harmony.PatchAll();

            position_MouseOnScreen = new Vector2(0f, 0f);
            position_Source = new Vector2(0f, 0f);
            vector_PlayerToDestination = new Vector2(0f, 0f);
        }

        // To update the map when player place object (funiture ... ), used in EpPathFinding
        private void LocationEvents_ObjectsChanged(object sender, EventArgsLocationObjectsChanged e)
        {
            //No code yet
        }


        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            bool flag = Context.IsWorldReady;
            if (flag)
            {
                if (isRightClickMoveModeOn)
                {
                    if (Context.IsPlayerFree)
                    {
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
                        else if (!pathDone)
                        {
                            isMovingAutomaticaly = true; // When path not done, this alway set to on, make sure player not stop when not going though all the path
                            if (path.Count != 0)
                            {
                                if (IsAtTile())
                                {
                                    node = path.Pop();
                                    vector_PlayerToDestination.X = node.X * 64f - Game1.player.Position.X;
                                    vector_PlayerToDestination.Y = node.Y * 64f - Game1.player.Position.Y;
                                    
                                }
                            }
                            else if (IsAtTile())
                            {
                                pathDone = true;
                            }
                            vector_PlayerToDestination.X = node.X * 64f - Game1.player.Position.X;
                            vector_PlayerToDestination.Y = node.Y * 64f - Game1.player.Position.Y;
                        }

                        vector_PlayerToMouse.X = position_MouseOnScreen.X + Game1.viewport.X - Game1.player.Position.X - 32f;
                        vector_PlayerToMouse.Y = position_MouseOnScreen.Y + Game1.viewport.Y - Game1.player.Position.Y - 10f;
                        
                    }
                }
            }
        }


        private void PlayerEvents_Warped(object sender, EventArgsPlayerWarped e)
        {
            isMovingAutomaticaly = false;
            pathDone = true;

            // Basic map loading, init the grid map for EpPathFinding to work
            if (isUsingEpPathFinding)
            {
                xTile.Map map = e.NewLocation.Map;

                int height = Convert.ToInt32(map.DisplayHeight / 32);
                int width = Convert.ToInt32(map.DisplayWidth / 32);

                staticGrid = new StaticGrid(height, width);

                for (int i = 0; i < height; i++)
                    for (int j = 0; j < width; j++)
                    {
                        staticGrid.SetWalkableAt(i, j, // Flag to check if the tile is passable (isTilePassable, isObjectAtTile, isBehindBush, ... )
                                e.NewLocation.isTilePassable(new xTile.Dimensions.Location(i, j), new xTile.Dimensions.Rectangle(i, j, 32, 32)) // This not enough, even this True: Tile still can't passable ( :D ??), Funiture/ Tree/ Rock ... not even count
                                && !e.NewLocation.isObjectAtTile(i, j)  // Random flag is being test
                                && !e.NewLocation.isBehindBush(new Vector2((float)i, (float)j)) // Need more flag for Bush
                            );
                    }

                iParam = new JumpPointParam(staticGrid, EndNodeUnWalkableTreatment.ALLOW, DiagonalMovement.Never);
            }
        }

        private void InputEvents_ButtonPressed(object sender, EventArgsInput e)
        {
            bool flag = Context.IsWorldReady;

            if (flag)
            {
                string button = e.Button.ToString();

                bool flag2 = button == "G";
                if (flag2)
                {
                    isRightClickMoveModeOn = !isRightClickMoveModeOn;
                }

                bool flag3 = button == "MouseRight" && isRightClickMoveModeOn && Context.IsPlayerFree;
                if (flag3)
                {
                    ModEntry.isMovingAutomaticaly = true;
                    isHoldingMove = true;
                    isBeingControl = false;
                    isMouseOutsiteHitBox = vector_PlayerToMouse.Length().CompareTo(hitboxRadius) > 0;

                    if (isDebugMode)
                    {
                        base.Monitor.Log(string.Format("isMouseOutsiteHitBox = {0} ", isMouseOutsiteHitBox), LogLevel.Debug);
                    }

                    if (isMouseOutsiteHitBox)
                    {
                        e.SuppressButton();
                    }
                }
                else
                {
                    if (e.IsUseToolButton)
                    {
                        tickCount = 15;
                    }
                    isBeingControl = true;
                }

            }
        }

        private void ControlEvents_MouseChanged(object sender, StardewModdingAPI.Events.EventArgsMouseStateChanged e)
        {
            bool flag = Context.IsWorldReady;

            if (flag)
            {
                if (isRightClickMoveModeOn)
                {
                    if (Context.IsPlayerFree)
                    {
                        position_MouseOnScreen.X = (float)e.NewPosition.X;
                        position_MouseOnScreen.Y = (float)e.NewPosition.Y;
                    }
                }
            }
        }

        private void InputEvents_ButtonReleased(object sender, EventArgsInput e)
        {
            bool flag = Context.IsWorldReady;
            if (flag)
            {
                string button = e.Button.ToString();
                if (isRightClickMoveModeOn)
                {
                    bool flag2 = button == "MouseRight" && isHoldingMove;

                    if (flag2)
                    {
                        isHoldingMove = false;

                        position_Destination.X = (float)e.Cursor.ScreenPixels.X + Game1.viewport.X;
                        position_Destination.Y = (float)e.Cursor.ScreenPixels.Y + Game1.viewport.Y;

                        position_Source.X = Game1.player.Position.X + 32f;
                        position_Source.Y = Game1.player.Position.Y + 10f;

                        vector_PlayerToDestination.X = position_Destination.X - Game1.player.Position.X - 32f;
                        vector_PlayerToDestination.Y = position_Destination.Y - Game1.player.Position.Y - 10f;
                        
                        if (isUsingEpPathFinding && iParam != null)
                        {
                            iParam.Reset( //StartPos = player tile location, EndPos = Cursor tile Location
                                    new GridPos(Convert.ToInt32(Game1.player.getTileLocation().X), Convert.ToInt32(Game1.player.getTileLocation().Y)),
                                    new GridPos(Convert.ToInt32(Game1.currentCursorTile.X), Convert.ToInt32(Game1.currentCursorTile.Y))
                                );

                            pathGridPos = EpPathFinding.cs.JumpPointFinder.FindPath(iParam);
                            path = new Stack<Point>(); 

                            if (pathGridPos != null)
                                for (int i = pathGridPos.Count - 1 ; i >= 0; i--)
                                {
                                    path.Push(new Point(pathGridPos[i].x, pathGridPos[i].y));
                                }
                        }
                        else
                        {
                            path = StardewValley.PathFindController.findPathForNPCSchedules(
                                new Point(Convert.ToInt32(Game1.player.getTileLocation().X), Convert.ToInt32(Game1.player.getTileLocation().Y)),
                                new Point(Convert.ToInt32(Game1.currentCursorTile.X), Convert.ToInt32(Game1.currentCursorTile.Y)),
                                Game1.player.currentLocation,
                                500);
                        }

                        if (path.Count != 0)
                        {
                            node = path.Pop();
                            if (path.Count != 0) node = path.Pop();
                            pathDone = false;
                        }
                    }
                }
            }
        }


        //This checking the Farmer are in the detination yet (stand in side the tile)
        public bool IsAtTile()
        {
            bool flag = vector_PlayerToDestination.X.CompareTo(5f) < 0 && vector_PlayerToDestination.Y.CompareTo(-5f) < 0;
            flag = flag && vector_PlayerToDestination.X.CompareTo(-5f) > 0 && vector_PlayerToDestination.Y.CompareTo(-25f) > 0;
            return flag;
        }

        //This provide the moving command to Farmer (optimal for stand in side the tile)
        public static void MoveVectorToCommand()
        {
            bool flag = ModEntry.isMovingAutomaticaly;
            
            if (flag)
            {
                if (isHoldingMove)
                {
                    vector_AutoMove.X = vector_PlayerToMouse.X;
                    vector_AutoMove.Y = vector_PlayerToMouse.Y;
                }
                else
                {
                    vector_AutoMove.X = vector_PlayerToDestination.X;
                    vector_AutoMove.Y = vector_PlayerToDestination.Y;
                }
                
                Game1.player.movementDirections.Clear();
                
                if (vector_AutoMove.X <= 5 && vector_AutoMove.X >= -5) { }
                else if (vector_AutoMove.X >= 5)
                {
                    Game1.player.SetMovingRight(true);
                }
                else if (vector_AutoMove.X <= -5)
                {
                    Game1.player.SetMovingLeft(true);
                }

                if (vector_AutoMove.Y <= -5 && vector_AutoMove.Y >= -25) { }
                else if (vector_AutoMove.Y >= -5)
                {
                    Game1.player.SetMovingDown(true);
                }
                else if (vector_AutoMove.Y <= -25)
                {
                    Game1.player.SetMovingUp(true);
                }

                if (vector_PlayerToDestination.X.CompareTo(5f) < 0 && vector_PlayerToDestination.X.CompareTo(-5f) > 0
                     && vector_PlayerToDestination.Y.CompareTo(-5f) < 0 && vector_PlayerToDestination.Y.CompareTo(-25f) > 0)
                    ModEntry.isMovingAutomaticaly = false;
            }
        }
    }
}

