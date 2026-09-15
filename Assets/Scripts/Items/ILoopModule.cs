using Assets.Scripts.Atmospherics;
using ErixMekx.Organs;

namespace ErixMekx.Items
{
    /// <summary>
    /// Defines the contract for modules compatible with the LungsRobot system.
    /// </summary>
    public interface ILoopModule : IReferencable
    {
        /// <summary>
        /// The volume of the module's internal atmosphere.
        /// </summary>
        VolumeLitres Volume { get; }

        /// <summary>
        /// Whether the module is currently operational.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Whether the module is currently empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Reference to the parent LungsRobot system this module is slotted into.
        /// </summary>
        LungsRobot Loop { get; }

        /// <summary>
        /// Logic executed every simulation tick.
        /// </summary>
        void OnTick();
    }
}
