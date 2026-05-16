using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Util;
using ErixMekx.Items;
using ErixMekx.UI;
using UnityEngine;

namespace ErixMekx.Organs
{
    public class LungsRobot : Lungs
    {
        // The slot where the CoolingModule will be placed
        public Slot ModuleSlot => Slots[0];

        protected override TemperatureKelvin TemperatureMin => new(Chemistry.Temperature.ZERO_DEGREES + Systems.RobotConfig.TempMinCelsius.Value);
        protected override TemperatureKelvin TemperatureMax => new(Chemistry.Temperature.ZERO_DEGREES + Systems.RobotConfig.TempMaxCelsius.Value);
        protected override PressurekPa ToxinLevel => base.InternalAtmosphere.PartialPressureAcid;
        public PressurekPa PressureLimit => new(Chemistry.ONE_ATMOSPHERE * Systems.RobotConfig.PressureLimitAtm.Value);
        public float VentRate => Systems.RobotConfig.PumpRate.Value;
        public float PumpRate => Systems.RobotConfig.PumpRate.Value;
        public float Overpressure => Systems.RobotConfig.OverpressureThreshold.Value;

        private BatteryCell lastBatteryInstance = null;
        private float lastBatteryCharge = -1f;
        public override void OnLifeTick()
        {
            if (InternalAtmosphere == null) return;

            // 1. Global Power-to-Heat Conversion
            HandleGlobalWasteHeat();

            // 2. Module Logic
            if (ModuleSlot.Contains<ILoopModule>())
            {
                ILoopModule module = ModuleSlot.Get<ILoopModule>();
                if (module.IsActive)
                    module?.Tick();
            }

            // 3. Damage Tracking
            ProcessEnvironmentalDamage();
        }

        private void HandleGlobalWasteHeat()
        {
            Human human = ParentEntity as Human;
            if (human == null || human.RobotBattery == null) return;

            BatteryCell currentBattery = human.RobotBattery;
            float currentCharge = currentBattery.PowerStored;

            // 1. Handle Battery Swap: If the instance changed, reset and exit
            if (lastBatteryInstance != currentBattery)
            {
                lastBatteryInstance = currentBattery;
                lastBatteryCharge = currentCharge;
                return; // Skip heat calculation for this tick to avoid spikes
            }

            // 2. Handle First-Run Initialization
            if (lastBatteryCharge < 0)
            {
                lastBatteryCharge = currentCharge;
                return;
            }

            // 3. Calculate Waste Heat from Drain
            float chargeDelta = lastBatteryCharge - currentCharge;

            // Convert energy change to heat using the config coefficient
            float heatAmount = chargeDelta;
            MoleEnergy wasteHeat = new(Mathf.Abs(heatAmount));
            InternalAtmosphere.GasMixture.AddEnergy(wasteHeat);

            // Update tracker for next tick
            lastBatteryCharge = currentCharge;
        }

