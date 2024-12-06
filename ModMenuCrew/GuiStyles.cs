using UnityEngine;

namespace ModMenuCrew.UI.Styles;

public static class GuiStyles
{
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
                    normal = { textColor = new Color(1f, 0.8f, 0f) },
                    padding = { left = 5, right = 5, top = 8, bottom = 8 },
                    margin = { bottom = 10 }
                };
            }
            return headerStyle;
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
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
                    padding = { left = 5, right = 5, top = 5, bottom = 5 },
                    margin = { bottom = 5 }
                };
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
                    fontSize = 13,
                    normal = { textColor = Color.white },
                    padding = { left = 15, right = 15, top = 8, bottom = 8 },
                    margin = { left = 5, right = 5, top = 5, bottom = 5 },
                    fixedHeight = 35
                };
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
                    fontSize = 13,
                    padding = { left = 10, right = 10, top = 8, bottom = 8 },
                    margin = { left = 5, right = 5, top = 3, bottom = 3 },
                    fixedHeight = 30
                };
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
                    margin = { left = 10, right = 10, top = 10, bottom = 10 },
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
                    fontSize = 13,
                    normal = { textColor = Color.white },
                    padding = { left = 5, right = 5, top = 5, bottom = 5 },
                    margin = { bottom = 5 }
                };
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
                    padding = { left = 15, right = 15, top = 8, bottom = 8 },
                    margin = { left = 2, right = 2 },
                    fixedHeight = 35,
                    normal = { textColor = Color.white }
                };
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
                    normal = { textColor = new Color(1f, 0.8f, 0f) }
                };
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
                    normal = { textColor = Color.red },
                    padding = { left = 10, right = 10, top = 10, bottom = 10 },
                    wordWrap = true
                };
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
                    padding = { left = 10, right = 10, top = 10, bottom = 10 },
                    margin = { left = 5, right = 5, top = 5, bottom = 5 }
                };
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
                    padding = { left = 10, right = 10, top = 10, bottom = 10 },
                    margin = { left = 0, right = 0, top = 5, bottom = 5 }
                };
            }
            return sectionStyle;
        }
    }
}