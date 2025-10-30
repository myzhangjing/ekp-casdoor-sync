using System;
using System.IO;
using System.Windows;

namespace SyncEkpToCasdoor.UI;

public partial class ErrorLogWindow : Window
{
    private readonly string _errorMessage;
    private readonly string _logPath;

    public ErrorLogWindow(string errorMessage, string logPath)
    {
        InitializeComponent();
        
        _errorMessage = errorMessage;
        _logPath = logPath;
        
        ErrorTextBox.Text = errorMessage;
        LogPathTextBlock.Text = $"日志文件: {logPath}";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_errorMessage);
            MessageBox.Show("错误信息已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDirectory = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(logDirectory) && Directory.Exists(logDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDirectory);
            }
            else
            {
                MessageBox.Show("日志文件夹不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开日志文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
