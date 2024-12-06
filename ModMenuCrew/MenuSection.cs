using System;
using UnityEngine;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew.UI.Controls;

public class MenuSection
{
    private readonly string title;
    private readonly Action drawContent;

    public MenuSection(string title, Action drawContent)
    {
        this.title = title;
        this.drawContent = drawContent;
    }

    public void Draw()
    {
        GUILayout.Label(title, GuiStyles.HeaderStyle);
        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        drawContent?.Invoke();
        GUILayout.EndVertical();
        GUILayout.Space(10);
    }
}