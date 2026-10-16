// What an Apple build needs before it can go to TestFlight: the icons (Apple TV's layered app icon and
// Top Shelf images, the square icon for iPhone, iPad and Mac), a build number that grows with every
// upload, the signing team, and the Info.plist line that says the game uses no encryption of its own.
// Adapted from The Black Bird's. BuildTools.Apple calls Prepare; the post-process runs on the Xcode project.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Schofield.EditorTools
{
    public static class AppleRelease
    {
        const string Icons = "Assets/Schofield/Icons/";

        /// The paid developer team (developer.apple.com ▸ Membership), from Tools/apple/team.txt
        public static string Team
        {
            get
            {
                var f = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "apple", "team.txt");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : "";
            }
        }

        /// A build number that only grows: the minute it was built, in UTC (yyMMddHHmm).
        public static string BuildNumber => DateTime.UtcNow.ToString("yyMMddHHmm");

        public static void Prepare(BuildTarget target)
        {
            var team = Team;
            if (!string.IsNullOrEmpty(team))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }
            ApplePlugins();
            if (target == BuildTarget.tvOS) TvIcons();
            else SquareIcons(target == BuildTarget.iOS ? NamedBuildTarget.iOS : NamedBuildTarget.Standalone);
        }

        // the native audio-session plugin goes into iPhone/iPad and Apple TV builds only
        static void ApplePlugins()
        {
            const string path = "Assets/Schofield/Plugins/Apple/SchofieldAudio.mm";
            var imp = AssetImporter.GetAtPath(path) as PluginImporter;
            if (!imp) { Debug.LogError("[Schofield] Missing plugin " + path); return; }
            if (imp.GetCompatibleWithPlatform(BuildTarget.iOS) && imp.GetCompatibleWithPlatform(BuildTarget.tvOS) && !imp.GetCompatibleWithAnyPlatform()) return;
            imp.SetCompatibleWithAnyPlatform(false);
            imp.SetCompatibleWithEditor(false);
            imp.SetCompatibleWithPlatform(BuildTarget.iOS, true);
            imp.SetCompatibleWithPlatform(BuildTarget.tvOS, true);
            imp.SaveAndReimport();
        }

        static Texture2D Tex(string file)
        {
            var path = Icons + file;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp && (imp.npotScale != TextureImporterNPOTScale.None || imp.mipmapEnabled || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.maxTextureSize < 8192))
            {
                imp.textureType = TextureImporterType.Default;
                imp.npotScale = TextureImporterNPOTScale.None; imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 8192;
                imp.alphaIsTransparency = file.Contains("front");
                imp.SaveAndReimport();
            }
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!t) Debug.LogError("[Schofield] Missing icon art: " + path);
            return t;
        }

        static void TvIcons()
        {
            var nb = NamedBuildTarget.tvOS;
            Texture2D back = Tex("tv_icon_back.png"), front = Tex("tv_icon_front.png"), shelf = Tex("tv_topshelf.png"), wide = Tex("tv_topshelf_wide.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(nb))
            {
                var icons = PlayerSettings.GetPlatformIcons(nb, kind);
                string k = kind.ToString().ToLowerInvariant();
                foreach (var ic in icons)
                {
                    if (k.Contains("shelf")) ic.SetTexture(k.Contains("wide") ? wide : shelf);
                    else
                    {
                        int n = Mathf.Max(2, ic.minLayerCount);
                        var layers = new Texture2D[n];
                        // Xcode's image stacks run front to back: the last layer is the opaque background
                        for (int i = 0; i < n - 1; i++) layers[i] = front;
                        layers[n - 1] = back;
                        ic.SetTextures(layers);
                    }
                }
                PlayerSettings.SetPlatformIcons(nb, kind, icons);
            }
        }

        static void SquareIcons(NamedBuildTarget nb)
        {
            var sq = Tex("icon_1024.png");
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(nb))
            {
                var icons = PlayerSettings.GetPlatformIcons(nb, kind);
                foreach (var ic in icons) ic.SetTexture(sq);
                PlayerSettings.SetPlatformIcons(nb, kind, icons);
            }
        }

        [PostProcessBuild(100)]
        public static void OnBuilt(BuildTarget target, string path)
        {
            if (target != BuildTarget.tvOS && target != BuildTarget.iOS) return;
            var plist = Path.Combine(path, "Info.plist");
            if (!File.Exists(plist)) return;
            var s = File.ReadAllText(plist);
            s = SetKey(s, "ITSAppUsesNonExemptEncryption", "<false/>");
            s = SetKey(s, "CFBundleVersion", $"<string>{BuildNumber}</string>");
            if (target == BuildTarget.tvOS) s = SetKey(s, "GCSupportsControllerUserInteraction", "<true/>");
            File.WriteAllText(plist, s);
            // Unity's prebuilt UnityRuntime.framework claims an older minimum OS than the app's own; App
            // Store Connect rejects the mismatch (ITMS-90208)
            var min = target == BuildTarget.tvOS ? PlayerSettings.tvOS.targetOSVersionString : PlayerSettings.iOS.targetOSVersionString;
            var fwDir = Path.Combine(path, "Frameworks");
            if (Directory.Exists(fwDir))
                foreach (var fw in Directory.GetFiles(fwDir, "Info.plist", SearchOption.AllDirectories))
                {
                    if (!fw.Contains(".framework")) continue;
                    var t = File.ReadAllText(fw);
                    if (!t.Contains("<key>MinimumOSVersion</key>")) continue;
                    File.WriteAllText(fw, SetKey(t, "MinimumOSVersion", $"<string>{min}</string>"));
                }
            Debug.Log($"[Schofield] Info.plist: build {BuildNumber}, no non-exempt encryption.");
        }

        // replace a key's value in a plain XML plist, or add the key before the closing </dict>
        static string SetKey(string plist, string key, string valueXml)
        {
            var tag = $"<key>{key}</key>";
            int i = plist.IndexOf(tag, StringComparison.Ordinal);
            if (i >= 0)
            {
                int v0 = plist.IndexOf('<', i + tag.Length);
                int close = plist.IndexOf('>', v0);
                int v1 = plist[close - 1] == '/' ? close + 1 : plist.IndexOf('>', plist.IndexOf("</", close, StringComparison.Ordinal)) + 1;
                return plist.Substring(0, v0) + valueXml + plist.Substring(v1);
            }
            int end = plist.LastIndexOf("</dict>", StringComparison.Ordinal);
            return plist.Substring(0, end) + $"\t{tag}\n\t{valueXml}\n" + plist.Substring(end);
        }
    }
}
