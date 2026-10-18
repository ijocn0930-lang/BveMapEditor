using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace BveMapEditor
{
    public partial class MainWindow : Window
    {
        private void MenuBveSite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        //サイト
        private void MenuBveSiteGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/edit/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        //サイト
        private void MenuBveformat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // BVE Trainsim の公式サイトURL
                string url = "https://bvets.net/jp/edit/formats/";

                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"ブラウザを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
