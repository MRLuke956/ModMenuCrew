using System;
using System.Linq;
using AmongUs.Data;
using AmongUs.Data.Player;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using ModMenuCrew.Features;
using ModMenuCrew.Patches;
using ModMenuCrew.UI.Controls;
using ModMenuCrew.UI.Managers;
using ModMenuCrew.UI.Menus;
using ModMenuCrew.UI.Styles;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using InnerNet;

namespace ModMenuCrew
{
    [BepInPlugin(Id, "Among Us Mod Menu Crew", ModVersion)]
    [BepInProcess("Among Us.exe")]
    public class ModMenuCrewPlugin : BasePlugin
    {
        public const string Id = "com.crewmod.oficial";
        public const string ModVersion = "5.4.0";

        public DebuggerComponent Component { get; private set; } = null!;
        public static ModMenuCrewPlugin Instance { get; private set; }
        public Harmony Harmony { get; } = new Harmony(Id);
        private Harmony _harmony;

        public override void Load()
        {
            Instance = this;
            Instance.Log.LogInfo($"Plugin {Id} version {ModVersion} is loading.");
            // Registrar o tipo IL2CPP antes de instanciar
            try { ClassInjector.RegisterTypeInIl2Cpp<DebuggerComponent>(); } catch {}
            Component = AddComponent<DebuggerComponent>();
            Harmony.PatchAll();
            _harmony = new Harmony("com.modmenucrew.votetracker");
            if (this.Config != null) LobbyHarmonyPatches.InitializeConfig(this.Config);
            Instance.Log.LogInfo($"Plugin {Id} loaded successfully.");
        }

        public override bool Unload()
        {
            try
            {
                if (Component != null) Component.CleanupResources();
                Harmony?.UnpatchSelf();
                _harmony?.UnpatchSelf();
                // Avoid setting Instance to null to prevent NREs during teardown callbacks
            }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error during plugin unload: {ex}"); } // ex é usado aqui
            return base.Unload();
        }

        public class DebuggerComponent : MonoBehaviour
        {
            public bool DisableGameEnd { get; set; }
            public bool ForceImpostor { get; set; }
            public bool IsNoclipping { get; set; }
            public uint NetId;
            public float PlayerSpeed { get; set; } = 2.1f; public float KillCooldown { get; set; } = 25f;
            public bool InfiniteVision { get; set; }
            public bool NoKillCooldown { get; set; }
            public bool InstantWin { get; set; }
            private const int BanPointsPerClick = 10;
            private DragWindow mainWindow; private TabControl tabControl;
            private TabControl banAndPickTabControl;
            private TeleportManager teleportManager; private CheatManager cheatManager;
            private PlayerPickMenu playerPickMenu;
            private Il2CppSystem.Collections.Generic.List<string> pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>();
            // Estado UI: seleção de role em grids
            private int lobbyRoleGridIndex = 0;
            private int inGameRoleGridIndex = 0;


            public DebuggerComponent(IntPtr ptr) : base(ptr) { }

