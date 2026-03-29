using CommonControls;
﻿using System.Windows;

namespace AssetManagement.Strategies.Fbx.ImportDialog.Views
{
    /// <summary>
    /// Interaction logic for d.xaml
    /// </summary>
    public partial class FbxSettingsDialogView : Window
    {
        public FbxSettingsDialogView()
        {
            InitializeComponent();
            DarkTitleBarHelper.Enable(this);
            UpdateLayout();
            ImportButton.Click += ImportButton_Click;
            this.DataContext = this;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)     
        {            
            DialogResult = true;
            Close();
        }
    }
}
