using System.Collections;
using Isle.Core.Ids;
using Isle.Core.Util;
using Isle.Data;
using Isle.Modding.Defs;
using Isle.UI.Art;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Isle.UI.Prototype
{
    /// <summary>
    /// T-153: the title screen the game opens on — New game (seed), Continue (when a save exists), Options, Quit — over
    /// a small animated scene: a stick figure fishing off a beach at dusk beside a campfire. Built at runtime like
    /// everything else (no prefab). Hands off to <see cref="GameSession"/>; holds no game state itself.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        enum Page { Main, NewGame, Load, Options, ConfirmOverwrite, ConfirmDelete, Loading }

        // Layout and backdrop values — presentation only.
        const float ButtonWidth = 280f;
        const float ButtonHeight = 46f;
        static readonly Color Dusk = new(0.96f, 0.72f, 0.5f);
        static readonly Color SeaColour = new(0.27f, 0.5f, 0.72f);
        static readonly Color SandColour = new(0.93f, 0.84f, 0.62f);

        Page _page = Page.Main;
        string _seedText;
        (SaveData Save, System.DateTime Written)?[] _slots = new (SaveData, System.DateTime)?[SaveGame.MaxSlots + 1];
        int _latest, _targetSlot = 1;
        GUIStyle _title, _subtitle;
        StickFigureAnimator _figure;
        VectorMesh _vector;
        Mesh _figureMesh;
        Transform _fire;
        Material _material;
        FigureOutfit _outfit;

        void Start()
        {
            SaveGame.MigrateLegacy();
            RefreshSlots();
            _targetSlot = FirstEmptySlot();
            _seedText = GameSession.RandomSeed().ToString();
            BuildBackdrop();
        }

        void OnDestroy()
        {
            if (_figureMesh != null) Destroy(_figureMesh);
            if (_material != null) Destroy(_material);
        }

        // ------------------------------------------------------------------ backdrop

        void BuildBackdrop()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 4.2f;
                camera.transform.position = new Vector3(0f, 1.8f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Dusk;
            }

            Block("Sea", new Vector2(0f, 0.9f), new Vector2(40f, 2.4f), SeaColour, -20);
            Block("Sun", new Vector2(3.4f, 2.5f), new Vector2(1.6f, 1.6f), new Color(1f, 0.88f, 0.55f), -21, ShapeLibrary.Sprite("default", Color.white));
            Block("Beach", new Vector2(0f, -1.6f), new Vector2(40f, 3.2f), SandColour, -19);

            Prop("palm", new Vector2(-1.8f, -1.2f), 4.2f, new Color(0.3f, 0.6f, 0.3f));
            Prop("palm", new Vector2(6.2f, -0.9f), 3.6f, new Color(0.34f, 0.62f, 0.32f));
            Prop("tree", new Vector2(-7.4f, 0.2f), 3.8f, new Color(0.25f, 0.52f, 0.26f));
            Prop("rock", new Vector2(3.6f, -0.15f), 0.9f, new Color(0.55f, 0.56f, 0.58f));
            _fire = Prop("campfire", new Vector2(0.6f, -0.5f), 0.9f, Color.white).transform;

            // The figure: fishing, line out to a bobber on the water.
            _material = VectorMaterial.Create();
            if (_material == null) return;
            _figure = new StickFigureAnimator();
            _vector = new VectorMesh();
            _figureMesh = new Mesh { name = "MenuFigure" };
            var go = new GameObject("Figure");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(2.3f, -0.35f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = _figureMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = 2;
            var rod = DefRegistry.TryGet<ItemDef>(NamespacedId.Parse("isle:fishing_rod"), out var r) ? r : null;
            _outfit = new FigureOutfit { MainHand = rod };
        }

        void Block(string name, Vector2 at, Vector2 size, Color colour, int order, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite ?? Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            renderer.color = colour;
            renderer.sortingOrder = order;
            go.transform.position = at;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        GameObject Prop(string shape, Vector2 foot, float size, Color colour)
        {
            var go = new GameObject(shape);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ShapeLibrary.StandingSprite(shape, colour);
            renderer.sortingOrder = foot.y > -0.4f ? 1 : 3;
            go.transform.position = foot;
            go.transform.localScale = Vector3.one * size;
            return go;
        }

        void LateUpdate()
        {
            if (_fire != null) _fire.localScale = Vector3.one * (0.9f + 0.05f * Mathf.Sin(Time.time * 11f) + 0.03f * Mathf.Sin(Time.time * 23f));
            if (_figure == null || _figureMesh == null) return;
            // Waits, then a bite every few seconds — the rod jerks.
            var t = Mathf.Repeat(Time.time, 6f);
            var action = t > 4.6f ? FigureAction.Bite : FigureAction.Cast;
            _figure.Step(new FigureInput { FacingTarget = 1f, Action = action, ActionTime = action == FigureAction.Bite ? t - 4.6f : 2f }, Time.deltaTime);
            _vector.Clear();
            var bob = action == FigureAction.Bite ? Mathf.Sin(Time.time * 30f) * 0.05f : Mathf.Sin(Time.time * 2f) * 0.03f;
            StickFigureDrawer.Draw(_vector, _figure.Pose, _outfit, new Vector2(2.6f, 0.95f + bob), Time.time);
            _vector.Fill(_figureMesh);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (_page is Page.NewGame or Page.Options or Page.Load or Page.ConfirmOverwrite or Page.ConfirmDelete) _page = Page.Main;
        }

        // ------------------------------------------------------------------ screen

        void OnGUI()
        {
            var st = UiTheme.Styles;
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _subtitle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.2f, 0.14f, 0.1f) } };

            // Title with a thick ink shadow, cartoon style.
            var titleRect = new Rect(0, Screen.height * 0.1f, Screen.width, 110f);
            _title.normal.textColor = new Color(0.09f, 0.07f, 0.06f);
            foreach (var o in new[] { new Vector2(4, 4), new Vector2(-3, 3), new Vector2(3, -3), new Vector2(-3, -3) })
                GUI.Label(new Rect(titleRect.x + o.x, titleRect.y + o.y, titleRect.width, titleRect.height), "ISLE", _title);
            _title.normal.textColor = new Color(1f, 0.96f, 0.86f);
            GUI.Label(titleRect, "ISLE", _title);
            GUI.Label(new Rect(0, titleRect.yMax, Screen.width, 28f), Lang.Get("@ui.menu_tagline"), _subtitle);

            // Menu column on the left third, so the little scene on the right stays visible.
            var x = Mathf.Max(24f, Screen.width * 0.22f - ButtonWidth * 0.5f);
            var y = Screen.height * 0.42f;
            switch (_page)
            {
                case Page.Main: MainPage(st, x, y); break;
                case Page.NewGame: NewGamePage(st, y); break;
                case Page.ConfirmOverwrite: ConfirmPage(st, y); break;
                case Page.Load: LoadPage(st, y); break;
                case Page.ConfirmDelete: ConfirmDeletePage(st, y); break;
                case Page.Options: OptionsPage(st, y); break;
                case Page.Loading: LoadingPage(st); break;
            }

            GUI.Label(new Rect(12f, Screen.height - 26f, 400f, 20f), Lang.Get("@ui.menu_version"), st.Small);
        }

        void RefreshSlots()
        {
            for (var slot = 1; slot <= SaveGame.MaxSlots; slot++) _slots[slot] = SaveGame.Inspect(slot);
            _latest = SaveGame.LatestSlot();
        }

        int FirstEmptySlot()
        {
            for (var slot = 1; slot <= SaveGame.MaxSlots; slot++)
                if (_slots[slot] == null) return slot;
            return 1;
        }

        string SlotLabel(int slot)
        {
            var info = _slots[slot];
            if (info == null) return $"{slot}.  {Lang.Get("@ui.menu_slot_empty")}";
            var s = info.Value.Save;
            return $"{slot}.  {string.Format(Lang.Get("@ui.menu_save_info"), s.TotalMinutes / 1440 + 1, s.Seed)}   ·   {info.Value.Written:yyyy-MM-dd HH:mm}";
        }

        void MainPage(UiTheme.StyleSet st, float x, float y)
        {
            var panel = new Rect(x - 24f, y - 20f, ButtonWidth + 48f, 5 * (ButtonHeight + 12f) + 44f);
            GUI.Box(panel, GUIContent.none, st.Window);

            // Continue resumes the most recently played slot.
            GUI.enabled = _latest > 0;
            if (GUI.Button(new Rect(x, y, ButtonWidth, ButtonHeight), Lang.Get("@ui.menu_continue"), _latest > 0 ? st.ButtonOn : st.Button))
            {
                var slot = _latest;
                StartLoading(() => GameSession.Continue(slot));
            }
            GUI.enabled = true;
            if (_latest > 0 && _slots[_latest] is { } latest)
                GUI.Label(new Rect(x, y + ButtonHeight - 2f, ButtonWidth, 16f),
                    $"{_latest}. " + string.Format(Lang.Get("@ui.menu_save_info"), latest.Save.TotalMinutes / 1440 + 1, latest.Save.Seed), new GUIStyle(st.Small) { alignment = TextAnchor.UpperCenter });
            y += ButtonHeight + 16f;

            if (GUI.Button(new Rect(x, y, ButtonWidth, ButtonHeight), Lang.Get("@ui.menu_new"), st.Button)) _page = Page.NewGame;
            y += ButtonHeight + 12f;
            GUI.enabled = _latest > 0;
            if (GUI.Button(new Rect(x, y, ButtonWidth, ButtonHeight), Lang.Get("@ui.menu_load"), st.Button)) _page = Page.Load;
            GUI.enabled = true;
            y += ButtonHeight + 12f;
            if (GUI.Button(new Rect(x, y, ButtonWidth, ButtonHeight), Lang.Get("@ui.menu_options"), st.Button)) _page = Page.Options;
            y += ButtonHeight + 12f;
            if (GUI.Button(new Rect(x, y, ButtonWidth, ButtonHeight), Lang.Get("@ui.menu_quit"), st.Button)) Application.Quit();
        }

        const float SlotRow = 34f;

        void NewGamePage(UiTheme.StyleSet st, float y)
        {
            const float width = 520f;
            var x = Mathf.Max(24f, Screen.width * 0.22f - width * 0.5f + 60f);
            y -= 60f;
            var height = 170f + SaveGame.MaxSlots * SlotRow;
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, height), GUIContent.none, st.Window);
            GUI.Label(new Rect(x, y, width, 26f), Lang.Get("@ui.menu_new"), st.Title);
            y += 34f;
            GUI.Label(new Rect(x, y, width, 20f), Lang.Get("@ui.menu_pick_slot"), st.Muted);
            y += 24f;
            for (var slot = 1; slot <= SaveGame.MaxSlots; slot++)
            {
                if (GUI.Button(new Rect(x, y, width, SlotRow - 4f), SlotLabel(slot), slot == _targetSlot ? st.ButtonOn : st.Button)) _targetSlot = slot;
                y += SlotRow;
            }
            y += 8f;
            GUI.Label(new Rect(x, y, 80f, 30f), Lang.Get("@ui.menu_seed"), st.Text);
            _seedText = GUI.TextField(new Rect(x + 70f, y, width - 70f - 110f, 30f), _seedText ?? "", 10);
            if (GUI.Button(new Rect(x + width - 100f, y, 100f, 30f), Lang.Get("@ui.menu_random"), st.Button)) _seedText = GameSession.RandomSeed().ToString();
            y += 40f;
            if (GUI.Button(new Rect(x, y, 140f, ButtonHeight), Lang.Get("@ui.menu_back"), st.Button)) _page = Page.Main;
            if (GUI.Button(new Rect(x + width - 200f, y, 200f, ButtonHeight), Lang.Get("@ui.menu_start"), st.ButtonOn))
            {
                if (_slots[_targetSlot] != null) _page = Page.ConfirmOverwrite;
                else BeginNew();
            }
        }

        void LoadPage(UiTheme.StyleSet st, float y)
        {
            const float width = 560f;
            var x = Mathf.Max(24f, Screen.width * 0.22f - width * 0.5f + 80f);
            y -= 60f;
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 120f + SaveGame.MaxSlots * SlotRow), GUIContent.none, st.Window);
            GUI.Label(new Rect(x, y, width, 26f), Lang.Get("@ui.menu_load"), st.Title);
            y += 38f;
            for (var slot = 1; slot <= SaveGame.MaxSlots; slot++)
            {
                var filled = _slots[slot] != null;
                GUI.enabled = filled;
                if (GUI.Button(new Rect(x, y, width - 90f, SlotRow - 4f), SlotLabel(slot), slot == _latest ? st.ButtonOn : st.Button))
                {
                    var chosen = slot;
                    StartLoading(() => GameSession.Continue(chosen));
                }
                if (GUI.Button(new Rect(x + width - 84f, y, 84f, SlotRow - 4f), Lang.Get("@ui.menu_delete"), st.Button))
                {
                    _targetSlot = slot;
                    _page = Page.ConfirmDelete;
                }
                GUI.enabled = true;
                y += SlotRow;
            }
            y += 10f;
            if (GUI.Button(new Rect(x, y, 140f, ButtonHeight), Lang.Get("@ui.menu_back"), st.Button)) _page = Page.Main;
        }

        void ConfirmPage(UiTheme.StyleSet st, float y)
        {
            const float width = 420f;
            var x = Mathf.Max(24f, Screen.width * 0.22f - width * 0.5f);
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 170f), GUIContent.none, st.Window);
            GUI.Label(new Rect(x, y, width, 60f), string.Format(Lang.Get("@ui.menu_overwrite"), _targetSlot), st.Text);
            y += 76f;
            if (GUI.Button(new Rect(x, y, 160f, ButtonHeight), Lang.Get("@ui.menu_back"), st.Button)) _page = Page.NewGame;
            if (GUI.Button(new Rect(x + width - 200f, y, 200f, ButtonHeight), Lang.Get("@ui.menu_overwrite_yes"), st.ButtonOn)) BeginNew();
        }

        void ConfirmDeletePage(UiTheme.StyleSet st, float y)
        {
            const float width = 420f;
            var x = Mathf.Max(24f, Screen.width * 0.22f - width * 0.5f);
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 170f), GUIContent.none, st.Window);
            GUI.Label(new Rect(x, y, width, 60f), string.Format(Lang.Get("@ui.menu_delete_confirm"), _targetSlot), st.Text);
            y += 76f;
            if (GUI.Button(new Rect(x, y, 160f, ButtonHeight), Lang.Get("@ui.menu_back"), st.Button)) _page = Page.Load;
            if (GUI.Button(new Rect(x + width - 200f, y, 200f, ButtonHeight), Lang.Get("@ui.menu_delete"), st.ButtonOn))
            {
                SaveGame.DeleteFile(_targetSlot);
                RefreshSlots();
                _page = _latest > 0 ? Page.Load : Page.Main;
            }
        }

        void BeginNew()
        {
            var seed = int.TryParse(_seedText, out var s) && s > 0 ? s : Mathf.Abs((_seedText ?? "").GetHashCode()) % int.MaxValue + 1;
            var slot = _targetSlot;
            StartLoading(() => GameSession.StartNew(seed, slot));
        }

        void OptionsPage(UiTheme.StyleSet st, float y)
        {
            const float width = 460f;
            var x = Mathf.Max(24f, Screen.width * 0.22f - width * 0.5f);
            GUI.Box(new Rect(x - 20f, y - 20f, width + 40f, 330f), GUIContent.none, st.Window);
            GUI.Label(new Rect(x, y, width, 26f), Lang.Get("@ui.menu_options"), st.Title);
            y += 40f;

            Row(st, x, ref y, width, Lang.Get("@ui.opt_language"), () =>
            {
                var codes = new[] { "auto", "ko", "en" };
                var labels = new[] { Lang.Get("@ui.opt_language_auto"), "한국어", "English" };
                var current = System.Array.IndexOf(codes, GameOptions.Language);
                var bx = x + 180f;
                for (var i = 0; i < codes.Length; i++)
                {
                    if (GUI.Button(new Rect(bx, y, 88f, 30f), labels[i], i == current ? st.ButtonOn : st.Button)) GameOptions.Language = codes[i];
                    bx += 94f;
                }
            });
            Row(st, x, ref y, width, Lang.Get("@ui.opt_fullscreen"), () =>
            {
                if (GUI.Button(new Rect(x + 180f, y, 120f, 30f), Lang.Get(GameOptions.Fullscreen ? "@ui.opt_on" : "@ui.opt_off"), GameOptions.Fullscreen ? st.ButtonOn : st.Button))
                    GameOptions.Fullscreen = !GameOptions.Fullscreen;
            });
            Row(st, x, ref y, width, Lang.Get("@ui.opt_vsync"), () =>
            {
                if (GUI.Button(new Rect(x + 180f, y, 120f, 30f), Lang.Get(GameOptions.VSync ? "@ui.opt_on" : "@ui.opt_off"), GameOptions.VSync ? st.ButtonOn : st.Button))
                    GameOptions.VSync = !GameOptions.VSync;
            });
            Row(st, x, ref y, width, Lang.Get("@ui.opt_framecap"), () =>
            {
                GUI.enabled = !GameOptions.VSync;
                var cap = GameOptions.FrameCap;
                var label = cap <= 0 ? Lang.Get("@ui.opt_unlimited") : $"{cap} FPS";
                if (GUI.Button(new Rect(x + 180f, y, 120f, 30f), label, st.Button))
                {
                    var i = System.Array.IndexOf(GameOptions.FrameCaps, cap);
                    GameOptions.FrameCap = GameOptions.FrameCaps[(i + 1) % GameOptions.FrameCaps.Length];
                }
                GUI.enabled = true;
            });
            Row(st, x, ref y, width, Lang.Get("@ui.opt_volume"), () =>
            {
                var v = GUI.HorizontalSlider(new Rect(x + 180f, y + 10f, 200f, 20f), GameOptions.Volume, 0f, 1f);
                if (!Mathf.Approximately(v, GameOptions.Volume)) GameOptions.Volume = v;
                GUI.Label(new Rect(x + 390f, y + 4f, 60f, 24f), $"{GameOptions.Volume * 100f:0}%", st.Text);
            });
            y += 10f;
            if (GUI.Button(new Rect(x, y, 140f, ButtonHeight), Lang.Get("@ui.menu_back"), st.Button)) _page = Page.Main;
        }

        static void Row(UiTheme.StyleSet st, float x, ref float y, float width, string label, System.Action control)
        {
            GUI.Label(new Rect(x, y + 4f, 170f, 24f), label, st.Text);
            control();
            y += 42f;
        }

        void LoadingPage(UiTheme.StyleSet st)
        {
            var rect = new Rect((Screen.width - 360f) * 0.5f, Screen.height * 0.5f, 360f, 60f);
            GUI.Box(rect, GUIContent.none, st.Window);
            GUI.Label(rect, Lang.Get("@ui.menu_loading"), new GUIStyle(st.Title) { alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>Shows the loading card for a couple of frames before the (synchronous, ~1–2 s) island build.</summary>
        void StartLoading(System.Action begin)
        {
            _page = Page.Loading;
            StartCoroutine(LoadNextFrames(begin));
        }

        static IEnumerator LoadNextFrames(System.Action begin)
        {
            yield return null;
            yield return null;
            begin();
        }
    }
}
