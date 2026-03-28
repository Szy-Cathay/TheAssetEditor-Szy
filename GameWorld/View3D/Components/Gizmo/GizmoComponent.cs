using System;
using GameWorld.Core.Commands;
using GameWorld.Core.Components.Input;
using GameWorld.Core.Components.Rendering;
using GameWorld.Core.Components.Selection;
using GameWorld.Core.Services;
using GameWorld.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Shared.Core.Events;

namespace GameWorld.Core.Components.Gizmo
{
    public class GizmoComponent : BaseComponent, IDisposable
    {
        private readonly IMouseComponent _mouse;
        private readonly IEventHub _eventHub;

        private readonly IKeyboardComponent _keyboard;
        private readonly SelectionManager _selectionManager;
        private readonly CommandExecutor _commandManager;
        private readonly ArcBallCamera _camera;
        private readonly RenderEngineComponent _resourceLibary;
        private readonly IDeviceResolver _deviceResolverComponent;
        private readonly CommandFactory _commandFactory;
        Gizmo _gizmo;
        bool _isEnabled = false;
        TransformGizmoWrapper _activeTransformation;
        bool _isCtrlPressed = false;


        public GizmoComponent(IEventHub eventHub,
            IKeyboardComponent keyboardComponent, IMouseComponent mouseComponent, ArcBallCamera camera, CommandExecutor commandExecutor,
            RenderEngineComponent resourceLibary, IDeviceResolver deviceResolverComponent, CommandFactory commandFactory,
            SelectionManager selectionManager)
        {
            UpdateOrder = (int)ComponentUpdateOrderEnum.Gizmo;
            DrawOrder = (int)ComponentDrawOrderEnum.Gizmo;
            _eventHub = eventHub;
            _keyboard = keyboardComponent;
            _mouse = mouseComponent;
            _camera = camera;
            _commandManager = commandExecutor;
            _resourceLibary = resourceLibary;
            _deviceResolverComponent = deviceResolverComponent;
            _commandFactory = commandFactory;
            _selectionManager = selectionManager;

            _eventHub.Register<SelectionChangedEvent>(this, Handle);
        }

        public override void Initialize()
        {
            _gizmo = new Gizmo(_camera, _mouse, _deviceResolverComponent.Device, _resourceLibary);
            _gizmo.ActivePivot = PivotType.ObjectCenter;
            _gizmo.SetKeyboard(_keyboard);  // Enable keyboard axis locking
            _gizmo.TranslateEvent += GizmoTranslateEvent;
            _gizmo.RotateEvent += GizmoRotateEvent;
            _gizmo.ScaleEvent += GizmoScaleEvent;
            _gizmo.StartEvent += GizmoTransformStart;
            _gizmo.StopEvent += GizmoTransformEnd;
            _gizmo.RequestRestoreInitialState += OnRequestRestoreInitialState;
        }

        /// <summary>
        /// Get the Gizmo instance (for SelectionComponent to check modal transform state)
        /// </summary>
        public Gizmo Gizmo => _gizmo;

        private void OnSelectionChanged(ISelectionState state)
        {
            _gizmo.Selection.Clear();
            _activeTransformation = TransformGizmoWrapper.CreateFromSelectionState(state, _commandFactory);
            if (_activeTransformation != null)
                _gizmo.Selection.Add(_activeTransformation);

            _gizmo.ResetDeltas();
            // Note: Don't auto-enable Gizmo here - user must click toolbar icon first
        }

        /// <summary>
        /// Called when Gizmo needs to restore initial state for Blender-style modal transform
        /// (for rotation and scale, which calculate from initial state each frame)
        /// </summary>
        private void OnRequestRestoreInitialState()
        {
            if (_activeTransformation?.HasBackup == true)
            {
                // Restore vertices and reset transform state
                // The subsequent transform event will build the new total transform from scratch
                _activeTransformation.RestoreVertexState(resetTransform: true);
            }
        }

        private void GizmoTransformStart()
        {
            // Only set mouse owner, don't start command here
            // Command will be started in GizmoTransformEnd for confirm
            _mouse.MouseOwner = this;
        }

        private void GizmoTransformEnd()
        {
            // Check if this is a cancel operation
            if (_gizmo.IsModalCancelled)
            {
                // Cancel: restore vertices to initial state (like Blender's restoreTransObjects)
                // Reset transform state as well since we're going back to initial
                _activeTransformation?.RestoreVertexState(resetTransform: true);
                _activeTransformation?.ClearBackup();
                _gizmo.IsModalCancelled = false;
            }
            else
            {
                // Confirm: record the final transform for undo/redo
                // Use ConfirmModalTransform to avoid the Start() method which resets _totalGizomTransform
                _activeTransformation?.ClearBackup();
                _activeTransformation?.ConfirmModalTransform(_commandManager);
            }

            // Reset _isEnabled after modal transform ends
            // Gizmo should only be visible when explicitly enabled via toolbar button
            _isEnabled = false;

            if (_mouse.MouseOwner == this)
            {
                _mouse.MouseOwner = null;
                _mouse.ClearStates();
            }
        }