            public void CleanupResources()
            {
                try
                {
                    teleportManager = null; cheatManager = null; playerPickMenu = null;
                    mainWindow = null; tabControl = null; banAndPickTabControl = null;
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error resource cleanup: {ex}"); }
            }

            void OnDestroy() => CleanupResources();

            void Awake()
            {
                try
                {
                    ModMenuCrewPlugin.Instance.Log.LogInfo("DebuggerComponent: Awake started.");

                    InitializeFeatureManagers();
                    InitializeMainWindowIMGUI();
                    InitializeTabsForGameIMGUI();

                    ModMenuCrewPlugin.Instance.Log.LogInfo("DebuggerComponent: Awake completed.");
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Critical error DebuggerComponent.Awake: {ex}"); }
            }

            private void InitializeFeatureManagers() { teleportManager = new TeleportManager(); cheatManager = new CheatManager(); playerPickMenu = new PlayerPickMenu(); }

            private void InitializeMainWindowIMGUI()
            {
                // Apenas define uma largura e posição inicial. A altura será automática. (ajustado -3%)
                mainWindow = new DragWindow(new Rect(24, 24, 514, 0), $"ModMenuCrew v{ModMenuCrewPlugin.ModVersion}", DrawMainModWindowIMGUI)
                {
                    Enabled = false
                };
                // Altura mínima padrão do viewport (aba Game terá valor ainda menor)
                mainWindow.SetViewportMinHeight(160f);
            }

            private void InitializeTabsForGameIMGUI()
            {
                tabControl = new TabControl();
                tabControl.AddTab("Game", DrawGameTabIMGUI, "General game controls and basic settings");
                tabControl.AddTab("Movement", DrawMovementTabIMGUI, "Movement controls and teleportation");
                tabControl.AddTab("Sabotage", DrawSabotageTabIMGUI, "Sabotage and doors controls");
                tabControl.AddTab("Impostor", DrawImpostorTabIMGUI, "Impostor-specific options");
                if (cheatManager != null) tabControl.AddTab("Cheats", cheatManager.DrawCheatsTab, "Cheats and advanced features");
                // PlayerPick será gerenciado dinamicamente conforme estado (lobby ou in-game)

            }

            private void DrawMainModWindowIMGUI()
            {
                // Mostrar Lobby (DrawBan) quando ShipStatus.Instance é null; caso contrário, focar na aba Cheats
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
                    banAndPickTabControl.AddTab("Ban Menu", () => DrawBanMenuIMGUI(DateTime.Now), "Ban management and lobby");
                }

                // Exibir PlayerPick somente quando realmente em lobby (instância válida e conectado) ou in-game.
                bool connected = (AmongUsClient.Instance != null && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.NotJoined);
                bool hasContextObj = (LobbyBehaviour.Instance != null) || (ShipStatus.Instance != null);
                bool hasPlayers = false;
                try { hasPlayers = PlayerControl.AllPlayerControls != null && PlayerControl.AllPlayerControls.Count > 0; } catch { hasPlayers = false; }
                bool shouldShowPlayerPick = connected && hasContextObj && hasPlayers;
                if (shouldShowPlayerPick && playerPickMenu != null && !banAndPickTabControl.HasTab("PlayerPick"))
                {
                    banAndPickTabControl.AddTab("PlayerPick", playerPickMenu.Draw, "Player selection and management");
                }
                else if (!shouldShowPlayerPick && banAndPickTabControl.HasTab("PlayerPick"))
                {
                    banAndPickTabControl.RemoveTab("PlayerPick");
                }

                banAndPickTabControl.Draw();
            }
            private void DrawBanMenuIMGUI(DateTime dateTime)
            {
                bool mainLayoutStarted = false;
                try
                {
                    GUILayout.BeginVertical(GuiStyles.SectionStyle);
                    mainLayoutStarted = true;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Lobby Settings: {DateTime.Now:HH:mm}", GuiStyles.HeaderStyle);
                    bool isExpanded = GUILayout.Toggle(true, "▼", GuiStyles.ButtonStyle, GUILayout.Width(60));
                    GUILayout.EndHorizontal();
                    GuiStyles.DrawSeparator();

                    // Override de Role – somente para host, disponível no lobby
                    if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                    {
                        // Exibição role atual e seleção elegante de roles (override opcional)
                        string currentRoleText = "Current role: (lobby)";
                        Color roleColor = Color.white;
                        if (AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started && PlayerControl.LocalPlayer?.Data != null)
                        {
                            var rt = PlayerControl.LocalPlayer.Data.RoleType;
                            currentRoleText = $"Current role: {rt}";
                            bool isImpTeam = (rt == RoleTypes.Impostor || rt == RoleTypes.Shapeshifter);
                            roleColor = isImpTeam ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.6f, 0.95f, 1f);
                        }
                        var prevColor = GUI.color;
                        GUI.color = roleColor;
                        GUILayout.Label(currentRoleText, GuiStyles.LabelStyle);
                        GUI.color = prevColor;
                        GuiStyles.DrawSeparator();

                        GUILayout.Label("Role Override (Host)", GuiStyles.HeaderStyle);
                        bool prevOverride = ModMenuCrew.Features.ImpostorForcer.RoleOverrideEnabled;
                        bool newOverride = GUILayout.Toggle(prevOverride, "Enable role override", GuiStyles.ToggleStyle);
                        if (newOverride != prevOverride)
                        {
                            ModMenuCrew.Features.ImpostorForcer.SetRoleOverrideEnabled(newOverride);
                        }

                        var roles = ModMenuCrew.Features.ImpostorForcer.GetSupportedRoles();
                        var roleNames = roles.Select(r => r.ToString()).ToArray();
                        int currentIndex = System.Array.IndexOf(roles, ModMenuCrew.Features.ImpostorForcer.SelectedRoleForHost);
                        if (currentIndex < 0) currentIndex = 0;
                        lobbyRoleGridIndex = currentIndex;

                        GUI.enabled = newOverride;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Select your preferred role:", GuiStyles.LabelStyle);
                        var selectedRole = roles[Mathf.Clamp(lobbyRoleGridIndex, 0, roles.Length - 1)];
                        var saveColor = GUI.color;
                        GUI.color = GetRolePreviewColor(selectedRole);
                        GUILayout.Label($"{selectedRole}", GuiStyles.SubHeaderStyle, GUILayout.Width(120));
                        GUI.color = saveColor;
                        GUILayout.EndHorizontal();

                        int newIndex = DrawSimpleSelectionGrid(lobbyRoleGridIndex, roleNames, 1);
                        if (newIndex != lobbyRoleGridIndex)
                        {
                            lobbyRoleGridIndex = newIndex;
                            var chosen = roles[Mathf.Clamp(lobbyRoleGridIndex, 0, roles.Length - 1)];
                            ModMenuCrew.Features.ImpostorForcer.SetSelectedRoleForHost(chosen);
                            ShowNotification($"Selected host role: {chosen}");
                        }
                        GUI.enabled = true;

                        GuiStyles.DrawSeparator();
                    }

                    // Conteúdo do menu de banimento
                    var playerBanData = DataManager.Player?.ban;
                    if (isExpanded)
                    {
                        GUILayout.BeginVertical(GUI.skin.box);

                        // Ações de lobby/host: renderizar somente para HOST
                        if (PlayerControl.LocalPlayer && AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button("Remove Lobby/Map", GuiStyles.ButtonStyle))
                            {
                                GameCheats.MapCheats.DestroyMap();
                                ShowNotification("Map/Lobby removed by host!");
                            }
                            if (GUILayout.Button("Add Lobby/Map", GuiStyles.ButtonStyle))
                            {
                                GameCheats.MapCheats.SpawnLobby();
                                ShowNotification("Map/Lobby added by host!");
                            }
                            GuiStyles.DrawSeparator();
                            GUILayout.EndHorizontal();
                        }

                        if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                        {
                            if (GUILayout.Button("Rainbow Names (BAN)", GuiStyles.ButtonStyle))
                            {
                                // Removido SendChatMessage
                                StartCoroutine(ImpostorForcer.ForceNameRainbowForEveryone().WrapToIl2Cpp());
                            }
                        }
                        GUILayout.Label("Visual Name Cheats", GuiStyles.HeaderStyle);
                        if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                        {
                            if (GUILayout.Button("Name Changer (BAN)", GuiStyles.ButtonStyle))
                            {
                                // Removido SendChatMessage
                                ImpostorForcer.StartForceUniqueNamesForAll();
                                GameCheats.LocalVisualScanForEveryone(15f);
                                ShowNotification("Name changer activated for all players!");
                            }
                            if (GUILayout.Button("Stop Name Changer", GuiStyles.ButtonStyle))
                            {
                                ImpostorForcer.StopForceUniqueNames();
                                ShowNotification("Name changer stopped!");
                            }
                            if (GUILayout.Button("Reveal Roles in Names (BAN)", GuiStyles.ButtonStyle))
                            {
                                // Removido SendChatMessage
                                ImpostorForcer.HostNameManager.ToggleYtHostName();
                                ShowNotification("Roles revealed in player names!");
                            }
                        }

                        // Controles específicos de ban
                        // Exibir se: (a) não estiver no lobby; ou (b) estiver no lobby e NÃO for host
                        if (playerBanData != null)
                        {
                            bool isLobby = LobbyBehaviour.Instance != null;
                            bool isHost = (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost);

                            bool showControls = !isLobby || !isHost;
                            if (showControls)
                            {
                                int banMinutesLeft = playerBanData.BanMinutesLeft;
                                GUILayout.Label($"Ban Time Remaining: {banMinutesLeft} minutes", GuiStyles.LabelStyle);

                                if (GUILayout.Button($"Add Ban Time (+{BanPointsPerClick} pts)", GuiStyles.ButtonStyle))
                                {
                                    AddBanPoints(playerBanData, BanPointsPerClick);
                                }

                                if (playerBanData.BanPoints > 0 && GUILayout.Button("Remove ALL Bans", GuiStyles.ButtonStyle))
                                {
                                    RemoveAllBans(playerBanData);
                                }

                                if (playerBanData.BanPoints > 0)
                                {
                                    float banMinutes = playerBanData.BanMinutes;
                                    string timeDisplay = banMinutes < 60 ? $"{banMinutes:F0} minutes" : $"{banMinutes / 60:F1} hours";
                                    GUILayout.Label($"Current Ban Points: {playerBanData.BanPoints}", GuiStyles.LabelStyle);
                                    GUILayout.Label($"Time until Ban Removal: {timeDisplay}", GuiStyles.LabelStyle);
                                }
                            }
                        }
                        // Sem 'Ban data not available' para evitar espaço vazio

                        GuiStyles.DrawSeparator();
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndVertical();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ModMenuCrew] Error in DrawBanMenuIMGUI: {ex}");
                    if (mainLayoutStarted) GUILayout.EndVertical();
                    else GUILayout.Label("Error loading ban menu.", GuiStyles.ErrorStyle);
                }
            }

