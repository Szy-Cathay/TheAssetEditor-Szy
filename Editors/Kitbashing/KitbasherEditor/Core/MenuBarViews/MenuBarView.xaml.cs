using Shared.Ui.Common.MenuSystem;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KitbasherEditor.Views
{
    /// <summary>
    /// Interaction logic for MenuBarView.xaml
    /// </summary>
    public partial class MenuBarView : UserControl
    {
        public MenuBarView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.KeyUp += HandleKeyPress;
                window.KeyDown += HandleKeyDown;
            }
        }

        private void HandleKeyPress(object sender, KeyEventArgs e)
        {
            // Only handle keyboard events if this editor is visible (active tab)
            if (!IsEditorVisible())
                return;

            if (e.OriginalSource is TextBox)
            {
                e.Handled = true;
                return;
            }

            if (DataContext is IKeyboardHandler keyboardHandler)
            {
                var res = keyboardHandler.OnKeyReleased(e.Key, e.SystemKey, Keyboard.Modifiers);
                if (res)
                    e.Handled = true;
            }
        }

        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            // Only handle keyboard events if this editor is visible (active tab)
            if (!IsEditorVisible())
                return;

            if (DataContext is IKeyboardHandler keyboardHandler)
            {
                keyboardHandler.OnKeyDown(e.Key, e.SystemKey, Keyboard.Modifiers);
            }
        }

        /// <summary>
        /// Check if this editor is currently visible (active tab in the tab control)
        /// This prevents keyboard events from being processed by inactive editors
        /// </summary>
        private bool IsEditorVisible()
        {
            // Check if the control is visible and rendered
            if (!IsVisible)
                return false;

            // Check if the control has positive actual width/height (rendered)
            if (ActualWidth == 0 || ActualHeight == 0)
                return false;

            // Walk up the visual tree to check if any parent is collapsed or hidden
            DependencyObject current = this;
            while (current != null)
            {
                if (current is FrameworkElement element)
                {
                    if (!element.IsVisible)
                        return false;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return true;
        }
    }
}
