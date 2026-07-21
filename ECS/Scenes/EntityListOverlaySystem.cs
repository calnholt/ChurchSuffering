using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Draws a simple window listing all active entities in the world.
    /// </summary>
    public class EntityListOverlaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private Texture2D _pixel;
        private const string CopyButtonName = "EntityListOverlay_Copy";
        private Rectangle _panelRect;
        private Rectangle _copyRect;

        public EntityListOverlaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<EntityListOverlay>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var overlay = entity.GetComponent<EntityListOverlay>();
            if (overlay == null) return;

            EnsureRootComponents(entity, overlay);
            InputContext context = entity.GetComponent<InputContext>();
            UIElement rootUi = entity.GetComponent<UIElement>();
            context.IsActive = overlay.IsOpen;
            rootUi.IsInteractable = overlay.IsOpen;
            if (!overlay.IsOpen)
            {
                rootUi.Bounds = Rectangle.Empty;
                SetCopyButtonActive(false);
                return;
            }

            int width = Math.Min(overlay.PanelWidth, Game1.VirtualWidth - overlay.PanelX - 10);
            int height = Math.Min(overlay.PanelHeight, Game1.VirtualHeight - overlay.PanelY - 10);
            _panelRect = new Rectangle(overlay.PanelX, overlay.PanelY, width, height);
            _copyRect = new Rectangle(
                _panelRect.Right - overlay.Padding - 120,
                _panelRect.Y + overlay.Padding,
                120,
                overlay.RowHeight);
            rootUi.Bounds = _panelRect;

            Entity copyButton = EnsureCopyButton(context.Id);
            UIElement copyUi = copyButton.GetComponent<UIElement>();
            copyUi.Bounds = _copyRect;
            copyUi.IsInteractable = true;
            copyButton.GetComponent<Transform>().Position = new Vector2(_copyRect.X, _copyRect.Y);
            if (copyUi.IsClicked)
            {
                TryCopyToClipboard(BuildEntityListExport());
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
            if (input.ScrollDelta != 0f)
            {
                overlay.ScrollOffset = Math.Max(
                    0f,
                    overlay.ScrollOffset - input.ScrollDelta * overlay.RowHeight * 2f);
            }
            if (MathF.Abs(input.RightStick.Y) > 0.15f)
            {
                overlay.ScrollOffset = Math.Max(
                    0f,
                    overlay.ScrollOffset - input.RightStick.Y * overlay.RowHeight * 20f * dt);
            }
        }

        public void Draw()
        {
			var overlayEntity = GetRelevantEntities().FirstOrDefault();
			if (overlayEntity == null) return;
			var overlay = overlayEntity.GetComponent<EntityListOverlay>();
			if (overlay == null || !overlay.IsOpen) return;

            // Panel
            int x = _panelRect.X;
            int y = _panelRect.Y;
            int w = _panelRect.Width;
            int h = _panelRect.Height;
            Fill(_panelRect, new Color(10, 10, 10, 200));
            Stroke(_panelRect, Color.White, 1);

            int cursorY = y + overlay.Padding;
            // Title
            string title = "Entities";
            _spriteBatch.DrawString(FontSingleton.ContentFont, title, new Vector2(x + overlay.Padding, cursorY), Color.White, 0f, Vector2.Zero, overlay.TextScale, SpriteEffects.None, 0f);
            // Copy button at top-right
            Entity copyButton = EntityManager.GetEntity(CopyButtonName);
            bool hoverCopy = copyButton?.GetComponent<UIElement>()?.IsHovered == true;
            var copyBg = hoverCopy ? new Color(120, 120, 120) : new Color(70, 70, 70);
            Fill(_copyRect, copyBg);
            Stroke(_copyRect, Color.White, 1);
            _spriteBatch.DrawString(FontSingleton.ContentFont, "Copy", new Vector2(_copyRect.X + 10, _copyRect.Y + 3), Color.White, 0f, Vector2.Zero, overlay.TextScale, SpriteEffects.None, 0f);
            cursorY += (int)Math.Round(FontSingleton.ContentFont.LineSpacing * overlay.TextScale) + overlay.Padding;

            // Header row
            string hdr = "ID   Name";
            _spriteBatch.DrawString(FontSingleton.ContentFont, hdr, new Vector2(x + overlay.Padding, cursorY), Color.LightGreen, 0f, Vector2.Zero, overlay.TextScale, SpriteEffects.None, 0f);
            cursorY += overlay.RowHeight;

            int visibleTop = y + overlay.Padding;
            int visibleBottom = y + h - overlay.Padding;

            // Entities sorted by Id
            var all = EntityManager.GetAllEntities().OrderBy(e => e.Id).ToList();
            int blockH = overlay.RowHeight * 2; // two lines per entity (id/name + components)
            int startIndex = (int)(overlay.ScrollOffset / blockH);
            int maxRows = Math.Max(0, (visibleBottom - cursorY) / blockH) + 1;
            for (int i = 0; i < maxRows; i++)
            {
                int idx = startIndex + i;
                if (idx < 0 || idx >= all.Count) break;
                int rowY = cursorY + i * blockH - (int)(overlay.ScrollOffset % blockH);
                if (rowY + blockH < visibleTop || rowY > visibleBottom) continue;
                var e = all[idx];

                // First line: ID and Name
                string line1 = $"{e.Id,3}   {e.Name}";
                _spriteBatch.DrawString(FontSingleton.ContentFont, line1, new Vector2(x + overlay.Padding, rowY), Color.White, 0f, Vector2.Zero, overlay.TextScale, SpriteEffects.None, 0f);

                // Second line: component list
                var componentNames = e.GetComponentTypes()
                    .OrderBy(t => t.Name)
                    .Select(t => t.Name);
                string line2 = "- " + string.Join(", ", componentNames);
                int line2Y = rowY + overlay.RowHeight;
                _spriteBatch.DrawString(FontSingleton.ContentFont, line2, new Vector2(x + overlay.Padding + 20, line2Y), Color.LightGray, 0f, Vector2.Zero, overlay.TextScale, SpriteEffects.None, 0f);
            }

        }

        private void EnsureRootComponents(Entity entity, EntityListOverlay overlay)
        {
            if (entity.GetComponent<Transform>() == null)
            {
                EntityManager.AddComponent(entity, new Transform
                {
                    Position = Vector2.Zero,
                    ZOrder = 50000,
                });
            }
            if (entity.GetComponent<UIElement>() == null)
            {
                EntityManager.AddComponent(entity, new UIElement());
            }
            if (entity.GetComponent<InputContext>() == null)
            {
                EntityManager.AddComponent(entity, new InputContext
                {
                    Id = "diagnostic.entity-list",
                    Priority = 910,
                    IsActive = overlay.IsOpen,
                    IsDiagnostic = true,
                });
            }
            if (entity.GetComponent<InputContextMember>() == null)
            {
                EntityManager.AddComponent(entity, new InputContextMember
                {
                    ContextId = "diagnostic.entity-list",
                });
            }
        }

        private Entity EnsureCopyButton(string contextId)
        {
            Entity entity = EntityManager.GetEntity(CopyButtonName);
            if (entity != null) return entity;

            entity = EntityManager.CreateEntity(CopyButtonName);
            EntityManager.AddComponent(entity, new Transform
            {
                Position = Vector2.Zero,
                ZOrder = 50001,
            });
            EntityManager.AddComponent(entity, new UIElement());
            EntityManager.AddComponent(entity, new InputContextMember
            {
                ContextId = contextId,
            });
            EntityManager.AddComponent(entity, new DontDestroyOnLoad());
            return entity;
        }

        private void SetCopyButtonActive(bool active)
        {
            UIElement ui = EntityManager.GetEntity(CopyButtonName)?.GetComponent<UIElement>();
            if (ui == null) return;
            ui.IsInteractable = active;
            if (!active) ui.Bounds = Rectangle.Empty;
        }

        private string BuildEntityListExport()
        {
            var all = EntityManager.GetAllEntities().OrderBy(e => e.Id).ToList();
            var lines = new List<string>(all.Count + 1);
            lines.Add("ID\tName");
            foreach (var e in all)
            {
                lines.Add($"{e.Id}\t{e.Name}");
            }
            return string.Join(Environment.NewLine, lines);
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

        private void Fill(Rectangle rect, Color color)
        {
            _spriteBatch.Draw(_pixel, rect, color);
        }

        private void Stroke(Rectangle rect, Color color, int thickness)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
