using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;

namespace Moswar
{
    [XmlRoot]
    public class clsTaskManager
    {
        [XmlElement("TimeManager:")]
        public clsTManagerArt[] Arts;

        //Используется для создания и чтения структур из XML файлов.
        public static double Version = 0.0;
        public static XmlRootAttribute XmlRoot = new XmlRootAttribute("TimeEventPlaner:");
        public static XmlSerializer XmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(clsTaskManager), XmlRoot);

        public enum TaskEvent { NotToday, Time };

        [Serializable()]
        public struct stcTaskEx
        {
            public DayOfWeek DoW;
            public TimeSpan[] StartTS;
            public TimeSpan[] StopTS;
        }

        [Serializable()]
        public struct stcArtsEx
        {
            public stcTaskEx[] Tasks { get; set; }
        }

        [Serializable()]
        public struct stcTaskManagerEx
        {
            public stcArtsEx[] Arts;
            public int test;
        }

    }

    [Serializable()]
    public class clsTManagerArt
    {
        public clsMTask[] Tasks { get; set; }
    }
 

    [Serializable()]
    public class clsMTask
    {
        [Serializable()]
        public struct stcTask
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
            public string Text;
            public int Grp;
            public int Item;
        }

        public string Text
        {
            get { return CurrEvent.Text; }
            set { CurrEvent.Text = value; }
        }
        public int Grp
        {
            get { return CurrEvent.Grp; }
            set { CurrEvent.Grp = value; }
        }
        public int Item
        {
            get { return CurrEvent.Item; }
            set { CurrEvent.Item = value; }
        }

        private stcTask CurrEvent;

        public override string ToString()
        { return CurrEvent.Text; } //Для верного отображения в листбоксах

        #region Конструкторы
        public clsMTask(int Grp, int Item, string Text)
        {
            CurrEvent.Grp = Grp;
            CurrEvent.Item = Item;
            CurrEvent.Text = Text;
        }
        public clsMTask(stcTask T)
        {
            CurrEvent.Grp = T.Grp;
            CurrEvent.Item = T.Item;
            CurrEvent.Text = T.Text;
        }
        public clsMTask()
        { 
        }
        #endregion

        

        static private bool UpgradeTimePlanerVer(ComboBox CB_Event, ComboBox CB_Item, ComboBox CB_Art, string FPath, ref clsTaskManager Arr)
        {
            bool bRet = true;
            int EventOffset = 0;
            int TypeOffset = 0;

            #region Апгрэйд списка допингов
            Regex regex = new Regex("(?<=. )([^:])+(?(?=[:] [0-9]):|.+)");
            foreach (clsMTask TimeEvent in Arr.Arts[CB_Art.SelectedIndex].Tasks)
            {
                switch (TimeEvent.Grp)
                {
                    case 1: //Event
                        TimeEvent.Item = CB_Event.Items.IndexOf(regex.Match(TimeEvent.Text).Value);
                        if (TimeEvent.Item < 0) bRet = false; //Допинг не найден!
                        else TimeEvent.Item += EventOffset;
                        break;
                    case 2: //Item
                        TimeEvent.Item = CB_Item.Items.IndexOf(regex.Match(TimeEvent.Text).Value);
                        if (TimeEvent.Item < 0) bRet = false; //Допинг не найден!
                        else TimeEvent.Item += TypeOffset;
                        break;
                }
            }
            #endregion
            return bRet;
        }
        static private clsTaskManager TaskManagerExToTaskManager(clsTaskManager.stcTaskManagerEx TaskManagerEx)
        {
            if (TaskManagerEx.Arts == null) return new clsTaskManager { Arts = new clsTManagerArt[0] };
            clsMTask[] Task;
            clsTaskManager TaskManager = new clsTaskManager { Arts = new clsTManagerArt[TaskManagerEx.Arts.Count()] };
            for (int i = 0; i < TaskManagerEx.Arts.Count(); i++)
            {
                Task = new clsMTask[0]; 
                for (int k = 0; k < TaskManagerEx.Arts[i].Tasks.Count(); k++)
                {
                    Array.Resize(ref Task, Task.Count() + 1);
                    Task[Task.Count() - 1] = new clsMTask();
                    Task[Task.Count() - 1].Grp = 1;
                    Task[Task.Count() - 1].Item = (int)TaskManagerEx.Arts[i].Tasks[k].DoW;
                    Task[Task.Count() - 1].Text = "* " + Regex.Replace(CultureInfo.CreateSpecificCulture("ru-RU").DateTimeFormat.GetDayName(TaskManagerEx.Arts[i].Tasks[k].DoW), "^[\\w]", m => m.Value.ToUpper());
                                        
                    for (int x = 0; x < TaskManagerEx.Arts[i].Tasks[k].StartTS.Count(); x++)
                    {
                        Array.Resize(ref Task, Task.Count() + 1);
                        Task[Task.Count() - 1] = new clsMTask();
                        Task[Task.Count() - 1].Grp = 2;
                        Task[Task.Count() - 1].Item = (int)(TaskManagerEx.Arts[i].Tasks[k].StartTS[x] == new TimeSpan() && TaskManagerEx.Arts[i].Tasks[k].StopTS[x] == new TimeSpan() ? clsTaskManager.TaskEvent.NotToday : clsTaskManager.TaskEvent.Time);
                        Task[Task.Count() - 1].Text = "- " + (Task[Task.Count() - 1].Item == 0 ? "Не ходить" : "Участвовать с " + TaskManagerEx.Arts[i].Tasks[k].StartTS[x].ToString("%h\\:%m") + " до " + TaskManagerEx.Arts[i].Tasks[k].StopTS[x].ToString("%h\\:%m"));
                    }
                }
                TaskManager.Arts[i] = new clsTManagerArt { Tasks = Task };
            }
            return TaskManager;
        }
        static private clsTaskManager.stcTaskManagerEx TaskManagerToTaskmanagerEx(clsTaskManager TaskManager)
        {
            clsTaskManager.stcTaskEx[] TaskEx;
            clsTaskManager.stcTaskManagerEx TaskManagerEx = new clsTaskManager.stcTaskManagerEx { Arts = new clsTaskManager.stcArtsEx[TaskManager.Arts.Count()] };

            for (int i = 0; i < TaskManager.Arts.Count(); i++)
            {
                TaskEx = new clsTaskManager.stcTaskEx[0];
                for (int k = 0; k < TaskManager.Arts[i].Tasks.Count(); k++)
                {
                    switch (TaskManager.Arts[i].Tasks[k].Grp)
                    {
                        case 1: //Event
                            {
                                Array.Resize(ref TaskEx, TaskEx.Count() + 1);
                                TaskEx[TaskEx.Count() - 1].DoW = (DayOfWeek)TaskManager.Arts[i].Tasks[k].Item;
                                TaskEx[TaskEx.Count() - 1].StartTS = new TimeSpan[0];
                                TaskEx[TaskEx.Count() - 1].StopTS = new TimeSpan[0];
                            }
                            break;
                        case 2: //Item
                            {
                                Array.Resize<TimeSpan>(ref TaskEx[TaskEx.Count() - 1].StartTS, TaskEx[TaskEx.Count() - 1].StartTS.Count() + 1);
                                Array.Resize<TimeSpan>(ref TaskEx[TaskEx.Count() - 1].StopTS, TaskEx[TaskEx.Count() - 1].StopTS.Count() + 1);
                                Match match = Regex.Match(TaskManager.Arts[i].Tasks[k].Text, "Участвовать с (?<From>([0-9:])+) до (?<Till>([0-9:])+)");
                                TaskEx[TaskEx.Count() - 1].StartTS[TaskEx[TaskEx.Count() - 1].StartTS.Count() - 1] = (clsTaskManager.TaskEvent)TaskManager.Arts[i].Tasks[k].Item == clsTaskManager.TaskEvent.NotToday ? new TimeSpan() : TimeSpan.Parse(match.Groups["From"].Value);
                                TaskEx[TaskEx.Count() - 1].StopTS[TaskEx[TaskEx.Count() - 1].StopTS.Count() - 1] = (clsTaskManager.TaskEvent)TaskManager.Arts[i].Tasks[k].Item == clsTaskManager.TaskEvent.NotToday ? new TimeSpan() : TimeSpan.Parse(match.Groups["Till"].Value);
                                break;
                            }
                    }
                }
                TaskManagerEx.Arts[i].Tasks = TaskEx;
            }
            return TaskManagerEx;
        }
        static public void Save(ListBox LB, ComboBox CB_Art, ref clsTaskManager.stcTaskManagerEx TaskManagerEx, string FPath)
        {
            while (LB.Items.Count != 0 && ((clsMTask)(LB.Items[0])).Grp == 2) LB.Items.RemoveAt(0); //Удаляем пустые ивенты без типа
            
            FileStream FS = new FileStream(FPath, FileMode.Create);
            clsTaskManager.XmlSerializer.Serialize(FS, UpdateInfo(LB, CB_Art, ref TaskManagerEx));
            FS.Close();
        }
        static public bool Load(ListBox LB, ComboBox CB_Event, ComboBox CB_Item, ComboBox CB_Art, ref clsTaskManager.stcTaskManagerEx TaskManagerEx, string FPath)
        {
            bool bRet = true;

            if (File.Exists(FPath))
            {
                FileStream FS = new FileStream(FPath, FileMode.Open);
                clsTaskManager TaskManager = (clsTaskManager)clsTaskManager.XmlSerializer.Deserialize(FS);                
                FS.Close();
 
                #region Стираю прошлые допинги перед обновлением списка (обязательно после выбора типа допингов)
                LB.Items.Clear();
                #endregion
                if (TaskManager.Arts.Count() >= CB_Art.SelectedIndex && TaskManager.Arts[CB_Art.SelectedIndex].Tasks != null) //Есть допинг?
                {      
                    LB.Items.AddRange(TaskManager.Arts[CB_Art.SelectedIndex].Tasks);                    
                }
                TaskManagerEx = TaskManagerToTaskmanagerEx(TaskManager);
            }
            return bRet;              
        }
        static public void Remove(ListBox LB, ComboBox CB_Art, ref clsTaskManager.stcTaskManagerEx TaskManagerEx)
        {
            clsTaskManager TaskManager = new clsTaskManager();
            int i = LB.SelectedIndex;
            if (i == -1) return;
            if (((clsMTask)(LB.Items[i])).Grp == 1) //Удаляем целый ивент
            {
                do
                {
                    LB.Items.RemoveAt(i);
                    if (i >= LB.Items.Count) break;
                } while (((clsMTask)(LB.Items[i])).Grp == 2);
            }
            else LB.Items.RemoveAt(i); //Удаляем всего один допинг            

            UpdateInfo(LB, CB_Art, ref TaskManagerEx);
        }
        static private clsTaskManager UpdateInfo(ListBox LB, ComboBox CB_Art, ref clsTaskManager.stcTaskManagerEx TaskManagerEx)
        {
            clsTaskManager TaskManager = TaskManagerExToTaskManager(TaskManagerEx); //Создаём исходное положение

            for (int i = TaskManager.Arts.Count() - 1; i < CB_Art.SelectedIndex; i++)
            {
                Array.Resize(ref TaskManager.Arts, TaskManager.Arts.Count() + 1);
                TaskManager.Arts[TaskManager.Arts.Count() - 1] = new clsTManagerArt { Tasks = new clsMTask[0] };
            }
            clsMTask[] Tasks = new clsMTask[LB.Items.Count]; //Временный массив для хранения данных
            LB.Items.CopyTo(Tasks, 0); //Переносим данные во временный массив
            TaskManager.Arts[CB_Art.SelectedIndex] = new clsTManagerArt { Tasks = Tasks }; //Перезаписываю установленные таски
                   
            TaskManagerEx = TaskManagerToTaskmanagerEx(TaskManager); //Обновляю список
            return TaskManager;
        }        
    }
}
