using System;
using ModMenuCrew.UI.Styles;
using UnityEngine;

namespace ModMenuCrew.UI.Controls
{
    public class MenuSection
    {
        // --- Estados e Dados ---
        private readonly string _title;
        private readonly Action _drawContent;
        private bool _isExpanded = true;

        // --- Cache de Retângulos (Opcional, para evitar alocações em OnGUI se necessário) ---
        private Rect _cachedHeaderRect;
        private Rect _cachedTitleRect;
        private Rect _cachedButtonRect;

        public MenuSection(string title, Action drawContent)
        {
            _title = title;
            _drawContent = drawContent ?? (() => { }); // Garante que não seja nulo
        }

        public void Draw()
        {
            // Garante que os estilos estejam inicializados
            GuiStyles.EnsureInitialized();

            GUILayout.BeginVertical(GuiStyles.SectionStyle);

            // Cabeçalho moderno com gradiente e botão
            _cachedHeaderRect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true)); // Altura baseada no estilo do botão
            GUI.Box(_cachedHeaderRect, GUIContent.none, GuiStyles.HeaderBackgroundStyle);

            // Título centralizado com leve folga nas laterais
            _cachedTitleRect = new Rect(_cachedHeaderRect.x + 8, _cachedHeaderRect.y, _cachedHeaderRect.width - 56, _cachedHeaderRect.height);
            GUI.Label(_cachedTitleRect, _title, GuiStyles.TitleLabelStyle);

            // Botão expandir/recolher no canto direito
            _cachedButtonRect = new Rect(_cachedHeaderRect.xMax - 28, _cachedHeaderRect.y + 5, 20, _cachedHeaderRect.height - 10);
            if (GUI.Button(_cachedButtonRect, _isExpanded ? "▾" : "▸", GuiStyles.TitleBarButtonStyle))
            {
                _isExpanded = !_isExpanded;
            }

            if (_isExpanded)
            {
                // Conteúdo com container estilizado
                // Usando HighlightStyle em vez de ContainerStyle para reduzir padding interno excessivo dentro das seções
                GUILayout.BeginVertical(GuiStyles.HighlightStyle);
                _drawContent?.Invoke(); // Invoca o conteúdo passado no construtor
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
            GUILayout.Space(10); // Espaçamento após a seção
        }
    }
}