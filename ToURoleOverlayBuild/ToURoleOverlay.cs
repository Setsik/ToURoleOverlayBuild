// ToU Role Overlay – BepInEx IL2CPP plugin
// Shows each player's real Town of Us role above menu / anywhere using OnGUI.
// Tested with Among Us v2023.11.x + ToU v5.2.1 + BepInEx 6.0-pre.
// ---------------------------------------------------------------
// Build notes:
//  * Target framework: .NET Framework 4.7.2
//  * References:
//      - BepInEx.dll             (from BepInEx/core)
//      - BepInEx.IL2CPP.dll      (from BepInEx/core)
//      - HarmonyLib.dll          (from BepInEx/core)
//      - UnityEngine.CoreModule.dll (from Among Us/Among Us_Data/Managed)
//      - TownOfUs.dll            (from ToU/BepInEx/plugins)
//  * Output: Place resulting DLL in   BepInEx/plugins/
// ---------------------------------------------------------------

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
        private Harmony _harmony;

        public override void Load()
        {
            // Register custom MonoBehaviour in IL2CPP domain
            ClassInjector.RegisterTypeInIl2Cpp<RoleOverlayBehaviour>();
            // Create a persistent object so behaviour survives scene loads
            var go = new GameObject("ToURoleOverlayObject");
            go.AddComponent<RoleOverlayBehaviour>();
            GameObject.DontDestroyOnLoad(go);

            _harmony = new Harmony("me.yourname.touroles");
            _harmony.PatchAll();

            Logger.LogInfo("[ToURoleOverlay] Loaded");
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

        public RoleOverlayBehaviour(IntPtr ptr) : base(ptr) { }

        void Start()
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
        }

        void OnGUI()
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.InGame) return;

            float y = 15f;
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                string roleName = GetTouRole(pc);
                string color = pc.Data != null ? ColorUtility.ToHtmlStringRGB(PlayerControl.GameOptions.GetPlayerColor(pc.Data.ColorId)) : "FFFFFF";
                GUI.Label(new Rect(15f, y, 400f, 22f), $"<color=#{color}>{pc.name}: {roleName}</color>", _style);
                y += 20f;
            }
        }

        private string GetTouRole(PlayerControl pc)
        {
            // Try TownOfUs custom Role field first
            FieldInfo roleField = pc.GetType().GetField("Role", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (roleField != null)
            {
                object roleObj = roleField.GetValue(pc);
                if (roleObj != null)
                {
                    string rawName = roleObj.GetType().Name;
                    return rawName.EndsWith("Mod") ? rawName.Substring(0, rawName.Length - 3) : rawName;
                }
            }

            // Fallback to vanilla
            return pc.Data?.Role.ToString() ?? "Unknown";
        }
    }
}
