using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Airstrike
{
    // A development-only scene probe, not the game's player controller.
    public sealed class BootstrapProbe : MonoBehaviour
    {
        public Transform player;
        public int updateCount;
        public float elapsedSeconds;
        private string reportPath;
        private bool finished;

        [Serializable]
        private sealed class SmokeReport
        {
            public bool passed;
            public string unityVersion;
            public string scene;
            public bool isEditor;
            public int updateCount;
            public float elapsedSeconds;
            public float playerY;
        }

        private void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--bootstrap-report") reportPath = args[i + 1];
            if (!string.IsNullOrEmpty(reportPath)) Application.targetFrameRate = 60;
        }

        private void Update()
        {
            updateCount++;
            elapsedSeconds += Time.unscaledDeltaTime;
            if (finished || string.IsNullOrEmpty(reportPath)) return;
            if (elapsedSeconds < 2f || updateCount < 90) return;
            finished = true;
            var playerY = player == null ? float.NaN : player.position.y;
            var passed = player != null && playerY > 0.8f && playerY < 1.2f;
            var report = new SmokeReport
            {
                passed = passed,
                unityVersion = Application.unityVersion,
                scene = SceneManager.GetActiveScene().name,
                isEditor = Application.isEditor,
                updateCount = updateCount,
                elapsedSeconds = elapsedSeconds,
                playerY = playerY
            };
            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("AIRSTRIKE_BOOTSTRAP_SMOKE=" + (passed ? "PASS" : "FAIL"));
#if !UNITY_EDITOR
            Application.Quit(passed ? 0 : 1);
#endif
        }
    }
}
