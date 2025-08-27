

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
        /*──────────────────── UTIL ───────────────────*/
        private static RectOffset Off(int l, int r, int t, int b)
        {
            var o = new RectOffset();
            o.left = l; o.right = r; o.top = t; o.bottom = b;
            return o;
        }

        /*──────────────── STATIC DATA ────────────────*/
        private static readonly Dictionary<Color, Texture2D> texCache = new();
        private static readonly List<RoleTypes> allRoles = new();

        static PlayerPickMenu()
        {
            foreach (RoleTypes r in Enum.GetValues(typeof(RoleTypes)))
                if (r is not (RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost))
                    allRoles.Add(r);

            allRoles.Sort((a, b) =>
                string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase));
        }

       
        private GUIStyle colorBoxStyle;
        private Vector2 dropScroll;
        private bool showImp = true, showCrew = true, showFilters = false;

        private byte? openDrop = null;
        private byte? openPreAssign = null;
        private readonly List<PlayerControl> cache = new();
        private readonly HashSet<byte> triedFix = new();           // anti-spam
        private readonly Dictionary<byte, Color> colorCache = new(); // cor por player
        private int lastCount = 0;
        private uint frameCounter = 0; // Para atualizações periódicas

       
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
            if (colorBoxStyle != null) return;

            colorBoxStyle = new GUIStyle(GUI.skin.box)
            {
                stretchWidth = false,
                stretchHeight = false,
                fixedWidth = 20,
                fixedHeight = 20,
                margin = Off(4, 4, 4, 4),
                padding = Off(0, 0, 0, 0)
            };
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Player Selection", GuiStyles.HeaderStyle);
            GUILayout.FlexibleSpace();
            var filtLabel = showFilters ? "Filters ▲" : "Filters ▼";
            showFilters = GUILayout.Toggle(showFilters, filtLabel,
                                           GuiStyles.ButtonStyle, GUILayout.Width(88));
            GUILayout.EndHorizontal();
        }

        private void DrawFilters()
        {
            if (!showFilters) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Role Filters:", GuiStyles.SubHeaderStyle);
            GUILayout.BeginHorizontal();
            showImp = GUILayout.Toggle(showImp, "Show Impostors", GuiStyles.ToggleStyle);
            showCrew = GUILayout.Toggle(showCrew, "Show Crewmates", GuiStyles.ToggleStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawPlayerList()
        {
            // Render list without an internal scroll to avoid double scrollbars.
            RefreshCache();
            bool anyShown = false;
            foreach (var p in cache)
                if (ShouldShow(p))
                {
                    DrawPlayerEntry(p);
                    anyShown = true;
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
            frameCounter++; // Contador para atualizações periódicas

            int cur = PlayerControl.AllPlayerControls.Count;

            // Sempre recarregar e ordenar se contagem mudou ou a cada 10 frames (para capturar mudanças de role/cor)
            bool isInGame = ShipStatus.Instance != null;
            if (cur != lastCount || cache.Count == 0 || frameCounter % 10 == 0)
            {
                cache.Clear();
                foreach (var p in PlayerControl.AllPlayerControls)
                    if (p != null && p.Data != null && !p.Data.Disconnected)
                        cache.Add(p);

                /* ── Ordenação: Impostores vivos > Crew vivos > Imp mortos > Crew mortos, depois alfabético ── */
                cache.Sort((a, b) =>
                {
                    bool aImp = a.Data.Role?.IsImpostor == true;
                    bool bImp = b.Data.Role?.IsImpostor == true;
                    bool aDead = a.Data.IsDead;
                    bool bDead = b.Data.IsDead;

                    // Calcular prioridade: menores números = topo da lista
                    int aPriority = aDead ? (aImp ? 3 : 4) : (aImp ? 1 : 2);
                    int bPriority = bDead ? (bImp ? 3 : 4) : (bImp ? 1 : 2);

                    if (aPriority != bPriority) return aPriority.CompareTo(bPriority);
                    return string.Compare(a.Data.PlayerName, b.Data.PlayerName,
                                          StringComparison.OrdinalIgnoreCase);
                });

                /* Atualiza colorCache; remove órfãos e força atualização se cor mudou */
                var currentIds = new HashSet<byte>();
                foreach (var p in cache)
                {
                    currentIds.Add(p.PlayerId);
                    // Força resolução de cor, com persistência in-game e para mortos
                    colorCache[p.PlayerId] = ResolveColor(p, isInGame);
                }
                foreach (var id in new List<byte>(colorCache.Keys))
                    if (!currentIds.Contains(id))
                        colorCache.Remove(id);

                lastCount = cur;

                // Limpa triedFix periodicamente para permitir re-tentativas se necessário
                if (frameCounter % 100 == 0) triedFix.Clear();
            }
            else
            {
                // Remover desconectados sem recarregar tudo
                for (int i = cache.Count - 1; i >= 0; i--)
                    if (cache[i].Data.Disconnected)
                    {
                        colorCache.Remove(cache[i].PlayerId);
                        cache.RemoveAt(i);
                    }
            }
        }

        private bool ShouldShow(PlayerControl p)
        {
            bool imp = p.Data.Role?.IsImpostor == true;
            return !p.Data.Disconnected && ((imp && showImp) || (!imp && showCrew));
        }

        /*──────── PLAYER ENTRY ─────────*/
        private void DrawPlayerEntry(PlayerControl pl)
        {
            bool amHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            bool isLobby = ShipStatus.Instance == null;

            /* Anti-spam: corrige role local uma única vez por ciclo */
            if (amHost && pl.Data.Role == null && !triedFix.Contains(pl.PlayerId))
            {
                if (pl.GetComponents<RoleBehaviour>().Length > 0)
                    ImpostorForcer.UpdateRoleLocally(pl, pl.Data.RoleType);
                triedFix.Add(pl.PlayerId);
            }

            int done = 0;
            int totalTasks = 0;
            var tasks = pl.Data.Tasks;
            if (tasks != null)
            {
                totalTasks = tasks.Count;
                foreach (var t in tasks)
                    if (t.Complete) done++;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            Color col = colorCache[pl.PlayerId];
            var tex = GetColorTexture(col, pl.Data.IsDead);
            var cellStyle = new GUIStyle(colorBoxStyle);
            cellStyle.normal.background = tex;
            cellStyle.hover.background = tex;
            cellStyle.active.background = tex;
            cellStyle.onNormal.background = tex;
            cellStyle.onHover.background = tex;
            cellStyle.onActive.background = tex;
            GUILayout.Box(GUIContent.none, cellStyle,
                          GUILayout.Width(20), GUILayout.Height(20));

            bool imp = pl.Data.Role?.IsImpostor == true;
            bool dead = pl.Data.IsDead;

            // Estilos para partes do texto
            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Normal,
                padding = Off(4, 0, 4, 0) // Sem padding direito para colar no próximo
            };
            nameStyle.normal.textColor = Color.Lerp(Palette.White, col, 0.7f); // Cor do jogador, levemente clara

            var impStyle = new GUIStyle(nameStyle)
            {
                fontStyle = FontStyle.Bold,
                padding = Off(0, 0, 4, 0)
            };
            impStyle.normal.textColor = Palette.ImpostorRed; // Vermelho oficial para "(Impostor)"

            var restStyle = new GUIStyle(nameStyle)
            {
                padding = Off(0, 4, 4, 4)
            };
            restStyle.normal.textColor = imp ? Palette.White : Color.Lerp(Palette.White, col, 0.3f);

            // Desenhar texto dividido
            GUILayout.Label(pl.Data.PlayerName, nameStyle);

            if (imp)
                GUILayout.Label(" (Impostor)", impStyle);

            string restTxt = (imp ? "" : $" Tasks: {done}/{totalTasks}")
                           + (dead ? " ⚪ Dead" : " 🔴 Alive")
                           + (amHost && pl == PlayerControl.LocalPlayer ? " (You – Host)" : "");
            GUILayout.Label(restTxt, restStyle);

            GUILayout.FlexibleSpace();

            /*── Botões ─────────────────*/
            if (!dead && !pl.Data.Disconnected &&
                GUILayout.Button("TP", GuiStyles.ButtonStyle, GUILayout.Width(42)))
                PlayerControl.LocalPlayer.NetTransform.SnapTo(pl.transform.position);

            if (amHost && ShipStatus.Instance != null && !pl.Data.IsDead &&
                GUILayout.Button("Kill", GuiStyles.ButtonStyle, GUILayout.Width(42)))
                PlayerControl.LocalPlayer.RpcMurderPlayer(pl, true);

            if (amHost && !isLobby && GUILayout.Button("Role ▼", GuiStyles.ButtonStyle, GUILayout.Width(95)))
            {
                openDrop = openDrop == pl.PlayerId ? (byte?)null : pl.PlayerId;
                if (openDrop != null) openPreAssign = null;
                dropScroll = Vector2.zero;
            }

            GUILayout.EndHorizontal();

            // Lobby-only: Pré-atribuição de role em NOVA LINHA para manter layout estável
            if (amHost && isLobby)
            {
                GUILayout.BeginHorizontal();
                RoleTypes existingPre;
                bool hasPre = ImpostorForcer.PreGameRoleAssignments.TryGetValue(pl.PlayerId, out existingPre);
                if (hasPre)
                {
                    var preStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, padding = Off(4,4,2,2) };
                    preStyle.normal.textColor = (existingPre == RoleTypes.Impostor || existingPre == RoleTypes.Shapeshifter)
                        ? Palette.ImpostorRed
                        : Palette.CrewmateBlue;
                    GUILayout.Label($"Pre: {existingPre}", preStyle);
                }
                if (GUILayout.Button("Set ▼", GuiStyles.ButtonStyle, GUILayout.Width(64)))
                {
                    openPreAssign = openPreAssign == pl.PlayerId ? (byte?)null : pl.PlayerId;
                    if (openPreAssign != null) openDrop = null;
                }
                GUILayout.EndHorizontal();
            }

            if (amHost && !isLobby && openDrop == pl.PlayerId)
                DrawRoleDropdown(pl);

            if (amHost && isLobby && openPreAssign == pl.PlayerId)
                DrawPreAssignDropdown(pl);

            GUILayout.EndVertical();
        }

        /*──────── ROLE DROPDOWN ───────*/
        private void DrawRoleDropdown(PlayerControl pl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Select Role:", GuiStyles.SubHeaderStyle);

            dropScroll = GUILayout.BeginScrollView(dropScroll, GUILayout.Height(200));

            var bs = new GUIStyle(GuiStyles.ButtonStyle)
            {
                fontSize = 14,
                padding = Off(8, 8, 4, 4),
                margin = Off(4, 4, 2, 2),
                alignment = TextAnchor.MiddleCenter
            };

            foreach (var role in allRoles)
            {
                Color roleCol = RoleColor(role);
                bs.normal.textColor = roleCol;
                bs.hover.textColor = roleCol;
                bs.active.textColor = roleCol;
                bs.onNormal.textColor = roleCol;
                bs.onHover.textColor = roleCol;
                bs.onActive.textColor = roleCol;

                if (GUILayout.Button(role.ToString(), bs,
                                     GUILayout.Width(127), GUILayout.Height(28)))
                {
                    pl.RpcSetRole(role, true);
                    if (role is RoleTypes.Impostor or RoleTypes.Shapeshifter)
                        pl.Data.RpcSetTasks(Array.Empty<byte>());
                    openDrop = null;
                }
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GuiStyles.ButtonStyle,
                                 GUILayout.Width(60), GUILayout.Height(22)))
                openDrop = null;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        /*──────── PRE-ASSIGN DROPDOWN (LOBBY) ───────*/
        private void DrawPreAssignDropdown(PlayerControl pl)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Pre-assign Role (Lobby):", GuiStyles.SubHeaderStyle);

            var roles = ImpostorForcer.GetSupportedRoles();
            // Use Grid com quebras consistentes para evitar desalinhamento em Repaint
            int columns = 3;
            int i = 0;
            GUILayout.BeginHorizontal();
            foreach (var role in roles)
            {
                var btn = new GUIStyle(GuiStyles.ButtonStyle);
                btn.normal.textColor = (role == RoleTypes.Impostor || role == RoleTypes.Shapeshifter) ? Palette.ImpostorRed : Palette.CrewmateBlue;
                if (GUILayout.Button(role.ToString(), btn, GUILayout.Width(117)))
                {
                    ImpostorForcer.SetPreGameRoleForPlayer(pl, role);
                    openPreAssign = null;
                }
                i++;
                if (i % columns == 0)
                {
                    GUILayout.EndHorizontal();
                    if (i < roles.Length) GUILayout.BeginHorizontal();
                }
            }
            if (i % columns != 0) GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GuiStyles.ButtonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                openPreAssign = null;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        /*──────── COLOR HELPERS ───────*/
        private Color ResolveColor(PlayerControl p, bool isInGame)
        {
            if (p == null || p.Data == null) return Palette.DisabledGrey;

            // Se morto, persistir cor cacheada se existir
            if (p.Data.IsDead && colorCache.TryGetValue(p.PlayerId, out var cachedCol))
                return cachedCol;

            int id = p.Data.DefaultOutfit?.ColorId ?? -1;
            if (id >= 0 && id < Palette.PlayerColors.Length)
                return Palette.PlayerColors[id];

            // Fallback para SpriteRenderer se in-game e sem cache
            var sr = p.GetComponentInChildren<SpriteRenderer>();
            return sr != null ? sr.color : Palette.CrewmateBlue;
        }

        private Texture2D GetColorTexture(Color c, bool isDead)
        {
            // Se morto, escurece a cor em vez de reduzir alpha (evita desaparecer)
            if (isDead) c = Color.Lerp(c, Color.black, 0.4f);

            if (texCache.TryGetValue(c, out var t)) return t;

            t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            t.SetPixel(0, 0, c);
            t.Apply();
            texCache[c] = t;
            return t;
        }

        private static Color RoleColor(RoleTypes r) => r switch
        {
            RoleTypes.Impostor or RoleTypes.Shapeshifter => Palette.ImpostorRed,
            RoleTypes.Crewmate or RoleTypes.Engineer => Palette.CrewmateBlue,
            RoleTypes.Scientist => Palette.LogSuccessColor, // Verde oficial de sucesso
            RoleTypes.GuardianAngel => Palette.CosmicubeQuality_Hat, // Amarelo oficial
            _ => Palette.White
        };
    }
}