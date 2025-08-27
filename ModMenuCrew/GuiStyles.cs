using System;
using UnityEngine;

namespace ModMenuCrew.UI.Styles;

public static class GuiStyles
{
    public static class Theme
    {
        public static readonly Color BgDarkA = new Color(0.07f, 0.07f, 0.07f, 0.92f);
        public static readonly Color BgDarkB = new Color(0.06f, 0.06f, 0.06f, 0.92f);
        public static readonly Color HeaderTop = new Color(0.10f, 0.02f, 0.04f, 0.95f);
        public static readonly Color HeaderBottom = new Color(0.06f, 0.01f, 0.03f, 0.95f);
        public static readonly Color Accent = new Color(1f, 0f, 0.2f, 1f);
        public static readonly Color AccentSoft = new Color(1f, 0.18f, 0.36f, 1f);
        public static readonly Color AccentDim = new Color(0.65f, 0f, 0.13f, 1f);
        public static readonly Color ButtonTop = new Color(0.11f, 0.11f, 0.12f, 0.95f);
        public static readonly Color ButtonBottom = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        public static readonly Color ButtonHoverTop = new Color(0.13f, 0.13f, 0.16f, 0.95f);
        public static readonly Color ButtonHoverBottom = new Color(0.10f, 0.10f, 0.13f, 0.95f);
        public static readonly Color ButtonActiveTop = new Color(0.12f, 0.02f, 0.05f, 0.95f);
        public static readonly Color ButtonActiveBottom = new Color(0.09f, 0.02f, 0.04f, 0.95f);
        public static readonly Color TextPrimary = new Color(0.96f, 0.96f, 0.98f, 1f);
        public static readonly Color TextMuted = new Color(0.78f, 0.78f, 0.82f, 1f);
        public static readonly Color Error = new Color(1f, 0.15f, 0.15f, 1f);
    }