            private void AddBanPoints(PlayerBanData playerBanData, int points) { /* SEU CÓDIGO ORIGINAL AQUI */ if (playerBanData == null) { UnityEngine.Debug.LogError("[ModMenuCrew] PlayerBanData is null in AddBanPoints."); return; } playerBanData.BanPoints += points; playerBanData.OnBanPointsChanged?.Invoke(); playerBanData.PreviousGameStartDate = new Il2CppSystem.DateTime(DateTime.UtcNow.Ticks); UnityEngine.Debug.Log($"[ModMenuCrew] Ban points added. New value: {playerBanData.BanPoints}"); ShowNotification($"Ban points added: {points}. Total: {playerBanData.BanPoints}"); }
            private void RemoveAllBans(PlayerBanData playerBanData) { /* SEU CÓDIGO ORIGINAL AQUI */ if (playerBanData == null) { UnityEngine.Debug.LogError("[ModMenuCrew] PlayerBanData is null in RemoveAllBans."); return; } playerBanData.BanPoints = 0f; playerBanData.OnBanPointsChanged?.Invoke(); playerBanData.PreviousGameStartDate = new Il2CppSystem.DateTime(DateTime.MinValue.Ticks); UnityEngine.Debug.Log("[ModMenuCrew] All bans removed."); ShowNotification("All bans removed!"); }
            private void DrawGameTabIMGUI() { /* SEU CÓDIGO ORIGINAL AQUI */ GUILayout.BeginVertical(GuiStyles.SectionStyle); GUILayout.Label("Game Controls", GuiStyles.HeaderStyle); GuiStyles.DrawSeparator(); if (GUILayout.Button("Force Game End", GuiStyles.ButtonStyle)) { GameEndManager.ForceGameEnd(GameOverReason.ImpostorsByKill); ShowNotification("Game end forced!"); } if (PlayerControl.LocalPlayer != null && GUILayout.Button("Call Emergency Meeting", GuiStyles.ButtonStyle)) { PlayerControl.LocalPlayer.CmdReportDeadBody(null); ShowNotification("Emergency meeting called!"); } GUILayout.BeginHorizontal(); bool prevInfiniteVision = InfiniteVision; InfiniteVision = GuiStyles.DrawBetterToggle(InfiniteVision, "Infinite Vision", "Infinite vision for all players"); GuiStyles.DrawStatusIndicator(InfiniteVision); GUILayout.EndHorizontal(); if (prevInfiniteVision != InfiniteVision && HudManager.Instance?.ShadowQuad != null) { HudManager.Instance.ShadowQuad.gameObject.SetActive(!InfiniteVision); UnityEngine.Debug.Log($"[ModMenuCrew] Infinite vision changed to: {InfiniteVision}"); } GUILayout.BeginHorizontal(); GUILayout.Label($"Player Speed: {PlayerSpeed:F2}x", GuiStyles.LabelStyle); PlayerSpeed = GUILayout.HorizontalSlider(PlayerSpeed, 0.5f, 6f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb); GUILayout.EndHorizontal(); GuiStyles.DrawSeparator(); GUILayout.EndVertical(); }
            private void DrawMovementTabIMGUI() { /* SEU CÓDIGO ORIGINAL AQUI */ GUILayout.BeginVertical(GuiStyles.SectionStyle); GUILayout.Label("Movement Controls", GuiStyles.HeaderStyle); GuiStyles.DrawSeparator(); if (ShipStatus.Instance == null) { GUILayout.Label("This tab is available during a game.", GuiStyles.SubHeaderStyle); GUILayout.Label("Join or start a match to use movement features.", GuiStyles.LabelStyle); GUILayout.EndVertical(); return; } if (PlayerControl.LocalPlayer != null) { IsNoclipping = GuiStyles.DrawBetterToggle(IsNoclipping, "Enable Noclip", "Allows walking through walls"); if (PlayerControl.LocalPlayer.Collider != null) PlayerControl.LocalPlayer.Collider.enabled = !IsNoclipping; } if (teleportManager != null) { if (GUILayout.Button("Teleport to Nearest Player", GuiStyles.ButtonStyle)) { teleportManager.TeleportToPlayer(teleportManager.GetClosestPlayer()); ShowNotification("Teleported to the nearest player!"); } foreach (var location in teleportManager.Locations) { if (GUILayout.Button($"Teleport to {location.Key}", GuiStyles.ButtonStyle)) { teleportManager.TeleportToLocation(location.Key); ShowNotification($"Teleported to {location.Key}!"); } } } GuiStyles.DrawSeparator(); GUILayout.EndVertical(); }
            private void DrawSabotageTabIMGUI() { /* SEU CÓDIGO ORIGINAL AQUI */ GUILayout.BeginVertical(GuiStyles.SectionStyle); GUILayout.Label("Sabotage Controls", GuiStyles.HeaderStyle); GuiStyles.DrawSeparator(); if (ShipStatus.Instance == null) { GUILayout.Label("This tab is available during a game.", GuiStyles.SubHeaderStyle); GUILayout.Label("Join or start a match to control sabotages and doors.", GuiStyles.LabelStyle); GUILayout.EndVertical(); return; } if (GUILayout.Button("Close Cafeteria Doors", GuiStyles.ButtonStyle)) { SystemManager.CloseDoorsOfType(SystemTypes.Cafeteria); ShowNotification("Cafeteria doors closed!"); } if (GUILayout.Button("Close Storage Doors", GuiStyles.ButtonStyle)) { SystemManager.CloseDoorsOfType(SystemTypes.Storage); ShowNotification("Storage doors closed!"); } if (GUILayout.Button("Close Medbay Doors", GuiStyles.ButtonStyle)) { SystemManager.CloseDoorsOfType(SystemTypes.MedBay); ShowNotification("Medbay doors closed!"); } if (GUILayout.Button("Close Security Doors", GuiStyles.ButtonStyle)) { SystemManager.CloseDoorsOfType(SystemTypes.Security); ShowNotification("Security doors closed!"); } if (GUILayout.Button("Sabotage All", GuiStyles.ButtonStyle)) { SabotageService.TriggerReactorMeltdown(); SabotageService.TriggerOxygenDepletion(); SabotageService.TriggerLightsOut(); SabotageService.ToggleAllDoors(); SabotageService.TriggerAllSabotages(); ShowNotification("All sabotages activated!"); } GuiStyles.DrawSeparator(); GUILayout.EndVertical(); }
            private void DrawImpostorTabIMGUI() { /* SEU CÓDIGO ORIGINAL AQUI */ try { GUILayout.BeginVertical(GuiStyles.SectionStyle); GUILayout.Label("Impostor Controls", GuiStyles.HeaderStyle); GuiStyles.DrawSeparator(); if (ShipStatus.Instance == null || PlayerControl.LocalPlayer?.Data == null) { GUILayout.Label("This tab is available during a game.", GuiStyles.SubHeaderStyle); GUILayout.Label("Join or start a match to use impostor features.", GuiStyles.LabelStyle); GUILayout.EndVertical(); return; } if (PlayerControl.LocalPlayer?.Data != null) { var rt = PlayerControl.LocalPlayer.Data.RoleType; bool isImpTeam = (rt == RoleTypes.Impostor || rt == RoleTypes.Shapeshifter); var prevColor = GUI.color; GUI.color = isImpTeam ? new Color(0.9f,0.2f,0.2f) : new Color(0.6f,0.95f,1f); GUILayout.Label($"Your role: {rt}", GuiStyles.LabelStyle); GUI.color = prevColor; GUILayout.Space(2); GUILayout.Label("Select role (local):", GuiStyles.LabelStyle); var roles = ImpostorForcer.GetSupportedRoles(); var roleNames = roles.Select(r => r.ToString()).ToArray(); int currIdx = System.Array.IndexOf(roles, rt); if (currIdx < 0) currIdx = 0; if (inGameRoleGridIndex <= 0) inGameRoleGridIndex = currIdx; int newIdx = DrawSimpleSelectionGrid(inGameRoleGridIndex, roleNames, 1); if (newIdx != inGameRoleGridIndex) { inGameRoleGridIndex = newIdx; } GUILayout.BeginHorizontal(); if (GUILayout.Button("Apply now (Local)", GuiStyles.ButtonStyle)) { var chosen = roles[Mathf.Clamp(inGameRoleGridIndex, 0, roles.Length-1)]; ImpostorForcer.TrySetLocalPlayerRole(chosen); ShowNotification($"Role applied locally: {chosen}"); } if (!isImpTeam && GUILayout.Button("Become Impostor (Local)", GuiStyles.ButtonStyle)) { ImpostorForcer.TrySetLocalPlayerAsImpostor(); ShowNotification("You are now Impostor (local)."); } GUILayout.EndHorizontal();

                    if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                    {
                        GuiStyles.DrawSeparator();
                        GUILayout.Label("Host Actions (Real Role)", GuiStyles.SubHeaderStyle);
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Apply Override (Host)", GuiStyles.ButtonStyle))
                        {
                            ImpostorForcer.HostApplySelectedRoleNow();
                            ShowNotification("Override applied (host).");
                        }
                        if (GUILayout.Button("Force ME as Impostor (Host)", GuiStyles.ButtonStyle))
                        {
                            ImpostorForcer.HostForceImpostorNow(PlayerControl.LocalPlayer);
                            ShowNotification("You are now Impostor (host).");
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                NoKillCooldown = GuiStyles.DrawBetterToggle(NoKillCooldown, "No Kill Cooldown", "Removes kill cooldown time"); if (NoKillCooldown) { if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.SetKillTimer(0f); GUILayout.Label("Kill Cooldown: 0s (No cooldown)", GuiStyles.LabelStyle); } else { GUILayout.Label($"Kill Cooldown: {KillCooldown:F1}s", GuiStyles.LabelStyle); float newKillCooldown = GUILayout.HorizontalSlider(KillCooldown, 0f, 60f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb); if (Math.Abs(newKillCooldown - KillCooldown) > 0.01f) { KillCooldown = newKillCooldown; if (PlayerControl.LocalPlayer != null) PlayerControl.LocalPlayer.SetKillTimer(KillCooldown); } } GuiStyles.DrawSeparator(); GUILayout.EndVertical(); } catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error in DrawImpostorTabIMGUI: {ex}"); GUILayout.Label("Error loading impostor tab.", GuiStyles.ErrorStyle); } } // ex usado aqui
            private void UpdateGameState() { /* SEU CÓDIGO ORIGINAL AQUI */ if (PlayerControl.LocalPlayer == null) return; try { if (HudManager.Instance != null && HudManager.Instance.ShadowQuad != null) { bool shadowQuadState = !InfiniteVision; HudManager.Instance.ShadowQuad.gameObject.SetActive(shadowQuadState); if (HudManager.Instance.ShadowQuad.gameObject.activeSelf != shadowQuadState) { HudManager.Instance.ShadowQuad.gameObject.SetActive(shadowQuadState); UnityEngine.Debug.Log($"[ModMenuCrew] Corrigindo estado do ShadowQuad: {shadowQuadState}"); } } if (PlayerControl.LocalPlayer.MyPhysics != null) { PlayerControl.LocalPlayer.MyPhysics.Speed = PlayerSpeed; } } catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro em UpdateGameState: {ex}"); } } // ex usado aqui
            private float lastLogTime = 0f;
            private void ShowNotification(string message) { try { UnityEngine.Debug.Log($"[ModMenuCrew] Notification: {message}"); if (HudManager.Instance?.Notifier != null) { HudManager.Instance.Notifier.AddDisconnectMessage(message); while (pendingNotifications.Count > 0 && (pendingNotifications.Count > 5 || Time.time - lastLogTime > 0.5f)) { lastLogTime = Time.time; string pendingMsg = pendingNotifications[0]; pendingNotifications.RemoveAt(0); if (HudManager.Instance?.Notifier != null) HudManager.Instance.Notifier.AddDisconnectMessage(pendingMsg); } } else { if (pendingNotifications == null) pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>(); pendingNotifications.Add(message); } } catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error showing notification: {ex}"); if (pendingNotifications == null) pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>(); pendingNotifications.Add(message + " (Err)"); } } // ex usado aqui

            // Fallback de SelectionGrid para IL2CPP (evita MissingMethodException em GUIGridSizer)
            [HideFromIl2Cpp]
            private int DrawSimpleSelectionGrid(int selectedIndex, string[] labels, int columns)
            {
                if (labels == null || labels.Length == 0) return 0;
                if (columns <= 0) columns = 1;
                int newSelected = selectedIndex;
                int rows = Mathf.CeilToInt(labels.Length / (float)columns);
                int labelIdx = 0;
                for (int r = 0; r < rows; r++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < columns; c++)
                    {
                        if (labelIdx >= labels.Length)
                        {
                            GUILayout.FlexibleSpace();
                        }
                        else
                        {
                            bool isSel = (labelIdx == selectedIndex);
                            if (isSel) GUILayout.BeginVertical(GuiStyles.HighlightStyle);
                            var style = GuiStyles.ButtonStyle;
                            string text = isSel ? $"✓ {labels[labelIdx]}" : labels[labelIdx];
                            if (GUILayout.Button(text, style, GUILayout.ExpandWidth(true)))
                            {
                                newSelected = labelIdx;
                            }
                            if (isSel) GUILayout.EndVertical();
                        }
                        labelIdx++;
                    }
                    GUILayout.EndHorizontal();
                }
                return newSelected;
            }

            private Color GetRolePreviewColor(RoleTypes role)
            {
                switch (role)
                {
                    case RoleTypes.Impostor:
                        return new Color(0.9f, 0.2f, 0.2f);
                    case RoleTypes.Engineer:
                        return new Color(0.95f, 0.75f, 0.2f);
                    case RoleTypes.Scientist:
                        return new Color(0.2f, 0.85f, 0.4f);
                    case RoleTypes.Crewmate:
                    default:
                        return new Color(0.6f, 0.95f, 1f);
                }
            }


            void Update()
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.F1))
                    {
                        if (mainWindow != null) mainWindow.Enabled = !mainWindow.Enabled;
                    }

                    if (mainWindow != null && mainWindow.Enabled)
                    {
                        if (cheatManager != null) cheatManager.Update();
                        AdjustWindowSizeBySelectedTab();
                        EnsurePlayerPickTabVisibility();
                    }

                    GameCheats.CheckTeleportInput();
                    UpdateGameState();
                    ImpostorForcer.Update();
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro DebuggerComponent.Update: {ex}"); } // ex usado aqui
            }
            void OnGUI() { if (mainWindow != null && mainWindow.Enabled) mainWindow.OnGUI(); }
            // Ajusta a altura mínima do viewport para evitar espaço cinza dependendo da aba
            private void AdjustWindowSizeBySelectedTab()
            {
                try
                {
                    if (mainWindow == null || tabControl == null) return;
                    int idx = tabControl.GetSelectedTabIndex();
                    // 0 = Game, usa viewport menor para diminuir espaço vazio
                    if (idx == 0)
                        mainWindow.SetViewportMinHeight(120f);
                    else
                        mainWindow.SetViewportMinHeight(180f);
                }
                catch { }
            }

            // Exibe a aba PlayerPick apenas quando existe LobbyBehaviour.Instance (lobby) ou ShipStatus.Instance (in-game)
            private void EnsurePlayerPickTabVisibility()
            {
                try
                {
                    if (tabControl == null) return;
                    bool connected = (AmongUsClient.Instance != null && AmongUsClient.Instance.GameState != InnerNetClient.GameStates.NotJoined);
                    bool hasContextObj = (LobbyBehaviour.Instance != null) || (ShipStatus.Instance != null);
                    bool hasPlayers = false; try { hasPlayers = PlayerControl.AllPlayerControls != null && PlayerControl.AllPlayerControls.Count > 0; } catch { hasPlayers = false; }
                    bool shouldShow = connected && hasContextObj && hasPlayers;
                    bool hasTab = tabControl.HasTab("PlayerPick");

                    if (shouldShow && !hasTab && playerPickMenu != null)
                    {
                        tabControl.AddTab("PlayerPick", playerPickMenu.Draw, "Player selection and management");
                    }
                    else if (!shouldShow && hasTab)
                    {
                        tabControl.RemoveTab("PlayerPick");
                    }
                }
                catch { }
            }

            
        }
    }

}
