using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Isle.Gameplay.Editor
{
    /// <summary>
    /// Generates the placeholder character rig described by SYS-CHAR-01 §Rig.
    ///
    /// ISLE's character is a <b>cutout</b> rig (ART_PIPELINE §Rig: fourteen separate parts), not a
    /// deformed mesh, so every part is a plain SpriteRenderer on its own bone Transform. Nothing
    /// here needs the Skinning Editor, a PSD, or a SpriteSkin — IK Manager 2D's Limb solver drives
    /// Transform chains directly, which is what T-002 attaches to.
    ///
    /// The real <c>player_rig.psd</c> replaces this by importing over the same hierarchy and part
    /// names. Re-run this to regenerate the placeholder from scratch.
    /// </summary>
    public static class PlaceholderRigBuilder
    {
        public const string SpriteDir = "Assets/Art/Characters/Placeholder";
        public const string PrefabPath = "Assets/Prefabs/Characters/player_rig_placeholder.prefab";

        /// <summary>Tile size in pixels (ART_PIPELINE §Style). One world unit is one tile.</summary>
        const float PixelsPerUnit = 32f;

        // ART_PIPELINE §Style: outline is dark brown, never black.
        static readonly Color Outline = new Color32(0x3A, 0x2A, 0x1E, 0xFF);

        enum Tone { Skin, Cloth, Trouser, Boot }

        static readonly Dictionary<Tone, Color> Tones = new Dictionary<Tone, Color>
        {
            { Tone.Skin,    new Color32(0xE0, 0xA8, 0x78, 0xFF) },
            { Tone.Cloth,   new Color32(0xB5, 0x62, 0x3C, 0xFF) },
            { Tone.Trouser, new Color32(0x7A, 0x5A, 0x3C, 0xFF) },
            { Tone.Boot,    new Color32(0x4A, 0x35, 0x24, 0xFF) },
        };

        readonly struct Part
        {
            public readonly string Name, Parent;
            public readonly int W, H;                 // pixels; 0 means a jointless empty Transform
            public readonly float PivotX, PivotY;     // normalised, sits on the joint this part rotates about
            public readonly float LocalX, LocalY;     // pixels, from the parent's pivot
            public readonly int Order;                // sprite sorting order; back limbs negative
            public readonly Tone Tone;

            public Part(string name, string parent, int w, int h,
                        float pivotX, float pivotY, float localX, float localY, int order, Tone tone)
            {
                Name = name; Parent = parent; W = w; H = h;
                PivotX = pivotX; PivotY = pivotY; LocalX = localX; LocalY = localY;
                Order = order; Tone = tone;
            }

            public bool IsSprite => W > 0 && H > 0;
        }

        // Character is 48 px tall — 1.5 tiles (ART_PIPELINE §Style). Segments overlap their parent
        // joint by 1 px so rotation never opens a gap (ART_PIPELINE §Rig).
        // Proportions read off the developer's concept sketch: ~2.5 heads tall, head 20 of the 48 px
        // (ART_PIPELINE: character height ~48 px = 1.5 tiles), thin limbs, no neck.
        // Stacked from the ground: foot 3 + shin 8 + thigh 8 = hip at 17, torso 12 = shoulder line at
        // 29, head 20 tops out at 48. Every joint overlaps its parent by 1 px so rotation never
        // opens a gap (ART_PIPELINE Rig spec).
        static readonly Part[] Parts =
        {
            new Part("Hip",              "Root",             0,  0, 0,    0, 0,   17,  0, Tone.Cloth),

            new Part("Torso",            "Hip",             10, 12, 0.5f, 0, 0,    0,  0, Tone.Cloth),
            // Square, so the rounded-box radius collapses to a circle — the sketch's round head.
            new Part("Head",             "Torso",           20, 20, 0.5f, 0, 0,   11,  2, Tone.Skin),

            new Part("Arm_Back_Upper",   "Torso",            3,  7, 0.5f, 1, 0,   10, -4, Tone.Cloth),
            new Part("Arm_Back_Lower",   "Arm_Back_Upper",   3,  7, 0.5f, 1, 0,   -6, -4, Tone.Cloth),
            new Part("Hand_Back",        "Arm_Back_Lower",   3,  3, 0.5f, 1, 0,   -6, -5, Tone.Skin),

            new Part("Leg_Back_Upper",   "Hip",              4,  8, 0.5f, 1, 0,    0, -3, Tone.Trouser),
            new Part("Leg_Back_Lower",   "Leg_Back_Upper",   4,  8, 0.5f, 1, 0,   -7, -3, Tone.Trouser),
            new Part("Foot_Back",        "Leg_Back_Lower",   6,  3, 0.3f, 1, 0,   -7, -3, Tone.Boot),

            new Part("Leg_Front_Upper",  "Hip",              4,  8, 0.5f, 1, 0,    0,  3, Tone.Trouser),
            new Part("Leg_Front_Lower",  "Leg_Front_Upper",  4,  8, 0.5f, 1, 0,   -7,  3, Tone.Trouser),
            new Part("Foot_Front",       "Leg_Front_Lower",  6,  3, 0.3f, 1, 0,   -7,  3, Tone.Boot),

            new Part("Arm_Front_Upper",  "Torso",            3,  7, 0.5f, 1, 0,   10,  4, Tone.Cloth),
            new Part("Arm_Front_Lower",  "Arm_Front_Upper",  3,  7, 0.5f, 1, 0,   -6,  4, Tone.Cloth),
            new Part("Hand_Front",       "Arm_Front_Lower",  3,  3, 0.5f, 1, 0,   -6,  5, Tone.Skin),

            // SYS-CHAR-01 §Weapon grips: swapping a weapon must only move IK targets, so the socket
            // is an empty Transform the grip binder parents weapons under (T-005).
            new Part("WeaponSocket",     "Hand_Front",       0,  0, 0,    0, 0,   -2,  0, Tone.Skin),
        };

        [MenuItem("ISLE/Rebuild placeholder character rig")]
        public static void Build()
        {
            Directory.CreateDirectory(SpriteDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            foreach (var p in Parts)
            {
                if (p.IsSprite) WriteSprite(p);
            }
            AssetDatabase.Refresh();
            foreach (var p in Parts)
            {
                if (p.IsSprite) ApplyImportSettings(p);
            }

            var root = BuildHierarchy();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"Placeholder rig rebuilt: {PrefabPath}");
        }

        static string SpritePath(in Part p) => $"{SpriteDir}/{p.Name}.png";

        static void WriteSprite(in Part p)
        {
            var tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, mipChain: false);
            var fill = Tones[p.Tone];
            var pixels = new Color[p.W * p.H];

            // Rounded-box signed distance: negative inside. Radius of half the short side gives a
            // capsule, which reads as a limb without needing drawn art.
            float halfW = p.W * 0.5f, halfH = p.H * 0.5f;
            float radius = Mathf.Min(halfW, halfH) - 0.5f;

            for (int y = 0; y < p.H; y++)
            for (int x = 0; x < p.W; x++)
            {
                float px = x + 0.5f - halfW;
                float py = y + 0.5f - halfH;
                float qx = Mathf.Abs(px) - (halfW - radius);
                float qy = Mathf.Abs(py) - (halfH - radius);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                          + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                pixels[y * p.W + x] = d < -1f ? fill
                                    : d < 0f  ? Outline
                                    : Color.clear;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(SpritePath(p), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void ApplyImportSettings(in Part p)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath(p));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            // ART_PIPELINE §Style: pixel perfect is deliberately off — IK on a cutout rig breaks the
            // pixel grid, so limbs filter smoothly rather than snapping.
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(p.PivotX, p.PivotY);
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }

        static GameObject BuildHierarchy()
        {
            var transforms = new Dictionary<string, Transform>();
            var root = new GameObject("Root");
            transforms["Root"] = root.transform;

            foreach (var p in Parts)
            {
                var go = new GameObject(p.Name);
                go.transform.SetParent(transforms[p.Parent], worldPositionStays: false);
                go.transform.localPosition = new Vector3(p.LocalX / PixelsPerUnit, p.LocalY / PixelsPerUnit, 0f);

                if (p.IsSprite)
                {
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath(p));
                    renderer.sortingOrder = p.Order;
                }

                transforms[p.Name] = go.transform;
            }

            return root;
        }
    }
}
