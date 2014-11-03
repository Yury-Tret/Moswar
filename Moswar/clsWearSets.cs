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
    [Serializable()]
    public class clsWearSets
    {
        public struct stcSetItem
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 40)]
            public string Btn;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
            public string ID;
        }

        [Serializable()]
        public struct stcSet
        {
            public int IndexName;
            public stcSetItem[] Item;
        }

        //Используется для создания и чтения структур из XML файлов.
        private static XmlRootAttribute XmlRoot = new XmlRootAttribute("Set:");
        private static XmlSerializer XmlSerializer = new XmlSerializer(typeof(stcSet[]), XmlRoot);

        public clsWearSets()
        {
        }

        static public void Save(WebBrowser WB, ref stcSet[] ArrSet, int IndexName, string FPath) 
        {
            int i;
            stcSet Set2Save = new stcSet();
            Set2Save.IndexName = IndexName;
            Set2Save.Item = new stcSetItem[14];
            #region Сбор информации о напялянных на мне сейчас вещей!
            for (i = 0; i < 14; i++)
            {
                string ItemHtml = (string)frmMain.GetJavaVar(WB, "$(\"#main .slots .slot" + (i + 1) + "\").html()");
                Set2Save.Item[i].ID = ItemHtml == null ? "" : Regex.Match(ItemHtml, "(?<=data-id=\")([0-9])+(?=\")").Value;
                Set2Save.Item[i].Btn = ItemHtml == null ? "" : (string)frmMain.GetJavaVar(WB, "m.items['" + Set2Save.Item[i].ID + "'].btn['0'].id");
            }
            #endregion 
            #region Сохранение сэта
            if (ArrSet == null) ArrSet = new stcSet[1] { Set2Save }; //Это первый сэт?
            else 
            {
                for (i = 0; i < ArrSet.Count<stcSet>(); i++) //Поиск и перезапись сэта по его имени.
                {
                    if (ArrSet[i].IndexName == IndexName)
                    {
                        ArrSet[i] = Set2Save;
                        break;
                    }
                }
                if (i == ArrSet.Count<stcSet>()) //Сэт небыл найден? - Добавляем!
                {
                    Array.Resize<stcSet>(ref ArrSet, i + 1);
                    ArrSet[i] = Set2Save;
                }
            }
            #endregion                      
            Stream FS = new FileStream(FPath, FileMode.Create);
            XmlSerializer.Serialize(FS, ArrSet);
            FS.Close();
        }
        static public bool Load(ref stcSet[] ArrSet, string FPath)
        {
            if (File.Exists(FPath))
            {
                FileStream FS = new FileStream(FPath, FileMode.Open);
                ArrSet = (stcSet[])XmlSerializer.Deserialize(FS);
                FS.Close();
                return true;
            }
            return false;
        }        
    }
}

