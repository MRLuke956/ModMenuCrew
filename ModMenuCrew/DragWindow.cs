using System;
using UnityEngine;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew
{
    public class DragWindow
    {
        // --- Constantes ---
        private const float MinWindowWidth = 200f;
        private const float MinWindowHeight = 100f; // Inclui HeaderHeight
        private const float MaxWindowHeight = 600f; // Inclui HeaderHeight
        private const float ContentPadding = 4f; // Espaçamento reduzido (era 8f) para subir o conteúdo

        // --- Estados e Dados ---
        private Rect _windowRect;
        private readonly Action _onGuiContent;
        // MANTIDO conforme solicitado
        private bool _isDragging;
        private Vector2 _dragOffset;
        private bool _isMinimized;
        private bool _heightInitialized;
        private Vector2 _scrollPosition;
        // MANTIDO conforme solicitado, com valores padrão e clamping
        private float _minViewportHeight = 180f; // Altura mínima da área de conteúdo rolável (excluindo cabeçalho)

        // --- Propriedades Públicas ---
        public bool Enabled { get; set; }
        public string Title { get; set; }

        // --- Cache de Retângulos (Opcional, para evitar alocações em OnGUI se necessário) ---
        private Rect _cachedHeaderRect;
        private Rect _cachedContentRect;
        private Rect _cachedButtonArea;

        public DragWindow(Rect initialRect, string title, Action onGuiContent)
        {
            // Aplica clamping inicial para garantir valores válidos
            _windowRect = new Rect(
                initialRect.x,
                initialRect.y,
                Mathf.Max(initialRect.width, MinWindowWidth),
                Mathf.Clamp(initialRect.height, MinWindowHeight, MaxWindowHeight)
            );
            Title = title;
            _onGuiContent = onGuiContent ?? (() => { });
        }

        public void OnGUI()
        {
            if (!Enabled) return;

            // Garante que os estilos estejam inicializados
            GuiStyles.EnsureInitialized();

            // Inicializa altura padrão se necessário
            if (!_heightInitialized)
            {
                float defaultHeight = Mathf.Min(Screen.height * 0.5f, 360f);
                _windowRect.height = Mathf.Clamp(defaultHeight, MinWindowHeight, MaxWindowHeight);
                _heightInitialized = true;
            }

            // Aplica clamping de tamanho a cada frame
            _windowRect.width = Mathf.Max(_windowRect.width, MinWindowWidth);
            _windowRect.height = Mathf.Clamp(_windowRect.height, MinWindowHeight, MaxWindowHeight);

            // 1. Fundo da janela
            GUI.Box(_windowRect, GUIContent.none, GuiStyles.WindowStyle);

            // 2. Cabeçalho
            float headerHeight = GuiStyles.TitleBarButtonStyle.fixedHeight + 4; // Altura do botão + margem
            _cachedHeaderRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, headerHeight);
            GUI.Box(_cachedHeaderRect, GUIContent.none, GuiStyles.HeaderBackgroundStyle);

            // Botões de controle da janela (Minimizar, Fechar)
            float buttonWidth = GuiStyles.TitleBarButtonStyle.fixedWidth;
            float buttonHeight = GuiStyles.TitleBarButtonStyle.fixedHeight;
            float buttonMargin = GuiStyles.TitleBarButtonStyle.margin.left + GuiStyles.TitleBarButtonStyle.margin.right;
            float totalButtonWidth = (2 * buttonWidth) + (2 * buttonMargin); // Min e Close

            _cachedButtonArea = new Rect(
                _windowRect.x + _windowRect.width - totalButtonWidth - 4, // Pequena margem direita
                _windowRect.y + 2, // Pequena margem superior
                totalButtonWidth,
                headerHeight - 4  // Altura menos margem superior/inferior
            );

            GUILayout.BeginArea(_cachedButtonArea);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isMinimized ? "▭" : "—", GuiStyles.TitleBarButtonStyle)) // Usando símbolos mais comuns
            {
                _isMinimized = !_isMinimized;
            }
            if (GUILayout.Button("✕", GuiStyles.TitleBarButtonStyle))
            {
                Enabled = false;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return; // Sai imediatamente após fechar
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Título centralizado no cabeçalho
            // Calcula retângulo para o título, excluindo a área dos botões
            var titleRect = new Rect(
                _cachedHeaderRect.x + 4, // Margem esquerda
                _cachedHeaderRect.y,
                _cachedButtonArea.x - _cachedHeaderRect.x - 4, // Largura até antes dos botões, com margem
                _cachedHeaderRect.height
            );
            GUI.Label(titleRect, Title, GuiStyles.TitleLabelStyle);

            // 3. Conteúdo da janela (se não estiver minimizado)
            if (!_isMinimized)
            {
                // Calcula a área do conteúdo
                _cachedContentRect = new Rect(
                    _windowRect.x + ContentPadding,
                    _windowRect.y + headerHeight + ContentPadding,
                    _windowRect.width - (2 * ContentPadding),
                    _windowRect.height - headerHeight - (2 * ContentPadding)
                );

                GUILayout.BeginArea(_cachedContentRect);

                // Inicia ScrollView com altura calculada
                float maxViewportHeight = MaxWindowHeight - headerHeight - (2 * ContentPadding);
                float currentViewportHeight = Mathf.Clamp(
                    _windowRect.height - headerHeight - (2 * ContentPadding),
                    _minViewportHeight, // Usa o valor configurável
                    maxViewportHeight
                );

                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true, GUILayout.Height(currentViewportHeight));
                _onGuiContent?.Invoke(); // Invoca o conteúdo passado no construtor
                GUILayout.EndScrollView();

                GUILayout.EndArea();
            }

            // 4. Lida com o arrasto
            HandleDragging(_cachedHeaderRect);

            // 5. Mantém a janela dentro da tela
            ClampToScreen();
        }

        public Rect GetRect() => _windowRect;

        public void SetSize(float width, float height)
        {
            _windowRect.width = Mathf.Max(width, MinWindowWidth);
            _windowRect.height = Mathf.Clamp(height, MinWindowHeight, MaxWindowHeight);
        }

        public void SetPosition(float x, float y)
        {
            _windowRect.x = x;
            _windowRect.y = y;
        }

        // MANTIDO conforme solicitado
        public void SetViewportMinHeight(float minHeight)
        {
            // Aplica clamping ao valor recebido para manter consistência
            _minViewportHeight = Mathf.Clamp(minHeight, 60f, 400f); // Exemplo de limites internos
        }

        // MANTIDO conforme solicitado, com lógica ajustada para usar campos de classe
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
    }
}