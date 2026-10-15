using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class AddHostingAccount : ChromelessWindow
    {
        public AddHostingAccount()
        {
            CloseOnESC = true;
            InitializeComponent();
        }

        private async void OnSure(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ViewModels.AddHostingAccount vm && await vm.SaveAsync())
                Close();
        }

        private async void OnSignInWithBrowser(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            if (DataContext is ViewModels.AddHostingAccount vm && await vm.SignInWithBrowserAsync())
                Close();
        }

        private void OnCancel(object _, RoutedEventArgs e)
        {
            e.Handled = true;
            Close();
        }
    }
}
