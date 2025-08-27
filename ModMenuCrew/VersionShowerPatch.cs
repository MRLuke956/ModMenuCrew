using System;
using System.Collections;
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
        private TextMeshPro _text;
        private string _baseText;
        private string _modText;
        private bool _isEffectRunning;
        private bool _isGlitchActive;
        private Vector2 _baseAnchoredPosition;
        private float _baseScale;
        private Color _baseColor;
        private Color _baseOutlineColor;
        private float _baseOutlineWidth;

        // Probabilities and controls
        private float _probPhantom = 0.005f;    // 0.5%
        private float _probMythic = 0.025f;     // 2.5%
        private float _probRare = 0.09f;        // 9%
        private float _probUncommon = 0.23f;    // 23%
        private float _heavyCooldownSeconds = 12f;
        private float _nextHeavyAllowedTime = 0f;
        private static bool sFnaf3BiasEnabled = false;

        // Color palette
        private static readonly Color FnafGreen = new Color(0.6f, 1f, 0.3f);
        private static readonly Color AlertOrange = new Color(1f, 0.5f, 0f);

        // Idle delay
        private float _minIdleDelaySeconds = 3.5f;
        private float _maxIdleDelaySeconds = 7.0f;

        private static readonly System.Random sRandom = new System.Random();
        private static readonly string[] sSystemMessages = { "SUS", "[REDACTED]", "ACCESS DENIED", "SECURITY ALERT", "UNKNOWN SIGNAL", "WHO IS IT?", "NOT THE IMPOSTOR", "VENT ERROR", "AUDIO ERROR", "VIDEO ERROR", "CAM SYS ERROR", "REBOOT ALL", "PLAY AUDIO", "SEALING VENT", "MAP TOGGLE", "SYSTEM FAILURE", "MOTION DETECTED" };
        private static readonly char[] sNoisePool = { '░', '▒', '▓', '█', '▚', '▞', '▙', '▟', '_', '#', '/', '!', '?' };
        private static readonly WaitForSeconds sWaitFrame = new WaitForSeconds(0.016f);
        private static readonly WaitForSeconds sWaitPico = new WaitForSeconds(0.025f);
        private static readonly WaitForSeconds sWaitMicro = new WaitForSeconds(0.04f);
        private static readonly WaitForSeconds sWaitShort = new WaitForSeconds(0.08f);
        private static readonly string[] sCamRooms = { "Electrical", "MedBay", "Security", "Reactor", "O2", "Admin", "Navigation", "Cafeteria", "Storage", "Shields", "Weapons", "Lower Engine", "Upper Engine", "Comms", "Specimen", "Office", "Dropship", "Vault", "Brig", "Records", "Viewing Deck" };

        public VersionShowerFx(IntPtr ptr) : base(ptr) { }

        public void Initialize(TextMeshPro text)
        {
            _text = text;
            _baseText = text.text;
            _modText = $"Mod Menu Crew {ModMenuCrewPlugin.ModVersion}";

            _baseAnchoredPosition = _text.rectTransform.anchoredPosition;
            _baseScale = _text.rectTransform.localScale.x;
            _baseColor = _text.color;
            _baseOutlineColor = _text.outlineColor;
            _baseOutlineWidth = _text.outlineWidth;

            if (!_isEffectRunning)
            {
                _isEffectRunning = true;
                _nextHeavyAllowedTime = Time.time;
                StartCoroutine(GlitchScheduler().WrapToIl2Cpp());
                StartCoroutine(IdleBreathing().WrapToIl2Cpp());
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator GlitchScheduler()
        {
            SetText(_modText);
            while (_isEffectRunning)
            {
                float delay = (float)sRandom.NextDouble() * 2.0f + 1.8f;
                yield return new WaitForSeconds(delay);

                if (_isGlitchActive || _text == null) continue;

                _isGlitchActive = true;

                // Effective probabilities (can be biased in FNAF3 mode)
                float probPhantom = _probPhantom;
                float probMythic = _probMythic;
                float probRare = _probRare;
                float probUncommon = _probUncommon;

                if (sFnaf3BiasEnabled)
                {
                    // Push a bit more towards Rare/Mythic to highlight FNAF-like sequences
                    probMythic += 0.015f;
                    probRare += 0.05f;
                }

                bool heavyAllowed = Time.time >= _nextHeavyAllowedTime;
                double roll = sRandom.NextDouble();
                float t1 = probPhantom;
                float t2 = probPhantom + probMythic;
                float t3 = probPhantom + probMythic + probRare;
                float t4 = probPhantom + probMythic + probRare + probUncommon;

                if (roll < t1)
                {
                    if (heavyAllowed)
                    {
                        _nextHeavyAllowedTime = Time.time + _heavyCooldownSeconds;
                        yield return PhantomTierEvent();
                    }
                    else
                    {
                        yield return RareTierEvent();
                    }
                }
                else if (roll < t2)
                {
                    if (heavyAllowed)
                    {
                        _nextHeavyAllowedTime = Time.time + _heavyCooldownSeconds;
                        yield return MythicTierEvent();
                    }
                    else
                    {
                        yield return RareTierEvent();
                    }
                }
                else if (roll < t3)
                {
                    yield return RareTierEvent();
                }
                else if (roll < t4)
                {
                    yield return UncommonTierEvent();
                }
                else
                {
                    yield return CommonTierEvent();
                }

                _isGlitchActive = false;
                ResetVisualsToStable();
            }
        }

        [HideFromIl2Cpp]
        private IEnumerator IdleBreathing()
        {
            while (_isEffectRunning)
            {
                if (_text == null) yield break;
                if (!_isGlitchActive)
                {
                    var rt = _text.rectTransform;
                    float t = Time.time;
                    float angle = Mathf.Sin(t * 3f) * 0.4f; // micro sway only
                    rt.localRotation = Quaternion.Euler(0, 0, angle);
                    rt.localScale = new Vector3(_baseScale, _baseScale, 1f);
                    rt.anchoredPosition = _baseAnchoredPosition;
                }
                yield return sWaitFrame;
            }
        }

        // --- GERENCIADORES DE EVENTOS POR RARIDADE ---

        private IEnumerator CommonTierEvent()
        {
            switch (sRandom.Next(0, 7))
            {
                case 0: yield return Jitter(0.3f, 1.5f, 2.0f); break;
                case 1: yield return TextCorruption(0.4f, 4, _modText); break;
                case 2: yield return ScanlineFlicker(0.5f); break;
                case 3: yield return VHSStaticBurst(0.35f); break;
                case 4: yield return GreenPulse(0.4f); break;
                case 5: yield return NoiseScroll(0.4f); break;
                case 6: yield return NoiseBurstQuick(0.35f); break;
            }
        }

        private IEnumerator UncommonTierEvent()
        {
            switch (sRandom.Next(0, 10))
            {
                case 0: yield return ChromaticAberration(0.35f, 6f); break;
                case 1: yield return VerticalRoll(0.25f, 30f); break;
                case 2: yield return NoiseRain(0.5f); break;
                case 3: yield return SystemWarning("COMMS ERROR", 0.6f, Color.cyan); break;
                case 4: yield return AfterimageEcho(0.5f, 3); break;
                case 5: yield return Wobble(0.4f, 10f, 2f); break;
                case 6: yield return CameraLabelFlash(0.6f); break;
                case 7: yield return MotionPing(0.6f); break;
                case 8: yield return NoiseScroll(0.45f); break;
                case 9: yield return NoiseHalo(0.5f); break;
            }
        }

        private IEnumerator RareTierEvent()
        {
            switch (sRandom.Next(0, 17))
            {
                case 0: yield return ImpostorFlash(new Color(0.8f, 0.1f, 0.1f)); break;
                case 1: yield return TextCorruption(0.8f, 10, sSystemMessages[sRandom.Next(sSystemMessages.Length)]); break;
                case 2: yield return Jitter(0.5f, 8f, 10f); break;
                case 3: yield return SystemWarning("O2 DEPLETED", 1.0f, Color.yellow); break;
                case 4: yield return SystemWarning("REACTOR MELTDOWN", 1.2f, AlertOrange); break;
                case 5: yield return TrackingNoise(0.7f); break;
                case 6: yield return NoiseFrame(0.6f); break;
                case 7: yield return ColorDrain(0.8f); break;
                case 8: yield return TypewriterText(sSystemMessages[sRandom.Next(sSystemMessages.Length)], 0.05f); break;
                case 9: yield return CharacterSwapGlitch(0.6f); break;
                case 10: yield return BurnInPulse(0.7f); break;
                case 11: yield return HorizontalTear(0.5f); break;
                case 12: yield return CRTCrosstalk(0.6f, 8f); break;
                case 13: yield return MotionPing(0.9f); break;
                case 14: yield return BarcodeNoise(0.6f); break;
                case 15: yield return NoiseHalo(0.7f); break;
                case 16: yield return NoisyTypewriterMessage(0.06f); break;
            }
        }

        private IEnumerator MythicTierEvent()
        {
            switch (sRandom.Next(0, 8))
            {
                case 0: yield return Sequence_EmergencyMeeting(); break;
                case 1: yield return Sequence_ImpostorReveal(); break;
                case 2: yield return Sequence_SecurityFeedLost(); break;
                case 3: yield return Sequence_SystemReboot(); break;
                case 4: yield return Sequence_SabotageCritical(); break;
                case 5: yield return Sequence_VentilationError(); break;
                case 6: yield return Sequence_AudioError(); break;
                case 7: yield return Sequence_RebootAll(); break;
            }
        }

        private IEnumerator PhantomTierEvent()
        {
            switch (sRandom.Next(0, 8))
            {
                case 0: yield return PhantomAppearance("GHOST OF CYAN", Color.cyan); break;
                case 1: yield return PhantomAppearance("EJECTED", Color.white); break;
                case 2: yield return PhantomAppearance("WHERE?", Color.yellow); break;
                case 3: yield return PhantomAppearance("IT WASN'T ME", Color.magenta); break;
                case 4: yield return CrewmateColorCycle(); break;
                case 5: yield return GameCrash(); break;
                case 6: yield return PhantomAppearance("SPRINGTRAP", FnafGreen); break;
                case 7: yield return PhantomSignal(0.7f); break;
            }
        }

        // --- SEQUÊNCIAS MÍTICAS ---

        private IEnumerator Sequence_EmergencyMeeting()
        {
            SetText("", true);
            yield return QuickZoom(5f, 0.1f);
            ResetVisualsToStable();
            yield return TypewriterText("WHO IS THE IMPOSTOR?", 0.06f);
            yield return Jitter(0.5f, 5f, 5f);
            SetText("", true);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator Sequence_ImpostorReveal()
        {
            yield return TypewriterText("THE IMPOSTOR IS...", 0.08f);
            yield return Jitter(0.4f, 15f, 15f);
            yield return ImpostorFlash(Color.red);
            _isGlitchActive = true; // Lock in red
            SetText(CorruptText("IMPOSTOR", 10), true);
            yield return new WaitForSeconds(1.5f);
        }

        private IEnumerator Sequence_SecurityFeedLost()
        {
            yield return ChromaticAberration(0.5f, 15f);
            yield return TrackingNoise(1.0f);
            yield return SystemWarning("SECURITY FEED LOST", 1.2f, Color.white);
            SetText(CorruptText("NO SIGNAL", 10), true);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator Sequence_SystemReboot()
        {
            yield return SystemWarning("SYSTEM RESTART REQUIRED", 1.5f, Color.red);
            SetText("", true);
            yield return new WaitForSeconds(1.0f);
            yield return TypewriterText("REBOOTING...", 0.1f);
            SetText(CorruptText("............", 12), true);
            yield return new WaitForSeconds(0.5f);
        }

        private IEnumerator Sequence_SabotageCritical()
        {
            float duration = 1.5f;
            float endTime = Time.time + duration;
            while (Time.time < endTime)
            {
                float t = 1 - ((endTime - Time.time) / duration);
                Color alertColor = (Time.frameCount % 10 < 5) ? Color.red : Color.yellow;
                _text.color = alertColor;
                _text.outlineColor = Color.red;
                SetText(CorruptText("CRITICAL SABOTAGE", (int)(t * 20)), true);
                yield return Jitter(0.05f, t * 8f, t * 8f);
            }
        }

        // --- EVENTOS FANTASMA ---

        private IEnumerator PhantomAppearance(string name, Color color)
        {
            _text.color = new Color(0, 0, 0, 0);
            _text.outlineWidth = 0;
            color.a = 0.5f;
            string phantomText = $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{CorruptText(name, 5)}</color>";
            for (int i = 0; i < 3; i++)
            {
                SetText(phantomText, true);
                yield return sWaitPico;
                SetText("", true);
                yield return sWaitMicro;
            }
        }

        private IEnumerator CrewmateColorCycle()
        {
            Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta, Color.white };
            for (int i = 0; i < colors.Length * 2; i++)
            {
                _text.color = colors[i % colors.Length];
                yield return sWaitPico;
            }
        }

        private IEnumerator GameCrash()
        {
            _text.color = Color.red;
            _text.outlineColor = Color.red;
            yield return Jitter(0.6f, 20f, 20f);
            gameObject.SetActive(false); // Simulates a crash
            yield return new WaitForSeconds(5f); // Keep it off for a while if the object isn't destroyed
            gameObject.SetActive(true);
        }

        // --- EFEITOS DE GLITCH BÁSICOS ---

        [HideFromIl2Cpp] private void SetText(string content, bool isGlitching = false) { if (_text == null) return; string colorTag = ColorUtility.ToHtmlStringRGB(_baseColor); string displayText = isGlitching ? content : _modText; _text.text = $"{_baseText} <color=#{colorTag}><b><i>{displayText}</i></b></color>"; }
        [HideFromIl2Cpp] private IEnumerator TextCorruption(float d, int i, string t) { float e = Time.time + d; while (Time.time < e) { SetText(CorruptText(t, i), true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator Jitter(float d, float p, float r) { float e = Time.time + d; var rt = _text.rectTransform; while (Time.time < e) { rt.anchoredPosition = _baseAnchoredPosition + new Vector2(((float)sRandom.NextDouble() - 0.5f) * p, ((float)sRandom.NextDouble() - 0.5f) * p); rt.localRotation = Quaternion.Euler(0, 0, ((float)sRandom.NextDouble() - 0.5f) * 2f * r); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator ChromaticAberration(float d, float i) { float e = Time.time + d; while (Time.time < e) { string gR = $"<color=#FF0000AA><pos={((float)sRandom.NextDouble() - 0.5f) * i}%>{_modText}</pos></color>"; string gC = $"<color=#00FFFFAA><pos={((float)sRandom.NextDouble() - 0.5f) * i}%>{_modText}</pos></color>"; SetText(_modText + gR + gC, true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator VerticalRoll(float d, float i) { float e = Time.time + d; var rt = _text.rectTransform; while (Time.time < e) { rt.anchoredPosition = _baseAnchoredPosition + new Vector2(0, Mathf.Sin(Time.time * i) * (i / 1.5f)); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator ImpostorFlash(Color c) { _text.color = c; _text.outlineColor = c * 0.7f; _text.outlineWidth = 0.45f; yield return sWaitShort; }
        [HideFromIl2Cpp] private IEnumerator ScanlineFlicker(float d) { float e = Time.time + d; while (Time.time < e) { string s = "\n<color=#00000022>----- ----- -----</color>"; SetText(_modText + s, true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator SystemWarning(string m, float d, Color c) { _text.color = c; float e = Time.time + d; while (Time.time < e) { SetText(sRandom.Next(0, 2) == 1 ? m : CorruptText(m, 3), true); yield return sWaitShort; } }
        [HideFromIl2Cpp] private IEnumerator QuickZoom(float s, float d) { var rt = _text.rectTransform; Vector3 start = _text.transform.localScale; Vector3 end = new Vector3(_baseScale * s, _baseScale * s, 1f); float t = 0; while (t < d) { rt.localScale = Vector3.Lerp(start, end, t / d); t += Time.deltaTime; yield return null; } rt.localScale = end; }
        [HideFromIl2Cpp] private IEnumerator TrackingNoise(float d) { float e = Time.time + d; while (Time.time < e) { string n = $"\n<mark=#00000044><mspace=1.5em>{CorruptText("               ", 10)}</mspace></mark>"; SetText(_modText + n, true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator ColorDrain(float d) { float e = Time.time + d; while (Time.time < e) { float t = (e - Time.time) / d; _text.color = Color.Lerp(Color.gray, _baseColor, t); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator TypewriterText(string t, float d) { for (int i = 0; i <= t.Length; i++) { if (_text == null) yield break; SetText(t.Substring(0, i), true); yield return new WaitForSeconds(d); } }
        [HideFromIl2Cpp] private IEnumerator CharacterSwapGlitch(float d) { float e = Time.time + d; while (Time.time < e) { var c = _modText.ToCharArray(); int i1 = sRandom.Next(c.Length); int i2 = sRandom.Next(c.Length); (c[i1], c[i2]) = (c[i2], c[i1]); SetText(new string(c), true); yield return sWaitMicro; } }

        // --- EFEITOS FNAF 3 / CRT ADICIONAIS ---
        [HideFromIl2Cpp] private IEnumerator VHSStaticBurst(float d) { float e = Time.time + d; while (Time.time < e) { string n1 = "\n<mark=#00FF0033>████████████████████</mark>"; string n2 = "\n<mark=#00FF001A>████ ███ ███ ███ ███</mark>"; SetText(_modText + n1 + n2, true); yield return sWaitPico; SetText(_modText, true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator CRTCrosstalk(float d, float i) { float e = Time.time + d; while (Time.time < e) { string gY = $"<color=#88FF00AA><pos={(float)(sRandom.NextDouble() - 0.5) * i}%>{_modText}</pos></color>"; string gG = $"<color=#66AA00AA><pos={(float)(sRandom.NextDouble() - 0.5) * i}%>{_modText}</pos></color>"; SetText(_modText + gY + gG, true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator GreenPulse(float d) { float e = Time.time + d; var target = new Color(0.7f, 1f, 0.3f); while (Time.time < e) { float t = Mathf.PingPong(Time.time * 2f, 1f); _text.color = Color.Lerp(_baseColor, target, t); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator AfterimageEcho(float d, int echoes) { float e = Time.time + d; while (Time.time < e) { string combined = _modText; for (int n = 1; n <= echoes; n++) { float off = n * 0.6f; string col = n == echoes ? "#66AA0088" : "#66AA0044"; combined += $"<color={col}><pos={off}%>{_modText}</pos></color>"; } SetText(combined, true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator HorizontalTear(float d) { float e = Time.time + d; while (Time.time < e) { float off = ((float)sRandom.NextDouble() - 0.5f) * 0.4f; string content = $"<voffset={off}em>{_modText}</voffset>"; SetText(content, true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator CameraLabelFlash(float d) { float e = Time.time + d; int cam = sRandom.Next(1, 11); while (Time.time < e) { SetText($"CAM {cam:00}", true); cam = cam % 10 + 1; yield return sWaitShort; } }
        [HideFromIl2Cpp] private IEnumerator BurnInPulse(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; float t = Mathf.PingPong(Time.time * 2f, 1f); _text.outlineWidth = Mathf.Lerp(_baseOutlineWidth, 0.55f, t); _text.outlineColor = Color.Lerp(_baseOutlineColor, FnafGreen, t); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator Wobble(float d, float freq, float amp) { float e = Time.time + d; var rt = _text.rectTransform; while (Time.time < e) { float t = Time.time * freq; rt.anchoredPosition = _baseAnchoredPosition + new Vector2(Mathf.Sin(t) * amp, Mathf.Cos(t * 0.5f) * amp * 0.5f); rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 0.7f) * amp * 0.3f); yield return sWaitFrame; } }
        [HideFromIl2Cpp] private IEnumerator MotionPing(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string room = RandomRoom(); _text.color = Color.yellow; _text.outlineColor = new Color(1f, 0.8f, 0.2f); SetText($"MOTION DETECTED - {room}", true); yield return sWaitShort; SetText(_modText, true); yield return sWaitShort; } }
        [HideFromIl2Cpp] private IEnumerator NoiseScroll(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string n = RandomNoise(24); SetText(_modText + $"\n<mark=#66AA0018><mspace=1.2em>{n}</mspace></mark>", true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator NoiseBurstQuick(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string n = RandomNoise(Mathf.Max(8, _modText.Length)); SetText(n, true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator NoiseRain(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string l1 = RandomNoise(18); string l2 = RandomNoise(18); SetText(_modText + $"\n<color=#66AA0066>{l1}</color>\n<color=#66AA0033>{l2}</color>", true); yield return sWaitPico; } }
        [HideFromIl2Cpp] private IEnumerator NoiseHalo(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string halo = RandomNoise(10); SetText($"{_modText} <color=#88FF0088>{halo}</color>", true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator BarcodeNoise(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string bars = new string('█', sRandom.Next(6, 12)); string gaps = new string('░', sRandom.Next(6, 12)); SetText(_modText + $"\n<color=#00FF0033>{bars}{gaps}{bars}</color>", true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator NoiseFrame(float d) { float e = Time.time + d; while (Time.time < e) { if (_text == null) yield break; string top = RandomNoise(22); string bot = RandomNoise(22); SetText($"<color=#66AA0044>{top}</color>\n{_modText}\n<color=#66AA0044>{bot}</color>", true); yield return sWaitMicro; } }
        [HideFromIl2Cpp] private IEnumerator NoisyTypewriterMessage(float step) { string msg = sSystemMessages[sRandom.Next(sSystemMessages.Length)]; for (int i = 0; i <= msg.Length; i++) { if (_text == null) yield break; string part = msg.Substring(0, i); if (sRandom.NextDouble() < 0.35) part = CorruptText(part, 1); SetText(part, true); yield return new WaitForSeconds(step); } }
        [HideFromIl2Cpp] private IEnumerator PhantomSignal(float d) { float e = Time.time + d; while (Time.time < e) { string sig = $"{RandomNoise(3)} {CorruptText("SIGNAL", 3)} {RandomNoise(3)}"; SetText($"<color=#FFFFFFAA>{sig}</color>", true); yield return sWaitPico; } }

        // --- SEQUÊNCIAS FNAF 3 ---
        private IEnumerator Sequence_VentilationError() { string room = RandomRoom(); yield return GreenPulse(0.6f); yield return SystemWarning($"VENT ERROR - {room}", 1.0f, AlertOrange); yield return Wobble(0.4f, 12f, 2f); yield return TypewriterText($"SEALING VENT - {room}", 0.06f); yield return TypewriterText("REBOOT VENTILATION", 0.06f); yield return BurnInPulse(0.6f); }
        private IEnumerator Sequence_AudioError() { string room = RandomRoom(); yield return SystemWarning("AUDIO ERROR", 1.0f, Color.red); yield return VHSStaticBurst(0.7f); yield return TypewriterText($"PLAY AUDIO - {room}", 0.08f); yield return AfterimageEcho(0.4f, 3); }
        private IEnumerator Sequence_RebootAll() { SetText("", true); yield return SystemWarning("SYSTEM FAILURE", 1.0f, Color.red); yield return CRTCrosstalk(0.6f, 10f); yield return TypewriterText("REBOOT ALL", 0.08f); yield return ChromaticAberration(0.4f, 10f); }

        // --- FUNÇÕES UTILITÁRIAS ---

        private string CorruptText(string input, int passes) { if (string.IsNullOrEmpty(input)) return ""; var c = input.ToCharArray(); for (int k = 0; k < passes; k++) { int i = sRandom.Next(0, c.Length); c[i] = sNoisePool[sRandom.Next(sNoisePool.Length)]; } return new string(c); }
        private string RandomNoise(int length) { if (length <= 0) return ""; var sb = new System.Text.StringBuilder(length); for (int i = 0; i < length; i++) { sb.Append(sNoisePool[sRandom.Next(sNoisePool.Length)]); } return sb.ToString(); }
        private string RandomRoom() { if (sCamRooms == null || sCamRooms.Length == 0) return "Unknown"; return sCamRooms[sRandom.Next(sCamRooms.Length)]; }

        // Controls for external toggle/config
        [HideFromIl2Cpp]
        public static void EnableFnaf3Bias(bool enabled) { sFnaf3BiasEnabled = enabled; }
        [HideFromIl2Cpp]
        public void ConfigureHeavyCooldown(float seconds) { _heavyCooldownSeconds = Mathf.Max(0f, seconds); }
        [HideFromIl2Cpp]
        public void ConfigureIdleDelays(float minSeconds, float maxSeconds)
        {
            _minIdleDelaySeconds = Mathf.Clamp(minSeconds, 0f, 60f);
            _maxIdleDelaySeconds = Mathf.Clamp(Mathf.Max(maxSeconds, _minIdleDelaySeconds), _minIdleDelaySeconds, 120f);
        }

        private void ResetVisualsToStable()
        {
            if (_text == null) return;
            var rt = _text.rectTransform;
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
            StopAllCoroutines();
            if (_text != null && gameObject.activeInHierarchy)
            {
                ResetVisualsToStable();
            }
        }
        private void OnDestroy() => OnDisable();
    }
}