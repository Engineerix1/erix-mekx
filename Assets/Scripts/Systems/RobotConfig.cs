using BepInEx.Configuration;
using ErixMekx;
using UnityEngine;

namespace ErixMekx.Systems
{
    public static class RobotConfig
    {
        // Thermal Limits
        public static ConfigEntry<float> TempMinCelsius;
        public static ConfigEntry<float> TempMaxCelsius;
        public static ConfigEntry<float> PressureLimitAtm;

        // Performance Rates
        public static ConfigEntry<float> PumpRate;
        public static ConfigEntry<float> EnergyCoefficient;
        public static ConfigEntry<float> HeatCoefficient;
        public static ConfigEntry<float> ThermalBleedCoefficient;
        // public static ConfigEntry<float> VentingEfficiencyMult;
        // public static ConfigEntry<float> PumpingEfficiencyMult;

        // Damage Settings
        public static ConfigEntry<float> OverpressureThreshold; // Ratio (e.g., 1.5f)
        public static ConfigEntry<float> PressureDamageRate;

        // Leak & Rupture
        public static ConfigEntry<float> OverpressureStressRate;
        public static ConfigEntry<float> SustainedOverpressureLimit;
        public static ConfigEntry<float> LeakSeverityBase;
        public static ConfigEntry<float> LeakGrowthRate;
        public static ConfigEntry<float> LeakVentRate;
        public static ConfigEntry<float> LeakStructuralDamageRate;
        public static ConfigEntry<float> LeakRepairAmount;

        // Coolant Degradation
        public static ConfigEntry<float> CoolantDegradationRate;
        public static ConfigEntry<float> CoolantDegradationHeatFactor;
        public static ConfigEntry<float> CoolantDegradationDamageFactor;
        public static ConfigEntry<float> FilterRate;

        public static void Init(ConfigFile config)
        {
            TempMinCelsius = config.Bind("Thermal Limits", "Temperature Min Celsius", -20f, "Minimum operating temperature before cold damage occurs.");
            TempMaxCelsius = config.Bind("Thermal Limits", "Temperature Max Celsius", 60f, "Maximum operating temperature before heat damage occurs.");
            PressureLimitAtm = config.Bind("Thermal Limits", "Pressure Limit Atm", .5f, "The nominal pressure limit of the cooling loop.");

            PumpRate = config.Bind("Performance", "Pump Rate", .1f, "Base volume moved per tick during pumping/venting.");
            EnergyCoefficient = config.Bind("Performance", "Energy Coefficient", .1f, "Modify the Work (W) cost/gained based on the pressure gradient between the internal atmosphere and the external environment.");
            HeatCoefficient = config.Bind("Performance", "Heat Coefficient", 1f, "Modify the Heat (K) gained/lost based on the charging/discharding of the internal battery.");
            ThermalBleedCoefficient = config.Bind("Performance", "Thermal Bleed Coefficient", .8f, "Modify the Heat (K) gained/lost based on overcharging of the internal battery by modules.");
            // VentingEfficiencyMult = config.Bind("Performance", "Venting Efficiency Multiplier", 1f, "Multiplier for gas venting efficiency.");
            // PumpingEfficiencyMult = config.Bind("Performance", "Pumping Efficiency Multiplier", 1f, "Multiplier for liquid pumping efficiency.");

            OverpressureThreshold = config.Bind("Damage", "Overpressure Threshold", 1.5f, "Pressure ratio (Current/Limit) at which brute damage begins.");
            PressureDamageRate = config.Bind("Damage", "Pressure Damage Rate", 3f, "Amount of brute damage applied per tick when overpressured.");

            OverpressureStressRate = config.Bind("Damage", "Overpressure Stress Rate", 1f, "How quickly sustained overpressure builds toward a rupture per tick, scaled by how far over the threshold the pressure ratio is. Decays at the same rate once pressure drops back below the threshold.");
            SustainedOverpressureLimit = config.Bind("Damage", "Sustained Overpressure Limit", 15f, "Accumulated overpressure stress required before the loop springs a leak.");
            LeakSeverityBase = config.Bind("Damage", "Leak Severity Base", .1f, "Starting severity (0-1) of a newly sprung leak.");
            LeakGrowthRate = config.Bind("Damage", "Leak Growth Rate", .01f, "How much worse an unpatched leak gets each tick.");
            LeakVentRate = config.Bind("Damage", "Leak Vent Rate", .5f, "Moles of gas lost per tick through a leak at full (1.0) severity.");
            LeakStructuralDamageRate = config.Bind("Damage", "Leak Structural Damage Rate", 1f, "Brute damage applied per tick from an active, unpatched leak at full (1.0) severity.");
            LeakRepairAmount = config.Bind("Damage", "Leak Repair Amount", .35f, "How much leak severity is reduced each time the loop is serviced via the Flush interaction.");

            CoolantDegradationRate = config.Bind("Coolant", "Coolant Degradation Rate", .01f, "Baseline moles of coolant converted into corrosive contaminant per tick.");
            CoolantDegradationHeatFactor = config.Bind("Coolant", "Coolant Degradation Heat Factor", .05f, "Extra degradation added per degree the loop runs above its maximum safe temperature.");
            CoolantDegradationDamageFactor = config.Bind("Coolant", "Coolant Degradation Damage Factor", 1f, "Extra degradation added in proportion to existing organ damage, i.e. how far DamageEfficiency has fallen below 1.");
            FilterRate = config.Bind("Coolant", "Filter Rate", .2f, "Moles of contaminant a Coolant Filter Module can scrub per tick at full efficiency.");
        }
    }
}
