using SmithForge.Features.ChaterManager;
using SmithForge.Main.Models;
using System;
using System.Windows;

namespace SmithForge.Features.ChaterProfile
{
    public partial class ChaterProfileWindow : Window
    {
        public ChaterProfileWindow(Chater chater)
        {
            InitializeComponent();
            DataContext = new ChaterProfileViewModel(chater);
            Owner = Application.Current.MainWindow;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}