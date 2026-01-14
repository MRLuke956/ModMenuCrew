using System;
using System.Collections.Generic;
using ModMenuCrew.UI.Styles;
using UnityEngine;

namespace ModMenuCrew.UI.Controls
{
    public class TabControl
    {
        // --- Estados e Dados ---
        private readonly List<TabItem> _tabs;
        private int _selectedTab;
        private Vector2 _mousePosition;
        private string _currentTooltip = string.Empty;
        private Rect _cachedTooltipRect; // Cache para a posição do tooltip

        // --- Cache de Retângulos (Opcional, para evitar alocações em OnGUI se necessário) ---
        private Rect _cachedContainerRect;

        public TabControl()
        {
            _tabs = new List<TabItem>();
            _selectedTab = 0;
        }

        public void AddTab(string name, Action drawContent, string tooltip = "", Texture2D icon = null)
        {
            _tabs.Add(new TabItem(name, drawContent, tooltip, icon));
        }

        public void Draw()
        {
            if (_tabs.Count == 0) return;

            // Garante que os estilos estejam inicializados
            GuiStyles.EnsureInitialized();

            _mousePosition = Event.current.mousePosition;
            _currentTooltip = string.Empty;

            // CORREÇÃO: Removido o GetRect e o ContainerStyle que causavam padding excessivo
            // e empurravam as abas para baixo.
            
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            try
            {
                DrawTabHeaders();
                GUILayout.Space(2); // Pequeno espaço entre abas e conteúdo
                DrawSelectedTabContent();
            }
            finally
            {
                GUILayout.EndVertical();
            }

            // Desenhar tooltip fora do layout principal
            if (!string.IsNullOrEmpty(_currentTooltip))
            {
                // Calcula o tamanho do tooltip com base no conteúdo e em limites máximos
                GUIContent tooltipContent = new GUIContent(_currentTooltip);
                Vector2 size = GuiStyles.TooltipStyle.CalcSize(tooltipContent);
                // Aplica padding definido no estilo
                size.x += GuiStyles.TooltipStyle.padding.horizontal;
                size.y += GuiStyles.TooltipStyle.padding.vertical;

                // Define limites máximos e mínimos para o tooltip
                float maxWidth = Mathf.Max(160f, Screen.width * 0.35f);
                float maxHeight = Mathf.Max(40f, Screen.height * 0.25f);
                float minWidth = 120f;
                float minHeight = 30f;

                size.x = Mathf.Clamp(size.x, minWidth, maxWidth);
                size.y = Mathf.Clamp(size.y, minHeight, maxHeight);

                // Calcula posição, respeitando os limites da tela
                float x = Mathf.Clamp(_mousePosition.x + 12f, 0, Screen.width - size.x);
                float y = Mathf.Clamp(_mousePosition.y - 24f, 0, Screen.height - size.y);
                Rect tooltipRect = new Rect(x, y, size.x, size.y);

                // Desenha o tooltip com o estilo GuiStyles
                GUI.Label(tooltipRect, tooltipContent, GuiStyles.TooltipStyle);
            }
        }

        private void DrawTabHeaders()
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            try
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    var style = i == _selectedTab ? GuiStyles.SelectedTabStyle : GuiStyles.TabStyle;
                    if (DrawTabButton(_tabs[i], style, i))
                    {
                        _selectedTab = i;
                    }
                }
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }

        private bool DrawTabButton(TabItem tab, GUIStyle style, int tabIndex)
        {
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            try
            {
                // Desenha o ícone se existir
                if (tab.Icon != null)
                {
                    // Usa o estilo GuiStyles.IconStyle para consistência
                    GUILayout.Box(tab.Icon, GuiStyles.IconStyle, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
                }
                // Desenha o botão da aba com o nome
                bool clicked = GUILayout.Button(tab.Name, style, GUILayout.ExpandWidth(false));

                // Verificar tooltip apenas após o botão ser desenhado (EventType.Repaint)
                if (Event.current.type == EventType.Repaint)
                {
                    Rect buttonRect = GUILayoutUtility.GetLastRect();
                    if (buttonRect.Contains(_mousePosition) && !string.IsNullOrEmpty(tab.Tooltip))
                    {
                        _currentTooltip = tab.Tooltip;
                        // O retângulo do tooltip é calculado no início do Draw()
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
            if (_selectedTab >= 0 && _selectedTab < _tabs.Count)
            {
                _tabs[_selectedTab].DrawContent?.Invoke(); // Invoca o conteúdo passado no construtor da aba
            }
        }

        public void ClearTabs()
        {
            _tabs.Clear();
            _selectedTab = 0;
        }

        public bool HasTab(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] != null && _tabs[i].Name == name) return true;
            }
            return false;
        }

        public void RemoveTab(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i] != null && _tabs[i].Name == name)
                {
                    _tabs.RemoveAt(i);
                    // Ajusta o índice selecionado se necessário
                    if (_selectedTab >= _tabs.Count)
                    {
                        _selectedTab = Mathf.Max(0, _tabs.Count - 1);
                    }
                    return;
                }
            }
        }

        public int GetSelectedTabIndex() => _selectedTab;

        public void SetSelectedTab(int index)
        {
            if (index >= 0 && index < _tabs.Count)
            {
                _selectedTab = index;
            }
        }

        public void ReorderTabs(int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < _tabs.Count && toIndex >= 0 && toIndex < _tabs.Count && fromIndex != toIndex)
            {
                var tab = _tabs[fromIndex];
                _tabs.RemoveAt(fromIndex);
                _tabs.Insert(toIndex, tab);
                // Opcional: Atualizar o índice selecionado se a aba selecionada foi movida
                if (_selectedTab == fromIndex)
                {
                    _selectedTab = toIndex;
                }
                else if (_selectedTab == toIndex)
                {
                    _selectedTab = toIndex;
                }
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
            DrawContent = drawContent ?? (() => { }); // Garante que não seja nulo
            Tooltip = tooltip;
            Icon = icon;
        }
    }
}