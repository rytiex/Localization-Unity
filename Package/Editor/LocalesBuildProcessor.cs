using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace PicoShot.Localization.Editor
{
    /// <summary>
    /// Build processor to handle locales folder for different platforms.
    /// </summary>
    public class LocalesBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        /// <summary>
        /// Before build: Copy locales to StreamingAssets for mobile/web platforms.
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (IsStreamingAssetsPlatform(report.summary.platform))
            {
                CopyLocalesToStreamingAssets();
            }
        }

        /// <summary>
        /// After build: Copy locales next to executable for standalone platforms.
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (IsStandalonePlatform(report.summary.platform))
            {
                CopyLocalesToBuild(report.summary.outputPath);
            }
        }

        private static void CopyLocalesToStreamingAssets()
        {
            string sourcePath = LocalizationManager.LanguagesPath;
            string targetPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "Assets",
                "StreamingAssets",
                LocalizationManager.LanguagesDirectory);

            CopyLocales(sourcePath, targetPath, "StreamingAssets");
        }

        [MenuItem("Tools/Localization/Copy Locales to Build")]
        private static void ManualCopyToBuild()
        {
            string buildPath = EditorUtility.OpenFilePanelWithFilters(
                "Select Built Executable",
                "",
                new[] { "Executable", "exe" });

            if (string.IsNullOrEmpty(buildPath))
                return;

            CopyLocalesToBuild(buildPath);
        }

        private static void CopyLocalesToBuild(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
                return;

            string buildDirectory = Path.GetDirectoryName(executablePath);
            string sourcePath = LocalizationManager.LanguagesPath;
            string targetPath = Path.Combine(buildDirectory, LocalizationManager.LanguagesDirectory);

            CopyLocales(sourcePath, targetPath, "build");
        }

        private static void CopyLocales(string sourcePath, string targetPath, string destinationName)
        {
            if (!Directory.Exists(sourcePath))
            {
                UnityEngine.Debug.LogWarning($"[LocalesBuildProcessor] No locales folder found at: {sourcePath}");
                return;
            }

            try
            {
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }

                Directory.CreateDirectory(targetPath);

                var files = Directory.GetFiles(sourcePath, "*.bloc", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string relativePath = file.Substring(sourcePath.Length + 1);
                    string destFile = Path.Combine(targetPath, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, true);
                }

                UnityEngine.Debug.Log($"[LocalesBuildProcessor] Copied {files.Length} locale files to {destinationName}: {targetPath}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalesBuildProcessor] Failed to copy locales: {ex.Message}");
            }
        }

        private static bool IsStandalonePlatform(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows ||
                   target == BuildTarget.StandaloneWindows64 ||
                   target == BuildTarget.StandaloneOSX ||
                   target == BuildTarget.StandaloneLinux64;
        }

        private static bool IsStreamingAssetsPlatform(BuildTarget target)
        {
            return target == BuildTarget.Android ||
                   target == BuildTarget.iOS ||
                   target == BuildTarget.WebGL ||
                   target == BuildTarget.tvOS ||
                   target == BuildTarget.WSAPlayer ||
                   target == BuildTarget.PS4 ||
                   target == BuildTarget.PS5 ||
                   target == BuildTarget.XboxOne ||
                   target == BuildTarget.Switch;
        }
    }
}
