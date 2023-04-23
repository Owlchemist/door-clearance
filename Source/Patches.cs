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
		static Setup()
		{
			new Harmony("owlchemist.doorclearance").PatchAll();
			var list = DefDatabase<ThingDef>.AllDefsListForReading;
			Type building_door = typeof(Building_Door);
			for (int i = list.Count; i-- > 0;)
			{
				var def = list[i];
				var thingClass = def.thingClass;
				if (thingClass == building_door || thingClass.IsSubclassOf(building_door) || thingClass.Name.Contains("DoorsExpanded")) HarmonyPatches.doors.Add(def.shortHash);
			}
			if (Prefs.DevMode) Log.Message("[Door Clearance] Cache built with a size of " + HarmonyPatches.doors.Count);
		}
	}

	public static class HarmonyPatches
	{
		public static HashSet<ushort> doors = new HashSet<ushort>();

		[HarmonyPatch(typeof(HaulAIUtility), "HaulablePlaceValidator")]
		class Patch_HaulAIUtility_HaulablePlaceValidator
		{
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var getEdifice = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetEdifice));
				var validate = AccessTools.Method(typeof(Patch_HaulAIUtility_HaulablePlaceValidator), nameof(Validate));
				var editor = new CodeMatcher(instructions);
				// --------------------------ORIGINAL--------------------------
				// Building edifice = c.GetEdifice(worker.Map);
				// if (edifice != null && edifice is Building_Trap){...
				editor.Start().MatchEndForward(
					new CodeMatch(OpCodes.Call, getEdifice),
					new CodeMatch(OpCodes.Stloc_0), // pushes result to local
					new CodeMatch(OpCodes.Ldloc_0) // pops local as arg to next call
				);
				if (!editor.IsInvalid)
				{
					// --------------------------MODIFIED--------------------------
					// Building edifice = c.GetEdifice(worker.Map);
					// if (patch.Validate(edifice1)){...
					return editor
					.Advance(1) // Our start position is the last instruction of our match, move ahead one
					.InsertAndAdvance(new CodeInstruction(OpCodes.Call, validate))
					.Advance(1) // Leave the branch instruction intact
					.RemoveInstructions(3) // Remove the remaining `if` qualifiers
					.InstructionEnumeration();
				}
				
				Log.Error("[Door Clearance] Patch_HaulAIUtility_HaulablePlaceValidator transpiler failed to find its target. Did RimWorld update?");
				return editor.InstructionEnumeration();
			}
			public static bool Validate(Building edifice)
			{
				if (edifice == null) return false;
				return doors.Contains(edifice.def.shortHash) || edifice is Building_Trap;
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
				for (int i = things.Count; i-- > 0;)
				{
					if (doors.Contains(things[i].def.shortHash)) return false;
				}
				return true;
			}
		}
	}
}