using System;
using System.Threading;
using System.Windows;

namespace GameStart
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static Mutex mutex = new Mutex(true, "{8F6F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");
        [STAThread()]
        static void Main()
        {
            App app = new App();
            app.InitializeComponent();

            try
            {
                if (mutex.WaitOne(TimeSpan.Zero, true))
                {
                    app.Run();
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "App Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}