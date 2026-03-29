using CommonControls;
﻿using System.Windows;

namespace CommonControls.BaseDialogs.ToolSelector
{
    /// <summary>
    /// Interaction logic for ToolSelectorWindow.xaml
    /// </summary>
    public partial class ToolSelectorWindow : Window
    {
        public ToolSelectorWindow()
        {
            InitializeComponent();
            DarkTitleBarHelper.Enable(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
