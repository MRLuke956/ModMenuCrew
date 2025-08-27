using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using InnerNet;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModMenuCrew.Patches
{
    [HarmonyPatch]
    public static class LobbyHarmonyPatches
    {
        #region Constants & Enums
        public enum Lang { PTBR, EN, ES }
        private enum LogLevel { Info, Warning, Error }

        private const string COLOR_GREEN = "#80FF80", COLOR_YELLOW = "#FFFF80", COLOR_RED = "#FF8080";
        private const string COLOR_CYAN = "#80FFFF", COLOR_ORANGE = "#FFBF80", COLOR_PURPLE = "#D980FF";
        private const string COLOR_WHITE = "#FFFFFF", COLOR_ROLES = "#a7f";

        private const string SIZE_HEADER = "<size=37%>", SIZE_DETAILS = "<size=30%>", SIZE_ENDTAG = "</size>";

        private const float DEFAULT_LOBBY_TIMER_DURATION = 600f;

        private const int VANILLA_MAX_PLAYERS = 15;
        private const int VANILLA_MAX_IMPOSTORS = 3;
        private const float VANILLA_MAX_SPEED = 3.0f;
        private const float VANILLA_MIN_KILL_CD = 10.0f;

        private static readonly string[] KNOWN_MOD_SIGNATURES = { "(M)", "[MOD]", "REACTOR" };
        #endregion

        #region BepInEx Config Entries
        public static ConfigFile Config;

        private static ConfigEntry<bool> cfgEnableDebugLogging;
        private static ConfigEntry<string> cfgLanguage;
        private static ConfigEntry<bool> cfgDisableLobbyMusic;
        private static ConfigEntry<bool> cfgShowLobbyInfo;
        private static ConfigEntry<bool> cfgShowGameSettings;
        private static ConfigEntry<bool> cfgShowRolesInfo;
        private static ConfigEntry<bool> cfgShowPlayerList;
        private static ConfigEntry<int> cfgPlayerListLimit;
        private static ConfigEntry<bool> cfgShowLobbyTimer;
        private static ConfigEntry<bool> cfgRgbLobbyCode;
        private static ConfigEntry<bool> cfgStreamerMode;
        private static ConfigEntry<bool> cfgHideCode;
        private static ConfigEntry<string> cfgCustomCode;
        private static ConfigEntry<bool> cfgAutoExtendTimer;
        private static ConfigEntry<int> cfgAutoExtendThreshold;
        #endregion

        #region State
        public static float LobbyTimer { get; private set; } = DEFAULT_LOBBY_TIMER_DURATION;
        public static bool JoinedAsHost { get; private set; } = false;
        private static int rgbFrame = 0;
        private static float rgbTimer = 0f;
        private const float RGB_UPDATE_INTERVAL = 0.05f;
        public static Lang CurrentLang { get; private set; } = Lang.PTBR;
        #endregion

        #region Language System
        private static readonly Dictionary<Lang, Dictionary<string, string>> UI_STRINGS = new()
        {
            { Lang.PTBR, new() {
                { "Players", "Jogadores" }, { "Time", "Tempo" }, { "Lobby", "LOBBY" },
                { "Impostors", "Imps" }, { "KillCD", "Kill CD" }, { "Speed", "Veloc." },
                { "Map", "Mapa" }, { "Roles", "Roles Ativos" }, { "MODDED", "MOD" },
                { "Reason", "Motivo" }, { "Signature", "Assinatura de Mod" },
                { "CustomMode", "Modo de Jogo Custom" }, { "InvalidRules", "Regras Inválidas" }
            }},
            { Lang.EN, new() {
                { "Players", "Players" }, { "Time", "Time" }, { "Lobby", "LOBBY" },
                { "Impostors", "Imps" }, { "KillCD", "Kill CD" }, { "Speed", "Speed" },
                { "Map", "Map" }, { "Roles", "Roles Active" }, { "MODDED", "MOD" },
                { "Reason", "Reason" }, { "Signature", "Mod Signature" },
                { "CustomMode", "Custom Game Mode" }, { "InvalidRules", "Invalid Rules" }
            }},
        };

        private static string UI(string key)
        {
            if (UI_STRINGS.TryGetValue(CurrentLang, out var dict) && dict.TryGetValue(key, out var value)) return value;
            if (UI_STRINGS[Lang.EN].TryGetValue(key, out var fallbackValue)) return fallbackValue;
            return key;
        }
        #endregion

        #region Initialization
        public static void InitializeConfig(ConfigFile config)
        {
            try { ClassInjector.RegisterTypeInIl2Cpp<ModTooltipHandler>(); } catch {}
            if (config == null) { Debug.LogError("[LobbyPatches] ConfigFile is null!"); return; }
            Config = config;

            cfgLanguage = Config.Bind("1. General", "Language", "PTBR", "Language for UI. Options: PTBR, EN, ES");
            cfgEnableDebugLogging = Config.Bind("1. General", "Enable Debug Logging", false, "Enable detailed logs for troubleshooting.");
            cfgDisableLobbyMusic = Config.Bind("1. General", "Disable Lobby Music", false, "Disable the music in the lobby.");
            cfgShowLobbyInfo = Config.Bind("2. Game List Info", "Show Extra Info Panel", true, "Show the entire extra information panel in the game list.");
            cfgShowGameSettings = Config.Bind("2. Game List Info", "Show Game Settings", true, "Show game settings like speed, kill cooldown, etc.");
            cfgShowRolesInfo = Config.Bind("2. Game List Info", "Show Roles Info", true, "Show if the lobby has special roles enabled.");
            cfgShowPlayerList = Config.Bind("2. Game List Info", "Show Player List", false, "Show a partial list of players in the lobby (can cause clutter).");
            cfgPlayerListLimit = Config.Bind("2. Game List Info", "Player List Limit", 3, "How many player names to show in the list if enabled.");
            cfgStreamerMode = Config.Bind("3. In-Lobby Display", "Streamer Mode", false, "Enable features for streamers.");
            cfgHideCode = Config.Bind("3. In-Lobby Display", "Hide Code (Streamer Mode)", true, "Hides the lobby code when Streamer Mode is active.");
            cfgCustomCode = Config.Bind("3. In-Lobby Display", "Hidden Code Text", "SECRET", "Text to display instead of the lobby code.");
            cfgRgbLobbyCode = Config.Bind("3. In-Lobby Display", "RGB Lobby Code", true, "Enable a rainbow RGB effect for the lobby code text.");
            cfgShowLobbyTimer = Config.Bind("3. In-Lobby Display", "Show Lobby Countdown", true, "Shows a countdown timer for when the lobby might expire.");
            cfgAutoExtendTimer = Config.Bind("4. Host Features", "Auto-Extend Timer", true, "Automatically extend lobby timer when it's low (host only).");
            cfgAutoExtendThreshold = Config.Bind("4. Host Features", "Auto-Extend Threshold (s)", 90, "Time remaining in seconds to trigger the auto-extend.");

            if (Enum.TryParse<Lang>(cfgLanguage.Value, true, out var lang)) CurrentLang = lang;
            DebugLog("Configuration Initialized and Loaded.");
        }
        #endregion

        #region Helper Functions
        private static bool IsStreamerModeEnabled() => cfgStreamerMode?.Value ?? false;
        private static bool ShouldHideCode() => IsStreamerModeEnabled() && (cfgHideCode?.Value ?? false);
        private static string Colorize(string text, string color) => $"<color={color}>{text}</color>";

        private static string FormatPlatform(Platforms platform) => platform switch
        {
            Platforms.StandaloneSteamPC => "Steam",
            Platforms.StandaloneEpicPC => "Epic",
            Platforms.StandaloneWin10 => "MS Store",
            Platforms.IPhone or Platforms.Android => "Mobile",
            Platforms.Switch => "Switch",
            Platforms.Xbox => "Xbox",
            Platforms.Playstation => "PlayStation",
            _ => "PC"
        };
        #endregion

        #region SUPER IMPROVED MOD DETECTION
        private static (bool IsModded, string Reason) DetectIfModded(InnerNet.GameListing listing)
        {
            foreach (var signature in KNOWN_MOD_SIGNATURES)
            {
                if (listing.HostName.IndexOf(signature, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (true, UI("Signature"));
                }
            }

            var options = listing.Options;
            if (options == null) return (false, "");

            if (!Enum.IsDefined(typeof(GameModes), options.GameMode) || !Enum.IsDefined(typeof(RulesPresets), options.RulesPreset))
            {
                return (true, UI("CustomMode"));
            }

            if (options.NumImpostors > VANILLA_MAX_IMPOSTORS ||
                listing.MaxPlayers > VANILLA_MAX_PLAYERS ||
                options.GetFloat(FloatOptionNames.PlayerSpeedMod) > VANILLA_MAX_SPEED ||
                options.GetFloat(FloatOptionNames.KillCooldown) < VANILLA_MIN_KILL_CD)
            {
                return (true, UI("InvalidRules"));
            }

            return (false, "");
        }
        #endregion

        #region String Formatting & Harmony Patches
        private static string FormatGameSettings(IGameOptions options)
        {
            if (!(cfgShowGameSettings?.Value ?? false) || options == null) return "";
            var sb = new StringBuilder();
            sb.Append("\n").Append(SIZE_DETAILS);
            string gameMode = options.GameMode == GameModes.HideNSeek ? UI("HnS") : UI("Classic");
            sb.Append(Colorize(gameMode, COLOR_PURPLE)).Append(" | ");
            sb.Append($"{UI("Impostors")}: {Colorize(options.NumImpostors.ToString(), COLOR_WHITE)} | ");
            sb.Append($"{UI("KillCD")}: {Colorize(options.GetFloat(FloatOptionNames.KillCooldown) + "s", COLOR_WHITE)} | ");
            sb.Append($"{UI("Speed")}: {Colorize(options.GetFloat(FloatOptionNames.PlayerSpeedMod) + "x", COLOR_WHITE)}");
            sb.Append(SIZE_ENDTAG);
            return sb.ToString();
        }

        private static string FormatRolesInfo(IGameOptions options)
        {
            if (!(cfgShowRolesInfo?.Value ?? false) || options == null) return "";
            if (options.RulesPreset == RulesPresets.StandardRoles)
            {
                return $"\n{SIZE_DETAILS}{Colorize($"• {UI("Roles")}", COLOR_ROLES)}{SIZE_ENDTAG}";
            }
            return "";
        }

        [HarmonyPatch(typeof(GameContainer), nameof(GameContainer.SetupGameInfo))]
        [HarmonyPostfix]
        public static void OnSetupGameInfo(GameContainer __instance)
        {
            if (!(cfgShowLobbyInfo?.Value ?? false) || __instance.gameListing == null || __instance.capacity == null) return;

            try
            {
                var listing = __instance.gameListing;
                var (isModded, modReason) = DetectIfModded(listing);

                var sb = new StringBuilder();
                sb.Append(SIZE_HEADER);

                sb.Append(Colorize(listing.TrueHostName ?? "Lobby", COLOR_WHITE));
                if (isModded)
                {
                    string tooltipText = $"{UI("Reason")}: {modReason}";
                    string modTag = $"<link=\"{tooltipText}\">{Colorize($"[{UI("MODDED")}]", COLOR_RED)}</link>";
                    sb.Append(" ").Append(modTag);
                }
                sb.Append("\n");

                string playerCount = $"{listing.PlayerCount}/{listing.MaxPlayers}";
                string playerCountColor = listing.PlayerCount < 4 ? COLOR_RED : listing.PlayerCount < 10 ? COLOR_YELLOW : COLOR_GREEN;
                sb.Append(Colorize(playerCount, playerCountColor)).Append("   ");
                string lobbyCode = ShouldHideCode() ? (cfgCustomCode?.Value ?? "SECRET") : GameCode.IntToGameName(listing.GameId);
                sb.Append(Colorize(lobbyCode, COLOR_ORANGE)).Append("\n");

                sb.Append($"{UI("Map")}: {Colorize(Constants.MapNames[listing.MapId], COLOR_WHITE)} | ");
                sb.Append(Colorize(FormatPlatform(listing.Platform), COLOR_CYAN));

                sb.Append(SIZE_ENDTAG);

                sb.Append(FormatGameSettings(listing.Options));
                sb.Append(FormatRolesInfo(listing.Options));

                __instance.capacity.text = sb.ToString();
                __instance.capacity.richText = true;

                var tooltipHandler = __instance.gameObject.GetComponent<ModTooltipHandler>() ?? __instance.gameObject.AddComponent<ModTooltipHandler>();
                tooltipHandler.Setup(__instance.capacity);
            }
            catch (Exception e)
            {
                DebugLog($"Error in OnSetupGameInfo: {e}", LogLevel.Error);
                if (__instance?.capacity != null && __instance.gameListing != null)
                {
                    try { __instance.capacity.text = $"{__instance.gameListing.PlayerCount}/{__instance.gameListing.MaxPlayers}"; } catch { }
                }
            }
        }

        [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
        [HarmonyPostfix]
        public static void OnGameStartManagerUpdate(GameStartManager __instance)
        {
            if (__instance?.GameRoomNameCode == null) return;
            try
            {
                string lobbyCode = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
                string codeToShow = ShouldHideCode() ? (cfgCustomCode?.Value ?? "SECRET") : lobbyCode;
                string rgbText = (cfgRgbLobbyCode?.Value ?? false) ? GetRGBText(codeToShow) : codeToShow;

                string timerDisplay = "";
                if ((cfgShowLobbyTimer?.Value ?? false) && JoinedAsHost)
                {
                    int seconds = (int)LobbyTimer;
                    string timeStr = $"{seconds / 60}:{(seconds % 60):D2}";
                    string colorTag = seconds <= 60 ? COLOR_RED : seconds <= 180 ? COLOR_YELLOW : COLOR_GREEN;
                    timerDisplay = $" {Colorize($"({timeStr})", colorTag)}";
                }

                __instance.GameRoomNameCode.text = rgbText + timerDisplay;
                __instance.GameRoomNameCode.richText = true;
            }
            catch (Exception e)
            {
                DebugLog($"Error in OnGameStartManagerUpdate: {e}", LogLevel.Error);
                if (__instance?.GameRoomNameCode != null) { try { __instance.GameRoomNameCode.text = "ERROR"; } catch { } }
            }
        }

        private static string GetRGBText(string text)
        {
            rgbTimer += Time.deltaTime;
            if (rgbTimer > RGB_UPDATE_INTERVAL)
            {
                rgbFrame = (rgbFrame + 4) % 360;
                rgbTimer = 0f;
            }
            float hue = (rgbFrame / 360f);
            Color c = Color.HSVToRGB(hue, 0.9f, 1f);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
        }

        [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
        [HarmonyPostfix]
        public static void OnLobbyStart()
        {
            LobbyTimer = DEFAULT_LOBBY_TIMER_DURATION;
            JoinedAsHost = AmongUsClient.Instance?.AmHost ?? false;
        }

        [HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
        [HarmonyPostfix]
        public static void OnLobbyUpdate(LobbyBehaviour __instance)
        {
            if (cfgDisableLobbyMusic?.Value ?? false)
            {
                if (SoundManager.Instance && __instance.MapTheme)
                    SoundManager.Instance.StopSound(__instance.MapTheme);
            }

            LobbyTimer -= Time.deltaTime;

            if ((cfgAutoExtendTimer?.Value ?? false) && JoinedAsHost && LobbyTimer <= (cfgAutoExtendThreshold?.Value ?? 90))
            {
                __instance.RpcExtendLobbyTimer();
                LobbyTimer = DEFAULT_LOBBY_TIMER_DURATION;
                DebugLog("Auto-extended lobby timer.");
            }
        }
        #endregion

        #region Logging Utility
        private static void DebugLog(string message, LogLevel level = LogLevel.Info)
        {
            if (!(cfgEnableDebugLogging?.Value ?? false)) return;
            string formattedMessage = $"[LobbyPatches] {message}";
            switch (level)
            {
                case LogLevel.Warning: Debug.LogWarning(formattedMessage); break;
                case LogLevel.Error: Debug.LogError(formattedMessage); break;
                default: Debug.Log(formattedMessage); break;
            }
        }
        #endregion
    }

    #region Componente de Tooltip
    public class ModTooltipHandler : MonoBehaviour
    {
        private TextMeshPro _textComponent;
        private Camera _mainCamera;
        private GameObject _tooltipObject;
        private TextMeshProUGUI _tooltipText;

        public ModTooltipHandler(IntPtr ptr) : base(ptr) { }

        public void Setup(TextMeshPro textComponent)
        {
            _textComponent = textComponent;
        }

        void Update()
        {
            if (_textComponent == null || !gameObject.activeInHierarchy) return;

            _mainCamera = HudManager.Instance?.UICamera ?? Camera.main;
            if (_mainCamera == null) return;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(_textComponent, Input.mousePosition, _mainCamera);

            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = _textComponent.textInfo.linkInfo[linkIndex];
                ShowTooltip(linkInfo.GetLinkID());
            }
            else
            {
                HideTooltip();
            }
        }

        private void ShowTooltip(string text)
        {
            if (_tooltipObject == null) CreateTooltipObject();

            _tooltipObject.SetActive(true);
            _tooltipText.text = text;

            RectTransform tooltipRect = _tooltipObject.GetComponent<RectTransform>();
            tooltipRect.position = Input.mousePosition + new Vector3(15, 15, 0);
        }

        private void HideTooltip()
        {
            if (_tooltipObject != null)
                _tooltipObject.SetActive(false);
        }

        private void CreateTooltipObject()
        {
            Canvas topCanvas = FindObjectsOfType<Canvas>().OrderByDescending(c => c.sortingOrder).FirstOrDefault();
            if (topCanvas == null) { Debug.LogError("No Canvas found to create tooltip."); return; }

            _tooltipObject = new GameObject("ModTooltip");
            _tooltipObject.transform.SetParent(topCanvas.transform, false);

            RectTransform rect = _tooltipObject.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            var layoutElement = _tooltipObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 100;
            var contentSizeFitter = _tooltipObject.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image bg = _tooltipObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);

            _tooltipText = new GameObject("TooltipText").AddComponent<TextMeshProUGUI>();
            _tooltipText.transform.SetParent(rect, false);
            _tooltipText.fontSize = 16;
            _tooltipText.color = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.Left;

            _tooltipText.margin = new Vector4(8f, 4f, 8f, 4f);

            RectTransform textRect = _tooltipText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            _tooltipObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_tooltipObject != null) Object.Destroy(_tooltipObject);
        }
    }
    #endregion
}