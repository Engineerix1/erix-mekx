using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Inventory;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Util;
using ErixMekx.Organs;
using ErixMekx.Systems;
using UnityEngine;
using UnityEngine.Serialization;
using GameString = Assets.Scripts.Localization2.GameString;

namespace ErixMekx.Items
{
    public abstract class LoopModuleBase : Item, ILoopModule
    {
        [SerializeField]
        public float volume = 1f;
        public VolumeLitres Volume => new(volume);
        public BatteryCell RobotBattery => Loop.RobotBattery;
        public virtual bool IsActive { get; protected set; } = true;
        public bool IsEmpty => InternalAtmosphere?.GasMixture.GetTotalMolesGassesAndLiquids < Chemistry.MINIMUM_QUANTITY_MOLES;
        public LungsRobot Loop
        {
            get
            {
                Slot.Class? obj = base.ParentSlot?.Type;
                if (!obj.HasValue || obj != Slot.Class.SuitMod) return null;
                return base.ParentSlot?.Parent as LungsRobot;
            }
        }
        public override void InitInternalAtmosphere()
        {
            base.InternalAtmosphere ??= new Atmosphere(this, Volume, 0L);
            base.InternalAtmosphere.Add(GasMixtureHelper.Create ());
            // AtmosphericEventInstance.CreateAdd(atmosphere: base.InternalAtmosphere, gasMixture: GasMixtureHelper.Create ());
        }
		// public override void OnAtmosphericTick()
		// {
        //             ErixMekxMain.Log("OnAtmosphericTick");
		// 	if (GameManager.GameState == GameState.Running && Loop != null)
		// 	{
		// 		CheckPowerState();
		// 		CheckImportState();
		// 		CheckExportState();
		// 	}
		// 	base.OnAtmosphericTick();
		// }
        public abstract void OnTick();

        // protected void CheckPowerState()
        // {
        //     if (Powered && (RobotBattery?.IsEmpty is not false))
        //         OnServer.Interact(base.InteractPowered, 0, skipAnimation: true);
        //     else if (!Powered && RobotBattery?.IsEmpty is false)
        //         OnServer.Interact(base.InteractPowered, 1, skipAnimation: true);
        // }
        // protected void CheckImportState()
        // {
        //     if (Importing == 1 && (Loop?.IsEmpty is not false))
        //         OnServer.Interact(base.InteractImport, 0, skipAnimation: true);
        //     else if (Importing != 1 && Loop?.IsEmpty is false)
        //         OnServer.Interact(base.InteractImport, 1, skipAnimation: true);
        // }
        // protected void CheckExportState()
        // {
        //     if (Exporting == 1 && (IsEmpty || !Loop))
        //         OnServer.Interact(base.InteractImport, 0, skipAnimation: true);
        //     else if (Exporting != 1 && !IsEmpty && Loop)
        //         OnServer.Interact(base.InteractImport, 1, skipAnimation: true);
        // }

        // --- REUSABLE LOGIC START ---

        /// <summary>
        /// Shared utility for passive gas transfer between this module and another atmosphere.
        /// </summary>
        protected void PerformPassiveGasTransfer(Atmosphere inputAtmos, Atmosphere outputAtmos, float rate = 0.1f)
        {
            if (inputAtmos == null || outputAtmos == null) return;
            // density of gas in lungs (higher density = harder to pump gas)
            // AtmosphereHelper.GetDensityMilliMolesPerLire(InternalAtmosphere);
            float densityGasFactor = (float)IdealGas.GetMilliMolesPerLitre(inputAtmos.Volume, inputAtmos.TotalMolesGases);
            // pump head factor (higher pressure = harder to pump)
            float pumpHeadFactor = (inputAtmos.PressureGassesAndLiquids / outputAtmos.PressureGassesAndLiquids).ToFloat();
            // Maitenance factor (low maintenance = less efficient)
            // OrganLungs.DamageEfficiency

            float factor = Mathf.Clamp01((pumpHeadFactor * Loop.DamageEfficiency) / densityGasFactor) * (float)DifficultySetting.Current.BreathingRate;

            // Use Stationeers' built-in passive transfer logic or a simplified mole move
            AtmosphereHelper.MoveVolume(inputAtmos, outputAtmos, new VolumeLitres(rate), AtmosphereHelper.MatterState.Gas, MoleQuantity.Zero);
            // AtmosphereHelper.MoveRegulatedGas(InternalAtmosphere, target, new VolumeLitres(rate * factor));
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
                case InteractableType.Export:
                    if (IsLocked)
                    {
                        return delayedActionInstance.Fail(GameStrings.DeviceLocked);
                    }
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

        // --- REUSABLE LOGIC END ---
    }
}
