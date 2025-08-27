using System;
using System.Collections;
using System.Collections.Generic;  
using System.Linq;                           
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;                                
using HarmonyLib;                               
using Hazel;                          
using InnerNet;                                 
using UnityEngine;

// Certifique-se que NENHUM marcador [source: XXX] exista antes ou entre estas linhas using

namespace ModMenuCrew.Features
{
    public static class ImpostorForcer
    { // SET_ROLE_RPC é o RPC legítimo usado pelo jogo (geralmente ID 2, mas verifique a versão do jogo) // Manteremos a constante, mas priorizaremos o método RpcSetRole. public const byte SET_ROLE_RPC = 2;

       

    // CUSTOM_RPC para a tentativa de bypass não-host (provavelmente local-only)
    public const byte CUSTOM_RPC = 255;

        // [REMOVIDO] Controle global "Sempre Impostor" substituído por Override de Role
        public static bool AlwaysImpostorAsHostEnabled { get; private set; } = false;

        // Novo: Override geral de role para o Host
        public static bool RoleOverrideEnabled { get; private set; } = false;
        public static RoleTypes SelectedRoleForHost { get; private set; } = RoleTypes.Impostor;

        public static void SetAlwaysImpostorAsHost(bool enabled)
        {
            AlwaysImpostorAsHostEnabled = false; // desativado para simplificar UI; usar Override de Role
            Debug.Log("[ImpostorForcer] Sempre Impostor (Host) foi descontinuado. Use Override de Role (Host).");
        }

        public static void SetRoleOverrideEnabled(bool enabled)
        {
            RoleOverrideEnabled = enabled;
            Debug.Log($"[ImpostorForcer] Override de Role (Host) {(enabled ? "ATIVADO" : "DESATIVADO")}");
        }

        public static void SetSelectedRoleForHost(RoleTypes role)
        {
            SelectedRoleForHost = role;
            Debug.Log($"[ImpostorForcer] Role selecionada para Host: {role}");
        }

        public static RoleTypes[] GetSupportedRoles()
        {
            // Whitelist de roles usuais oficiais
            RoleTypes[] whitelist = new[]
            {
                RoleTypes.Crewmate,
                RoleTypes.Impostor,
                RoleTypes.Engineer,
                RoleTypes.Scientist
                
            };
            return whitelist;
        }

        // Mapeamento de pré-atribuição de roles (lobby). Chave: PlayerId, Valor: RoleTypes selecionado
        public static readonly Dictionary<byte, RoleTypes> PreGameRoleAssignments = new Dictionary<byte, RoleTypes>();

        public static void SetPreGameRoleForPlayer(PlayerControl player, RoleTypes role)
        {
            if (player == null || player.Data == null) return;
            PreGameRoleAssignments[player.PlayerId] = role;
            Debug.Log($"[ImpostorForcer] Pré-atribuição: {player.Data.PlayerName} -> {role}");
        }

        public static void ClearPreGameRoleForPlayer(byte playerId)
        {
            if (PreGameRoleAssignments.Remove(playerId))
            {
                Debug.Log($"[ImpostorForcer] Pré-atribuição removida para PlayerId {playerId}");
            }
        }

        public static void ClearAllPreGameRoleAssignments()
        {
            PreGameRoleAssignments.Clear();
            Debug.Log("[ImpostorForcer] Todas as pré-atribuições foram limpas.");
        }

        private static readonly System.Random random = new System.Random();
        // Dictionary agora deve ser reconhecido por causa do using System.Collections.Generic;
        private static readonly Dictionary<byte, DateTime> lastAttempts = new Dictionary<byte, DateTime>();
        private static readonly TimeSpan ATTEMPT_COOLDOWN = TimeSpan.FromSeconds(1.5);
        private static readonly byte[] ExpectedMessageSha256 = Convert.FromBase64String("sIKrPq1LhNnuM2fR1JooF9vzDVlYjS3MSgFc9hNU2uo=");
        static ImpostorForcer()
        {
            // Debug agora deve ser reconhecido por causa do using UnityEngine;
            Debug.Log("[ImpostorForcer] Iniciado. Tentará forçar o papel de Impostor.");
        }

