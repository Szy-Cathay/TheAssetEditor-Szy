using GameWorld.Core.Components.Input;
using GameWorld.Core.Components.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace GameWorld.Core.Components.Navigation
{
    /// <summary>
    /// Navigation axis gizmo (Blender-style viewport corner axis indicator)
    /// Reference: Blender source/blender/editors/space_view3d/view3d_gizmo_navigate_type.cc
    /// Shows 6 axis endpoints: +X, -X, +Y, -Y, +Z, -Z
    /// </summary>
    public class NavigationGizmo : IDisposable
    {
        private readonly GraphicsDevice _graphics;
        private readonly ArcBallCamera _camera;
        private readonly IMouseComponent _mouse;
        private readonly RenderEngineComponent _renderEngine;

        // Gizmo size and position
        private const float GIZMO_SIZE = 70f;           // Display size (pixels)
        private const float GIZMO_MARGIN = 20f;         // Margin from edge
        private const float AXIS_LENGTH = 1.0f;         // Axis length in local space
#pragma warning disable CA1823 // Unused field - kept for documentation/future use
        private const float AXIS_HANDLE_SIZE = 0.20f;   // Axis endpoint size ratio (Blender: 0.20)
#pragma warning restore CA1823
        private const float HIT_RADIUS = 18f;           // Click detection radius (pixels)
        private const float LINE_THICKNESS = 2f;        // Axis line thickness
        private const float CIRCLE_RADIUS = 8f;         // Label circle radius
        private const float CENTER_RADIUS = 6f;         // Center indicator radius

        // Colors (Blender style)
        private static readonly Color ColorX = new Color(220, 60, 60);    // Red
        private static readonly Color ColorY = new Color(60, 220, 60);    // Green
        private static readonly Color ColorZ = new Color(60, 100, 220);   // Blue
        private static readonly Color ColorHighlight = new Color(255, 200, 50); // Gold
        private static readonly Color ColorOutline = new Color(40, 40, 40); // Dark outline
        private static readonly Color ColorCenter = new Color(80, 80, 80); // Center circle

        // Rendering
        private Texture2D _whiteTexture;

        // State
        private NavigationAxis _hoveredAxis = NavigationAxis.None;
        private Vector2 _screenPosition;

        // Axis data for rendering
        private struct AxisDrawData
        {
            public NavigationAxis Axis;
            public float Depth;
            public Vector2 ScreenPos;
            public bool IsPositive;
            public int AxisIndex; // 0=X, 1=Y, 2=Z
        }

        public NavigationAxis HoveredAxis => _hoveredAxis;

        public event Action<ViewPresetType> ViewPresetRequested;

        public NavigationGizmo(GraphicsDevice graphics, ArcBallCamera camera,
            IMouseComponent mouse, RenderEngineComponent renderEngine)
        {
            _graphics = graphics;
            _camera = camera;
            _mouse = mouse;
            _renderEngine = renderEngine;

            Initialize();
        }

        private void Initialize()
        {
            // Create a 1x1 white texture for drawing shapes
            _whiteTexture = new Texture2D(_graphics, 1, 1);
            _whiteTexture.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Update gizmo state (detect mouse hover)
        /// </summary>
        public void Update(GameTime gameTime)
        {
            // Calculate screen position (top-right corner)
            _screenPosition = new Vector2(
                _graphics.Viewport.Width - GIZMO_MARGIN - GIZMO_SIZE / 2,
                GIZMO_MARGIN + GIZMO_SIZE / 2
            );

            // Detect mouse hover
            _hoveredAxis = HitTestAxis(_mouse.Position());
        }

        /// <summary>
        /// Hit test: check if mouse is near any of the 6 axis endpoints
        /// </summary>
        private NavigationAxis HitTestAxis(Vector2 mousePos)
        {
            var axisEndpoints = GetAllAxisScreenPositions();

            float minDist = float.MaxValue;
            NavigationAxis closestAxis = NavigationAxis.None;

            foreach (var data in axisEndpoints)
            {
                float dist = Vector2.Distance(mousePos, data.ScreenPos);
                if (dist < HIT_RADIUS && dist < minDist)
                {
                    minDist = dist;
                    closestAxis = data.Axis;
                }
            }

            return closestAxis;
        }

        /// <summary>
        /// Get screen positions and data for all 6 axis endpoints
        /// </summary>
        private List<AxisDrawData> GetAllAxisScreenPositions()
        {
            var result = new List<AxisDrawData>();
            var rotationMatrix = Matrix.CreateFromYawPitchRoll(_camera.Yaw, _camera.Pitch, 0);
            float scale = GIZMO_SIZE / (AXIS_LENGTH * 2);

            // 6 axes: +X, -X, +Y, -Y, +Z, -Z
            var axes = new[] { NavigationAxis.PosX, NavigationAxis.NegX,
                              NavigationAxis.PosY, NavigationAxis.NegY,
                              NavigationAxis.PosZ, NavigationAxis.NegZ };

            foreach (var axis in axes)
            {
                int axisIndex = ((int)axis - 1) / 2;  // 0=X, 1=Y, 2=Z
                bool isPositive = ((int)axis - 1) % 2 == 0;

                // Get base axis direction
                var baseDir = axisIndex switch
                {
                    0 => Vector3.UnitX,
                    1 => Vector3.UnitY,
                    2 => Vector3.UnitZ,
                    _ => Vector3.Zero
                };

                // Apply sign and rotate
                var axisEnd = baseDir * (isPositive ? AXIS_LENGTH : -AXIS_LENGTH);
                var rotatedAxis = Vector3.Transform(axisEnd, rotationMatrix);

                // Calculate screen position
                var screenPos = _screenPosition + new Vector2(rotatedAxis.X, -rotatedAxis.Y) * scale;

                result.Add(new AxisDrawData
                {
                    Axis = axis,
                    Depth = rotatedAxis.Z,
                    ScreenPos = screenPos,
                    IsPositive = isPositive,
                    AxisIndex = axisIndex
                });
            }

            return result;
        }

        /// <summary>
        /// Handle mouse click
        /// </summary>
        public bool HandleClick(Vector2 mousePos)
        {
            var hitAxis = HitTestAxis(mousePos);
            if (hitAxis != NavigationAxis.None)
            {
                var viewPreset = ViewPresets.AxisToViewPreset(hitAxis);
                ViewPresetRequested?.Invoke(viewPreset);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Draw navigation gizmo
        /// </summary>
        public void Draw()
        {
            // Save current render state
            var oldDepthStencil = _graphics.DepthStencilState;
            var oldRasterizer = _graphics.RasterizerState;
            var oldBlend = _graphics.BlendState;

            try
            {
                // Set render state for 2D overlay
                _graphics.DepthStencilState = DepthStencilState.None;
                _graphics.RasterizerState = RasterizerState.CullNone;
                _graphics.BlendState = BlendState.AlphaBlend;

                // Begin SpriteBatch for all drawing
                _renderEngine.CommonSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

                // Get all axis data and sort by depth
                var axisDataList = GetAllAxisScreenPositions();
                axisDataList.Sort((a, b) => a.Depth.CompareTo(b.Depth));

                // Draw axes (lines first, then endpoints)
                DrawAxesLines(axisDataList);
                DrawAxisEndpoints(axisDataList);

                // Draw center indicator
                DrawCenterIndicator();
            }
            finally
            {
                // Always end the sprite batch
                _renderEngine.CommonSpriteBatch.End();

                // Restore render state
                _graphics.DepthStencilState = oldDepthStencil;
                _graphics.RasterizerState = oldRasterizer;
                _graphics.BlendState = oldBlend;
            }
        }

        /// <summary>
        /// Draw axis lines from center to endpoints
        /// </summary>
        private void DrawAxesLines(List<AxisDrawData> axisDataList)
        {
            var rotationMatrix = Matrix.CreateFromYawPitchRoll(_camera.Yaw, _camera.Pitch, 0);
            float scale = GIZMO_SIZE / (AXIS_LENGTH * 2);

            // Draw lines for each axis pair (+/-)
            for (int axisIndex = 0; axisIndex < 3; axisIndex++)
            {
                var baseDir = axisIndex switch
                {
                    0 => Vector3.UnitX,
                    1 => Vector3.UnitY,
                    2 => Vector3.UnitZ,
                    _ => Vector3.Zero
                };

                // Get color for this axis
                var axisColor = axisIndex switch
                {
                    0 => ColorX,
                    1 => ColorY,
                    2 => ColorZ,
                    _ => Color.White
                };

                // Check if either positive or negative is hovered
                bool isHovered = (_hoveredAxis == (NavigationAxis)(axisIndex * 2 + 1)) ||
                                 (_hoveredAxis == (NavigationAxis)(axisIndex * 2 + 2));
                if (isHovered)
                    axisColor = ColorHighlight;

                // Draw positive line (from center to +endpoint)
                var posEnd = baseDir * AXIS_LENGTH;
                var rotatedPos = Vector3.Transform(posEnd, rotationMatrix);
                var posScreen = _screenPosition + new Vector2(rotatedPos.X, -rotatedPos.Y) * scale;
                DrawThickLine(_screenPosition, posScreen, axisColor * 0.8f, LINE_THICKNESS);

                // Draw negative line (from center to -endpoint)
                var negEnd = -baseDir * AXIS_LENGTH;
                var rotatedNeg = Vector3.Transform(negEnd, rotationMatrix);
                var negScreen = _screenPosition + new Vector2(rotatedNeg.X, -rotatedNeg.Y) * scale;
                DrawThickLine(_screenPosition, negScreen, axisColor * 0.6f, LINE_THICKNESS);
            }
        }

        /// <summary>
        /// Draw axis endpoint circles (6 endpoints)
        /// </summary>
        private void DrawAxisEndpoints(List<AxisDrawData> axisDataList)
        {
            foreach (var data in axisDataList)
            {
                bool isHovered = (_hoveredAxis == data.Axis);
                bool isFront = data.Depth <= 0;

                // Get base color
                var baseColor = data.AxisIndex switch
                {
                    0 => ColorX,
                    1 => ColorY,
                    2 => ColorZ,
                    _ => Color.White
                };

                // Determine final color
                Color circleColor;
                if (isHovered)
                {
                    circleColor = ColorHighlight;
                }
                else if (data.IsPositive)
                {
                    // Positive axis: full color
                    circleColor = baseColor * (isFront ? 1.0f : 0.7f);
                }
                else
                {
                    // Negative axis: dimmer, blended with background for back-facing
                    if (isFront)
                    {
                        // Front-facing negative: blend with white
                        circleColor = new Color(
                            (int)(baseColor.R * 0.5f + 127),
                            (int)(baseColor.G * 0.5f + 127),
                            (int)(baseColor.B * 0.5f + 127),
                            220
                        );
                    }
                    else
                    {
                        // Back-facing negative: very dim
                        circleColor = baseColor * 0.4f;
                    }
                }

                // Draw circle background
                DrawFilledCircle(data.ScreenPos, CIRCLE_RADIUS, circleColor);

                // Draw outline
                DrawCircleOutline(data.ScreenPos, CIRCLE_RADIUS, ColorOutline * 0.8f, 1f);

                // Draw label only for positive axes
                if (data.IsPositive)
                {
                    DrawAxisLabel(data.AxisIndex, data.ScreenPos, isHovered);
                }
            }
        }

        /// <summary>
        /// Draw axis label (X, Y, Z) - only for positive axes
        /// </summary>
        private void DrawAxisLabel(int axisIndex, Vector2 position, bool isHovered)
        {
            string label = axisIndex switch
            {
                0 => "X",
                1 => "Y",
                2 => "Z",
                _ => ""
            };

            var font = _renderEngine.DefaultFont;
            var textSize = font.MeasureString(label);
            var textPos = position - textSize / 2;

            // Draw outline for better visibility
            Color outlineColor = ColorOutline;
            for (int ox = -1; ox <= 1; ox++)
            {
                for (int oy = -1; oy <= 1; oy++)
                {
                    if (ox != 0 || oy != 0)
                    {
                        _renderEngine.CommonSpriteBatch.DrawString(
                            font,
                            label,
                            textPos + new Vector2(ox, oy),
                            outlineColor
                        );
                    }
                }
            }

            // Draw main text
            _renderEngine.CommonSpriteBatch.DrawString(
                font,
                label,
                textPos,
                Color.White
            );
        }

        /// <summary>
        /// Draw center indicator (Blender style: shows projection mode)
        /// </summary>
        private void DrawCenterIndicator()
        {
            // Draw center circle background
            DrawFilledCircle(_screenPosition, CENTER_RADIUS, ColorCenter * 0.9f);

            // Draw projection mode indicator
            bool isPerspective = _camera.CurrentProjectionType == ProjectionType.Perspective;

            if (isPerspective)
            {
                // Perspective: draw a small filled circle
                DrawFilledCircle(_screenPosition, CENTER_RADIUS * 0.4f, Color.White * 0.8f);
            }
            else
            {
                // Ortho: draw a small outline square
                float size = CENTER_RADIUS * 0.5f;
                var p1 = _screenPosition + new Vector2(-size, -size);
                var p2 = _screenPosition + new Vector2(size, -size);
                var p3 = _screenPosition + new Vector2(size, size);
                var p4 = _screenPosition + new Vector2(-size, size);
                DrawThickLine(p1, p2, Color.White * 0.8f, 1.5f);
                DrawThickLine(p2, p3, Color.White * 0.8f, 1.5f);
                DrawThickLine(p3, p4, Color.White * 0.8f, 1.5f);
                DrawThickLine(p4, p1, Color.White * 0.8f, 1.5f);
            }

            // Draw outline
            DrawCircleOutline(_screenPosition, CENTER_RADIUS, ColorOutline * 0.7f, 1f);
        }

        private void DrawThickLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.001f) return; // Skip zero-length lines

            float angle = (float)Math.Atan2(delta.Y, delta.X);

            var origin = new Vector2(0, 0.5f);
            var scale = new Vector2(length, thickness);

            _renderEngine.CommonSpriteBatch.Draw(
                _whiteTexture,
                start,
                null,
                color,
                angle,
                origin,
                scale,
                SpriteEffects.None,
                0
            );
        }

        private void DrawFilledCircle(Vector2 center, float radius, Color color)
        {
            int r = (int)Math.Ceiling(radius);
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        _renderEngine.CommonSpriteBatch.Draw(
                            _whiteTexture,
                            new Vector2((int)(center.X + x), (int)(center.Y + y)),
                            color
                        );
                    }
                }
            }
        }

        private void DrawCircleOutline(Vector2 center, float radius, Color color, float thickness)
        {
            int segments = 24;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;

                DrawThickLine(p1, p2, color, thickness);
            }
        }

        public void Dispose()
        {
            _whiteTexture?.Dispose();
        }
    }
}
