using System;
using System.IO;
using Airstrike;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildPrototype
{
    public static object Create()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop this project's Play Mode before rebuilding.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Art/Prototype");
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        var palette=new[]
        {
            Make("Clay",new Color(0.34f,0.24f,0.19f)),
            Make("Turf",new Color(0.37f,0.46f,0.29f)),
            Make("Bedrock",new Color(0.20f,0.19f,0.19f)),
            Make("Amber",new Color(0.98f,0.62f,0.22f)),
            Make("Aircraft",new Color(0.27f,0.39f,0.43f)),
            Make("Red",new Color(0.73f,0.26f,0.21f)),
            Make("Ridge",new Color(0.14f,0.23f,0.27f)),
            Make("Dark",new Color(0.07f,0.12f,0.15f)),
            Make("Flash",new Color(1,0.65f,0.21f),true),
            Make("Sun",new Color(0.84f,0.63f,0.38f),true)
        };
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        camera.tag="MainCamera"; camera.transform.position=new Vector3(-4,7.4f,-32);
        camera.transform.rotation=Quaternion.Euler(8,0,0);
        camera.orthographic=true; camera.orthographicSize=12.5f;
        camera.nearClipPlane=0.1f; camera.farClipPlane=120;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(0.075f,0.13f,0.18f);
        var sun=new GameObject("Warm key light").AddComponent<Light>();
        sun.type=LightType.Directional; sun.intensity=1.6f;
        sun.color=new Color(1,0.89f,0.72f); sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(38,-32,0);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(0.48f,0.57f,0.64f);
        RenderSettings.fog=false;
        var game=new GameObject("Skybreach game").AddComponent<AirstrikeGame>();
        game.Palette=palette; game.MainCamera=camera;
        PlayerSettings.productName="SKYBREACH - Combat Prototype v0.3";
        PlayerSettings.bundleVersion=AirstrikeGame.Version;
        PlayerSettings.companyName="Independent Prototype";
        PlayerSettings.defaultScreenWidth=1600; PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.resizableWindow=true;
        PlayerSettings.runInBackground=false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        const string path="Assets/Scenes/Prototype.unity";
        if(!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save prototype scene.");
        EditorBuildSettings.scenes=new[] { new EditorBuildSettingsScene(path,true) };
        AssetDatabase.SaveAssets();
        return new {scene=path,saved=!scene.isDirty,paletteMaterials=palette.Length,gameplay="runtime procedural world"};
    }

    private static Material Make(string name,Color color,bool unlit=false)
    {
        var path="Assets/Art/Prototype/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
            if(shader==null) throw new InvalidOperationException("Required URP shader is unavailable.");
            material=new Material(shader); AssetDatabase.CreateAsset(material,path);
        }
        material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",0.12f);
        EditorUtility.SetDirty(material); return material;
    }
}
