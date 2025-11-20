using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using ModMenuCrew.Features;
using ModMenuCrew.UI.Styles;
using UnityEngine;

namespace ModMenuCrew.UI.Menus
{
    public class PlayerPickMenu
    {
        /*───────────────────── UTIL ────────────────────*/
        private static readonly RectOffset Margin4 = CreateRectOffset(4, 4, 4, 4);
        private static readonly RectOffset Margin0 = CreateRectOffset(0, 0, 0, 0);
        private static readonly RectOffset Padding4_0_4_0 = CreateRectOffset(4, 0, 4, 0);
        private static readonly RectOffset Padding0_0_4_0 = CreateRectOffset(0, 0, 4, 0);
        private static readonly RectOffset Padding0_4_4_4 = CreateRectOffset(0, 4, 4, 4);
        private static readonly RectOffset Padding4_4_2_2 = CreateRectOffset(4, 4, 2, 2);
        private static readonly RectOffset Padding8_8_4_4 = CreateRectOffset(8, 8, 4, 4);
        private static readonly GUIContent EmptyContent = GUIContent.none; 
        private static readonly List<byte> KeysToRemove = new();

        private static RectOffset CreateRectOffset(int left, int right, int top, int bottom)
        {
            var offset = new RectOffset();
            offset.left = left;
            offset.right = right;
            offset.top = top;
            offset.bottom = bottom;
            return offset;
        }

        /*─────────────────── STATIC DATA ───────────────*/
        private static readonly List<RoleTypes> AllRoles = new();
        private static GUIStyle ColorBoxStyle;
        private static GUIStyle PlayerNameStyle;
        private static GUIStyle ImpostorNameStyle;
        private static GUIStyle StatusStyle;
        private static GUIStyle PreAssignLabelStyle;
        private static GUIStyle RoleButtonStyle;
        private static GUIStyle PreAssignButtonStyle;

