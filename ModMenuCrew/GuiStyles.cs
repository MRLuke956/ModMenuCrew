using System;
using UnityEngine;

namespace ModMenuCrew.UI.Styles;

public static class GuiStyles
{
    /// <summary>
    /// Defini��es de cores para o tema visual do mod.
    /// </summary>
    public static class Theme
    {
        // === SHOWCASE VERSION BADGE ===
        public static readonly Color ShowcaseBadge = new Color(1f, 0.5f, 0f, 1f); // Orange badge
        public static readonly Color ShowcaseGlow = new Color(1f, 0.6f, 0.1f, 0.3f); // Orange glow
        // Cores de Fundo
        public static readonly Color BgDarkA = new Color(0.07f, 0.07f, 0.07f, 0.92f); // Fundo principal mais escuro
        public static readonly Color BgDarkB = new Color(0.06f, 0.06f, 0.06f, 0.92f); // Fundo principal mais claro
        public static readonly Color BgSection = new Color(0.05f, 0.05f, 0.06f, 0.85f); // Fundo para se��es

        // Cores de Cabe�alho
        public static readonly Color HeaderTop = new Color(0.10f, 0.02f, 0.04f, 0.95f); // Gradiente superior do cabe�alho
        public static readonly Color HeaderBottom = new Color(0.06f, 0.01f, 0.03f, 0.95f); // Gradiente inferior do cabe�alho

        // Cores de Destaque e Acentua��o
        public static readonly Color Accent = new Color(1f, 0.5f, 0f, 1f); // Laranja vitrine
        public static readonly Color AccentSoft = new Color(1f, 0.6f, 0.2f, 1f); // Laranja suave
        public static readonly Color AccentDim = new Color(0.7f, 0.35f, 0f, 1f); // Laranja escuro
        public static readonly Color AccentHover = new Color(1f, 0.55f, 0.1f, 1f); // Laranja hover
        public static readonly Color AccentActive = new Color(0.85f, 0.42f, 0f, 1f); // Laranja ativo

        // Cores de Bot�o
        public static readonly Color ButtonTop = new Color(0.11f, 0.11f, 0.12f, 0.95f); // Gradiente superior do bot�o
        public static readonly Color ButtonBottom = new Color(0.08f, 0.08f, 0.10f, 0.95f); // Gradiente inferior do bot�o
        public static readonly Color ButtonHoverTop = new Color(0.13f, 0.13f, 0.16f, 0.95f); // Gradiente superior do bot�o em hover
        public static readonly Color ButtonHoverBottom = new Color(0.10f, 0.10f, 0.13f, 0.95f); // Gradiente inferior do bot�o em hover
        public static readonly Color ButtonActiveTop = new Color(0.12f, 0.02f, 0.05f, 0.95f); // Gradiente superior do bot�o ativo
        public static readonly Color ButtonActiveBottom = new Color(0.09f, 0.02f, 0.04f, 0.95f); // Gradiente inferior do bot�o ativo

        // Cores de Texto
        public static readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.98f, 1f); // Texto principal claro
        public static readonly Color TextMuted = new Color(0.78f, 0.78f, 0.82f, 1f); // Texto secund�rio mais claro
        public static readonly Color TextDisabled = new Color(0.5f, 0.5f, 0.55f, 1f); // Texto para itens desabilitados

