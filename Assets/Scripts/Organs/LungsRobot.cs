using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Localization2;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Util;
using ErixMekx.Items;
using ErixMekx.Systems;
using ErixMekx.UI;
using UnityEngine;

namespace ErixMekx.Organs
{
    public class LungsRobot : Lungs
    {
        // The slot where the CoolingModule will be placed
        public Slot ModuleSlot => Slots[0];
        public Human Robot => ParentEntity as Human;
        private const float EXPLOSION_FORCE = 200f;

        private const float EXPLOSION_RADIUS = 2.3f;

        protected override TemperatureKelvin TemperatureMin => new(Chemistry.Temperature.ZERO_DEGREES + RobotConfig.TempMinCelsius.Value);
        protected override TemperatureKelvin TemperatureMax => new(Chemistry.Temperature.ZERO_DEGREES + RobotConfig.TempMaxCelsius.Value);
        protected override PressurekPa ToxinLevel => base.InternalAtmosphere.PartialPressureAcid;
        public PressurekPa PressureLimit => new(Chemistry.ONE_ATMOSPHERE * RobotConfig.PressureLimitAtm.Value);
        public float VentRate = Chemistry.ONE_ATMOSPHERE / 2;
        public float PumpRate => RobotConfig.PumpRate.Value;
        public float Overpressure => RobotConfig.OverpressureThreshold.Value;
        public bool IsEmpty => InternalAtmosphere?.GasMixture.GetTotalMolesGassesAndLiquids < Chemistry.MINIMUM_VALID_TOTAL_MOLES;

        private BatteryCell lastBatteryInstance = null;
        public BatteryCell RobotBattery => (ParentEntity as Human).RobotBattery;
        private float lastBatteryCharge = -1f;
        private bool IsError
        {
            get
            {
                if (!IsEmpty)
                {
                    // return !_hasBlown;
                    // return leaks;
                    return false;
                }
                return true;
            }
        }
        public override void OnLifeTick()
        {
            if (GameManager.GameState == GameState.Running)
            {
                HandleGlobalWasteHeat();
                EvaluateModules();
                CheckAtmosphereState();
                ProcessEnvironmentalDamage();
            }
        }

        private void HandleGlobalWasteHeat()
        {
            if (ParentEntity == null || RobotBattery == null) return;

            float currentCharge = RobotBattery.PowerStored;

            // 1. Handle Battery Swap: If the instance changed, reset and exit
            if (lastBatteryInstance != RobotBattery)
            {
                lastBatteryInstance = RobotBattery;
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

        private void EvaluateModules()
        {
            if (ModuleSlot.Contains<ILoopModule>())
            {
                ILoopModule module = ModuleSlot.Get<ILoopModule>();
                if (module.IsActive)
                    module.OnTick();
                    // float num = module.OnTick();
                    // RequestPower(num);
            }
        }
        public bool RequestPower(float delta)
        {
            if (!Powered) return false;
            // if (RobotBattery == null) return false;

            // Apply difficulty multipliers and metabolism here once
            float multiplier = ParentEntity.OrganBrain.IsOnline ? 1f : (float)DifficultySetting.Current.OfflineMetabolism;
            float finalDelta = multiplier * delta * (float)DifficultySetting.Current.RobotBatteryRate;

            if (finalDelta < 0) // Draining
            {
                if (RobotBattery.PowerStored > Mathf.Abs(finalDelta))
                {
                    RobotBattery.PowerStored += finalDelta;
                    return true;
                }
                return false;
            }
            else // Charging/Excess
            {
                // Handle charging and Thermal Bleed into the loop atmosphere
                float spaceLeft = RobotBattery.PowerMaximum - RobotBattery.PowerStored;
                float amountToStore = Mathf.Min(finalDelta, spaceLeft);
                RobotBattery.PowerStored += amountToStore;

                float excess = finalDelta - amountToStore;
                if (excess > 0 && InternalAtmosphere != null)
                {
                    InternalAtmosphere.GasMixture.AddEnergy(new MoleEnergy(excess * RobotConfig.ThermalBleedCoefficient.Value));
                }
                return true;
            }
        }
        private void CheckAtmosphereState()
        {
            if (Powered)
            {
                bool isError = IsError;
                if (Error == 0 && isError)
                {
                    OnServer.Interact(base.InteractError, 1);
                }
                OnServer.Interact(base.InteractError, 0);
            }
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
                if (pressureRatio > RobotConfig.OverpressureThreshold.Value)
                {
                    float overpressureFactor = Mathf.Clamp01(pressureRatio - RobotConfig.OverpressureThreshold.Value);
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


        //TODO: Either we add leaks up and keep around to give players a chance to fix lungs or kill the player with the explosion
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
        // public override void OnDestroy()
        // {
        // 	if (Singleton<GameManager>.IsQuitting)
        // 	{
        // 		return;
        // 	}
        // 	base.OnDestroy();
        // 	foreach (LeakReference leakReference in LeakReferences)
        // 	{
        // 		if (leakReference != null && !(leakReference.Visualizer == null))
        // 		{
        // 			leakReference.Visualizer.SetActive(value: false);
        // 			if (leakReference.LeakTask.Status != UniTaskStatus.Pending)
        // 			{
        // 				leakReference.Cancel();
        // 			}
        // 		}
        // 	}
        // 	LeakReferences = null;
        // 	if (!Singleton<GameManager>.IsQuitting)
        // 	{
        // 		base.OnDestroy();
        // 		ElectricityManager.Deregister(this);
        // 		if (!IsCursor)
        // 		{
        // 			CircuitHolders.Deregister(this);
        // 		}
        // 	}
        // }
    }
}
