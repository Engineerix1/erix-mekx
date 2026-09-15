// using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Inventory;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using UnityEngine;

namespace ErixMekx.Items
{
    /// <summary>
    /// A test module that simulates a liquid cooling loop.
    /// It passively moves heat from the internal lungs atmosphere
    /// and dumps it into the external environment via convection.
    /// </summary>
    public class RadiatorModule : LoopModuleBase
    {
        public static GameString ImportLoop = GameString.Create("VentLoop", "Import ");
        public static GameString ExportLoop = GameString.Create("PumpLoop", "Export ");
        // public Event OnPressureChanged;
        // public Slot CircuitSlot => Slots[0];
        //TODO: Finish Circuit and look into adding a filter
        public override void OnTick()
        {
            // if (Loop == null || Loop.InternalAtmosphere == null) return;
            HandleLoopIntake();
            HandleConvection();
            HandleLoopReturn();
        }
        private void HandleLoopIntake()
        {
            // TODO: This could be handled with a pressure regulator style, with interacable buttons for player to set
            if (Importing == 0) return;

                // PerformPassiveGasTransfer(Loop.InternalAtmosphere, InternalAtmosphere);
                float flowRate = Loop.PumpRate * PumpingEfficiency(); // Simplified rate

                // Active return: If the module has power, it flows in
                if (Loop.RequestPower(-20f * flowRate))
                {
                    AtmosphereHelper.MoveVolume(
                        Loop.InternalAtmosphere,
                        InternalAtmosphere,
                        new VolumeLitres(flowRate),
                        AtmosphereHelper.MatterState.Gas,
                        MoleQuantity.Zero);
                }
        }
        private void HandleConvection()
        {
            if (WorldAtmosphere == null) return;

            // Simulate convection: Heat moves from the module's internal atmo to the world
            TemperatureKelvin tempDiff = InternalAtmosphere.Temperature - WorldAtmosphere.Temperature;

            MoleEnergy energyTransfer = new(tempDiff.ToFloat());

            InternalAtmosphere.GasMixture.RemoveEnergy(energyTransfer);
            WorldAtmosphere.GasMixture.AddEnergy(energyTransfer);
        }
        private void HandleLoopReturn()
        {
            if (Exporting == 0) return;
            Debug.Log("Loop out");

            float flowRate = Loop.PumpRate * PumpingEfficiency(); // Simplified rate

            // Active return: If the module has power, it flows back
            if (Loop.RequestPower(-100f * flowRate))
            {
                AtmosphereHelper.MoveVolume(
                    InternalAtmosphere,
                    Loop.InternalAtmosphere,
                    new VolumeLitres(flowRate),
                    AtmosphereHelper.MatterState.Liquid,
                    MoleQuantity.Zero);
            }
        }
        public float PumpingEfficiency()
        {
            // fluid density of liquid tank (higher density = harder to pump fluid)
            float densityLiquidFactor = AtmosphereHelper.GetDensityMilliMolesPerLire(Loop.InternalAtmosphere);

            // Pump_head factor (higher head/pressure = harder to pump fluid) 
            float pumpHeadFactor = (Loop.InternalAtmosphere.PressureGassesAndLiquids / InternalAtmosphere.PressureGassesAndLiquids).ToFloat();

            // Maitenance factor (low maintenance = less efficient)
            // OrganLungs.DamageEfficiency

            return Mathf.Clamp01((pumpHeadFactor * Loop.DamageEfficiency) / densityLiquidFactor) * (float)DifficultySetting.Current.BreathingRate;
        }

        // public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        // {
        //     ErixMekxMain.Log($"{interactable}:  State {interactable.State}");
        //     DelayedActionInstance delayedActionInstance = new()
        //     {
        //         Duration = 0f,
        //         ActionMessage = interactable.ContextualName
        //     };
        //     switch (interactable.Action)
        //     {
        // 	case InteractableType.Button1:
        // 		if (OutputSetting >= MaxSetting)
        // 		{
        // 			return delayedActionInstance.Fail(GameStrings.GlobalAlreadyMax);
        // 		}
        // 		if (!doAction)
        // 		{
        // 			return delayedActionInstance.Succeed();
        // 		}
        // 		// if (Loop.ParentEntity?.IsLocalPlayer is true)
        // 		// {
        // 		// 	UIAudioManager.Play(SuitButtonUpHash);
        // 		// }
        // 		if (GameManager.RunSimulation)
        // 		{
        // 			OutputSetting = Mathf.Min(OutputSetting + 1f, MaxSetting);
        // 		}
        //             OnPressureChanged?.Invoke();
        //             return delayedActionInstance.Succeed();
        // 	case InteractableType.Button2:
        // 		if (OutputSetting <= MinSetting)
        // 		{
        // 			return delayedActionInstance.Fail(GameStrings.GlobalAlreadyMin);
        // 		}
        // 		if (!doAction)
        // 		{
        // 			return delayedActionInstance.Succeed();
        // 		}
        // 		// if (Loop.ParentEntity?.IsLocalPlayer is true)
        // 		// {
        // 		// 	UIAudioManager.Play(SuitButtonDownHash);
        // 		// }
        // 		if (GameManager.RunSimulation)
        // 		{
        // 			OutputSetting = Mathf.Max(OutputSetting - 1f, MinSetting);
        // 		}
        //             OnPressureChanged?.Invoke();
        //             return delayedActionInstance.Succeed();
        //         default:
        //             return base.InteractWith(interactable, interaction, doAction);
        //     }
        // }
        public override string GetContextualName(Interactable interactable)
        {
            return interactable.Action switch
            {
                InteractableType.Import => ImportLoop + ((Importing == 0) ? ActionStrings.Off : ActionStrings.On),
                InteractableType.Export => ExportLoop + ((Exporting == 0) ? ActionStrings.Off : ActionStrings.On),
                _ => base.GetContextualName(interactable),
            };
        }
    }
}
