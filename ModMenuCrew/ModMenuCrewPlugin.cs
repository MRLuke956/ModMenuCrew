using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
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
using TMPro;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using InnerNet;

namespace ModMenuCrew
{
    [BepInPlugin(Id, "Among Us Mod Menu Crew", ModVersion)]
    [BepInProcess("Among Us.exe")]
    public class ModMenuCrewPlugin : BasePlugin
    {
        public const string Id = "com.crewmod.oficial";
        public const string ModVersion = "5.3.0";

        public DebuggerComponent Component { get; private set; } = null!;
        public static ModMenuCrewPlugin Instance { get; private set; }
        public Harmony Harmony { get; } = new Harmony(Id);
        private Harmony _harmony;

        public override void Load()
        {
            Instance = this;
            Instance.Log.LogInfo($"Plugin {Id} version {ModVersion} is loading.");
            CleanupPlayerPrefsAndResetValidation();
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

        private void CleanupPlayerPrefsAndResetValidation()
        {
            try
            {
                string keyPrefix = "ModMenuCrew_Activated_";
                string versionSpecificKey = keyPrefix + ModVersion;
                PlayerPrefs.DeleteKey(versionSpecificKey); PlayerPrefs.DeleteKey(versionSpecificKey + "_Message");
                PlayerPrefs.DeleteKey(keyPrefix + "4.0.0"); PlayerPrefs.DeleteKey(keyPrefix + "4.0.0_Message");
                PlayerPrefs.DeleteKey(keyPrefix + "5.0.0"); PlayerPrefs.DeleteKey(keyPrefix + "5.0.0_Message");
                PlayerPrefs.Save();
                ModKeyValidator.ResetValidationState();
                Instance.Log.LogInfo("PlayerPrefs (incluindo versão atual) limpos. Estado de ativação resetado.");
            }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro ao limpar PlayerPrefs: {ex}"); } // ex é usado aqui
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
            private bool isModGloballyActivated = false;
            private string currentActivationStatusMessage = "Loading...";
            private bool isValidatingNow = false; private Task pendingValidationTask = null;
            private Canvas activationCanvasTMP; private TMP_InputField apiKeyInputFieldTMP;
            private TextMeshProUGUI statusMessageTextTMP; private Button validateButtonTMP;
            private TextMeshProUGUI validateButtonTextTMP; private Button getKeyButtonTMP;
            private GameObject activationPanelGO; private GameObject eventSystemGO;
            private GameObject canvasGO;
            private Il2CppSystem.Collections.Generic.List<string> pendingNotifications = new Il2CppSystem.Collections.Generic.List<string>();
            // Estado UI: seleção de role em grids
            private int lobbyRoleGridIndex = 0;
            private int inGameRoleGridIndex = 0;

            // **CORREÇÃO: Variável 'hasAttemptedInitialActivationUIShow' declarada aqui**
            private bool hasAttemptedInitialActivationUIShow = false;

            // Sistema de Pop-up de Sucesso
            private GameObject successPopupGO;

            // Cached assets for UI
            private TMP_FontAsset _cachedFont;
            private Texture2D _cachedNoiseTexture;
            private Sprite _cachedNoiseSprite;
            // Cached runtime textures/sprites to avoid leaks
            private Texture2D _headerGradientTexture;
            private Sprite _headerGradientSprite;
            private Texture2D _validateButtonGradientTexture;
            private Texture2D _getKeyButtonGradientTexture;
            private Texture2D _okButtonGradientTexture;

            // Throttle state for activation UI updates
            private string _lastStatusMessage;
            private bool _lastValidatingState;
            private string _lastInputText;


            public DebuggerComponent(IntPtr ptr) : base(ptr) { }

            public void CleanupResources()
            {
                try
                {
                    CleanupActivationUI();
                    CloseActivationSuccessPopup();
                    if (_cachedNoiseSprite != null) { Destroy(_cachedNoiseSprite); _cachedNoiseSprite = null; }
                    if (_cachedNoiseTexture != null) { Destroy(_cachedNoiseTexture); _cachedNoiseTexture = null; }
                    if (_headerGradientSprite != null) { Destroy(_headerGradientSprite); _headerGradientSprite = null; }
                    if (_headerGradientTexture != null) { Destroy(_headerGradientTexture); _headerGradientTexture = null; }
                    if (_validateButtonGradientTexture != null) { Destroy(_validateButtonGradientTexture); _validateButtonGradientTexture = null; }
                    if (_getKeyButtonGradientTexture != null) { Destroy(_getKeyButtonGradientTexture); _getKeyButtonGradientTexture = null; }
                    if (_okButtonGradientTexture != null) { Destroy(_okButtonGradientTexture); _okButtonGradientTexture = null; }
                    if (eventSystemGO != null) { Destroy(eventSystemGO); eventSystemGO = null; }
                    teleportManager = null; cheatManager = null; playerPickMenu = null;
                    mainWindow = null; tabControl = null; banAndPickTabControl = null;
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Error resource cleanup: {ex}"); }  // ex é usado aqui
            }

            void OnDestroy() => CleanupResources();

            void Awake()
            {
                try
                {
                    ModMenuCrewPlugin.Instance.Log.LogInfo("DebuggerComponent: Awake started.");
                    ModKeyValidator.LoadValidationState();
                    isModGloballyActivated = ModKeyValidator.IsKeyValidated;
                    currentActivationStatusMessage = ModKeyValidator.LastValidationMessage;

                    InitializeFeatureManagers();
                    InitializeMainWindowIMGUI();
                    InitializeTabsForGameIMGUI(); // Seu código original aqui

                    if (!isModGloballyActivated)
                    {
                        currentActivationStatusMessage = "Enter your activation key or get a new one.";
                        SetupActivationUI_TMP();
                        // A abertura automática da UI de ativação agora é no primeiro Update.
                    }
                    ModMenuCrewPlugin.Instance.Log.LogInfo($"DebuggerComponent: Awake completed. Mod initially {(isModGloballyActivated ? "activated" : "deactivated")}.");
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Critical error DebuggerComponent.Awake: {ex}"); } // ex é usado aqui
            }

            private void InitializeFeatureManagers() { teleportManager = new TeleportManager(); cheatManager = new CheatManager(); playerPickMenu = new PlayerPickMenu(); }

            private void InitializeMainWindowIMGUI()
            {
                // Apenas define uma largura e posição inicial. A altura será automática. (ajustado -3%)
                mainWindow = new DragWindow(new Rect(24, 24, 514, 0), $"ModMenuCrew v{ModMenuCrewPlugin.ModVersion} - ACTIVATED", DrawMainModWindowIMGUI)
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

            #region TMP Activation UI Creation and Management
            private TMP_FontAsset LoadGameFont(string primaryName = null, string fallbackName = null)
            {
                if (_cachedFont != null) return _cachedFont;
                try
                {
                    // IL2CPP: use non-generic overload with Il2CppType and cast objects to TMP_FontAsset
                    var found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>());
                    if (found != null && found.Length > 0)
                    {
                        TMP_FontAsset match = null;
                        if (!string.IsNullOrWhiteSpace(primaryName))
                        {
                            foreach (var obj in found)
                            {
                                var f = (obj != null) ? obj.TryCast<TMP_FontAsset>() : null;
                                if (f != null && f.name.IndexOf(primaryName, StringComparison.OrdinalIgnoreCase) >= 0) { match = f; break; }
                            }
                            if (match != null) return _cachedFont = match;
                        }
                        if (!string.IsNullOrWhiteSpace(fallbackName))
                        {
                            foreach (var obj in found)
                            {
                                var f = (obj != null) ? obj.TryCast<TMP_FontAsset>() : null;
                                if (f != null && f.name.IndexOf(fallbackName, StringComparison.OrdinalIgnoreCase) >= 0) { match = f; break; }
                            }
                            if (match != null) return _cachedFont = match;
                        }
                        // fallback to first available TMP font
                        foreach (var obj in found) { var f = (obj != null) ? obj.TryCast<TMP_FontAsset>() : null; if (f != null) { return _cachedFont = f; } }
                    }
                }
                catch (Exception ex)
                {
                    ModMenuCrewPlugin.Instance?.Log?.LogWarning($"[UI] Could not load TMP font: {ex.Message}");
                }
                return null; // TMP may fallback to a default; safe but not ideal
            }

            private void SetupActivationUI_TMP(bool forceRebuild = false)
            {
                try // O aviso CS0168 do usuário na linha 210 deve ser referente a este bloco catch (ou um próximo) no código original dele.
                {
                    if (activationCanvasTMP != null)
                    {
                        if (!forceRebuild)
                        {
                            activationCanvasTMP.gameObject.SetActive(false);
                            return;
                        }
                        // Força recriação completa para refletir novo tema
                        if (canvasGO != null) Destroy(canvasGO);
                        activationCanvasTMP = null; activationPanelGO = null; statusMessageTextTMP = null; validateButtonTMP = null; getKeyButtonTMP = null; validateButtonTextTMP = null; apiKeyInputFieldTMP = null;
                    }
                    ModMenuCrewPlugin.Instance.Log.LogInfo("Configuring Activation UI with TextMeshPro...");

                    if (FindObjectOfType<EventSystem>() == null) { if (eventSystemGO == null) { eventSystemGO = new GameObject("ModMenuCrew_EventSystem"); eventSystemGO.AddComponent<EventSystem>(); eventSystemGO.AddComponent<StandaloneInputModule>(); DontDestroyOnLoad(eventSystemGO); } }
                    else { if (eventSystemGO != null) { Destroy(eventSystemGO); eventSystemGO = null; } }

                    canvasGO = new GameObject("ModMenuCrew_ActivationCanvas"); DontDestroyOnLoad(canvasGO);
                    activationCanvasTMP = canvasGO.AddComponent<Canvas>();
                    activationCanvasTMP.renderMode = RenderMode.ScreenSpaceOverlay; activationCanvasTMP.sortingOrder = 32767;
                    CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
                    canvasGO.AddComponent<GraphicRaycaster>();
                    activationPanelGO = new GameObject("ActivationPanel");
                    activationPanelGO.transform.SetParent(activationCanvasTMP.transform, false);
                    Image panelImage = activationPanelGO.AddComponent<Image>(); panelImage.color = new Color(0.03f, 0.03f, 0.05f, 0.98f);
                    // Borda vermelha sutil
                    var panelOutline = activationPanelGO.AddComponent<Outline>(); panelOutline.effectColor = new Color(1f, 0.15f, 0.25f, 0.9f); panelOutline.effectDistance = new Vector2(2f, -2f);
                    RectTransform panelRect = activationPanelGO.GetComponent<RectTransform>();
                    panelRect.anchorMin = new Vector2(0.5f, 0.5f); panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                    panelRect.pivot = new Vector2(0.5f, 0.5f); panelRect.sizeDelta = new Vector2(450, 350);
                    panelRect.anchoredPosition = Vector2.zero;

                    // Header vermelho/preto
                    var headerGO = new GameObject("HeaderBar"); headerGO.transform.SetParent(panelRect, false);
                    var headerImg = headerGO.AddComponent<Image>();
                    var headerRt = headerGO.GetComponent<RectTransform>(); headerRt.anchorMin = new Vector2(0, 1); headerRt.anchorMax = new Vector2(1, 1); headerRt.pivot = new Vector2(0.5f, 1); headerRt.sizeDelta = new Vector2(0, 54); headerRt.anchoredPosition = new Vector2(0, 0);
                    // Gradiente vermelho -> preto
                    _headerGradientTexture = new Texture2D(1, 32);
                    for (int i = 0; i < 32; i++) { float t = i / 31f; Color c = Color.Lerp(new Color(0.18f, 0.02f, 0.04f, 0.98f), new Color(0.06f, 0.00f, 0.01f, 0.98f), t); _headerGradientTexture.SetPixel(0, i, c); }
                    _headerGradientTexture.Apply();
                    _headerGradientSprite = Sprite.Create(_headerGradientTexture, new Rect(0, 0, 1, 32), Vector2.one * 0.5f);
                    headerImg.sprite = _headerGradientSprite;
                    var headerOutline = headerGO.AddComponent<Outline>(); headerOutline.effectColor = new Color(1f, 0.12f, 0.22f, 0.9f); headerOutline.effectDistance = new Vector2(1.5f, -1.5f);

                    // Título no header
                    CreateTMPText(headerRt, "MOD MENU CREW", 24, new Color(1f, 0.2f, 0.35f), new Vector2(0, -4), new Vector2(420, 42), TextAlignmentOptions.Center);

                    // Mensagem de status logo abaixo do header
                    statusMessageTextTMP = CreateTMPText(panelRect, currentActivationStatusMessage, 18, new Color(0.95f, 0.95f, 0.98f), new Vector2(0, 80), new Vector2(400, 60), TextAlignmentOptions.Center);
                    if (statusMessageTextTMP != null) statusMessageTextTMP.enableWordWrapping = true;
                    CreateTMPText(panelRect, "Your Activation Key:", 18, new Color(1f, 0.3f, 0.45f), new Vector2(0, 20), new Vector2(400, 30), TextAlignmentOptions.Center);
                    apiKeyInputFieldTMP = CreateTMPInputField(panelRect, "", "Paste your key here...", new Vector2(0, -20), new Vector2(400, 50));
                    validateButtonTMP = CreateTMPButton(panelRect, "VALIDATE KEY", (UnityAction)delegate { if (!isValidatingNow && apiKeyInputFieldTMP != null) ProcessApiKeyValidation(apiKeyInputFieldTMP.text); }, new Vector2(-110, -90), new Vector2(180, 60));

                    getKeyButtonTMP = CreateTMPButton(panelRect, "GET NEW KEY", (UnityAction)delegate
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://mrluke956.github.io/userAmongKey/user.html", UseShellExecute = true }); }
                        catch (Exception ex) { currentActivationStatusMessage = "Error opening website. Visit: mrluke956.github.io/userAmongKey/user.html"; if (statusMessageTextTMP) statusMessageTextTMP.text = currentActivationStatusMessage; ManageActivationUIVisibility(); UnityEngine.Debug.LogError($"[ModMenuCrew] Erro ao abrir site: {ex}"); }
                    }, new Vector2(110, -90), new Vector2(180, 60));
                    CreateTMPText(panelRect, $"Mod Menu Crew | Version {ModMenuCrewPlugin.ModVersion}", 14, new Color(0.7f, 0.7f, 0.75f), new Vector2(0, -150), new Vector2(400, 30), TextAlignmentOptions.Center);

                    // Botões com tema vermelho/preto (gradiente + outline)
                    if (validateButtonTMP != null)
                    {
                        var img = validateButtonTMP.GetComponent<Image>();
                        if (img != null)
                        {
                            _validateButtonGradientTexture = new Texture2D(1, 32);
                            for (int i = 0; i < 32; i++) { float t = i / 31f; Color c = Color.Lerp(new Color(0.75f, 0.05f, 0.12f), new Color(0.45f, 0.02f, 0.06f), t); _validateButtonGradientTexture.SetPixel(0, i, c); }
                            _validateButtonGradientTexture.Apply();
                            img.sprite = Sprite.Create(_validateButtonGradientTexture, new Rect(0, 0, 1, 32), Vector2.one * 0.5f);
                            var outline = validateButtonTMP.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1f, 0.2f, 0.35f, 0.9f); outline.effectDistance = new Vector2(1.5f, -1.5f);
                        }
                        validateButtonTextTMP = validateButtonTMP.GetComponentInChildren<TextMeshProUGUI>(); if (validateButtonTextTMP == null) ModMenuCrewPlugin.Instance.Log.LogError("validateButtonTextTMP é NULO após GetComponentInChildren em Setup!");
                    }
                    if (getKeyButtonTMP != null)
                    {
                        var img = getKeyButtonTMP.GetComponent<Image>();
                        if (img != null)
                        {
                            _getKeyButtonGradientTexture = new Texture2D(1, 32);
                            for (int i = 0; i < 32; i++) { float t = i / 31f; Color c = Color.Lerp(new Color(0.35f, 0.02f, 0.06f), new Color(0.18f, 0.01f, 0.03f), t); _getKeyButtonGradientTexture.SetPixel(0, i, c); }
                            _getKeyButtonGradientTexture.Apply();
                            img.sprite = Sprite.Create(_getKeyButtonGradientTexture, new Rect(0, 0, 1, 32), Vector2.one * 0.5f);
                            var outline2 = getKeyButtonTMP.gameObject.AddComponent<Outline>(); outline2.effectColor = new Color(1f, 0.12f, 0.22f, 0.85f); outline2.effectDistance = new Vector2(1.5f, -1.5f);
                        }
                    }

                    activationCanvasTMP.gameObject.SetActive(false);
                    ModMenuCrewPlugin.Instance.Log.LogInfo("Activation UI TMP created and initially hidden (red/black theme).");

                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro SetupActivationUI_TMP: {ex}"); } // ex usado aqui (esta é aproximadamente a linha 210 do código original que você forneceu)
            }

            private TextMeshProUGUI CreateTMPText(RectTransform parent, string text, int fontSize, Color color, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
            {
                GameObject textGO = new GameObject($"TMPText_{Guid.NewGuid().ToString().Substring(0, 8)}"); textGO.transform.SetParent(parent, false); TextMeshProUGUI tmpText = textGO.AddComponent<TextMeshProUGUI>(); tmpText.text = text; tmpText.fontSize = fontSize; tmpText.color = color; tmpText.alignment = alignment; tmpText.font = LoadGameFont(); RectTransform rectTransform = textGO.GetComponent<RectTransform>(); rectTransform.anchoredPosition = anchoredPosition; rectTransform.sizeDelta = sizeDelta; return tmpText;
            }
            private TMP_InputField CreateTMPInputField(RectTransform parent, string initialText, string placeholderText, Vector2 anchoredPosition, Vector2 sizeDelta)
            {
                GameObject inputFieldGO = new GameObject("TMP_InputField_Activation"); inputFieldGO.transform.SetParent(parent, false); Image bgImage = inputFieldGO.AddComponent<Image>(); bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f); bgImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect); TMP_InputField inputField = inputFieldGO.AddComponent<TMP_InputField>(); RectTransform rectTransform = inputFieldGO.GetComponent<RectTransform>(); rectTransform.anchoredPosition = anchoredPosition; rectTransform.sizeDelta = sizeDelta;
                GameObject textAreaGO = new GameObject("Text Area"); textAreaGO.transform.SetParent(rectTransform, false); RectTransform textAreaRect = textAreaGO.AddComponent<RectTransform>(); textAreaRect.anchorMin = Vector2.zero; textAreaRect.anchorMax = Vector2.one; textAreaRect.offsetMin = new Vector2(10, 5); textAreaRect.offsetMax = new Vector2(-10, -5); textAreaGO.AddComponent<RectMask2D>();
                var inputFont = LoadGameFont();
                GameObject placeholderGO = new GameObject("Placeholder"); placeholderGO.transform.SetParent(textAreaRect, false); TextMeshProUGUI placeholderTMP = placeholderGO.AddComponent<TextMeshProUGUI>(); placeholderTMP.text = placeholderText; placeholderTMP.fontSize = 16; placeholderTMP.fontStyle = FontStyles.Italic; placeholderTMP.color = new Color(0.7f, 0.7f, 0.7f, 0.5f); placeholderTMP.alignment = TextAlignmentOptions.Left; placeholderTMP.font = inputFont; RectTransform placeholderRect = placeholderGO.GetComponent<RectTransform>(); placeholderRect.anchorMin = Vector2.zero; placeholderRect.anchorMax = Vector2.one; placeholderRect.sizeDelta = Vector2.zero;
                GameObject textGO = new GameObject("Text"); textGO.transform.SetParent(textAreaRect, false); TextMeshProUGUI textTMP_Input = textGO.AddComponent<TextMeshProUGUI>(); textTMP_Input.text = initialText; textTMP_Input.fontSize = 18; textTMP_Input.color = Color.white; textTMP_Input.alignment = TextAlignmentOptions.Left; textTMP_Input.font = inputFont; RectTransform textRect = textGO.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
                inputField.textViewport = textAreaRect; inputField.textComponent = textTMP_Input; inputField.placeholder = placeholderTMP; return inputField;
            }
            private Button CreateTMPButton(RectTransform parent, string buttonText, UnityEngine.Events.UnityAction onClickAction, Vector2 anchoredPosition, Vector2 sizeDelta)
            {
                GameObject buttonGO = new GameObject($"TMPButton_{buttonText.Replace(" ", "")}"); buttonGO.transform.SetParent(parent, false); Image buttonImage = buttonGO.AddComponent<Image>(); buttonImage.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect); Button button = buttonGO.AddComponent<Button>(); RectTransform rectTransform = buttonGO.GetComponent<RectTransform>(); rectTransform.anchoredPosition = anchoredPosition; rectTransform.sizeDelta = sizeDelta;
                GameObject textGO = new GameObject("Text (TMP)"); textGO.transform.SetParent(rectTransform, false); TextMeshProUGUI tmpText_Button = textGO.AddComponent<TextMeshProUGUI>(); tmpText_Button.text = buttonText; tmpText_Button.fontSize = 18; tmpText_Button.color = Color.white; tmpText_Button.alignment = TextAlignmentOptions.Center; tmpText_Button.font = LoadGameFont(); RectTransform textRect = textGO.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.sizeDelta = Vector2.zero;
                button.targetGraphic = buttonImage; button.onClick.AddListener(onClickAction); return button;
            }

            private void ManageActivationUIVisibility()
            {
                try
                {
                    if (activationCanvasTMP == null) return;

                    if (statusMessageTextTMP != null)
                    {
                        if (!string.Equals(_lastStatusMessage, currentActivationStatusMessage, StringComparison.Ordinal))
                        {
                            statusMessageTextTMP.text = currentActivationStatusMessage;
                            string lower = currentActivationStatusMessage.ToLowerInvariant();
                            if (lower.Contains("error") || lower.Contains("invalid") || lower.Contains("failed") || lower.Contains("timeout"))
                            { statusMessageTextTMP.color = Color.red; }
                            else if (lower.Contains("success") || lower.Contains("validated") || lower.Contains("activated"))
                            { statusMessageTextTMP.color = Color.green; }
                            else { statusMessageTextTMP.color = Color.white; }
                            _lastStatusMessage = currentActivationStatusMessage;
                        }
                    }
                    else { ModMenuCrewPlugin.Instance.Log.LogWarning("ManageActivationUIVisibility: statusMessageTextTMP é nulo."); }

                    if (validateButtonTMP != null)
                    {
                        string inputText = apiKeyInputFieldTMP != null ? apiKeyInputFieldTMP.text : null;
                        bool newInteractable = !isValidatingNow && !string.IsNullOrWhiteSpace(inputText);
                        if (validateButtonTMP.interactable != newInteractable)
                        {
                            validateButtonTMP.interactable = newInteractable;
                        }
                        if (validateButtonTextTMP != null)
                        {
                            string targetText = isValidatingNow ? "Validating..." : "VALIDATE KEY";
                            if (!string.Equals(validateButtonTextTMP.text, targetText, StringComparison.Ordinal))
                            {
                                validateButtonTextTMP.text = targetText;
                            }
                        }
                        else { ModMenuCrewPlugin.Instance.Log.LogWarning("ManageActivationUIVisibility: validateButtonTextTMP (texto do botão) é nulo."); }
                        _lastValidatingState = isValidatingNow;
                        _lastInputText = inputText;
                    }
                    else { ModMenuCrewPlugin.Instance.Log.LogWarning("ManageActivationUIVisibility: validateButtonTMP é nulo."); }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro em ManageActivationUIVisibility: {ex}"); } // ex usado aqui
            }
            #endregion
            #region Sistema de Pop-up de Sucesso
            // Método que inicia o processo de fechamento com fade-out
            private void CloseActivationSuccessPopup()
            {
                if (successPopupGO != null)
                {
                    // Inicia a Coroutine para o fade-out em vez de destruir imediatamente
                    StartCoroutine(FadeOutAndDestroy(successPopupGO, 0.3f).WrapToIl2Cpp());
                    successPopupGO = null;
                    ModMenuCrewPlugin.Instance.Log.LogInfo("Pop-up de sucesso fechando com fade-out.");
                }
            }

            // Coroutine que executa a animação de fade-out
            [HideFromIl2Cpp]
            private System.Collections.IEnumerator FadeOutAndDestroy(GameObject objectToFade, float duration)
            {
                // Pega todos os elementos gráficos do pop-up (imagens, textos, etc.)
                var graphics = objectToFade.GetComponentsInChildren<Graphic>();
                float timer = 0f;

                // Guarda as cores originais para calcular o fade de forma correta
                var originalColors = new System.Collections.Generic.List<Color>();
                foreach (var graphic in graphics)
                {
                    originalColors.Add(graphic.color);
                }

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float alpha = 1.0f - (timer / duration); // Calcula a nova transparência

                    for (int i = 0; i < graphics.Length; i++)
                    {
                        if (graphics[i] != null)
                        {
                            Color newColor = originalColors[i];
                            newColor.a = alpha;
                            graphics[i].color = newColor;
                        }
                    }
                    yield return null; // Espera o próximo frame
                }

                Destroy(objectToFade); // Destrói o objeto após o fade-out
                // Cleanup cached sprites/textures if any
                if (_cachedNoiseSprite != null) { Destroy(_cachedNoiseSprite); _cachedNoiseSprite = null; }
                if (_cachedNoiseTexture != null) { Destroy(_cachedNoiseTexture); _cachedNoiseTexture = null; }
            }


            private void ShowActivationSuccessPopup(string message)
            {
                try
                {
                    if (successPopupGO != null) Destroy(successPopupGO);

                    // --- Canvas Principal ---
                    successPopupGO = new GameObject("ModMenuCrew_SuccessPopupCanvas");
                    DontDestroyOnLoad(successPopupGO);
                    var popupCanvas = successPopupGO.AddComponent<Canvas>();
                    popupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    popupCanvas.sortingOrder = 32767;

                    var scaler = successPopupGO.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);
                    successPopupGO.AddComponent<GraphicRaycaster>();

                    // --- Fundo Overlay com Ruído (Efeito Espacial) ---
                    var overlayGO = new GameObject("SpaceOverlayNoise");
                    overlayGO.transform.SetParent(popupCanvas.transform, false);
                    var overlayImage = overlayGO.AddComponent<Image>();
                    if (_cachedNoiseTexture == null)
                    {
                        _cachedNoiseTexture = new Texture2D(128, 128);
                        for (int y = 0; y < _cachedNoiseTexture.height; y++)
                        {
                            for (int x = 0; x < _cachedNoiseTexture.width; x++)
                            {
                                float noise = UnityEngine.Random.Range(0.05f, 0.1f);
                                _cachedNoiseTexture.SetPixel(x, y, new Color(noise, noise, noise, 1));
                            }
                        }
                        _cachedNoiseTexture.Apply();
                    }
                    if (_cachedNoiseSprite == null)
                    {
                        _cachedNoiseSprite = Sprite.Create(_cachedNoiseTexture, new Rect(0, 0, 128, 128), Vector2.one * 0.5f);
                    }
                    overlayImage.sprite = _cachedNoiseSprite;
                    overlayImage.type = Image.Type.Tiled;
                    overlayImage.color = new Color(1f, 1f, 1f, 0.65f); // Ficou menos intenso para destacar o painel
                    var overlayRect = overlayGO.GetComponent<RectTransform>();
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;

                    // --- Painel Principal com Efeito de Chanfro (Bevel) ---
                    // Sombra (para o efeito 3D)
                    var panelShadowGO = new GameObject("PanelBevel_Shadow");
                    panelShadowGO.transform.SetParent(popupCanvas.transform, false);
                    panelShadowGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
                    panelShadowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 300);
                    panelShadowGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(2, -2);

                    // Destaque (para o efeito 3D)
                    var panelHighlightGO = new GameObject("PanelBevel_Highlight");
                    panelHighlightGO.transform.SetParent(popupCanvas.transform, false);
                    panelHighlightGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.1f);
                    panelHighlightGO.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 300);
                    panelHighlightGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(-2, 2);

