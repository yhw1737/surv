using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Isle.UI.Art
{
    /// <summary>
    /// An orange outline around whatever E would act on right now (a tree, a station, a loot pile, a carcass). A sprite
    /// gets one copy drawn with <c>Isle/Outline</c>, which paints only the ring outside its shape; a vector figure (a
    /// carcass) gets flat-orange copies nudged out in eight directions behind it. The HUD names the target each frame
    /// (<see cref="Show"/>); with none named for a moment the outline goes. Presentation only.
    /// </summary>
    public sealed class InteractHighlight : MonoBehaviour
    {
        static readonly Color Orange = new(1f, 0.55f, 0.1f, 1f);

        /// <summary>Outline width in tiles. [invented look]</summary>
        const float WidthTiles = 0.05f;

        static GameObject _wanted;
        static int _wantedFrame = -10;

        /// <summary>Called by the HUD every frame it shows an E prompt for <paramref name="target"/>.</summary>
        public static void Show(GameObject target)
        {
            _wanted = target;
            _wantedFrame = Time.frameCount;
        }

        GameObject _current;
        SortingGroup _addedGroup;
        readonly List<(SpriteRenderer Copy, SpriteRenderer Source)> _spriteCopies = new();
        readonly List<GameObject> _copies = new();
        Material _spriteMaterial, _meshMaterial;

        static readonly Vector2[] Directions =
        {
            new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
            new(0.71f, 0.71f), new(-0.71f, 0.71f), new(0.71f, -0.71f), new(-0.71f, -0.71f),
        };

        void LateUpdate()
        {
            var wanted = Time.frameCount - _wantedFrame <= 2 ? _wanted : null;
            if (wanted == null || !wanted.activeInHierarchy) wanted = null;
            // A figure attaches a frame after its creature appears: rebuild when the drawing under the target changes.
            if (wanted != _current || (wanted != null && FigureCount(wanted) != _figures))
            {
                Clear();
                _current = wanted;
                if (wanted != null) Build(wanted);
            }
            // A sprite can change under the outline (a crop grows, a tree is felled).
            foreach (var (copy, source) in _spriteCopies)
            {
                if (copy == null || source == null) continue;
                copy.sprite = source.sprite;
                copy.flipX = source.flipX;
                copy.enabled = source.enabled;
            }
        }

        void OnDestroy()
        {
            Clear();
            if (_spriteMaterial != null) Destroy(_spriteMaterial);
            if (_meshMaterial != null) Destroy(_meshMaterial);
        }

        int _figures;

        static int FigureCount(GameObject target)
        {
            var count = 0;
            foreach (var mesh in target.GetComponentsInChildren<MeshRenderer>())
                if (mesh.name != "Outline" && mesh.enabled) count++;
            return count;
        }

        void Build(GameObject target)
        {
            _figures = FigureCount(target);
            var root = target.GetComponent<SpriteRenderer>();
            var meshes = target.GetComponentsInChildren<MeshRenderer>();
            var order = root != null && root.enabled ? root.sortingOrder : meshes.Length > 0 ? meshes[0].sortingOrder : 0;
            var layer = root != null ? root.sortingLayerID : meshes.Length > 0 ? meshes[0].sortingLayerID : 0;

            // The copies sort just behind their source inside a group that sorts like the target did.
            if (!target.TryGetComponent<SortingGroup>(out _))
            {
                _addedGroup = target.AddComponent<SortingGroup>();
                _addedGroup.sortingOrder = order;
                _addedGroup.sortingLayerID = layer;
            }

            if (root != null && root.enabled && root.sprite != null && Material(ref _spriteMaterial, "Isle/Outline") is { } spriteMaterial)
            {
                // The ring's width in the sprite's uv: the outline width over the sprite's drawn width.
                var drawnWidth = root.sprite.rect.width / root.sprite.pixelsPerUnit * Mathf.Abs(target.transform.lossyScale.x);
                spriteMaterial.SetFloat("_OutlineUV", WidthTiles / Mathf.Max(0.01f, drawnWidth));
                var go = new GameObject("Outline");
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(target.transform, false);
                var copy = go.AddComponent<SpriteRenderer>();
                copy.sprite = root.sprite;
                copy.flipX = root.flipX;
                copy.color = Orange;
                copy.sharedMaterial = spriteMaterial;
                copy.sortingLayerID = root.sortingLayerID;
                copy.sortingOrder = root.sortingOrder + 1; // a ring only, never over the sprite itself
                _spriteCopies.Add((copy, root));
                _copies.Add(go);
            }

            foreach (var mesh in meshes)
            {
                if (!mesh.enabled || !mesh.TryGetComponent<MeshFilter>(out var filter) || mesh.name == "Outline") continue;
                if (_meshMaterial == null)
                {
                    _meshMaterial = VectorMaterial.Create();
                    if (_meshMaterial == null) break;
                    _meshMaterial.SetColor("_Silhouette", Orange);
                }
                var scale = Mathf.Max(0.0001f, Mathf.Abs(mesh.transform.lossyScale.x));
                foreach (var direction in Directions)
                {
                    var go = new GameObject("Outline");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.transform.SetParent(mesh.transform, false);
                    go.transform.localPosition = (Vector3)(direction * (WidthTiles / scale));
                    go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var copy = go.AddComponent<MeshRenderer>();
                    copy.sharedMaterial = _meshMaterial;
                    copy.sortingLayerID = mesh.sortingLayerID;
                    copy.sortingOrder = mesh.sortingOrder - 1;
                    _copies.Add(go);
                }
            }
        }

        static Material Material(ref Material cached, string shaderName)
        {
            if (cached != null) return cached;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Art] shader {shaderName} not found");
                return null;
            }
            return cached = new Material(shader) { name = shaderName };
        }

        void Clear()
        {
            foreach (var go in _copies)
                if (go != null) Destroy(go);
            _copies.Clear();
            _spriteCopies.Clear();
            if (_addedGroup != null) Destroy(_addedGroup);
            _addedGroup = null;
            _current = null;
        }
    }
}
