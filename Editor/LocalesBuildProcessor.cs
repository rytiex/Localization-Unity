using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace PicoShot.Localization.Editor
{
    /// <summary>
    /// Post-build processor to copy locales folder to build output.
    /// </summary>
    public class LocalesBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            CopyLocalesToBuild(report.summary.outputPath);
        }

        [MenuItem("Tools/Localization/Copy Locales to Build")]
        private static void ManualCopy()
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
            string sourceLocalesPath = LocalizationManager.LanguagesPath;
            string targetLocalesPath = Path.Combine(buildDirectory, LocalizationManager.LanguagesDirectory);

            if (!Directory.Exists(sourceLocalesPath))
            {
                UnityEngine.Debug.LogWarning($"[LocalesBuildProcessor] No locales folder found at: {sourceLocalesPath}");
                return;
            }

            try
            {
                if (Directory.Exists(targetLocalesPath))
                {
                    Directory.Delete(targetLocalesPath, true);
                }

                Directory.CreateDirectory(targetLocalesPath);

                var files = Directory.GetFiles(sourceLocalesPath, "*.bloc");
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetLocalesPath, fileName);
                    File.Copy(file, destFile, true);
                }

                UnityEngine.Debug.Log($"[LocalesBuildProcessor] Copied {files.Length} locale files to: {targetLocalesPath}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[LocalesBuildProcessor] Failed to copy locales: {ex.Message}");
            }
        }
    }
}
