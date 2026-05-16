using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.UI;
using ErixMekx.Organs;
using ErixMekx.UI;
using HarmonyLib;
using UnityEngine;

namespace ErixMekx.Patches
{

    [HarmonyPatch(typeof(Human))]
    public static class HumanPatch
    {
        [HarmonyPatch(nameof(Human.CreateLungs))]
        [HarmonyPostfix]
        public static void CreateCoolantLungs(Human __instance)
        {
            // 1. Check if we are targeting a robot (Artificial species)
            if (!__instance.IsArtificial)
                return;

            // 2. Execute custom lung creation logic here
            LungsRobot loop = OnServer.Create<LungsRobot>(Prefab.Find<Lungs>("OrganLungsRobot"), __instance.LungsSlot);

            const Chemistry.GasType initCoolant = Chemistry.GasType.Water;
            MoleQuantity quantity = new((loop.InternalAtmosphere.Volume * 0.5 / Mole.MolarVolume(initCoolant)).ToDouble());
            MoleEnergy energy = new(Chemistry.Temperature.TwentyDegrees, Mole.SpecificHeat(initCoolant), quantity);
            GasMixture coolant = new(new Mole(initCoolant, quantity, energy));
            AtmosphericEventInstance.CreateAdd(atmosphere: loop.InternalAtmosphere, gasMixture: coolant);
        }
    }

    [HarmonyPatch(typeof(Brain))]
    public static class BrainPatch
    {
        [HarmonyPatch("OnLifeTick")]
        [HarmonyPrefix]
        public static void OnLifeTickBrainPatch(Brain __instance)
        {
            if (__instance.ParentHuman?.IsArtificial is not true) return;

            bool flag = __instance.ParentEntity is Npc;
            float num2 = ((flag || __instance.IsOnline) ? 3f : (3f * Mathf.Clamp((float)DifficultySetting.Current.OfflineMetabolism, 0.1f, 1f)));
            if (__instance.ParentHuman.OrganLungs?.HasAtmosphere is not true)
                __instance.DamageState.Damage(ChangeDamageType.Increment, num2, DamageUpdateType.Stun);
        }
    }

    [HarmonyPatch(typeof(PlayerStateWindow))]
    public static class PlayerStateWindowPatch
    {
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        public static void UpdatePostfix(PlayerStateWindow __instance)
        {
            // Delegate all UI work to the Manager
            // TODO Possibly check for presence of lungs and remove if not ours or not present
            if (__instance.Parent?.IsArtificial is not true) { RobotUIManager.Instance.Cleanup(); return; }
            if (!RobotUIManager.Instance.IsSetup) RobotUIManager.Instance.SetupRobotUI();
            RobotUIManager.Instance.SetVisible(true);
        }
    }

}
