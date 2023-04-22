using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;
using System;
using System.Reflection.Emit;
 
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
            Type building_door = typeof(Building_Door);
            for (int i = list.Count; i-- > 0;)
            {
                var def = list[i];
                var thingClass = def.thingClass;
                if (thingClass == building_door || thingClass.IsSubclassOf(building_door) || thingClass.Name.Contains("DoorsExpanded")) doors.Add(def.shortHash);
            }
        }
    }
    
    [HarmonyPatch(typeof(HaulAIUtility), "HaulablePlaceValidator")]
    class Patch_HaulAIUtility_HaulablePlaceValidator
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var method = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetEdifice));
            int offset = 0;
            foreach (var instruction in instructions)
            {
                if (offset > 0 && offset-- == 1)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_HaulAIUtility_HaulablePlaceValidator), nameof(Validate)));
                    yield return instruction; //If not filtered then just transfer control to normal vanilla handling
			        yield return new CodeInstruction(OpCodes.Ldc_I4_0); //Otherwise, push false to the return
			        yield return new CodeInstruction(OpCodes.Ret);
                    offset = -1;
                    continue;
                }
                if (offset < 0 && offset-- > -6) continue; //Skip next 5 instructions, no longer needed
                if (offset == 0 && instruction.opcode == OpCodes.Call && instruction.OperandIs(method))
                {
                    offset = 3;
                }
                yield return instruction;
            }
            if (offset >= 0) Log.Error("[Door Clearance] Patch_HaulAIUtility_HaulablePlaceValidator transpiler failed to find its target. Did RimWorld update?");
        }
        public static bool Validate(Building edifice)
        {
            if (edifice == null) return false;
            if (Setup.doors.Contains(edifice.def.shortHash) || edifice is Building_Trap) return true;
            return false;
        }
    }
    
    [HarmonyPatch(typeof(GenPlace), "PlaceSpotQualityAt")]
    class Patch_GenPlace_PlaceSpotQualityAt
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(AccessTools.Method(typeof(GenGrid), nameof(GenGrid.Walkable)),
				AccessTools.Method(typeof(Patch_GenPlace_PlaceSpotQualityAt), nameof(Validate)));
        }

        public static bool Validate(IntVec3 cell, Map map)
        {
            var index = map.cellIndices.CellToIndex(cell);
            if (!map.pathing.Normal.pathGrid.WalkableFast(index) || !map.pathing.FenceBlocked.pathGrid.WalkableFast(index)) return false;

            var things = map.thingGrid.ThingsListAtFast(index);
            var doors = Setup.doors;
            for (int i = things.Count; i-- > 0;)
            {
                if (doors.Contains(things[i].def.shortHash)) return false;
            }
            return true;
        }
    }
}