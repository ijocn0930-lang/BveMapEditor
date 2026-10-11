using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.Json;

namespace BveMapEditor
{
    /// <summary>
    /// FontSettingsDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class FontSettingsDialog : Window
    {
        public FontFamily? SelectedFontFamily { get; private set; }
        public double SelectedFontSize { get; private set; }
        public FontSettingsDialog(FontFamily currentFamily, double currentSize)
        {
            InitializeComponent();
            // システムにインストールされているフォント一覧を設定
            FontFamilyComboBox.ItemsSource = Fonts.SystemFontFamilies;
            FontFamilyComboBox.SelectedItem = currentFamily;

            FontSizeTextBox.Text = currentSize.ToString();
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is FontFamily family)
            {
                SelectedFontFamily = family;
            }

            // テキストボックスの文字列が空、または有効な数値に変換できない場合
            string inputSizeText = FontSizeTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(inputSizeText) || !double.TryParse(inputSizeText, out double size) || size <= 0)
            {
                MessageBox.Show(
                    "有効なフォントサイズを入力してください。",
                    "警告 - フォント設定",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // ダイアログを閉じずにフォーカスを移して再入力を促す
                FontSizeTextBox.Focus();
                FontSizeTextBox.SelectAll();
                return;
            }
            // ★ 制限値（最小 6、最大 72）を超えている場合は自動で制限内に収める（クランプする）
            if (size < 6)
            {
                size = 6;
            }
            else if (size > 72)
            {
                size = 72;
            }
            SelectedFontSize = size;

            DialogResult = true;
            Close();
        }        // サイズを大きくするボタン
        private void FontZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FontSizeTextBox.Text, out double size))
            {
                if (size < 72) // 最大値の制限（例: 72まで）
                {
                    FontSizeTextBox.Text = (size + 1).ToString();
                }
            }
            else
            {
                FontSizeTextBox.Text = "14";
            }
        }

        // サイズを小さくするボタン
        private void FontZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FontSizeTextBox.Text, out double size))
            {
                if (size > 6) // 最小値の制限（例: 6を下回らないように）
                {
                    FontSizeTextBox.Text = (size - 1).ToString();
                }
            }
            else
            {
                FontSizeTextBox.Text = "14";
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

}
