# ErixMekx - Robot Cooling Systems

**ErixMekx** transforms the survival experience for artificial species in Stationeers. Instead of traditional biological needs, robots are equipped with a **Modular Cooling Loop**. This system shifts the focus from solely battery management to thermal and pressure regulation, introducing a new layer of engineering directly into the robot's anatomy.

## 🛠️ Core Mechanics

### The Robot Lung (Cooling Loop)
Robots no longer consume power with no respect to the laws of thermodynamics; using the battery generates heat that they must circulate away from sensitive electonics. The `LungsRobot` organ acts as a heat exchanger and fluid reservoir.
- **Thermal Regulation:** Your robot character generates waste heat from battery usage. If the internal atmosphere of the lungs becomes too hot or too cold, you'll take burn/freeze damage.
- **Pressure Management:** High pressure in the cooling loop can cause "Brute" damage (Overpressure). Conversely, a total loss of coolant leads to rapid system failure.
- **Toxin Sensitivity:** Certain gases act as contaminants within the loop, causing toxic damage if not flushed.
- **Leaks & Ruptures:** Overpressure that isn't brought back under control doesn't just tick damage forever - sustained overpressure builds structural stress, and once that stress caps out the loop springs an active leak. A leaking loop keeps bleeding gas and taking brute damage, and keeps getting worse, until a player actively services it (the Flush interaction now also patches leaks). Ignore it long enough and the organ ruptures catastrophically instead of quietly tanking damage.
- **Coolant Degradation:** The loop's own housing slowly corrodes under sustained heat and structural damage, leaching a corrosive contaminant into whatever coolant is running through it (the coolant chemical itself isn't consumed - any chemical can be used as coolant). That buildup feeds directly into the toxin damage above, so a neglected loop starts poisoning itself.

### Modular Upgrades
The cooling organ features a dedicated module slot. By installing different modules, you can change how your robot interacts with the environment:
- **Passive Radiator Module:** 
    - **Pumping:** Pulls liquid coolant from an external source (e.g., a liquid canister) into the internal loop.
    - **Venting:** Dumps internal gases/heat into the surrounding atmosphere or a connected container.
    - **Power Cost:** These operations draw power from the Robot Battery.
- **Coolant Filter Module:**
    - **Filtering:** Actively scrubs the corrosive contaminant produced by coolant degradation out of the loop, trading module-slot flexibility (vs. the Radiator) for lower-maintenance coolant.
    - **Power Cost:** Filtering draws power from the Robot Battery, and scales with the organ's own damage efficiency - a damaged loop filters worse, not better.

### Integrated UI
The mod injects a custom **Robot HUD Panel** into the player state window, providing real-time telemetry:
- **Internal Temperature:** Monitor your coolant temperature in Celsius.
- **Pressure Gauge:** A visual bar showing current pressure relative to the system's safety limit.
- **Status Icons:** Visual warnings for "Too Hot," "Too Cold," "High Pressure," or "Low Pressure."

## ⚙️ Configuration
The mod is configurable via the BepInEx config file (`erixmekx.cfg`). You can tune:
- **Thermal Limits:** Adjust the Celsius thresholds for heat/cold damage.
- **Pressure Limits:** Change the nominal pressure limit before overpressure damage occurs.
- **Performance Rates:** Modify pump speeds and energy coefficients.
- **Damage Scaling:** Tune how quickly a robot takes damage from environmental failures, including how fast sustained overpressure builds toward a leak and how quickly an unpatched leak worsens.
- **Coolant Degradation:** Tune how fast coolant degrades into contaminant, and how much a Coolant Filter Module can scrub per tick.

## 🚀 Installation
1. Ensure you have [**BepInEx**](https://github.com/BepInEx/BepInEx) and [**StationeersLaunchPad**](https://github.com/StationeersLaunchPad/StationeersLaunchPad) installed for Stationeers.
2. Download the latest release of `ErixMekx`.
3. Place the mod folder into your `~/documents/My Games/Stationeers/mods` folder.

## ⚠️ Technical Notes for Users
- **Battery Dependency:** The cooling modules rely on the `RobotBattery` creating a feedback loop of heat buildup with particularly inefficient coolants.
- **Thermal Bleed:** Overcharging the battery via certain modules will convert excess energy into heat within the lungs—manage your power carefully!
