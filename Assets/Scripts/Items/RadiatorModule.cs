using Assets.Scripts.Atmospherics;
using Assets.Scripts.Inventory;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using ErixMekx.Organs;
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
        public static readonly GameString VentLoop = GameString.Create("VentLoop", "Vent ");
        public bool CanVent => InternalAtmosphere != null && TargetAtmosphere != null;

        public static readonly GameString PumpLoop = GameString.Create("PumpLoop", "Pump ");
        public bool CanPump => SourceAtmosphere != null && InternalAtmosphere != null;
        public Slot CircuitSlot => Slots[0];
        public Slot IntakeSlot => Slots[1];
        public Slot ExhaustSlot => Slots[2];
        //TODO Look into adding a filter as well
        public Atmosphere SourceAtmosphere => IntakeSlot.Get()?.InternalAtmosphere;
        //TODO possibly add to the world grid as a last resort
        public Atmosphere TargetAtmosphere => ExhaustSlot.Get<GasCanister>()?.InternalAtmosphere ?? Robot?.BreathingAtmosphere;

        public override void Tick()
        {
            if (Robot == null || Loop == null) return;
            if (Loop.InternalAtmosphere == null || Loop.ParentEntity.BreathingAtmosphere == null) return;
            // Handle continuous pumping and venting/flushing when toggles are active
            if ((Loop.ParentEntity as Human).RobotBattery)
            {
                if (Importing == 1) DoPumpLoop();
                if (Exporting == 1) DoVentLoop();
            }
        }

        public float VentingEfficiency()
        {
            // density of gas in lungs (higher density = harder to pump gas)
            // AtmosphereHelper.GetDensityMilliMolesPerLire(InternalAtmosphere);
            float densityGasFactor = (float)IdealGas.GetMilliMolesPerLitre(InternalAtmosphere.Volume, InternalAtmosphere.TotalMolesGases);
            // pump head factor (higher pressure = harder to pump)
            float pumpHeadFactor = (InternalAtmosphere.PressureGassesAndLiquids / TargetAtmosphere.PressureGassesAndLiquids).ToFloat();
            // Maitenance factor (low maintenance = less efficient)
            // OrganLungs.DamageEfficiency

            return Mathf.Clamp01((pumpHeadFactor * Loop.DamageEfficiency) / densityGasFactor) * (float)DifficultySetting.Current.BreathingRate;
        }

        public float PumpingEfficiency()
        {
            // fluid density of liquid tank (higher density = harder to pump fluid)
            float densityLiquidFactor = AtmosphereHelper.GetDensityMilliMolesPerLire(SourceAtmosphere);

            // Pump_head factor (higher head/pressure = harder to pump fluid) 
            float pumpHeadFactor = (SourceAtmosphere.PressureGassesAndLiquids / InternalAtmosphere.PressureGassesAndLiquids).ToFloat();

            // Maitenance factor (low maintenance = less efficient)
            // OrganLungs.DamageEfficiency

            return Mathf.Clamp01((pumpHeadFactor * Loop.DamageEfficiency) / densityLiquidFactor) * (float)DifficultySetting.Current.BreathingRate;
        }
        // Integration into VentLoop
        public void DoVentLoop()
        {
            float flowRate = Loop.VentRate * VentingEfficiency();
            float energyChange = -20f * flowRate;

            if (ApplyPowerDelta(energyChange))
            {
                AtmosphereHelper.MoveVolume(
                    InternalAtmosphere,
                    TargetAtmosphere,
                    new VolumeLitres(flowRate),
                    AtmosphereHelper.MatterState.Gas, MoleQuantity.Zero);
            }
        }

        public void DoPumpLoop()
        {
            float flowRate = Loop.PumpRate * PumpingEfficiency();
            float energyChange = -100f * flowRate;

            if (ApplyPowerDelta(energyChange))
            {
                AtmosphereHelper.MoveLiquidVolume(
                SourceAtmosphere,
                InternalAtmosphere,
                new VolumeLitres(flowRate));
            }
        }
        public override DelayedActionInstance InteractWith(Interactable interactable, Interaction interaction, bool doAction = true)
        {
            DelayedActionInstance delayedActionInstance = new()
            {
                Duration = 0f,
                ActionMessage = interactable.ContextualName
            };
            switch (interactable.Action)
            {
                case InteractableType.Open:
                case InteractableType.OnOff:
                    if (!doAction)
                    {
                        return delayedActionInstance.Succeed();
                    }
                    OnServer.Interact(interactable, (interactable.State != 1) ? 1 : 0);
                    return delayedActionInstance.Succeed();
                case InteractableType.Import:
                    if (IsLocked)
                    {
                        return delayedActionInstance.Fail(GameStrings.DeviceLocked);
                    }
                    if (!CanPump) return delayedActionInstance.Fail("Can't Pump");
                    if (!doAction)
                    {
                        return delayedActionInstance.Succeed();
                    }
                    // if (ParentEntity?.IsLocalPlayer is true)
                    // {
                    //     UIAudioManager.Play(SuitButtonUpHash);
                    // }
                    OnServer.Interact(interactable, (interactable.State != 1) ? 1 : 0);
                    return delayedActionInstance.Succeed();
                case InteractableType.Export:
                    if (IsLocked)
                    {
                        return delayedActionInstance.Fail(GameStrings.DeviceLocked);
                    }
                    if (!CanVent) return delayedActionInstance.Fail("Can't Vent");
                    if (!doAction)
                    {
                        return delayedActionInstance.Succeed();
                    }
                    // if (ParentEntity?.IsLocalPlayer is true)
                    // {
                    //     UIAudioManager.Play(SuitButtonUpHash);
                    // }
                    OnServer.Interact(interactable, (interactable.State != 1) ? 1 : 0);
                    return delayedActionInstance.Succeed();
                default:
                    return base.InteractWith(interactable, interaction, doAction);
            }
        }

        public override string GetContextualName(Interactable interactable)
        {
            return interactable.Action switch
            {
                InteractableType.Import => PumpLoop + ((Importing == 0) ? ActionStrings.On : ActionStrings.Off),
                InteractableType.Export => VentLoop + ((Exporting == 0) ? ActionStrings.On : ActionStrings.Off),
                _ => base.GetContextualName(interactable),
            };
        }
    }
}