                    // Painel Base
                    var panelGO = new GameObject("MainPanel");
                    panelGO.transform.SetParent(popupCanvas.transform, false);
                    var panelImage = panelGO.AddComponent<Image>();
                    panelImage.color = new Color(0.18f, 0.20f, 0.25f); // Cinza escuro metálico do jogo
                    var panelRect = panelGO.GetComponent<RectTransform>();
                    panelRect.sizeDelta = new Vector2(500, 300);

                    // --- Elementos Visuais Internos ---
                    // Ícone de Checkmark (✓) Estilizado
                    var checkmarkIconGO = new GameObject("Icon_Checkmark");
                    checkmarkIconGO.transform.SetParent(panelRect, false);
                    var checkmarkRect = checkmarkIconGO.AddComponent<RectTransform>();
                    checkmarkRect.anchoredPosition = new Vector2(0, 95);
                    checkmarkRect.sizeDelta = new Vector2(80, 80);
                    // Parte curta do ✓ (usar Image ao invés de Button para reduzir overhead)
                    var checkPart1 = new GameObject("CheckBarShort");
                    checkPart1.transform.SetParent(checkmarkRect, false);
                    var checkImg1 = checkPart1.AddComponent<Image>();
                    checkImg1.color = new Color(0.1f, 0.8f, 0.2f);
                    var checkRt1 = checkPart1.GetComponent<RectTransform>();
                    checkRt1.anchoredPosition = new Vector2(-15, 0);
                    checkRt1.sizeDelta = new Vector2(15, 40);
                    checkRt1.localRotation = Quaternion.Euler(0, 0, 45);
                    // Parte longa do ✓
                    var checkPart2 = new GameObject("CheckBarLong");
                    checkPart2.transform.SetParent(checkmarkRect, false);
                    var checkImg2 = checkPart2.AddComponent<Image>();
                    checkImg2.color = new Color(0.1f, 0.8f, 0.2f);
                    var checkRt2 = checkPart2.GetComponent<RectTransform>();
                    checkRt2.anchoredPosition = new Vector2(10, -10);
                    checkRt2.sizeDelta = new Vector2(15, 70);
                    checkRt2.localRotation = Quaternion.Euler(0, 0, -45);