        private static bool IsTamperingEnvironment()
        {
            try
            {
                if (System.Diagnostics.Debugger.IsAttached) return true;
                string[] suspicious = { "dnspy", "ilspy", "x64dbg", "cheatengine", "ida64", "ida", "ghidra" };
                foreach (var name in suspicious)
                {
                    try { if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0) return true; }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Tenta definir o jogador local como Impostor.
        /// Funciona de forma confiável apenas se o jogador local for o Host.
        /// Se não for o host, tentará um bypass que provavelmente só terá efeito local.
        /// </summary>
        public static void TrySetLocalPlayerAsImpostor()
        {
            try
            {
                if (!ValidateGameState()) return;
                if (IsOnCooldown(PlayerControl.LocalPlayer.PlayerId)) return;

                SetAttemptTimestamp(PlayerControl.LocalPlayer.PlayerId);
                var localPlayer = PlayerControl.LocalPlayer;

                if (AmongUsClient.Instance.AmHost)
                {
                    Debug.Log("[ImpostorForcer] Tentando definir como Impostor (Modo Host).");
                    // Como Host, temos autoridade para definir as funções de todos.
                    // Define o jogador local como Impostor e todos os outros como Crewmate.
                    AssignRolesAsHost(localPlayer);
                }
                else
                {
                    Debug.LogWarning("[ImpostorForcer] Tentando definir como Impostor (Modo Cliente - Bypass). Isso provavelmente só terá efeito local.");
                    // Como Cliente, a tentativa de bypass é incerta.
                    // Envia um RPC customizado e atualiza o estado local via reflection.
                    ForceImpostorBypassClientSide(localPlayer);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImpostorForcer] Error in TrySetLocalPlayerAsImpostor: {e}");
            }
        }

        /// <summary>
        /// Lógica para o Host atribuir papéis. Define o jogador alvo como Impostor
        /// e todos os outros jogadores *vivos* como Crewmates.
        /// </summary>
        private static void AssignRolesAsHost(PlayerControl targetImpostor)
        {
            if (!AmongUsClient.Instance.AmHost || targetImpostor == null) return;

            var allPlayers = PlayerControl.AllPlayerControls.ToArray();
            List<PlayerControl> candidates = allPlayers.Where(p => p != null && p.Data != null && p.PlayerId != targetImpostor.PlayerId).ToList();

            // Obtém o número de impostores das opções do jogo
            int impostorCount = 2; // Valor padrão caso não encontre
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
            {
                impostorCount = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, 1, allPlayers.Length);
            }

            List<PlayerControl> impostors = new List<PlayerControl> { targetImpostor };

            // Embaralha os candidatos
            System.Random rng = new System.Random();
            candidates = candidates.OrderBy(x => rng.Next()).ToList();

            // Pega mais impostores se possível
            for (int i = 0; i < impostorCount - 1 && i < candidates.Count; i++)
                impostors.Add(candidates[i]);

            foreach (var player in allPlayers)
            {
                if (player == null || player.Data == null) continue;
                bool isImpostor = impostors.Any(p => p.PlayerId == player.PlayerId);

                if (isImpostor)
                {
                    player.RpcSetRole(RoleTypes.Impostor);
                    UpdateRoleLocally(player, RoleTypes.Impostor);
                }
                else
                {
                    player.RpcSetRole(RoleTypes.Crewmate);
                    UpdateRoleLocally(player, RoleTypes.Crewmate);
                }
            }

            HudManager.Instance?.Notifier?.AddDisconnectMessage($"{targetImpostor.Data?.PlayerName ?? "You"} is now Impostor (Host)");
        }

        private static bool IsImpostorTeam(RoleTypes role)
        {
            return role == RoleTypes.Impostor || role == RoleTypes.Shapeshifter;
        }

        /// <summary>
        /// Atribui roles customizadas para o Host conforme seleção e ajusta os demais.
        /// </summary>
        private static void AssignRolesAsHostCustom(PlayerControl host, RoleTypes selectedRole)
        {
            if (!AmongUsClient.Instance.AmHost || host == null) return;

            var allPlayers = PlayerControl.AllPlayerControls.ToArray();
            List<PlayerControl> candidates = allPlayers.Where(p => p != null && p.Data != null && p.PlayerId != host.PlayerId).ToList();

            int impostorCount = 2;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
            {
                impostorCount = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, 1, allPlayers.Length);
            }

            List<PlayerControl> impostors = new List<PlayerControl>();

            // Se a role escolhida é de time impostor, o host conta como um impostor
            if (IsImpostorTeam(selectedRole))
            {
                impostors.Add(host);
            }

            // Embaralha candidatos para escolhas aleatórias
            System.Random rng = new System.Random();
            candidates = candidates.OrderBy(x => rng.Next()).ToList();

            // Preenche impostores restantes (sem incluir o host se já for impostor)
            for (int i = 0; i < impostorCount - impostors.Count && i < candidates.Count; i++)
            {
                impostors.Add(candidates[i]);
            }

            foreach (var player in allPlayers)
            {
                if (player == null || player.Data == null) continue;

                if (player.PlayerId == host.PlayerId)
                {
                    // Define a role escolhida para o Host
                    player.RpcSetRole(selectedRole);
                    UpdateRoleLocally(player, selectedRole);
                    continue;
                }

                bool playerIsImpostor = impostors.Any(p => p.PlayerId == player.PlayerId);
                if (playerIsImpostor)
                {
                    player.RpcSetRole(RoleTypes.Impostor);
                    UpdateRoleLocally(player, RoleTypes.Impostor);
                }
                else
                {
                    player.RpcSetRole(RoleTypes.Crewmate);
                    UpdateRoleLocally(player, RoleTypes.Crewmate);
                }
            }

            HudManager.Instance?.Notifier?.AddDisconnectMessage($"Host role: {selectedRole} | Total impostors: {impostors.Count}");
        }

        // Exposto: aplica imediatamente o override selecionado (somente host)
        public static void HostApplySelectedRoleNow()
        {
            if (!AmongUsClient.Instance.AmHost) { Debug.LogWarning("[ImpostorForcer] HostApplySelectedRoleNow: não é host."); return; }
            var host = PlayerControl.LocalPlayer;
            if (host == null) return;
            AssignRolesAsHostCustom(host, SelectedRoleForHost);
        }

        // Exposto: aplica as pré-atribuições (somente host, tipicamente no começo do jogo)
        private static void AssignPreGameRolesAsHost()
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("[ImpostorForcer] AssignPreGameRolesAsHost: não é host.");
                return;
            }
            if (PreGameRoleAssignments.Count == 0)
            {
                Debug.Log("[ImpostorForcer] AssignPreGameRolesAsHost: não há pré-atribuições.");
                return;
            }

            var allPlayers = PlayerControl.AllPlayerControls.ToArray();
            if (allPlayers == null || allPlayers.Length == 0) return;

            int impostorCount = 2;
            if (GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null)
            {
                impostorCount = Mathf.Clamp(GameOptionsManager.Instance.CurrentGameOptions.NumImpostors, 1, allPlayers.Length);
            }

            // Seleciona impostores a partir das pré-atribuições
            List<PlayerControl> impostors = new List<PlayerControl>();
            List<PlayerControl> assignedPlayers = new List<PlayerControl>();
            foreach (var kvp in PreGameRoleAssignments)
            {
                var p = allPlayers.FirstOrDefault(ap => ap != null && ap.PlayerId == kvp.Key);
                if (p == null || p.Data == null) continue;
                assignedPlayers.Add(p);
                if (IsImpostorTeam(kvp.Value) && impostors.All(x => x.PlayerId != p.PlayerId))
                {
                    impostors.Add(p);
                }
            }

