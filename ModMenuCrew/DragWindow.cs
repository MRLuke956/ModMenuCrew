using System;
using UnityEngine;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew
{
    public class DragWindow
    {
        private Rect _windowRect;
        private readonly Action _onGuiContent;
        private bool _isDragging;
        private Vector2 _dragOffset;
        private const float HeaderHeight = 20f;
        private const float Padding = 16f; // 8px de margem em cima e 8px em baixo do conteúdo
        // CornerRadius não é usado com os estilos atuais; removido para evitar warnings
        private bool _isMinimized;
        private bool _heightInitialized;
        private Vector2 _scrollPosition;
        private float _minViewportHeight = 180f;

        public bool Enabled { get; set; }
        public string Title { get; set; }

        public DragWindow(Rect initialRect, string title, Action onGuiContent)
        {
            _windowRect = initialRect;
            Title = title;
            _onGuiContent = onGuiContent ?? (() => { });
        }

        public void OnGUI()
        {
            if (!Enabled) return;
            GuiStyles.EnsureInitialized();

            // Inicializa altura padrão compacta
            if (!_heightInitialized)
            {
                float defaultHeight = Mathf.Min(Screen.height * 0.5f, 360f);
                if (_windowRect.height <= 0f) _windowRect.height = defaultHeight;
                _heightInitialized = true;
            }

            // (Revertido) Sem clamp dinâmico de tamanho – voltar ao comportamento original

            // 1. Fundo com estilo customizado
            GUI.Box(_windowRect, GUIContent.none, GuiStyles.WindowStyle);

            // 2. Desenhamos o cabeçalho
            var headerRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, HeaderHeight);
            GUI.Box(headerRect, GUIContent.none, GuiStyles.HeaderBackgroundStyle);

            // Botões de janela (minimizar e fechar)
            var btnArea = new Rect(headerRect.x + 6, headerRect.y + 2, 46, HeaderHeight - 4);
            GUILayout.BeginArea(btnArea);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isMinimized ? "▣" : "▬", GuiStyles.TitleBarButtonStyle))
            {
                _isMinimized = !_isMinimized;
            }
            if (GUILayout.Button("✕", GuiStyles.TitleBarButtonStyle))
            {
                Enabled = false;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Título centralizado
            GUI.Label(headerRect, Title, GuiStyles.TitleLabelStyle);

            // 3. Definimos a área para o nosso conteúdo
            var contentAreaRect = new Rect(
                _windowRect.x + 8,
                _windowRect.y + HeaderHeight + 8,
                _windowRect.width - 16,
                _isMinimized ? 0 : _windowRect.height - HeaderHeight - Padding
            );

            if (!_isMinimized)
            {
                GUILayout.BeginArea(contentAreaRect);
                try
                {
                    // Scroll aparece somente quando necessário; altura da janela não é forçada
                    float maxViewport = Mathf.Min(Screen.height * 0.7f, 520f);
                    float viewportHeight = Mathf.Clamp(_windowRect.height - HeaderHeight - Padding, _minViewportHeight, maxViewport - HeaderHeight - Padding);
                    // Scroll sempre disponível, mas discreto; altura de viewport baseada na janela
                    _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(viewportHeight));
                    _onGuiContent();
                    GUILayout.EndScrollView();
                }
                finally
                {
                    GUILayout.EndArea();
                }
            }

            // 5. Lidamos com o arrasto da janela
            HandleDragging(headerRect);
            ClampToScreen();
        }

        public Rect GetRect() => _windowRect;
        public void SetSize(float width, float height)
        {
            _windowRect.width = width;
            _windowRect.height = height; // A altura será recalculada, mas isso define um valor inicial
        }

        public void SetPosition(float x, float y)
        {
            _windowRect.x = x;
            _windowRect.y = y;
        }

        public void SetViewportMinHeight(float minHeight)
        {
            _minViewportHeight = Mathf.Clamp(minHeight, 60f, 600f);
        }

        private void HandleDragging(Rect dragArea)
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown && dragArea.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = e.mousePosition - new Vector2(_windowRect.x, _windowRect.y);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _isDragging)
            {
                Vector2 newPos = e.mousePosition - _dragOffset;
                _windowRect.x = newPos.x;
                _windowRect.y = newPos.y;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
            }
        }

        private void ClampToScreen()
        {
            _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Screen.width - _windowRect.width);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Screen.height - _windowRect.height);
        }

        // Contorno removido para evitar chamadas não suportadas em IL2CPP (GUI.DrawTexture)
    }
}