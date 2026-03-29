using System.Windows.Controls;
using System.Windows;
using CommonControls;


namespace AssetManagement.Strategies.Fbx.Views.FBXSettings
{
    /// <summary>
    /// Interaction logic for d.xaml
    /// </summary>
    public partial class FBXSetttingsView : Window
    {
        public FBXSetttingsView()
        {
            InitializeComponent();
            DarkTitleBarHelper.Enable(this);

            ImportButton.Click += ImportButton_Click;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)     
        {
            
            DialogResult = true;
            Close();
        }

    }
}