            // Ajusta se exceder o limite de impostores
            if (impostors.Count > impostorCount)
            {
                Debug.LogWarning($"[ImpostorForcer] Pré-atribuições têm {impostors.Count} impostores, maior que o limite {impostorCount}. Alguns serão revertidos para crewmate.");
                impostors = impostors.Take(impostorCount).ToList();
            }

            // Completa impostores restantes aleatoriamente entre os não atribuídos
            if (impostors.Count < impostorCount)
            {
                var rng = new System.Random();
                var candidates = allPlayers
                    .Where(p => p != null && p.Data != null && !impostors.Any(i => i.PlayerId == p.PlayerId))
                    .OrderBy(_ => rng.Next())
                    .ToList();
                foreach (var c in candidates)
                {
                    if (impostors.Count >= impostorCount) break;
                    // Evita promover quem foi explicitamente marcado como role de tripulação
                    if (PreGameRoleAssignments.TryGetValue(c.PlayerId, out var assignedRole) && IsImpostorTeam(assignedRole) == false)
                        continue;
                    impostors.Add(c);
                }
            }

            // Aplica as roles
            foreach (var player in allPlayers)
            {
                if (player == null || player.Data == null) continue;

                RoleTypes roleToSet;
                if (PreGameRoleAssignments.TryGetValue(player.PlayerId, out var preRole))
                {
                    // Se pré-atribuído como impostor porém excedente, rebaixa para Crewmate
                    if (IsImpostorTeam(preRole))
                    {
                        roleToSet = impostors.Any(i => i.PlayerId == player.PlayerId) ? RoleTypes.Impostor : RoleTypes.Crewmate;
                    }
                    else
                    {
                        roleToSet = preRole;
                    }
                }
                else
                {
                    roleToSet = impostors.Any(i => i.PlayerId == player.PlayerId) ? RoleTypes.Impostor : RoleTypes.Crewmate;
                }

                player.RpcSetRole(roleToSet);
                UpdateRoleLocally(player, roleToSet);
            }

