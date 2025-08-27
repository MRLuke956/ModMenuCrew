using System;
using System.Collections.Generic;
using ModMenuCrew.UI.Styles;
using UnityEngine;

namespace ModMenuCrew.UI.Controls;

public class TabControl
{
    private readonly List<TabItem> tabs;
    private int selectedTab;
    private Vector2 mousePosition;
    private string currentTooltip = string.Empty;
    private Rect tooltipRect;

    public TabControl()
    {
        tabs = new List<TabItem>();
        selectedTab = 0;
    }

    public void AddTab(string name, Action drawContent, string tooltip = "", Texture2D icon = null)
    {
        tabs.Add(new TabItem(name, drawContent, tooltip, icon));
    }

    public void Draw()
    {
        if (tabs.Count == 0) return;

        mousePosition = Event.current.mousePosition;
        currentTooltip = string.Empty;

        GUILayout.BeginVertical(GuiStyles.ContainerStyle, GUILayout.ExpandWidth(true));
        try
        {
            DrawTabHeaders();
            DrawSelectedTabContent();
        }
        finally
        {
            GUILayout.EndVertical();
        }

        // Desenhar tooltip fora do layout principal
        if (!string.IsNullOrEmpty(currentTooltip))
        {
            Vector2 size = GUI.skin.box.CalcSize(new GUIContent(currentTooltip));
            float padding = 10f;
            float width = Mathf.Min(size.x + padding, Mathf.Max(160f, Screen.width * 0.35f));
            float height = Mathf.Min(size.y + padding, Mathf.Max(40f, Screen.height * 0.25f));
            float x = Mathf.Clamp(mousePosition.x + 12f, 0, Screen.width - width);
            float y = Mathf.Clamp(mousePosition.y - 24f, 0, Screen.height - height);
            Rect rect = new Rect(x, y, width, height);
            GUI.Label(rect, currentTooltip, GuiStyles.TooltipStyle);
        }
    }

    private void DrawTabHeaders()
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
        try
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                var style = i == selectedTab ? GuiStyles.SelectedTabStyle : GuiStyles.TabStyle;
                if (DrawTabButton(tabs[i], style))
                {
                    selectedTab = i;
                }
            }
        }
        finally
        {
            GUILayout.EndHorizontal();
        }
    }

    private bool DrawTabButton(TabItem tab, GUIStyle style)
    {
        GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
        try
        {
            if (tab.Icon != null)
            {
                GUILayout.Box(tab.Icon, GuiStyles.IconStyle);
            }
            bool clicked = GUILayout.Button(tab.Name, style, GUILayout.ExpandWidth(false));
            // Verificar tooltip
            if (Event.current.type == EventType.Repaint)
            {
                Rect buttonRect = GUILayoutUtility.GetLastRect();
                if (buttonRect.Contains(mousePosition) && !string.IsNullOrEmpty(tab.Tooltip))
                {
                    currentTooltip = tab.Tooltip;
                    tooltipRect = new Rect(mousePosition.x, mousePosition.y - 30, 0, 0); // tamanho calculado no Draw()
                }
            }
            return clicked;
        }
        finally
        {
            GUILayout.EndHorizontal();
        }
    }

    private void DrawSelectedTabContent()
    {
        if (selectedTab >= 0 && selectedTab < tabs.Count)
        {
            tabs[selectedTab].DrawContent();
        }
    }

    public void ClearTabs()
    {
        tabs.Clear();
        selectedTab = 0;
    }

    public bool HasTab(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null && tabs[i].Name == name) return true;
        }
        return false;
    }

    public void RemoveTab(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null && tabs[i].Name == name)
            {
                tabs.RemoveAt(i);
                if (selectedTab >= tabs.Count) selectedTab = Math.Max(0, tabs.Count - 1);
                return;
            }
        }
    }

    public int GetSelectedTabIndex() => selectedTab;

    public void SetSelectedTab(int index)
    {
        if (index >= 0 && index < tabs.Count)
        {
            selectedTab = index;
        }
    }

    public void ReorderTabs(int fromIndex, int toIndex)
    {
        if (fromIndex >= 0 && fromIndex < tabs.Count && toIndex >= 0 && toIndex < tabs.Count)
        {
            var tab = tabs[fromIndex];
            tabs.RemoveAt(fromIndex);
            tabs.Insert(toIndex, tab);
            selectedTab = toIndex;
        }
    }
}

public class TabItem
{
    public string Name { get; }
    public Action DrawContent { get; }
    public string Tooltip { get; }
    public Texture2D Icon { get; }

    public TabItem(string name, Action drawContent, string tooltip = "", Texture2D icon = null)
    {
        Name = name;
        DrawContent = drawContent;
        Tooltip = tooltip;
        Icon = icon;
    }
}