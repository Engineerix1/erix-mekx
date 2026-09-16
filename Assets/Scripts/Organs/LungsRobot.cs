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
        // Public mirror of ToxinLevel for modules (e.g. CoolantFilterModule) that live outside this class and can't see the protected override.
        public PressurekPa ContaminantLevel => base.InternalAtmosphere.PartialPressureAcid;
        public PressurekPa PressureLimit => new(Chemistry.ONE_ATMOSPHERE * RobotConfig.PressureLimitAtm.Value);
        public float VentRate = Chemistry.ONE_ATMOSPHERE / 2;
        public float PumpRate => RobotConfig.PumpRate.Value;
        public float Overpressure => RobotConfig.OverpressureThreshold.Value;
        public bool IsEmpty => InternalAtmosphere?.GasMixture.GetTotalMolesGassesAndLiquids < Chemistry.MINIMUM_VALID_TOTAL_MOLES;

        // Leak / Rupture state
        private float overpressureStress = 0f;
        public bool IsLeaking { get; private set; } = false;
        public float LeakSeverity { get; private set; } = 0f;
        private bool hasExploded = false;

        private BatteryCell lastBatteryInstance = null;
        public BatteryCell RobotBattery => (ParentEntity as Human).RobotBattery;
        private float lastBatteryCharge = -1f;
        private bool IsError
        {
            get
            {
                if (IsEmpty) return true;
                if (IsLeaking) return true;
                return false;
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
                ProcessLeakState();
                ProcessCoolantDegradation();
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

        /// <summary>
        /// Sustained overpressure builds structural stress; once that stress crosses a
        /// threshold the loop springs an active leak that keeps bleeding gas and taking
        /// brute damage - and keeps getting worse - until a player patches it via
        /// RepairLeak(). This replaces passively tanking overpressure damage forever.
        /// </summary>
        private void ProcessLeakState()
        {
            float pressureRatio = (InternalAtmosphere.PressureGassesAndLiquids / PressureLimit).ToFloat();

            if (pressureRatio > RobotConfig.OverpressureThreshold.Value)
            {
                overpressureStress += (pressureRatio - RobotConfig.OverpressureThreshold.Value) * RobotConfig.OverpressureStressRate.Value;
            }
            else
            {
                overpressureStress = Mathf.Max(0f, overpressureStress - RobotConfig.OverpressureStressRate.Value);
            }

            if (!IsLeaking)
            {
                if (overpressureStress < RobotConfig.SustainedOverpressureLimit.Value) return;
                IsLeaking = true;
                LeakSeverity = RobotConfig.LeakSeverityBase.Value;
            }

            LeakSeverity = Mathf.Clamp01(LeakSeverity + RobotConfig.LeakGrowthRate.Value);

            float ventAmount = RobotConfig.LeakVentRate.Value * LeakSeverity;
            foreach (Chemistry.GasType gasType in Assets.Scripts.EnumCollections.GasTypes.Values)
            {
                if (gasType == Chemistry.GasType.Undefined) continue;
                InternalAtmosphere.GasMixture.Remove(gasType, new MoleQuantity(ventAmount));
            }

            DamageState.Damage(ChangeDamageType.Increment, RobotConfig.LeakStructuralDamageRate.Value * LeakSeverity, DamageUpdateType.Brute);
        }

        /// <summary>
        /// Patches an active leak. Invoked when a player services the loop (Flush interaction) -
        /// severe leaks need repeated servicing to fully seal.
        /// </summary>
        private void RepairLeak()
        {
            if (!IsLeaking) return;

            overpressureStress = 0f;
            LeakSeverity -= RobotConfig.LeakRepairAmount.Value;
            if (LeakSeverity > 0f) return;

            LeakSeverity = 0f;
            IsLeaking = false;
        }

        /// <summary>
        /// The coolant chemical itself is never consumed or transmuted here - any chemical
        /// can serve as coolant, and there's no real reaction that turns an arbitrary gas
        /// into acid. Instead, sustained heat and structural damage corrode the loop's own
        /// housing, leaching trace hydrochloric acid into whatever is flowing through it -
        /// the same way an overheated or damaged metal loop leaches contaminants into its
        /// working fluid in reality. That buildup feeds directly into the existing
        /// ToxinLevel/ToxicTypes damage check above, so a neglected loop slowly poisons
        /// itself regardless of what chemical it's filled with.
        /// </summary>
        private void ProcessCoolantDegradation()
        {
            if (IsEmpty) return;

            float heatStress = Mathf.Max(0f, (InternalAtmosphere.Temperature - TemperatureMax).ToFloat()) * RobotConfig.CoolantDegradationHeatFactor.Value;
            float damageStress = Mathf.Max(0f, 1f - DamageEfficiency) * RobotConfig.CoolantDegradationDamageFactor.Value;
            float corrosionAmount = RobotConfig.CoolantDegradationRate.Value * (1f + heatStress + damageStress);
            if (corrosionAmount <= 0f) return;

            MoleQuantity quantity = new(corrosionAmount);
            MoleEnergy energy = new(InternalAtmosphere.Temperature, Mole.SpecificHeat(Chemistry.GasType.HydrochloricAcid), quantity);
            InternalAtmosphere.Add(new GasMixture(new Mole(Chemistry.GasType.HydrochloricAcid, quantity, energy)));
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
                        RepairLeak();
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


        /// <summary>
        /// Sustained overpressure gives players a repairable warning (see ProcessLeakState/
        /// RepairLeak) well before the organ is actually destroyed. If it's ignored long
        /// enough to reach full damage, the loop finally ruptures catastrophically here.
        /// </summary>
        public override void OnDamageDestroyed()
        {
            if (GameManager.RunSimulation && !hasExploded)
            {
                if (InternalAtmosphere.PressureGassesAndLiquids > PressurekPa.Zero)
                {
                    float pressureFactor = (InternalAtmosphere.PressureGassesAndLiquids / PressureLimit).ToFloat();
                    global::Explosion.Explode(
                        EXPLOSION_FORCE * pressureFactor,
                        radius: Mathf.Clamp(EXPLOSION_RADIUS * pressureFactor, 0f, EXPLOSION_RADIUS),
                        pos: transform.position,
                        maxDamage: float.MaxValue,
                        mineTerrain: true);
                    AtmosphericEventInstance.CloneGlobalAddGasMix(WorldGrid, new GasMixture(InternalAtmosphere.GasMixture), spark: true);
                    AtmosphericEventInstance.Reset(InternalAtmosphere);
                }
                DamageState.Damage(ChangeDamageType.Set, 0f, DamageUpdateType.Burn);
                DamageState.Damage(ChangeDamageType.Set, 0f, DamageUpdateType.Brute);
                hasExploded = true;
                return;
            }

            base.OnDamageDestroyed();
        }
    }
}