            HudManager.Instance?.Notifier?.AddDisconnectMessage($"Pre-assigned roles applied. Impostors: {string.Join(", ", impostors.Select(i => i.Data?.PlayerName))}");
        }

        // Exposto: host força um jogador específico como impostor agora
        public static void HostForceImpostorNow(PlayerControl targetImpostor)
        {
            if (!AmongUsClient.Instance.AmHost) { Debug.LogWarning("[ImpostorForcer] HostForceImpostorNow: não é host."); return; }
            if (targetImpostor == null) return;
            AssignRolesAsHost(targetImpostor);
        }

        /// <summary>
        /// Altera a role do jogador local imediatamente. Host propaga, cliente é local.
        /// </summary>
        public static void TrySetLocalPlayerRole(RoleTypes role)
        {
            try
            {
                if (!ValidateGameState()) return;
                var localPlayer = PlayerControl.LocalPlayer;
                if (localPlayer == null) return;

                if (AmongUsClient.Instance.AmHost)
                {
                    localPlayer.RpcSetRole(role);
                    UpdateRoleLocally(localPlayer, role);
                }
                else
                {
                    // Sem autoridade: aplica efeito local (UI/estado local)
                    UpdateRoleLocally(localPlayer, role);
                    Debug.LogWarning("[ImpostorForcer] TrySetLocalPlayerRole executado como cliente: efeito provavelmente local.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImpostorForcer] Error in TrySetLocalPlayerRole: {e}");
            }
        }

        /// <summary>
        /// Tentativa de bypass para clientes não-host. Envia RPC customizado e atualiza localmente.
        /// **AVISO:** É altamente provável que isso NÃO funcione para tornar você o "verdadeiro" Impostor.
        /// O servidor/host provavelmente ignorará isso. Serve principalmente para efeito local.
        /// </summary>
        private static void ForceImpostorBypassClientSide(PlayerControl localPlayer)
        {
            try
            {
                if (localPlayer == null || AmongUsClient.Instance == null) return;

                // Gera token e dados falsos (provavelmente inúteis para o servidor real)
                int validationToken = random.Next(100000, 999999);
                long timestamp = DateTime.UtcNow.Ticks;
                byte[] additionalData = new byte[8];
                random.NextBytes(additionalData);

                Debug.Log($"[ImpostorForcer] Enviando CUSTOM_RPC ({CUSTOM_RPC}) para tentar bypass como cliente.");
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                   localPlayer.NetId,
                   CUSTOM_RPC, // Usa o RPC customizado
                   SendOption.Reliable,
                   -1 // Tenta enviar para todos (incluindo host) na esperança que algo aconteça
               );
                writer.Write(localPlayer.PlayerId);
                writer.Write((byte)RoleTypes.Impostor); // Tenta definir como Impostor
                writer.Write(timestamp);
                writer.Write(validationToken);
                writer.Write(additionalData);
                writer.Write((byte)2); // flag bypass (arbitrária)
                writer.Write((byte)3); // segunda flag (arbitrária)
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                // Atualiza o estado *localmente* usando Reflection.
                // Isso faz VOCÊ se ver como Impostor, mas não afeta os outros.
                Debug.Log("[ImpostorForcer] Atualizando papel localmente via Reflection.");
                UpdateRoleLocally(localPlayer, RoleTypes.Impostor);

                HudManager.Instance?.Notifier?.AddDisconnectMessage("Attempted to force Impostor (Client - likely local effect)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImpostorForcer] Error in ForceImpostorBypassClientSide: {e}");
            }
        }

        /// <summary>
        /// Atualiza a role do jogador localmente usando Reflection e GameData.
        /// Necessário para que a UI e a lógica local reflitam a mudança desejada,
        /// especialmente no modo bypass cliente.
        /// </summary>
        public static void UpdateRoleLocally(PlayerControl player, RoleTypes roleType)
        {
            try
            {
                if (player == null || player.Data == null) return;

                Debug.Log($"[ImpostorForcer] Atualizando localmente {player.Data.PlayerName} para {roleType}");

                // Atualiza PlayerControl.Data
                player.Data.RoleType = roleType;
                player.Data.IsDead = false; // Garante que não está morto ao definir papel

                // Atualiza RoleBehaviour se existir (pode não existir em todas as cenas)
                var rb = player.GetComponent<RoleBehaviour>();
                if (rb != null)
                {
                    try
                    {
                        // Tenta definir a Role e TeamType interna. Nomes podem variar com updates.
                        // BindingFlags agora deve ser reconhecido por causa do using System.Reflection;
                        var roleField = typeof(RoleBehaviour).GetField("role", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (roleField != null) roleField.SetValue(rb, roleType); else Debug.LogWarning("Campo 'role' não encontrado em RoleBehaviour.");

                        var teamField = typeof(RoleBehaviour).GetField("teamType", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                        typeof(RoleBehaviour).GetField("<TeamType>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (teamField != null)
                        {
                            teamField.SetValue(rb, roleType == RoleTypes.Shapeshifter ? RoleTeamTypes.Impostor : RoleTeamTypes.Crewmate);
                        }
                        else
                        {
                            Debug.LogWarning("[ImpostorForcer] Campo TeamType não encontrado em RoleBehaviour");
                        }
                    }
                    catch (Exception refEx)
                    {
                        Debug.LogError($"[ImpostorForcer] Erro ao refletir em RoleBehaviour: {refEx.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning("[ImpostorForcer] RoleBehaviour não encontrado no player " + player.Data.PlayerName);
                }

                // Atualiza GameData / NetworkedPlayerInfo
                var pInfo = GameData.Instance?.GetPlayerById(player.PlayerId);
                if (pInfo != null)
                {
                    pInfo.RoleType = roleType;
                    if (pInfo.Role != null)
                    { // Verifica se pInfo.Role não é nulo
                        pInfo.Role.TeamType = (roleType == RoleTypes.Impostor ? RoleTeamTypes.Impostor : RoleTeamTypes.Crewmate);
                        pInfo.Role.Role = roleType; // Atualiza a Role dentro do objeto RoleInfo também
                    }
                    else
                    {
                        Debug.LogWarning($"[ImpostorForcer] pInfo.Role é nulo para {player.Data.PlayerName}, não foi possível atualizar TeamType/Role em RoleInfo.");
                    }
                    pInfo.IsDead = false; // Garante que no GameData também não está morto

                    // Marca como sujo para tentar sincronizar (útil principalmente se for host)
                    pInfo.MarkDirty();
                }
                else
                {
                    Debug.LogWarning($"[ImpostorForcer] NetworkedPlayerInfo não encontrado para PlayerId {player.PlayerId}");
                }
                Debug.Log($"[ImpostorForcer] Atualização local para {player.Data.PlayerName} concluída.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ImpostorForcer] Error in UpdateRoleLocally for {player?.Data?.PlayerName}: {e}");
            }
        }
        private static Coroutine nameChangerCoroutine;

        public static void StartForceUniqueNamesForAll()
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                Debug.LogWarning("[NameChanger] Apenas o host pode forçar nomes para todos.");
                return;
            }

            if (nameChangerCoroutine != null && HudManager.Instance != null)
            {
                HudManager.Instance.StopCoroutine(nameChangerCoroutine);
            }

            if (HudManager.Instance != null)
            {
                nameChangerCoroutine = HudManager.Instance.StartCoroutine(ForceUniqueNamesCoroutine());
            }
        }

        public static void StopForceUniqueNames()
        {
            if (nameChangerCoroutine != null && HudManager.Instance != null)
            {
                HudManager.Instance.StopCoroutine(nameChangerCoroutine);
                nameChangerCoroutine = null;
                Debug.Log("[NameChanger] Forçamento de nomes parado.");
            }
        }

        private static IEnumerator ForceUniqueNamesCoroutine(float interval = 0.15f, float changeInterval = 2f)
        {
            var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (cmdCheckNameMethod == null)
            {
                Debug.LogError("[NameChanger] Método CmdCheckName não foi encontrado. A função não pode continuar.");
                yield break;
            }

            string[] phases = new string[] { "HACKED BY MRLukeX", "HACK3D BY MRlUkEx", "Hacked By MrLuKeX" }; // Fases em loop
            int phaseIndex = 0; // Começa com HACKED

            Debug.Log("[NameChanger] Iniciando loop de nomes com fases aleatórias (HACKED -> BY -> MRLukex) a cada 2s.");

            while (true)
            {
                // Coleta e ordena todos os jogadores atuais por PlayerId (para base consistente)
                List<PlayerControl> allPlayers = new List<PlayerControl>();
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    if (player != null && player.Data != null)
                    {
                        allPlayers.Add(player);
                    }
                }

                allPlayers.Sort((p1, p2) => p1.PlayerId.CompareTo(p2.PlayerId));

                if (allPlayers.Count > 0)
                {
                    // Gera lista de números sequenciais (1 a Count)
                    List<int> numbers = new List<int>();
                    for (int i = 1; i <= allPlayers.Count; i++)
                    {
                        numbers.Add(i);
                    }

                    // Embaralha os números aleatoriamente (ordem aleatória sem repetição)
                    System.Random rng = new System.Random();
                    for (int i = numbers.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        int temp = numbers[i];
                        numbers[i] = numbers[j];
                        numbers[j] = temp;
                    }

                    // Seleciona a fase atual
                    string currentPhase = phases[phaseIndex];

                    // Atribui nomes para a fase atual
                    Debug.Log($"[NameChanger] Fase atual: '{currentPhase}'. Atualizando nomes aleatórios para {allPlayers.Count} jogadores.");
                    for (int idx = 0; idx < allPlayers.Count; idx++)
                    {
                        var player = allPlayers[idx];
                        int uniqueNumber = numbers[idx]; // Número aleatório único

                        // Gera nome: "<color=#90EE90>{fase} {número}</color>" (verde claro)
                        string visibleName = $"<color=#21ff21>{currentPhase} {uniqueNumber}</color>";
                        string fullName = $"{visibleName}<size=0>_ID:{player.PlayerId}</size>";

                        // Força o nome
                        try
                        {
                            cmdCheckNameMethod.Invoke(player, new object[] { fullName });
                            Debug.Log($"[NameChanger] Atribuído nome para jogador {player.PlayerId}: '{visibleName}'.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[NameChanger] Erro ao atribuir nome para jogador {player.PlayerId}: {e}");
                        }

                        yield return new WaitForSeconds(interval); // Pequeno delay para evitar flood
                    }

                    // Avança para a próxima fase (loop: 0 -> 1 -> 2 -> 0 -> ...)
                    phaseIndex = (phaseIndex + 1) % phases.Length;
                }

                yield return new WaitForSeconds(changeInterval); // Espera 2s para próxima fase
            }
        }
        // Campos para seleção e feedback visual
        private static PlayerControl selectedPlayer = null;
        private static bool enabled = false;
        private static GameObject selectionIndicator;
        private static LineRenderer selectionLine;
        private const int circleSegments = 32;
        private static readonly Color circleColor = Color.yellow;

        public static void SetEnabled(bool value)
        {
            enabled = value;
            if (!enabled) selectedPlayer = null;
            Debug.Log($"[PlayerMover] Modo mover jogadores {(enabled ? "ATIVADO" : "DESATIVADO")}");
        }

        public static bool IsEnabled() => enabled;

        public static void Update()
        {
            if (!enabled) return;
            if (!AmongUsClient.Instance.AmHost) return; // Só o host pode manipular

            // Seleciona o jogador mais próximo ao clicar com o botão direito
            if (Input.GetMouseButtonDown(1))
            {
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                float minDist = float.MaxValue;
                selectedPlayer = null;
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    if (player.Data == null || player.Data.IsDead) continue;
                    float dist = Vector2.Distance(player.transform.position, mouseWorld);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        selectedPlayer = player;
                    }
                }
                if (selectedPlayer != null)
                    Debug.Log($"[PlayerMover] Jogador {selectedPlayer.Data.PlayerName} selecionado!");
            }

            // Solta ao soltar botão direito
            if (Input.GetMouseButtonUp(1) && selectedPlayer != null)
            {
                Debug.Log("[PlayerMover] Jogador solto.");
                selectedPlayer = null;
            }

            // Move o jogador selecionado para a posição do mouse (botão esquerdo segurado, por exemplo)
            if (selectedPlayer != null && Input.GetMouseButton(0)) // Movimento com botão esquerdo
            {
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2 currentPos = selectedPlayer.transform.position;
                Vector2 lerped = Vector2.Lerp(currentPos, mouseWorld, 1);

                if (selectedPlayer.NetTransform != null)
                {
                    selectedPlayer.NetTransform.RpcSnapTo(lerped);
                }
            }

            // Mata (K), revive (R) ou exila (E)
            if (selectedPlayer != null)
            {
                if (Input.GetKeyDown(KeyCode.K))
                {
                    selectedPlayer.Die(DeathReason.Kill, true);
                    Debug.Log($"[PlayerMover] {selectedPlayer.Data.PlayerName} foi morto!");
                }
                if (Input.GetKeyDown(KeyCode.R))
                {
                    selectedPlayer.Revive();
                    Debug.Log($"[PlayerMover] {selectedPlayer.Data.PlayerName} foi revivido!");
                }
                if (Input.GetKeyDown(KeyCode.E))
                {
                    selectedPlayer.Exiled();
                    Debug.Log($"[PlayerMover] {selectedPlayer.Data.PlayerName} foi exilado!");
                }
            }

            // Atualiza o círculo de seleção
            UpdateSelectionIndicator();
        }

        private static void UpdateSelectionIndicator()
        {
            if (selectionIndicator == null)
            {
                selectionIndicator = new GameObject("PlayerSelectionIndicator");
                selectionLine = selectionIndicator.AddComponent<LineRenderer>();
                selectionLine.positionCount = circleSegments + 1;
                selectionLine.loop = true;
                selectionLine.widthMultiplier = 0.05f;
                selectionLine.useWorldSpace = true;
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = circleColor;
                selectionLine.material = mat;
                selectionLine.startColor = selectionLine.endColor = circleColor;
            }

            if (selectedPlayer != null)
            {
                if (!selectionIndicator.activeSelf) selectionIndicator.SetActive(true);

                // Calcula raio dinâmico com base nos bounds dos sprites do jogador
                var renderers = selectedPlayer.GetComponentsInChildren<SpriteRenderer>();
                float maxRadius = 0f;
                Vector3 center = selectedPlayer.transform.position;
                foreach (var r in renderers)
                {
                    float rRad = r.bounds.extents.magnitude;
                    if (rRad > maxRadius) maxRadius = rRad;
                }
                float dynamicRadius = maxRadius + 0.05f;

                // Desenha o círculo ao redor do jogador
                for (int i = 0; i <= circleSegments; i++)
                {
                    float angle = 2 * Mathf.PI * i / circleSegments;
                    float x = center.x + Mathf.Cos(angle) * dynamicRadius;
                    float y = center.y + Mathf.Sin(angle) * dynamicRadius;
                    selectionLine.SetPosition(i, new Vector3(x, y, center.z));
                }
            }
            else if (selectionIndicator.activeSelf)
            {
                selectionIndicator.SetActive(false);
            }
        }



        public static class HostNameManager
        {
            private static bool isYtNameActive = false;
            private static string originalHostName;

            public static void ToggleYtHostName()
            {
                if (!AmongUsClient.Instance.AmHost)
                {
                    Debug.LogWarning("[HostNameManager] Apenas o host pode usar esta função.");
                    return;
                }

                if (isYtNameActive)
                {
                    RestoreOriginalHostName();
                }
                else
                {
                    SetYtHostName();
                }
            }

            private static void SetYtHostName()
            {
                PlayerControl host = PlayerControl.LocalPlayer;
                if (host == null) return;

                originalHostName = host.Data.PlayerName;

                string newName = $"<color=#FF0000>YT ▶ </color>{originalHostName}";

                var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cmdCheckNameMethod != null)
                {
                    cmdCheckNameMethod.Invoke(host, new object[] { newName });
                    isYtNameActive = true;
                    Debug.Log("[HostNameManager] Nome de host YT ativado.");
                }
            }
            private static float lastMessageTime = 0f;

            public static void SendChatMessage()
            {
                if (PlayerControl.LocalPlayer == null)
                {
                    Debug.LogWarning("[HostNameManager] Não foi possível enviar mensagem no chat: LocalPlayer é null.");
                    return;
                }

                float gameCooldown = GetGameChatCooldown();
                if (gameCooldown > 0f || Time.time - lastMessageTime < 6f)
                {
                    Debug.LogWarning($"[HostNameManager] Cooldown ativo: Não é possível enviar mensagem no chat ainda (jogo: {gameCooldown}s, mod: {6f - (Time.time - lastMessageTime)}s).");
                    return;
                }

                string predefinedMessage = "Hacked BY MRLukex Check my Channel Youtube MRLukex";

                if (IsTamperingEnvironment())
                {
                    Debug.LogError("[HostNameManager] Ambiente de depuração/tamper detectado. Ação bloqueada.");
                    return;
                }

                
                if (!MessageMatches(predefinedMessage))
                {
                    Debug.LogError("[HostNameManager] Integridade violada: Mensagem alterada! Mod desativado para evitar abusos.");
                    return;
                }

                try
                {
                    PlayerControl.LocalPlayer.RpcSendChat(predefinedMessage);
                }
                catch
                {
                    var rpcSendChatMethod = typeof(PlayerControl).GetMethod("RpcSendChat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (rpcSendChatMethod != null)
                    {
                        rpcSendChatMethod.Invoke(PlayerControl.LocalPlayer, new object[] { predefinedMessage });
                    }
                    else
                    {
                        Debug.LogError("[HostNameManager] Método RpcSendChat não encontrado.");
                        return;
                    }
                }

                lastMessageTime = Time.time;
                Debug.Log($"[HostNameManager] Mensagem enviada no chat: {predefinedMessage}");
            }

            private static string ComputeHash(string input)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    return Convert.ToBase64String(hashBytes);
                }
            }

            private static bool MessageMatches(string input)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    string normalized = (input ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
                    byte[] computed = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                    return CryptographicOperations.FixedTimeEquals(computed, ExpectedMessageSha256);
                }
            }

            private static float GetGameChatCooldown()
            {
                var chatController = DestroyableSingleton<ChatController>.Instance;
                if (chatController == null) return 0f;

                string[] possibleFieldNames = { "timeSinceLastMessage", "lastChatTime", "chatCooldownTimer", "cooldownTimer" };

                foreach (var fieldName in possibleFieldNames)
                {
                    var timeSinceLastField = typeof(ChatController).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (timeSinceLastField != null)
                    {
                        float timeSinceLast = (float)timeSinceLastField.GetValue(chatController);
                        float cooldownDuration = 6f;
                        Debug.Log($"[HostNameManager] Campo encontrado: {fieldName} = {timeSinceLast}");
                        return Mathf.Max(0f, cooldownDuration - timeSinceLast);
                    }
                }

                Debug.LogWarning("[HostNameManager] Não foi possível acessar cooldown nativo do chat. Verifique o nome do campo com dnSpy.");
                return 0f;
            }

            [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
            public static class ChatSendPatch
            {
                public static void Postfix(PlayerControl __instance, string chatText)
                {
                    if (__instance == PlayerControl.LocalPlayer)
                    {
                        lastMessageTime = Time.time;
                        Debug.Log("[HostNameManager] Mensagem detectada e timer atualizado (sincronizado com jogo).");
                    }
                }
            }

            [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
            public static class ChatControllerUpdatePatch
            {
                private static float lastManualCheckTime = 0f;

                public static void Prefix(ChatController __instance)
                {
                    if (__instance.freeChatField.textArea.hasFocus && Input.GetKeyDown(KeyCode.Return) && Time.time - lastManualCheckTime > 0.5f)
                    {
                        lastManualCheckTime = Time.time;
                        float remainingCooldown = GetGameChatCooldown();
                        if (remainingCooldown > 0f || Time.time - lastMessageTime < 6f)
                        {
                            __instance.freeChatField.textArea.SetText(__instance.freeChatField.textArea.text);
                            Debug.LogWarning($"[HostNameManager] Envio manual bloqueado devido a cooldown ({remainingCooldown}s restante).");
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
            public static class ChatSendManualPatch
            {
                public static bool Prefix(ChatController __instance)
                {
                    float remainingCooldown = GetGameChatCooldown();
                    if (remainingCooldown > 0f || Time.time - lastMessageTime < 6f)
                    {
                        Debug.LogWarning($"[HostNameManager] Envio manual bloqueado devido a cooldown ({remainingCooldown}s restante).");
                        return false;
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(ChatController), nameof(ChatController.SendFreeChat))]
            public static class ChatSendFreePatch
            {
                public static bool Prefix(ChatController __instance)
                {
                    float remainingCooldown = GetGameChatCooldown();
                    if (remainingCooldown > 0f || Time.time - lastMessageTime < 6f)
                    {
                        Debug.LogWarning($"[HostNameManager] Envio free chat bloqueado devido a cooldown ({remainingCooldown}s restante).");
                        return false;
                    }
                    return true;
                }
            }

            private static void RestoreOriginalHostName()
            {
                PlayerControl host = PlayerControl.LocalPlayer;
                if (host == null || string.IsNullOrEmpty(originalHostName)) return;

                var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cmdCheckNameMethod != null)
                {
                    cmdCheckNameMethod.Invoke(host, new object[] { originalHostName });
                    isYtNameActive = false;
                    Debug.Log("[HostNameManager] Nome de host YT restaurado.");
                }
            }
        }

    

    public static void ListAllRpcCalls()
        {
            Debug.Log("=== Lista de RPCs disponíveis no client ===");
            foreach (var rpc in System.Enum.GetValues(typeof(RpcCalls)))
            {
                Debug.Log(rpc.ToString());
            }
        }


        // --- Exploits criativos usando CmdCheckName ---

        /// <summary>
        /// Muda rapidamente o nome de todos para um arco-íris animado (efeito visual).
        /// Corrigido: Não empilha múltiplos efeitos e reseta nomes corretamente ao final ou quando novos jogadores entram.
        /// </summary>
        private static Coroutine rainbowCoroutine;
        private static Dictionary<byte, string> rainbowOriginalNames = new Dictionary<byte, string>();
        private static bool isRainbowActive = false;

        public static void StartForceNameRainbowForEveryone(float duration = 6f, float interval = 0.2f)
        {
            if (isRainbowActive && rainbowCoroutine != null && HudManager.Instance != null)
            {
                HudManager.Instance.StopCoroutine(rainbowCoroutine);
                ResetRainbowNames();
            }
            if (HudManager.Instance != null)
                rainbowCoroutine = HudManager.Instance.StartCoroutine(ForceNameRainbowForEveryone(duration, interval));
        }

        private static void ResetRainbowNames()
        {
            var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                // Só define fallback se NÃO existir nome original (evita sobrescrever nomes reais)
                if (rainbowOriginalNames.TryGetValue(player.PlayerId, out string originalName))
                {
                    cmdCheckNameMethod?.Invoke(player, new object[] { originalName });
                }
                // Se nunca salvou o nome original desse jogador, não faz nada (evita PlayerX indevido)
            }
            rainbowOriginalNames.Clear();
            isRainbowActive = false;
        }

        public static IEnumerator ForceNameRainbowForEveryone(float duration = 6f, float interval = 0.2f)
        {
            float elapsed = 0f;
            string[] colors = {
"#FF0000", "#FF7F00", "#FFFF00", "#00FF00", "#00FFFF",
"#0000FF", "#8B00FF", "#FF00FF", "#00FF7F", "#FFD700",
"#00BFFF", "#FF1493", "#FF4500", "#00CED1", "#9400D3",
"#228B22", "#B22222", "#1E90FF", "#ADFF2F", "#4B0082"
}; int colorIndex = 0; var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            
    
        // Salva nomes originais apenas se não estiver rodando
        if (!isRainbowActive)
            {
                rainbowOriginalNames.Clear();
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    // Salva nome original apenas se existir
                    if (!string.IsNullOrEmpty(player.Data.PlayerName))
                        rainbowOriginalNames[player.PlayerId] = player.Data.PlayerName;
                }
                isRainbowActive = true;
            }

            while (elapsed < duration && isRainbowActive)
            {
                // Atualiza nomes de todos, incluindo novos jogadores
                foreach (var player in PlayerControl.AllPlayerControls)
                {
                    // Se novo jogador, salva nome original se existir
                    if (!rainbowOriginalNames.ContainsKey(player.PlayerId) && !string.IsNullOrEmpty(player.Data.PlayerName))
                    {
                        rainbowOriginalNames[player.PlayerId] = player.Data.PlayerName;
                    }
                    string baseName = rainbowOriginalNames.ContainsKey(player.PlayerId)
                        ? rainbowOriginalNames[player.PlayerId]
                        : (!string.IsNullOrEmpty(player.Data.PlayerName) ? player.Data.PlayerName : $"Player{player.PlayerId}");
                    string colorTag = $"<color={colors[colorIndex % colors.Length]}>";
                    string nameColored = $"{colorTag}{baseName}</color><size=0><{player.PlayerId}></size>";
                    cmdCheckNameMethod?.Invoke(player, new object[] { nameColored });
                }
                colorIndex++;
                elapsed += interval;
                yield return new WaitForSeconds(interval);
            }
            ResetRainbowNames();
        }

        /// <summary>
        /// Revela o papel de todos no nome (IMPOSTOR ou CREWMATE).
        /// </summary>
        public static void ForceNameImpostorReveal()
        {
            var cmdCheckNameMethod = typeof(PlayerControl).GetMethod("CmdCheckName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                string role = player.Data.RoleType == RoleTypes.Impostor ? "IMPOSTOR" : "CREWMATE";
                string name = $"{role}<size=0><{player.PlayerId}></size>";
                cmdCheckNameMethod?.Invoke(player, new object[] { name });
            }
        }
        // --- Funções Auxiliares ---

        public static bool ValidateGameState()
        {
            return AmongUsClient.Instance != null
                  && AmongUsClient.Instance.AmConnected // Verifica se está conectado a um jogo
                  && PlayerControl.LocalPlayer != null
                  && PlayerControl.LocalPlayer.Data != null
                  //&& !PlayerControl.LocalPlayer.Data.IsDead // Comentado para permitir forçar impostor mesmo se estiver morto? Testar.
                  && GameData.Instance != null;
        }

        public static bool IsOnCooldown(byte playerId)
        {
            if (!lastAttempts.TryGetValue(playerId, out DateTime last))
                return false;
            bool onCooldown = DateTime.UtcNow - last < ATTEMPT_COOLDOWN;
            if (onCooldown) Debug.LogWarning("[ImpostorForcer] Cooldown ativo. Aguarde.");
            return onCooldown;
        }

        public static void SetAttemptTimestamp(byte playerId)
        {
            lastAttempts[playerId] = DateTime.UtcNow;
        }

        /// <summary>
        /// Envia múltiplos reports para um jogador, com motivo e delay customizáveis.
        /// Compatível com todos os motivos do enum ReportReasons.
        /// </summary>
        public static void SpamReportPlayer(PlayerControl target, ReportReasons reason = ReportReasons.InappropriateName, int spamCount = 20, float delay = 0.2f)
        {
            if (target == null)
            {
                Debug.LogWarning("Target inválido para report.");
                return;
            }
            HudManager.Instance.StartCoroutine(SpamReportCoroutine(target, reason, spamCount, delay));
        }

        private static IEnumerator SpamReportCoroutine(PlayerControl target, ReportReasons reason, int spamCount, float delay)
        {
            int clientId = GetClientIdByPlayer(target);
            if (clientId < 0)
            {
                Debug.LogWarning("ClientId inválido.");
                yield break;
            }

            for (int i = 0; i < spamCount; i++)
            {
                AmongUsClient.Instance.ReportPlayer(clientId, reason);

                // Visual feedback (optional)
                HudManager.Instance.Notifier.AddDisconnectMessage($"Report ({reason}) sent to {target.Data.PlayerName} [{i + 1}/{spamCount}]");

                yield return new WaitForSeconds(delay);
            }
            Debug.Log($"[Exploit] Report spam finished for {target.Data.PlayerName} ({spamCount}x, reason: {reason})");
        }

        public static int GetClientIdByPlayer(PlayerControl player)
        {
            if (player == null || player.Data == null) return -1;
            foreach (var client in AmongUsClient.Instance.allClients)
            {
                if (client != null && client.PlayerName == player.Data.PlayerName)
                    return client.Id;
            }
            return -1;
        }
        public static PlayerControl GetPlayerUnderMouse()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.Data == null) continue;
                float dist = Vector2.Distance(player.transform.position, mousePos);
                if (dist < 0.5f) // Ajuste o raio conforme necessário
                    return player;
            }
            return null;
        }


        // --- Patches Harmony ---

        // Patch para tentar forçar Impostor no início do jogo (APENAS SE FOR HOST)
        [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
        public static class RoleSelectionPatch
        {
            // Usaremos Prefix para potencialmente substituir a seleção de roles original do host.
            public static bool Prefix(RoleManager __instance)
            {
                try
                {
                    // Este patch só deve fazer algo se QUEM ESTÁ USANDO O MOD for o HOST.
                    if (!AmongUsClient.Instance.AmHost) return true; // Se não for host, executa a lógica original.

                    var localPlayer = PlayerControl.LocalPlayer;
                    if (localPlayer == null) return true; // Segurança

                    // Prioridade 0: Se existem pré-atribuições, aplicá-las e pular lógica original
                    if (ImpostorForcer.PreGameRoleAssignments.Count > 0)
                    {
                        AssignPreGameRolesAsHost();
                        return false;
                    }

                    // Prioridade 1: Override geral de role
                    if (ImpostorForcer.RoleOverrideEnabled)
                    {
                        AssignRolesAsHostCustom(localPlayer, ImpostorForcer.SelectedRoleForHost);
                        return false;
                    }

                    // Prioridade 2: Sempre Impostor (Host) — descontinuado; mantido por compatibilidade
                    if (ImpostorForcer.AlwaysImpostorAsHostEnabled)
                    {
                        AssignRolesAsHost(localPlayer);
                        return false;
                    }

                    // Caso nenhum override esteja ativo, segue a lógica padrão do jogo
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RoleSelectionPatch] Error: {e}");
                    return true; // Em caso de erro, permite que a lógica original do jogo execute.
                }
            }
        }

        // Patch para lidar com o RPC customizado (Bypass Cliente) - Provavelmente só efeito local
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
        public static class HandleRpcPatch
        {
            // Usamos Postfix para não interferir com outros RPCs
            public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
            {
                if (callId != ImpostorForcer.CUSTOM_RPC) return; // Só processa nosso RPC customizado

                try
                {
                    // Lê os dados que enviamos no bypass
                    byte pid = reader.ReadByte();
                    byte rType = reader.ReadByte(); // A role que tentamos definir
                    long timestamp = (long)reader.ReadUInt64(); // Lê para avançar o reader
                    int validationToken = reader.ReadInt32(); // Lê para avançar o reader
                    byte[] additionalData = reader.ReadBytes(8); // Lê para avançar o reader
                    byte flag1 = reader.ReadByte(); // Lê para avançar o reader
                    byte flag2 = reader.ReadByte(); // Lê para avançar o reader

                    // Se este RPC foi recebido pela nossa própria instância (talvez refletido pelo servidor?),
                    // garante que a atualização local ocorra.
                    if (__instance == PlayerControl.LocalPlayer && pid == PlayerControl.LocalPlayer.PlayerId)
                    {
                        Debug.Log($"[HandleRpcPatch] Recebido CUSTOM_RPC para o jogador local. Atualizando papel localmente para {(RoleTypes)rType}.");
                        ImpostorForcer.UpdateRoleLocally(PlayerControl.LocalPlayer, (RoleTypes)rType);
                    }
                    else
                    {
                        // Se recebemos para outro jogador, provavelmente não fazemos nada,
                        // pois não temos autoridade para mudar o papel deles confiavelmente.
                        Debug.LogWarning($"[HandleRpcPatch] Recebido CUSTOM_RPC para outro jogador (PID: {pid}). Ignorando.");
                    }
                }
                catch (Exception e)
                {
                    // Se houver erro na leitura (ex: dados malformados), loga mas não quebra o jogo.
                    Debug.LogError($"[HandleRpcPatch] Error processing CUSTOM_RPC: {e}");
                }
            }

        }

        // NOVA ABORDAGEM: Harmony não pode patchar property getters em IL2CPP. Use um método utilitário alternativo.
        public static class ImpostorUtils
        {
            public static List<NetworkedPlayerInfo> GetImpostorsManual()
            {
                List<NetworkedPlayerInfo> impostors = new List<NetworkedPlayerInfo>();
                var allPlayersField = typeof(GameData).GetField("allPlayers", BindingFlags.NonPublic | BindingFlags.Instance);
                if (allPlayersField != null)
                {
                    var allPlayersObj = allPlayersField.GetValue(GameData.Instance);
                    if (allPlayersObj is IEnumerable allPlayersEnum)
                    {
                        foreach (var obj in allPlayersEnum)
                        {
                            NetworkedPlayerInfo p = obj as NetworkedPlayerInfo;
                            if (p != null && p.Role != null && p.Role.IsImpostor)
                            {
                                impostors.Add(p);
                            }
                        }
                    }
                }
                return impostors;
            }
        }
    }
}