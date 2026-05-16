using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.UI;
using ErixMekx.Organs;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErixMekx.UI
{
    public class PanelRobot : UserInterfaceBase
    {
        [Header("UI Elements")]
        public TextMeshProUGUI TitleLabel;
        public TextMeshProUGUI InfoIntakePressure;
        public TextMeshProUGUI InfoIntakeTemperature;
        public TextMeshProUGUI InfoCoolantFill;
        public Image pressureBar;
        public ImageToggle PressureChangeIcon;
        public ImageToggle TermperatureChangeIcon;


        [Header("StateInstances")]
        private StateInstance _intakeTemperatureState;
        private StateInstance _intakePressureState;
        private StateInstance _coolantFillState;

        [Header("Data")]
        private PressurekPa _intakePressure;

        // Tracking variables to prevent redundant UI updates and calculate deltas
        private float _lastTempIntake = -999f;
        private float _lastIntakePressureFloat = -1f;

        private Human Robot => InventoryWindowManager.Instance.Parent as Human;
        private LungsRobot CoolingLoop => Robot?.LungsSlot.Get<LungsRobot>();

        [Header("Thresholds")]
        // Matching base game logic for "Too Hot/Cold" and "High/Low Pressure"
        private const float ShowPressureLowLimit = 10f;    // kPa
        private float ShowPressureHighLimit => CoolingLoop.PressureLimit.ToFloat(); // kPa
        private float ShowTooColdLimit => Systems.RobotConfig.TempMinCelsius.Value;  // Celsius offset from ZeroDegrees
        private float ShowTooHotLimit => Systems.RobotConfig.TempMaxCelsius.Value; // Celsius offset from ZeroDegrees

        [UsedImplicitly]
        void Awake()
        {
            _intakeTemperatureState = new StateInstance(InfoIntakeTemperature);
            _intakeTemperatureState.ChangeEvents();

            _intakePressureState = new StateInstance(InfoIntakePressure);
            // _intakePressureState.OnChanged += delegate
            // {
            //     float num = PressureCurve.Evaluate(_intakePressure.ToFloat());
            //     float num2 = pressureBar.rectTransform.rect.width * num;
            //     pressureBar.fillAmount = num2;
            // };
            _intakePressureState.ChangeEvents();

            _coolantFillState = new StateInstance(InfoCoolantFill);
            _coolantFillState.ChangeEvents();
        }

        [UsedImplicitly]
        void Update()
        {
            if (CoolingLoop == null)
            {
                pressureBar.fillAmount = 0f;
                _intakeTemperatureState.UpdateText(0f);
                _intakePressureState.UpdateText(0f);
                UpdateTitle();
                return;
            }

            RefreshUI();
        }

        void RefreshUI()
        {
            // 1. Handle Temperature Logic
            float currentTempK = CoolingLoop.InternalAtmosphere.Temperature.ToFloat();
            float currentTempC = (CoolingLoop.InternalAtmosphere.Temperature - Chemistry.Temperature.ZeroDegrees).ToFloat();

            if (!Mathf.Approximately(_lastTempIntake, currentTempC))
            {
                _lastTempIntake = currentTempC;
                _intakeTemperatureState.UpdateText(_lastTempIntake);
                HandleTemperatureIcons(currentTempK);
            }

            // 2. Handle Pressure Logic
            PressurekPa currentPressure = CoolingLoop.InternalAtmosphere.PressureGassesAndLiquids;
            float currentPressureFloat = currentPressure.ToFloat();

            if (!Mathf.Approximately(_lastIntakePressureFloat, currentPressureFloat))
            {
                _intakePressure = currentPressure;
                _lastIntakePressureFloat = currentPressureFloat;

                _intakePressureState.UpdateText(currentPressureFloat);

                // Update Pressure Bar based on the organ's specific limit
                float pressureRatio = (currentPressure / CoolingLoop.PressureLimit).ToFloat();
                pressureBar.fillAmount = Mathf.Clamp01(pressureRatio);

                HandlePressureIcons(currentPressure);
            }

            _coolantFillState.UpdateText(CoolingLoop.InternalAtmosphere.LiquidVolumeRatio * 100f);

            UpdateTitle();
        }

        private void HandleTemperatureIcons(float tempK)
        {
            // Logic mirrored from base game's HandleInternalExternalIcons
            if (tempK > (Chemistry.Temperature.ZERO_DEGREES + ShowTooHotLimit))
            {
                TermperatureChangeIcon.SetImage(1); // High/Hot
                TermperatureChangeIcon.HideImage(false);
            }
            else if (tempK < (Chemistry.Temperature.ZERO_DEGREES + ShowTooColdLimit))
            {
                TermperatureChangeIcon.SetImage(0); // Low/Cold
                TermperatureChangeIcon.HideImage(false);
            }
            else
            {
                TermperatureChangeIcon.HideImage();
            }
        }

        private void HandlePressureIcons(PressurekPa pressure)
        {
            float pfloat = pressure.ToFloat();
            if (pfloat > ShowPressureHighLimit)
            {
                PressureChangeIcon.SetImage(1); // High
                PressureChangeIcon.HideImage(false);
            }
            else if (pfloat < ShowPressureLowLimit)
            {
                PressureChangeIcon.SetImage(0); // Low
                PressureChangeIcon.HideImage(false);
            }
            else
            {
                PressureChangeIcon.HideImage();
            }
        }

        public void UpdateTitle()
        {
            TitleLabel.text = string.Format("<color=#C8C8C8>{0}</color>", CoolingLoop ? CoolingLoop.DisplayName : "Empty");
        }
    }
}
