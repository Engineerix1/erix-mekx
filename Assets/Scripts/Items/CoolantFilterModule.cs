using Assets.Scripts.Atmospherics;
using ErixMekx.Systems;
using UnityEngine;

namespace ErixMekx.Items
{
    /// <summary>
    /// Actively scrubs the corrosive contaminant that coolant degradation produces out of
    /// the loop atmosphere, as an alternative to relying purely on periodic Flush servicing.
    /// </summary>
    public class CoolantFilterModule : LoopModuleBase
    {
        public override void OnTick()
        {
            HandleFiltering();
        }

        private void HandleFiltering()
        {
            if (Loop?.InternalAtmosphere == null) return;
            if (Loop.ContaminantLevel <= PressurekPa.Zero) return;

            float filterRate = RobotConfig.FilterRate.Value * Mathf.Clamp01(Loop.DamageEfficiency);
            if (filterRate <= 0f) return;

            if (Loop.RequestPower(-10f * filterRate))
            {
                Loop.InternalAtmosphere.GasMixture.Remove(Chemistry.GasType.HydrochloricAcid, new MoleQuantity(filterRate));
            }
        }
    }
}
