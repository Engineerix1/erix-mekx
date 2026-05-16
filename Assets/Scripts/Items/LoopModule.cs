
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Util;
using ErixMekx.Organs;
using ErixMekx.Systems;
using UnityEngine;

namespace ErixMekx.Items
{
    public interface ILoopModule : IReferencable//, IDensePoolable
    {
        // The core method called by the Organ every tick
        void Tick();

        // Allows the organ to query if the module is currently active/powered
        bool IsActive { get; }
    }
    public abstract class LoopModuleBase : Item, ILoopModule
    {
        //TODO: Circuit connections and I'd like bboxes for it's slots as well as a reading from the atmospheric tablet
        public Human Robot => ParentSlot.Parent as Human;
        public LungsRobot Loop => Robot.OrganLungs as LungsRobot;
        public virtual bool IsActive { get; protected set; } = true;

        // Abstract method: Each specific module (Water, Cryo, etc.) implements its own loop logic
        public abstract void Tick();

        /// <summary>
        /// Handles bidirectional power flow between the module and the Robot Battery.
        /// </summary>
        /// <param name="powerDelta">Positive for charging, negative for draining.</param>
        /// <returns>True if the operation was successful (e.g., enough power existed for a drain).</returns>
        protected bool ApplyPowerDelta(float powerDelta)
        {
            Human human = Loop.ParentEntity as Human;
            if (human == null || human.RobotBattery == null) return false;

            float multiplier = (Loop.ParentEntity.OrganBrain.IsOnline ? 1f : (float)DifficultySetting.Current.OfflineMetabolism);
            float finalDelta = multiplier * powerDelta * (float)DifficultySetting.Current.RobotBatteryRate;

            // --- CASE 1: DRAINING POWER (Negative Delta) ---
            if (finalDelta < 0)
            {
                if (human.RobotBattery.PowerStored > Mathf.Abs(finalDelta))
                {
                    human.RobotBattery.PowerStored += finalDelta;
                    return true;
                }
                // If we can't afford the full cost, we don't run the loop at all
                return false;
            }

            // --- CASE 2: GAINING POWER (Positive Delta) ---
            if (finalDelta > 0)
            {
                float currentPower = human.RobotBattery.PowerStored;
                float maxPower = human.RobotBattery.PowerMaximum;

                if (currentPower < maxPower)
                {
                    float spaceLeft = maxPower - currentPower;
                    float amountToStore = Mathf.Min(finalDelta, spaceLeft);
                    float excessEnergy = finalDelta - amountToStore;

                    human.RobotBattery.PowerStored += amountToStore;

                    // HANDLE EXCESS ENERGY: Convert waste to heat in the lungs
                    if (excessEnergy > 0 && Loop.InternalAtmosphere != null)
                    {
                        HandleThermalBleed(excessEnergy);
                    }

                    return true; // We stored at least some energy
                }
                else
                {
                    // Battery is completely full: All energy becomes heat
                    if (Loop.InternalAtmosphere != null)
                    {
                        HandleThermalBleed(finalDelta);
                        return true;
                    }
                    return false; // Failed to store any energy
                }
            }

            return true;
        }

        private void HandleThermalBleed(float wastedEnergy)
        {
            // Convert the "Power" units (Watts/Joules) into MoleEnergy for the atmosphere
            // We use a coefficient to simulate efficiency losses
            MoleEnergy heatGain = new(wastedEnergy * RobotConfig.ThermalBleedCoefficient.Value);

            Loop.InternalAtmosphere.GasMixture.AddEnergy(heatGain);
        }
    }
}