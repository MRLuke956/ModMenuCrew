using System;
using ModMenuCrew.UI.Styles;
using UnityEngine;

namespace ModMenuCrew.UI.Controls;

public class MenuSection
{
    private readonly string title;
    private readonly Action drawContent;
    private bool isExpanded = true;

    public MenuSection(string title, Action drawContent)
    {
        this.title = title;
        this.drawContent = drawContent;
    }

    public void Draw()
    {
        GUILayout.BeginVertical(GuiStyles.SectionStyle);

        // Cabeçalho moderno com gradiente e botão
        Rect headerRect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
        GUI.Box(headerRect, GUIContent.none, GuiStyles.HeaderBackgroundStyle);

        // Título centralizado com leve folga nas laterais
        Rect titleRect = new Rect(headerRect.x + 8, headerRect.y, headerRect.width - 56, headerRect.height);
        GUI.Label(titleRect, title, GuiStyles.TitleLabelStyle);

        // Botão expandir/recolher no canto direito
        Rect buttonRect = new Rect(headerRect.xMax - 28, headerRect.y + 5, 20, headerRect.height - 10);
        if (GUI.Button(buttonRect, isExpanded ? "▾" : "▸", GuiStyles.TitleBarButtonStyle))
        {
            isExpanded = !isExpanded;
        }

        if (isExpanded)
        {
            // Conteúdo com container estilizado
            GUILayout.BeginVertical(GuiStyles.ContainerStyle);
            drawContent?.Invoke();
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }
}