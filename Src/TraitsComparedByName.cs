using System;
using System.Collections.Generic;
using System.Reflection;
using Experience;
using FinePrint.Utilities;
using HarmonyLib;

namespace com.github.lhervier.ksp.fix.constructionlaser
{
    /// <summary>
    /// Makes the stock code that looks for a kerbal's trait compare its internal name ("Engineer"), the same in every
    /// language, instead of its title, translated in a game that is not in English. On stock, in such a game, an
    /// engineer is never recognised as one: the EVA construction mode does not weld the parts an engineer attaches
    /// (no turn to the part, no visor, no laser) and writes "Kerbal is not an engineer!" to KSP.log, and the
    /// contracts that ask for a trait aboard never find it.
    /// </summary>
    internal static class TraitsComparedByName
    {
        private static readonly MethodInfo TypeNameGetter =
            AccessTools.PropertyGetter(typeof(ExperienceTrait), nameof(ExperienceTrait.TypeName));
        private static readonly MethodInfo InternalNameMethod =
            AccessTools.Method(typeof(TraitsComparedByName), nameof(InternalName));

        // The stock methods that compare TypeName to a trait name, with the number of comparisons each one makes.
        // A method missing, or with another count, means another game version, and is left alone.
        private static readonly KeyValuePair<MethodInfo, int>[] Comparers =
        {
            // KerbalEVA.Weld (the laser), EVAConstructionModeEditor (engineers who help lift), contracts.
            Comparer(AccessTools.Method(typeof(VesselUtilities), nameof(VesselUtilities.VesselCrewWithTraitCount),
                new[] { typeof(string), typeof(Vessel) }), 1),
            Comparer(AccessTools.Method(typeof(VesselUtilities), nameof(VesselUtilities.VesselCrewWithTrait),
                new[] { typeof(string), typeof(Vessel) }), 1),
            // Crew requests of the contracts.
            Comparer(AccessTools.Method(typeof(VesselUtilities), nameof(VesselUtilities.CrewTraitMissionAvailability),
                new[] { typeof(string), typeof(CelestialBody), typeof(Vessel) }), 2),
            // Its early exit when the kerbal already has the trait asked for.
            Comparer(AccessTools.Method(typeof(KerbalRoster), nameof(KerbalRoster.SetExperienceTrait),
                new[] { typeof(ProtoCrewMember), typeof(string) }), 1),
        };

        // Comparisons replaced by the transpiler in the method being patched.
        private static int replaced;

        /// <summary>
        /// Patches every stock comparison of a trait name it knows. A method missing, or whose comparisons are not
        /// the ones expected, is left as on stock, with a warning.
        /// </summary>
        /// <returns>The number of methods patched.</returns>
        /// <exception cref="MissingMemberException"><c>ExperienceTrait.TypeName</c> does not exist.</exception>
        public static int Install(Harmony harmony)
        {
            if (TypeNameGetter == null)
            {
                throw new MissingMemberException("ExperienceTrait.TypeName");
            }
            int patched = 0;
            foreach (KeyValuePair<MethodInfo, int> comparer in Comparers)
            {
                if (comparer.Key == null)
                {
                    Log.Warning("A stock method that compares trait names was not found, left as on stock");
                    continue;
                }
                string name = $"{comparer.Key.DeclaringType.Name}.{comparer.Key.Name}";
                // Counted first, without patching: a method that does not look as expected is left untouched.
                int found = CountTypeNameReads(comparer.Key);
                if (found != comparer.Value)
                {
                    Log.Warning($"{name} reads the trait name {found} time(s) instead of {comparer.Value}, left as on stock");
                    continue;
                }
                replaced = 0;
                harmony.Patch(comparer.Key,
                    transpiler: new HarmonyMethod(typeof(TraitsComparedByName), nameof(Transpiler)));
                Log.Debug($"{name}: {replaced} comparison(s) by internal name");
                patched++;
            }
            return patched;
        }

        /// <summary>
        /// Returns the name a trait is compared by: the stock <c>ExperienceTrait.TypeName</c>, except that a trait
        /// of the base class gives its internal name (<c>Config.Name</c>) instead of its translated title.
        /// </summary>
        public static string InternalName(ExperienceTrait trait)
        {
            // Stock TypeName: the title for the base class (every stock trait), the class name for a subclass
            // (traits of mods). Only the first one depends on the language.
            Type type = trait.GetType();
            return type == typeof(ExperienceTrait) ? trait.Config.Name : type.Name;
        }

        /// <summary>Returns how many times <paramref name="method"/> reads <c>ExperienceTrait.TypeName</c>.</summary>
        private static int CountTypeNameReads(MethodBase method)
        {
            int count = 0;
            foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(method))
            {
                if (instruction.Calls(TypeNameGetter))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>Replaces each read of <c>ExperienceTrait.TypeName</c> with <see cref="InternalName"/>.</summary>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(TypeNameGetter))
                {
                    // Same stack in and out: the trait, then a string. Labels and blocks stay on the instruction.
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = InternalNameMethod;
                    replaced++;
                }
                yield return instruction;
            }
        }

        private static KeyValuePair<MethodInfo, int> Comparer(MethodInfo method, int comparisons)
        {
            return new KeyValuePair<MethodInfo, int>(method, comparisons);
        }
    }
}
