using System;
using System.Linq;
using AmongUs.Data;
using AmongUs.Data.Player;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using ModMenuCrew.Features;
using ModMenuCrew.Patches;
using ModMenuCrew.UI.Controls;
using ModMenuCrew.UI.Managers;
using ModMenuCrew.UI.Styles;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using InnerNet;

namespace ModMenuCrew
{
    [BepInPlugin(Id, "Among Us Mod Menu Crew - Showcase", ModVersion)]
    [BepInProcess("Among Us.exe")]
    public class ModMenuCrewPlugin : BasePlugin
    {
        public const string Id = "com.crewmod.showcase";
        public const string ModVersion = "5.4.0-SHOWCASE";

        public DebuggerComponent Component { get; private set; } = null!;
        public static ModMenuCrewPlugin Instance { get; private set; }
        public Harmony Harmony { get; } = new Harmony(Id);

        public static ConfigEntry<float> CfgPlayerSpeed { get; private set; }
        public static ConfigEntry<bool> CfgInfiniteVision { get; private set; }
        public static ConfigEntry<bool> CfgIsNoclipping { get; private set; }
        public static ConfigEntry<bool> CfgTeleportWithCursor { get; private set; }
        public static ConfigEntry<KeyCode> CfgMenuToggleKey { get; private set; }

        public override void Load()
        {
            Instance = this;
            Instance.Log.LogInfo($"Plugin {Id} version {ModVersion} is loading.");
            InitializeConfig();
            try { ClassInjector.RegisterTypeInIl2Cpp<DebuggerComponent>(); } catch {}
            Component = AddComponent<DebuggerComponent>();
            Harmony.PatchAll();
            if (this.Config != null) LobbyHarmonyPatches.InitializeConfig(this.Config);
            Instance.Log.LogInfo($"Plugin {Id} loaded successfully.");
        }

        public override bool Unload()
        {
            try
            {
                if (Component != null) Component.CleanupResources();
                Harmony?.UnpatchSelf();
            }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error during plugin unload: {ex}"); }
            return base.Unload();
        }

        private void InitializeConfig()
        {
            if (Config == null) return;
            CfgPlayerSpeed = Config.Bind("5. Preferences", "Player Speed", 2.1f, "Default player speed multiplier for the menu.");
            CfgInfiniteVision = Config.Bind("5. Preferences", "Infinite Vision", false, "Persisted toggle for Infinite Vision.");
            CfgIsNoclipping = Config.Bind("5. Preferences", "Enable Noclip", false, "Persisted toggle for Noclip.");
            CfgTeleportWithCursor = Config.Bind("5. Preferences", "Teleport With Cursor", false, "Persisted toggle for teleporting to the cursor.");
            CfgMenuToggleKey = Config.Bind("5. Preferences", "Menu Toggle Key", KeyCode.F1, "Hotkey to open/close the menu.");
        }

        public class DebuggerComponent : MonoBehaviour
        {
            public bool IsNoclipping { get; set; }
            public float PlayerSpeed { get; set; } = 2.1f;
            public float KillCooldown { get; set; } = 25f;
            public bool InfiniteVision { get; set; }
            public bool NoKillCooldown { get; set; }

            private DragWindow mainWindow;
            private TabControl tabControl;
            private TabControl banAndPickTabControl;
            private TeleportManager teleportManager;
            private CheatManager cheatManager;
            private Il2CppSystem.Collections.Generic.List<string> pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>();

            public DebuggerComponent(IntPtr ptr) : base(ptr) { }

