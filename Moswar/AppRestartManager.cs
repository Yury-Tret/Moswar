using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace Moswar
{
    public class clsAppRestartManager
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern uint RegisterApplicationRestart(string pwzCommandLine, RestartFlags dwFlags);

        [DllImport("kernel32.dll")]
        static extern uint RegisterApplicationRecoveryCallback(RecoveryCallbackDelegate pRecoveryCallback, string pvParameter, UInt32 dwPingInterval, UInt32 dwFlags);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryInProgress(out bool pbCancelled);

        [DllImport("kernel32.dll")]
        static extern uint ApplicationRecoveryFinished(bool bSuccess);

        [Flags]
        enum RestartFlags
        {
            NONE = 0,
            RESTART_NO_CRASH = 1,
            RESTART_NO_HANG = 2,
            RESTART_NO_PATCH = 4,
            RESTART_NO_REBOOT = 8
        }

        private delegate int RecoveryCallbackDelegate(string pvParameter);

        static private void KeepAlive(object source, ElapsedEventArgs e) //Функция сообшает ApplicationRestartManager, что программа продолжает сохранение данных
        {
            bool isCanceled;
            ApplicationRecoveryInProgress(out isCanceled);
            if (isCanceled) Environment.Exit(2);
        }
        static private int DoRecovery(string Info) //Функция сохранения данных вызываемая коллбэком
        {
            Timer NotifyTimer = new Timer(1000); //1s
            NotifyTimer.Elapsed += new ElapsedEventHandler(KeepAlive); //Запускаем в таймере функцию мешаюшую Windows отключить меня до полного сохранения данных           
            NotifyTimer.Enabled = true;

            //###############################
            SaveRecovery(ref frmMain.Bot.Me); //Сохраняем данные            
            //###############################

            ApplicationRecoveryFinished(true); //Сохранение данных успешно законченно.

            return 0; //Возвращаем, значение что всё прошло ОК
        }

        static public void SaveRecovery<T>(ref T Info) //Сохранение данных
        {
            XmlSerializer XmlRecovery = new XmlSerializer(typeof(T));
            Stream FS = new FileStream("Recovery.xml", FileMode.Create);
            XmlRecovery.Serialize(FS, Info);
            FS.Close();
        }
        static public void LoadRecovery<T>(ref T Info) //Загрузка данных
        {
            XmlSerializer XmlRecovery = new XmlSerializer(typeof(T));
            Stream FS = new FileStream("Recovery.xml", FileMode.Open);
            Info = (T)XmlRecovery.Deserialize(FS);
            FS.Close();
        }

        static public bool RegisterAppRestart(string StartParam) //Автоматический рестарт программы
        {
            return RegisterApplicationRestart(StartParam, RestartFlags.RESTART_NO_REBOOT | RestartFlags.RESTART_NO_PATCH) == 0;
        }
        static public bool RegisterAppRecovery() //Сохранение данных при падении 
        {
            //Создаем делегат коллбэка для сохранения данных при падении
            RecoveryCallbackDelegate recoveryCallback = new RecoveryCallbackDelegate(DoRecovery);
            return RegisterApplicationRecoveryCallback(recoveryCallback, "Register", 5000, 0) == 0;
        }
    }
}
