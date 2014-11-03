using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;


namespace Moswar
{
    [XmlRoot]
    public class clsDopingArray
    {
        [XmlAttribute("Version")]
        public double Ver { get; set; }
        [XmlElement("Dopings:")]
        public clsDoping[] ArrDoping { get; set; }
    }

    [Serializable()]
    public class clsDoping
    {
        [Serializable()]
        public struct stcDoping
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
            public string Text;
            public int Grp;
            public int Item;
        }

        public string Text
        {
            get { return  Doping.Text; }
            set { Doping.Text = value; }
        }
        public int Grp
        {
            get { return Doping.Grp; }
            set { Doping.Grp = value; }
        }
        public int Item
        {
            get { return Doping.Item; }
            set { Doping.Item = value; }
        }

        private stcDoping Doping;

        public override string ToString()
        { return Doping.Text; } //Для верного отображения в листбоксах
        #region Конструкторы
        public clsDoping(int Grp, int Item, string Text)
        {
            Doping.Grp = Grp;
            Doping.Item = Item;
            Doping.Text = Text;
        }
        public clsDoping(stcDoping DI)
        {
            Doping.Grp = DI.Grp;
            Doping.Item = DI.Item;
            Doping.Text = DI.Text;
        }
        public clsDoping()
        { 
        }
        #endregion