                    checkPart2.GetComponent<Image>().color = new Color(0.1f, 0.8f, 0.2f);


                    // Título com Sombra
                    var titleShadow = CreateTMPText(panelRect, "ATIVAÇÃO CONCLUÍDA", 28, Color.black, new Vector2(2, 43), new Vector2(480, 50), TextAlignmentOptions.Center);
                    titleShadow.fontStyle = FontStyles.Bold;
                    var titleText = CreateTMPText(panelRect, "ATIVAÇÃO CONCLUÍDA", 28, new Color(0.95f, 0.85f, 0.1f), new Vector2(0, 45), new Vector2(480, 50), TextAlignmentOptions.Center);
                    titleText.fontStyle = FontStyles.Bold;

                    // Mensagem
                    var messageText = CreateTMPText(panelRect, message, 18, Color.white, new Vector2(0, -15), new Vector2(450, 80), TextAlignmentOptions.Center);
                    if (messageText != null) messageText.enableWordWrapping = true;

                    // Botão "ENTENDI"
                    var okButton = CreateTMPButton(panelRect, "ENTENDI", (UnityAction)delegate { CloseActivationSuccessPopup(); }, new Vector2(0, -95), new Vector2(180, 50));
                    var okButtonImage = okButton.GetComponent<Image>();
                    if (okButtonImage != null)
                    {
                        _okButtonGradientTexture = new Texture2D(1, 32);
                        for (int i = 0; i < 32; i++)
                        {
                            float t = i / 31f;
                            Color emergencyColor = Color.Lerp(new Color(0.9f, 0.6f, 0.1f), new Color(0.8f, 0.4f, 0.05f), t);
                            _okButtonGradientTexture.SetPixel(0, i, emergencyColor);
                        }
                        _okButtonGradientTexture.Apply();
                        okButtonImage.sprite = Sprite.Create(_okButtonGradientTexture, new Rect(0, 0, 1, 32), Vector2.one * 0.5f);
                    }
                    var okButtonText = okButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (okButtonText != null)
                    {
                        okButtonText.fontSize = 20;
                        okButtonText.fontStyle = FontStyles.Bold;
                        okButtonText.color = Color.white;
                    }

