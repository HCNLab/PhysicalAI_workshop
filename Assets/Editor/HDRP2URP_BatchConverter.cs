// Assets/Editor/HDRP2URP_BatchConverter.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class HDRP2URP_BatchConverter : EditorWindow
{
    [Header("변환 대상 폴더 (비우면 전체 Assets 검색)")]
    public DefaultAsset targetFolder;

    [Header("옵션")]
    public bool dryRun = true;             // 미리보기(실제 저장 X)
    public bool processOnlyPink = false;   // 핑크(셰이더 미적용)만 처리
    public bool keepOriginalShaderNameLog = true;

    const string URP_LIT_SHADER = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/HDRP→URP/Batch Convert Materials")]
    static void Open() => GetWindow<HDRP2URP_BatchConverter>("HDRP→URP Batch Converter");

    void OnGUI()
    {
        GUILayout.Label("HDRP → URP 머티리얼 일괄 변환기", EditorStyles.boldLabel);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
        dryRun = EditorGUILayout.Toggle("Dry Run (미리보기)", dryRun);
        processOnlyPink = EditorGUILayout.Toggle("핑크(Unsupported)만 처리", processOnlyPink);
        keepOriginalShaderNameLog = EditorGUILayout.Toggle("원래 셰이더명 로그", keepOriginalShaderNameLog);

        if (GUILayout.Button("검색 & 변환 실행"))
        {
            Run();
        }

        EditorGUILayout.HelpBox(
@"기능:
• HDRP/Lit 기반 머티리얼을 URP/Lit로 교체
• HDRP Mask Map(R=Metallic, G=AO, A=Smoothness)을
  → URP Metallic(A=Smoothness), URP AO로 자동 재패킹 및 재연결
• Albedo, Normal, Emission도 가능한 범위에서 이관
주의:
• 일부 HDRP 전용 기능(Detail Mask, Parallax, Subsurface 등)은 1:1 매핑 불가
• Dry Run 해보고 실제 적용 추천", MessageType.Info);
    }

    void Run()
    {
        string searchRoot = targetFolder ? AssetDatabase.GetAssetPath(targetFolder) : "Assets";
        var guids = AssetDatabase.FindAssets("t:Material", new[] { searchRoot });
        int total = 0, converted = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            total++;

            // 핑크만 처리 옵션일 때: 현재 셰이더가 unsupported/없음이면 처리
            if (processOnlyPink && !IsPink(mat))
            {
                skipped++;
                continue;
            }

            // HDRP인지 여부(엄격히는 필요없지만 로그/필터링용)
            bool looksHDRP = mat.shader != null && mat.shader.name.StartsWith("HDRP/");
            string originalShaderName = mat.shader != null ? mat.shader.name : "(null)";

            // 변환 시도
            bool changed = ConvertMaterial(mat, path, looksHDRP, originalShaderName);

            if (changed) converted++; else skipped++;
        }

        Debug.Log($"[HDRP→URP] Done. Total: {total}, Converted: {converted}, Skipped: {skipped}");
    }

    bool IsPink(Material m)
    {
        // 이름만으로 판별 어려움. 간단히: 셰이더가 null이거나 Hidden/InternalErrorShader 추정
        return m.shader == null || m.shader.name.Contains("Hidden/InternalError");
    }

    bool ConvertMaterial(Material m, string matPath, bool looksHDRP, string originalShaderName)
    {
        bool changed = false;

        // 원본 텍스처들 추출 (가능한 범위에서 최대 호환)
        // HDRP 주 슬롯
        Texture albedo = TryGet(m, "_BaseColorMap", "_BaseMap", "_MainTex");
        Color baseColor = TryGetColor(m, "_BaseColor", "_Color");
        Texture normal = TryGet(m, "_NormalMap", "_BumpMap");
        Texture mask = TryGet(m, "_MaskMap"); // HDRP: R=Metallic, G=AO, B=DetailMask, A=Smoothness
        Texture emission = TryGet(m, "_EmissiveColorMap", "_EmissionMap");

        Color emissionColor = TryGetColor(m, "_EmissiveColor", "_EmissionColor");
        bool emissionEnabled = m.IsKeywordEnabled("_EMISSION");

        // 재패킹: HDRP Mask → URP Metallic(A=Smoothness) & AO
        Texture2D urpMetallicSmooth = null;
        Texture2D urpAO = null;
        if (mask is Texture2D mask2D)
        {
            RepackMask(mask2D, matPath, out urpMetallicSmooth, out urpAO);
            changed = true;
        }

        // 실제로 URP/Lit로 셰이더 변경
        Shader urpLit = Shader.Find(URP_LIT_SHADER);
        if (urpLit == null)
        {
            Debug.LogError("URP/Lit 셰이더를 찾을 수 없습니다. URP 패키지/파이프라인 설정을 확인하세요.");
            return false;
        }

        if (m.shader != urpLit)
        {
            if (dryRun)
                Debug.Log($"[DRY] {m.name}: Shader {originalShaderName} → {URP_LIT_SHADER}");
            else
                m.shader = urpLit;
            changed = true;
        }

        // 슬롯 재연결
        // Base Map & Color
        if (albedo != null)
        {
            SetTex(m, "_BaseMap", albedo, dryRun);
            changed = true;
        }
        if (baseColor != default)
        {
            SetColor(m, "_BaseColor", baseColor, dryRun);
            changed = true;
        }

        // Normal
        if (normal != null)
        {
            SetTex(m, "_BumpMap", normal, dryRun);
            SetFloat(m, "_BumpScale", Mathf.Max(1f, m.GetFloat("_NormalScale")), dryRun); // 대충 값 이관
            changed = true;
        }

        // Metallic(A=Smoothness)
        if (urpMetallicSmooth != null)
        {
            SetTex(m, "_MetallicGlossMap", urpMetallicSmooth, dryRun);
            // 금속맵 사용 키워드
            SetKeyword(m, "_METALLICSPECGLOSSMAP", true, dryRun);
            // Smoothness 채널: Metallic Alpha(=0)
            SetFloat(m, "_SmoothnessTextureChannel", 0f, dryRun);
            changed = true;
        }

        // AO
        if (urpAO != null)
        {
            SetTex(m, "_OcclusionMap", urpAO, dryRun);
            SetFloat(m, "_OcclusionStrength", 1.0f, dryRun);
            changed = true;
        }

        // Emission
        if (emission != null || emissionColor != default || emissionEnabled)
        {
            if (emission != null) SetTex(m, "_EmissionMap", emission, dryRun);
            if (emissionColor != default) SetColor(m, "_EmissionColor", emissionColor, dryRun);
            SetKeyword(m, "_EMISSION", true, dryRun);
            changed = true;
        }

        // 적용 저장
        if (!dryRun && changed)
        {
            EditorUtility.SetDirty(m);
        }

        if (keepOriginalShaderNameLog && changed)
        {
            Debug.Log($"[HDRP→URP] {m.name} converted. (from: {originalShaderName})");
        }
        return changed;
    }

    Texture TryGet(Material m, params string[] names)
    {
        foreach (var n in names)
            if (m.HasProperty(n))
            {
                var t = m.GetTexture(n);
                if (t != null) return t;
            }
        return null;
    }
    Color TryGetColor(Material m, params string[] names)
    {
        foreach (var n in names)
            if (m.HasProperty(n))
                return m.GetColor(n);
        return default;
    }

    void SetTex(Material m, string prop, Texture t, bool dry)
    {
        if (!m.HasProperty(prop)) return;
        if (dry) { Debug.Log($"[DRY] {m.name}: SetTex {prop} <- {t.name}"); return; }
        m.SetTexture(prop, t);
    }
    void SetColor(Material m, string prop, Color c, bool dry)
    {
        if (!m.HasProperty(prop)) return;
        if (dry) { Debug.Log($"[DRY] {m.name}: SetColor {prop} <- {c}"); return; }
        m.SetColor(prop, c);
    }
    void SetFloat(Material m, string prop, float v, bool dry)
    {
        if (!m.HasProperty(prop)) return;
        if (dry) { Debug.Log($"[DRY] {m.name}: SetFloat {prop} <- {v}"); return; }
        m.SetFloat(prop, v);
    }
    void SetKeyword(Material m, string kw, bool enable, bool dry)
    {
        if (dry) { Debug.Log($"[DRY] {m.name}: {(enable ? "Enable" : "Disable")} {kw}"); return; }
        if (enable) m.EnableKeyword(kw); else m.DisableKeyword(kw);
    }

    void RepackMask(Texture2D src, string matPath, out Texture2D msOut, out Texture2D aoOut)
    {
        // 원본 읽기 가능하도록 임포터 조정
        string path = AssetDatabase.GetAssetPath(src);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool prevReadable = importer.isReadable;
        var prevType = importer.textureType;
        var prevSRGB = importer.sRGBTexture;
        importer.isReadable = true;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();

        int w = src.width, h = src.height;
        var pixels = src.GetPixels32();

        // 1) Metallic(A=Smoothness): R=metallic, A=smoothness
        var msPixels = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte metallic = pixels[i].r;
            byte smooth   = pixels[i].a;
            msPixels[i] = new Color32(metallic, 0, 0, smooth);
        }
        msOut = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        msOut.SetPixels32(msPixels);
        msOut.Apply();

        // 2) AO: G 채널 → 그레이 이미지(R=G=B=AO)
        var aoPixels = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte ao = pixels[i].g;
            aoPixels[i] = new Color32(ao, ao, ao, 255);
        }
        aoOut = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        aoOut.SetPixels32(aoPixels);
        aoOut.Apply();

        // 저장 경로: 머티리얼 옆 폴더
        string dir = Path.GetDirectoryName(matPath);
        if (string.IsNullOrEmpty(dir)) dir = "Assets";
        string baseName = Path.GetFileNameWithoutExtension(path);

        string msPath = Path.Combine(dir, baseName + "_URP_MetallicSmooth.png").Replace("\\", "/");
        string aoPath = Path.Combine(dir, baseName + "_URP_AO.png").Replace("\\", "/");

        File.WriteAllBytes(msPath, msOut.EncodeToPNG());
        File.WriteAllBytes(aoPath, aoOut.EncodeToPNG());

        AssetDatabase.ImportAsset(msPath);
        AssetDatabase.ImportAsset(aoPath);

        // 임포트 옵션: MetallicSmooth은 sRGB off, AO도 sRGB off
        var impMS = (TextureImporter)AssetImporter.GetAtPath(msPath);
        impMS.sRGBTexture = false;
        impMS.alphaSource = TextureImporterAlphaSource.FromInput;
        impMS.textureType = TextureImporterType.Default;
        impMS.SaveAndReimport();

        var impAO = (TextureImporter)AssetImporter.GetAtPath(aoPath);
        impAO.sRGBTexture = false;
        impAO.alphaSource = TextureImporterAlphaSource.FromInput;
        impAO.textureType = TextureImporterType.Default;
        impAO.SaveAndReimport();

        // 원본 복구
        importer.isReadable = prevReadable;
        importer.textureType = prevType;
        importer.sRGBTexture = prevSRGB;
        importer.SaveAndReimport();

        Debug.Log($"[HDRP→URP] Repacked: {msPath}, {aoPath}");
    }
}
