using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Reactor.Utilities.Attributes;
using Reactor.Utilities.ImGui;
using UnityEngine;
using Reactor;
using Hazel;
using AmongUs.GameOptions;

namespace ModMenuCrew;

[BepInAutoPlugin]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class ModMenuCrewPlugin : BasePlugin
{
    public Harmony Harmony { get; } = new(Id);
    public DebuggerComponent Component { get; private set; } = null!;

    public override void Load()
    {
        Component = this.AddComponent<DebuggerComponent>();
        Harmony.PatchAll();
      
    }
   



    [RegisterInIl2Cpp]
    public class DebuggerComponent : MonoBehaviour
    {
        [HideFromIl2Cpp]
        public bool DisableGameEnd { get; set; }
        public bool Impostor { get; set; }
        public bool IsNoclipping { get; set; }
        bool previousNoclipState;
        public uint NetId;
       

        internal static class RandomHelper
        {
            private static Il2CppSystem.Random random = new Il2CppSystem.Random();

            public static byte GetRandomByte()
            {
                return (byte)random.Next(0, 15);
            }
        }
        byte randomByte = RandomHelper.GetRandomByte();
        [HideFromIl2Cpp]
        public DragWindow TestWindow { get; }

        public DebuggerComponent(IntPtr ptr) : base(ptr)
        {
            string GenerateRandomId(int length)
            {
                string id = Guid.NewGuid().ToString();

                return id.Substring(0, length);
            }

            // Chamar quando criar o menu
            string randomId = GenerateRandomId(7);

            TestWindow = new DragWindow(new Rect(15, 15, 100, 100), $"BETA MOD MENU BY MRLukex {randomId}", () =>
            {


                if (!ShipStatus.Instance)
                {

                    GUILayout.Label("\r Join A Game", GUILayout.ExpandWidth(true));
                    string banTime = StatsManager.Instance.BanMinutes.ToString("F0");
                    string iAmBan = StatsManager.Instance.AmBanned.ToString();
                    if (GUILayout.Button($"ban ({banTime} min left,\b am i banned? ({iAmBan})"))
                    {
                        StatsManager.Instance.BanPoints += 100;

                    }
                    if (GUILayout.Button("removeban"))
                    {

                        StatsManager.Instance.BanPoints = 0;
                    }

                }


                if (ShipStatus.Instance && AmongUsClient.Instance.AmClient)
                {
                    MessageWriter messageWriter2 = AmongUsClient.Instance.StartRpc(this.NetId, 6, SendOption.Reliable);
                    messageWriter2.Write(name);
                    messageWriter2.EndMessage();


                    // NO WORKING GUILayout.Label("Name: " + DataManager.Player.Customization.Name);
                    //   DisableGameEnd = GUILayout.Toggle(DisableGameEnd, "Disable game end (Host Only)", GUILayout.ExpandWidth(true));


                    if (GUILayout.Button("Force game end (Host Only)"))
                    {

                        ShipStatus.Instance.enabled = false;
                        GameManager.Instance.RpcEndGame(GameOverReason.ImpostorBySabotage, false);
                        return;

                    }

                    if (GUILayout.Button("Call a meeting"))
                    {
                        PlayerControl.LocalPlayer.CmdReportDeadBody(null);

                    }
                    if (GUILayout.Button("Color Changer"))
                    {
                            byte randomByte = RandomHelper.GetRandomByte();
                            PlayerControl.LocalPlayer.CmdCheckColor(randomByte);
                    }

                        if (GUILayout.Button("Scanner"))
                        {
                            byte b = (byte)(PlayerControl.LocalPlayer.scannerCount + 1);
                            PlayerControl.LocalPlayer.scannerCount = b;
                            byte b2 = b;
                            PlayerControl.LocalPlayer.SetScanner(true, b2);
                            if (AmongUsClient.Instance.AmClient)
                            {
                                MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(this.NetId, (byte)RpcCalls.SetScanner, SendOption.Reliable);
                                messageWriter.StartMessage((byte)RpcCalls.SetScanner);
                            
                                messageWriter.Write(true);
                                messageWriter.Write(b2);
                                messageWriter.EndMessage();
                            }
                        }

                        if (PlayerControl.LocalPlayer)
                        {

                            var noclipButton = GUILayout.Toggle(IsNoclipping, "Noclip");

                            if (noclipButton != IsNoclipping)
                            {

                                if (noclipButton)
                                {

                                    //Muda estado
                                    PlayerControl.LocalPlayer.Collider.enabled = false;
                                    IsNoclipping = true;


                                    //Compara com estado anterior
                                    if (previousNoclipState != IsNoclipping)
                                    {

                                        AddNotification("OFF", "ON");

                                    }

                                    previousNoclipState = IsNoclipping;

                                }
                                else
                                {

                                    //Muda estado 
                                    PlayerControl.LocalPlayer.Collider.enabled = true;
                                    IsNoclipping = false;

                                    //Compara com estado anterior
                                    if (previousNoclipState != IsNoclipping)
                                    {

                                        AddNotification("ON", "OFF");

                                    }

                                    previousNoclipState = IsNoclipping;

                                }

                            }

                        }


                        void AddNotification(string previous, string current)
                        {

                            string message = $"Noclip -> {current} ";

                            DestroyableSingleton<HudManager>.Instance.Notifier
                              .AddItem($"{message}");

                        }

                        if (!PlayerControl.LocalPlayer.Data.Role.IsImpostor && ShipStatus.Instance.MapPrefab)
                        {
                            if (GUILayout.Button("BeImpostor"))
                            {
                                PlayerControl.LocalPlayer.SetRole(RoleTypes.Shapeshifter);
                                PlayerControl.LocalPlayer.Data.Role.CanUseKillButton = false;

                            }
                        }

                        if (GUILayout.Button("Teleport Closest Player"))
                        {
                            PlayerControl closestPlayer = GetClosestPlayer();

                            if (closestPlayer)
                            {

                                PlayerControl.LocalPlayer.transform.position = closestPlayer.transform.position;

                            }

                        }

                        PlayerControl GetClosestPlayer()
                        {
                            PlayerControl closest = null;
                            float closestDistance = Mathf.Infinity;

                            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                            {
                                if (player == PlayerControl.LocalPlayer) continue;
                                if (!player.Data.IsDead)
                                {


                                    float distance = (player.transform.position - PlayerControl.LocalPlayer.transform.position).sqrMagnitude;

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

                    var players = GameData.Instance.AllPlayers;
                    foreach (var player in players)
                    {
                        if (!player.Role.IsImpostor)
                        {
                            continue;
                        }
                        player.PlayerName.Color(Color.blue);
                        // Informações básicas
                        string status = player.Role.IsImpostor
                         ? $"<color=red>Sim</color>"
                         : "Não";

                        GUILayout.Label($"Nome: {player.PlayerName} - Impostor: {status}");

                    }

                }
            )
            {
                Enabled = false,
            };

        }
        public void Draw()
        {
            GUILayout.BeginVertical("TestWindow");

            GUILayout.HorizontalScrollbar(15f,15f,15f,15);
            GUILayout.BeginHorizontal();

            // elementos horizontais
           

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            // etc

        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                TestWindow.Enabled = !TestWindow.Enabled;
            }
        }

        private void OnGUI()
        {
            TestWindow.OnGUI();
        }
    }


}