                    ModMenuCrewPlugin.Instance.Log.LogInfo("Pop-up de sucesso aprimorado visualmente e exibido!");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ModMenuCrew] Erro ao criar o pop-up de sucesso aprimorado: {ex}");
                }
            }
            #endregion


            private void ProcessApiKeyValidation(string keyToValidate)
            {
                if (isValidatingNow) return;
                if (string.IsNullOrWhiteSpace(keyToValidate))
                { currentActivationStatusMessage = "Please enter a key."; ManageActivationUIVisibility(); return; }
                keyToValidate = keyToValidate.Trim();
                isValidatingNow = true;
                currentActivationStatusMessage = "Validating your key, please wait...";
                ManageActivationUIVisibility(); // Atualiza UI imediatamente
                pendingValidationTask = ModKeyValidator.ValidateKeyAsync(keyToValidate);
            }

            private void CleanupActivationUI()
            {
                try
                {
                    if (canvasGO != null)
                    {
                        if (activationCanvasTMP != null && activationCanvasTMP.gameObject.activeInHierarchy) activationCanvasTMP.gameObject.SetActive(false);
                        activationPanelGO = null; apiKeyInputFieldTMP = null; statusMessageTextTMP = null;
                        validateButtonTMP = null; getKeyButtonTMP = null; validateButtonTextTMP = null;
                        activationCanvasTMP = null;
                        if (Application.isPlaying) Destroy(canvasGO);
                        canvasGO = null;
                        ModMenuCrewPlugin.Instance.Log.LogInfo("Activation UI cleaned up.");
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro CleanupActivationUI: {ex}"); } // ex usado aqui
            }

            private void HandleValidationComplete()
            {
                try
                {
                    isModGloballyActivated = ModKeyValidator.IsKeyValidated;
                    currentActivationStatusMessage = ModKeyValidator.LastValidationMessage;

                    if (!isModGloballyActivated)
                    {
                        ModMenuCrewPlugin.Instance.Log.LogError($"[ModMenuCrew] Validation ended. Result: Failed. Message: {currentActivationStatusMessage}");
                    }

                    if (isModGloballyActivated)
                    {
                        CleanupActivationUI();
                        if (mainWindow != null)
                        {
                            mainWindow.Title = $"ModMenuCrew v{ModMenuCrewPlugin.ModVersion} - ACTIVATED";
                        }

                        // ## MELHORIA 2: Mensagem de instrução da tecla F1 aprimorada para ser mais clara e amigável.
                        string successMessage = "O menu agora pode ser aberto ou fechado a qualquer momento pressionando a tecla F1.\n\nBom jogo!";
                        ShowActivationSuccessPopup(successMessage);
                    }
                    else
                    {
                        ShowNotification($"Activation failed: {currentActivationStatusMessage}");
                        if (apiKeyInputFieldTMP != null) apiKeyInputFieldTMP.text = "";
                        if (activationCanvasTMP != null && activationCanvasTMP.gameObject.activeSelf) { ManageActivationUIVisibility(); }
                        else { ModMenuCrewPlugin.Instance.Log.LogWarning("HandleValidationComplete (Falha): Canvas de ativação nulo ou inativo."); }
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro HandleValidationComplete: {ex}"); }
                finally
                {
                    isValidatingNow = false;
                    if (!isModGloballyActivated && activationCanvasTMP != null && activationCanvasTMP.gameObject.activeSelf) { ManageActivationUIVisibility(); }
                }
            }


            private void DrawMainModWindowIMGUI()
            {
                if (!isModGloballyActivated)
                {
                    GUILayout.Label("Mod not activated. Press F1.", GuiStyles.ErrorStyle);
                    return;
                }

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
                    GUILayout.Label("Created by MR Luke X", GuiStyles.LabelStyle);
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
                            if (GUILayout.Button("Rainbow Names (All)", GuiStyles.ButtonStyle))
                            {
                                ImpostorForcer.HostNameManager.SendChatMessage();
                                StartCoroutine(ImpostorForcer.ForceNameRainbowForEveryone().WrapToIl2Cpp());
                            }
                        }
                        GUILayout.Label("Visual Name Cheats", GuiStyles.HeaderStyle);
                        if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost)
                        {
                            if (GUILayout.Button("Name Changer (HACKED/BY/MRLukex)", GuiStyles.ButtonStyle))
                            {
                                ImpostorForcer.HostNameManager.SendChatMessage();
                                ImpostorForcer.StartForceUniqueNamesForAll();
                                GameCheats.LocalVisualScanForEveryone(15f);
                                ShowNotification("Name changer activated for all players!");
                            }
                            if (GUILayout.Button("Stop Name Changer", GuiStyles.ButtonStyle))
                            {
                                ImpostorForcer.StopForceUniqueNames();
                                ShowNotification("Name changer stopped!");
                            }
                            if (GUILayout.Button("YT", GuiStyles.ButtonStyle))
                            {
                                ImpostorForcer.HostNameManager.SendChatMessage();
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
                    // If activation UI is visible, keep checking validation/paste states
                    if (!isModGloballyActivated && !hasAttemptedInitialActivationUIShow)
                    {
                        if (activationCanvasTMP == null) SetupActivationUI_TMP();
                        if (activationCanvasTMP != null)
                        {
                            if (!activationCanvasTMP.gameObject.activeSelf)
                            {
                                activationCanvasTMP.gameObject.SetActive(true);
                                ModMenuCrewPlugin.Instance.Log.LogInfo("Activation UI panel opened automatically via Update's initial check.");
                            }
                            ManageActivationUIVisibility();
                        }
                        hasAttemptedInitialActivationUIShow = true;
                    }

                    if (pendingValidationTask != null && pendingValidationTask.IsCompleted)
                    {
                        try { HandleValidationComplete(); }
                        catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro ao processar validação no Update: {ex}"); } // ex usado aqui
                        finally { pendingValidationTask = null; }
                    }

                    if (!isModGloballyActivated && activationCanvasTMP != null && activationCanvasTMP.gameObject.activeSelf)
                    {
                        ManageActivationUIVisibility();
                    }

                    if (Input.GetKeyDown(KeyCode.F1))
                    {
                        if (isModGloballyActivated)
                        {
                            if (mainWindow != null) mainWindow.Enabled = !mainWindow.Enabled;
                        }
                        else
                        {
                            if (activationCanvasTMP == null) SetupActivationUI_TMP();
                            if (activationCanvasTMP != null)
                            {
                                bool newState = !activationCanvasTMP.gameObject.activeSelf;
                                activationCanvasTMP.gameObject.SetActive(newState);
                                if (newState) ManageActivationUIVisibility();
                                if (newState && !hasAttemptedInitialActivationUIShow) hasAttemptedInitialActivationUIShow = true;
                            }
                        }
                    }
                    if (isModGloballyActivated)
                    {
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
                    else { if (IsNoclipping && PlayerControl.LocalPlayer?.Collider != null) { PlayerControl.LocalPlayer.Collider.enabled = true; IsNoclipping = false; } if (InfiniteVision && HudManager.Instance?.ShadowQuad != null) { HudManager.Instance.ShadowQuad.gameObject.SetActive(true); InfiniteVision = false; } }
                }
                catch (Exception ex) { UnityEngine.Debug.LogError($"[ModMenuCrew] Erro DebuggerComponent.Update: {ex}"); } // ex usado aqui
            }
            void OnGUI() { if (isModGloballyActivated && mainWindow != null && mainWindow.Enabled) mainWindow.OnGUI(); }
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

    public class ApiValidationResponse { [JsonPropertyName("status")] public string Status { get; set; } [JsonPropertyName("message")] public string Message { get; set; } [JsonPropertyName("download_token")] public string Download_Token { get; set; } }
    public static class ModKeyValidator
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string ApiBaseUrl = "https://keygenx-ce8h.onrender.com";
        private const string PlayerPrefsKeyActivatedPrefix = "ModMenuCrew_Activated_";
        private static string GetPlayerPrefsKeyActivated() => PlayerPrefsKeyActivatedPrefix + ModMenuCrewPlugin.ModVersion;
        public static bool IsKeyValidated { get; private set; } = false;
        public static string LastValidationMessage { get; private set; } = "Aguardando validação...";
        static ModKeyValidator() { try { httpClient.Timeout = TimeSpan.FromSeconds(20); httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ModMenuCrew/{ModMenuCrewPlugin.ModVersion}"); } catch (Exception ex) { UnityEngine.Debug.LogError($"[ModKeyValidator] Erro HttpClient init: {ex.Message}"); } } // ex usado aqui

        public static async Task ValidateKeyAsync(string keyFromInput)
        {
            if (string.IsNullOrWhiteSpace(keyFromInput)) { LastValidationMessage = "No key provided."; IsKeyValidated = false; SaveValidationState(false, LastValidationMessage); return; }
            IsKeyValidated = false; LastValidationMessage = "Validating key...";
            try
            {
                string requestUrl = $"{ApiBaseUrl}/validate?key={Uri.EscapeDataString(keyFromInput)}";
                HttpResponseMessage response = await httpClient.GetAsync(requestUrl);
                string jsonResponse = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                if (response.IsSuccessStatusCode) { ApiValidationResponse validationResponse = JsonSerializer.Deserialize<ApiValidationResponse>(jsonResponse, options); if (validationResponse != null && "success".Equals(validationResponse.Status, StringComparison.OrdinalIgnoreCase)) { IsKeyValidated = true; LastValidationMessage = validationResponse.Message ?? "Key validated!"; } else { IsKeyValidated = false; LastValidationMessage = validationResponse?.Message ?? "Invalid key/API error."; } }
                else { IsKeyValidated = false; string serverErrorMessage = $"Server error ({response.StatusCode})."; try { if (!string.IsNullOrWhiteSpace(jsonResponse) && jsonResponse.TrimStart().StartsWith("{")) { ApiValidationResponse errorDetails = JsonSerializer.Deserialize<ApiValidationResponse>(jsonResponse, options); if (!string.IsNullOrWhiteSpace(errorDetails?.Message)) serverErrorMessage = errorDetails.Message; } } catch (JsonException) { } LastValidationMessage = serverErrorMessage; }
            }
            catch (HttpRequestException httpEx) { IsKeyValidated = false; LastValidationMessage = $"Network error: {TruncateMessage(httpEx.Message)}"; UnityEngine.Debug.LogError($"[ModKeyValidator] HttpRequestException: {httpEx}"); } // ex usado aqui
            catch (TaskCanceledException) { IsKeyValidated = false; LastValidationMessage = "Validation timeout."; UnityEngine.Debug.LogWarning($"[ModKeyValidator] TaskCanceledException (Timeout)"); }
            catch (JsonException jsonEx) { IsKeyValidated = false; LastValidationMessage = $"API data error: {TruncateMessage(jsonEx.Message)}"; UnityEngine.Debug.LogError($"[ModKeyValidator] JsonException: {jsonEx}"); } // ex usado aqui
            catch (Exception ex) { IsKeyValidated = false; LastValidationMessage = $"Unexpected error: {TruncateMessage(ex.Message)}"; UnityEngine.Debug.LogError($"[ModKeyValidator] Exception: {ex}"); } // ex usado aqui
            SaveValidationState(IsKeyValidated, LastValidationMessage);
        }
        public static string TruncateMessage(string message, int maxLength = 50) { return string.IsNullOrEmpty(message) ? "" : (message.Length <= maxLength ? message : message.Substring(0, maxLength) + "..."); }
        private static bool _forceStartDeactivated = true;
        public static void LoadValidationState() { if (_forceStartDeactivated) { ResetValidationState(); return; } }
        public static void ResetValidationState()
        {
            IsKeyValidated = false; LastValidationMessage = "Enter your activation key.";
            PlayerPrefs.DeleteKey(GetPlayerPrefsKeyActivated()); PlayerPrefs.DeleteKey(GetPlayerPrefsKeyActivated() + "_Message"); PlayerPrefs.Save();
            if (ModMenuCrewPlugin.Instance?.Log != null) ModMenuCrewPlugin.Instance.Log.LogInfo("[ModKeyValidator] Validation state reset.");
        }
        private static void SaveValidationState(bool isValidated, string message)
        {
            PlayerPrefs.SetInt(GetPlayerPrefsKeyActivated(), isValidated ? 1 : 0); PlayerPrefs.SetString(GetPlayerPrefsKeyActivated() + "_Message", message); PlayerPrefs.Save();
            IsKeyValidated = isValidated; LastValidationMessage = message;
        }
    }
}