        private void ProcessEnvironmentalDamage()
        {
            //TODO Tie into repair system so damage can be healed
            float maxDamagePerTick = 1f / (float)DifficultySetting.Current.LungDamageRate * 4f;
            float damageCap = DamageState.MaxDamage / maxDamagePerTick;

            if (InternalAtmosphere.PressureGassesAndLiquids > Chemistry.ResetThreshold)
            {
                // Toxin Damage
                if (ToxinLevel > Entity.ToxicPartialPressureForDamage)
                {
                    float toxicDamage = Mathf.Min(ToxinLevel.ToFloat() * 0.2f, damageCap);
                    DamageState.Damage(ChangeDamageType.Increment, toxicDamage, DamageUpdateType.Toxic);
                    Chemistry.GasType[] values = Assets.Scripts.EnumCollections.GasTypes.Values;
                    foreach (Chemistry.GasType gasType in values)
                    {
                        if (gasType != Chemistry.GasType.Undefined && (ToxicTypes & gasType) != Chemistry.GasType.Undefined)
                        {
                            base.InternalAtmosphere.GasMixture.Remove(gasType, new MoleQuantity(toxicDamage));
                        }
                    }
                }

                // Overpressure Damage
                float pressureRatio = (InternalAtmosphere.PressureGassesAndLiquids / PressureLimit).ToFloat();
                if (pressureRatio > Systems.RobotConfig.OverpressureThreshold.Value)
                {
                    float overpressureFactor = Mathf.Clamp01(pressureRatio - Systems.RobotConfig.OverpressureThreshold.Value);
                    DamageState.Damage(ChangeDamageType.Increment, Mathf.Min(3f * overpressureFactor, damageCap), DamageUpdateType.Brute);
                }

                // Temperature Damage
                if (InternalAtmosphere.Temperature < TemperatureMin)
                {
                    float tempDiff = ((TemperatureKelvin.One - InternalAtmosphere.Temperature) / TemperatureMin).ToFloat();
                    DamageState.Damage(ChangeDamageType.Increment, Mathf.Min(3f * tempDiff, damageCap), DamageUpdateType.Burn);
                }
                if (InternalAtmosphere.Temperature > TemperatureMax)
                {
                    float overheatFactor = Mathf.Clamp01((InternalAtmosphere.Temperature - TemperatureMax).ToFloat() / 200f);
                    DamageState.Damage(ChangeDamageType.Increment, Mathf.Min(3f * overheatFactor, damageCap), DamageUpdateType.Burn);
                }

                //TODO check how this works, if frozen material needs to be added back in, etc.
                // GasMixture solids = InternalAtmosphere.GasMixture.CheckForFreezing(InternalAtmosphere.PressureGassesAndLiquids);
                // DamageState.Damage(ChangeDamageType.Increment, Mathf.Min(solids.GetQuantity, damageCap), DamageUpdateType.Brute);
            }
            else
            {
                // Missing Coolant/Atmosphere Damage
                float coolantDamage = Mathf.Min(0.4f, damageCap);
                DamageState.Damage(ChangeDamageType.Increment, coolantDamage * 0.6f, DamageUpdateType.Brute);
                DamageState.Damage(ChangeDamageType.Increment, coolantDamage * 0.4f, DamageUpdateType.Burn);
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
                    if (Assets.Scripts.GameManager.RunSimulation)
                    {
                        FlushLoop();
                    }
                    return delayedActionInstance.Succeed();
                default:
                    return base.InteractWith(interactable, interaction, doAction);
            }
        }
        public void FlushLoop()
        {
            AtmosphereHelper.Mix(InternalAtmosphere, ParentEntity.BreathingAtmosphere, AtmosphereHelper.MatterState.Liquid);
        }


        //TODO: Either we blow up and keep around to give players a chance to fix lungs or kill the player with the explosion
		// public override void OnDamageDestroyed()
        // {
        //     // SetBrokenMesh();
        //     if (GameManager.RunSimulation && !_hasBlown)
		// 	{
		// 		if (base.InternalAtmosphere.PressureGassesAndLiquids > PressurekPa.Zero)
		// 		{
		// 			global::Explosion.Explode(_explosionForce * (base.InternalAtmosphere.PressureGassesAndLiquids / PressureLimit).ToFloat(), radius: Mathf.Clamp(_explosionRadius * (base.InternalAtmosphere.PressureGassesAndLiquids / PressureLimit).ToFloat(), 0f, _maxExplosionRadius), pos: base.transform.position, maxDamage: float.MaxValue, mineTerrain: true);
		// 			AtmosphericEventInstance.CloneGlobalAddGasMix(base.WorldGrid, new GasMixture(base.InternalAtmosphere.GasMixture), spark: true);
		// 			AtmosphericEventInstance.Reset(base.InternalAtmosphere);
		// 		}
		// 		DamageState.Damage(ChangeDamageType.Set, 0f, DamageUpdateType.Burn);
		// 		DamageState.Damage(ChangeDamageType.Set, 0f, DamageUpdateType.Brute);
		// 		_hasBlown = true;
		// 	}
		// 	else if (_hasBlown)
		// 	{
		// 		base.OnDamageDestroyed();
		// 	}
		// }
    }
}
