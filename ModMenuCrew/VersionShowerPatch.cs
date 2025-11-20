using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using TMPro;
using UnityEngine;

namespace ModMenuCrew.Patches
{
    [HarmonyPatch(typeof(VersionShower))]
    public static class VersionShowerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(VersionShower.Start))]
        public static void Postfix(VersionShower __instance)
        {
            try
            {
                if (__instance == null || __instance.text == null) return;
                var tmp = __instance.text;
                tmp.richText = true;
                tmp.color = new Color(0.0f, 1.0f, 0.55f, 1f);
                tmp.outlineColor = new Color(0.0f, 1.0f, 0.55f, 1f);
                tmp.outlineWidth = 0.2f;
                try { ClassInjector.RegisterTypeInIl2Cpp<VersionShowerFx>(); } catch { }

                tmp.text = StripBuildNum(tmp.text);

                var fx = __instance.gameObject.GetComponent<VersionShowerFx>();
                if (fx == null) fx = __instance.gameObject.AddComponent<VersionShowerFx>();
                fx.Initialize(tmp);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModMenuCrew] VersionShowerPatch error: {ex}");
            }
        }

        private static string StripBuildNum(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int idx = text.IndexOf("(build num:");
            if (idx >= 0)
            {
                int start = idx;
                if (start > 0 && text[start - 1] == ' ') start -= 1;
                int end = text.IndexOf(')', idx);
                if (end >= 0) return text.Remove(start, end - start + 1);
                return text.Substring(0, start);
            }
            return text;
        }
    }

    public class VersionShowerFx : MonoBehaviour
    {
        // --- Cache de Componentes ---
        private TextMeshPro _text;
        private RectTransform _textRectTransform;

        // --- Dados de Texto ---
        private string _baseText;
        private string _modText;
        private readonly StringBuilder _textBuilder = new StringBuilder(256);

        // --- Estados e Controles ---
        private bool _isEffectRunning;
        private bool _isGlitchActive;
        private Coroutine _schedulerRoutine;
        private Coroutine _breathingRoutine;

        // --- Cache de Valores Base ---
        private Vector2 _baseAnchoredPosition;
        private float _baseScale;
        private Color _baseColor;
        private Color _baseOutlineColor;
        private float _baseOutlineWidth;

        // --- PERFORMANCE: Cache de WaitForSeconds otimizado ---
        private static readonly WaitForSeconds sWaitFrame = new WaitForSeconds(0.016f);
        private static readonly WaitForSeconds sWait025 = new WaitForSeconds(0.025f);
        private static readonly WaitForSeconds sWait04 = new WaitForSeconds(0.04f);
        private static readonly WaitForSeconds sWait05 = new WaitForSeconds(0.05f);
        private static readonly WaitForSeconds sWait08 = new WaitForSeconds(0.08f);
        private static readonly WaitForSeconds sWait1 = new WaitForSeconds(0.1f);
        private static readonly WaitForSeconds sWait15 = new WaitForSeconds(0.15f);
        private static readonly WaitForSeconds sWait2 = new WaitForSeconds(0.2f);
        private static readonly WaitForSeconds sWait3 = new WaitForSeconds(0.3f);

        // --- Probabilidades e Cooldowns (MAIS AGRESSIVO PARA TERROR) ---
        private static float sProbPhantom = 0.04f;
        private static float sProbMythic = 0.12f;
        private static float sProbRare = 0.30f;
        private float _heavyCooldownSeconds = 8f;
        private float _nextHeavyAllowedTime = 0f;

        // --- Paleta de Cores Terror ---
        private static readonly Color FnafGreen = new Color(0.6f, 1f, 0.3f);
        private static readonly Color AlertOrange = new Color(1f, 0.4f, 0f);
        private static readonly Color DeadRed = new Color(1f, 0.0f, 0.0f); // Vermelho puro
        private static readonly Color BloodDark = new Color(0.5f, 0.0f, 0.0f); // Vermelho sangue escuro
        private static readonly Color GhostCyan = new Color(0.4f, 1f, 1f, 0.8f);

        private static readonly System.Random sRandom = new System.Random();

        // --- Dados Estáticos (MENSAGENS DE TERROR) ---
        private static readonly string[] sSystemMessages =
        {
            "SUS", "[REDACTED]", "ACCESS DENIED", "SECURITY ALERT", "UNKNOWN SIGNAL",
            "WHO IS IT?", "NOT THE IMPOSTOR", "VENT ERROR", "AUDIO ERROR", "VIDEO ERROR",
            "CAM SYS ERROR", "REBOOT ALL", "BODY FOUND", "INTRUDER ALERT", "LOCKDOWN",
            "RUN", "HIDE", "THEY ARE HERE", "DON'T LOOK BACK", "I SEE YOU", "YOU ARE NEXT",
            "IT'S TOO LATE", "NO ESCAPE", "KILL", "DEAD", "HELP ME", "BEHIND YOU", "ERROR 666",
            "WATCHING...", "DO NOT MOVE", "IMPOSTOR WIN", "GAME OVER", "BLOOD", "DARKNESS"
        };

        private static readonly char[] sNoisePool =
        {
            '░', '▒', '▓', '█', '▚', '▞', '▙', '▟', '_', '#', '/', '!', '?', 'Ø', '¤', '◊',
            '†', '‡', '☠', '☢', '☣', '⚡', '⚠', '⚔', '⚖', '§', '¶'
        };

        private static readonly string[] sCamRooms =
        {
            "Electrical", "MedBay", "Security", "Reactor", "O2", "Admin", "Navigation",
            "Cafeteria", "Storage", "Shields", "Lower Engine", "Upper Engine", "THE VOID", "MORGUE"
        };

        private Color[] _colorCycle = { DeadRed, BloodDark, Color.black, DeadRed, GhostCyan, Color.grey };
        private Color[] _flashColors = { DeadRed, GhostCyan, Color.white, BloodDark };

        public VersionShowerFx(IntPtr ptr) : base(ptr) { }

        [HideFromIl2Cpp]
        public void Initialize(TextMeshPro text)
        {
            if (_text != null) return;
            if (text == null)
            {
                Debug.LogError("[VersionShowerFx] TextMeshPro é null!");
                return;
            }

            _text = text;
            _textRectTransform = text.rectTransform;
            _baseText = text.text;
            _modText = $"Mod Menu Crew {ModMenuCrewPlugin.ModVersion}";

            _baseAnchoredPosition = _textRectTransform.anchoredPosition;
            _baseScale = _textRectTransform.localScale.x;
            _baseColor = _text.color;
            _baseOutlineColor = _text.outlineColor;
            _baseOutlineWidth = _text.outlineWidth;

            if (!_isEffectRunning)
            {
                _isEffectRunning = true;
                _nextHeavyAllowedTime = Time.time + 3f;

                if (_schedulerRoutine != null) StopCoroutine(_schedulerRoutine);
                if (_breathingRoutine != null) StopCoroutine(_breathingRoutine);

                _schedulerRoutine = StartCoroutine(GlitchScheduler().WrapToIl2Cpp());
                _breathingRoutine = StartCoroutine(IdleBreathing().WrapToIl2Cpp());

                Debug.Log("[VersionShowerFx] Efeitos de terror inicializados!");
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator GlitchScheduler()
        {
            SetText(_modText);

            while (_isEffectRunning && _text != null)
            {
                float delay = (float)sRandom.NextDouble() * 2f + 1.5f; 
                yield return new WaitForSeconds(delay);

                if (_isGlitchActive || _text == null) continue;

                _isGlitchActive = true;

                bool heavyAllowed = Time.time >= _nextHeavyAllowedTime;
                double roll = sRandom.NextDouble();

                if (roll < sProbPhantom && heavyAllowed)
                {
                    _nextHeavyAllowedTime = Time.time + _heavyCooldownSeconds;
                    yield return PhantomTierEvent();
                }
                else if (roll < (sProbPhantom + sProbMythic) && heavyAllowed)
                {
                    _nextHeavyAllowedTime = Time.time + _heavyCooldownSeconds;
                    yield return MythicTierEvent();
                }
                else if (roll < (sProbPhantom + sProbMythic + sProbRare))
                {
                    yield return RareTierEvent();
                }
                else if (roll < 0.85f)
                {
                    yield return UncommonTierEvent();
                }
                else
                {
                    yield return CommonTierEvent();
                }

                _isGlitchActive = false;
                ResetVisualsToStable();
                yield return sWait2;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator IdleBreathing()
        {
            float seed = (float)sRandom.NextDouble() * 100f;
            while (_isEffectRunning)
            {
                if (_text == null) yield break;
                if (!_isGlitchActive)
                {
                    var rt = _textRectTransform;
                    float t = Time.time + seed;
                    
                    // Respiração irregular
                    float breathingSpeed = 1.5f + Mathf.Sin(t * 0.3f); 
                    float scaleNoise = (Mathf.Sin(t * breathingSpeed) * 0.08f) + (Mathf.Sin(t * 5f) * 0.02f);
                    float angle = Mathf.Sin(t * 0.8f) * 1.2f;
                    
                    rt.localRotation = Quaternion.Euler(0, 0, angle);
                    rt.localScale = new Vector3(_baseScale + scaleNoise, _baseScale + scaleNoise, 1f);
                    // Removido modificação de posição no Idle para evitar drift ou sair da tela
                    // rt.anchoredPosition = _baseAnchoredPosition + ...
                }
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator CommonTierEvent()
        {
            int eventType = sRandom.Next(0, 6);
            switch (eventType)
            {
                case 0: yield return Jitter(0.3f, 1.0f, 1.5f); break; // Reduzido range
                case 1: yield return TextCorruption(0.4f, 6, _modText); break;
                case 2: yield return VHSStaticBurst(0.35f); break;
                case 3: yield return GreenPulse(0.4f); break;
                case 4: yield return NoiseScroll(0.4f); break;
                case 5: yield return ColorFlash(DeadRed, 0.25f); break;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator UncommonTierEvent()
        {
            int eventType = sRandom.Next(0, 8);
            switch (eventType)
            {
                case 0: yield return ChromaticAberration(0.5f); break;
                case 1: yield return VerticalRoll(0.3f, 40f, 3f); break; // Amplitude explícita 3f
                case 2: yield return NoiseRain(0.6f); break;
                case 3: yield return SystemWarning("RUN AWAY", 0.7f, DeadRed); break;
                case 4: yield return Wobble(0.5f, 15f, 3f); break; // Amplitude 3f
                case 5: yield return CameraLabelFlash(0.6f); break;
                case 6: yield return MotionPing(0.7f); break;
                case 7: yield return NoiseHalo(0.5f); break;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator RareTierEvent()
        {
            int eventType = sRandom.Next(0, 14);
            switch (eventType)
            {
                case 0: yield return ImpostorFlash(DeadRed); break;
                case 1: yield return TextCorruption(1.0f, 15, sSystemMessages[sRandom.Next(sSystemMessages.Length)]); break;
                case 2: yield return Jitter(0.8f, 4f, 5f); break; // Reduzido de 10f para 4f
                case 3: yield return SystemWarning("O2 DEPLETED", 1f, AlertOrange); break;
                case 4: yield return SystemWarning("REACTOR CRITICAL", 1.2f, DeadRed); break;
                case 5: yield return TrackingNoise(0.9f); break;
                case 6: yield return NoiseFrame(0.8f); break;
                case 7: yield return ColorDrain(1.0f); break;
                case 8: yield return TypewriterText(sSystemMessages[sRandom.Next(sSystemMessages.Length)], 0.05f); break;
                case 9: yield return CharacterSwapGlitch(0.8f); break;
                case 10: yield return BurnInPulse(0.9f); break;
                case 11: yield return CRTCrosstalk(0.8f); break;
                case 12: yield return HeartbeatHorror(); break;
                case 13: yield return BloodDripEffect(); break;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator MythicTierEvent()
        {
            int eventType = sRandom.Next(0, 6);
            switch (eventType)
            {
                case 0: yield return Sequence_EmergencyMeeting(); break;
                case 1: yield return Sequence_ImpostorReveal(); break;
                case 2: yield return Sequence_SystemReboot(); break;
                case 3: yield return Sequence_SabotageCritical(); break;
                case 4: yield return Sequence_CriticalBreach(); break;
                case 5: yield return Sequence_TheStare(); break;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator PhantomTierEvent()
        {
            int eventType = sRandom.Next(0, 6);
            switch (eventType)
            {
                case 0: yield return PhantomAppearance("I SEE YOU", DeadRed, 0.8f); break;
                case 1: yield return PhantomAppearance("BEHIND YOU", Color.black, 0.7f); break;
                case 2: yield return PhantomAppearance("IT'S HERE", DeadRed, 0.8f); break;
                case 3: yield return CrewmateColorCycle(); break;
                case 4: yield return PhantomSignal(1.0f); break;
                case 5: yield return TerrifyingSequence(); break;
            }
        }

        // --- SEQUÊNCIAS TERROR ---

        [HideFromIl2Cpp]
        private IEnumerator Sequence_TheStare()
        {
             yield return SystemWarning("DON'T BLINK", 1.0f, Color.white);
             yield return sWait1;
             SetText("", true);
             yield return sWait1;
             _text.color = DeadRed;
             SetText("O_O", true);
             yield return new WaitForSeconds(0.5f);
             yield return Jitter(0.5f, 6f, 5f); // Reduzido de 20f
             yield return ImpostorFlash(Color.black);
        }
        
        [HideFromIl2Cpp]
        private IEnumerator HeartbeatHorror()
        {
            for (int i = 0; i < 4; i++)
            {
                _text.color = DeadRed;
                _text.outlineColor = BloodDark;
                _textRectTransform.localScale = new Vector3(_baseScale * 1.4f, _baseScale * 1.4f, 1f);
                SetText("THUMP", true);
                yield return sWait05;
                _textRectTransform.localScale = new Vector3(_baseScale, _baseScale, 1f);
                SetText(_modText, true);
                yield return sWait08;
                _textRectTransform.localScale = new Vector3(_baseScale * 1.2f, _baseScale * 1.2f, 1f);
                 _text.color = BloodDark;
                yield return sWait05;
                _textRectTransform.localScale = new Vector3(_baseScale, _baseScale, 1f);
                yield return new WaitForSeconds(0.6f);
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator BloodDripEffect()
        {
             _text.color = DeadRed;
             string current = _modText;
             for(int i=0; i<current.Length; i++)
             {
                 _textBuilder.Clear().Append(current.Substring(0, i)).Append("<color=red>█</color>").Append(current.Substring(i+1));
                 SetText(_textBuilder.ToString(), true);
                 yield return sWait05;
             }
             yield return ImpostorFlash(DeadRed);
        }

        [HideFromIl2Cpp]
        private IEnumerator Sequence_EmergencyMeeting()
        {
            SetText("", true);
            yield return sWait15;
            yield return TypewriterText("WHO IS THE IMPOSTOR?", 0.06f);
            yield return sWait2;
            yield return Jitter(0.5f, 5f, 5f);
            SetText("", true);
            yield return sWait2;
        }

        [HideFromIl2Cpp]
        private IEnumerator Sequence_ImpostorReveal()
        {
            yield return TypewriterText("THE IMPOSTOR IS...", 0.08f);
            yield return sWait2;
            yield return Jitter(0.4f, 15f, 15f);
            yield return ImpostorFlash(DeadRed);
            SetText(CorruptText("IMPOSTOR", 10), true);
            yield return sWait15;
        }

        [HideFromIl2Cpp]
        private IEnumerator Sequence_SystemReboot()
        {
            yield return SystemWarning("SYSTEM FAILURE", 1.5f, DeadRed);
            SetText("", true);
            yield return new WaitForSeconds(1f);
            yield return TypewriterText("REBOOTING...", 0.1f);
            yield return sWait2;
            SetText(CorruptText("............", 12), true);
            yield return sWait2;
        }

        [HideFromIl2Cpp]
        private IEnumerator Sequence_SabotageCritical()
        {
            float duration = 1.5f;
            float endTime = Time.time + duration;

            while (Time.time < endTime && _text != null)
            {
                float t = 1 - ((endTime - Time.time) / duration);
                Color alertColor = (Time.frameCount % 8 < 4) ? DeadRed : Color.yellow;
                _text.color = alertColor;
                SetText(CorruptText("SABOTAGE", (int)(t * 15)), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator Sequence_CriticalBreach()
        {
            yield return IntensiveGlitchSequence(1.2f);
            yield return sWait15;
            yield return SystemWarning("BREACH", 1.2f, DeadRed);
            yield return sWait15;
            yield return TypewriterText("INTRUDER", 0.08f);
        }

        [HideFromIl2Cpp]
        private IEnumerator PhantomAppearance(string name, Color color, float duration)
        {
            _text.color = color;
            _text.outlineColor = color * 0.6f;
            _text.outlineWidth = 0.4f;
            string phantomText = CorruptText(name, 5);

            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                SetText(phantomText, true);
                yield return sWait08;
                SetText("", true);
                yield return sWait08;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator CrewmateColorCycle()
        {
            int iterations = _colorCycle.Length * 2;
            for (int i = 0; i < iterations && _text != null; i++)
            {
                _text.color = _colorCycle[i % _colorCycle.Length];
                _text.outlineColor = _colorCycle[i % _colorCycle.Length] * 0.7f;
                SetText(CorruptText(_modText, 3), true);
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator TerrifyingSequence()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return Jitter(0.4f + (i * 0.1f), 3f + (i * 1.5f), 6f + (i * 3f)); // Reduzido pos range
                yield return sWait15;
                yield return ChromaticAberration(0.35f);
                yield return sWait15;
            }

            yield return PhantomAppearance("IT'S HERE", DeadRed, 0.6f);
            yield return sWait15;
            yield return Jitter(1f, 8f, 25f); // Reduzido max de 20f para 8f
        }

        // --- EFEITOS BÁSICOS OTIMIZADOS ---

        [HideFromIl2Cpp]
        private void SetText(string content, bool isGlitching = false)
        {
            if (_text == null) return;
            string colorTag = ColorUtility.ToHtmlStringRGB(_baseColor);
            string displayText = isGlitching ? content : _modText;
            _textBuilder.Clear().Append(_baseText).Append(" <color=#").Append(colorTag).Append("><b><i>").Append(displayText).Append("</i></b></color>");
            _text.text = _textBuilder.ToString();
        }

        [HideFromIl2Cpp]
        private IEnumerator TextCorruption(float duration, int intensity, string baseText)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                SetText(CorruptText(baseText, intensity), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator Jitter(float duration, float posRange, float rotRange)
        {
            float endTime = Time.time + duration;
            var rt = _textRectTransform;

            while (Time.time < endTime && _text != null)
            {
                float jitterX = ((float)sRandom.NextDouble() - 0.5f) * posRange;
                float jitterY = ((float)sRandom.NextDouble() - 0.5f) * posRange;
                float jitterRot = ((float)sRandom.NextDouble() - 0.5f) * 2f * rotRange;

                rt.anchoredPosition = new Vector2(_baseAnchoredPosition.x + jitterX, _baseAnchoredPosition.y + jitterY);
                rt.localRotation = Quaternion.Euler(0, 0, jitterRot);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator ChromaticAberration(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                _textBuilder.Clear().Append(_modText).Append("\n<color=#FF0000>").Append(_modText).Append("</color>\n<color=#00FF00>").Append(_modText).Append("</color>");
                SetText(_textBuilder.ToString(), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator VerticalRoll(float duration, float frequency, float amplitude)
        {
            float endTime = Time.time + duration;
            var rt = _textRectTransform;

            while (Time.time < endTime && _text != null)
            {
                float offset = Mathf.Sin(Time.time * frequency) * amplitude;
                rt.anchoredPosition = new Vector2(_baseAnchoredPosition.x, _baseAnchoredPosition.y + offset);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator ImpostorFlash(Color color)
        {
            if (_text == null) yield break;
            _text.color = color;
            _text.outlineColor = color * 0.7f;
            _text.outlineWidth = 0.45f;
            yield return sWait1;
        }

        [HideFromIl2Cpp]
        private IEnumerator SystemWarning(string message, float duration, Color color)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                _text.color = color;
                _text.outlineColor = color * 0.6f;
                SetText(sRandom.Next(0, 2) == 1 ? message : CorruptText(message, 3), true);
                yield return sWait08;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator TrackingNoise(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                _textBuilder.Clear().Append(_modText).Append("\n").Append(CorruptText("█ ░ █ ░ █", 6));
                SetText(_textBuilder.ToString(), true);
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator ColorDrain(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                float progress = (endTime - Time.time) / duration;
                _text.color = Color.Lerp(new Color(0.1f, 0.1f, 0.1f), _baseColor, progress);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator TypewriterText(string text, float delayPerChar)
        {
            for (int i = 0; i <= text.Length && _text != null; i++)
            {
                _textBuilder.Clear().Append(text.Substring(0, i));
                SetText(_textBuilder.ToString(), true);
                yield return new WaitForSeconds(delayPerChar);
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator CharacterSwapGlitch(float duration)
        {
            float endTime = Time.time + duration;
            char[] chars = _modText.ToCharArray();
            while (Time.time < endTime && _text != null)
            {
                int idx1 = sRandom.Next(chars.Length);
                int idx2 = sRandom.Next(chars.Length);
                (chars[idx1], chars[idx2]) = (chars[idx2], chars[idx1]);
                _textBuilder.Clear().Append(chars);
                SetText(_textBuilder.ToString(), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator VHSStaticBurst(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                _textBuilder.Clear().Append(_modText).Append("\n").Append('█', sRandom.Next(8, 16));
                SetText(_textBuilder.ToString(), true);
                yield return sWait025;
                SetText(_modText, true);
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator CRTCrosstalk(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                _textBuilder.Clear().Append(_modText).Append("\n<color=#FFFF00>").Append(_modText).Append("</color>");
                SetText(_textBuilder.ToString(), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator GreenPulse(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                float t = Mathf.PingPong(Time.time * 2f, 1f);
                _text.color = Color.Lerp(_baseColor, FnafGreen, t);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator NoiseScroll(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string noise = RandomNoise(20);
                _textBuilder.Clear().Append(_modText).Append("\n").Append(noise);
                SetText(_textBuilder.ToString(), true);
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator NoiseRain(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string line1 = RandomNoise(18);
                string line2 = RandomNoise(18);
                _textBuilder.Clear().Append(_modText).Append("\n").Append(line1).Append("\n").Append(line2);
                SetText(_textBuilder.ToString(), true);
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator NoiseHalo(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string halo = RandomNoise(10);
                _textBuilder.Clear().Append(_modText).Append(" ").Append(halo);
                SetText(_textBuilder.ToString(), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator NoiseFrame(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string top = RandomNoise(20);
                string bottom = RandomNoise(20);
                _textBuilder.Clear().Append(top).Append("\n").Append(_modText).Append("\n").Append(bottom);
                SetText(_textBuilder.ToString(), true);
                yield return sWait04;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator CameraLabelFlash(float duration)
        {
            float endTime = Time.time + duration;
            int cam = sRandom.Next(1, 9);

            while (Time.time < endTime && _text != null)
            {
                _textBuilder.Clear().Append("CAM ").Append(cam.ToString("00"));
                SetText(_textBuilder.ToString(), true);
                _text.color = Color.cyan;
                cam = (cam % 8) + 1;
                yield return sWait1;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator BurnInPulse(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                float t = Mathf.PingPong(Time.time * 2f, 1f);
                _text.outlineWidth = Mathf.Lerp(_baseOutlineWidth, 0.5f, t);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator Wobble(float duration, float frequency, float amplitude)
        {
            float endTime = Time.time + duration;
            var rt = _textRectTransform;

            while (Time.time < endTime && _text != null)
            {
                float t = Time.time * frequency;
                float offsetX = Mathf.Sin(t) * amplitude;
                float offsetY = Mathf.Cos(t * 0.5f) * amplitude * 0.5f;

                rt.anchoredPosition = new Vector2(_baseAnchoredPosition.x + offsetX, _baseAnchoredPosition.y + offsetY);
                yield return sWaitFrame;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator MotionPing(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string room = RandomRoom();
                _text.color = Color.yellow;
                _textBuilder.Clear().Append("MOTION - ").Append(room);
                SetText(_textBuilder.ToString(), true);
                yield return sWait1;
                SetText(_modText, true);
                yield return sWait1;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator PhantomSignal(float duration)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime && _text != null)
            {
                string sig = $"{RandomNoise(3)} SIGNAL {RandomNoise(3)}";
                SetText(sig, true);
                _text.color = GhostCyan;
                yield return sWait025;
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator ColorFlash(Color color, float duration)
        {
            if (_text == null) yield break;
            _text.color = color;
            _text.outlineColor = color;
            yield return new WaitForSeconds(duration);
        }

        [HideFromIl2Cpp]
        private IEnumerator IntensiveGlitchSequence(float duration)
        {
            float endTime = Time.time + duration;
            var rt = _textRectTransform;

            while (Time.time < endTime && _text != null)
            {
                float progress = 1 - ((endTime - Time.time) / duration);
                float intensity = Mathf.Lerp(1f, 2.5f, progress);

                SetText(CorruptText(_modText, (int)(intensity * 5)), true);

                float jitterX = ((float)sRandom.NextDouble() - 0.5f) * intensity * 2f; // Reduzido de 6f
                float jitterY = ((float)sRandom.NextDouble() - 0.5f) * intensity * 2f; // Reduzido de 6f

                rt.anchoredPosition = new Vector2(_baseAnchoredPosition.x + jitterX, _baseAnchoredPosition.y + jitterY);

                _text.color = _flashColors[(int)(progress * 4) % 4];
                yield return sWait025;
            }
        }

        // --- UTILIDADES ---

        [HideFromIl2Cpp]
        private string CorruptText(string input, int passes)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var chars = input.ToCharArray();
            for (int k = 0; k < passes && k < chars.Length; k++)
            {
                int idx = sRandom.Next(0, chars.Length);
                chars[idx] = sNoisePool[sRandom.Next(sNoisePool.Length)];
            }
            _textBuilder.Clear().Append(chars);
            return _textBuilder.ToString();
        }

        [HideFromIl2Cpp]
        private string RandomNoise(int length)
        {
            if (length <= 0) return "";
            _textBuilder.Clear();
            for (int i = 0; i < length; i++)
            {
                _textBuilder.Append(sNoisePool[sRandom.Next(sNoisePool.Length)]);
            }
            return _textBuilder.ToString();
        }

        [HideFromIl2Cpp]
        private string RandomRoom()
        {
            return sCamRooms[sRandom.Next(sCamRooms.Length)];
        }

        private void ResetVisualsToStable()
        {
            if (_text == null) return;
            var rt = _textRectTransform;
            rt.anchoredPosition = _baseAnchoredPosition;
            rt.localRotation = Quaternion.identity;
            rt.localScale = new Vector3(_baseScale, _baseScale, 1f);
            _text.color = _baseColor;
            _text.outlineColor = _baseOutlineColor;
            _text.outlineWidth = _baseOutlineWidth;
            SetText(_modText);
        }

        private void OnDisable()
        {
            _isEffectRunning = false;

            if (_schedulerRoutine != null)
                StopCoroutine(_schedulerRoutine);
            if (_breathingRoutine != null)
                StopCoroutine(_breathingRoutine);

            if (_text != null)
                ResetVisualsToStable();
        }

        private void OnDestroy()
        {
            OnDisable();
            _text = null;
            _textRectTransform = null;
        }
    }
}