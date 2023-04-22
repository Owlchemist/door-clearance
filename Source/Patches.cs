using HarmonyLib;
using Verse;
using RimWorld;
using System.Collections.Generic;
 
namespace DoorClearance
{
    [StaticConstructorOnStartup]
	public static class Setup
	{
        public static HashSet<ushort> doors = new HashSet<ushort>();
        static Setup()
        {
            new Harmony("owlchemist.doorclearance").PatchAll();
            var list = DefDatabase<ThingDef>.AllDefsListForReading;
            System.Type building_door = typeof(Building_Door);
            for (int i = list.Count; i-- > 0;)
            {
                var def = list[i];
                if (def.thingClass == building_door || (def.building != null && def.building.soundDoorOpenManual != null)) doors.Add(def.shortHash);
            }
        }
    }
    
    [HarmonyPatch(typeof(GenPlace), nameof(GenPlace.PlaceSpotQualityAt))]
    class Patch_GenPlace_PlaceSpotQualityAt
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(GenGrid), nameof(GenGrid.Walkable)),
				AccessTools.Method(typeof(Patch_GenPlace_PlaceSpotQualityAt), nameof(Validate)));
        }

        public static bool Validate(IntVec3 cell, Map map)
        {
            if (!cell.Walkable(map)) return false;

            var things = map.thingGrid.ThingsListAtFast(cell);
            var doors = Setup.doors;
            for (int i = things.Count; i-- > 0;)
            {
                if (doors.Contains(things[i].def.shortHash)) return false;
            }
            return true;
        }
    }
}