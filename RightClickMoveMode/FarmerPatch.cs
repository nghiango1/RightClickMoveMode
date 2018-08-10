using RightClickMoveMode;
using Harmony;
using StardewValley;

namespace FarmerPatch
{
    [HarmonyPatch(typeof(Farmer)), HarmonyPatch("Halt")]
    internal class HaltPatch
    {
        public static bool Prefix(Game1 __instance)
        {
            if (ModEntry.isRightClickMoveModeOn)
                return !ModEntry.isMovingAutomaticaly || ModEntry.isBeingAutoCommand;
            else
                return true;
        }
    }
}
