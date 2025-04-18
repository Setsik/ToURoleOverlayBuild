// ────────────────────────────────────────────────────────────────
// ToU Role Overlay – BepInEx 6 IL2CPP plugin
// • Pokazuje role nad głowami w grze oraz listę ról w lobby
// • Kompatybilne z Among Us 2023/2024 + TownOfUs 5.x + BepInEx 6
// • Kompiluj jako netstandard2.1  (patrz csproj)
// ────────────────────────────────────────────────────────────────

using System;
using System.Reflection;
using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ToURoleOverlay
{
    [BepInPlugin(Id, Name, Version)]
    [BepInProcess("Among Us.exe")]
    public class ToURoleOverlayPlugin : BasePlugin
    {
        public const string Id      = "me.setsik.touroleoverlay";
        public const string Name    = "ToU Role Overlay";
        public const string Version = "1.0.0";

        private Harmony _harmony;

        public override void Load()
        {
            // rejestrujemy Behaviour w domenie IL2CPP
            ClassInjector.RegisterTypeInIl2Cpp<RoleOverlayBehaviour>();

            // trwały obiekt — przeżyje zmianę scen
            var go = new GameObject("ToURoleOverlayObject");
            go.AddComponent<RoleOverlayBehaviour>();
            GameObject.DontDestroyOnLoad(go);

            _harmony = new Harmony(Id);
            _harmony.PatchAll();

            Logger.LogInfo($"{Name} {Version} loaded");
        }

        public override bool Unload()
        {
            _harmony?.UnpatchSelf();
            return true;
        }
    }

    /// <summary>MonoBehaviour odpowiedzialny za rysowanie overlaya.</summary>
    public class RoleOverlayBehaviour : MonoBehaviour
    {
        private GUIStyle _style;

        public RoleOverlayBehaviour(IntPtr ptr) : base(ptr) { }

        void Start()
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize   = 16,
                alignment  = TextAnchor.UpperLeft,
                richText   = true,
                fontStyle  = FontStyle.Bold
            };

            // prosta komenda konsolowa: wpisz „roles” i zobacz listę w logu
            ConsoleCommandHelper.Register("roles", () =>
                BepInEx.Logging.Logger.CreateLogSource("ToURoleOverlay")
                    .LogInfo(GetAllRolesAsString()));
        }

        void OnGUI()
        {
            if (AmongUsClient.Instance == null) return;

            // w lobby – lewa góra; w grze – nad głowami
            if (!AmongUsClient.Instance.InGame)
                DrawLobbyRoleList();
            else
                DrawInGameOverHeads();
        }

        #region Rysowanie

        private void DrawLobbyRoleList()
        {
            float y = 20f;
            GUI.Label(new Rect(15, y, 400, 25),
                      "<color=#FFFF00><b>Role List:</b></color>", _style);
            y += 25;

            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                string role = GetTouRole(pc);
                string col  = ColorUtility.ToHtmlStringRGB(
                              PlayerControl.GameOptions.GetPlayerColor(pc.Data.ColorId));

                GUI.Label(new Rect(15, y, 400, 22),
                          $"<color=#{col}>{pc.name}</color>: {role}", _style);
                y += 20;
            }
        }

        private void DrawInGameOverHeads()
        {
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc?.Data == null) continue;

                Vector3 world = pc.transform.position + Vector3.up * 0.6f;
                Vector3 scr   = Camera.main.WorldToScreenPoint(world);
                if (scr.z < 0) continue;            // za kamerą

                string role = GetTouRole(pc);
                string col  = ColorUtility.ToHtmlStringRGB(
                              PlayerControl.GameOptions.GetPlayerColor(pc.Data.ColorId));

                var rect = new Rect(scr.x - 60,
                                    Screen.height - scr.y - 9,
                                    120, 18);

                GUI.Label(rect, $"<color=#{col}>{role}</color>", _style);
            }
        }

        #endregion


        #region Logika ról

        /// <summary>Próbuje odczytać rolę TownOfUs/vanilla.</summary>
        private string GetTouRole(PlayerControl pc)
        {
            object roleObj = null;

            // 1) TownOfUs – pole Role
            var roleField = pc.GetType().GetField("Role",
                           BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            roleObj = roleField?.GetValue(pc);

            // 2) inne mody – właściwość CustomRole
            if (roleObj == null)
            {
                var prop = pc.GetType().GetProperty("CustomRole",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                roleObj = prop?.GetValue(pc, null);
            }

            // 3) vanilla
            if (roleObj == null)
                return pc.Data?.Role.ToString() ?? "None";

            // "SheriffMod"  → "Sheriff"
            string raw = roleObj.GetType().Name;
            return raw.EndsWith("Mod") ? raw[..^3] : raw;
        }

        private string GetAllRolesAsString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var pc in PlayerControl.AllPlayerControls)
                sb.AppendLine($"{pc.name}: {GetTouRole(pc)}");
            return sb.ToString();
        }

        #endregion
    }

    // ─────────── util: najprostszy rejestrator komend do konsoli Reactor/TownOfUs
    internal static class ConsoleCommandHelper
    {
        private static readonly Harmony h = new("ToURoleOverlay.Console");
        private static readonly System.Collections.Generic.Dictionary<string, Action> cmds = new();

        static ConsoleCommandHelper()
        {
            var awake = AccessTools.Method(typeof(InnerNet.InnerNetClient), "Awake");
            h.Patch(awake, postfix: new HarmonyMethod(typeof(ConsoleCommandHelper), nameof(Postfix)));
        }

        internal static void Register(string name, Action act)
        {
            if (!cmds.ContainsKey(name)) cmds.Add(name, act);
        }

        private static void Postfix()
        {
            foreach (var kv in cmds) ConsoleCommands.AddCommand(kv.Key, kv.Value);
        }
    }
}