        //Используется для создания и чтения структур из XML файлов.
        public static double Version = 0.6;
        private static XmlRootAttribute XmlRoot = new XmlRootAttribute("Dopings:");
        private static XmlSerializer XmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(clsDopingArray), XmlRoot); 

        public enum DopingEvent { EnemyLvl, Timer, PVP, Rat, Neft, Agent, HC, Allways };
        public enum DopingType { RatSyncLvl, LeninTicket, Gum1, Gum2, Gum3, Gum4, Gum5, Gum6, Gum1Adv, Gum2Adv, Gum3Adv, Gum4Adv, Gum5Adv, Gum6Adv, Gum1Ex, Gum2Ex, Gum3Ex, Gum4Ex, Gum5Ex, Gum6Ex, Pyani, Tvorog, Coctail1, Coctail2, Coctail3, Coctail4, Coctail5, Coctail6, Vitamin, NovajaZhizn, Barjomi, AquaDeminerale, WeakNPC1, WeakNPC2, WeakNPC3, Valujki, ValujkiAdv, GasMask, Respirator, Tea1, Shoko1, Tea4, Shoko4, Tea7, Shoko7, Tea10, Shoko10, Tea15, Shoko15, CandyExp, CandyAntiExp, CarAll, Car4, Car6, Car5, Car2, Car3, Car1, Car10, Car12, Car11, Car8, Car9, Car7, Car16, Car18, Car17, Car14, Car15, Car13, Car19, Car20, Car21, Car22, Car23, Car24, Car25, Car26, Car27, Car28, Car29, Car30, Car31, Car34, Car35, Car36, Car37, Car38, Car39, Car40 }; //Car34 - Эвакуатор, 32-33 понятия не имею!

        public struct stcDopingEx
        {
            public int StartLvl;
            public int SyncLvl;
            public int Defeats;
            public DateTime StartDT;
            public DateTime StopDT;
            public DopingType[] Items;
            public DopingEvent Event;
            public bool Done;
        }

        public static bool[] NeedToBuy = new bool[Enum.GetValues(typeof(DopingType)).Length];
        public static bool[] AlreadyEated = new bool[Enum.GetValues(typeof(DopingType)).Length + 1]; //Последний элемент показывает все ли допинги из списка были сьедены.

        static private bool UpgradeDopingVer(ComboBox CB_Event, ComboBox CB_Item, ComboBox CB_Art, string FPath, ref clsDopingArray Arr)
        {
            bool bRet = true;
            int EventOffset = 0;
            int TypeOffset = 0;

            #region Определения типа обрабатываемого допинга и оффсэты
            switch (FPath)
            {
                case "OilLenin.doping":
                    break;
                case "Rat.doping":
                    TypeOffset = 2; //Оффсэт на синхронизазию крыс и партбилеты
                    break;
                case "Usual.doping":
                    EventOffset = 1; //Оффсэт на нападать при уровне.
                    TypeOffset = 2; //Оффсэт на синхронизазию крыс и партбилеты
                    break;
            }
            #endregion
            #region Апгрэйд списка допингов
            Regex regex = new Regex("(?<=. )([^:])+(?(?=[:] [0-9]):|.+)");
            foreach (clsDoping Doping in Arr.ArrDoping)
            {
                switch (Doping.Grp)
                {
                    case 1: //Event
                       // Match match = Regex.Match(Doping.Text, "(?<=. )([^:])+(?(?=[:] [0-9]):|.+)");
                        Doping.Item = CB_Event.Items.IndexOf(regex.Match(Doping.Text).Value);
                        if (Doping.Item < 0) bRet = false; //Допинг не найден!
                        else Doping.Item += EventOffset;
                        break;
                    case 2: //Item
                        Doping.Item = CB_Item.Items.IndexOf(regex.Match(Doping.Text).Value);
                        if (Doping.Item < 0) bRet = false; //Допинг не найден!
                        else Doping.Item += TypeOffset;
                        break;
                }
            }
            #endregion
            return bRet;
        }
        static public void Save(ListBox LB, ref stcDopingEx[] ArrDoping, string FPath)
        {
            while (LB.Items.Count != 0 && ((clsDoping)(LB.Items[0])).Grp == 2) LB.Items.RemoveAt(0); //Удаляем пустые ивенты без типа
            ArrDoping = null; //Стираю прошлые допинги перед обновлением списка.
            Stream FS = new FileStream(FPath, FileMode.Create);            
            clsDoping[] ArrTmpDoping = new clsDoping[LB.Items.Count]; //Временный массив для хранения данных
            LB.Items.CopyTo(ArrTmpDoping, 0); //Переносим данные во временный массив
            XmlSerializer.Serialize(FS, new clsDopingArray { ArrDoping = ArrTmpDoping, Ver = Version }); //         
            FS.Close();
            UpdateDopingArr(LB, ref ArrDoping);
        }
        static public bool Load(ListBox LB, ComboBox CB_Event, ComboBox CB_Item, ComboBox CB_Art, ref stcDopingEx[] ArrDoping, string FPath)
        {
            bool bRet = true;

            if (File.Exists(FPath))
            {
                FileStream FS = new FileStream(FPath, FileMode.Open);
                clsDopingArray ArrTmpDoping = (clsDopingArray)XmlSerializer.Deserialize(FS);                
                #region Совсем старая версия без версионизации? (Пересерелиазиция)
                if (ArrTmpDoping.Ver == 0) //Совсем старая версия без версионизации!
                {
                    FS.Position = 0; //Прыгаем в начало стрима
                    ArrTmpDoping.ArrDoping = (clsDoping[])(new System.Xml.Serialization.XmlSerializer(typeof(clsDoping[]), XmlRoot)).Deserialize(FS);
                }
                #endregion
                FS.Close();               
                #region Определение обрабатываемого к апгрэйду типа (тут, чтоб даже если апгрэйда не будет всё было правильно отображено)
                switch (FPath)
                {
                    case "OilLenin.doping":
                        CB_Art.SelectedIndex = 2;
                        break;
                    case "Rat.doping":
                        CB_Art.SelectedIndex = 1;
                        break;
                    case "Usual.doping":
                        CB_Art.SelectedIndex = 0;
                        break;
                }
                #endregion         
                #region Стираю прошлые допинги перед обновлением списка (обязательно после выбора типа допингов)
                LB.Items.Clear();
                ArrDoping = null;
                #endregion
                if (ArrTmpDoping.ArrDoping != null) //Есть допинг?
                {
                    #region Обновление версии допингов?
                    if (ArrTmpDoping.Ver < Version) bRet &= UpgradeDopingVer(CB_Event, CB_Item, CB_Art, FPath, ref ArrTmpDoping);
                    #endregion        
                    LB.Items.AddRange(ArrTmpDoping.ArrDoping);
                    UpdateDopingArr(LB, ref ArrDoping);
                }
            }
            return bRet;              
        }                
        static public void Remove(ListBox LB, ref stcDopingEx[] ArrDoping)
        {            
            int i = LB.SelectedIndex;
            if (i == -1) return;            
            if (((clsDoping)(LB.Items[i])).Grp == 1) //Удаляем целый ивент
            {
                do
                {
                    LB.Items.RemoveAt(i);
                    if (i >= LB.Items.Count) break;
                } while (((clsDoping)(LB.Items[i])).Grp == 2);
            }
            else LB.Items.RemoveAt(i); //Удаляем всего один допинг
            ArrDoping = null; //Стираю прошлые допинги перед обновлением списка.
            UpdateDopingArr(LB, ref ArrDoping);
        }
        static private void UpdateDopingArr(ListBox LB, ref stcDopingEx[] ArrDoping)
        {
            int i = 0, k = 0;

            foreach (clsDoping Doping in LB.Items)
            {
                switch (Doping.Grp)
                {
                    case 1: //Event
                        {
                            i = ArrDoping == null ? 1 : ArrDoping.Count<stcDopingEx>() + 1;
                            Array.Resize<stcDopingEx>(ref ArrDoping, i);
                            ArrDoping[i - 1].Event = (DopingEvent)Doping.Item;
                            switch (Doping.Item)
                            {                                
                                case 0: //Считывание уровня
                                    ArrDoping[i - 1].StartLvl = Convert.ToInt32(Regex.Match(Doping.Text, " ([0-9])+").Value);
                                    break;
                                case 1: //Сьесть в:
                                    ArrDoping[i - 1].StartDT = Convert.ToDateTime(Regex.Match(Doping.Text, " ([0-9:])+").Value);
                                    break;
                            }
                            break;
                        }
                    case 2: //Item
                        {
                            //if (i == 0) { LB.Items.Clear(); ArrDoping = null; return; } //Стараемся удалить доппинги без предшествующего ивента!
                            k = ArrDoping[i - 1].Items == null ? 1 : ArrDoping[i - 1].Items.Count<DopingType>() + 1;
                            Array.Resize<DopingType>(ref ArrDoping[i - 1].Items, k);
                            ArrDoping[i - 1].Items[k - 1] = (DopingType)Doping.Item;
                            switch (Doping.Item)
                            {
                                case 0: //Считывание уровня синхронизации
                                    ArrDoping[i - 1].SyncLvl = Convert.ToInt32(Regex.Match(Doping.Text, " ([0-9])+").Value);
                                    break;
                                default :
                                    ArrDoping[i - 1].SyncLvl = ArrDoping[i - 1].SyncLvl == 0 ? -1 : ArrDoping[i - 1].SyncLvl; //для беспрепятственного прохождения синхронизации
                                    break;
                            }
                            break;
                        }
                }
            }            
        }        
    }
}
