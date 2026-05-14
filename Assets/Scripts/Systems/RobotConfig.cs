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

        public static void Init(ConfigFile config)
        {
            TempMinCelsius = config.Bind("Thermal Limits", "Temperature Min Celsius", -60f, "Minimum operating temperature before cold damage occurs.");
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
        }
    }
}
