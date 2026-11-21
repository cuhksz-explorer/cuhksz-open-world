#if UNITY_EDITOR
using System;
using System.IO;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using UnityEditor;
using UnityEngine;

/// <summary>
/// lib2f：Unity 内置 Blender 无法转换该 .blend，需用本机 Blender 导出 FBX 后再导入。
/// </summary>
public static class LibraryFloor2BlendExport
{
    public const string BlendAssetPath = "Assets/scene/lib2f.blend";
    public const string FbxAssetPath = "Assets/scene/lib2f.fbx";

    static readonly string[] BlenderCandidates =
    {
        @"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
        @"D:\Blender\Blender4.0.1(64bit)\Blender4.0.1(64bit)\blender-4.0.1\blender.exe",
        @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe",
        @"C:\Program Files\Blender Foundation\Blender 4.1\blender.exe",
        @"C:\Program Files\Blender Foundation\Blender 4.0\blender.exe",
    };

    const string MenuExport = "Tools/场景/libraryfloor2 - 从 blend 导出 FBX（需 Blender）";

    [MenuItem(MenuExport, false, 47)]
    public static void ExportFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("libraryfloor2", "请先退出 Play Mode。", "确定");
            return;
        }

        try
        {
            ExportFbx(force: true);
            AssetDatabase.ImportAsset(FbxAssetPath, ImportAssetOptions.ForceUpdate);
            EditorUtility.DisplayDialog("libraryfloor2", $"已导出：{FbxAssetPath}", "确定");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogException(ex);
            EditorUtility.DisplayDialog("libraryfloor2", ex.Message, "确定");
        }
    }

    public static void EnsureFbx(bool forceReexport = false)
    {
        string fbxFull = GetFullPath(FbxAssetPath);
        if (!forceReexport && File.Exists(fbxFull) && new FileInfo(fbxFull).Length > 1024 * 1024)
            return;

        ExportFbx(force: forceReexport);
        AssetDatabase.ImportAsset(FbxAssetPath, ImportAssetOptions.ForceUpdate);
    }

    public static void ExportFbx(bool force)
    {
        string blendFull = ResolveBlendFullPath();
        if (!File.Exists(blendFull))
            throw new FileNotFoundException($"未找到 blend：{blendFull}");

        string fbxFull = GetFullPath(FbxAssetPath);
        string fbxDir = Path.GetDirectoryName(fbxFull);
        if (!string.IsNullOrEmpty(fbxDir) && !Directory.Exists(fbxDir))
            Directory.CreateDirectory(fbxDir);

        if (!force && File.Exists(fbxFull))
        {
            var blendTime = File.GetLastWriteTimeUtc(blendFull);
            var fbxTime = File.GetLastWriteTimeUtc(fbxFull);
            if (fbxTime >= blendTime && new FileInfo(fbxFull).Length > 1024 * 1024)
                return;
        }

        string blender = FindBlenderExecutable();
        if (string.IsNullOrEmpty(blender))
            throw new InvalidOperationException("未找到 Blender。请安装 Blender 4.x/5.x，或在 Edit > Preferences > Autodesk FBX / Blender 中配置路径。");

        string scriptPath = Path.Combine(GetProjectRoot(), "Logs", "export_lib2f_editor.py");
        WriteExportScript(scriptPath, blendFull, fbxFull);

        var psi = new ProcessStartInfo
        {
            FileName = blender,
            Arguments = $"--background --python \"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        EditorUtility.DisplayProgressBar("libraryfloor2", "Blender 正在导出 FBX（约 1–3 分钟）…", 0.35f);
        try
        {
            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("无法启动 Blender 进程。");

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(stdout))
                UnityEngine.Debug.Log(stdout);
            if (!string.IsNullOrEmpty(stderr))
                UnityEngine.Debug.Log(stderr);

            if (process.ExitCode != 0 || !File.Exists(fbxFull))
                throw new InvalidOperationException(
                    $"Blender 导出失败（退出码 {process.ExitCode}）。\n请查看 Console，或手动用 Blender 打开 lib2f.blend 导出为 Assets/scene/lib2f.fbx。");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        UnityEngine.Debug.Log($"[libraryfloor2] 已导出 FBX：{FbxAssetPath}");
    }

    static void WriteExportScript(string scriptPath, string blendFull, string fbxFull)
    {
        string dir = Path.GetDirectoryName(scriptPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string blendEsc = blendFull.Replace("\\", "\\\\");
        string fbxEsc = fbxFull.Replace("\\", "\\\\");
        string content =
            "import bpy\n" +
            $"bpy.ops.wm.open_mainfile(filepath=r\"{blendEsc}\")\n" +
            "for obj in bpy.data.objects:\n" +
            "    obj.select_set(False)\n" +
            "for obj in bpy.data.objects:\n" +
            "    if obj.type == 'MESH':\n" +
            "        obj.select_set(True)\n" +
            $"bpy.ops.export_scene.fbx(\n" +
            $"    filepath=r\"{fbxEsc}\",\n" +
            "    use_selection=False,\n" +
            "    apply_scale_options='FBX_SCALE_ALL',\n" +
            "    path_mode='COPY',\n" +
            "    embed_textures=True,\n" +
            "    mesh_smooth_type='FACE',\n" +
            "    use_mesh_modifiers=True,\n" +
            "    add_leaf_bones=False,\n" +
            "    bake_anim=False,\n" +
            ")\n" +
            "print('EXPORT_OK')\n";
        File.WriteAllText(scriptPath, content);
    }

    static string ResolveBlendFullPath()
    {
        string assetsBlend = GetFullPath(BlendAssetPath);
        if (File.Exists(assetsBlend))
            return assetsBlend;

        if (File.Exists(LibraryFloor2SceneSetup.SourceBlendPath))
            return LibraryFloor2SceneSetup.SourceBlendPath;

        throw new FileNotFoundException($"未找到 {BlendAssetPath} 或微信源文件。");
    }

    static string FindBlenderExecutable()
    {
        foreach (string path in BlenderCandidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static string GetProjectRoot() => Path.GetDirectoryName(Application.dataPath);

    static string GetFullPath(string assetPath) =>
        Path.Combine(GetProjectRoot(), assetPath.Replace('/', Path.DirectorySeparatorChar));
}
#endif