        static PlayerPickMenu()
        {
            foreach (RoleTypes r in Enum.GetValues(typeof(RoleTypes)))
                if (r is not (RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost))
                    AllRoles.Add(r);
            AllRoles.Sort((a, b) =>
                string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        // --- ESTRUTURA DE CACHE OTIMIZADA (POOLABLE) ---
        private class CachedPlayerData
        {
            public PlayerControl Player;
            public string Name;
            public bool IsImpostor;
            public bool IsDead;
            public Color DisplayColor;
            public string StatusText;
            public int SortPriority;
            public bool Disconnected;
            public bool Active; // Para Pooling
        }

        private uint LastRefreshFrame = 0;
        private float LastCleanupTime = 0f;
        private Vector2 DropScroll;
        private bool ShowImp = true, ShowCrew = true, ShowFilters = false;
        private byte? OpenDrop = null;
        private byte? OpenPreAssign = null;
        
        // Pool de dados para evitar alocações
        private readonly List<CachedPlayerData> DataPool = new(15); 
        private readonly List<CachedPlayerData> ActiveList = new(15); // Lista para exibição
        
        private readonly HashSet<byte> TriedFix = new();
        private readonly Dictionary<byte, Color> DeadColorCache = new(); 

        public void Draw()
        {
            if (PlayerControl.LocalPlayer == null) return;
            InitStyles(); 
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            DrawHeader();
            DrawFilters();
            DrawPlayerList();
            GUILayout.EndVertical();
        }

        private void InitStyles()
        {
            if (ColorBoxStyle != null) return; 

            ColorBoxStyle = new GUIStyle(GUI.skin.box)
            {
                stretchWidth = false,
                stretchHeight = false,
                fixedWidth = 20,
                fixedHeight = 20,
                margin = Margin4,
                padding = Margin0
            };
            ColorBoxStyle.normal.background = Texture2D.whiteTexture;

            PlayerNameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Normal,
                padding = Padding4_0_4_0,
                normal = { textColor = Color.white } // Base branca para tinting
            };

            ImpostorNameStyle = new GUIStyle(PlayerNameStyle)
            {
                fontStyle = FontStyle.Bold,
                padding = Padding0_0_4_0
            };

            StatusStyle = new GUIStyle(PlayerNameStyle)
            {
                padding = Padding0_4_4_4
            };

            PreAssignLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                padding = Padding4_4_2_2,
                normal = { textColor = Color.white }
            };

            RoleButtonStyle = new GUIStyle(GuiStyles.ButtonStyle)
            {
                fontSize = 14,
                padding = Padding8_8_4_4,
                margin = Margin4,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            PreAssignButtonStyle = new GUIStyle(GuiStyles.ButtonStyle)
            {
                normal = { textColor = Color.white }
            }; 
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player Selection", GuiStyles.HeaderStyle);
            GUILayout.FlexibleSpace();
            var filtLabel = ShowFilters ? "Filters ▲" : "Filters ▼";
            ShowFilters = GUILayout.Toggle(ShowFilters, filtLabel,
                                           GuiStyles.ButtonStyle, GUILayout.Width(88));
            GUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            if (!ShowFilters) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Role Filters:", GuiStyles.SubHeaderStyle);
            GUILayout.BeginHorizontal();
            ShowImp = GUILayout.Toggle(ShowImp, "Show Impostors", GuiStyles.ToggleStyle);
            ShowCrew = GUILayout.Toggle(ShowCrew, "Show Crewmates", GuiStyles.ToggleStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawPlayerList()
        {
            // Atualiza o cache apenas a cada 30 frames (~0.5s a 60fps)
            // Isso evita chamadas caras de Interop a cada OnGUI
            if (Time.frameCount - LastRefreshFrame >= 30)
            {
                RefreshCache();
            }

            bool anyShown = false;
            // Itera sobre a lista de exibição sem alocações
            for (int i = 0; i < ActiveList.Count; i++)
            {
                var data = ActiveList[i];
                if (ShouldShow(data))
                {
                    DrawPlayerEntry(data);
                    anyShown = true;
                }
            }

            if (!anyShown)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("No players to display.", GuiStyles.SubHeaderStyle);
                GUILayout.Label("Join a lobby or wait for players to appear.", GuiStyles.LabelStyle);
                GUILayout.EndVertical();
            }
        }

        private void RefreshCache()
        {
            LastRefreshFrame = (uint)Time.frameCount;
            float currentTime = Time.unscaledTime;

            // Marca todos como inativos para reciclagem
            for (int i = 0; i < DataPool.Count; i++) DataPool[i].Active = false;
            ActiveList.Clear();

            var allPlayers = PlayerControl.AllPlayerControls;
            // HashSet para busca rápida de IDs presentes (para limpeza de DeadCache)
            var currentIds = new HashSet<byte>();

            int poolIndex = 0;

            foreach (var p in allPlayers)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;

                currentIds.Add(p.PlayerId);

                // Obter ou criar objeto do pool
                CachedPlayerData data;
                if (poolIndex < DataPool.Count)
                {
                    data = DataPool[poolIndex];
                }
                else
                {
                    data = new CachedPlayerData();
                    DataPool.Add(data);
                }
                poolIndex++;

                // Preencher dados
                data.Active = true;
                data.Player = p;
                data.Disconnected = false;
                data.IsDead = p.Data.IsDead;
                data.IsImpostor = p.Data.Role?.IsImpostor == true;
                data.Name = p.Data.PlayerName;

                // Calcular Tasks (simplificado)
                int done = 0;
                int total = 0;
                var tasks = p.Data.Tasks;
                if (tasks != null)
                {
                    total = tasks.Count;
                    for (int k = 0; k < total; k++) if (tasks[k].Complete) done++;
                }
                
                bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
                data.StatusText = (data.IsImpostor ? "" : $" Tasks: {done}/{total}")
                                + (data.IsDead ? " ⚪ Dead" : " 🔴 Alive")
                                + (amHost && p == PlayerControl.LocalPlayer ? " (You – Host)" : "");

                // Resolver Cor
                data.DisplayColor = ResolveColor(p, data.IsDead);
                if (!data.IsDead) 
                {
                    DeadColorCache[p.PlayerId] = data.DisplayColor;
                }

                // Prioridade de Sort
                int priority;
                if (data.IsDead) priority = data.IsImpostor ? 3 : 4;
                else priority = data.IsImpostor ? 1 : 2;
                data.SortPriority = priority;

                ActiveList.Add(data);
            }

            // Sort da lista ativa
            ActiveList.Sort((a, b) =>
            {
                if (a.SortPriority != b.SortPriority) return a.SortPriority.CompareTo(b.SortPriority);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            // Limpeza eficiente do DeadColorCache
            KeysToRemove.Clear();
            foreach (var id in DeadColorCache.Keys)
            {
                if (!currentIds.Contains(id)) KeysToRemove.Add(id);
            }
            foreach (var id in KeysToRemove) DeadColorCache.Remove(id);

            // Limpeza do TriedFix a cada 2s
            if (currentTime - LastCleanupTime >= 2.0f)
            {
                TriedFix.Clear();
                LastCleanupTime = currentTime;
            }
        }

        private bool ShouldShow(CachedPlayerData data)
        {
            if (data.Disconnected) return false;
            return (data.IsImpostor && ShowImp) || (!data.IsImpostor && ShowCrew);
        }

        /*──────── PLAYER ENTRY (OTIMIZADO - ZERO ALOC) ─────────*/
        private void DrawPlayerEntry(CachedPlayerData data)
        {
            PlayerControl pl = data.Player; 
            bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            bool isLobby = ShipStatus.Instance == null;

            // Fix de role (com verificação de hashset antes de qualquer lógica pesada)
            if (amHost && !TriedFix.Contains(pl.PlayerId) && pl.Data.Role == null)
            {
                TriedFix.Add(pl.PlayerId); 
                ImpostorForcer.UpdateRoleLocally(pl, pl.Data.RoleType);
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            // --- Cor Box ---
            var oldGuiColor = GUI.color;
            GUI.color = data.DisplayColor;
            GUILayout.Box(EmptyContent, ColorBoxStyle, GUILayout.Width(20), GUILayout.Height(20));
            
            // --- Nomes com Tinting (GUI.contentColor) em vez de modificar Style ---
            // Isso evita clonar estados internos do GUIStyle
            GUI.color = oldGuiColor; // Restaura cor global
            var oldContentColor = GUI.contentColor;

            // Nome
            GUI.contentColor = Color.Lerp(Palette.White, data.DisplayColor, 0.7f);
            GUILayout.Label(data.Name, PlayerNameStyle);

            // Impostor Tag
            if (data.IsImpostor)
            {
                GUI.contentColor = Palette.ImpostorRed;
                GUILayout.Label(" (Impostor)", ImpostorNameStyle);
            }

            // Status
            GUI.contentColor = data.IsImpostor ? Palette.White : Color.Lerp(Palette.White, data.DisplayColor, 0.3f);
            GUILayout.Label(data.StatusText, StatusStyle);

            // Restaura cor de conteúdo original
            GUI.contentColor = oldContentColor;

            GUILayout.FlexibleSpace();

            // --- Botões ---
            if (!data.IsDead && !data.Disconnected &&
                GUILayout.Button("TP", GuiStyles.ButtonStyle, GUILayout.Width(42)))
                PlayerControl.LocalPlayer.NetTransform.SnapTo(pl.transform.position);

            if (amHost && ShipStatus.Instance != null && !data.IsDead &&
                GUILayout.Button("Kill", GuiStyles.ButtonStyle, GUILayout.Width(42)))
                PlayerControl.LocalPlayer.RpcMurderPlayer(pl, true);

            if (amHost && !isLobby && GUILayout.Button("Role ▼", GuiStyles.ButtonStyle, GUILayout.Width(95)))
            {
                OpenDrop = OpenDrop == pl.PlayerId ? (byte?)null : pl.PlayerId;
                if (OpenDrop != null) OpenPreAssign = null;
                DropScroll = Vector2.zero;
            }

            GUILayout.EndHorizontal();

            // --- Pre-Assign e Dropdowns ---
            if (amHost && isLobby)
            {
                GUILayout.BeginHorizontal();
                RoleTypes existingPre;
                bool hasPre = ImpostorForcer.PreGameRoleAssignments.TryGetValue(pl.PlayerId, out existingPre);
                if (hasPre)
                {
                    // Tinting para o label "Pre:"
                    var prevContent = GUI.contentColor;
                    GUI.contentColor = (existingPre == RoleTypes.Impostor || existingPre == RoleTypes.Shapeshifter)
                        ? Palette.ImpostorRed
                        : Palette.CrewmateBlue;
                    GUILayout.Label($"Pre: {existingPre}", PreAssignLabelStyle);
                    GUI.contentColor = prevContent;
                }
                if (GUILayout.Button("Set ▼", GuiStyles.ButtonStyle, GUILayout.Width(64)))
                {
                    OpenPreAssign = OpenPreAssign == pl.PlayerId ? (byte?)null : pl.PlayerId;
                    if (OpenPreAssign != null) OpenDrop = null;
                }
                GUILayout.EndHorizontal();
            }

            if (amHost && !isLobby && OpenDrop == pl.PlayerId)
                DrawRoleDropdown(pl);
            if (amHost && isLobby && OpenPreAssign == pl.PlayerId)
                DrawPreAssignDropdown(pl);

            GUILayout.EndVertical();
        }

        /*──────── ROLE DROPDOWN ───────*/
        private void DrawRoleDropdown(PlayerControl pl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Select Role:", GuiStyles.SubHeaderStyle);
            DropScroll = GUILayout.BeginScrollView(DropScroll, GUILayout.Height(200));

            var oldContent = GUI.contentColor;
            foreach (var role in AllRoles)
            {
                Color roleCol = RoleColor(role);
                GUI.contentColor = roleCol; // Tinting eficiente

                if (GUILayout.Button(role.ToString(), RoleButtonStyle, GUILayout.Width(127), GUILayout.Height(28)))
                {
                    pl.RpcSetRole(role, true);
                    if (role is RoleTypes.Impostor or RoleTypes.Shapeshifter)
                        pl.Data.RpcSetTasks(Array.Empty<byte>());
                    OpenDrop = null;
                }
            }
            GUI.contentColor = oldContent;

            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GuiStyles.ButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                OpenDrop = null;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /*──────── PRE-ASSIGN DROPDOWN ───────*/
        private void DrawPreAssignDropdown(PlayerControl pl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Pre-assign Role (Lobby):", GuiStyles.SubHeaderStyle);
            var roles = ImpostorForcer.GetSupportedRoles();

            int columns = 3;
            int i = 0;
            GUILayout.BeginHorizontal();
            
            var oldContent = GUI.contentColor;
            foreach (var role in roles)
            {
                Color roleCol = (role == RoleTypes.Impostor || role == RoleTypes.Shapeshifter) ? Palette.ImpostorRed : Palette.CrewmateBlue;
                GUI.contentColor = roleCol;

                if (GUILayout.Button(role.ToString(), PreAssignButtonStyle, GUILayout.Width(117)))
                {
                    ImpostorForcer.SetPreGameRoleForPlayer(pl, role);
                    OpenPreAssign = null;
                }

                i++;
                if (i % columns == 0)
                {
                    GUILayout.EndHorizontal();
                    if (i < roles.Length) GUILayout.BeginHorizontal();
                }
            }
            GUI.contentColor = oldContent;

            if (i % columns != 0) GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GuiStyles.ButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                OpenPreAssign = null;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        /*──────── COLOR HELPERS ───────*/
        private Color ResolveColor(PlayerControl p, bool isDead)
        {
            if (p == null || p.Data == null) return Palette.DisabledGrey;
            
            Color col = Palette.CrewmateBlue;

            if (isDead && DeadColorCache.TryGetValue(p.PlayerId, out var cachedCol))
            {
                col = cachedCol; 
            }
            else
            {
                int id = p.Data.DefaultOutfit?.ColorId ?? -1;
                if (id >= 0 && id < Palette.PlayerColors.Length)
                {
                    col = Palette.PlayerColors[id];
                }
                else
                {
                    var sr = p.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) col = sr.color;
                }
            }

            if (isDead) col = Color.Lerp(col, Color.black, 0.4f);
            return col;
        }

        private static Color RoleColor(RoleTypes r) => r switch
        {
            RoleTypes.Impostor or RoleTypes.Shapeshifter => Palette.ImpostorRed,
            RoleTypes.Crewmate or RoleTypes.Engineer => Palette.CrewmateBlue,
            RoleTypes.Scientist => Palette.LogSuccessColor,
            RoleTypes.GuardianAngel => Palette.CosmicubeQuality_Hat,
            _ => Palette.White
        };
    }
}