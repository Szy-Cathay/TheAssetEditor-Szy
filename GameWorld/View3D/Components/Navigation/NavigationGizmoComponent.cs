using GameWorld.Core.Components.Input;
using GameWorld.Core.Components.Rendering;
using GameWorld.Core.Services;
using GameWorld.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace GameWorld.Core.Components.Navigation
{
    /// <summary>
    /// Navigation Gizmo Component - Integrates navigation indicator, view switching and camera transition
    /// </summary>
    public class NavigationGizmoComponent : BaseComponent, IDisposable
    {
        private readonly ArcBallCamera _camera;
        private readonly IKeyboardComponent _keyboard;
        private readonly IMouseComponent _mouse;
        private readonly RenderEngineComponent _renderEngine;
        private readonly IDeviceResolver _deviceResolver;
        private readonly FocusSelectableObjectService _focusService;

        private NavigationGizmo _navigationGizmo;
        private CameraTransition _cameraTransition;

        // Orthographic view state
        private bool _isInOrthoView = false;
        private ViewPresetType _currentOrthoView = ViewPresetType.Perspective;

        public bool IsInOrthoView => _isInOrthoView;
        public ViewPresetType CurrentView => _currentOrthoView;

        public NavigationGizmoComponent(
            ArcBallCamera camera,
            IKeyboardComponent keyboard,
            IMouseComponent mouse,
            RenderEngineComponent renderEngine,
            IDeviceResolver deviceResolver,
            FocusSelectableObjectService focusService)
        {
            _camera = camera;
            _keyboard = keyboard;
            _mouse = mouse;
            _renderEngine = renderEngine;
            _deviceResolver = deviceResolver;
            _focusService = focusService;

            UpdateOrder = (int)ComponentUpdateOrderEnum.NavigationGizmo;
            DrawOrder = (int)ComponentDrawOrderEnum.NavigationGizmo;
        }

        public override void Initialize()
        {
            _navigationGizmo = new NavigationGizmo(
                _deviceResolver.Device,
                _camera,
                _mouse,
                _renderEngine
            );

            _cameraTransition = new CameraTransition(_camera);

            // Subscribe to gizmo view preset requests
            _navigationGizmo.ViewPresetRequested += OnViewPresetRequested;
        }

        public override void Update(GameTime gameTime)
        {
            // Update camera transition animation
            _cameraTransition.Update(gameTime);

            // If transitioning, skip other processing
            if (_cameraTransition.IsTransitioning)
                return;

            // Handle numpad shortcuts
            HandleNumpadShortcuts();

            // Update gizmo hover state
            _navigationGizmo.Update(gameTime);

            // Handle mouse click on navigation gizmo
            if (_mouse.IsMouseButtonPressed(MouseButton.Left))
            {
                if (_navigationGizmo.HandleClick(_mouse.Position()))
                {
                    // Clicked on gizmo - don't steal ownership, just trigger view switch
                    // Mouse ownership should remain free for other components
                }
            }

            // Blender behavior: middle-mouse rotation in orthographic mode stays orthographic.
            // No need to exit ortho view - the camera handles rotation naturally in both modes.
        }

        private void HandleNumpadShortcuts()
        {
            // Numpad 1 - Front view / Ctrl+Numpad1 - Back view
            if (_keyboard.IsKeyReleased(Keys.NumPad1))
            {
                var view = IsCtrlDown()
                    ? ViewPresetType.Back
                    : ViewPresetType.Front;
                SwitchToView(view);
            }

            // Numpad 3 - Right view / Ctrl+Numpad3 - Left view
            if (_keyboard.IsKeyReleased(Keys.NumPad3))
            {
                var view = IsCtrlDown()
                    ? ViewPresetType.Left
                    : ViewPresetType.Right;
                SwitchToView(view);
            }

            // Numpad 7 - Top view / Ctrl+Numpad7 - Bottom view
            if (_keyboard.IsKeyReleased(Keys.NumPad7))
            {
                var view = IsCtrlDown()
                    ? ViewPresetType.Bottom
                    : ViewPresetType.Top;
                SwitchToView(view);
            }

            // Numpad 5 - Toggle perspective/orthographic
            if (_keyboard.IsKeyReleased(Keys.NumPad5))
            {
                ToggleProjectionType();
            }

            // Numpad . (Decimal) - Focus on selection (Blender style)
            if (_keyboard.IsKeyReleased(Keys.Decimal))
            {
                _focusService?.FocusSelection();
            }
        }

        private bool IsCtrlDown()
        {
            return _keyboard.IsKeyDown(Keys.LeftControl) || _keyboard.IsKeyDown(Keys.RightControl);
        }

        private void SwitchToView(ViewPresetType view)
        {
            _currentOrthoView = view;
            _isInOrthoView = (view != ViewPresetType.Perspective);
            _cameraTransition.StartTransition(view, _camera.LookAt);
        }

        private void ExitOrthoView()
        {
            _isInOrthoView = false;
            _currentOrthoView = ViewPresetType.Perspective;
            _cameraTransition.StartTransition(ViewPresetType.Perspective);
        }

        private void ToggleProjectionType()
        {
            if (_isInOrthoView)
            {
                // Exit ortho: switch to perspective, keep current camera angles (Blender behavior)
                _isInOrthoView = false;
                _currentOrthoView = ViewPresetType.Perspective;
                _camera.CurrentProjectionType = ProjectionType.Perspective;
            }
            else
            {
                // Enter ortho: just toggle projection type, keep current camera angles (Blender behavior)
                // Blender's Numpad 5 only flips rv3d->persp without changing view rotation
                _isInOrthoView = true;
                _currentOrthoView = ViewPresetType.Perspective; // No specific axis view
                _camera.CurrentProjectionType = ProjectionType.Orthographic;
                _camera.OrthoSize = _camera.Zoom * 0.5f;
            }
        }

        private void HandleOrthoPan()
        {
            var delta = _mouse.DeltaPosition();
            _camera.MoveOrthoCamera(delta);
        }

        private void OnViewPresetRequested(ViewPresetType view)
        {
            SwitchToView(view);
        }

        public override void Draw(GameTime gameTime)
        {
            // Draw navigation gizmo (always on top)
            _navigationGizmo?.Draw();
        }

        public void Dispose()
        {
            _navigationGizmo?.Dispose();
        }
    }
}
