using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Events;
using System.Diagnostics;
using System.Text;
using System.Reflection;
using Crusaders30XX.ECS.Utils;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// System to render a simple toggleable debug menu and its buttons
    /// </summary>
    [DebugTab("Debug Menu")]
    public class DebugMenuSystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.ContentFont;
        private readonly SystemManager _systemManager;
        private Texture2D _pixel;
        private PlayerInputFrame _inputFrame;
        private readonly List<Action> _drawCommands = new();
        private DateTime _lastDrawTime = DateTime.UtcNow;
        private float _scrollOffset = 0f; // vertical scroll for panel content
        private float _dropdownScrollOffset = 0f; // vertical scroll for open dropdown list
        private bool _dragging = false;
        private Point _dragOffset;

        // Cache invalidation triggers
        private string _systemsSignatureSnapshot = string.Empty;
        private SceneId _lastSceneId = SceneId.TitleMenu;

        // TODO: Caches populate on first use; if systems are added/removed at runtime or their reflected members change dynamically, we can add an explicit invalidation later.
        
		// Caches to avoid repeated reflection and list building every frame
		private List<(string name, Core.System sys)> _annotatedSystemsCache;
		private readonly Dictionary<Type, List<(string label, Func<object> get, Action<object> set, Type type, DebugEditableAttribute attr)>> _editableMembersCache
			= new();
		private readonly Dictionary<Type, List<(string label, MethodInfo method)>> _debugActionsCache
			= new();
		private readonly Dictionary<Type, List<(string label, MethodInfo method, DebugActionIntAttribute meta, int current)>> _debugActionsIntCache
			= new();
		private readonly Dictionary<Type, List<(string label, MethodInfo method)>> _debugActionListsCache
			= new();
		private readonly Dictionary<Type, Dictionary<string, object>> _bootSnapshots = new();

        // Editable layout and behavior settings
		[DebugEditable(DisplayName = "Margin", Step = 1, Min = 0, Max = 200)]
		public int Margin { get; set; } = 74;

		[DebugEditable(DisplayName = "Panel Width", Step = 10, Min = 200, Max = 1000)]
		public int PanelWidth { get; set; } = 500;

		[DebugEditable(DisplayName = "Padding", Step = 1, Min = 0, Max = 64)]
		public int Padding { get; set; } = 9;

		[DebugEditable(DisplayName = "Spacing", Step = 1, Min = 0, Max = 64)]
		public int Spacing { get; set; } = 3;

		[DebugEditable(DisplayName = "Row Height", Step = 1, Min = 24, Max = 80)]
		public int RowHeight { get; set; } = 34;

        [DebugEditable(DisplayName = "Button Height", Step = 1, Min = 24, Max = 80)]
        public int ButtonHeight { get; set; } = 36;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.05f, Min = 0.2f, Max = 2.0f)]
		public float TitleScale { get; set; } = 0.175f;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 2.0f)]
        public float TextScale { get; set; } = 0.1375f;

        [DebugEditable(DisplayName = "Tab Height", Step = 1, Min = 20, Max = 80)]
        public int TabHeight { get; set; } = 36;

		[DebugEditable(DisplayName = "Panel Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int PanelBorderThickness { get; set; } = 1;

		[DebugEditable(DisplayName = "Bottom Margin", Step = 2, Min = 0, Max = 200)]
		public int BottomMargin { get; set; } = 0;

        [DebugEditable(DisplayName = "Dropdown Row Height", Step = 1, Min = 20, Max = 80)]
        public int DropdownRowHeight { get; set; } = 36;

		[DebugEditable(DisplayName = "Copy Button Width", Step = 5, Min = 80, Max = 320)]
		public int CopyButtonWidth { get; set; } = 155;

		[DebugEditable(DisplayName = "Triangle Width", Step = 1, Min = 6, Max = 32)]
		public int TriangleWidth { get; set; } = 10;

        [DebugEditable(DisplayName = "Triangle Height", Step = 1, Min = 6, Max = 32)]
        public int TriangleHeight { get; set; } = 8;

		[DebugEditable(DisplayName = "Triangle Right Padding", Step = 1, Min = 0, Max = 40)]
		public int TriangleRightPadding { get; set; } = 15;

		[DebugEditable(DisplayName = "Scroll Pixels / Notch", Step = 2, Min = 8, Max = 200)]
		public float ScrollPixelsPerNotch { get; set; } = 56f;

		[DebugEditable(DisplayName = "Initial Offset Y", Step = 2, Min = 0, Max = 400)]
		public int InitialOffsetY { get; set; } = 50;

        private class HoldState
        {
            public Rectangle Rect;
            public Func<object> Getter;
            public Action<object> Setter;
            public Type Type;
            public DebugEditableAttribute Attr;
            public float Step;
            public float Sign; // +1 or -1
            public double HeldSeconds;
            public double RepeatAccumulator;
        }

        private HoldState _hold;
        private DateTime _copiedStatusUntil = DateTime.MinValue;

        public DebugMenuSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, SystemManager systemManager)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _systemManager = systemManager;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<DebugMenu>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            _inputFrame = PlayerInputService.GetFrame(EntityManager);
            var menu = entity.GetComponent<DebugMenu>();
            InputContextService.EnsureContext(
                EntityManager,
                entity,
                "diagnostic.debug-menu",
                900,
                menu?.IsOpen == true,
                true);
            var ui = entity.GetComponent<UIElement>();
            if (ui != null) ui.IsInteractable = menu?.IsOpen == true;
            BuildFrame();
        }

        private void BuildFrame()
        {
			_drawCommands.Clear();
			// Early-out if there is no menu; while ensuring UIElement interactivity reflects open state
			var menuEntity = GetRelevantEntities().FirstOrDefault();
			if (menuEntity == null) return;
			var menu = menuEntity.GetComponent<DebugMenu>();
			var transform = menuEntity.GetComponent<Transform>();
			var ui = menuEntity.GetComponent<UIElement>();
			if (ui != null) ui.IsInteractable = menu != null && menu.IsOpen;
			if (menu == null || !menu.IsOpen)
			{
				// Ensure the debug panel itself is non-hit-testable when closed
				if (ui != null)
				{
					ui.IsInteractable = false;
					ui.Bounds = Rectangle.Empty;
				}
				// Also ensure dropdown UI (if present) is fully collapsed and non-hit-testable
				var ddEntityClosed = EntityManager.GetEntitiesWithComponent<UIDropdown>().FirstOrDefault(e => e.GetComponent<UIDropdown>().Owner == e);
				var ddClosed = ddEntityClosed?.GetComponent<UIDropdown>();
				var ddUiClosed = ddEntityClosed?.GetComponent<UIElement>();
				if (ddClosed != null) ddClosed.IsOpen = false;
				if (ddUiClosed != null)
				{
					ddUiClosed.IsInteractable = false;
					ddUiClosed.Bounds = Rectangle.Empty;
				}
				return;
			}
			if (transform == null || ui == null) return;

			// Invalidate debug menu caches when scene or system set changes
			TryInvalidateCachesOnSceneOrSystemsChange();

            Point mousePosition = _inputFrame.PointerPosition.ToPoint();
            
            var now = DateTime.UtcNow;
            double dt = (now - _lastDrawTime).TotalSeconds;
            _lastDrawTime = now;
            bool click = _inputFrame.WasPressed(PlayerButton.Primary);
            bool clickForContent = click;

            // Layout values
            int viewportW = Game1.VirtualWidth;

            // Initialize saved panel position on first open
            if (!menu.IsPositionSet)
            {
                menu.PanelX = viewportW - PanelWidth - Margin;
                menu.PanelY = Margin + InitialOffsetY;
                menu.IsPositionSet = true;
            }

            // Allow dragging by the title label area
            bool mouseDown = _inputFrame.IsDown(PlayerButton.Primary);
            bool mouseJustPressed = _inputFrame.WasPressed(PlayerButton.Primary);

            int panelX = menu.PanelX;
            int panelY = menu.PanelY;

			// Build tabs from annotated systems (cached)
			var systems = GetAnnotatedSystemsCached();
            if (systems.Count == 0)
            {
                var panelRectEmpty = new Rectangle(panelX, panelY, PanelWidth, 80);
                ui.Bounds = panelRectEmpty;
                DrawFilledRect(panelRectEmpty, new Color(15, 30, 55) * 0.95f);
                DrawRect(panelRectEmpty, Color.White, PanelBorderThickness);
                return;
            }
            if (menu.ActiveTabIndex < 0 || menu.ActiveTabIndex >= systems.Count)
                menu.ActiveTabIndex = 0;

            // First pass: measure height
            int measureY = panelY + Padding;
            if (_font != null)
            {
                measureY += (int)(_font.LineSpacing * TitleScale) + Spacing; // title height
            }

            // Tab bar height
            int tabH = TabHeight;
            int tabBarY = measureY;
            measureY += tabH + Spacing;

            // Active tab fields count
            var active = systems[menu.ActiveTabIndex];
			var members = GetEditableMembersCached(active.sys);
            int fieldsCount = members.Count;
            measureY += fieldsCount * (RowHeight + Spacing);

			// Buttons section from DebugActionAttribute on the active system
			var actionMethods = GetDebugActionsCached(active.sys);
			var actionIntMethods = GetDebugActionsIntCached(active.sys);
			var actionLists = GetDebugActionLists(active.sys);
			int actionListCount = actionLists.Sum(list => list.actions.Count);
			if (_font != null && (actionMethods.Count > 0 || actionIntMethods.Count > 0 || actionListCount > 0))
			{
				measureY += (int)(_font.LineSpacing * TextScale) + Spacing; // header
				measureY += (actionMethods.Count + actionIntMethods.Count + actionListCount) * (ButtonHeight + Spacing);
			}

            int panelHeight = measureY - panelY + Padding;
            // Constrain panel height to viewport with a bottom margin
            int bottomMargin = BottomMargin;
            int maxPanelHeight = Math.Max(120, Game1.VirtualHeight - bottomMargin - panelY);
            bool needScroll = panelHeight > maxPanelHeight;
            int displayHeight = needScroll ? maxPanelHeight : panelHeight;
            var panelRect = new Rectangle(panelX, panelY, PanelWidth, displayHeight);
            ui.Bounds = panelRect;

            // Draw panel and title
            DrawFilledRect(panelRect, new Color(15, 30, 55) * 0.95f);
            DrawRect(panelRect, Color.White, PanelBorderThickness);

            int cursorY = panelY + Padding;
            if (_font != null)
            {
                // Title label rect for dragging
                var titleSize = _font.MeasureString("Debug Menu") * TitleScale;
                var titleRect = new Rectangle(panelX + Padding, cursorY, (int)Math.Ceiling(titleSize.X), (int)Math.Ceiling(titleSize.Y));
                DrawStringScaled("Debug Menu", new Vector2(panelX + Padding, cursorY), Color.White, TitleScale);
                // Handle drag start when pressing on title label
                if (mouseJustPressed && titleRect.Contains(mousePosition))
                {
                    _dragging = true;
                    _dragOffset = new Point(mousePosition.X - panelX, mousePosition.Y - panelY);
                }
                // Release drag on mouse up
                if (!mouseDown)
                {
                    _dragging = false;
                }
                // Apply dragging movement
                if (_dragging)
                {
                    int newX = mousePosition.X - _dragOffset.X;
                    int newY = mousePosition.Y - _dragOffset.Y;
                    // Clamp so the title label remains fully on-screen
                    int viewportWClamped = Game1.VirtualWidth;
                    int viewportHClamped = Game1.VirtualHeight;
                    int titleW = (int)Math.Ceiling(titleSize.X);
                    int titleH = (int)Math.Ceiling(titleSize.Y);
                    // Keep the title label fully on-screen: clamp panel position so (panel + padding) within [0, viewport - titleSize]
                    int minPanelX = -Padding;
                    int maxPanelX = viewportWClamped - titleW - Padding;
                    int minPanelY = -Padding;
                    int maxPanelY = viewportHClamped - titleH - Padding;
                    newX = Math.Max(minPanelX, Math.Min(newX, maxPanelX));
                    newY = Math.Max(minPanelY, Math.Min(newY, maxPanelY));
                    menu.PanelX = newX;
                    menu.PanelY = newY;
                    panelX = newX;
                    panelY = newY;
                    panelRect = new Rectangle(panelX, panelY, PanelWidth, displayHeight);
                    ui.Bounds = panelRect;
                    // Update titleRect X/Y for hit test continuity during the same frame
                    titleRect.X = panelX + Padding;
                    titleRect.Y = panelY + Padding;
                }
                // Copy Settings button at top-right
                int btnW = CopyButtonWidth;
                int btnH = ButtonHeight;
                var copyRect = new Rectangle(panelX + PanelWidth - Padding - btnW, cursorY, btnW, btnH);
                bool hoverCopy = copyRect.Contains(mousePosition);
                var copyBg = hoverCopy ? new Color(120, 120, 120) : new Color(70, 70, 70);
                DrawFilledRect(copyRect, copyBg);
                DrawRect(copyRect, Color.White, 1);
                DrawStringScaled("Copy Settings", new Vector2(copyRect.X + 8, copyRect.Y + 3), Color.White, TextScale);
                if (click && hoverCopy)
                {
                    var activeSys = systems[menu.ActiveTabIndex].sys;
                    string export = BuildSettingsExport(activeSys);
                    TryCopyToClipboard(export);
                    _copiedStatusUntil = DateTime.UtcNow.AddSeconds(1.5);
                }
                // transient copied label
                if (DateTime.UtcNow < _copiedStatusUntil)
                {
                    DrawStringScaled("Copied!", new Vector2(copyRect.X - 80, copyRect.Y + 3), Color.LightGreen, TextScale);
                }

                cursorY += (int)(_font.LineSpacing * TitleScale) + Spacing;
            }

            // Dropdown for tabs
            // Create/find a dropdown entity to manage selection
            var ddEntity = EntityManager.GetEntitiesWithComponent<UIDropdown>().FirstOrDefault(e => e.GetComponent<UIDropdown>().Owner == e);
            if (ddEntity == null)
            {
                ddEntity = EntityManager.CreateEntity("DebugMenu_TabDropdown");
				var dd = new UIDropdown { Items = systems.Select(s => s.name).ToList(), SelectedIndex = Math.Clamp(menu.ActiveTabIndex, 0, systems.Count - 1), RowHeight = DropdownRowHeight, TextScale = TextScale };
				dd.Owner = ddEntity;
                var ddBounds = new Rectangle(panelX + Padding, cursorY, PanelWidth - Padding * 2, DropdownRowHeight);
                EntityManager.AddComponent(ddEntity, dd);
                EntityManager.AddComponent(ddEntity, new UIElement { Bounds = ddBounds, IsInteractable = true });
                EntityManager.AddComponent(ddEntity, new InputContextMember
                {
                    ContextId = "diagnostic.debug-menu",
                });
                // Set very high Z so dropdown (and especially its options) stays on top of other UI
                EntityManager.AddComponent(ddEntity, new Transform { Position = new Vector2(ddBounds.X, ddBounds.Y), ZOrder = 50000 });
            }
            else
            {
                var dd = ddEntity.GetComponent<UIDropdown>();
                var uiDD = ddEntity.GetComponent<UIElement>();
                var tDD = ddEntity.GetComponent<Transform>();
                dd.Items = systems.Select(s => s.name).ToList();
                dd.SelectedIndex = Math.Clamp(menu.ActiveTabIndex, 0, systems.Count - 1);
                dd.RowHeight = DropdownRowHeight;
                dd.TextScale = TextScale;
				uiDD.Bounds = new Rectangle(panelX + Padding, cursorY, PanelWidth - Padding * 2, DropdownRowHeight);
				uiDD.IsInteractable = true;
                if (tDD != null)
                {
                    // Boost ZOrder while open to ensure options render above everything and capture hover
                    tDD.ZOrder = dd.IsOpen ? 60000 : 50000;
                }
                InputContextService.EnsureMember(
                    EntityManager,
                    ddEntity,
                    "diagnostic.debug-menu");
            }
            var ddCurrent = ddEntity.GetComponent<UIDropdown>();
            var ddUI = ddEntity.GetComponent<UIElement>();

            // Draw dropdown closed state (the bar)
            DrawFilledRect(ddUI.Bounds, new Color(35, 35, 35));
            DrawRect(ddUI.Bounds, Color.White, 1);
            string ddLabel = (ddCurrent.SelectedIndex >= 0 && ddCurrent.SelectedIndex < ddCurrent.Items.Count) ? ddCurrent.Items[ddCurrent.SelectedIndex] : "";
            DrawStringScaled(ddLabel, new Vector2(ddUI.Bounds.X + 8, ddUI.Bounds.Y + 4), Color.White, TextScale);

            // Triangle indicator at right side (down when closed, up when open)
            int triW = TriangleWidth;
            int triH = TriangleHeight;
            int triRight = ddUI.Bounds.Right - TriangleRightPadding;
            int triCenterY = ddUI.Bounds.Y + ddUI.Bounds.Height / 2;
            var triRect = new Rectangle(triRight - triW, triCenterY - triH / 2, triW, triH);
            if (ddCurrent.IsOpen) DrawTriangleUp(triRect, Color.White); else DrawTriangleDown(triRect, Color.White);

            if (click && ddUI.Bounds.Contains(mousePosition))
            {
                ddCurrent.IsOpen = !ddCurrent.IsOpen;
            }

            // Defer drawing the open list until AFTER fields so it overlays everything
            bool drawListAfter = ddCurrent.IsOpen;
            // Build visible options list excluding the currently selected item
            var visibleOptions = systems
                .Select((s, idx) => (index: idx, name: s.name))
                .Where(t => t.index != ddCurrent.SelectedIndex)
                .ToList();
            int deferredItemCount = visibleOptions.Count;
            int deferredRowH = ddCurrent.RowHeight;
            // Open the dropdown list downward
            var deferredListRectFull = new Rectangle(ddUI.Bounds.X, ddUI.Bounds.Bottom, ddUI.Bounds.Width, deferredItemCount * deferredRowH);
            int availableBelow = Math.Max(0, Game1.VirtualHeight - ddUI.Bounds.Bottom - 8);
            int listTotalHeight = deferredItemCount * deferredRowH;
            int listDisplayHeight = Math.Min(listTotalHeight, availableBelow);
            var deferredListRect = new Rectangle(ddUI.Bounds.X, ddUI.Bounds.Bottom, ddUI.Bounds.Width, listDisplayHeight);
            bool dropdownNeedScroll = listTotalHeight > listDisplayHeight;

            if (ddCurrent.SelectedIndex != menu.ActiveTabIndex)
            {
                menu.ActiveTabIndex = Math.Clamp(ddCurrent.SelectedIndex, 0, systems.Count - 1);
            }
            int dropdownHeight = ddUI.Bounds.Height;
            cursorY += dropdownHeight + Spacing;

            // Scroll handling for content below the dropdown
            int headerHeight = cursorY - panelY; // title + dropdown + Spacing consumed so far
            int scrollAreaHeight = Math.Max(0, displayHeight - headerHeight - Padding);
            float maxScroll = Math.Max(0, (panelHeight - headerHeight - Padding) - scrollAreaHeight);
            if (!needScroll)
            {
                _scrollOffset = 0f;
            }
            else
            {
                float deltaPixels = -_inputFrame.ScrollDelta * ScrollPixelsPerNotch;
                _scrollOffset = MathHelper.Clamp(_scrollOffset + deltaPixels, 0f, maxScroll);
            }
            int yOffset = -(int)Math.Round(_scrollOffset);
            // If dropdown open and mouse is over its area, suppress clicks to content behind
            if (drawListAfter && deferredListRect.Contains(mousePosition))
            {
                clickForContent = false;
            }

            // Visible region bounds for culling
            int visibleTop = panelY + headerHeight;
            int visibleBottom = panelY + displayHeight - Padding;

            // Fields for active tab (scrollable)
            foreach (var m in members)
            {
                var (label, getter, setter, type, attr) = m;
                string display = string.IsNullOrWhiteSpace(attr.DisplayName) ? label : attr.DisplayName;
                object val = getter();

                var rowRect = new Rectangle(panelX + Padding, cursorY + yOffset, PanelWidth - Padding * 2, RowHeight);
                if (rowRect.Bottom < visibleTop || rowRect.Y > visibleBottom)
                {
                    cursorY += RowHeight + Spacing;
                    continue;
                }
                DrawFilledRect(rowRect, new Color(30, 30, 30));
                DrawRect(rowRect, Color.White, 1);
                int right = rowRect.Right - 8;

                if (type == typeof(bool))
                {
                    string vs = ((bool)val) ? "ON" : "OFF";
                    var txtSize = _font.MeasureString(vs) * TextScale;
                    var valRect = new Rectangle(right - (int)txtSize.X - 10, rowRect.Y + 3, (int)txtSize.X + 10, RowHeight - 6);
                    // Label clipped to available space
                    int labelMaxWidth = Math.Max(0, valRect.X - 6 - (rowRect.X + 8));
                    DrawStringClippedScaled(display, new Vector2(rowRect.X + 8, rowRect.Y + 4), Color.LightGray, labelMaxWidth, TextScale);
                    DrawFilledRect(valRect, new Color(70, 70, 70));
                    DrawRect(valRect, Color.White, 1);
                    DrawStringScaled(vs, new Vector2(valRect.X + 5, valRect.Y + 2), Color.White, TextScale);
                    if (clickForContent && valRect.Contains(mousePosition)) setter(!(bool)val);
                }
                else if (type == typeof(int) || type == typeof(float) || type == typeof(byte))
                {
                    int btnW = 22;
                    var plusRect = new Rectangle(right - btnW, rowRect.Y + 3, btnW, RowHeight - 6);
                    var valRect = new Rectangle(plusRect.X - 100, rowRect.Y + 3, 100, RowHeight - 6);
                    var minusRect = new Rectangle(valRect.X - btnW, rowRect.Y + 3, btnW, RowHeight - 6);

                    // Label clipped to available space
                    int labelMaxWidth = Math.Max(0, minusRect.X - 6 - (rowRect.X + 8));
                    DrawStringClippedScaled(display, new Vector2(rowRect.X + 8, rowRect.Y + 4), Color.LightGray, labelMaxWidth, TextScale);

                    DrawFilledRect(minusRect, new Color(70, 70, 70));
                    DrawRect(minusRect, Color.White, 1);
                    DrawStringScaled("-", new Vector2(minusRect.X + 7, minusRect.Y + 2), Color.White, TextScale);

                    DrawFilledRect(valRect, new Color(50, 50, 50));
                    DrawRect(valRect, Color.White, 1);
                    string vs = type == typeof(float) ? $"{Convert.ToSingle(val):0.###}" : Convert.ToInt32(val).ToString();
                    DrawStringScaled(vs, new Vector2(valRect.X + 6, valRect.Y + 2), Color.White, TextScale);

                    DrawFilledRect(plusRect, new Color(70, 70, 70));
                    DrawRect(plusRect, Color.White, 1);
                    DrawStringScaled("+", new Vector2(plusRect.X + 6, plusRect.Y + 2), Color.White, TextScale);

                    float step = attr.Step <= 0f ? 1f : attr.Step;
                    if (click)
                    {
                        if (minusRect.Contains(mousePosition))
                        {
                            ApplyNumericDelta(type, setter, val, -step, attr);
                            _hold = new HoldState { Rect = minusRect, Getter = getter, Setter = setter, Type = type, Attr = attr, Step = step, Sign = -1f, HeldSeconds = 0, RepeatAccumulator = 0 };
                        }
                        else if (plusRect.Contains(mousePosition))
                        {
                            ApplyNumericDelta(type, setter, val, +step, attr);
                            _hold = new HoldState { Rect = plusRect, Getter = getter, Setter = setter, Type = type, Attr = attr, Step = step, Sign = +1f, HeldSeconds = 0, RepeatAccumulator = 0 };
                        }
                    }
                    // Holding logic
                    if (_inputFrame.IsDown(PlayerButton.Primary) && _hold != null)
                    {
                        // Refresh current rect positions to keep tracking in case layout shifts slightly
                        if ((_hold.Rect == minusRect || _hold.Rect == plusRect) && _hold.Rect.Contains(mousePosition))
                        {
                            _hold.HeldSeconds += dt;
                            _hold.RepeatAccumulator += dt;
                            double initialDelay = 0.4;
                            if (_hold.HeldSeconds >= initialDelay)
                            {
                                double interval = _hold.HeldSeconds > 2.0 ? 0.02 : (_hold.HeldSeconds > 1.0 ? 0.05 : 0.1);
                                while (_hold.RepeatAccumulator >= interval)
                                {
                                    var cur = _hold.Getter();
                                    ApplyNumericDelta(_hold.Type, _hold.Setter, cur, _hold.Step * _hold.Sign, _hold.Attr);
                                    _hold.RepeatAccumulator -= interval;
                                }
                            }
                        }
                        else if (!_hold.Rect.Contains(mousePosition))
                        {
                            _hold = null; // moved off the button
                        }
                    }
                    else
                    {
                        _hold = null; // released
                    }
                }
                else
                {
                    string vs = val?.ToString() ?? "null";
                    var txtSize = _font.MeasureString(vs) * TextScale;
                    var valRect = new Rectangle(right - (int)txtSize.X - 10, rowRect.Y + 3, (int)txtSize.X + 10, RowHeight - 6);
                    int labelMaxWidth = Math.Max(0, valRect.X - 6 - (rowRect.X + 8));
                    DrawStringClippedScaled(display, new Vector2(rowRect.X + 8, rowRect.Y + 4), Color.LightGray, labelMaxWidth, TextScale);
                    DrawFilledRect(valRect, new Color(50, 50, 50));
                    DrawRect(valRect, Color.White, 1);
                    DrawStringScaled(vs, new Vector2(valRect.X + 5, valRect.Y + 2), Color.White, TextScale);
                }

                cursorY += RowHeight + Spacing;
            }

			// Buttons section (DebugAction and DebugActionInt) - also scrollable
			if (_font != null && (actionMethods.Count > 0 || actionIntMethods.Count > 0 || actionListCount > 0))
            {
                int headerY = cursorY + yOffset;
                if (headerY + (int)(_font.LineSpacing * TextScale) >= visibleTop && headerY <= visibleBottom)
                {
                    DrawStringScaled("Buttons", new Vector2(panelX + Padding, headerY), Color.LightGreen, TextScale);
                }
                cursorY += (int)(_font.LineSpacing * TextScale) + Spacing;

				foreach (var am in actionMethods)
                {
					var rect = new Rectangle(panelX + Padding, cursorY + yOffset, PanelWidth - Padding * 2, ButtonHeight);
                    if (rect.Bottom < visibleTop || rect.Y > visibleBottom)
                    {
                        cursorY += ButtonHeight + Spacing;
                        continue;
                    }
					var bgColor = new Color(70, 70, 70);
                    DrawFilledRect(rect, bgColor);
                    DrawRect(rect, Color.White, 1);

					if (_font != null)
                    {
						string label = am.label;
						var size = _font.MeasureString(label) * TextScale;
                        int textX = rect.X + (int)((rect.Width - size.X) / 2f);
                        int textY = rect.Y + (int)((rect.Height - size.Y) / 2f);
						DrawStringScaled(label, new Vector2(textX, textY), Color.White, TextScale);
                    }
					// Handle click
					if (clickForContent && rect.Contains(mousePosition))
					{
						try
						{
							am.method.Invoke(active.sys, Array.Empty<object>());
						}
						catch (Exception ex)
						{
							var tie = ex as System.Reflection.TargetInvocationException;
							var root = tie?.InnerException ?? ex;
							Console.WriteLine($"[DebugMenu] Action '{am.label}' failed:\n{root}");
						}
					}
                    cursorY += ButtonHeight + Spacing;
                }

				// Render int-parameter actions
				for (int i = 0; i < actionIntMethods.Count; i++)
				{
					var ai = actionIntMethods[i];
					var rect = new Rectangle(panelX + Padding, cursorY + yOffset, PanelWidth - Padding * 2, ButtonHeight);
					if (rect.Bottom < visibleTop || rect.Y > visibleBottom)
					{
						cursorY += ButtonHeight + Spacing;
						continue;
					}
					DrawFilledRect(rect, new Color(70, 70, 70));
					DrawRect(rect, Color.White, 1);
					string label = ai.label + $" ({ai.current})";
					var size = _font.MeasureString(label) * TextScale;
					int textX = rect.X + 8;
					int textY = rect.Y + (int)((rect.Height - size.Y) / 2f);
					DrawStringScaled(label, new Vector2(textX, textY), Color.White, TextScale);
					int btnW = 22;
					var applyRect = new Rectangle(rect.Right - btnW, rect.Y + 3, btnW, rect.Height - 6);
					var plusRect = new Rectangle(applyRect.X - btnW - 4, rect.Y + 3, btnW, rect.Height - 6);
					var minusRect = new Rectangle(plusRect.X - btnW - 4, rect.Y + 3, btnW, rect.Height - 6);
					DrawFilledRect(minusRect, new Color(60,60,60)); DrawRect(minusRect, Color.White, 1); DrawStringScaled("-", new Vector2(minusRect.X + 7, minusRect.Y + 2), Color.White, TextScale);
					DrawFilledRect(plusRect, new Color(60,60,60)); DrawRect(plusRect, Color.White, 1); DrawStringScaled("+", new Vector2(plusRect.X + 6, plusRect.Y + 2), Color.White, TextScale);
					DrawFilledRect(applyRect, new Color(60,60,60)); DrawRect(applyRect, Color.White, 1); DrawStringScaled(">", new Vector2(applyRect.X + 6, applyRect.Y + 2), Color.White, TextScale);
					if (clickForContent)
					{
						if (minusRect.Contains(mousePosition)) { ai.current = (int)Math.Max(ai.meta.Min, Math.Min(ai.meta.Max, ai.current - (int)Math.Round(ai.meta.Step))); actionIntMethods[i] = ai; }
						else if (plusRect.Contains(mousePosition)) { ai.current = (int)Math.Max(ai.meta.Min, Math.Min(ai.meta.Max, ai.current + (int)Math.Round(ai.meta.Step))); actionIntMethods[i] = ai; }
						else if (applyRect.Contains(mousePosition))
						{
							try { ai.method.Invoke(active.sys, new object[] { ai.current }); }
							catch (System.Exception ex)
							{
								var tie = ex as System.Reflection.TargetInvocationException;
								var root = tie?.InnerException ?? ex;
                                Console.WriteLine($"[DebugMenu] Action-int '{ai.label}' failed:\n{root}");
							}
						}
					}
					cursorY += ButtonHeight + Spacing;
				}

				foreach (var actionList in actionLists)
				{
					foreach (var namedAction in actionList.actions)
					{
						var rect = new Rectangle(panelX + Padding, cursorY + yOffset, PanelWidth - Padding * 2, ButtonHeight);
						if (rect.Bottom < visibleTop || rect.Y > visibleBottom)
						{
							cursorY += ButtonHeight + Spacing;
							continue;
						}
						DrawFilledRect(rect, namedAction.IsEnabled ? new Color(70, 70, 70) : new Color(40, 40, 40));
						DrawRect(rect, Color.White, 1);
						string label = namedAction.Label ?? string.Empty;
						DrawStringClippedScaled(label, new Vector2(rect.X + 8, rect.Y + 4), namedAction.IsEnabled ? Color.White : Color.Gray, rect.Width - 16, TextScale);
						if (clickForContent && namedAction.IsEnabled && rect.Contains(mousePosition))
						{
							try
							{
								namedAction.Invoke?.Invoke();
							}
							catch (Exception ex)
							{
								var tie = ex as System.Reflection.TargetInvocationException;
								var root = tie?.InnerException ?? ex;
								Console.WriteLine($"[DebugMenu] Action-list '{label}' failed:\n{root}");
							}
						}
						cursorY += ButtonHeight + Spacing;
					}
				}
            }

            // Finally, if dropdown is open, draw its list on top of everything else in the panel
            if (drawListAfter && deferredItemCount > 0)
            {
                DrawFilledRect(deferredListRect, new Color(25, 25, 25));
                DrawRect(deferredListRect, Color.White, 1);
                int listTotalHeight2 = deferredItemCount * deferredRowH;
                int listDisplayHeight2 = deferredListRect.Height;
                bool dropdownNeedScroll2 = listTotalHeight2 > listDisplayHeight2;
                int firstRow = 0;
                float rowOffset = 0f;
                if (dropdownNeedScroll2)
                {
                    float deltaPixels = -_inputFrame.ScrollDelta * ScrollPixelsPerNotch;
                    float maxDD = Math.Max(0, listTotalHeight2 - listDisplayHeight2);
                    _dropdownScrollOffset = MathHelper.Clamp(_dropdownScrollOffset + deltaPixels, 0f, maxDD);
                    firstRow = (int)Math.Floor(_dropdownScrollOffset / deferredRowH);
                    rowOffset = _dropdownScrollOffset - firstRow * deferredRowH;
                }
                int rowsToDraw = Math.Min(deferredItemCount - firstRow, (int)Math.Ceiling(deferredListRect.Height / (float)deferredRowH) + 1);
                for (int i = 0; i < rowsToDraw; i++)
                {
                    int rowIndex = firstRow + i;
                    if (rowIndex < 0 || rowIndex >= deferredItemCount) continue;
                    var (actualIndex, label) = visibleOptions[rowIndex];
                    int itemY = deferredListRect.Y + (int)Math.Round(i * deferredRowH - rowOffset);
                    var itemRect = new Rectangle(deferredListRect.X, itemY, deferredListRect.Width, deferredRowH);
                    bool hover = itemRect.Contains(mousePosition);
                    if (hover) DrawFilledRect(itemRect, new Color(60, 60, 60));
                    DrawStringScaled(label, new Vector2(itemRect.X + 8, itemRect.Y + 4), Color.White, TextScale);
                    DrawRect(itemRect, Color.White, 1);
                }
                if (click && deferredListRect.Contains(mousePosition))
                {
                    int relY = mousePosition.Y - deferredListRect.Y + (int)_dropdownScrollOffset;
                    int sel = relY / deferredRowH;
                    if (sel >= 0 && sel < deferredItemCount)
                    {
                        var (actualIndex, _label) = visibleOptions[sel];
                        var dd = ddEntity.GetComponent<UIDropdown>();
                        dd.SelectedIndex = actualIndex;
                        dd.IsOpen = false;
                        menu.ActiveTabIndex = actualIndex;
                    }
                }
            }

        }

        private void TryInvalidateCachesOnSceneOrSystemsChange()
        {
            try
            {
                // Scene change detection
                var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
                var currentScene = scene?.Current ?? _lastSceneId;
                // Systems signature: count + names to detect add/remove
                var systems = _systemManager.GetAllSystems().Select(s => s.GetType().FullName).OrderBy(n => n);
                string sig = string.Join("|", systems);
                if (currentScene != _lastSceneId || !string.Equals(sig, _systemsSignatureSnapshot, StringComparison.Ordinal))
                {
                    _annotatedSystemsCache = null; // force rebuild
                    _editableMembersCache.Clear();
                    _debugActionsCache.Clear();
                    _debugActionsIntCache.Clear();
                    _debugActionListsCache.Clear();
                    _lastSceneId = currentScene;
                    _systemsSignatureSnapshot = sig;
                    CaptureMissingBootSnapshots();
                }
            }
            catch { }
        }

		private void CaptureMissingBootSnapshots()
		{
			foreach (var (_, sys) in GetAnnotatedSystemsCached())
			{
				var type = sys.GetType();
				if (_bootSnapshots.ContainsKey(type)) continue;
				var snap = new Dictionary<string, object>();
				foreach (var (label, get, _, _, _) in GetEditableMembers(sys))
				{
					snap[label] = get();
				}
				_bootSnapshots[type] = snap;
			}
		}

		private string BuildSettingsExport(Core.System system)
        {
            CaptureMissingBootSnapshots();
            var sb = new StringBuilder();
            string systemName = system.GetType().Name;
            var systemType = system.GetType();
            _bootSnapshots.TryGetValue(systemType, out var baseline);
            bool anyChanged = false;
            var members = GetEditableMembers(system);
            foreach (var (label, get, _, memberType, _attr) in members)
            {
                if (memberType != typeof(int) && memberType != typeof(byte) && memberType != typeof(float))
                    continue;
                object current = get();
                if (baseline == null || !baseline.TryGetValue(label, out var bootValue))
                    continue;
                if (ValuesEqual(current, bootValue, memberType))
                    continue;
                if (!anyChanged)
                {
                    sb.AppendLine($"{systemName} changed settings - update the system with these values:");
                    anyChanged = true;
                }
                if (memberType == typeof(int) || memberType == typeof(byte))
                {
                    sb.AppendLine($"{label}={Convert.ToInt32(current)}");
                }
                else if (memberType == typeof(float))
                {
                    sb.AppendLine($"{label}={Convert.ToSingle(current):0.###}");
                }
            }
            if (!anyChanged)
                return $"{systemName}: no changed settings";
            return sb.ToString();
        }

		private static bool ValuesEqual(object current, object baseline, Type type)
		{
			if (type == typeof(int) || type == typeof(byte))
				return Convert.ToInt32(current) == Convert.ToInt32(baseline);
			if (type == typeof(float))
				return Math.Abs(Convert.ToSingle(current) - Convert.ToSingle(baseline)) < 0.0005f;
			return Equals(current, baseline);
		}

		private List<(string name, Core.System sys)> GetAnnotatedSystemsCached()
		{
			if (_annotatedSystemsCache == null)
			{
				_annotatedSystemsCache = GetAnnotatedSystems(_systemManager.GetAllSystems())
					.OrderBy(t => t.name)
					.ToList();
				CaptureMissingBootSnapshots();
			}
			return _annotatedSystemsCache;
		}

		private List<(string label, Func<object> get, Action<object> set, Type type, DebugEditableAttribute attr)> GetEditableMembersCached(Core.System system)
		{
			var type = system.GetType();
			if (_editableMembersCache.TryGetValue(type, out var cached)) return cached;
			var computed = GetEditableMembers(system);
			_editableMembersCache[type] = computed;
			return computed;
		}

		private List<(string label, MethodInfo method)> GetDebugActionsCached(Core.System system)
		{
			var type = system.GetType();
			if (_debugActionsCache.TryGetValue(type, out var cached)) return cached;
			var computed = GetDebugActions(system);
			_debugActionsCache[type] = computed;
			return computed;
		}

        private static List<(string label, MethodInfo method)> GetDebugActions(Core.System system)
        {
            var actions = new List<(string, MethodInfo)>();
            var t = system.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attr = m.GetCustomAttribute<DebugActionAttribute>();
                if (attr == null) continue;
                if (m.GetParameters().Length != 0) continue; // only parameterless for now
                actions.Add((string.IsNullOrWhiteSpace(attr.DisplayName) ? m.Name : attr.DisplayName, m));
            }
            return actions;
        }

		private List<(string label, List<DebugNamedAction> actions)> GetDebugActionLists(Core.System system)
		{
			var type = system.GetType();
			if (!_debugActionListsCache.TryGetValue(type, out var methods))
			{
				methods = GetDebugActionListMethods(system);
				_debugActionListsCache[type] = methods;
			}

			var result = new List<(string label, List<DebugNamedAction> actions)>();
			foreach (var (label, method) in methods)
			{
				try
				{
					var value = method.Invoke(system, Array.Empty<object>());
					if (value is IEnumerable<DebugNamedAction> actions)
					{
						result.Add((label, actions.Where(action => action != null).ToList()));
					}
				}
				catch (Exception ex)
				{
					var tie = ex as System.Reflection.TargetInvocationException;
					var root = tie?.InnerException ?? ex;
					Console.WriteLine($"[DebugMenu] Action-list '{label}' failed:\n{root}");
				}
			}
			return result;
		}

		private static List<(string label, MethodInfo method)> GetDebugActionListMethods(Core.System system)
		{
			var list = new List<(string, MethodInfo)>();
			var t = system.GetType();
			foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
			{
				var attr = m.GetCustomAttribute<DebugActionListAttribute>();
				if (attr == null) continue;
				if (m.GetParameters().Length != 0) continue;
				if (!typeof(IEnumerable<DebugNamedAction>).IsAssignableFrom(m.ReturnType)) continue;
				list.Add((string.IsNullOrWhiteSpace(attr.DisplayName) ? m.Name : attr.DisplayName, m));
			}
			return list;
		}

        private List<(string label, MethodInfo method, DebugActionIntAttribute meta, int current)> GetDebugActionsIntCached(Core.System system)
        {
            var type = system.GetType();
            if (_debugActionsIntCache.TryGetValue(type, out var cached)) return cached;
            var computed = GetDebugActionsInt(system);
            _debugActionsIntCache[type] = computed;
            return computed;
        }

        private static List<(string label, MethodInfo method, DebugActionIntAttribute meta, int current)> GetDebugActionsInt(Core.System system)
        {
            var list = new List<(string, MethodInfo, DebugActionIntAttribute, int)>();
            var t = system.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
            {
                var attr = m.GetCustomAttribute<DebugActionIntAttribute>();
                if (attr == null) continue;
                var ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(int)) continue;
                int current = attr.Default;
                list.Add((string.IsNullOrWhiteSpace(attr.DisplayName) ? m.Name : attr.DisplayName, m, attr, current));
            }
            return list;
        }

        private static void TryCopyToClipboard(string text)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    var psi = new ProcessStartInfo("cmd.exe", "/c clip")
                    {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        p.StandardInput.Write(text);
                        p.StandardInput.Close();
                        p.WaitForExit(2000);
                    }
                }
                else if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
                {
                    var psi = new ProcessStartInfo("/usr/bin/pbcopy")
                    {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        p.StandardInput.Write(text);
                        p.StandardInput.Close();
                        p.WaitForExit(2000);
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    bool copied = false;
                    try
                    {
                        var psi = new ProcessStartInfo("xclip", "-selection clipboard")
                        {
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            CreateNoWindow = true
                        };
                        using var p = Process.Start(psi);
                        if (p != null)
                        {
                            p.StandardInput.Write(text);
                            p.StandardInput.Close();
                            p.WaitForExit(2000);
                            copied = p.ExitCode == 0;
                        }
                    }
                    catch { }
                    if (!copied)
                    {
                        try
                        {
                            var psi2 = new ProcessStartInfo("xsel", "--clipboard --input")
                            {
                                UseShellExecute = false,
                                RedirectStandardInput = true,
                                CreateNoWindow = true
                            };
                            using var p2 = Process.Start(psi2);
                            if (p2 != null)
                            {
                                p2.StandardInput.Write(text);
                                p2.StandardInput.Close();
                                p2.WaitForExit(2000);
                                copied = p2.ExitCode == 0;
                            }
                        }
                        catch { }
                    }
                    if (!copied)
                    {
                        Console.WriteLine("[Clipboard] xclip/xsel not available. Export below:\n" + text);
                    }
                }
                else
                {
                    Console.WriteLine("[Clipboard] Copy not supported on this OS. Export below:\n" + text);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Clipboard] Failed: " + ex.Message + "\n" + text);
            }
        }

        public void Draw()
        {
            foreach (Action drawCommand in _drawCommands)
            {
                drawCommand();
            }
        }

        private static IEnumerable<(string name, Core.System sys)> GetAnnotatedSystems(IEnumerable<Core.System> systems)
        {
            foreach (var s in systems)
            {
                var attr = s.GetType().GetCustomAttribute<DebugTabAttribute>();
                if (attr != null)
                {
                    yield return (string.IsNullOrWhiteSpace(attr.Name) ? s.GetType().Name : attr.Name, s);
                }
            }
        }

        private static List<(string label, Func<object> get, Action<object> set, Type type, DebugEditableAttribute attr)>
            GetEditableMembers(Core.System system)
        {
            var list = new List<(string, Func<object>, Action<object>, Type, DebugEditableAttribute)>();
            var t = system.GetType();

            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = p.GetCustomAttribute<DebugEditableAttribute>();
                if (attr == null) continue;
                if (!p.CanRead || !p.CanWrite) continue;
                var localP = p;
                list.Add((
                    localP.Name,
                    () => localP.GetValue(system),
                    v => localP.SetValue(system, v),
                    localP.PropertyType,
                    attr
                ));
            }

            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = f.GetCustomAttribute<DebugEditableAttribute>();
                if (attr == null) continue;
                var localF = f;
                list.Add((
                    localF.Name,
                    () => localF.GetValue(system),
                    v => localF.SetValue(system, v),
                    localF.FieldType,
                    attr
                ));
            }

            return list;
        }

        private static void ApplyNumericDelta(Type type, Action<object> setter, object current, float delta, DebugEditableAttribute attr)
        {
            if (type == typeof(int))
            {
                int v = Convert.ToInt32(current);
                int nv = v + (int)Math.Round(delta);
                nv = ClampWithAttr(nv, attr);
                setter(nv);
            }
            else if (type == typeof(float))
            {
                float v = Convert.ToSingle(current);
                float nv = v + delta;
                nv = ClampWithAttr(nv, attr);
                setter(nv);
            }
            else if (type == typeof(byte))
            {
                int v = Convert.ToInt32(current);
                int nv = v + (int)Math.Round(delta);
                nv = Math.Max(0, Math.Min(255, nv));
                if (!float.IsNaN(attr.Min)) nv = Math.Max(nv, (int)attr.Min);
                if (!float.IsNaN(attr.Max)) nv = Math.Min(nv, (int)attr.Max);
                setter((byte)nv);
            }
        }

        private static int ClampWithAttr(int v, DebugEditableAttribute attr)
        {
            if (!float.IsNaN(attr.Min)) v = Math.Max(v, (int)attr.Min);
            if (!float.IsNaN(attr.Max)) v = Math.Min(v, (int)attr.Max);
            return v;
        }

        private static float ClampWithAttr(float v, DebugEditableAttribute attr)
        {
            if (!float.IsNaN(attr.Min)) v = Math.Max(v, attr.Min);
            if (!float.IsNaN(attr.Max)) v = Math.Min(v, attr.Max);
            return v;
        }

        private void DrawFilledRect(Rectangle rect, Color color)
        {
            _drawCommands.Add(() => _spriteBatch.Draw(_pixel, rect, color));
        }

        private void DrawRect(Rectangle rect, Color color, int thickness)
        {
            Rectangle top = new(rect.X, rect.Y, rect.Width, thickness);
            Rectangle bottom = new(rect.X, rect.Bottom - thickness, rect.Width, thickness);
            Rectangle left = new(rect.X, rect.Y, thickness, rect.Height);
            Rectangle right = new(rect.Right - thickness, rect.Y, thickness, rect.Height);
            _drawCommands.Add(() =>
            {
                _spriteBatch.Draw(_pixel, top, color);
                _spriteBatch.Draw(_pixel, bottom, color);
                _spriteBatch.Draw(_pixel, left, color);
                _spriteBatch.Draw(_pixel, right, color);
            });
        }

        private void DrawStringScaled(string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text)) return;
            string safe = TextUtils.FilterUnsupportedGlyphs(_font, text);
            if (string.IsNullOrEmpty(safe)) return;
            _drawCommands.Add(() => _spriteBatch.DrawString(
                _font,
                safe,
                position,
                color,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f));
        }

        private void DrawStringClippedScaled(string text, Vector2 position, Color color, int maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text)) return;
            string safeText = TextUtils.FilterUnsupportedGlyphs(_font, text);
            if (string.IsNullOrEmpty(safeText)) return;
            float width = _font.MeasureString(safeText).X * scale;
            if (width <= maxWidth)
            {
                _drawCommands.Add(() => _spriteBatch.DrawString(
                    _font,
                    safeText,
                    position,
                    color,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    0f));
                return;
            }
            const string ellipsis = "...";
            float ellipsisWidth = _font.MeasureString(ellipsis).X * scale;
            string s = safeText;
            while (s.Length > 0 && _font.MeasureString(s).X * scale + ellipsisWidth > maxWidth)
            {
                s = s.Substring(0, s.Length - 1);
            }
            string clipped = s + ellipsis;
            _drawCommands.Add(() => _spriteBatch.DrawString(
                _font,
                clipped,
                position,
                color,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f));
        }

        // Simple rasterized triangle draw helpers using the 1x1 pixel texture
        private void DrawTriangleDown(Rectangle r, Color color)
        {
            int rows = Math.Max(1, r.Height);
            for (int i = 0; i < rows; i++)
            {
                // Grow width from top to bottom
                float t = (i + 1) / (float)rows;
                int w = Math.Max(1, (int)Math.Round(r.Width * t));
                int x = r.X + (r.Width - w) / 2;
                int y = r.Y + i;
                Rectangle line = new(x, y, w, 1);
                _drawCommands.Add(() => _spriteBatch.Draw(_pixel, line, color));
            }
        }

        private void DrawTriangleUp(Rectangle r, Color color)
        {
            int rows = Math.Max(1, r.Height);
            for (int i = 0; i < rows; i++)
            {
                // Shrink width from top to bottom
                float t = 1f - (i / (float)rows);
                int w = Math.Max(1, (int)Math.Round(r.Width * t));
                int x = r.X + (r.Width - w) / 2;
                int y = r.Y + i;
                Rectangle line = new(x, y, w, 1);
                _drawCommands.Add(() => _spriteBatch.Draw(_pixel, line, color));
            }
        }
    }
}
