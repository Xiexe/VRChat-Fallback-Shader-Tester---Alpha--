using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;

//reset on new transform
[ExecuteInEditMode]
public class RenderFallbacks : EditorWindow 
{

    [MenuItem("Tools/Xiexe/Fallback Tester")]
    static public void Init()
    {
        RenderFallbacks window = EditorWindow.GetWindow<RenderFallbacks>(true, "Fallback Tester", true);
        window.minSize = new Vector2(400, 300);
        window.maxSize = new Vector2(401, 501);
    }

    public Transform Avatar;

    private enum TrustRankColors {Visitor, NewUser, User, KnownUser, Trusted, Friend};
    private TrustRankColors trustColor;

    private static Renderer[] allRenderers;
    private static Shader[][] currentShader;
    private static Shader[][] oldShader;
    private static Texture[][][] oldTextures;
    private Transform oldAvatar;
    private static bool shadersAreFallbacks = false;

    public void OnGUI()
    {
        doLabel("Shader Fallback Tester");
        SeparatorThin();
        trustColor = (TrustRankColors)EditorGUILayout.EnumPopup("Trust Rank: ", trustColor);

        EditorGUI.BeginChangeCheck();
        Avatar = (Transform)EditorGUILayout.ObjectField(new GUIContent("Avatar","Drag and drop your avatar into this slot."), Avatar, typeof(Transform), true);

        if (oldAvatar != null && shadersAreFallbacks)
        {
            if (EditorGUI.EndChangeCheck())
                RevertFallbacks();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                RevertFallbacks();
        }

        if (Avatar != null)
        {
            SeparatorThin();
            if (!shadersAreFallbacks)
            {
                if (GUILayout.Button("Show Fallback Shaders"))
                {

                    if (!shadersAreFallbacks)
                    {
                        StoreOldTextures();
                        oldAvatar = Avatar;
                        allRenderers = Avatar.GetComponentsInChildren<Renderer>();

                        currentShader = new Shader[allRenderers.Length][];
                        oldShader = new Shader[allRenderers.Length][];

                        for (int i = 0; i < allRenderers.Length; i++)
                        {
                            Renderer r = allRenderers[i];
                            currentShader[i] = new Shader[r.sharedMaterials.Length];
                            oldShader[i] = new Shader[r.sharedMaterials.Length];

                            for(int j = 0; j < r.sharedMaterials.Length; j++)
                            {
                                currentShader[i][j] = r.sharedMaterials[j].shader;
                                oldShader[i][j] = r.sharedMaterials[j].shader;

                                DetermineFallbacks(i, j, r);
                            }
                        }

                        Debug.Log("Showing Fallback Shaders");
                        shadersAreFallbacks = true;
                    }
                    else
                    {
                        Debug.Log("You cannot test fallback shaders on an avatar that already has fallbacks set.");
                    }
                }
            }

            if (shadersAreFallbacks)
            {
                if (GUILayout.Button("Revert Shaders From Fallback"))
                {
                    if (shadersAreFallbacks)
                        RevertFallbacks();
                    else
                        Debug.Log("Shaders aren't set to fallbacks, can't revert.");
                }
            }
        }
        else
        {
            HelpBox("Please drag an Avatar into the Avatar slot to test fallback shaders.", MessageType.Warning);
        }

        HelpBox("The 'Trust Rank' option only changes how the matcap fallback shader is displayed, based on what your rank is.", MessageType.Info);
    }

    private void DetermineFallbacks(int i, int j, Renderer r)
    {
        string currShaderName = currentShader[i][j].name;

        var rankToColors = new Dictionary<TrustRankColors, Color>()
        {
            {TrustRankColors.Visitor, new Color(0.8f, 0.8f, 0.8f, 1)},
            {TrustRankColors.NewUser, new Color(0.09020f, 0.47059f, 1f, 1)},
            {TrustRankColors.User, new Color(0.16863f, .81176f, 0.36078f, 1)},
            {TrustRankColors.KnownUser, new Color(1f, 0.48235f, 0.25882f, 1)},
            {TrustRankColors.Trusted, new Color(0.50588f, 0.26275f, 0.90196f, 1)},
            {TrustRankColors.Friend,  new Color(1f, 1f, 0f, 1)}
        };
        var trustRankColor = rankToColors[trustColor];

        if (r.sharedMaterials[j].HasProperty("_MainTex"))
        {
            r.sharedMaterials[j].shader = Shader.Find("Diffuse"); //default to standard Diffuse

            if (currShaderName.Contains("Cutout") && !currShaderName.Contains("Toon"))
                r.sharedMaterials[j].shader = Shader.Find("Transparent/Cutout/Diffuse");

            if (!currShaderName.Contains("Cutout") && currShaderName.Contains("Toon"))
                r.sharedMaterials[j].shader = Shader.Find("Fallback/Toon/Opaque");

            if (currShaderName.Contains("Cutout") && currShaderName.Contains("Toon"))
                r.sharedMaterials[j].shader = Shader.Find("Fallback/Toon/Cutout");

            if (currShaderName.Contains("Transparent"))
                r.sharedMaterials[j].shader = Shader.Find("Transparent/Diffuse");

            if (currShaderName.Contains("Unlit"))
                r.sharedMaterials[j].shader = Shader.Find("Fallback/Unlit");
        }
        else
        {
            r.sharedMaterials[j].shader = Shader.Find("Fallback/Matcap");
            r.sharedMaterials[j].SetColor("_MatcapColorTintHolyMolyDontReadThis", trustRankColor);
        }
        currentShader[i][j] = r.sharedMaterials[j].shader;
    }

