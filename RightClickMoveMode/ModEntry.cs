using System;
using StardewValley;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Harmony;
using Microsoft.Xna.Framework;
using System.Reflection;

namespace RightClickMoveMode
{
    public class ModEntry : Mod
    {
        //private Config Config;

        public const float hitboxRadius = 64f * 2;

        public static bool isRightClickMoveModeOn = true;
        public static bool isExtendedModeOn = true;
        
        public static bool isMovingAutomaticaly = false;  
        public static bool isBeingAutoCommand = false; 
        public static bool isMouseOutsiteHitBox = false;

        public static bool isBeingControl = false;

        public static bool isHoldingMove = false;

        public static bool isHoldingLeftCtrl = false;
        public static bool isHoldingRightCtrl = false;
        public static bool isHoldingRightAlt = false;
        public static bool isWheeling = false;
        

        private static Vector2 vector_PlayerToDestination;  
        private static Vector2 vector_PlayerToMouse;
        private static Vector2 vector_AutoMove; 

        private static Vector2 position_MouseOnScreen;
        private static Vector2 position_Source;
        private static Vector2 position_Destination;
        
        private static int tickCount = 15;

        private static int currentToolIndex = 1;
        
        public static bool isDebugMode = false;
        
        public override void Entry(IModHelper helper)
        {
            InputEvents.ButtonPressed += this.InputEvents_ButtonPressed;
            ControlEvents.MouseChanged += this.ControlEvents_MouseChanged;
            InputEvents.ButtonReleased += this.InputEvents_ButtonReleased;
            GameEvents.UpdateTick += this.GameEvents_UpdateTick;
            PlayerEvents.Warped += this.PlayerEvents_Warped;

            StartPatching();

            position_MouseOnScreen = new Vector2(0f, 0f);
            position_Source = new Vector2(0f, 0f);
            vector_PlayerToDestination = new Vector2(0f, 0f);
        }
        


        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            bool flag = Context.IsWorldReady;
            if (flag)
            {
                if (isExtendedModeOn)
                {
                    if ((isHoldingLeftCtrl || isHoldingRightCtrl) && isWheeling)
                    {
                        Game1.player.CurrentToolIndex = currentToolIndex;
                    }
                }

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
                        else
                        {
                            vector_PlayerToDestination.X = position_Destination.X - Game1.player.Position.X - 32f;
                            vector_PlayerToDestination.Y = position_Destination.Y - Game1.player.Position.Y - 10f;
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
        }

        private void InputEvents_ButtonPressed(object sender, EventArgsInput e)
        {
            bool flag = Context.IsWorldReady;
            string button = e.Button.ToString();
            
            if (button == "G")
            {
                isRightClickMoveModeOn = !isRightClickMoveModeOn;
            }
            if (button == "H")
            {
                isExtendedModeOn = !isExtendedModeOn;
            }

            if (isExtendedModeOn)
            {
                if (button == "RightControl")
                {
                    isHoldingRightCtrl = true;
                }
                if (button == "LeftControl")
                {
                    isHoldingLeftCtrl = true;
                }
                if (button == "RightAlt")
                {
                    isHoldingRightAlt = true;
                }

                if (button == "Enter" && isHoldingRightAlt)
                {
                    if (Game1.options.isCurrentlyWindowedBorderless())
                        Game1.options.setWindowedOption("Windowed");
                    else
                    {
                        Game1.options.setWindowedOption("Windowed Borderless");
                    }
                    Game1.exitActiveMenu();
                }
            }

            if (flag)
            {
                bool flag2 = button == "MouseRight" && isRightClickMoveModeOn && Context.IsPlayerFree;

                if (Game1.player.ActiveObject != null)
                    flag2 = flag2 && !(Game1.player.ActiveObject.getCategoryName() == "Furniture");

                if (flag2)
                {
                    currentToolIndex = Game1.player.CurrentToolIndex;

                    ModEntry.isMovingAutomaticaly = true;
                    isHoldingMove = true;
                    isBeingControl = false;
                    isMouseOutsiteHitBox = vector_PlayerToMouse.Length().CompareTo(hitboxRadius) > 0;

                    bool flag3 = false;
                    flag3 = flag3 && isMouseOutsiteHitBox;

                    if (flag3)
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
                    else
                        tickCount = 0;
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

                if (isExtendedModeOn)
                {
                    if (e.PriorState.ScrollWheelValue != e.NewState.ScrollWheelValue)
                        isWheeling = true;
                    else
                        isWheeling = false;

                    if (isHoldingLeftCtrl || isHoldingRightCtrl)
                    {
                        if (e.PriorState.ScrollWheelValue < e.NewState.ScrollWheelValue)
                        {
                            currentToolIndex = Game1.player.CurrentToolIndex;
                            if (Game1.options.zoomLevel <= Options.maxZoom)
                                Game1.options.zoomLevel += 0.05f;
                            Game1.exitActiveMenu();
                        }
                        else if (e.PriorState.ScrollWheelValue > e.NewState.ScrollWheelValue)
                        {
                            currentToolIndex = Game1.player.CurrentToolIndex;
                            if (Game1.options.zoomLevel >= Options.minZoom)
                                Game1.options.zoomLevel -= 0.05f;
                            Game1.exitActiveMenu();
                        }
                    }
                }
            }
        }

        private void InputEvents_ButtonReleased(object sender, EventArgsInput e)
        {
            bool flag = Context.IsWorldReady;

            string button = e.Button.ToString();

            if (isExtendedModeOn)
            {
                if (button == "RightControl")
                {
                    isHoldingRightCtrl = false;
                }

                if (button == "LeftControl")
                {
                    isHoldingLeftCtrl = false;
                }

                if (button == "RightAlt")
                {
                    isHoldingRightAlt = false;
                }
            }

            if (flag)
            {
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
                    }
                }
            }
        }
        
