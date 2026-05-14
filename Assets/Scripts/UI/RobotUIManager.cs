using System.Collections.Generic;
using Assets.Scripts.Inventory;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.UI;
using Assets.Scripts.Util;
using ErixMekx.Items;
using ErixMekx.Organs;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace ErixMekx.UI
{
    public class RobotUIManager : ManagerBase
    {
        public static RobotUIManager Instance { get; private set; }

        private GameObject _robotHUDPanelObj;
        private readonly List<GameObject> _injectedSlots = new();
        public Human Robot => InventoryManager.ParentHuman;
        public bool IsSetup = false;

        private SlotDisplayButton ButtonPrefab => InventoryWindowManager.Instance.WindowPrefab.ButtonPrefab;
        // private ClothingPanel ClothingPanel => InventoryManager.Instance.ClothingPanel;
        private Slot TargetSlot => Robot.LungsSlot.Get<LungsRobot>().ModuleSlot;
        public void Initialize()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void SetupRobotUI()
        {
            Cleanup();

            // Handle the Custom HUD Panel
            SpawnRobotHUD();

            InjectTargetSlot();
            IsSetup = true;
        }

        private void SpawnRobotHUD()
        {
            if (ErixMekxMain.PanelRobotPrefab == null) return;

            // Locate the PanelVerticalGroup in the hierarchy
            GameObject verticalGroupObj = GameObject.Find("PanelVerticalGroup");

            if (verticalGroupObj == null)
            {
                Debug.LogError("[ErixMekx] Could not find PanelVerticalGroup in scene!");
                return;
            }

            Transform grid = verticalGroupObj.transform;
            _robotHUDPanelObj = Object.Instantiate(ErixMekxMain.PanelRobotPrefab, grid, false);
        }

        private void InjectTargetSlot()
        {
            try
            {
                if (!Robot.LungsSlot.Contains<LungsRobot>()) return;
                // Transform grid = ClothingPanel.transform.Find("DynamicGrid");
                Transform grid = _robotHUDPanelObj.transform;

                if (grid == null) return;

                TargetSlot.IsInteractable = true;

                // Instantiate and track for cleanup
                SlotDisplayButton targetBtn = Object.Instantiate(ButtonPrefab, grid, false);
                targetBtn.name = "Slot" + TargetSlot.Action;
                _injectedSlots.Add(targetBtn.gameObject);

                // Bind logic to UI
                TargetSlot.Display = new SlotDisplay(targetBtn, TargetSlot);
                if (TargetSlot.Display.SlotText)
                {
                    (TargetSlot.Display.SlotText).text = ((TargetSlot.Type == Slot.Class.None) ? string.Empty : TargetSlot.DisplayName);
                }
                if (TargetSlot.Display.QuantityText)
                {
                    (TargetSlot.Display.QuantityText).text = string.Empty;
                }
                TargetSlot.OnOccupantChange += OnSlotOccupantChanged;
                TargetSlot.RefreshSlotDisplay();
            }
            catch (System.Exception ex)
            {
                ErixMekxMain.Log.LogError($"[RobotUIManager] Lungs Injection Failed: {ex}");
            }
        }

        public void OnSlotOccupantChanged()
        {
            // if (TargetSlot.Contains<ILoopModule>()) TargetSlot.Get<ILoopModule>().Install(Robot.LungsSlot.Get<LungsRobot>());
        }


        public void Cleanup()
        {
            // Destroy the HUD panel
            if (_robotHUDPanelObj != null)
                Object.Destroy(_robotHUDPanelObj);

            // Destroy all injected UI buttons from the Clothing Panel
            foreach (GameObject slot in _injectedSlots)
            {
                if (slot != null) Object.Destroy(slot);
            }
            _injectedSlots.Clear();
            IsSetup = false;
        }

        public void SetVisible(bool state)
        {
            if (_robotHUDPanelObj == null) return;
            if (_robotHUDPanelObj.activeSelf != state)
                _robotHUDPanelObj.SetActive(state);
        }

    }
}