    private void RevertFallbacks()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];
            for (int j = 0; j < r.sharedMaterials.Length; j++)
            {
                r.sharedMaterials[j].shader = oldShader[i][j];
            }
        }

        Debug.Log("Reverted shaders to previous state");
        shadersAreFallbacks = false;
        RestoreTextureSlots();
    }


    private void StoreOldTextures()
    {
        allRenderers = Avatar.GetComponentsInChildren<Renderer>();
        oldTextures = new Texture[allRenderers.Length][][];

        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];

            oldTextures[i] = new Texture[r.sharedMaterials.Length][];
            for (int j = 0; j < r.sharedMaterials.Length; j++)
            {
                Material m = r.sharedMaterials[j];
                Shader s = m.shader;

                oldTextures[i][j] = new Texture[ShaderUtil.GetPropertyCount(s)];
                for (int n = 0; n < ShaderUtil.GetPropertyCount(s); n++)
                {
                    if (ShaderUtil.GetPropertyType(s, n) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propName = ShaderUtil.GetPropertyName(s, n);
                        Texture t = m.GetTexture(propName);
                        oldTextures[i][j][n] = t;
                    }
                }
            }
        }
        Debug.Log("Texture slots stored successfully.");
    }

    private void RestoreTextureSlots()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer r = allRenderers[i];

            for (int j = 0; j < r.sharedMaterials.Length; j++)
            {
                Material m = r.sharedMaterials[j];
                Shader s = m.shader;

                for (int n = 0; n < ShaderUtil.GetPropertyCount(s); n++)
                {
                    if (ShaderUtil.GetPropertyType(s, n) == ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        string propName = ShaderUtil.GetPropertyName(s, n);
                        m.SetTexture(propName, oldTextures[i][j][n]);
                        
                    }
                }
            }
        }
        Debug.Log("Restored texture slots.");
    }

    public void OnDestroy()
    {
        if (oldAvatar != null && shadersAreFallbacks)
            RevertFallbacks();
    }

    // Labels

    public static void doLabel(string text)
    {
        GUILayout.Label(text, new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fontSize = 12
        });
    }

    static public void Separator()
    {
        GUILayout.Space(4);
        GUILine(new Color(.3f, .3f, .3f), 1);
        GUILine(new Color(.9f, .9f, .9f), 1);
        GUILayout.Space(4);
    }

    static public void SeparatorThin()
    {
        GUILayout.Space(2);
        GUILine(new Color(.1f, .1f, .1f), 1f);
        GUILine(new Color(.3f, .3f, .3f), 1f);
        GUILayout.Space(2);
    }

    static public void GUILine(Color color, float height = 2f)
    {
        Rect position = GUILayoutUtility.GetRect(0f, float.MaxValue, height, height, LineStyle);

        if (Event.current.type == EventType.Repaint)
        {
            Color orgColor = GUI.color;
            GUI.color = orgColor * color;
            LineStyle.Draw(position, false, false, false, false);
            GUI.color = orgColor;
        }
    }

    static public GUIStyle _LineStyle;
    static public GUIStyle LineStyle
    {
        get
        {
            if (_LineStyle == null)
            {
                _LineStyle = new GUIStyle();
                _LineStyle.normal.background = EditorGUIUtility.whiteTexture;
                _LineStyle.stretchWidth = true;
            }

            return _LineStyle;
        }
    }

    //Help Box
    public static void HelpBox(string message, MessageType type)
    {
        EditorGUILayout.HelpBox(message, type);
    }
    // ---- 
}
