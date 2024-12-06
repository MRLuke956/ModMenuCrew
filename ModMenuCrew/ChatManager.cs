using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hazel;
using ModMenuCrew.UI.Extensions;
using ModMenuCrew.UI.Styles;

namespace ModMenuCrew.UI.Managers;

public class ChatManager
{
    private readonly Queue<string> chatHistory = new();
    private const int MaxHistorySize = 10;
    private string currentMessage = string.Empty;
    private bool showTimestamp = true;
    private Vector2 scrollPosition;
    private Vector2 mainScrollPosition; // Added for main scroll view
    private readonly string[] quickMessages = new[]
    {
        "Who?",
        "Skip vote",
        "I was in {location}",
        "Emergency meeting!"
    };

    public void SendChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || PlayerControl.LocalPlayer == null) return;

        var formattedMessage = showTimestamp
            ? $"[{System.DateTime.Now:HH:mm:ss}] {message}"
            : message;

        PlayerControl.LocalPlayer.RpcSendChat(formattedMessage);
        AddToHistory(formattedMessage);
    }

    


    public void DrawQuickMessages()
    {
        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        GUILayout.Label("Quick Messages", GuiStyles.HeaderStyle);
        foreach (var message in quickMessages)
        {
            if (GUILayout.Button(message, GuiStyles.ButtonStyle))
            {
                var finalMessage = message;
                if (message.Contains("{location}"))
                {
                    var room = PlayerControl.LocalPlayer?.GetTruePosition().GetRoom();
                    finalMessage = message.Replace("{location}", room?.ToString() ?? "Unknown");
                }
                SendChatMessage(finalMessage);
            }
        }
        GUILayout.EndVertical();
    }

    public void DrawChatHistory()
    {
        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        GUILayout.Label("Chat History", GuiStyles.HeaderStyle);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        foreach (var message in chatHistory.Reverse())
        {
            GUILayout.Label(message, GuiStyles.LabelStyle);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    public void DrawSettings()
    {
        GUILayout.BeginVertical(GuiStyles.SectionStyle);
        GUILayout.Label("Chat Settings", GuiStyles.HeaderStyle);
        showTimestamp = GUILayout.Toggle(showTimestamp, "Show Timestamps", GuiStyles.ToggleStyle);
        GUILayout.EndVertical();
    }

    private void AddToHistory(string message)
    {
        chatHistory.Enqueue(message);
        while (chatHistory.Count > MaxHistorySize)
        {
            chatHistory.Dequeue();
        }
    }
}
