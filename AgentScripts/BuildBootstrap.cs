using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildBootstrap
{
    public static object Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop this project's Play Mode first.");
        var probeType = Type.GetType("Airstrike.BootstrapProbe, Assembly-CSharp");
        if (probeType == null) throw new InvalidOperationException("Compile BootstrapProbe before building the scene.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Art/Materials");
        AssetDatabase.Refresh();
        var soil = Material("Soil", new Color(0.29f, 0.22f, 0.16f));
        var turf = Material("Turf", new Color(0.29f, 0.38f, 0.21f));
        var soldier = Material("Soldier", new Color(0.91f, 0.57f, 0.18f));
        var aircraft = Material("Aircraft", new Color(0.24f, 0.47f, 0.56f));
        var backdrop = Material("Backdrop", new Color(0.13f, 0.22f, 0.28f));
        Primitive("Ground", PrimitiveType.Cube, new Vector3(0, -1, 0), new Vector3(30, 2, 3), soil);
        Primitive("Grass Edge", PrimitiveType.Cube, new Vector3(0, 0.04f, 0), new Vector3(30, 0.08f, 3.02f), turf);
        var player = Primitive("Player Placeholder", PrimitiveType.Capsule, new Vector3(-6, 2.5f, 0), Vector3.one, soldier);
        var body = player.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        var plane = new GameObject("Aircraft Placeholder");
        plane.transform.position = new Vector3(4, 7, 0);
        Part(plane, "Fuselage", new Vector3(0, 0, 0), new Vector3(3.2f, 0.45f, 0.6f), aircraft);
        Part(plane, "Wings", new Vector3(0, 0, 0), new Vector3(0.9f, 0.15f, 3.6f), aircraft);
        Part(plane, "Tail", new Vector3(1.2f, 0.4f, 0), new Vector3(0.5f, 0.8f, 0.15f), aircraft);
        for (var i = 0; i < 7; i++)
            Primitive("Background Block " + i, PrimitiveType.Cube,
                new Vector3(-14 + i * 4.5f, 1.5f + i % 3, 8), new Vector3(3, 4 + i % 3, 2), backdrop);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(3, 7, -25);
        camera.transform.LookAt(new Vector3(0, 3.1f, 0));
        camera.orthographic = true;
        camera.orthographicSize = 8;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.09f, 0.14f);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100;
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.8f;
        light.transform.rotation = Quaternion.Euler(42, -28, 0);
        light.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.6f);
        RenderSettings.fog = false;
        var probe = new GameObject("Bootstrap Verification").AddComponent(probeType);
        probeType.GetField("player").SetValue(probe, player.transform);
        const string scenePath = "Assets/Scenes/Bootstrap.unity";
        if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new IOException("Scene save failed.");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        PlayerSettings.productName = "Airstrike Prototype";
        PlayerSettings.companyName = "Airstrike Development";
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        AssetDatabase.SaveAssets();
        return new { scene = scenePath, saved = !scene.isDirty,
            rootObjects = scene.rootCount, runtimeProbeAttached = probe != null,
            renderer = GraphicsSettings.currentRenderPipeline == null ? "Built-in" : GraphicsSettings.currentRenderPipeline.GetType().Name };
    }

    private static Material Material(string name, Color color)
    {
        var path = "Assets/Art/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void Part(GameObject parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = Primitive(name, PrimitiveType.Cube, Vector3.zero, scale, material);
        part.transform.SetParent(parent.transform, false);
        part.transform.localPosition = position;
    }
}
