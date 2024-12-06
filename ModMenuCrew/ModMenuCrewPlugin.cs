using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor.Utilities.Attributes;
using Reactor.Utilities.ImGui;
using UnityEngine;
using Reactor;
using AmongUs.GameOptions;
using ModMenuCrew.UI.Controls;
using ModMenuCrew.UI.Styles;
using ModMenuCrew.UI.Managers;
using Hazel;
using System.Linq;
using Reactor.Utilities.Extensions;

namespace ModMenuCrew;

[BepInPlugin(Id, "ModMenu", ModVersion)]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public class ModMenuCrewPlugin : BasePlugin
{
    public const string Id = "com.mrluke.crewmod";
    public const string ModVersion = "2.0.0";
    public DebuggerComponent Component { get; private set; } = null!;
    public static ModMenuCrewPlugin Instance { get; private set; }
    public Harmony Harmony { get; } = new Harmony(Id);

    public override void Load()
    {
        Instance = this;
        Component = this.AddComponent<DebuggerComponent>();
        Harmony.PatchAll();
    }

    [RegisterInIl2Cpp]
    public class DebuggerComponent : MonoBehaviour
    {
        // Core properties
        public bool DisableGameEnd { get; set; }
        public bool ForceImpostor { get; set; }
        public bool IsNoclipping { get; set; }
        public uint NetId;

        // Game modification properties
        public float PlayerSpeed { get; set; } = 1.8f;
        public float KillCooldown { get; set; } = 25f * Time.deltaTime;
        public bool InfiniteVision { get; set; }
        public bool NoKillCooldown { get; set; }
        public bool InstantWin { get; set; }

        // UI Components
        private DragWindow mainWindow;
        private TabControl tabControl;
        private TeleportManager teleportManager;
        private ImpostorManager impostorManager;
        private ChatManager chatManager;
        private CheatManager cheatManager;
        private const byte CUSTOM_RPC_ID = 201;
        private bool noKillCooldown = false;
        private float killCooldown = 25f * Time.deltaTime;

        public DebuggerComponent(IntPtr ptr) : base(ptr)
        {
            mainWindow = new DragWindow(
                new Rect(15, 15, 350, 500),
                $"Among Us Mod Menu v{ModVersion}",
                DrawMenu
            )
            { Enabled = false };

            tabControl = new TabControl();
            teleportManager = new TeleportManager();
            impostorManager = new ImpostorManager();
            chatManager = new ChatManager();
            cheatManager = new CheatManager();
            InitializeTabs();
        }

        private void InitializeTabs()
        {
            tabControl.AddTab("Game", DrawGameTab);
            tabControl.AddTab("Movement", DrawMovementTab);
            tabControl.AddTab("Sabotage", DrawSabotageTab);
            tabControl.AddTab("Impostor", DrawImpostorTab);
            tabControl.AddTab("Chat", DrawChatTab);
            tabControl.AddTab("Cheats",cheatManager.DrawCheatsTab);
        }

        private void DrawMenu()
        {
            if (!ShipStatus.Instance)
            {
                DrawBanMenu();
                return;
            }

            if (AmongUsClient.Instance.AmClient)
            {
                tabControl.Draw();
            }
        }

        private void DrawBanMenu()
        {
            try
            {
                // Begin the main vertical layout
                GUILayout.BeginVertical(GuiStyles.SectionStyle);

                // Header section with collapsible toggle
                GUILayout.BeginHorizontal();
                GUILayout.Label("Join Game Settings", GuiStyles.HeaderStyle);
                bool isExpanded = GUILayout.Toggle(true, "▼", GuiStyles.ButtonStyle, GUILayout.Width(30));
                GUILayout.EndHorizontal();

                if (isExpanded)
                {
                    // Ban controls section
                    GUILayout.BeginVertical(GUI.skin.box);

                    // Display current ban time and add ban button
                    string banTime = StatsManager.Instance.BanMinutes.ToString("F0");
                    if (GUILayout.Button($"Add Ban Time ({banTime} min left)", GuiStyles.ButtonStyle))
                    {
                        try
                        {
                            StatsManager.Instance.BanPoints += 100;
                            StatsManager.Instance.SaveStats();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error adding ban time: {ex}");
                        }
                    }

                    // Remove all bans button
                    GUI.enabled = StatsManager.Instance.BanPoints > 0;
                    if (GUILayout.Button("Remove All Bans", GuiStyles.ButtonStyle))
                    {
                        try
                        {
                            StatsManager.Instance.BanPoints = 0;
                            StatsManager.Instance.SaveStats();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error removing bans: {ex}");
                        }
                    }
                    GUI.enabled = true;

                    // Display ban status if there are active bans
                    if (StatsManager.Instance.BanPoints > 0)
                    {
                        float minutes = StatsManager.Instance.BanMinutes;
                        string timeDisplay = minutes < 60
                            ? $"{minutes:F0} minutes"
                            : $"{minutes / 60:F1} hours";

                        GUILayout.Label($"Current Ban Points: {StatsManager.Instance.BanPoints}", GuiStyles.LabelStyle);
                        GUILayout.Label($"Time Until Unbanned: {timeDisplay}", GuiStyles.LabelStyle);
                    }

                    GUILayout.EndVertical();
                }

                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawBanMenu: {ex}");
                GUILayout.BeginVertical(GuiStyles.SectionStyle);
                GUILayout.Label("Error loading ban menu. Please try again.", GuiStyles.ErrorStyle);
                GUILayout.EndVertical();
            }
        }

        private void DrawGameTab()
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            GUILayout.Label("Game Controls", GuiStyles.HeaderStyle);

            if (GUILayout.Button("Force Game End", GuiStyles.ButtonStyle))
            {
                GameEndManager.ForceGameEnd(GameOverReason.HumansByTask);
            }

            if (GUILayout.Button("Call Emergency Meeting", GuiStyles.ButtonStyle))
            {
                PlayerControl.LocalPlayer.CmdReportDeadBody(null);
            }

            InfiniteVision = GUILayout.Toggle(InfiniteVision, "Infinite Vision", GuiStyles.ToggleStyle);

            GUILayout.Label($"Player Speed: {PlayerSpeed:F2}x", GuiStyles.LabelStyle);
            PlayerSpeed = GUILayout.HorizontalSlider(PlayerSpeed, 0.5f, 6f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb);

            GUILayout.EndVertical();
        }

        private void DrawMovementTab()
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            GUILayout.Label("Movement Controls", GuiStyles.HeaderStyle);

            if (PlayerControl.LocalPlayer != null)
            {
                IsNoclipping = GUILayout.Toggle(IsNoclipping, "Enable Noclip", GuiStyles.ToggleStyle);
                PlayerControl.LocalPlayer.Collider.enabled = !IsNoclipping;
            }

            if (GUILayout.Button("Teleport to Nearest Player", GuiStyles.ButtonStyle))
            {
                teleportManager.TeleportToPlayer(teleportManager.GetClosestPlayer());
            }

            foreach (var location in teleportManager.Locations)
            {
                if (GUILayout.Button($"Teleport to {location.Key}", GuiStyles.ButtonStyle))
                {
                    teleportManager.TeleportToLocation(location.Key);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawSabotageTab()
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);

            if (GUILayout.Button("Close Cafeteria Doors", GuiStyles.ButtonStyle))
            {
                SystemManager.CloseDoorsOfType(SystemTypes.Cafeteria);
            }

            if (GUILayout.Button("Close Storage Doors", GuiStyles.ButtonStyle))
            {
                SystemManager.CloseDoorsOfType(SystemTypes.Storage);
            }

            if (GUILayout.Button("Close Medbay Doors", GuiStyles.ButtonStyle))
            {
                SystemManager.CloseDoorsOfType(SystemTypes.MedBay);
            }

            if (GUILayout.Button("Close Security Doors", GuiStyles.ButtonStyle))
            {
                SystemManager.CloseDoorsOfType(SystemTypes.Security);
            }
           
            if (GUILayout.Button("Sabotage All", GuiStyles.ButtonStyle))
            {
                SabotageService.TriggerReactorMeltdown();
                SabotageService.TriggerOxygenDepletion();
                SabotageService.TriggerLightsOut();
            }

            GUILayout.EndVertical();
        }

        private void DrawImpostorTab()
        {
            try
            {
                GUILayout.BeginVertical(GuiStyles.SectionStyle);
                GUILayout.Label("Impostor Controls", GuiStyles.HeaderStyle);

                if (PlayerControl.LocalPlayer != null)
                {
                    bool isImpostor = PlayerControl.LocalPlayer.Data?.Role?.IsImpostor ?? false;

                    if (isImpostor)
                    {
                        // Kill cooldown toggle
                        bool prevNoKillCooldown = noKillCooldown;
                        noKillCooldown = GUILayout.Toggle(noKillCooldown, "No Kill Cooldown", GuiStyles.ToggleStyle);

                        // Kill cooldown slider
                        GUILayout.Label($"Kill Cooldown: {killCooldown:F1}s", GuiStyles.LabelStyle);
                        killCooldown = GUILayout.HorizontalSlider(killCooldown, 0f, 60f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb);

                        // Apply kill cooldown changes
                        if (noKillCooldown != prevNoKillCooldown || !noKillCooldown)
                        {
                            try
                            {
                                PlayerControl.LocalPlayer.SetKillTimer(noKillCooldown ? 0f : killCooldown);

                                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                                writer.StartMessage(5);
                                writer.Write(AmongUsClient.Instance.GameId);
                                writer.StartMessage(CUSTOM_RPC_ID);
                                writer.Write(PlayerControl.LocalPlayer.PlayerId);
                                writer.Write(noKillCooldown);
                                writer.Write(killCooldown);
                                writer.EndMessage();
                                AmongUsClient.Instance.SendOrDisconnect(writer);
                                writer.Recycle();
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Error updating kill cooldown: {ex}");
                            }
                        }

                        // Kill all button
                        if (GUILayout.Button("Instant Kill All", GuiStyles.ButtonStyle))
                        {
                            try
                            {
                                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                                {
                                    if (!player.Data.Role.IsImpostor && !player.Data.IsDead)
                                    {
                                        PlayerControl.LocalPlayer.MurderPlayer(player, MurderResultFlags.Succeeded);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Error in kill all: {ex}");
                            }
                        }
                    }
                    else
                    {
                        // Force impostor button
                        if (GUILayout.Button("Force Impostor Role", GuiStyles.ButtonStyle))
                        {
                            try
                            {
                                if (AmongUsClient.Instance != null)
                                {
                                    MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                                    writer.StartMessage(5);
                                    writer.Write(AmongUsClient.Instance.GameId);
                                    writer.StartMessage(CUSTOM_RPC_ID);
                                    writer.Write(PlayerControl.LocalPlayer.PlayerId);
                                    writer.EndMessage();

                                    if (AmongUsClient.Instance.AmHost)
                                    {
                                        MessageWriter hostWriter = AmongUsClient.Instance.StartRpcImmediately(
                                            PlayerControl.LocalPlayer.NetId,
                                            (byte)RpcCalls.SetRole,
                                            SendOption.Reliable
                                        );
                                        hostWriter.Write((byte)RoleTypes.Impostor);
                                        AmongUsClient.Instance.FinishRpcImmediately(hostWriter);
                                        PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Impostor);
                                    }
                                    else
                                    {
                                        AmongUsClient.Instance.SendOrDisconnect(writer);
                                    }
                                    writer.Recycle();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"Error forcing impostor role: {ex}");
                            }
                        }
                    }
                }
                else
                {
                    GUILayout.Label("Join a game first!", GuiStyles.ErrorStyle);
                }

                GUILayout.EndVertical();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in DrawMenu: {ex}");
                GUILayout.Label("An error occurred. Check logs for details.", GuiStyles.ErrorStyle);
            }
        }

        [HarmonyPatch(nameof(PlayerControl.HandleRpc))]
        [HarmonyPrefix]
        public static bool HandleRpcPrefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
        {
            if (callId == CUSTOM_RPC_ID)
            {
                try
                {
                    byte playerId = reader.ReadByte();
                    // Fixed: Using FirstOrDefault with proper lambda syntax
                    PlayerControl player = PlayerControl.AllPlayerControls.ToArray().FirstOrDefault(x => x.PlayerId == playerId);

                    if (reader.BytesRemaining > 0)
                    {
                        // Handle kill cooldown sync
                        bool noKill = reader.ReadBoolean();
                        float cooldown = reader.ReadSingle();
                        if (player != null)
                        {
                            player.SetKillTimer(noKill ? 0f : cooldown);
                        }
                    }
                    else if (AmongUsClient.Instance.AmHost && player != null)
                    {
                        // Handle force impostor
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                            player.NetId,
                            (byte)RpcCalls.SetRole,
                            SendOption.Reliable
                        );
                        writer.Write((byte)RoleTypes.Impostor);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                        player.RpcSetRole(RoleTypes.Impostor);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in HandleRpcPrefix: {ex}");
                }
            }
            return true;
        }

        private void DrawChatTab()
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);

            GUILayout.Label("Chat Settings", GuiStyles.HeaderStyle);
            chatManager.DrawSettings();

           

            GUILayout.Space(10);
            GUILayout.Label("Quick Messages", GuiStyles.HeaderStyle);
            chatManager.DrawQuickMessages();

            GUILayout.EndVertical();
        }

        private void UpdateGameState()
        {

            if (InfiniteVision)
            {
               PlayerControl.LocalPlayer.Data.Destroy();
            }

            if (NoKillCooldown && PlayerControl.LocalPlayer.Data.Role.IsImpostor)
            {
                PlayerControl.LocalPlayer.SetKillTimer(0f);
            }

            PlayerControl.LocalPlayer.MyPhysics.Speed = PlayerSpeed;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                mainWindow.Enabled = !mainWindow.Enabled;
            }

            cheatManager.Update();
            UpdateGameState();
        }

        private void OnGUI()
        {
            mainWindow.OnGUI();
        }
    }
}