    private static Texture2D MakeVerticalGradientTexture(int width, int height, Color top, Color bottom)
    {
        if (width < 1) width = 1;
        if (height < 2) height = 2;
        var texture = new Texture2D(width, height);
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            var row = Color.Lerp(top, bottom, t);
            for (int x = 0; x < width; x++) texture.SetPixel(x, y, row);
        }
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeFrameTexture(int width, int height, Color innerTop, Color innerBottom, Color border, int borderThickness)
    {
        if (width < borderThickness * 2 + 1) width = borderThickness * 2 + 1;
        if (height < borderThickness * 2 + 2) height = borderThickness * 2 + 2;
        var tex = new Texture2D(width, height);
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color inner = Color.Lerp(innerTop, innerBottom, t);
            for (int x = 0; x < width; x++)
            {
                bool isBorder = x < borderThickness || x >= width - borderThickness || y < borderThickness || y >= height - borderThickness;
                tex.SetPixel(x, y, isBorder ? border : inner);
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeTexture(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) tex.SetPixel(x, y, color);
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.Apply();
        return texture;
    }

    private static GUIStyle headerStyle;
    private static GUIStyle subHeaderStyle;
    private static GUIStyle buttonStyle;
    private static GUIStyle toggleStyle;
    private static GUIStyle sliderStyle;
    private static GUIStyle labelStyle;
    private static GUIStyle tabStyle;
    private static GUIStyle selectedTabStyle;
    private static GUIStyle containerStyle;
    private static GUIStyle sectionStyle;
    private static GUIStyle errorStyle;
    private static GUIStyle iconStyle;
    private static GUIStyle tooltipStyle;
    private static GUIStyle statusIndicatorStyle;
    private static GUIStyle glowStyle;
    private static GUIStyle shadowStyle;
    private static GUIStyle highlightStyle;
    private static GUIStyle separatorStyle;
    private static GUIStyle betterToggleStyle;
    private static GUIStyle windowStyle;
    private static GUIStyle headerBackgroundStyle;
    private static GUIStyle titleLabelStyle;
    private static GUIStyle titleBarButtonStyle;
    private static GUIStyle textFieldStyle;

    public static GUIStyle HeaderStyle
    {
        get
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.Accent },
                    padding = { left = 8, right = 8, top = 9, bottom = 9 },
                    margin = { left = 5, right = 5, top = 2, bottom = 7 }
                };
                headerStyle.richText = true;
            }
            return headerStyle;
        }
    }

    public static string GetHeaderText(string text)
    {
        return $"{text} - {DateTime.Now:HH:mm:ss}";
    }

    public static GUIStyle GetAnimatedHeaderStyle()
    {
        var style = new GUIStyle(HeaderStyle);
        style.normal.textColor = Color.Lerp(Theme.Accent, Theme.AccentSoft, 0.25f);
        return style;
    }

    public static string GetCurrentTime()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    public static GUIStyle TextFieldStyle
    {
        get
        {
            if (textFieldStyle == null)
            {
                textFieldStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    padding = { left = 8, right = 8, top = 7, bottom = 7 },
                    margin = { left = 5, right = 5, top = 4, bottom = 7 }
                };
                textFieldStyle.normal.textColor = Theme.TextPrimary;
                textFieldStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.09f, 0.09f, 0.10f, 0.95f), new Color(0.07f, 0.07f, 0.09f, 0.95f), Theme.AccentDim, 1);
                textFieldStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.Accent, 1);
                textFieldStyle.focused.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.Accent, 1);
                textFieldStyle.richText = true;
            }
            else if (textFieldStyle.normal?.background == null || textFieldStyle.hover?.background == null || textFieldStyle.focused?.background == null)
            {
                textFieldStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.09f, 0.09f, 0.10f, 0.95f), new Color(0.07f, 0.07f, 0.09f, 0.95f), Theme.AccentDim, 1);
                textFieldStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.Accent, 1);
                textFieldStyle.focused.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.Accent, 1);
            }
            return textFieldStyle;
        }
    }

    public static GUIStyle SubHeaderStyle
    {
        get
        {
            if (subHeaderStyle == null)
            {
                subHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.TextMuted },
                    padding = { left = 8, right = 8, top = 6, bottom = 6 },
                    margin = { left = 5, right = 5, top = 0, bottom = 5 }
                };
                subHeaderStyle.richText = true;
            }
            return subHeaderStyle;
        }
    }

    public static GUIStyle ButtonStyle
    {
        get
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Theme.TextPrimary },
                    padding = { left = 8, right = 8, top = 5, bottom = 5 },
                    margin = { left = 3, right = 3, top = 3, bottom = 3 },
                    fixedHeight = 28
                };
                buttonStyle.normal.background = MakeFrameTexture(16, 64, Theme.ButtonTop, Theme.ButtonBottom, Theme.AccentDim, 1);
                buttonStyle.hover.background = MakeFrameTexture(16, 64, Theme.ButtonHoverTop, Theme.ButtonHoverBottom, Theme.Accent, 1);
                buttonStyle.active.background = MakeFrameTexture(16, 64, Theme.ButtonActiveTop, Theme.ButtonActiveBottom, Theme.Accent, 1);
                buttonStyle.focused.background = buttonStyle.hover.background;
                buttonStyle.richText = true;
            }
            else if (buttonStyle.normal?.background == null || buttonStyle.hover?.background == null || buttonStyle.active?.background == null)
            {
                buttonStyle.normal.background = MakeFrameTexture(16, 64, Theme.ButtonTop, Theme.ButtonBottom, Theme.AccentDim, 1);
                buttonStyle.hover.background = MakeFrameTexture(16, 64, Theme.ButtonHoverTop, Theme.ButtonHoverBottom, Theme.Accent, 1);
                buttonStyle.active.background = MakeFrameTexture(16, 64, Theme.ButtonActiveTop, Theme.ButtonActiveBottom, Theme.Accent, 1);
                buttonStyle.focused.background = buttonStyle.hover.background;
            }
            return buttonStyle;
        }
    }

    public static GUIStyle ToggleStyle
    {
        get
        {
            if (toggleStyle == null)
            {
                toggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.10f, 0.10f, 0.12f, 0.95f)) },
                    onNormal = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.12f, 0.03f, 0.06f, 0.95f)) },
                    hover = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.13f, 0.13f, 0.16f, 0.95f)) },
                    onHover = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.14f, 0.04f, 0.08f, 0.95f)) },
                    active = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.10f, 0.02f, 0.05f, 0.95f)) },
                    onActive = { textColor = Theme.TextPrimary, background = MakeTexture(new Color(0.15f, 0.05f, 0.10f, 0.95f)) },
                    padding = { left = 10, right = 10, top = 6, bottom = 6 },
                    margin = { left = 4, right = 4, top = 2, bottom = 2 },
                    fixedHeight = 26,
                    stretchWidth = true
                };
                toggleStyle.richText = true;
            }
            else if (toggleStyle.normal?.background == null || toggleStyle.onNormal?.background == null || toggleStyle.hover?.background == null || toggleStyle.onHover?.background == null || toggleStyle.active?.background == null || toggleStyle.onActive?.background == null)
            {
                toggleStyle.normal.background = MakeTexture(new Color(0.10f, 0.10f, 0.12f, 0.95f));
                toggleStyle.onNormal.background = MakeTexture(new Color(0.12f, 0.03f, 0.06f, 0.95f));
                toggleStyle.hover.background = MakeTexture(new Color(0.13f, 0.13f, 0.16f, 0.95f));
                toggleStyle.onHover.background = MakeTexture(new Color(0.14f, 0.04f, 0.08f, 0.95f));
                toggleStyle.active.background = MakeTexture(new Color(0.10f, 0.02f, 0.05f, 0.95f));
                toggleStyle.onActive.background = MakeTexture(new Color(0.15f, 0.05f, 0.10f, 0.95f));
            }
            return toggleStyle;
        }
    }

    public static GUIStyle SliderStyle
    {
        get
        {
            if (sliderStyle == null)
            {
                sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
                {
                    margin = { left = 10, right = 10, top = 9, bottom = 9 },
                    fixedHeight = 20
                };
            }
            return sliderStyle;
        }
    }

    public static GUIStyle LabelStyle
    {
        get
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Theme.TextPrimary },
                    padding = { left = 8, right = 8, top = 5, bottom = 5 },
                    margin = { left = 5, right = 5, top = 2, bottom = 4 }
                };
                labelStyle.richText = true;
            }
            return labelStyle;
        }
    }

    public static GUIStyle TabStyle
    {
        get
        {
            if (tabStyle == null)
            {
                tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    padding = { left = 8, right = 8, top = 4, bottom = 4 },
                    margin = { left = 2, right = 2, top = 1, bottom = 1 },
                    fixedHeight = 24,
                    normal = { textColor = Theme.TextPrimary }
                };
                tabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.10f, 0.10f, 0.12f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentDim, 1);
                tabStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.12f, 0.15f, 0.95f), new Color(0.10f, 0.10f, 0.13f, 0.95f), Theme.Accent, 1);
                tabStyle.active.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.Accent, 1);
                tabStyle.richText = true;
            }
            else if (tabStyle.normal?.background == null || tabStyle.hover?.background == null || tabStyle.active?.background == null)
            {
                tabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.10f, 0.10f, 0.12f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentDim, 1);
                tabStyle.hover.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.12f, 0.15f, 0.95f), new Color(0.10f, 0.10f, 0.13f, 0.95f), Theme.Accent, 1);
                tabStyle.active.background = MakeFrameTexture(16, 48, new Color(0.12f, 0.03f, 0.06f, 0.95f), new Color(0.10f, 0.02f, 0.05f, 0.95f), Theme.Accent, 1);
            }
            return tabStyle;
        }
    }

    public static GUIStyle SelectedTabStyle
    {
        get
        {
            if (selectedTabStyle == null)
            {
                selectedTabStyle = new GUIStyle(TabStyle)
                {
                    normal = { textColor = Theme.Accent }
                };
                selectedTabStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.13f, 0.04f, 0.08f, 0.95f), new Color(0.11f, 0.03f, 0.07f, 0.95f), Theme.Accent, 1);
            }
            return selectedTabStyle;
        }
    }

    public static GUIStyle ErrorStyle
    {
        get
        {
            if (errorStyle == null)
            {
                errorStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Theme.Error },
                    padding = { left = 10, right = 10, top = 8, bottom = 8 },
                    wordWrap = true
                };
                errorStyle.richText = true;
            }
            return errorStyle;
        }
    }

    public static GUIStyle ContainerStyle
    {
        get
        {
            if (containerStyle == null)
            {
                containerStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = { left = 11, right = 11, top = 11, bottom = 11 },
                    margin = { left = 6, right = 6, top = 6, bottom = 6 }
                };
                containerStyle.normal.background = MakeTexture(2, 2, new Color(0.08f, 0.08f, 0.10f, 0.75f));
            }
            else if (containerStyle.normal?.background == null)
            {
                containerStyle.normal.background = MakeTexture(2, 2, new Color(0.08f, 0.08f, 0.10f, 0.75f));
            }
            return containerStyle;
        }
    }

    public static GUIStyle SectionStyle
    {
        get
        {
            if (sectionStyle == null)
            {
                sectionStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = { left = 9, right = 9, top = 9, bottom = 9 },
                    margin = { left = 3, right = 3, top = 4, bottom = 4 }
                };
                sectionStyle.normal.background = MakeTexture(2, 2, new Color(0.06f, 0.06f, 0.08f, 0.75f));
            }
            else if (sectionStyle.normal?.background == null)
            {
                sectionStyle.normal.background = MakeTexture(2, 2, new Color(0.06f, 0.06f, 0.08f, 0.75f));
            }
            return sectionStyle;
        }
    }

    public static GUIStyle IconStyle
    {
        get
        {
            if (iconStyle == null)
            {
                iconStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = { left = 2, right = 2, top = 2, bottom = 2 },
                    margin = { left = 2, right = 2, top = 2, bottom = 2 },
                    fixedWidth = 24,
                    fixedHeight = 24
                };
                iconStyle.normal.background = MakeFrameTexture(8, 8, new Color(0.12f, 0.12f, 0.14f, 0.95f), new Color(0.10f, 0.10f, 0.12f, 0.95f), Theme.AccentDim, 1);
            }
            else if (iconStyle.normal?.background == null)
            {
                iconStyle.normal.background = MakeFrameTexture(8, 8, new Color(0.12f, 0.12f, 0.14f, 0.95f), new Color(0.10f, 0.10f, 0.12f, 0.95f), Theme.AccentDim, 1);
            }
            return iconStyle;
        }
    }

    public static GUIStyle TooltipStyle
    {
        get
        {
            if (tooltipStyle == null)
            {
                tooltipStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 12,
                    normal = { textColor = Theme.TextPrimary, background = MakeFrameTexture(16, 48, new Color(0.06f, 0.06f, 0.08f, 0.95f), new Color(0.05f, 0.05f, 0.07f, 0.95f), Theme.Accent, 1) },
                    padding = { left = 6, right = 6, top = 6, bottom = 6 },
                    margin = { left = 4, right = 4, top = 4, bottom = 4 },
                    wordWrap = true
                };
                tooltipStyle.richText = true;
            }
            else if (tooltipStyle.normal?.background == null)
            {
                tooltipStyle.normal.background = MakeFrameTexture(16, 48, new Color(0.06f, 0.06f, 0.08f, 0.95f), new Color(0.05f, 0.05f, 0.07f, 0.95f), Theme.Accent, 1);
            }
            return tooltipStyle;
        }
    }

    public static GUIStyle StatusIndicatorStyle
    {
        get
        {
            if (statusIndicatorStyle == null)
            {
                statusIndicatorStyle = new GUIStyle(GUI.skin.box)
                {
                    fixedWidth = 10,
                    fixedHeight = 10,
                    margin = { left = 5, right = 5 }
                };
            }
            return statusIndicatorStyle;
        }
    }

    public static GUIStyle GlowStyle
    {
        get
        {
            if (glowStyle == null)
            {
                glowStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(1f, 0f, 0.2f, 0.10f)) },
                    margin = { left = 2, right = 2, top = 2, bottom = 2 }
                };
            }
            return glowStyle;
        }
    }

    public static GUIStyle ShadowStyle
    {
        get
        {
            if (shadowStyle == null)
            {
                shadowStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.5f)) },
                    margin = { left = 2, right = 2, top = 2, bottom = 2 }
                };
            }
            return shadowStyle;
        }
    }

    public static GUIStyle HighlightStyle
    {
        get
        {
            if (highlightStyle == null)
            {
                highlightStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(2, 2, new Color(1f, 1f, 1f, 0.08f)) },
                    margin = { left = 2, right = 2, top = 2, bottom = 2 }
                };
            }
            return highlightStyle;
        }
    }

    public static GUIStyle SeparatorStyle
    {
        get
        {
            if (separatorStyle == null)
            {
                separatorStyle = new GUIStyle(GUI.skin.box)
                {
                    normal = { background = MakeTexture(1, 1, new Color(1f, 0f, 0.2f, 0.9f)) },
                    margin = { left = 6, right = 6, top = 4, bottom = 4 },
                    fixedHeight = 1
                };
            }
            return separatorStyle;
        }
    }

    public static GUIStyle BetterToggleStyle
    {
        get
        {
            if (betterToggleStyle == null)
            {
                betterToggleStyle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = { left = 16, right = 16, top = 10, bottom = 10 },
                    margin = { left = 8, right = 8, top = 6, bottom = 6 },
                    fixedHeight = 36,
                    stretchWidth = true
                };
                betterToggleStyle.normal.background = MakeFrameTexture(16, 64, new Color(0.11f, 0.11f, 0.13f, 0.95f), new Color(0.09f, 0.09f, 0.11f, 0.95f), Theme.AccentDim, 1);
                betterToggleStyle.onNormal.background = MakeFrameTexture(16, 64, new Color(0.16f, 0.04f, 0.08f, 0.95f), new Color(0.13f, 0.03f, 0.06f, 0.95f), Theme.Accent, 1);
                betterToggleStyle.normal.textColor = Theme.TextPrimary;
                betterToggleStyle.onNormal.textColor = Theme.TextPrimary;
                betterToggleStyle.hover.background = MakeFrameTexture(16, 64, new Color(0.13f, 0.13f, 0.16f, 0.95f), new Color(0.11f, 0.11f, 0.14f, 0.95f), Theme.Accent, 1);
                betterToggleStyle.onHover.background = MakeFrameTexture(16, 64, new Color(0.18f, 0.05f, 0.10f, 0.95f), new Color(0.15f, 0.04f, 0.08f, 0.95f), Theme.Accent, 1);
                betterToggleStyle.active.background = MakeFrameTexture(16, 64, new Color(0.12f, 0.12f, 0.14f, 0.95f), new Color(0.10f, 0.10f, 0.12f, 0.95f), Theme.Accent, 1);
                betterToggleStyle.onActive.background = MakeFrameTexture(16, 64, new Color(0.20f, 0.06f, 0.12f, 0.95f), new Color(0.17f, 0.05f, 0.10f, 0.95f), Theme.Accent, 1);
                betterToggleStyle.active.textColor = Theme.TextPrimary;
                betterToggleStyle.onActive.textColor = Theme.TextPrimary;
            }
            return betterToggleStyle;
        }
    }

    public static GUIStyle WindowStyle
    {
        get
        {
            if (windowStyle == null)
            {
                windowStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = { left = 8, right = 8, top = 8, bottom = 8 },
                    margin = { left = 0, right = 0, top = 0, bottom = 0 }
                };
                var top = new Color(Theme.BgDarkA.r, Theme.BgDarkA.g, Theme.BgDarkA.b, 0.75f);
                var bottom = new Color(Theme.BgDarkB.r, Theme.BgDarkB.g, Theme.BgDarkB.b, 0.75f);
                windowStyle.normal.background = MakeVerticalGradientTexture(2, 128, top, bottom);
            }
            else if (windowStyle.normal?.background == null)
            {
                var top = new Color(Theme.BgDarkA.r, Theme.BgDarkA.g, Theme.BgDarkA.b, 0.75f);
                var bottom = new Color(Theme.BgDarkB.r, Theme.BgDarkB.g, Theme.BgDarkB.b, 0.75f);
                windowStyle.normal.background = MakeVerticalGradientTexture(2, 128, top, bottom);
            }
            return windowStyle;
        }
    }

    public static GUIStyle HeaderBackgroundStyle
    {
        get
        {
            if (headerBackgroundStyle == null)
            {
                headerBackgroundStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = { left = 0, right = 0, top = 0, bottom = 0 },
                    margin = { left = 0, right = 0, top = 0, bottom = 0 }
                };
                var hTop = new Color(Theme.HeaderTop.r, Theme.HeaderTop.g, Theme.HeaderTop.b, 0.75f);
                var hBottom = new Color(Theme.HeaderBottom.r, Theme.HeaderBottom.g, Theme.HeaderBottom.b, 0.75f);
                headerBackgroundStyle.normal.background = MakeVerticalGradientTexture(2, 32, hTop, hBottom);
            }
            else if (headerBackgroundStyle.normal?.background == null)
            {
                var hTop = new Color(Theme.HeaderTop.r, Theme.HeaderTop.g, Theme.HeaderTop.b, 0.75f);
                var hBottom = new Color(Theme.HeaderBottom.r, Theme.HeaderBottom.g, Theme.HeaderBottom.b, 0.75f);
                headerBackgroundStyle.normal.background = MakeVerticalGradientTexture(2, 32, hTop, hBottom);
            }
            return headerBackgroundStyle;
        }
    }

    public static GUIStyle TitleLabelStyle
    {
        get
        {
            if (titleLabelStyle == null)
            {
                titleLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.98f, 0.98f, 1f, 1f) }
                };
            }
            return titleLabelStyle;
        }
    }

    public static GUIStyle TitleBarButtonStyle
    {
        get
        {
            if (titleBarButtonStyle == null)
            {
                titleBarButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    padding = { left = 0, right = 0, top = 0, bottom = 0 },
                    margin = { left = 2, right = 2, top = 2, bottom = 2 },
                    fixedWidth = 20,
                    fixedHeight = 16
                };
                titleBarButtonStyle.normal.textColor = Theme.TextPrimary;
                titleBarButtonStyle.normal.background = MakeFrameTexture(8, 32, new Color(0.14f, 0.14f, 0.16f, 0.95f), new Color(0.12f, 0.12f, 0.14f, 0.95f), Theme.AccentDim, 1);
                titleBarButtonStyle.hover.background = MakeFrameTexture(8, 32, new Color(0.16f, 0.05f, 0.10f, 0.95f), new Color(0.14f, 0.04f, 0.08f, 0.95f), Theme.Accent, 1);
                titleBarButtonStyle.active.background = MakeFrameTexture(8, 32, new Color(0.18f, 0.06f, 0.12f, 0.95f), new Color(0.16f, 0.05f, 0.10f, 0.95f), Theme.Accent, 1);
            }
            else if (titleBarButtonStyle.normal?.background == null || titleBarButtonStyle.hover?.background == null || titleBarButtonStyle.active?.background == null)
            {
                titleBarButtonStyle.normal.background = MakeFrameTexture(8, 32, new Color(0.14f, 0.14f, 0.16f, 0.95f), new Color(0.12f, 0.12f, 0.14f, 0.95f), Theme.AccentDim, 1);
                titleBarButtonStyle.hover.background = MakeFrameTexture(8, 32, new Color(0.16f, 0.05f, 0.10f, 0.95f), new Color(0.14f, 0.04f, 0.08f, 0.95f), Theme.Accent, 1);
                titleBarButtonStyle.active.background = MakeFrameTexture(8, 32, new Color(0.18f, 0.06f, 0.12f, 0.95f), new Color(0.16f, 0.05f, 0.10f, 0.95f), Theme.Accent, 1);
            }
            return titleBarButtonStyle;
        }
    }

    public static void EnsureInitialized()
    {
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

    public static void DrawTooltip(string tooltip, Rect rect)
    {
        if (!string.IsNullOrEmpty(tooltip) && rect.Contains(Event.current.mousePosition))
        {
            Vector2 size = GUI.skin.box.CalcSize(new GUIContent(tooltip));
            Rect tooltipRect = new Rect(Event.current.mousePosition.x + 10, Event.current.mousePosition.y, size.x + 12, size.y + 10);
            GUI.Label(tooltipRect, tooltip, TooltipStyle);
        }
    }

    public static void DrawStatusIndicator(bool isActive)
    {
        Color color = isActive ? new Color(0.2f, 1f, 0.4f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
        GUIStyle style = new GUIStyle(StatusIndicatorStyle);
        style.normal.background = MakeTexture(1, 1, color);
        GUILayout.Box(GUIContent.none, style);
    }

    public static void DrawSeparator()
    {
        GUILayout.Box(GUIContent.none, SeparatorStyle, GUILayout.ExpandWidth(true), GUILayout.Height(1));
    }

    public static bool DrawTab(string label, bool selected)
    {
        return GUILayout.Toggle(selected, label, selected ? SelectedTabStyle : TabStyle);
    }

    public static bool DrawBetterToggle(bool value, string label, string tooltip = null)
    {
        bool result = GUILayout.Toggle(value, label, BetterToggleStyle);
        Rect lastRect = GUILayoutUtility.GetLastRect();
        if (!string.IsNullOrEmpty(tooltip))
            DrawTooltip(tooltip, lastRect);
        return result;
    }
}