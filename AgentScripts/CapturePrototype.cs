using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public static class CapturePrototype
{
    public static object Capture()
    {
        var camera=Camera.main;
        if(camera==null) throw new InvalidOperationException("No main camera.");
        var batching=GraphicsSettings.useScriptableRenderPipelineBatching;
        var target=camera.targetTexture; var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1600,900,24);
        var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);
        try
        {
            GraphicsSettings.useScriptableRenderPipelineBatching=false;
            camera.targetTexture=rt;
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active=rt;
            texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply();
            var path=Path.GetFullPath("Screenshots/skybreach.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,texture.EncodeToPNG());
            return new {path,width=1600,height=900};
        }
        finally
        {
            camera.targetTexture=target; RenderTexture.active=active;
            GraphicsSettings.useScriptableRenderPipelineBatching=batching;
            RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