        public static void MoveVectorToCommand()
        {
            bool flag = ModEntry.isMovingAutomaticaly;
            bool flag2 = false;
            bool flag3 = false;
            
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
                if (vector_AutoMove.X <= 5 && vector_AutoMove.X >= -5)
                    flag2 = true;
                else if (vector_AutoMove.X >= 5)
                    Game1.player.SetMovingRight(true);
                else if (vector_AutoMove.X <= -5)
                    Game1.player.SetMovingLeft(true);

                if (vector_AutoMove.Y <= 5 && vector_AutoMove.Y >= -5)
                    flag3 = true;
                else if (vector_AutoMove.Y >= 5)
                    Game1.player.SetMovingDown(true);
                else if (vector_AutoMove.Y <= -5)
                    Game1.player.SetMovingUp(true);

                vector_AutoMove.Normalize();

                if (Game1.player.movementDirections.Count == 2)
                {
                    if (Math.Abs(vector_AutoMove.Y / vector_AutoMove.X).CompareTo(0.45f) < 0)
                    {
                        Game1.player.SetMovingDown(false);
                        Game1.player.SetMovingUp(false);
                    }
                    else if (Math.Abs(vector_AutoMove.Y) > Math.Sin(Math.PI/3))
                    {
                        Game1.player.SetMovingRight(false);
                        Game1.player.SetMovingLeft(false);
                    }
                }

                if (flag2 && flag3)
                {
                    ModEntry.isMovingAutomaticaly = false;
                }
            }
        }

        public static void StartPatching()
        {
            HarmonyInstance newHarmony = HarmonyInstance.Create("ylsama.RightClickMoveMode");

            MethodInfo farmerInfo = AccessTools.Method(typeof(Farmer), "Halt");
            MethodInfo farmerHaltPrefix = AccessTools.Method(typeof(ModEntry), "PrefixMethod_FarmerPatch");

            MethodInfo game1Info = AccessTools.Method(typeof(Game1), "UpdateControlInput", new Type[] { typeof(GameTime) });
            MethodInfo game1HaltPostfix = AccessTools.Method(typeof(ModEntry), "PostfixMethod_Game1Patch");

            newHarmony.Patch(farmerInfo, new HarmonyMethod(farmerHaltPrefix));
            newHarmony.Patch(game1Info, null, new HarmonyMethod(game1HaltPostfix));
        }

        public static bool PrefixMethod_FarmerPatch(Game1 __instance)
        {
            if (isRightClickMoveModeOn)
            {
                if (!isMovingAutomaticaly || isBeingAutoCommand)
                    return true;
                else
                    return false;
            }
            else
                return true;
        }

        public static void PostfixMethod_Game1Patch(Game1 __instance)
        {
            if (ModEntry.isRightClickMoveModeOn)
            {
                if (!ModEntry.isBeingControl && Context.IsPlayerFree)
                {
                    ModEntry.isBeingAutoCommand = true;
                    ModEntry.MoveVectorToCommand();
                    Game1.player.running = true;
                    ModEntry.isBeingAutoCommand = false;
                }
                else
                    ModEntry.isBeingAutoCommand = false;
            }
        }
    }   
}

