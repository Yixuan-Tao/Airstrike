using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Manual camera renders in a batch-mode Editor need batching disabled for
// reliable per-material constant buffers. Restore all state after capture.
public static class CaptureBootstrap
{
    public static object Capture()
    {
        var camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Main camera is missing.");
        var batching = GraphicsSettings.useScriptableRenderPipelineBatching;
        var target = camera.targetTexture;
        var active = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(1280, 720, 24);
        var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = false;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            texture.Apply();
            var path = Path.GetFullPath("Screenshots/bootstrap.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return new { path, width = 1280, height = 720 };
        }
        finally
        {
            camera.targetTexture = target;
            RenderTexture.active = active;
            GraphicsSettings.useScriptableRenderPipelineBatching = batching;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
