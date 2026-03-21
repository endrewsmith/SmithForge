using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] GetTemplate вызван с путем: {xamlPath}");

                string directory = Path.GetDirectoryName(xamlPath) ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Directory: {directory}");

                string fileName = Path.GetFileNameWithoutExtension(xamlPath);
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] FileName: {fileName}");

                int requestedRank = 0;
                if (fileName.StartsWith("rank_"))
                    int.TryParse(fileName.Replace("rank_", ""), out requestedRank);

                System.Diagnostics.Debug.WriteLine($"[SkinLoader] RequestedRank: {requestedRank}");

                // 1. КАСКАД: Пробуем от запрошенного ранга вниз до 1
                for (int rank = requestedRank; rank > 0; rank--)
                {
                    string pathToTry = Path.Combine(directory, $"rank_{rank}.xaml");
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Пробуем: {pathToTry}");
                    var t = TryLoadAndValidate(pathToTry);
                    if (t != null) return t;
                }

                // 2. Пробуем rank_0
                string rank0Path = Path.Combine(directory, "rank_0.xaml");
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Пробуем rank_0: {rank0Path}");
                var r0 = TryLoadAndValidate(rank0Path);
                if (r0 != null) return r0;

                // 3. Пробуем глобальный дефолт
                string? skinsDir = Path.GetDirectoryName(directory);
                if (skinsDir != null)
                {
                    string defaultPath = Path.Combine(skinsDir, "Default", "default.xaml");
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Пробуем default: {defaultPath}");
                    var def = TryLoadAndValidate(defaultPath);
                    if (def != null) return def;
                }

                return GetHardcodedTemplate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка в GetTemplate: {ex.Message}");
                return GetHardcodedTemplate();
            }
        }

        private static DataTemplate? TryLoadAndValidate(string path)
        {
            if (!File.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Файл не существует: {path}");
                return null;
            }

            // Если файл уже в кэше и он прошел валидацию ранее — отдаем
            if (_cache.TryGetValue(path, out var cached)) return cached;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Попытка загрузки: {path}");

                using (var stream = File.OpenRead(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Файл открыт, размер: {stream.Length} байт");

                    var pc = new ParserContext();
                    pc.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                    pc.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                    pc.XmlnsDictionary.Add("utils", "clr-namespace:SmithForge.Features.ChatOverlay;assembly=SmithForge");
                    pc.XmlnsDictionary.Add("cm", "clr-namespace:SmithForge.Features.ChaterManager;assembly=SmithForge");

                    // ЭТАП 1: Парсинг
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Начинаем парсинг XAML...");
                    var content = XamlReader.Load(stream, pc);
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Парсинг успешен, тип: {content?.GetType()}");

                    DataTemplate? template = null;
                    if (content is ResourceDictionary dict)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Это ResourceDictionary, ключи: {string.Join(", ", dict.Keys.Cast<object>())}");
                        if (dict.Contains("ChatMessageTemplate"))
                        {
                            template = dict["ChatMessageTemplate"] as DataTemplate;
                            System.Diagnostics.Debug.WriteLine($"[SkinLoader] ChatMessageTemplate найден, это DataTemplate: {template != null}");
                        }
                    }
                    else if (content is DataTemplate direct)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Это прямой DataTemplate");
                        template = direct;
                    }

                    if (template != null)
                    {
                        // ЭТАП 2: Тестовая отрисовка
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Тестовая отрисовка...");
                        template.LoadContent();
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Тестовая отрисовка успешна");

                        template.Seal();
                        _cache[path] = template;
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Шаблон закэширован и возвращен");
                        return template;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[SkinLoader] Шаблон не найден в файле");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] ОШИБКА загрузки {Path.GetFileName(path)}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] StackTrace: {ex.StackTrace}");
            }
            return null;
        }

        public static DataTemplate GetStickerTemplate()
        {
            try
            {
                string stickerPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "SF_Data", "Assets", "Skins", "Stickers", "sticker_template.xaml");

                // Прямая загрузка без поиска рангов
                return LoadDirectTemplate(stickerPath) ?? GetHardcodedTemplate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка загрузки шаблона стикера: {ex.Message}");
                return GetHardcodedTemplate();
            }
        }

        private static DataTemplate? LoadDirectTemplate(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Файл не существует: {path}");
                    return null;
                }

                using (var stream = File.OpenRead(path))
                {
                    var pc = new ParserContext();
                    pc.XmlnsDictionary.Add("", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
                    pc.XmlnsDictionary.Add("x", "http://schemas.microsoft.com/winfx/2006/xaml");
                    pc.XmlnsDictionary.Add("gif", "clr-namespace:XamlAnimatedGif;assembly=XamlAnimatedGif");

                    var content = XamlReader.Load(stream, pc);

                    if (content is ResourceDictionary dict && dict.Contains("ChatMessageTemplate"))
                    {
                        var template = dict["ChatMessageTemplate"] as DataTemplate;
                        template?.Seal();
                        return template;
                    }
                    else if (content is DataTemplate direct)
                    {
                        direct.Seal();
                        return direct;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка загрузки шаблона стикера: {ex.Message}");
            }
            return null;
        }

        private static DataTemplate GetHardcodedTemplate()
        {
            if (_fallbackTemplate != null) return _fallbackTemplate;
            lock (_lockObject)
            {
                if (_fallbackTemplate != null) return _fallbackTemplate;

                try
                {
                    const string xaml = @"
<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
              xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
              xmlns:local=""clr-namespace:SmithForge.Main.Services;assembly=SmithForge"">
    
    <DataTemplate.Resources>
        <local:StepToVisibilityConverter x:Key=""StepToVisibilityConverter""/>
    </DataTemplate.Resources>
    
    <Border Background=""#2D2D30"" CornerRadius=""4"" Padding=""0,0,0,2"" Margin=""2"">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""*""/>
                <ColumnDefinition Width=""Auto""/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column=""0"" TextWrapping=""Wrap"" FontSize=""15"" >
                <Run Text=""{Binding DisplayName, Mode=OneWay}"" FontWeight=""Bold"" Foreground=""White""/>
                <Run Text="": "" Foreground=""White""/>
                <Run Text=""{Binding MessageText, Mode=OneWay}"" Foreground=""#F0F0F0""/>
            </TextBlock>

            <!-- Контейнер для черточек и звезды -->
            <Grid Grid.Column=""1"" Margin=""8,0,0,0"" VerticalAlignment=""Bottom"">
                
                <!-- Черточки -->
                <StackPanel x:Name=""StepPanel"" VerticalAlignment=""Bottom"">
                    <Border x:Name=""Step10"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FF4500"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""10""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step9"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FF6347"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""9""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step8"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FF7F50"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""8""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step7"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FF8C00"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""7""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step6"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFA500"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""6""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step5"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFB347"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""5""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step4"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFC84D"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""4""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step3"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFD700"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""3""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step2"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFEA00"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""2""/>
                        </Border.Visibility>
                    </Border>
                    <Border x:Name=""Step1"" Height=""1.5"" Width=""4"" Margin=""0,0.5"" Background=""#FFFF00"" Opacity=""0"">
                        <Border.Visibility>
                            <Binding Path=""MessageCount"" Converter=""{StaticResource StepToVisibilityConverter}"" ConverterParameter=""1""/>
                        </Border.Visibility>
                    </Border>
                </StackPanel>

                <!-- Звезда -->
                <TextBlock x:Name=""RankStar"" 
                           Text=""★"" 
                           FontSize=""16"" 
                           Foreground=""#FFD700""
                           HorizontalAlignment=""Center""
                           VerticalAlignment=""Center""
                           Opacity=""0"">
                    <TextBlock.RenderTransform>
                        <ScaleTransform x:Name=""StarScale"" ScaleX=""1"" ScaleY=""1""/>
                    </TextBlock.RenderTransform>
                </TextBlock>
            </Grid>
        </Grid>
    </Border>

    <DataTemplate.Triggers>
    <DataTrigger Binding=""{Binding User.TotalKarma}"" Value=""0"">
        <DataTrigger.EnterActions>
            <BeginStoryboard>
                <Storyboard>
                    <!-- Анимация появления черточек при загрузке -->
                    <DoubleAnimation Storyboard.TargetName=""Step1"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.5""/>
                    <DoubleAnimation Storyboard.TargetName=""Step2"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.55""/>
                    <DoubleAnimation Storyboard.TargetName=""Step3"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.6""/>
                    <DoubleAnimation Storyboard.TargetName=""Step4"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.65""/>
                    <DoubleAnimation Storyboard.TargetName=""Step5"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.7""/>
                    <DoubleAnimation Storyboard.TargetName=""Step6"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.75""/>
                    <DoubleAnimation Storyboard.TargetName=""Step7"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.8""/>
                    <DoubleAnimation Storyboard.TargetName=""Step8"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.85""/>
                    <DoubleAnimation Storyboard.TargetName=""Step9"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.9""/>
                    <DoubleAnimation Storyboard.TargetName=""Step10"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.95""/>
                    
                    <!-- Исчезновение черточек через 2.5 секунды -->
                    <DoubleAnimation Storyboard.TargetName=""StepPanel"" 
                                   Storyboard.TargetProperty=""Opacity"" 
                                   To=""0"" Duration=""0:0:0.5"" 
                                   BeginTime=""0:0:2.5""/>
                </Storyboard>
            </BeginStoryboard>
        </DataTrigger.EnterActions>
    </DataTrigger>

    <!-- Триггер на 10 сообщений (только для ранга 0) -->
    <DataTrigger Binding=""{Binding MessageCount}"" Value=""10"">
        <DataTrigger.EnterActions>
            <BeginStoryboard>
                <Storyboard>
<!-- Анимация появления черточек при загрузке -->
                    <DoubleAnimation Storyboard.TargetName=""Step1"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.5""/>
                    <DoubleAnimation Storyboard.TargetName=""Step2"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.55""/>
                    <DoubleAnimation Storyboard.TargetName=""Step3"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.6""/>
                    <DoubleAnimation Storyboard.TargetName=""Step4"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.65""/>
                    <DoubleAnimation Storyboard.TargetName=""Step5"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.7""/>
                    <DoubleAnimation Storyboard.TargetName=""Step6"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.75""/>
                    <DoubleAnimation Storyboard.TargetName=""Step7"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.8""/>
                    <DoubleAnimation Storyboard.TargetName=""Step8"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.85""/>
                    <DoubleAnimation Storyboard.TargetName=""Step9"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.9""/>
                    <DoubleAnimation Storyboard.TargetName=""Step10"" Storyboard.TargetProperty=""Opacity"" From=""0"" To=""1"" Duration=""0:0:0.3"" BeginTime=""0:0:0.95""/>
                    
                    <!-- Исчезновение черточек через 2.5 секунды -->
                    <DoubleAnimation Storyboard.TargetName=""StepPanel"" 
                                   Storyboard.TargetProperty=""Opacity"" 
                                   To=""0"" Duration=""0:0:0.5"" 
                                   BeginTime=""0:0:2.5""/>
                    <!-- Появление звезды -->
                    <DoubleAnimation Storyboard.TargetName=""RankStar"" 
                                   Storyboard.TargetProperty=""Opacity"" 
                                   From=""0"" To=""1"" Duration=""0:0:0.3"" 
                                   BeginTime=""0:0:2.5""/>
                    
                    <!-- Пульсация звезды -->
                    <DoubleAnimation Storyboard.TargetName=""StarScale"" 
                                   Storyboard.TargetProperty=""ScaleX"" 
                                   From=""1"" To=""1.2"" Duration=""0:0:1.5"" 
                                   AutoReverse=""True"" 
                                   RepeatBehavior=""Forever""
                                   BeginTime=""0:0:2.6""/>
                    <DoubleAnimation Storyboard.TargetName=""StarScale"" 
                                   Storyboard.TargetProperty=""ScaleY"" 
                                   From=""1"" To=""1.2"" Duration=""0:0:1.5"" 
                                   AutoReverse=""True"" 
                                   RepeatBehavior=""Forever""
                                   BeginTime=""0:0:2.6""/>
                    
                    <!-- Исчезновение звезды -->
                    <DoubleAnimation Storyboard.TargetName=""RankStar"" 
                                   Storyboard.TargetProperty=""Opacity"" 
                                   To=""0"" Duration=""0:0:0.3"" 
                                   BeginTime=""0:0:7.0""/>
                    
                    <!-- Переливание цвета -->
                    <ColorAnimation Storyboard.TargetName=""RankStar"" 
                                  Storyboard.TargetProperty=""(TextBlock.Foreground).(SolidColorBrush.Color)""
                                  From=""#FFD700"" To=""#FFE55C"" Duration=""0:0:0.3"" 
                                  AutoReverse=""True"" 
                                  RepeatBehavior=""Forever""
                                  BeginTime=""0:0:2.6""/>
                </Storyboard>
            </BeginStoryboard>
        </DataTrigger.EnterActions>
    </DataTrigger>
</DataTemplate.Triggers>
</DataTemplate>";

                    var template = (DataTemplate)XamlReader.Parse(xaml);
                    template.Seal();
                    _fallbackTemplate = template;
                    return template;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SkinLoader] Ошибка создания hardcoded шаблона: {ex.Message}");

                    // Абсолютный минимум
                    var simpleTemplate = new DataTemplate();
                    var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                    textFactory.SetValue(TextBlock.TextProperty, "Default Template");
                    textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
                    simpleTemplate.VisualTree = textFactory;
                    simpleTemplate.Seal();
                    return simpleTemplate;
                }
            }
        }
    }
}