using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace Moswar
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            #region Проверка кол-ва запущенных инстанций из одной и тойже директории
            int InstanceCount = 0;
            foreach (Process P in Process.GetProcessesByName(Application.ProductName))
            {
                if (P.MainModule.FileName == Application.ExecutablePath) InstanceCount++;
                if (InstanceCount > 2) Process.GetCurrentProcess().Kill();
            }
            #endregion
            #region AppRestart windws XP or CrashReport + Restart
            if (true) //Работаем под XP? (Автоматический перезапуск с сохранением настроек) Environment.OSVersion.Version.Major < 6
            {
                Application.ThreadException += new ThreadExceptionEventHandler(OnThreadException);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.Automatic);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);
            }
            #endregion            
            #region AppRestart windows Vista+
            if (Environment.OSVersion.Version.Major >= 6) //Работаем под Виста или выше? (Автоматический перезапуск с сохранением настроек)
            {
                clsAppRestartManager.RegisterAppRestart("-recovery"); //Регистрирую рестарт
                clsAppRestartManager.RegisterAppRecovery(); //Регистрирую сохранение данных перед крэшем
            }
            #endregion

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
        #region Функции коллбэка от CrashReport
        static void OnThreadException(object sender, ThreadExceptionEventArgs e) 
        {
            Crash(e.Exception);
        }
        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Crash((Exception)e.ExceptionObject);
        }
        static void Crash(Exception e)
        {
            //MessageBox.Show(e.Message + "\r\n" + e.StackTrace + "\r\n Обязательно сделай скриншот этой гадости!");
            #region Запись Trace
            StreamWriter SW = new StreamWriter("BuG-Report\\Crash " + DateTime.Now.ToString("dd-MM-yyyy HH-mm-ss") + ".txt");
            SW.WriteLine(e);
            SW.Close();
            #endregion
            if (frmMain.BotThread.ThreadState != System.Threading.ThreadState.Unstarted) frmMain.Bot.RestartBotInstance(true);
        }
        #endregion
    }
}
