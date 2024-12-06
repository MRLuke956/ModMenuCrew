using System;
using System.Collections.Generic;
using UnityEngine;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew.UI.Controls;

public class TabControl
{
    private readonly List<TabItem> tabs;
    private int selectedTab;

    public TabControl()
    {
        tabs = new List<TabItem>();
        selectedTab = 0;
    }

    public void AddTab(string name, Action drawContent)
    {
        tabs.Add(new TabItem(name, drawContent));
    }

    public void Draw()
    {
        if (tabs.Count == 0) return;

        GUILayout.BeginVertical(GuiStyles.ContainerStyle);
        try
        {
            DrawTabHeaders();
            GUILayout.Space(10);
            DrawSelectedTabContent();
        }
        finally
        {
            GUILayout.EndVertical();
        }
    }

    private void DrawTabHeaders()
    {
        GUILayout.BeginHorizontal();
        try
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                var style = i == selectedTab ? GuiStyles.SelectedTabStyle : GuiStyles.TabStyle;
                if (GUILayout.Button(tabs[i].Name, style))
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

    private void DrawSelectedTabContent()
    {
        if (selectedTab >= 0 && selectedTab < tabs.Count)
        {
            GUILayout.BeginVertical(GuiStyles.SectionStyle);
            try
            {
                tabs[selectedTab].DrawContent();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error drawing tab content: {ex}");
                GUILayout.Label($"Error drawing content for tab: {tabs[selectedTab].Name}", GuiStyles.ErrorStyle);
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }
    }

    public void ClearTabs()
    {
        tabs.Clear();
        selectedTab = 0;
    }

    public int GetSelectedTabIndex() => selectedTab;

    public void SetSelectedTab(int index)
    {
        if (index >= 0 && index < tabs.Count)
        {
            selectedTab = index;
        }
    }
}

public class TabItem
{
    public string Name { get; }
    private readonly Action drawContent;

    public TabItem(string name, Action drawContent)
    {
        Name = name;
        this.drawContent = drawContent;
    }

    public void DrawContent() => drawContent?.Invoke();
}