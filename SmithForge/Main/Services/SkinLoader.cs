using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;

namespace SmithForge.Main.Services
{
    public static class SkinLoader
    {
        private static readonly ConcurrentDictionary<string, DataTemplate> _cache = new();
        private static DataTemplate? _fallbackTemplate;
        private static readonly object _lockObject = new object();

        public static DataTemplate GetTemplate(string xamlPath)
        {
            try
            {
                // Если путь пустой или файл не существует - возвращаем шаблон по умолчанию
                if (string.IsNullOrEmpty(xamlPath) || !File.Exists(xamlPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Файл не найден: {xamlPath}, используем шаблон по умолчанию");
                    return GetDefaultTemplate();
                }

                // Пробуем получить из кэша
                if (_cache.TryGetValue(xamlPath, out var cachedTemplate))
                {
                    return cachedTemplate;
                }

                // Пробуем загрузить из файла
                try
                {
                    using var fs = new FileStream(xamlPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var dict = (ResourceDictionary)XamlReader.Load(fs);

                    if (dict.Contains("ChatMessageTemplate") && dict["ChatMessageTemplate"] is DataTemplate template)
                    {
                        template.Seal();
                        _cache[xamlPath] = template;
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Успешно загружен шаблон: {xamlPath}");
                        return template;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка загрузки {xamlPath}: {ex.Message}");
                }

                return GetDefaultTemplate();
            }
            catch
            {
                return GetDefaultTemplate();
            }
        }

        private static DataTemplate GetDefaultTemplate()
        {
            if (_fallbackTemplate != null)
                return _fallbackTemplate;

            lock (_lockObject)
            {
                if (_fallbackTemplate != null)
                    return _fallbackTemplate;

                try
                {
                    // Создаем простой, но красивый шаблон через XAML строку (без ColumnDefinitionCollection)
                    const string xaml = @"
                        <DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                            <Border Background=""#2D2D30"" CornerRadius=""4"" Padding=""8,4"" Margin=""2"">
                                <StackPanel>
                                    <Grid Margin=""0,0,0,4"">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width=""*""/>
                                            <ColumnDefinition Width=""Auto""/>
                                        </Grid.ColumnDefinitions>
                                        
                                        <StackPanel Grid.Column=""0"" Orientation=""Horizontal"">
                                            <Border Background=""#404040"" CornerRadius=""3"" Padding=""4,1"" Margin=""0,0,6,0"">
                                                <TextBlock Text=""{Binding MessageNumber, StringFormat=#{0}}"" 
                                                           Foreground=""#FFD700"" FontSize=""9"" FontWeight=""Bold""/>
                                            </Border>
                                        </StackPanel>
                                        
                                        <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                                            <TextBlock Text=""{Binding User.KarmaDisplay}"" 
                                                       Foreground=""#00E5FF"" FontSize=""10"" FontWeight=""Bold"" Margin=""0,0,2,0""/>
                                            <TextBlock Text=""⚡"" Foreground=""#00E5FF"" FontSize=""10""/>
                                        </StackPanel>
                                    </Grid>
                                    
                                    <StackPanel Orientation=""Horizontal"" Margin=""0,2,0,2"">
                                        <TextBlock Text=""{Binding DisplayName}"" 
                                                   Foreground=""White"" FontWeight=""Bold"" FontSize=""13"" Margin=""0,0,4,0""/>
                                        <TextBlock Text="": "" Foreground=""White""/>
                                        <TextBlock Text=""{Binding MessageText}"" 
                                                   Foreground=""White"" TextWrapping=""Wrap"" FontSize=""13""/>
                                    </StackPanel>
                                    
                                    <Grid Margin=""0,4,0,0"">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width=""*""/>
                                            <ColumnDefinition Width=""Auto""/>
                                        </Grid.ColumnDefinitions>
                                        
                                        <StackPanel Grid.Column=""0"" Orientation=""Horizontal"">
                                            <TextBlock Text=""Всего: "" Foreground=""#666666"" FontSize=""9""/>
                                            <TextBlock Text=""{Binding MessageCount}"" Foreground=""#666666"" FontSize=""9""/>
                                        </StackPanel>
                                        
                                        <StackPanel Grid.Column=""1"" Orientation=""Horizontal"">
                                            <TextBlock Text=""+"" Foreground=""#4CAF50"" FontSize=""12"" FontWeight=""Bold"" Margin=""0,-2,2,0""/>
                                            <TextBlock Text=""0"" Foreground=""#4CAF50"" FontSize=""10"" Margin=""0,0,8,0""/>
                                            <TextBlock Text=""-"" Foreground=""#F44336"" FontSize=""14"" FontWeight=""Bold"" Margin=""0,-2,2,0""/>
                                            <TextBlock Text=""0"" Foreground=""#F44336"" FontSize=""10""/>
                                        </StackPanel>
                                    </Grid>
                                </StackPanel>
                            </Border>
                        </DataTemplate>";

                    using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml)))
                    {
                        var template = (DataTemplate)XamlReader.Load(stream);
                        template.Seal();
                        _fallbackTemplate = template;
                        return template;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка создания шаблона по умолчанию: {ex.Message}");

                    // Супер-простой шаблон на случай полного провала
                    try
                    {
                        var simpleTemplate = new DataTemplate();
                        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                        textFactory.SetBinding(TextBlock.TextProperty, new Binding("MessageText"));
                        textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
                        textFactory.SetValue(TextBlock.MarginProperty, new Thickness(5));
                        simpleTemplate.VisualTree = textFactory;
                        simpleTemplate.Seal();
                        return simpleTemplate;
                    }
                    catch
                    {
                        return new DataTemplate();
                    }
                }
            }
        }

        public static void ClearCache()
        {
            try { _cache.Clear(); } catch { }
        }
    }
}