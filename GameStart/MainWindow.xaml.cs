using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GameStart
{
    internal enum Slot
    {
        Top = 0,
        Right = 90,
        Bottom = 180,
        Left = 270
    };

    internal enum Styles
    {
        None = -1,
        White,
        Blue,
        Red,
        Green
    };
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private LinearGradientBrush bgGradient = new LinearGradientBrush();
        private Joypad controller = null;
        private Task controllerTask;
        private volatile bool isExitAfterRun = false;
        private volatile bool isHelpVisible = true;

        private volatile bool isSettingsOpen = false;
        private volatile int settingsPosition = 1;

        private Styles currentStyle = Styles.None;
        private SolidColorBrush colour;
        private List<string> exeLocation = new List<string>(4);
        private double rotateAngle;
        private string saveDir;
        private int slot;
        private List<LinearGradientBrush> slotGradients = new List<LinearGradientBrush>();
        private volatile bool isFileDialogOpen = false;

        public MainWindow()
        {
            InitializeComponent();

            Init();

            DataContext = this;

            LoadConfig();

            controllerTask = Task.Run(() =>
            {
                PollForDevices();
            });

            this.Activate();
            this.Focus();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public double RotateAngle
        {
            get
            {
                return rotateAngle;
            }

            private set
            {
                if (value != rotateAngle)
                {
                    rotateAngle = value;
                    Shrink();

                    var slotNum = (int)((rotateAngle % 360) / 90);
                    if (slotNum >= 0 && slotNum < 4)
                    {
                        slot = slotNum;

                        Zoom();
                    }

                    OnPropertyChanged("RotateAngle");
                }
            }
        }

        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            RestoreOpacity((Image)sender);
        }

        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            var img = (Image)sender;
            img.Opacity = 0.5;
        }

        private void CloseButton_ClickStart(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void CloseButton_ClickDone(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            CloseThreadSafe();
            RestoreOpacity((Image)sender);
        }

        private void CloseThreadSafe()
        {
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                SaveConfig();
                Close();
            });
        }

        private void ColourImages(Styles style)
        {
            string colourString;
            Uri closeImgPath;
            Uri minImgPath;
            Uri glowImgPath;
            Uri pointerImgPath;

            switch (style)
            {
                case Styles.White:
                    colourString = "white";
                    break;

                case Styles.Blue:
                    colourString = "blue";
                    break;

                case Styles.Red:
                    colourString = "red";
                    break;

                case Styles.Green:
                    colourString = "green";
                    break;

                default:
                    colourString = "white";
                    break;
            }

            closeImgPath = new Uri($"Images/close_{colourString}.png", UriKind.Relative);
            minImgPath = new Uri($"Images/min_{colourString}.png", UriKind.Relative);
            glowImgPath = new Uri($"Images/glow_{colourString}.png", UriKind.Relative);
            pointerImgPath = new Uri($"Images/pointer_{colourString}.png", UriKind.Relative);

            CloseButton.Source = new BitmapImage(closeImgPath);
            MinimizeButton.Source = new BitmapImage(minImgPath);
            Glow.Source = new BitmapImage(glowImgPath);
            Pointer.Source = new BitmapImage(pointerImgPath);
        }

        private void ColourLabels(Color color)
        {
            var labels = new List<Label>();

            labels.Add(run);
            labels.Add(exit);
            labels.Add(runExit);
            labels.Add(set);

            foreach (var label in labels)
            {
                label.Foreground = new SolidColorBrush(color);
            }
        }

        private void CreateImage(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return;
            }

            var icon = System.Drawing.Icon.ExtractAssociatedIcon(filename);
            var img = Imaging.CreateBitmapSourceFromHIcon(icon.Handle,
                                                          new Int32Rect(0, 0, icon.Width, icon.Height),
                                                          BitmapSizeOptions.FromEmptyOptions());
            var image = new Image();
            image.Source = img;
            image.Margin = new Thickness(10);
            Grid grid = GetCurrentGrid();

            if (grid.Children.Count > 0)
            {
                grid.Children.RemoveAt(0);
            }
            grid.Children.Add(image);
        }

        private void CreateSaveFile()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Legacy support

            if(Directory.Exists($"{dir}\\GameStart"))
            {
                if(File.Exists($"{dir}\\GameStart\\gsconfig.inf"))
                {
                    File.Delete($"{dir}\\GameStart\\gsconfig.inf");
                }

                Directory.Delete($"{dir}\\GameStart");
            }

            // End Legacy support

            dir = $"{dir}\\Embark";

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            saveDir = $"{dir}\\embark_config.ini";
        }

        private Grid GetCurrentGrid()
        {
            switch (slot)
            {
                case 0:
                    return top;

                case 1:
                    return right;

                case 2:
                    return bottom;

                case 3:
                    return left;

                default:
                    return null;
            }
        }

        private void HandleButtons(ButtonInfo buttons)
        {
            if (buttons == null || !IsKeyboardFocusWithin)
            {
                return;
            }

            if (isSettingsOpen)
            {
                HandleButtonsSettings(buttons);
                return;
            }
            var x = (double)(buttons.XAxis / 1000);
            var y = (double)(buttons.YAxis / 1000);

            // Experimental
            //if (Math.Abs(x) > 15 || Math.Abs(y) > 15)
            //{
            //    var rotation = Math.Atan2(x, y);
            //    rotation = rotation * (180.0 / Math.PI);

            //    if (rotation < 0)
            //    {
            //        rotation += 360;
            //    }

            //    MoveTo((int)rotation);
            //}

            if (buttons.DPad.Right ||
                (buttons.XAxis > 15000 && Math.Abs(buttons.XAxis) > Math.Abs(buttons.YAxis)))
            {
                RotateAngle = (double)Slot.Right;
            }
            else if (buttons.DPad.Left ||
                (buttons.XAxis < -15000 && Math.Abs(buttons.XAxis) > Math.Abs(buttons.YAxis)))
            {
                RotateAngle = (double)Slot.Left;
            }
            else if (buttons.DPad.Up
                || (buttons.YAxis > 15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                RotateAngle = (double)Slot.Top;
                System.Windows.Forms.SendKeys.SendWait("{UP}");
            }
            else if (buttons.DPad.Down ||
                (buttons.YAxis < -15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                RotateAngle = (double)Slot.Bottom;
                System.Windows.Forms.SendKeys.SendWait("{DOWN}");
            }

            if (controller.IsChanged)
            {
                if (buttons.A)
                {
                    if (Run() == true)
                    {
                        if (isExitAfterRun)
                        { 
                            Thread.Sleep(1000); // Delay before exit
                            CloseThreadSafe();
                        }
                    }
                }
                else if (buttons.B)
                {
                    CloseThreadSafe();
                }
                else if (buttons.X)
                {
                    if (Run() == true)
                    {
                        Thread.Sleep(1000); // Delay before exit
                        CloseThreadSafe();
                    }
                }
                else if (buttons.Y)
                {
                    Application.Current?.Dispatcher.Invoke((Action)delegate
                    {
                        SetExeSlot(slot);
                    });
                }
                else if (buttons.Start)
                {
                    ToggleSettingsMenu();

                    UpdateSettings(updatePosition:true);
                }
            }
        }

        // Experimental
        //private void MoveTo(int angle)
        //{
        //    var angleHigh = angle + 20;
        //    var angleLow = angle - 20;

        //    if (angleHigh > 360)
        //    {
        //        if (angleLow < 360)
        //            angle = 0;
        //    }
        //    else if (angleHigh > 270)
        //    {
        //        if (angleLow < 270)
        //            angle = 270;
        //    }
        //    else if (angleHigh > 180)
        //    {
        //        if (angleLow < 180)
        //            angle = 180;
        //    }
        //    else if (angleHigh > 90)
        //    {
        //        if (angleLow < 90)
        //            angle = 90;
        //    }
        //    else if (angleHigh > 0)
        //    {
        //        if (angleLow < 0)
        //            angle = 0;
        //    }

        //    RotateAngle = angle;
        //}

        private void HandleButtonsSettings(ButtonInfo buttons)
        {
            const int numSettings = 3;

            if (buttons.DPad.Up ||
               (buttons.YAxis > 15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                settingsPosition = (settingsPosition == 1) ? 1 : (settingsPosition - 1);

                UpdateSettings(updatePosition: true);

                Thread.Sleep(110);
            }
            else if (buttons.DPad.Down ||
                    (buttons.YAxis < -15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                settingsPosition = (settingsPosition == numSettings) ? numSettings : (settingsPosition + 1);

                UpdateSettings(updatePosition: true);
                Thread.Sleep(110);
            }

            if (controller.IsChanged)
            {
                if (buttons.A)
                {
                    if (settingsPosition == 1)
                    {
                        isExitAfterRun = !isExitAfterRun;
                        UpdateSettings();
                    }
                    else if(settingsPosition == 2)
                    {
                        isHelpVisible = !isHelpVisible;
                        UpdateSettings();
                    }
                    else
                    {
                        NextColour();
                    }
                }
                else if (buttons.B ||
                         buttons.Start)
                {
                    ToggleSettingsMenu();

                    Application.Current?.Dispatcher.Invoke((Action)delegate
                    {
                        exitAfterRunButton.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                    });
                }
            }

            return;
        }

        private async Task HandleButtonsFileDialog(ButtonInfo buttons)
        {
            if (buttons.DPad.Up
                || (buttons.YAxis > 15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                System.Windows.Forms.SendKeys.SendWait("{UP}");

                await Task.Delay(110);
            }
            else if (buttons.DPad.Down ||
                (buttons.YAxis < -15000 && Math.Abs(buttons.XAxis) < Math.Abs(buttons.YAxis)))
            {
                System.Windows.Forms.SendKeys.SendWait("{DOWN}");

                await Task.Delay(110);
            }
            else if (buttons.DPad.Right ||
                (buttons.XAxis > 15000 && Math.Abs(buttons.XAxis) > Math.Abs(buttons.YAxis)))
            {
                System.Windows.Forms.SendKeys.SendWait("{RIGHT}");

                await Task.Delay(110);
            }
            else if (buttons.DPad.Left ||
                (buttons.XAxis < -15000 && Math.Abs(buttons.XAxis) > Math.Abs(buttons.YAxis)))
            {
                System.Windows.Forms.SendKeys.SendWait("{LEFT}");

                await Task.Delay(110);
            }

            if (controller.IsChanged)
            {
                if (buttons.A)
                {
                    System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                }
                else if (buttons.B)
                {
                    System.Windows.Forms.SendKeys.SendWait("%{F4}");
                }
            }

            return;
        }

        private void Init()
        {
            slotGradients.Add(new LinearGradientBrush());
            slotGradients.Add(new LinearGradientBrush());
            slotGradients.Add(new LinearGradientBrush());
            slotGradients.Add(new LinearGradientBrush());

            //top
            slotGradients[(int)Slot.Top / 90].StartPoint = new Point(0.5, 0);
            slotGradients[(int)Slot.Top / 90].EndPoint = new Point(0.5, 1);
            //right
            slotGradients[(int)Slot.Right / 90].StartPoint = new Point(1, 0.5);
            slotGradients[(int)Slot.Right / 90].EndPoint = new Point(0, 0.5);
            //bottom
            slotGradients[(int)Slot.Bottom / 90].StartPoint = new Point(0.5, 1);
            slotGradients[(int)Slot.Bottom / 90].EndPoint = new Point(0.5, 0);
            //left
            slotGradients[(int)Slot.Left / 90].StartPoint = new Point(0, 0.5);
            slotGradients[(int)Slot.Left / 90].EndPoint = new Point(1, 0.5);

            bgGradient.StartPoint = new Point(0.5, 0);
            bgGradient.EndPoint = new Point(0.5, 1);

            CreateSaveFile();
        }

        private void LoadConfig()
        {
            if (!File.Exists(saveDir))
            {
                File.WriteAllText(saveDir, "");
            }

            exeLocation.Add("");
            exeLocation.Add("");
            exeLocation.Add("");
            exeLocation.Add("");

            var lines = File.ReadAllLines(saveDir);

            foreach (var line in lines)
            {
                if (line.StartsWith("slot"))
                {
                    var index = int.Parse(line[4].ToString());
                    var path = line.Substring(6);
                    exeLocation[index] = path;

                    slot = index;
                    CreateImage(path);
                }

                if (line.StartsWith("style="))
                {
                    var style = line.Substring(6);

                    if (style == "white")
                    {
                        SetStyle(Styles.White);
                    }
                    else if (style == "blue")
                    {
                        SetStyle(Styles.Blue);
                    }
                    else if (style == "red")
                    {
                        SetStyle(Styles.Red);
                    }
                    else if (style == "green")
                    {
                        SetStyle(Styles.Green);
                    }
                }

                if (line.StartsWith("exitafterrun="))
                {
                    var exitafterrun = line.Substring(13);

                    if (exitafterrun == "True")
                    {
                        isExitAfterRun = true;
                    }
                    else
                    {
                        isExitAfterRun = false;
                    }
                }

                if (line.StartsWith("help="))
                {
                    var help = line.Substring(5);

                    if (help == "True")
                    {
                        isHelpVisible = true;
                    }
                    else
                    {
                        isHelpVisible = false;
                    }
                }
            }

            if (currentStyle == Styles.None)
            {
                SetStyle(Styles.White);
            }

            slot = 0;

            UpdateSettings();
        }

        private void MinimizeButton_ClickStart(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void MinimizeButton_ClickDone(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            WindowState = WindowState.Minimized;
            RestoreOpacity((Image)sender);
        }

        private void OnPropertyChanged(string info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
        private void PollForDevices()
        {
            while (controller == null ||
                   !controller.IsConnected())
            {
                controller = Joypad.TryConnect();

                if (controller == null)
                {
                    Thread.Sleep(500);
                }
            }

            PollForInput();
        }

        private void PollForInput()
        {
            var buttons = new ButtonInfo();

            while (controller != null &&
                   controller.IsConnected())
            {
                buttons = controller.GetInput();
                HandleButtons(buttons);
                Thread.Sleep(20);
            }

            PollForDevices();
        }

        private void RestoreOpacity(Image img)
        {
            img.Opacity = 1;
        }

        private bool Run()
        {
            RunAnimation();

            if (string.IsNullOrEmpty(exeLocation[slot]))
            {
                return false;
            }

            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.WorkingDirectory = "c:\\";
            process.StartInfo.Arguments = $"/c start \"\" /b \"{exeLocation[slot]}\"";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();

            return true;
        }

        private void RunAnimation()
        {
            Shrink();
            Zoom();
        }

        private void SaveConfig()
        {
            var iniList = exeLocation;
            string[] styles = { "white", "blue", "red", "green" };

            iniList.Add($"style={styles[(int)currentStyle]}");
            iniList.Add($"exitafterrun={isExitAfterRun.ToString()}");
            iniList.Add($"help={isHelpVisible.ToString()}");

            for (int i = 0; i < 4; i++)
            {
                iniList[i] = $"slot{i}=" + iniList[i];
            }

            var infArr = iniList.ToArray();

            File.WriteAllLines(saveDir, infArr);
        }

        private void SetExeSlot(int slotNum)
        {
            if (slotNum < 0 || slotNum > 3)
            {
                throw new Exception("Index out of range");
            }

            var window = new System.Windows.Forms.OpenFileDialog();
            window.Filter = "App files|*.exe;*.lnk;*url";
            window.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            window.DereferenceLinks = false;
            window.CheckFileExists = true;
            window.CheckPathExists = true;
            isFileDialogOpen = true;

            Task.Run(() =>
            {
                SetUpDialog();
                PollForInput_FileDialog();
            });

            if (window.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                isFileDialogOpen = false;

                if (string.IsNullOrEmpty(window.FileName))
                {
                    return;
                }
            }
            else
            {
                
                isFileDialogOpen = false;
                return;
            }

            exeLocation[slotNum] = window.FileName;
            

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                CreateImage(window.FileName);
            });
        }

        private async void PollForInput_FileDialog()
        {
            var buttons = new ButtonInfo();
            var prevbuttons = new ButtonInfo();

            while (controller != null &&
                   controller.IsConnected() &&
                   isFileDialogOpen)
            {
                buttons = controller.GetInput();
                await HandleButtonsFileDialog(buttons);
                Thread.Sleep(20);
            }
        }

        private async void SetUpDialog()
        {
            await Task.Delay(300);
            System.Windows.Forms.SendKeys.SendWait("+{TAB}");
            System.Windows.Forms.SendKeys.SendWait("+{TAB}");
        }

        private void SetStyle(Styles newColour)
        {
            Color slotColor, bgColor;
            byte alpha = 200;

            switch (newColour)
            {
                case Styles.Blue:
                    slotColor = Colors.DodgerBlue;
                    bgColor = Colors.Blue;
                    break;

                case Styles.Red:
                    slotColor = Colors.Firebrick;
                    bgColor = Colors.Red;
                    break;

                case Styles.Green:
                    slotColor = Colors.ForestGreen;
                    bgColor = Colors.Green;
                    break;

                default:
                    slotColor = Colors.LightGray;
                    bgColor = Colors.White;
                    break;
            }

            colour = new SolidColorBrush(slotColor);

            ColourLabels(slotColor);
            ColourImages(newColour);

            bgColor.A = alpha;

            bgGradient.GradientStops = new GradientStopCollection();
            bgGradient.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 0, 0, 0), 0));
            bgGradient.GradientStops.Add(new GradientStop(bgColor, 1.75));

            foreach (var gradient in slotGradients)
            {
                gradient.GradientStops = new GradientStopCollection();
                slotColor.A = 255;
                gradient.GradientStops.Add(new GradientStop(slotColor, 0));
                slotColor.A = 25;
                gradient.GradientStops.Add(new GradientStop(slotColor, 1.5));
            }

            main.Background = bgGradient;

            top.Background = slotGradients[(int)Slot.Top / 90];
            bottom.Background = slotGradients[(int)Slot.Bottom / 90];
            left.Background = slotGradients[(int)Slot.Left / 90];
            right.Background = slotGradients[(int)Slot.Right / 90];

            currentStyle = newColour;
        }

        private void Settings_ClickStart(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Settings_ClickDone(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            ToggleSettingsMenu();
        }

        private void ToggleSettingsMenu()
        {
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                if (settingsMenu.Visibility == Visibility.Hidden)
                {
                    settingsMenu.Visibility = Visibility.Visible;
                    settingsBG.Background = new SolidColorBrush(Color.FromArgb(127, colour.Color.R, colour.Color.G, colour.Color.B));
                    isSettingsOpen = true;
                }
                else
                {
                    settingsMenu.Visibility = Visibility.Hidden;
                    settingsBG.Background = new SolidColorBrush(Colors.Transparent);
                    isSettingsOpen = false;
                }
                
            });
        }

        private void Shrink()
        {
            Shrink(GetCurrentGrid());
        }

        private void Shrink(Grid target)
        {
            // must run in application context
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                ScaleTransform trans = new ScaleTransform();
                trans.CenterX = target.ActualWidth / 2;
                trans.CenterY = target.ActualHeight / 2;
                target.RenderTransform = trans;
                DoubleAnimation anim = new DoubleAnimation(1.2, 1, TimeSpan.FromMilliseconds(250));
                trans.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                trans.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            });
        }

        private void slot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Zoom();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settingsMenu.Visibility == Visibility.Visible)
            {
                ToggleSettingsMenu();
            }
            DragMove();
        }

        private void Zoom()
        {
            Zoom(GetCurrentGrid());
        }

        private void Zoom(Grid target)
        {
            // must run in application context
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                ScaleTransform trans = new ScaleTransform();
                trans.CenterX = target.ActualWidth / 2;
                trans.CenterY = target.ActualHeight / 2;
                target.RenderTransform = trans;
                DoubleAnimation anim = new DoubleAnimation(1, 1.2, TimeSpan.FromMilliseconds(250));
                trans.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                trans.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            });
        }

        private void Slot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (!Run())
            {
                MessageBox.Show("No application chosen", "Failed to Run", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (isExitAfterRun)
            {
                CloseThreadSafe();
            }
        }

        private void SlotMouseEnter(object sender, MouseEventArgs e)
        {
            var slot = (Grid)sender;

            switch(slot.Name)
            {
                case "top":
                    RotateAngle = (double)Slot.Top;
                    break;
                case "right":
                    RotateAngle = (double)Slot.Right;
                    break;
                case "bottom":
                    RotateAngle = (double)Slot.Bottom;
                    break;
                case "left":
                    RotateAngle = (double)Slot.Left;
                    break;
            }
        }

        private void MenuDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void MenuRun(object sender, MouseButtonEventArgs e)
        {
            var label = (Label)sender;
            var grid = GetCurrentGrid();

            label.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            if (!Run())
            {
                MessageBox.Show("No application chosen", "Failed to Run", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            grid.ContextMenu.IsOpen = false;
        }

        private void MenuRunExit(object sender, MouseButtonEventArgs e)
        {
            var label = (Label)sender;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            if (!Run())
            {
                MessageBox.Show("No application chosen", "Failed to Run", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Thread.Sleep(1000);
            CloseThreadSafe();
        }

        private void MenuSet(object sender, MouseButtonEventArgs e)
        {
            var label = (Label)sender;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            SetExeSlot(slot);
        }

        private void MenuEnter(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            var label = (Label)sender;
            label.Foreground = colour;
        }

        private void MenuLeave(object sender, MouseEventArgs e)
        {
            e.Handled = true;

            var label = (Label)sender;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
        }

        private void SettingsExitAfterRun(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            isExitAfterRun = !isExitAfterRun;

            UpdateSettings();
        }

        private void SettingsColour(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            NextColour();
        }

        private void NextColour()
        {
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                switch (currentStyle)
                {
                    case Styles.White:
                        SetStyle(Styles.Blue);
                        break;

                    case Styles.Blue:
                        SetStyle(Styles.Red);
                        break;

                    case Styles.Red:
                        SetStyle(Styles.Green);
                        break;

                    case Styles.Green:
                        SetStyle(Styles.White);
                        break;
                }

                colourButton.Foreground = colour;
                settingsBG.Background = new SolidColorBrush(Color.FromArgb(127, colour.Color.R, colour.Color.G, colour.Color.B));
            });
        }

        private void SettingsHelp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            isHelpVisible = !isHelpVisible;

            UpdateSettings();
        }

        private void UpdateSettings(bool updatePosition)
        {
            Application.Current?.Dispatcher.Invoke((Action)delegate
            {
                if (isHelpVisible)
                {
                    helpButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                    helpPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    helpButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                    helpPanel.Visibility = Visibility.Hidden;
                }

                if (isExitAfterRun)
                {
                    exitAfterRunButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                }
                else
                {
                    exitAfterRunButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
                }

                if (updatePosition)
                {
                    exitAfterRunButton.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                    helpButton.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                    colourButton.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

                    switch (settingsPosition)
                    {
                        case 1:
                            exitAfterRunButton.Foreground = colour;
                            break;
                        case 2:
                            helpButton.Foreground = colour;
                            break;
                        case 3:
                            colourButton.Foreground = colour;
                            break;
                        default:
                            break;
                    }
                }
            });
        }

        private void UpdateSettings()
        {
            UpdateSettings(false);
        }
    }
}