        private void GizmoTranslateEvent(ITransformable transformable, TransformationEventArgs e)
        {
            _activeTransformation.GizmoTranslateEvent((Vector3)e.Value, e.Pivot);
        }

        private void GizmoRotateEvent(ITransformable transformable, TransformationEventArgs e)
        {
            _activeTransformation.GizmoRotateEvent((Matrix)e.Value, e.Pivot);
        }

        private void GizmoScaleEvent(ITransformable transformable, TransformationEventArgs e)
        {
            var value = (Vector3)e.Value;
            if (_isCtrlPressed)
            {
                if (value.X != 0)
                    value = new Vector3(value.X);
                else if (value.Y != 0)
                    value = new Vector3(value.Y);
                else if (value.Z != 0)
                    value = new Vector3(value.Z);
            }

            _activeTransformation.GizmoScaleEvent(value, e.Pivot);
        }

        public override void Update(GameTime gameTime)
        {
            var selectionMode = _selectionManager.GetState().Mode;
            switch (selectionMode)
            {
                case GeometrySelectionMode.Object:
                case GeometrySelectionMode.Face:
                case GeometrySelectionMode.Vertex:
                case GeometrySelectionMode.Bone:
                    break;
                default:
                    return;
            }

            // Blender-style hotkey triggers for modal transform
            // G = Translate, R = Rotate, S = Scale
            // Active whenever there is a selection (no need to enable Gizmo first)
            // Press hotkey to enter modal transform mode, mouse moves to transform
            // Left click to confirm, Right click or Escape to cancel
            if (_gizmo.Selection.Count > 0 && !_gizmo.IsInModalTransform)
            {
                if (_keyboard.IsKeyReleased(Keys.G))
                {
                    StartModalTransform(GizmoMode.Translate);
                    return;
                }
                else if (_keyboard.IsKeyReleased(Keys.R))
                {
                    StartModalTransform(GizmoMode.Rotate);
                    return;
                }
                else if (_keyboard.IsKeyReleased(Keys.S))
                {
                    StartModalTransform(GizmoMode.NonUniformScale);
                    return;
                }
            }

            // Handle modal transform updates
            if (_gizmo.IsInModalTransform)
            {
                var isCameraMoving = _keyboard.IsKeyDown(Keys.LeftAlt);
                _gizmo.Update(gameTime, !isCameraMoving);
                return;
            }

            if (!_isEnabled)
                return;

            _isCtrlPressed = _keyboard.IsKeyDown(Keys.LeftControl);
            if (_gizmo.ActiveMode == GizmoMode.NonUniformScale && _isCtrlPressed)
                _gizmo.ActiveMode = GizmoMode.UniformScale;
            else if (_gizmo.ActiveMode == GizmoMode.UniformScale && !_isCtrlPressed)
                _gizmo.ActiveMode = GizmoMode.NonUniformScale;

            var isCameraMoving2 = _keyboard.IsKeyDown(Keys.LeftAlt);
            _gizmo.Update(gameTime, !isCameraMoving2);
        }

        /// <summary>
        /// Start Blender-style modal transform
        /// </summary>
        private void StartModalTransform(GizmoMode mode)
        {
            // Don't set _isEnabled = true - Gizmo should not be visible during modal transform
            _mouse.MouseOwner = this;

            // Backup initial vertex state (like Blender's createTransData)
            // This allows cancel to restore vertices to original positions
            _activeTransformation?.BackupVertexState();

            _gizmo.StartModalTransform(mode);
        }

        public void SetGizmoMode(GizmoMode mode)
        {
            _gizmo.ActiveMode = mode;
            _isEnabled = true;
        }

        public void SetGizmoPivot(PivotType type)
        {
            _gizmo.ActivePivot = type;
        }

        public void Disable()
        {
            _isEnabled = false;
        }

        public override void Draw(GameTime gameTime)
        {
            var selectionMode = _selectionManager.GetState().Mode;

            switch (selectionMode)
            {
                case GeometrySelectionMode.Object:
                case GeometrySelectionMode.Face:
                case GeometrySelectionMode.Vertex:
                case GeometrySelectionMode.Bone:
                    break;
                default:
                    return;
            }

            // During modal transform, always draw (for dashed line visuals)
            // Otherwise, only draw if gizmo is enabled
            if (!_isEnabled && !_gizmo.IsInModalTransform)
                return;

            _gizmo.Draw();
        }

        public void ResetScale()
        {
            _gizmo.ScaleModifier = 1;
        }

        public void ModifyGizmoScale(float v)
        {
            _gizmo.ScaleModifier += v;
        }

        public void Dispose()
        {
            _gizmo.Dispose();
        }

        public void Handle(SelectionChangedEvent notification) => OnSelectionChanged(notification.NewState);
    }
}