        // Cores de Estado e Feedback
        public static readonly Color Error = new Color(1f, 0.15f, 0.15f, 1f); // Cor para erros
        public static readonly Color Success = new Color(0.2f, 0.8f, 0.4f, 1f); // Cor para sucesso
        public static readonly Color Warning = new Color(0.9f, 0.7f, 0.2f, 1f); // Cor para avisos
    }

    #region Textura Helpers (Otimizados)
    // Texturas s�o geradas uma vez e reutilizadas, ou criadas sob demanda e armazenadas em cache.
    // Usando HideFlags.HideAndDontSave para evitar que apare�am no Hierarchy/Inspector do Unity.
    private static Texture2D _cachedPixelDarkTexture;
    private static Texture2D _cachedPixelAccentTexture;
    private static Texture2D _cachedPixelErrorTexture;

    /// <summary>
    /// Cria uma textura vertical gradiente.
    /// </summary>
    private static Texture2D MakeVerticalGradientTexture(int width, int height, Color top, Color bottom)
    {
        if (width < 1) width = 1;
        if (height < 2) height = 2;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color rowColor = Color.Lerp(top, bottom, t);
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = rowColor;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Cria uma textura com borda.
    /// </summary>
    private static Texture2D MakeFrameTexture(int width, int height, Color innerTop, Color innerBottom, Color border, int borderThickness)
    {
        if (width < borderThickness * 2 + 1) width = borderThickness * 2 + 1;
        if (height < borderThickness * 2 + 2) height = borderThickness * 2 + 2;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color inner = Color.Lerp(innerTop, innerBottom, t);
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < borderThickness || x >= width - borderThickness || y < borderThickness || y >= height - borderThickness;
                pixels[y * width + x] = isBorder ? border : inner;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Cria uma textura monocrom�tica de tamanho espec�fico.
    /// </summary>
    private static Texture2D MakeTexture(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear; // Bilinear para suavizar levemente bordas se necess�rio
        tex.hideFlags = HideFlags.HideAndDontSave;

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// Cria uma textura monocrom�tica 1x1, reutiliz�vel. Caching para as mais comuns.
    /// </summary>
    private static Texture2D MakeTexture(Color color)
    {
        // Caching para texturas muito comuns para evitar recria��o
        if (color == Theme.BgDarkB) { if (_cachedPixelDarkTexture == null) _cachedPixelDarkTexture = MakeTexture(1, 1, color); return _cachedPixelDarkTexture; }
        if (color == Theme.Accent) { if (_cachedPixelAccentTexture == null) _cachedPixelAccentTexture = MakeTexture(1, 1, color); return _cachedPixelAccentTexture; }
        if (color == Theme.Error) { if (_cachedPixelErrorTexture == null) _cachedPixelErrorTexture = MakeTexture(1, 1, color); return _cachedPixelErrorTexture; }

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.filterMode = FilterMode.Bilinear; // Bilinear para texturas de cor s�lida tamb�m pode suavizar visualmente
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.Apply();
        return texture;
    }

    /// <summary>
    /// Cria um RectOffset reutiliz�vel.
    /// </summary>
    private static RectOffset CreateRectOffset(int left, int right, int top, int bottom)
    {
        var offset = new RectOffset();
        offset.left = left;
        offset.right = right;
        offset.top = top;
        offset.bottom = bottom;
        return offset;
    }
    #endregion

    #region Estilos Privados (Lentamente Inicializados)
    // Estilos s�o inicializados sob demanda para evitar overhead na inicializa��o do jogo.
    private static GUIStyle _headerStyle;
    private static GUIStyle _subHeaderStyle;
    private static GUIStyle _buttonStyle;
    private static GUIStyle _toggleStyle;
    private static GUIStyle _sliderStyle;
    private static GUIStyle _labelStyle;
    private static GUIStyle _tabStyle;
    private static GUIStyle _selectedTabStyle;
    private static GUIStyle _containerStyle;
    private static GUIStyle _sectionStyle;
    private static GUIStyle _errorStyle;
    private static GUIStyle _iconStyle;
    private static GUIStyle _tooltipStyle;
    private static GUIStyle _statusIndicatorStyle;
    private static GUIStyle _glowStyle;
    private static GUIStyle _shadowStyle;
    private static GUIStyle _highlightStyle;
    private static GUIStyle _separatorStyle;
    private static GUIStyle _betterToggleStyle;
    private static GUIStyle _windowStyle;
    private static GUIStyle _headerBackgroundStyle;
    private static GUIStyle _titleLabelStyle;
    private static GUIStyle _titleBarButtonStyle;
    private static GUIStyle _textFieldStyle;
    #endregion

    #region Estilos P�blicos (Propriedades com Inicializa��o Pregui�osa)
    public static GUIStyle HeaderStyle
    {
        get
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18, // Aumentado
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.Accent }, // Mantido o acento
                    padding = CreateRectOffset(12, 12, 10, 10), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(8, 8, 4, 8) // Ajustado e usando CreateRectOffset
                };
                _headerStyle.richText = true;
            }
            return _headerStyle;
        }
    }

    public static GUIStyle SubHeaderStyle
    {
        get
        {
            if (_subHeaderStyle == null)
            {
                _subHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.TextMuted }, // Mantido
                    padding = CreateRectOffset(10, 10, 7, 7), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(8, 8, 2, 6) // Ajustado e usando CreateRectOffset
                };
                _subHeaderStyle.richText = true;
            }
            return _subHeaderStyle;
        }
    }

    public static GUIStyle ButtonStyle
    {
        get
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Theme.TextPrimary },
                    padding = CreateRectOffset(14, 14, 8, 8),
                    margin = CreateRectOffset(6, 6, 3, 3),
                    fixedHeight = 36
                };
                _buttonStyle.normal.background = MakeFrameTexture(16, 64, Theme.ButtonTop, Theme.ButtonBottom, Theme.AccentDim, 1);
                _buttonStyle.hover.background = MakeFrameTexture(16, 64, Theme.ButtonHoverTop, Theme.ButtonHoverBottom, Theme.AccentHover, 1);
                _buttonStyle.active.background = MakeFrameTexture(16, 64, Theme.ButtonActiveTop, Theme.ButtonActiveBottom, Theme.AccentActive, 1);
                _buttonStyle.focused.background = _buttonStyle.hover.background; // Adicionado
                _buttonStyle.richText = true;
            }
            else if (_buttonStyle.normal?.background == null || _buttonStyle.hover?.background == null || _buttonStyle.active?.background == null)
            {
                // Recupera��o de falha de inicializa��o, se necess�rio
                _buttonStyle.normal.background = MakeFrameTexture(16, 64, Theme.ButtonTop, Theme.ButtonBottom, Theme.AccentDim, 1);
                _buttonStyle.hover.background = MakeFrameTexture(16, 64, Theme.ButtonHoverTop, Theme.ButtonHoverBottom, Theme.AccentHover, 1);
                _buttonStyle.active.background = MakeFrameTexture(16, 64, Theme.ButtonActiveTop, Theme.ButtonActiveBottom, Theme.AccentActive, 1);
                _buttonStyle.focused.background = _buttonStyle.hover.background;
            }
            return _buttonStyle;
        }
    }

    public static GUIStyle ToggleStyle
    {
        get
        {
            if (_toggleStyle == null)
            {
                _toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 14, // Ajustado
                    fontStyle = FontStyle.Normal, // Removido Bold para consist�ncia visual
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.TextMuted, background = MakeTexture(new Color(0.09f, 0.09f, 0.11f, 0.95f)) }, // Fundo mais escuro
                    onNormal = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.18f, 0.09f, 0.02f, 0.95f)) }, // Orange when on
                    hover = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.11f, 0.11f, 0.14f, 0.95f)) }, // Fundo hover
                    onHover = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.22f, 0.11f, 0.03f, 0.95f)) }, // Orange hover
                    active = { textColor = Theme.TextMuted, background = MakeTexture(new Color(0.09f, 0.09f, 0.11f, 0.95f)) }, // Fundo ativo (ao clicar)
                    onActive = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.25f, 0.13f, 0.04f, 0.95f)) }, // Orange active
                    padding = CreateRectOffset(16, 16, 9, 9), // Better padding
                    margin = CreateRectOffset(6, 6, 4, 4), // Better margin
                    fixedHeight = 34, // Taller for fluid look
                    stretchWidth = true
                };
                _toggleStyle.richText = true;
            }
            else if (_toggleStyle.normal?.background == null || _toggleStyle.onNormal?.background == null || _toggleStyle.hover?.background == null || _toggleStyle.onHover?.background == null || _toggleStyle.active?.background == null || _toggleStyle.onActive?.background == null)
            {
                // Recupera��o de falha de inicializa��o
                _toggleStyle.normal.background = MakeTexture(new Color(0.09f, 0.09f, 0.11f, 0.95f));
                _toggleStyle.onNormal.background = MakeTexture(new Color(0.12f, 0.03f, 0.06f, 0.95f));
                _toggleStyle.hover.background = MakeTexture(new Color(0.11f, 0.11f, 0.14f, 0.95f));
                _toggleStyle.onHover.background = MakeTexture(new Color(0.14f, 0.04f, 0.08f, 0.95f));
                _toggleStyle.active.background = MakeTexture(new Color(0.09f, 0.09f, 0.11f, 0.95f));
                _toggleStyle.onActive.background = MakeTexture(new Color(0.15f, 0.05f, 0.10f, 0.95f));
            }
            return _toggleStyle;
        }
    }

    public static GUIStyle SliderStyle
    {
        get
        {
            if (_sliderStyle == null)
            {
                _sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
                {
                    margin = CreateRectOffset(12, 12, 10, 10), // Ajustado e usando CreateRectOffset
                    fixedHeight = 24 // Aumentado
                };
            }
            return _sliderStyle;
        }
    }

    public static GUIStyle LabelStyle
    {
        get
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Theme.TextPrimary },
                    padding = CreateRectOffset(10, 10, 6, 6), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(6, 6, 3, 5) // Ajustado e usando CreateRectOffset
                };
                _labelStyle.richText = true;
            }
            return _labelStyle;
        }
    }

    public static GUIStyle TabStyle
    {
        get
        {
            if (_tabStyle == null)
            {
                _tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    padding = CreateRectOffset(10, 10, 5, 5), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(3, 3, 2, 2), // Ajustado e usando CreateRectOffset
                    fixedHeight = 28, // Ajustado
                    normal = { textColor = Theme.TextMuted } // Cor mais clara para abas inativas
                };
                _tabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.09f, 0.09f, 0.11f, 0.95f), new Color(0.07f, 0.07f, 0.09f, 0.95f), Theme.AccentDim, 1);
                _tabStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.11f, 0.14f, 0.95f), new Color(0.09f, 0.09f, 0.12f, 0.95f), Theme.AccentHover, 1);
                _tabStyle.active.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.AccentActive, 1);
                _tabStyle.richText = true;
            }
            else if (_tabStyle.normal?.background == null || _tabStyle.hover?.background == null || _tabStyle.active?.background == null)
            {
                _tabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.09f, 0.09f, 0.11f, 0.95f), new Color(0.07f, 0.07f, 0.09f, 0.95f), Theme.AccentDim, 1);
                _tabStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.11f, 0.14f, 0.95f), new Color(0.09f, 0.09f, 0.12f, 0.95f), Theme.AccentHover, 1);
                _tabStyle.active.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.AccentActive, 1);
            }
            return _tabStyle;
        }
    }

    public static GUIStyle SelectedTabStyle
    {
        get
        {
            if (_selectedTabStyle == null)
            {
                _selectedTabStyle = new GUIStyle(TabStyle)
                {
                    normal = { textColor = Theme.Accent } // Cor acentuada para a aba selecionada
                };
                _selectedTabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.13f, 0.04f, 0.08f, 0.95f), new Color(0.11f, 0.03f, 0.07f, 0.95f), Theme.Accent, 1); // Borda mais forte
            }
            return _selectedTabStyle;
        }
    }

    public static GUIStyle ErrorStyle
    {
        get
        {
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Theme.Error },
                    padding = CreateRectOffset(12, 12, 10, 10), // Ajustado e usando CreateRectOffset
                    wordWrap = true
                };
                _errorStyle.richText = true;
            }
            return _errorStyle;
        }
    }

    public static GUIStyle ContainerStyle
    {
        get
        {
            if (_containerStyle == null)
            {
                _containerStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = CreateRectOffset(8, 8, 8, 8), // Reduzido de 12 para 8
                    margin = CreateRectOffset(4, 4, 4, 4) // Reduzido de 8 para 4
                };
                _containerStyle.normal.background = MakeTexture(2, 2, new Color(0.07f, 0.07f, 0.09f, 0.80f)); // Fundo mais claro e com mais transpar�ncia
            }
            else if (_containerStyle.normal?.background == null)
            {
                _containerStyle.normal.background = MakeTexture(2, 2, new Color(0.07f, 0.07f, 0.09f, 0.80f));
            }
            return _containerStyle;
        }
    }

    public static GUIStyle SectionStyle
    {
        get
        {
            if (_sectionStyle == null)
            {
                _sectionStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = CreateRectOffset(10, 10, 10, 10), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(4, 4, 6, 6) // Ajustado e usando CreateRectOffset
                };
                _sectionStyle.normal.background = MakeTexture(2, 2, Theme.BgSection); // Usa cor do tema
            }
            else if (_sectionStyle.normal?.background == null)
            {
                _sectionStyle.normal.background = MakeTexture(2, 2, Theme.BgSection);
            }
            return _sectionStyle;
        }
    }

    public static GUIStyle IconStyle
    {
        get
        {
            if (_iconStyle == null)
            {
                _iconStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = CreateRectOffset(3, 3, 3, 3), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(3, 3, 3, 3), // Ajustado e usando CreateRectOffset
                    fixedWidth = 28, // Ajustado
                    fixedHeight = 28 // Ajustado
                };
                _iconStyle.normal.background = MakeFrameTexture(8, 8, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentDim, 1);
            }
            else if (_iconStyle.normal?.background == null)
            {
                _iconStyle.normal.background = MakeFrameTexture(8, 8, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentDim, 1);
            }
            return _iconStyle;
        }
    }

    public static GUIStyle TooltipStyle
    {
        get
        {
            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    normal = { textColor = Theme.TextPrimary, background = MakeFrameTexture(16, 48, new Color(0.07f, 0.07f, 0.09f, 0.98f), new Color(0.05f, 0.05f, 0.07f, 0.98f), Theme.Accent, 1) }, // Fundo mais opaco e com borda acentuada
                    padding = CreateRectOffset(8, 8, 8, 8), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(6, 6, 6, 6), // Ajustado e usando CreateRectOffset
                    wordWrap = true
                };
                _tooltipStyle.richText = true;
            }
            else if (_tooltipStyle.normal?.background == null)
            {
                _tooltipStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.07f, 0.07f, 0.09f, 0.98f), new Color(0.05f, 0.05f, 0.07f, 0.98f), Theme.Accent, 1);
            }
            return _tooltipStyle;
        }
    }

    public static GUIStyle StatusIndicatorStyle
    {
        get
        {
            if (_statusIndicatorStyle == null)
            {
                _statusIndicatorStyle = new GUIStyle(GUI.skin.box)
                {
                    fixedWidth = 12, // Ajustado
                    fixedHeight = 12, // Ajustado
                    margin = CreateRectOffset(6, 6, 4, 4) // Ajustado e usando CreateRectOffset
                };
            }
            return _statusIndicatorStyle;
        }
    }

    public static GUIStyle GlowStyle
    {
        get
        {
            if (_glowStyle == null)
            {
                _glowStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(1f, 0f, 0.2f, 0.15f)) }, // Ajustado alpha
                    margin = CreateRectOffset(3, 3, 3, 3) // Ajustado e usando CreateRectOffset
                };
            }
            return _glowStyle;
        }
    }

    public static GUIStyle ShadowStyle
    {
        get
        {
            if (_shadowStyle == null)
            {
                _shadowStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.6f)) }, // Ajustado alpha
                    margin = CreateRectOffset(3, 3, 3, 3) // Ajustado e usando CreateRectOffset
                };
            }
            return _shadowStyle;
        }
    }

    public static GUIStyle HighlightStyle
    {
        get
        {
            if (_highlightStyle == null)
            {
                _highlightStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(1f, 1f, 1f, 0.12f)) }, // Ajustado alpha
                    margin = CreateRectOffset(3, 3, 3, 3) // Ajustado e usando CreateRectOffset
                };
            }
            return _highlightStyle;
        }
    }

    public static GUIStyle SeparatorStyle
    {
        get
        {
            if (_separatorStyle == null)
            {
                _separatorStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(1, 1, Theme.Accent) }, // Usa cor do tema
                    margin = CreateRectOffset(8, 8, 6, 6), // Ajustado e usando CreateRectOffset
                    fixedHeight = 2 // Ajustado
                };
            }
            return _separatorStyle;
        }
    }

    public static GUIStyle BetterToggleStyle
    {
        get
        {
            if (_betterToggleStyle == null)
            {
                _betterToggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 15, // Ajustado
                    fontStyle = FontStyle.Normal, // Removido Bold
                    alignment = TextAnchor.MiddleLeft,
                    padding = CreateRectOffset(18, 18, 11, 11), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(10, 10, 8, 8), // Ajustado e usando CreateRectOffset
                    fixedHeight = 40, // Ajustado
                    stretchWidth = true
                };
                _betterToggleStyle.normal.background = MakeFrameTexture(16, 64, new Color(0.10f, 0.10f, 0.12f, 0.95f), new Color(0.08f, 0.08f, 0.10f, 0.95f), Theme.AccentDim, 1);
                _betterToggleStyle.onNormal.background = MakeFrameTexture(16, 64, new Color(0.15f, 0.03f, 0.07f, 0.95f), new Color(0.12f, 0.02f, 0.05f, 0.95f), Theme.Accent, 1); // Fundo mais escuro, borda acentuada
                _betterToggleStyle.normal.textColor = Theme.TextMuted; // Cor mais clara quando desligado
                _betterToggleStyle.onNormal.textColor = Theme.TextPrimary; // Cor principal quando ligado
                _betterToggleStyle.hover.background = MakeFrameTexture(16, 64, new Color(0.12f, 0.12f, 0.15f, 0.95f), new Color(0.10f, 0.10f, 0.13f, 0.95f), Theme.AccentHover, 1);
                _betterToggleStyle.onHover.background = MakeFrameTexture(16, 64, new Color(0.17f, 0.04f, 0.09f, 0.95f), new Color(0.14f, 0.03f, 0.07f, 0.95f), Theme.AccentHover, 1);
                _betterToggleStyle.active.background = MakeFrameTexture(16, 64, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentActive, 1);
                _betterToggleStyle.onActive.background = MakeFrameTexture(16, 64, new Color(0.19f, 0.05f, 0.11f, 0.95f), new Color(0.16f, 0.04f, 0.09f, 0.95f), Theme.AccentActive, 1);
                _betterToggleStyle.active.textColor = Theme.TextMuted;
                _betterToggleStyle.onActive.textColor = Theme.TextPrimary;
            }
            return _betterToggleStyle;
        }
    }

    public static GUIStyle WindowStyle
    {
        get
        {
            if (_windowStyle == null)
            {
                _windowStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = CreateRectOffset(10, 10, 10, 10), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(2, 2, 2, 2) // Ajustado e usando CreateRectOffset
                };
                var top = new Color(Theme.BgDarkA.r, Theme.BgDarkA.g, Theme.BgDarkA.b, 0.80f); // Ajustado alpha
                var bottom = new Color(Theme.BgDarkB.r, Theme.BgDarkB.g, Theme.BgDarkB.b, 0.80f); // Ajustado alpha
                _windowStyle.normal.background = MakeVerticalGradientTexture(2, 128, top, bottom);
            }
            else if (_windowStyle.normal?.background == null)
            {
                var top = new Color(Theme.BgDarkA.r, Theme.BgDarkA.g, Theme.BgDarkA.b, 0.80f);
                var bottom = new Color(Theme.BgDarkB.r, Theme.BgDarkB.g, Theme.BgDarkB.b, 0.80f);
                _windowStyle.normal.background = MakeVerticalGradientTexture(2, 128, top, bottom);
            }
            return _windowStyle;
        }
    }

    public static GUIStyle HeaderBackgroundStyle
    {
        get
        {
            if (_headerBackgroundStyle == null)
            {
                _headerBackgroundStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = CreateRectOffset(0, 0, 0, 0), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(0, 0, 0, 0) // Ajustado e usando CreateRectOffset
                };
                var hTop = new Color(Theme.HeaderTop.r, Theme.HeaderTop.g, Theme.HeaderTop.b, 0.80f); // Ajustado alpha
                var hBottom = new Color(Theme.HeaderBottom.r, Theme.HeaderBottom.g, Theme.HeaderBottom.b, 0.80f); // Ajustado alpha
                _headerBackgroundStyle.normal.background = MakeVerticalGradientTexture(2, 32, hTop, hBottom);
            }
            else if (_headerBackgroundStyle.normal?.background == null)
            {
                var hTop = new Color(Theme.HeaderTop.r, Theme.HeaderTop.g, Theme.HeaderTop.b, 0.80f);
                var hBottom = new Color(Theme.HeaderBottom.r, Theme.HeaderBottom.g, Theme.HeaderBottom.b, 0.80f);
                _headerBackgroundStyle.normal.background = MakeVerticalGradientTexture(2, 32, hTop, hBottom);
            }
            return _headerBackgroundStyle;
        }
    }

    public static GUIStyle TitleLabelStyle
    {
        get
        {
            if (_titleLabelStyle == null)
            {
                _titleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15, // Ajustado
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Theme.TextPrimary } // Mantido branco
                };
            }
            return _titleLabelStyle;
        }
    }

    public static GUIStyle TitleBarButtonStyle
    {
        get
        {
            if (_titleBarButtonStyle == null)
            {
                _titleBarButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13, // Ajustado
                    alignment = TextAnchor.MiddleCenter,
                    padding = CreateRectOffset(2, 2, 2, 2), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(4, 4, 4, 4), // Ajustado e usando CreateRectOffset
                    fixedWidth = 24, // Ajustado
                    fixedHeight = 20 // Ajustado
                };
                _titleBarButtonStyle.normal.textColor = Theme.TextPrimary;
                _titleBarButtonStyle.normal.background = MakeFrameTexture(8, 32, new Color(0.13f, 0.13f, 0.15f, 0.95f), new Color(0.11f, 0.11f, 0.13f, 0.95f), Theme.AccentDim, 1);
                _titleBarButtonStyle.hover.background = MakeFrameTexture(8, 32, new Color(0.15f, 0.05f, 0.10f, 0.95f), new Color(0.13f, 0.04f, 0.08f, 0.95f), Theme.AccentHover, 1);
                _titleBarButtonStyle.active.background = MakeFrameTexture(8, 32, new Color(0.17f, 0.06f, 0.12f, 0.95f), new Color(0.15f, 0.05f, 0.10f, 0.95f), Theme.AccentActive, 1);
            }
            else if (_titleBarButtonStyle.normal?.background == null || _titleBarButtonStyle.hover?.background == null || _titleBarButtonStyle.active?.background == null)
            {
                _titleBarButtonStyle.normal.background = MakeFrameTexture(8, 32, new Color(0.13f, 0.13f, 0.15f, 0.95f), new Color(0.11f, 0.11f, 0.13f, 0.95f), Theme.AccentDim, 1);
                _titleBarButtonStyle.hover.background = MakeFrameTexture(8, 32, new Color(0.15f, 0.05f, 0.10f, 0.95f), new Color(0.13f, 0.04f, 0.08f, 0.95f), Theme.AccentHover, 1);
                _titleBarButtonStyle.active.background = MakeFrameTexture(8, 32, new Color(0.17f, 0.06f, 0.12f, 0.95f), new Color(0.15f, 0.05f, 0.10f, 0.95f), Theme.AccentActive, 1);
            }
            return _titleBarButtonStyle;
        }
    }

    public static GUIStyle TextFieldStyle
    {
        get
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    padding = CreateRectOffset(10, 10, 8, 8), // Ajustado e usando CreateRectOffset
                    margin = CreateRectOffset(6, 6, 6, 8) // Ajustado e usando CreateRectOffset
                };
                _textFieldStyle.normal.textColor = Theme.TextPrimary;
                _textFieldStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.08f, 0.08f, 0.10f, 0.95f), new Color(0.06f, 0.06f, 0.08f, 0.95f), Theme.AccentDim, 1);
                _textFieldStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.10f, 0.10f, 0.12f, 0.95f), new Color(0.08f, 0.08f, 0.10f, 0.95f), Theme.AccentHover, 1);
                _textFieldStyle.focused.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.02f, 0.05f, 0.95f), new Color(0.09f, 0.01f, 0.04f, 0.95f), Theme.Accent, 1);
                _textFieldStyle.richText = true;
            }
            else if (_textFieldStyle.normal?.background == null || _textFieldStyle.hover?.background == null || _textFieldStyle.focused?.background == null)
            {
                _textFieldStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.08f, 0.08f, 0.10f, 0.95f), new Color(0.06f, 0.06f, 0.08f, 0.95f), Theme.AccentDim, 1);
                _textFieldStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.10f, 0.10f, 0.12f, 0.95f), new Color(0.08f, 0.08f, 0.10f, 0.95f), Theme.AccentHover, 1);
                _textFieldStyle.focused.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.02f, 0.05f, 0.95f), new Color(0.09f, 0.01f, 0.04f, 0.95f), Theme.Accent, 1);
            }
            return _textFieldStyle;
        }
    }
    #endregion

    #region Fun��es P�blicas de Utilidade
    /// <summary>
    /// Garante que todos os estilos sejam inicializados. Chamado uma vez, idealmente na inicializa��o do mod.
    /// </summary>
    public static void EnsureInitialized()
    {
        // Acessa cada estilo uma vez para for�ar a inicializa��o pregui�osa.
        _ = WindowStyle;
        _ = HeaderBackgroundStyle;
        _ = TitleLabelStyle;
        _ = TitleBarButtonStyle;
        _ = HeaderStyle;
        _ = SubHeaderStyle;
        _ = ButtonStyle;
        _ = ToggleStyle;
        _ = SliderStyle;
        _ = LabelStyle;
        _ = TabStyle;
        _ = SelectedTabStyle;
        _ = ContainerStyle;
        _ = SectionStyle;
        _ = ErrorStyle;
        _ = IconStyle;
        _ = TooltipStyle;
        _ = StatusIndicatorStyle;
        _ = GlowStyle;
        _ = ShadowStyle;
        _ = HighlightStyle;
        _ = SeparatorStyle;
        _ = BetterToggleStyle;
        _ = TextFieldStyle;
    }

    /// <summary>
    /// Desenha um tooltip na posi��o do mouse se o ret�ngulo fornecido cont�m o mouse.
    /// </summary>
    public static void DrawTooltip(string tooltip, Rect rect)
    {
        if (!string.IsNullOrEmpty(tooltip) && rect.Contains(Event.current.mousePosition))
        {
            Vector2 size = GUI.skin.box.CalcSize(new GUIContent(tooltip));
            Rect tooltipRect = new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y, size.x + 16, size.y + 12); // Ajustado offset e padding
            GUI.Label(tooltipRect, tooltip, TooltipStyle);
        }
    }

    /// <summary>
    /// Desenha um indicador de status (c�rculo colorido).
    /// </summary>
    public static void DrawStatusIndicator(bool isActive)
    {
        Color color = isActive ? Theme.Success : Theme.Error; // Usa cores do tema
        GUIStyle style = new GUIStyle(StatusIndicatorStyle);
        style.normal.background = MakeTexture(1, 1, color);
        GUILayout.Box(GUIContent.none, style);
    }

    /// <summary>
    /// Desenha um separador horizontal.
    /// </summary>
    public static void DrawSeparator()
    {
        GUILayout.Box(GUIContent.none, SeparatorStyle, GUILayout.ExpandWidth(true), GUILayout.Height(2)); // Ajustado altura
    }

    /// <summary>
    /// Desenha um bot�o de aba estilizado.
    /// </summary>
    public static bool DrawTab(string label, bool selected)
    {
        return GUILayout.Toggle(selected, label, selected ? SelectedTabStyle : TabStyle);
    }

    /// <summary>
    /// Desenha um toggle com tooltip opcional.
    /// </summary>
    public static bool DrawBetterToggle(bool value, string label, string tooltip = null)
    {
        bool result = GUILayout.Toggle(value, label, BetterToggleStyle);
        Rect lastRect = GUILayoutUtility.GetLastRect();
        if (!string.IsNullOrEmpty(tooltip))
            DrawTooltip(tooltip, lastRect);
        return result;
    }

    /// <summary>
    /// Gera o texto do cabe�alho com o sufixo de hora atual.
    /// </summary>
    public static string GetHeaderText(string text)
    {
        return $"{text} - {DateTime.Now:HH:mm:ss}";
    }

    /// <summary>
    /// Retorna um estilo de cabe�alho com cor animada (varia��o do acento).
    /// </summary>
    public static GUIStyle GetAnimatedHeaderStyle()
    {
        var style = new GUIStyle(HeaderStyle);
        style.normal.textColor = Color.Lerp(Theme.Accent, Theme.AccentSoft, Mathf.PingPong(Time.time * 2f, 1f)); // Anima��o de cor
        return style;
    }

    /// <summary>
    /// Retorna a hora atual formatada.
    /// </summary>
    public static string GetCurrentTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }
    #endregion
}