            public void CleanupResources()
            {
                try
                {
                    teleportManager = null;
                    cheatManager = null;
                    mainWindow = null;
                    tabControl = null;
                    banAndPickTabControl = null;
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error resource cleanup: {ex}"); }
            }

            void OnDestroy() => CleanupResources();

            void Awake()
            {
                try
                {
                    ModMenuCrewPlugin.Instance.Log.LogInfo("DebuggerComponent: Awake started (SHOWCASE MODE - No activation required).");
                    LoadConfigValues();
                    InitializeFeatureManagers();
                    InitializeMainWindowIMGUI();
                    InitializeTabsForGameIMGUI();
                    ModMenuCrewPlugin.Instance.Log.LogInfo("DebuggerComponent: Awake completed. SHOWCASE version active.");
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Critical error DebuggerComponent.Awake: {ex}"); }
            }

            private void InitializeFeatureManagers()
            {
                teleportManager = new TeleportManager();
                cheatManager = new CheatManager();
            }

            private void InitializeMainWindowIMGUI()
            {
                mainWindow = new DragWindow(new Rect(24, 24, 514, 0), $"ModMenuCrew v{ModMenuCrewPlugin.ModVersion} - SHOWCASE", DrawMainModWindowIMGUI)
                {
                    Enabled = false
                };
                mainWindow.SetViewportMinHeight(160f);
            }

            private void InitializeTabsForGameIMGUI()
            {
                tabControl = new TabControl();
                tabControl.AddTab("Game", DrawGameTabIMGUI, "General game controls and basic settings");
                tabControl.AddTab("Movement", DrawMovementTabIMGUI, "Movement controls and teleportation");
                tabControl.AddTab("Sabotage", DrawSabotageTabIMGUI, "Sabotage and doors controls");
                if (cheatManager != null) tabControl.AddTab("Cheats", cheatManager.DrawCheatsTab, "Cheats and advanced features");
            }

            private void DrawMainModWindowIMGUI()
            {
                bool isInGameByShip = (ShipStatus.Instance != null);

                if (!isInGameByShip)
                {
                    DrawLobbyUI_IMGUI();
                }
                else
                {
                    if (tabControl != null)
                    {
                        tabControl.Draw();
                    }
                    else
                    {
                        GUILayout.Label("Error: Game tabs not initialized.", GuiStyles.ErrorStyle);
                    }
                }
            }

            private void DrawLobbyUI_IMGUI()
            {
                if (banAndPickTabControl == null)
                {
                    banAndPickTabControl = new TabControl();
                    banAndPickTabControl.AddTab("Lobby Info", () => DrawLobbyInfoIMGUI(DateTime.Now), "Lobby information");
                }
                banAndPickTabControl.Draw();
            }

            private void DrawLobbyInfoIMGUI(DateTime dateTime)
            {
                bool mainLayoutStarted = false;
                try
                {
                    GUILayout.BeginVertical(GuiStyles.SectionStyle);
                    mainLayoutStarted = true;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Lobby Settings: {DateTime.Now:HH:mm}", GuiStyles.HeaderStyle);
                    GUILayout.EndHorizontal();
                    
                    if (GUILayout.Button("Mod Menu Crew - <color=#44AAFF>crewcore.online</color>", GuiStyles.LabelStyle))
                    {
                        Application.OpenURL("https://crewcore.online");
                    }
                    GuiStyles.DrawSeparator();

                    GUILayout.Label("<color=#FF6600>SHOWCASE VERSION</color>", GuiStyles.HeaderStyle);
                    GUILayout.Label("This is a demonstration version.", GuiStyles.LabelStyle);
                    GUILayout.Label("Visit crewcore.online for the full version!", GuiStyles.LabelStyle);
                    GuiStyles.DrawSeparator();

                    // Ban controls for non-host
                    var playerBanData = DataManager.Player?.ban;
                    if (playerBanData != null)
                    {
                        bool isLobby = LobbyBehaviour.Instance != null;
                        bool isHost = (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost);

                        bool showControls = !isLobby || !isHost;
                        if (showControls)
                        {
                            int banMinutesLeft = playerBanData.BanMinutesLeft;
                            GUILayout.Label($"Ban Time Remaining: {banMinutesLeft} minutes", GuiStyles.LabelStyle);

                            if (GUILayout.Button("Add Ban Time (+10 pts)", GuiStyles.ButtonStyle))
                            {
                                AddBanPoints(playerBanData, 10);
                            }

                            if (playerBanData.BanPoints > 0 && GUILayout.Button("Remove ALL Bans", GuiStyles.ButtonStyle))
                            {
                                RemoveAllBans(playerBanData);
                            }
                        }
                    }

                    GUILayout.EndVertical();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ModMenuCrew] Error in DrawLobbyInfoIMGUI: {ex}");
                    if (mainLayoutStarted) GUILayout.EndVertical();
                }
            }

            private void AddBanPoints(PlayerBanData playerBanData, int points)
            {
                if (playerBanData == null) return;
                playerBanData.BanPoints += points;
                playerBanData.OnBanPointsChanged?.Invoke();
                playerBanData.PreviousGameStartDate = new Il2CppSystem.DateTime(DateTime.UtcNow.Ticks);
                ShowNotification($"Ban points added: {points}. Total: {playerBanData.BanPoints}");
            }

            private void RemoveAllBans(PlayerBanData playerBanData)
            {
                if (playerBanData == null) return;
                playerBanData.BanPoints = 0f;
                playerBanData.OnBanPointsChanged?.Invoke();
                playerBanData.PreviousGameStartDate = new Il2CppSystem.DateTime(DateTime.MinValue.Ticks);
                ShowNotification("All bans removed!");
            }

            private void DrawGameTabIMGUI()
            {
                GUILayout.BeginVertical(GuiStyles.SectionStyle);
                GUILayout.Label("Game Controls", GuiStyles.HeaderStyle);
                GuiStyles.DrawSeparator();
                
                if (GUILayout.Button("Force Game End", GuiStyles.ButtonStyle))
                {
                    GameEndManager.ForceGameEnd(GameOverReason.ImpostorsByKill);
                    ShowNotification("Game end forced!");
                }
                
                if (PlayerControl.LocalPlayer != null && GUILayout.Button("Call Emergency Meeting", GuiStyles.ButtonStyle))
                {
                    PlayerControl.LocalPlayer.CmdReportDeadBody(null);
                    ShowNotification("Emergency meeting called!");
                }

                GUILayout.BeginHorizontal();
                bool prevInfiniteVision = InfiniteVision;
                InfiniteVision = GuiStyles.DrawBetterToggle(InfiniteVision, "Infinite Vision", "Infinite vision for all players");
                GuiStyles.DrawStatusIndicator(InfiniteVision);
                GUILayout.EndHorizontal();

                if (prevInfiniteVision != InfiniteVision && HudManager.Instance?.ShadowQuad != null)
                {
                    HudManager.Instance.ShadowQuad.gameObject.SetActive(!InfiniteVision);
                    PersistInfiniteVision();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Player Speed: {PlayerSpeed:F2}x", GuiStyles.LabelStyle);
                float previousSpeed = PlayerSpeed;
                PlayerSpeed = GUILayout.HorizontalSlider(PlayerSpeed, 0.5f, 6f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb);
                GUILayout.EndHorizontal();
                if (Mathf.Abs(previousSpeed - PlayerSpeed) > 0.01f)
                {
                    PersistPlayerSpeed();
                }

                GuiStyles.DrawSeparator();
                GUILayout.EndVertical();
            }

            private void DrawMovementTabIMGUI()
            {
                GUILayout.BeginVertical(GuiStyles.SectionStyle);
                GUILayout.Label("Movement Controls", GuiStyles.HeaderStyle);
                GuiStyles.DrawSeparator();
                
                if (ShipStatus.Instance == null)
                {
                    GUILayout.Label("This tab is available during a game.", GuiStyles.SubHeaderStyle);
                    GUILayout.Label("Join or start a match to use movement features.", GuiStyles.LabelStyle);
                    GUILayout.EndVertical();
                    return;
                }

                if (PlayerControl.LocalPlayer != null)
                {
                    bool previousNoclip = IsNoclipping;
                    IsNoclipping = GuiStyles.DrawBetterToggle(IsNoclipping, "Enable Noclip", "Allows walking through walls");
                    if (PlayerControl.LocalPlayer.Collider != null)
                        PlayerControl.LocalPlayer.Collider.enabled = !IsNoclipping;
                    if (previousNoclip != IsNoclipping)
                    {
                        PersistIsNoclipping();
                    }
                }

                if (teleportManager != null)
                {
                    if (GUILayout.Button("Teleport to Nearest Player", GuiStyles.ButtonStyle))
                    {
                        teleportManager.TeleportToPlayer(teleportManager.GetClosestPlayer());
                        ShowNotification("Teleported to the nearest player!");
                    }
                    
                    foreach (var location in teleportManager.Locations)
                    {
                        if (GUILayout.Button($"Teleport to {location.Key}", GuiStyles.ButtonStyle))
                        {
                            teleportManager.TeleportToLocation(location.Key);
                            ShowNotification($"Teleported to {location.Key}!");
                        }
                    }
                }

                GuiStyles.DrawSeparator();
                GUILayout.EndVertical();
            }

            private void DrawSabotageTabIMGUI()
            {
                GUILayout.BeginVertical(GuiStyles.SectionStyle);
                GUILayout.Label("Sabotage Controls", GuiStyles.HeaderStyle);
                GuiStyles.DrawSeparator();
                
                if (ShipStatus.Instance == null)
                {
                    GUILayout.Label("This tab is available during a game.", GuiStyles.SubHeaderStyle);
                    GUILayout.Label("Join or start a match to control sabotages and doors.", GuiStyles.LabelStyle);
                    GUILayout.EndVertical();
                    return;
                }

                if (GUILayout.Button("Close Cafeteria Doors", GuiStyles.ButtonStyle))
                {
                    SystemManager.CloseDoorsOfType(SystemTypes.Cafeteria);
                    ShowNotification("Cafeteria doors closed!");
                }
                if (GUILayout.Button("Close Storage Doors", GuiStyles.ButtonStyle))
                {
                    SystemManager.CloseDoorsOfType(SystemTypes.Storage);
                    ShowNotification("Storage doors closed!");
                }
                if (GUILayout.Button("Close Medbay Doors", GuiStyles.ButtonStyle))
                {
                    SystemManager.CloseDoorsOfType(SystemTypes.MedBay);
                    ShowNotification("Medbay doors closed!");
                }
                if (GUILayout.Button("Close Security Doors", GuiStyles.ButtonStyle))
                {
                    SystemManager.CloseDoorsOfType(SystemTypes.Security);
                    ShowNotification("Security doors closed!");
                }

                GuiStyles.DrawSeparator();
                GUILayout.EndVertical();
            }

            private void UpdateGameState()
            {
                if (PlayerControl.LocalPlayer == null) return;
                try
                {
                    if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null)
                    {
                        bool shadowQuadState = !InfiniteVision;
                        HudManager.Instance.ShadowQuad.gameObject.SetActive(shadowQuadState);
                    }
                    if (PlayerControl.LocalPlayer.MyPhysics != null)
                    {
                        PlayerControl.LocalPlayer.MyPhysics.Speed = PlayerSpeed;
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro em UpdateGameState: {ex}"); }
            }

            private void ShowNotification(string message)
            {
                try
                {
                    UnityEngine.Debug.Log($"[ModMenuCrew] Notification: {message}");
                    if (HudManager.Instance?.Notifier != null)
                    {
                        HudManager.Instance.Notifier.AddDisconnectMessage(message);
                    }
                    else
                    {
                        if (pendingNotifications == null)
                            pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>();
                        pendingNotifications.Add(message);
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error showing notification: {ex}"); }
            }

            void Update()
            {
                try
                {
                    // Toggle menu with F1
                    if (Input.GetKeyDown(ModMenuCrewPlugin.CfgMenuToggleKey?.Value ?? KeyCode.F1))
                    {
                        if (mainWindow != null) mainWindow.Enabled = !mainWindow.Enabled;
                    }

                    if (mainWindow != null && mainWindow.Enabled)
                    {
                        if (cheatManager != null) cheatManager.Update();
                    }

                    GameCheats.CheckTeleportInput();
                    UpdateGameState();

                    // Reset noclip if player leaves
                    if (IsNoclipping && PlayerControl.LocalPlayer?.Collider != null)
                    {
                        if (ShipStatus.Instance == null)
                        {
                            PlayerControl.LocalPlayer.Collider.enabled = true;
                            IsNoclipping = false;
                        }
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro DebuggerComponent.Update: {ex}"); }
            }

            void OnGUI()
            {
                if (mainWindow != null && mainWindow.Enabled) mainWindow.OnGUI();
            }

            private void LoadConfigValues()
            {
                PlayerSpeed = ModMenuCrewPlugin.CfgPlayerSpeed?.Value ?? PlayerSpeed;
                InfiniteVision = ModMenuCrewPlugin.CfgInfiniteVision?.Value ?? InfiniteVision;
                IsNoclipping = ModMenuCrewPlugin.CfgIsNoclipping?.Value ?? IsNoclipping;
            }

            private void PersistPlayerSpeed()
            {
                if (ModMenuCrewPlugin.CfgPlayerSpeed != null)
                {
                    ModMenuCrewPlugin.CfgPlayerSpeed.Value = PlayerSpeed;
                }
            }

            private void PersistInfiniteVision()
            {
                if (ModMenuCrewPlugin.CfgInfiniteVision != null)
                {
                    ModMenuCrewPlugin.CfgInfiniteVision.Value = InfiniteVision;
                }
            }

            private void PersistIsNoclipping()
            {
                if (ModMenuCrewPlugin.CfgIsNoclipping != null)
                {
                    ModMenuCrewPlugin.CfgIsNoclipping.Value = IsNoclipping;
                }
            }
        }
    }
}
