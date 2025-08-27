using System.Collections.Generic;
using ModMenuCrew.Messages;
using ModMenuCrew.Patches;
using ModMenuCrew.UI.Controls;
using ModMenuCrew.UI.Styles;
using UnityEngine;
using static ModMenuCrew.ModMenuCrewPlugin;

namespace ModMenuCrew.UI;

public class MenuSystem
{
    private readonly DebuggerComponent component;
    private readonly TabControl tabControl;
    private readonly Dictionary<SystemTypes, Vector2> teleportLocations;

    public MenuSystem(DebuggerComponent component)
    {
        this.component = component;
        this.tabControl = new TabControl();
        this.teleportLocations = InitializeTeleportLocations();

        InitializeTabs();
    }

    private Dictionary<SystemTypes, Vector2> InitializeTeleportLocations() => new()
    {
        { SystemTypes.Electrical, new Vector2(-8.6f, -7f) },
        { SystemTypes.Security, new Vector2(-13.3f, -5.9f) },
        { SystemTypes.MedBay, new Vector2(-10.5f, -3.5f) },
        { SystemTypes.Storage, new Vector2(-3.5f, -11.9f) },
        { SystemTypes.Cafeteria, new Vector2(0f, 0f) },
        { SystemTypes.Admin, new Vector2(3.5f, -7.5f) }
    };

    private void InitializeTabs()
    {
        tabControl.AddTab("Game", () =>
        {
            new MenuSection("Game Controls", DrawGameControls).Draw();
            new MenuSection("Enhanced Features", DrawEnhancedFeatures).Draw();
        });

        tabControl.AddTab("Movement", () =>
        {
            new MenuSection("Movement Controls", DrawMovementControls).Draw();
            new MenuSection("Teleport Options", DrawTeleportControls).Draw();
        });

        tabControl.AddTab("Sabotage", () =>
        {
            new MenuSection("Sabotage Controls", DrawSabotageControls).Draw();
        });

        tabControl.AddTab("Impostor", () =>
        {
            new MenuSection("Impostor Controls", DrawImpostorControls).Draw();
        });
    }

    public void Draw()
    {
        tabControl.Draw();
    }

    private void DrawGameControls()
    {
        if (GUILayout.Button("Force Start Game (Bypass)", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("ForceStartGame");
        }

        if (AmongUsClient.Instance.AmHost && GUILayout.Button("Force Game End", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("ForceGameEnd", GameOverReason.ImpostorsByKill.ToString());
        }

        if (GUILayout.Button("Call Emergency Meeting", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("CallEmergencyMeeting");
        }

        component.ForceImpostor = GUILayout.Toggle(component.ForceImpostor, "Force Impostor Role", GuiStyles.ToggleStyle);
        RoleManagerPatch.SetForceImpostor(component.ForceImpostor);
    }

    private void DrawEnhancedFeatures()
    {
        GUILayout.Label($"Player Speed: {component.PlayerSpeed:F2}x", GuiStyles.LabelStyle);
        component.PlayerSpeed = GUILayout.HorizontalSlider(
            component.PlayerSpeed, 0.5f, 6f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb
        );

        component.InfiniteVision = GUILayout.Toggle(component.InfiniteVision, "Infinite Vision", GuiStyles.ToggleStyle);

        if (GUILayout.Button("Complete All Tasks", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("CompleteAllTasks");
        }
    }

    private void DrawMovementControls()
    {
        if (PlayerControl.LocalPlayer != null)
        {
            bool noclipButton = GUILayout.Toggle(component.IsNoclipping, "Enable Noclip", GuiStyles.ToggleStyle);
            if (noclipButton != component.IsNoclipping)
            {
                PlayerControl.LocalPlayer.Collider.enabled = !noclipButton;
                component.IsNoclipping = noclipButton;
            }
        }
    }

    private void DrawTeleportControls()
    {
        if (GUILayout.Button("Teleport to Nearest Player", GuiStyles.ButtonStyle))
        {
            var target = GetClosestPlayer();
            if (target != null)
            {
                SendBypassCommand("TeleportToPlayer", target.PlayerId.ToString());
            }
        }

        if (AmongUsClient.Instance.AmHost && GUILayout.Button("Teleport All to Me", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("TeleportAllToMe");
        }

        GUILayout.Space(5);
        foreach (var location in teleportLocations)
        {
            if (GUILayout.Button($"Teleport to {location.Key}", GuiStyles.ButtonStyle))
            {
                SendBypassCommand("TeleportToLocation", location.Key.ToString(), location.Value.x.ToString(), location.Value.y.ToString());
            }
        }
    }

    private void DrawSabotageControls()
    {
        if (!ShipStatus.Instance?.MapPrefab)
        {
            GUILayout.Label("Host-only controls", GuiStyles.HeaderStyle);
            return;
        }

        if (GUILayout.Button("Trigger Reactor Meltdown", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("TriggerSabotage", SystemTypes.Reactor.ToString(), "128");
        }

        if (GUILayout.Button("Trigger Oxygen Depletion", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("TriggerSabotage", SystemTypes.LifeSupp.ToString(), "128");
        }

        if (GUILayout.Button("Trigger Lights Out", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("TriggerSabotage", SystemTypes.Electrical.ToString(), "128");
        }

        if (GUILayout.Button("Fix All Sabotages", GuiStyles.ButtonStyle))
        {
            SendBypassCommand("FixAllSabotages");
        }
    }

    private void DrawImpostorControls()
    {
        if (!PlayerControl.LocalPlayer?.Data.Role.IsImpostor ?? true)
        {
            GUILayout.Label("Impostor-only controls", GuiStyles.HeaderStyle);
            return;
        }

        component.NoKillCooldown = GUILayout.Toggle(component.NoKillCooldown, "No Kill Cooldown", GuiStyles.ToggleStyle);
        if (component.NoKillCooldown)
        {
            SendBypassCommand("SetKillCooldown", "0");
        }

        GUILayout.Space(5);
        GUILayout.Label($"Kill Cooldown: {component.KillCooldown:F0}s", GuiStyles.LabelStyle);
        float newKillCooldown = GUILayout.HorizontalSlider(
            component.KillCooldown, 10f, 60f, GuiStyles.SliderStyle, GUI.skin.horizontalSliderThumb
        );

        if (!component.NoKillCooldown && newKillCooldown != component.KillCooldown)
        {
            component.KillCooldown = newKillCooldown;
            SendBypassCommand("SetKillCooldown", component.KillCooldown.ToString());
        }
    }

    private void SendBypassCommand(string command, params string[] args)
    {

        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmConnected) return;

        // Cria uma mensagem personalizada
        var message = new CustomMessage(
            tag: 1, // Tag da mensagem
            senderId: PlayerControl.LocalPlayer.PlayerId, // ID do remetente
            senderName: PlayerControl.LocalPlayer.Data.PlayerName, // Nome do remetente
            content: $"{command}|{string.Join("|", args)}", // Conteúdo da mensagem
            type: MessageType.Command // Tipo da mensagem
        );

        // Envia a mensagem com bypass
        message.SendBypass();


    }

    private PlayerControl GetClosestPlayer()
    {
        PlayerControl closest = null;
        float closestDistance = float.MaxValue;

        foreach (var player in PlayerControl.AllPlayerControls)
        {
            if (player != PlayerControl.LocalPlayer && !player.Data.IsDead)
            {
                float distance = Vector2.Distance(
                    player.GetTruePosition(),
                    PlayerControl.LocalPlayer.GetTruePosition()
                );

                if (distance < closestDistance)
                {
                    closest = player;
                    closestDistance = distance;
                }
            }
        }

        return closest;
    }
}