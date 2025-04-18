// -----------------------------------------------------------------------------
//  ToU Role Overlay – BepInEx IL2CPP plugin
//  Pokazuje prawdziwe role ToU wszystkich graczy (GUI / menu / podczas gry).
//  Solo‑test: log do BepInEx + napis „Overlay ACTIVE” widoczny zawsze.
//  Testowane: Among Us v2023.11.x  •  ToU v5.2.1  •  BepInEx 6‑preview
// -----------------------------------------------------------------------------

using System;
using System.Reflection;
using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ToURoleOverlay
{
    [BepInPlugin("me.yourname.touroles", "ToU Role Overlay", "1.0.0")]
    [BepInProcess("Among Us.exe")]
    public class ToURoleOverlayPlugin : BasePlugin
    {
        public static ManualLogSource Log;   // <-- udostępniamy logger dla innych klas
        private Harmony _harmony;

        public override void Load()
        {
            Log = Logger;                    // zapamiętujemy logger

            // Rejestrujemy własny MonoBehaviour w domenie IL2CPP
            ClassInjector.RegisterTypeInIl2Cpp<RoleOverlayBehaviour>();

            // Tworzymy nieśmiertelny GameObject z naszym Behaviour
            var go = new GameObject("ToURoleOverlayObject");
            go.AddComponent<RoleOverlayBehaviour>();
            GameObject.DontDestroyOnLoad(go);

            _harmony = new Harmony("me.yourname.touroles");
            _harmony.PatchAll();

            Logger.LogInfo("✅ ToURoleOverlay successfully loaded!");
        }

        public override bool Unload()
        {
            _harmony.UnpatchSelf();
            return true;
        }
    }

    public class RoleOverlayBehaviour : MonoBehaviour
    {
        private GUIStyle _style;
        private float _nextLogTime = 0f;   // do ograniczenia spamowania logu

        public RoleOverlayBehaviour(IntPtr ptr) : base(ptr) { }

        void Start()
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 16,
                alignment  = TextAnchor.UpperLeft,
                richText   = true
            };
        }

        void OnGUI()
        {
            // Solo‑test: log i napis "Overlay ACTIVE" → widać nawet w menu/lobby
            if (Time.time >= _nextLogTime)
            {
                ToURoleOverlayPlugin.Log.LogInfo("[ToURoleOverlay] Overlay ACTIVE – OnGUI tick");
                _nextLogTime = Time.time + 10f;   // loguj co 10 sekund
            }
            GUI.Label(new Rect(10f, 10f, 300f, 22f),
                      "<color=#00FF00><b>Overlay ACTIVE</b></color>",
                      _style);

            // Jeśli nie jesteśmy w grze – nie pokazuj listy ról
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.InGame)
                return;

            float y = 35f;   // zaczynamy listę trochę niżej
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                string roleName = GetTouRole(pc);
                string color = pc.Data != null
                    ? ColorUtility.ToHtmlStringRGB(PlayerControl.GameOptions.GetPlayerColor(pc.Data.ColorId))
                    : "FFFFFF";

                GUI.Label(new Rect(15f, y, 600f, 22f),
                          $"<color=#{color}>{pc.name}: {roleName}</color>",
                          _style);
                y += 20f;
            }
        }

        // Próbuje najpierw customowego pola Role (ToU),
        // a gdyby coś poszło nie tak – zwraca rolę z vanilli
        private string GetTouRole(PlayerControl pc)
        {
            FieldInfo roleField = pc.GetType().GetField("Role",
                                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (roleField != null)
            {
                object roleObj = roleField.GetValue(pc);
                if (roleObj != null)
                {
                    string rawName = roleObj.GetType().Name;
                    return rawName.EndsWith("Mod") ? rawName[..^3] : rawName;
                }
            }
            return pc.Data?.Role.ToString() ?? "Unknown";
        }
    }
}
