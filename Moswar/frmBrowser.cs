using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Timers;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Windows.Input;
using System.Web;
using System.Net;
using System.Runtime.InteropServices;



namespace Moswar
{
    public partial class frmMain : Form, clsWBEx.IOleClientSite, clsWBEx.IDocHostUIHandler, clsWBEx.IDocHostShowUI, clsWBEx.IOleCommandTarget, clsWBEx.IServiceProvider, clsWBEx.IAuthenticate, clsWBEx.IProtectFocus
    {
        #region Browser Extensions + Authentication
        #region IOleClientSite Members
        public int SaveObject() 
        {
            return clsWBEx.S_FALSE;
        }
        public int GetMoniker(int dwAssign, int dwWhichMoniker, out object moniker) 
        {
            moniker = this; return clsWBEx.S_OK;
        }
        public int GetContainer(out object container) 
        {
            container = this; return clsWBEx.S_OK;
        }
        public int ShowObject() 
        {
            return clsWBEx.S_FALSE;
        }
        public int OnShowWindow(int fShow) 
        {
            return clsWBEx.S_FALSE;
        }
        public int RequestNewObjectLayout() 
        {
            return clsWBEx.S_FALSE;
        }
        #endregion
        #region IDocHostUIHandler
        public int ShowContextMenu(int dwID, ref clsWBEx.POINT pt, IntPtr pcmdtReserved, IntPtr pdispReserved)
        {
            return clsWBEx.S_FALSE;
        }
        public int GetHostInfo(ref clsWBEx.DOCHOSTUIINFO info)
        {
            return clsWBEx.S_FALSE;
        }
        public int ShowUI(int dwID, IntPtr activeObject, IntPtr commandTarget, IntPtr frame, IntPtr doc)
        {
            return clsWBEx.S_FALSE;
        }
        public int HideUI()
        {
            return clsWBEx.S_FALSE;
        }
        public int UpdateUI()
        {
            return clsWBEx.S_FALSE;
        }
        public int EnableModeless(bool fEnable)
        {
            return clsWBEx.S_FALSE;
        }
        public int OnDocWindowActivate(bool fActivate)
        {
            return clsWBEx.S_FALSE;
        }
        public int OnFrameWindowActivate(bool fActivate)
        {
            return clsWBEx.S_FALSE;
        }
        public int ResizeBorder(ref clsWBEx.COMRECT rect, IntPtr doc, bool fFrameWindow)
        {
            return clsWBEx.S_FALSE;
        }
        public int TranslateAccelerator(ref clsWBEx.MSG msg, ref Guid group, int nCmdID)
        {
            return clsWBEx.S_FALSE;
        }
        public int GetOptionKeyPath(string[] pbstrKey, int dw)
        {
            return clsWBEx.S_FALSE;
        }
        public int GetDropTarget(IntPtr pDropTarget, out IntPtr ppDropTarget)
        {
            ppDropTarget = IntPtr.Zero;
            return clsWBEx.S_FALSE;
        }
        public int GetExternal(out object ppDispatch)
        {
            ppDispatch = Bot;
            return clsWBEx.S_OK;
        }
        public int TranslateUrl(int dwTranslate, string strURLIn, out string pstrURLOut)
        {
            pstrURLOut = null;
            return clsWBEx.S_FALSE;
        }
        public int FilterDataObject(IntPtr pDO, out IntPtr ppDORet)
        {
            ppDORet = IntPtr.Zero;
            return clsWBEx.S_FALSE;
        }
        #endregion
        #region IOleCommandTarget
        public int QueryStatus(IntPtr pguidCmdGroup, uint cCmds, ref clsWBEx.OLECMD prgCmds, IntPtr pCmdText)
        {
            // Переедаем команда не распознана, команда будет выполнена иным IOleCommandTarget.
            return clsWBEx.OLECMDERR_E_UNKNOWNGROUP;
        }
        public int Exec(IntPtr pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if ((int)clsWBEx.OLECMDID.OLECMDID_SHOWSCRIPTERROR == nCmdID)
            { 
                if (pvaOut != IntPtr.Zero) Marshal.GetNativeVariantForObject(true, pvaOut); //true -> не останавливать выполнение скриптов, false -> остановить выполнение скриптов
                return clsWBEx.S_OK;
            }

            // Переедаем команда не распознана, команда будет выполнена иным IOleCommandTarget.
            return clsWBEx.OLECMDERR_E_UNKNOWNGROUP;
        }
        #endregion
        #region IDocHostShowUI
        public int ShowMessage(IntPtr hwnd, string lpstrText, string lpstrCaption, uint dwType, string lpstrHelpFile, uint dwHelpContext, ref int lpResult)
        {
            if (dwType == clsWBEx.MB_ICONWARNING || dwType == clsWBEx.MB_ICONERROR) //Блокировка по типу иконки в вылетающем MessageBox
            {
                return clsWBEx.S_OK; //Блокировка сообщений в стиле ActiveX был заблокирован
            }
            else return clsWBEx.S_FALSE;
        }
        public int ShowHelp(IntPtr hwnd, string pszHelpFile, uint uCommand, uint dwData, clsWBEx.POINT ptMouse, object pDispatchObjectHit)
        {
            return clsWBEx.E_NOTIMPL;
        }
        #endregion       
        #region IServiceProvider Members
        public int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject)
        {
            Guid IID_IAuthenticate = new Guid("79eac9d0-baf9-11ce-8c82-00aa004ba90b");
            Guid IID_ProtectFocus = new Guid("D81F90A3-8156-44F7-AD28-5ABB87003274");
           
            if (guidService == IID_IAuthenticate && riid == IID_IAuthenticate)
            {
                ppvObject = Marshal.GetComInterfaceForObject(this, typeof(clsWBEx.IAuthenticate));
                return clsWBEx.S_OK;
            }
            if (guidService == IID_ProtectFocus && riid == IID_ProtectFocus)
            {
                ppvObject = Marshal.GetComInterfaceForObject(this, typeof(clsWBEx.IProtectFocus));
                return clsWBEx.S_OK;                
            }

            ppvObject = new IntPtr();
            return clsWBEx.E_NOINTERFACE;
        }
        #endregion
        #region IAuthenticate Members
        public int Authenticate(ref IntPtr phwnd, ref IntPtr pszUsername, ref IntPtr pszPassword)
        {
            IntPtr sUser = Marshal.StringToCoTaskMemAuto(Bot.PrivSettings.ProxyUserName);
            IntPtr sPassword = Marshal.StringToCoTaskMemAuto(Bot.PrivSettings.ProxyPassword);

            pszUsername = sUser;
            pszPassword = sPassword;
            return clsWBEx.S_OK;
        }
        #endregion
        #region IProtectFocus Members
        public int AllowFocusChange(ref bool pfAllow)
        {
            pfAllow = false;
            return clsWBEx.S_OK;
        }
        #endregion
        #region IOleControl
        private int WBDownloadProperty;
        void SetWBDownloadManager(WebBrowser WB, bool LoadImages, bool LoadActiveX)
        {
            WBDownloadProperty = (int)
                (
                 (LoadActiveX ? 0 : clsWBEx.AmbientProperty.DLCTL_NO_DLACTIVEXCTLS | clsWBEx.AmbientProperty.DLCTL_NO_RUNACTIVEXCTLS) |
                 (LoadImages ? clsWBEx.AmbientProperty.DLCTL_DLIMAGES | clsWBEx.AmbientProperty.DLCTL_VIDEOS : clsWBEx.AmbientProperty.DLCTL_DOWNLOADONLY)
                //| clsWBEx.AmbientProperty.DLCTL_SILENT //использовать нельзя! Иначе после пары ошибок в скриптах блокирует их выполнение, решение через IOleCommandTarget
                );
            clsWBEx.IOleControl Control = (clsWBEx.IOleControl)WB.ActiveXInstance;
            Control.OnAmbientPropertyChange(clsWBEx.DISPID_AMBIENT_DLCONTROL);
        }

        [DispId(clsWBEx.DISPID_AMBIENT_DLCONTROL)] //CallBack
        [PreserveSig]
        public int AmbientDlControl()
        {            
            return WBDownloadProperty;
        }
        #endregion     
        #endregion

        static public clsBot Bot = new clsBot();        
        public static Thread BotThread;
        private Thread[] VictimThread = new Thread[2];       
        private System.Timers.Timer STimer = new System.Timers.Timer();
        private ToolTip Tip = new ToolTip();
        private DateTime LastCheckDT = DateTime.Now;
        private DateTime LastUpdateCheckDT = DateTime.Now;
        private DateTime LastMessageUpdateDT = DateTime.Now;
        
        private bool VictimHunting;
        private bool NeedHeal;

        private struct stcDopingStatus
        {
            public clsDoping[] NormalDoping; //Временное хранилище для допингов
            public clsDoping[] RatDoping;    //Временное хранилище для допингов
            public clsDoping[] OilLeninDoping;  //Временное хранилище для допингов
            public int LastDopingArt;
        }

        private stcDopingStatus DopingStatus;

        private struct stcTaskManagerStatus 
        {
            public clsTaskManager TaskManager;
            public int LastTaskManagerArt;
        }

        private stcTaskManagerStatus TaskManagerStatus;

        #region Настройки эксперт
        private void UpdateExpertSettings()
        {
            Bot.Expert.ProxyMin = numExpProxyMin.Value;
            Bot.Expert.ProxyMax = numExpProxyMax.Value;
            Bot.Expert.GoToMin = numExpGoToMin.Value;
            Bot.Expert.GoToMax = numExpGoToMax.Value;
            Bot.Expert.UseTimeout = numExpUseTimeout.Value;
            Bot.Expert.AnalyseFightMin = numExpAnalyseFightMin.Value;
            Bot.Expert.AnalyseFightMax = numExpAnalyseFightMax.Value;
            if (Bot.Expert.QuestFruitNr != numExpQuestFruit.Value || Bot.Expert.QuestMoneyNr != numExpQuestMoney.Value) Bot.Me.Events.StopQuest = false;
            Bot.Expert.QuestFruitNr = numExpQuestFruit.Value;
            Bot.Expert.QuestMoneyNr = numExpQuestMoney.Value;
            Bot.Expert.QuestNotAll = chkExpQuestNotAll.Checked;
            Bot.Expert.QuestIgnoreBonus = chkExpQuestIgnoreBonus.Checked;
            Bot.Expert.QuestUseAllTonusBottle = chkExpUseAllTonusBottle.Checked;
            Bot.Expert.RevengerPrc = numExpRevengerPrc.Value;
            Bot.Expert.DoNotProofTorture = chkExpDoNotProofTorture.Checked;
            Bot.Expert.DoNotUseIron = chkExpDoNotUseIron.Checked;
            Bot.Expert.DoNotUseAntiSafe = chkExpDoNotUseAntiSafe.Checked;
            Bot.Expert.DoNotUseAngleGrinder = chkExpDoNotUseAngleGrinder.Checked;
            Bot.Expert.DoNotEatPelmeni = chkExpDoNotEatPelmeni.Checked;
            if (chkExpDoNotLoadImage.Checked != Bot.Expert.DoNotLoadImage | chkExpDoNotLoadActiveX.Checked != Bot.Expert.DoNotLoadActiveX)
            {
                SetWBDownloadManager(ctrMainBrowser, !chkExpDoNotLoadImage.Checked, !chkExpDoNotLoadActiveX.Checked);
            }
            Bot.Expert.DoNotLoadImage = chkExpDoNotLoadImage.Checked;
            Bot.Expert.DoNotLoadActiveX = chkExpDoNotLoadActiveX.Checked;
            if (numExpMaxWebSockets.Value != Bot.Expert.MaxWebSockets) clsWBEx.SetMaxIEConnections(ctrMainBrowser, (int)numExpMaxWebSockets.Value);         
            Bot.Expert.MaxWebSockets = numExpMaxWebSockets.Value;
            #region Матрица закупок боевого инвентаря
            if (Bot.Expert.BuyFightItemType == null) Bot.Expert.BuyFightItemType = new bool[8]; //0 -> резерв, 1-> лечение, 2-> сыр (покачто купить невозможно), 3-> гранаты+, 4-> франаты%, 5-> пружина, 6-> каска, 7-> щит
            Bot.Expert.BuyFightItemType[1] = chkExpBuyHeal.Checked;
            Bot.Expert.BuyFightItemType[2] = chkExpBuyHeal.Checked;
            Bot.Expert.BuyFightItemType[3] = chkExpBuyGranades.Checked;
            Bot.Expert.BuyFightItemType[4] = chkExpBuyGranades.Checked;
            Bot.Expert.BuyFightItemType[5] = chkExpBuySprings.Checked;
            Bot.Expert.BuyFightItemType[6] = chkExpBuyHelms.Checked;
            Bot.Expert.BuyFightItemType[7] = chkExpBuyShilds.Checked;
            #endregion
            Bot.Expert.BuyMoreThenOneGranade = chkExpBuyMoreThenOneGranade.Checked;
            Bot.Expert.MaxBuyFightItemAmount = numExpMaxBuyFightItemAmount.Value;
            #region Матрица заполнения боевого инвентаря
            if (Bot.Expert.FightSlotItemTypes == null) Bot.Expert.FightSlotItemTypes = new int[70]; //10 типов боев по 6 слотов +1 питомец
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 0] = cboxExpGrpFightSlot1.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 1] = cboxExpGrpFightSlot2.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 2] = cboxExpGrpFightSlot3.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 3] = cboxExpGrpFightSlot4.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 4] = cboxExpGrpFightSlot5.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 5] = cboxExpGrpFightSlot6.SelectedIndex;
            Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 6] = cboxExpGrpFightPet.SelectedIndex; //Питомец
            #endregion

            Bot.SaveExpertSettings();
        }
        private void LoadExpertSettings()
        {
            if (File.Exists("Expert.xml"))
            {
                Bot.LoadExpertSettings();

                numExpProxyMin.Value = Bot.Expert.ProxyMin;
                numExpProxyMax.Value = Bot.Expert.ProxyMax;
                numExpGoToMin.Value = Bot.Expert.GoToMin;
                numExpGoToMax.Value = Bot.Expert.GoToMax;
                numExpUseTimeout.Value = Bot.Expert.UseTimeout;
                numExpAnalyseFightMin.Value = Bot.Expert.AnalyseFightMin;
                numExpAnalyseFightMax.Value = Bot.Expert.AnalyseFightMax;
                numExpQuestFruit.Value = Bot.Expert.QuestFruitNr;
                numExpQuestMoney.Value = Bot.Expert.QuestMoneyNr;
                chkExpQuestNotAll.Checked = Bot.Expert.QuestNotAll;
                chkExpQuestIgnoreBonus.Checked = Bot.Expert.QuestIgnoreBonus;
                chkExpUseAllTonusBottle.Checked = Bot.Expert.QuestUseAllTonusBottle;
                numExpRevengerPrc.Value = Bot.Expert.RevengerPrc;
                chkExpDoNotProofTorture.Checked = Bot.Expert.DoNotProofTorture;
                chkExpDoNotUseIron.Checked = Bot.Expert.DoNotUseIron;
                chkExpDoNotUseAntiSafe.Checked = Bot.Expert.DoNotUseAntiSafe;
                chkExpDoNotUseAngleGrinder.Checked = Bot.Expert.DoNotUseAngleGrinder;
                chkExpDoNotEatPelmeni.Checked = Bot.Expert.DoNotEatPelmeni;
                chkExpDoNotLoadImage.Checked = Bot.Expert.DoNotLoadImage;
                chkExpDoNotLoadActiveX.Checked = Bot.Expert.DoNotLoadActiveX;                
                SetWBDownloadManager(ctrMainBrowser, !Bot.Expert.DoNotLoadImage, !Bot.Expert.DoNotLoadActiveX);
                numExpMaxWebSockets.Value = Bot.Expert.MaxWebSockets;
                #region Матрица закупок боевого инвентаря
                if (Bot.Expert.BuyFightItemType != null)
                {
                    if (Bot.Expert.BuyFightItemType.Count<bool>() < 8) Array.Resize<bool>(ref Bot.Expert.BuyFightItemType, 8); //0 -> резерв, 1-> лечение, 2-> сыр (покачто купить невозможно), 3-> гранаты+, 4-> франаты%, 5-> пружина, 6-> каска, 7-> щит

                    chkExpBuyHeal.Checked = Bot.Expert.BuyFightItemType[1];
                    chkExpBuyGranades.Checked = Bot.Expert.BuyFightItemType[3];
                    chkExpBuySprings.Checked = Bot.Expert.BuyFightItemType[5];
                    chkExpBuyHelms.Checked = Bot.Expert.BuyFightItemType[6];
                    chkExpBuyShilds.Checked = Bot.Expert.BuyFightItemType[7];
                }
                else Bot.Expert.BuyFightItemType = new bool[8];
                #endregion
                numExpMaxBuyFightItemAmount.Value = Bot.Expert.MaxBuyFightItemAmount;
                #region Матрица заполнения боевого инвентаря
                if (Bot.Expert.FightSlotItemTypes != null)
                {
                    if (Bot.Expert.FightSlotItemTypes.Count<int>() < 70) Array.Resize<int>(ref Bot.Expert.FightSlotItemTypes, 70); //10 типов боев по 6 слотов +1 питомец
                    cboxExpGrpFightSlot1.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 0];
                    cboxExpGrpFightSlot2.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 1];
                    cboxExpGrpFightSlot3.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 2];
                    cboxExpGrpFightSlot4.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 3];
                    cboxExpGrpFightSlot5.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 4];
                    cboxExpGrpFightSlot6.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 5];
                    cboxExpGrpFightPet.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 6]; //Питомец
                }
                else Bot.Expert.FightSlotItemTypes = new int[70];
                #endregion
                chkExpBuyMoreThenOneGranade.Checked = Bot.Expert.BuyMoreThenOneGranade;
            }

            UpdateExpertSettings();
        }
        #endregion
        private void UpdateSettings()
        {
            Bot.PrivSettings.BotName = txtBotName.Text;
            Bot.PrivSettings.Email = txtEmail.Text;
            Bot.PrivSettings.Password = txtPassword.Text;

            Bot.Settings.HealMe50 = numHealMe50.Value;
            Bot.Settings.HealMe100 = numHealMe100.Value;
            Bot.Settings.AmountHealMe = numAmountHealMe.Value;
            Bot.Settings.BuyHealMe = chkBuyHealMe.Checked;
            Bot.Settings.HealPet50 = numHealPet50.Value;
            Bot.Settings.HealPet100 = numHealPet100.Value;
            Bot.Settings.BuyHealPet = chkBuyHealPet.Checked;
            Bot.Settings.SetWarPetType = cboxSetWarPetType.SelectedIndex;
            Bot.Settings.HealTrauma = chkHealTrauma.Checked;
            Bot.Settings.HealInjuredSlot = chkHealInjuredSlot.Checked;

            Bot.Settings.GoHC = chkHC.Checked;
            Bot.Settings.minHCLvl = numMinHCLvl.Value;
            Bot.Settings.maxHCLvl = numMaxHCLvl.Value;
            Bot.Settings.minHCStatDiff = numStatDiff.Value;
            Bot.Settings.StartHC = dtStartHC.Value;
            Bot.Settings.StopHC = dtStopHC.Value;
            Bot.Settings.HCMember = chkHCMember.Checked;
            Bot.Settings.HCUseTorture = chkHCUseTorture.Checked;
            Bot.Settings.HCRevenge = chkHCRevenge.Checked;
            Bot.Settings.HCRevengeMaxMoney = numHCRevengeMaxMoney.Value;

            if (Bot.Settings.maxRatLvl < numMaxRatLvl.Value) { Bot.Me.Rat.Val = 0; Bot.Me.Rat.Stop = false; } //При смене уровня крысомах, скидываем стоппер.
            if (Bot.Settings.maxSearchRatLvl < numMaxSearchRatLvl.Value || chkSearchRatUseOhara.Checked) { Bot.Me.RatHunting.Defeats = 0; Bot.Me.RatHunting.Stop = false; } //При смене уровня крысомах, скидываем стоппер.
            Bot.Settings.GoMetro = chkMetro.Checked;
            Bot.Settings.SearchRat = chkSearchRat.Checked;
            Bot.Settings.BuyMpick = rbBuyMPick.Checked;
            Bot.Settings.BuyRpick = rbBuyRPick.Checked;
            Bot.Settings.BuyHelmet = chkBuyHelmet.Checked;
            Bot.Settings.BuyCounter = chkBuyCounter.Checked;
            Bot.Settings.AttackRat = chkAttackRat.Checked;
            Bot.Settings.maxRatLvl = numMaxRatLvl.Value;
            Bot.Settings.maxRatDefeats = numMaxRatDefeats.Value;
            Bot.Settings.maxSearchRatLvl = numMaxSearchRatLvl.Value;            
            Bot.Settings.SearchRatLeaveNoKey = chkSearchRatLeaveNoKey.Checked;
            Bot.Settings.SearchRatLeaveNoElement = chkSearchRatLeaveNoElement.Checked;
            Bot.Settings.SearchRatLeaveNoBox = chkSearchRatLeaveNoBox.Checked;
            Bot.Settings.SearchRatIgnoreAll = chkSearchRatIgnoreAll.Checked;
            Bot.Settings.SearchRatRobinHood = chkSearchRatRobinHood.Checked;
            Bot.Settings.SearchRatBambula = chkSearchRatBambula.Checked;
            Bot.Settings.maxSearchRatDefeats = numMaxSearchRatDefeats.Value;
            Bot.Settings.SearchRatUseOhara = chkSearchRatUseOhara.Checked;
            Bot.Settings.UseRatFastSearch = chkUseRatFastSearch.Checked;
            Bot.Settings.RatFastSearch = numRatFastSearch.Value;
            Bot.Settings.RatFastSearchHoney = rbRatFastSearchHoney.Checked;

            #region Матрица использования предметов в крысиных стенках (Мултидименсиональные эррэи не сереализируются=()
            if (Bot.Settings.UseRatItems == null) Bot.Settings.UseRatItems = new bool[35]; //5 видов * 7 стенок
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 0] = chkUseItemRatFightLvl5.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 1] = chkUseItemRatFightLvl10.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 2] = chkUseItemRatFightLvl15.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 3] = chkUseItemRatFightLvl20.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 4] = chkUseItemRatFightLvl25.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 5] = chkUseItemRatFightLvl30.Checked;
            Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 6] = chkUseItemRatFightLvl35.Checked;            
            #endregion

            if (Bot.Settings.UseThimblesTicket != chkThimblesTicket.Checked) Bot.Me.Thimbles.Stop = false;
            Bot.Settings.PlayThimbles = numPlayThimbles.Value * 1000;
            Bot.Settings.ExchangeBankMoney = numExchangeBankMoney.Value * 1000;
            Bot.Settings.minThimblesMoney = numMinThimbles.Value;
            Bot.Settings.ThimblesImmunity = chkThimblesImmunity.Checked;
            Bot.Settings.UseThimblesTicket = chkThimblesTicket.Checked;
            Bot.Settings.UseBank = chkUseBank.Checked;

            Bot.Settings.UseVictims = chkUseVictims.Checked;
            Bot.Settings.AddVictim = numAddVictim.Value;
            Bot.Settings.DeleteVictim = numDeleteVictim.Value;
            Bot.Settings.UseOnlyHomelessVictims = chkUseOnlyHomelessVictims.Checked;

//            if (Bot.Settings.AttackNPC & !Bot.Settings.AttackNPC) Bot.Me.NPCHunting.LastDT = new DateTime();
//            Bot.Settings.AttackNPC = chkAttackNPC.Checked;
            Bot.Settings.UseAgent = chkUseAgent.Checked;
            Bot.Settings.MrPlushkin = chkMrPlushkin.Checked;
            Bot.Settings.UseHomeless = chkUseHomeless.Checked;

//            if (!Bot.Settings.GoPVPFight & chkGoPVPFight.Checked || !Bot.Settings.GoNPCFight & chkGoNPCFight.Checked) Bot.GrpFight.GroupStartDT = new DateTime();
//            Bot.Settings.GoNPCFight = chkGoNPCFight.Checked;
            Bot.Settings.GoPVPFight = chkGoPVPFight.Checked;
//            Bot.GrpFight.Val = (chkGoPVPFight.Checked ? 1 : 0) + (chkGoNPCFight.Checked ? 2 : 0); // 0-> Выкл., 1-> PVP, 2-> NPC, 3-> Все.
            Bot.Settings.GoPVPInstantly = chkGoPVPInstantly.Checked;
            Bot.Settings.GoPVPInstantlyOffset = numGoPVPInstantlyOffset.Value;
            Bot.Settings.SovetBuyAgitator = chkSovetBuyAgitator.Checked;

            Bot.Settings.GoPatrol = chkPatrol.Checked;
            Bot.Settings.PatrolType = cboxPatrol.SelectedIndex;
            Bot.Settings.PatrolTime = numPatrolTime.Value;

            Bot.Settings.PayPoliceAt = numPayPolice.Value;
            Bot.Settings.PayPolice = rbPolicePay.Checked;
            Bot.Settings.WaitPolice = rbPoliceWait.Checked;

            if (Bot.Settings.FactoryRang != cboxFactoryRang.SelectedIndex) Bot.ChainUpgrade.Stop = false;
            Bot.Settings.MakePetriki = chkPetriki.Checked;
            Bot.Settings.PetrikiBonus = cboxPetrikiBonus.SelectedIndex;
            Bot.Settings.minPetrikiMoney = numMinPetrikiMoney.Value;
            Bot.Settings.minPetrikiOre = numMinPetrikiOre.Value;
            Bot.Settings.GoFactory = chkFactory.Checked;
            Bot.Settings.FactoryChainCount = numFactoryChainCount.Value;
            Bot.Settings.FactoryRang = cboxFactoryRang.SelectedIndex;
            Bot.Settings.minFactoryMoney = numMinFactoryMoney.Value;
            Bot.Settings.minFactoryOre = numMinFactoryOre.Value;

            Bot.Settings.GoMC = chkMC.Checked;
            Bot.Settings.MCWorkTime = numMCWorkTime.Value;

            Bot.Settings.SDWorkTime = numSDWorkTime.Value;
            Bot.Settings.SDThimblesMoney = numSDPlayThimbles.Value * 1000;

            if (Bot.Settings.TrainWarPetType != cboxTrainWarPetType.SelectedIndex + 1) { Bot.Me.WarPet.Focus = 0; Bot.Me.WarPet.Loyality = 0; Bot.Me.WarPet.Mass = 0; Bot.Me.WarPet.TrainTimeOutDT = new DateTime(); } //При смене прокачеваемого питомца скидываем сохраненные характеристики
            Bot.Settings.TrainWarPet = chkTrainWarPet.Checked;
            Bot.Settings.TrainWarPetType = cboxTrainWarPetType.SelectedIndex + 1;
            Bot.Settings.maxTrainPetFocus = numMaxTrainPetFocus.Value;
            Bot.Settings.TrainPetFocus = chkTrainPetFocus.Checked;
            Bot.Settings.maxTrainPetLoyality = numMaxTrainPetLoyality.Value;
            Bot.Settings.TrainPetLoyality = chkTrainPetLoyality.Checked;
            Bot.Settings.maxTrainPetMass = numMaxTrainPetMass.Value;
            Bot.Settings.TrainPetMass = chkTrainPetMass.Checked;
            if (Bot.Settings.TrainRunPetType != cboxTrainRunPetType.SelectedIndex + 1) { Bot.Me.RunPet.Acceleration = 0; Bot.Me.RunPet.Speed = 0; Bot.Me.RunPet.Endurance = 0; Bot.Me.RunPet.Dexterity = 0; Bot.Me.RunPet.TrainTimeOutDT = new DateTime(); Bot.Me.RunPet.Doping = false; Bot.Me.RunPet.FreeRuns.Stop = false; } //При смене прокачеваемого питомца скидываем сохраненные характеристики
            Bot.Settings.TrainRunPet = chkTrainRunPet.Checked;
            Bot.Settings.TrainRunPetType = cboxTrainRunPetType.SelectedIndex + 1;
            Bot.Settings.maxTrainPetAcceleration = numMaxTrainPetAcceleration.Value;
            Bot.Settings.TrainPetAcceleration = chkTrainRunPetAcceleration.Checked;
            Bot.Settings.maxTrainPetSpeed = numMaxTrainPetSpeed.Value;
            Bot.Settings.TrainPetSpeed = chkTrainRunPetSpeed.Checked;
            Bot.Settings.maxTrainPetEndurance = numMaxTrainPetEndurance.Value;
            Bot.Settings.TrainPetEndurance = chkTrainRunPetEndurance.Checked;
            Bot.Settings.maxTrainPetDexterity = numMaxTrainPetDexterity.Value;
            Bot.Settings.TrainPetDexterity = chkTrainRunPetDexterity.Checked;
            Bot.Settings.UseTrainWhip = chkUseTrainWhip.Checked;
            Bot.Settings.UseRunPet = chkUseRunPet.Checked;
            Bot.Settings.minTrainPetMoney = numMinTrainPetMoney.Value;
            Bot.Settings.minTrainPetOre = numMinTrainPetOre.Value;
            Bot.Settings.minTrainPetOil = numMinTrainPetOil.Value;            

            if (Bot.Settings.WantedPlayThimbles != chkWantedPlayThimbles.Checked || Bot.Settings.WantedGoMC != chkWantedGoMC.Checked) Bot.Me.Wanted = false;
            Bot.Settings.WantedPlayThimbles = chkWantedPlayThimbles.Checked;
            Bot.Settings.minWantedPlayThimbles = numWantedPlayThimbles.Value * 1000;
            Bot.Settings.WantedGoMC = chkWantedGoMC.Checked;

            Bot.Settings.GoClanFight = chkGoClanFight.Checked;
            Bot.Settings.minClanMeFightHp = numClanMeFightHP.Value;
            Bot.Settings.minClanPetFightHp = numClanPetFightHP.Value;
            Bot.Settings.ClanLastMin = chkClanLastMin.Checked;
            Bot.Settings.UseAutoFightBagSlots = chkAutoFightSlots.Checked;

            #region Матрица использования предметов в стенках (Мултидименсиональные эррэи не сереализируются=()
            if (Bot.Settings.UseGrpFightItems == null) Bot.Settings.UseGrpFightItems = new bool[30]; //5 видов * 6 стенок (Хаот, Руда, Клан, NPC, Пахан, Резерв)
            Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 0] = chkGrpFightUseBomb.Checked;
            Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 1] = chkGrpFightUseChees.Checked;
            Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 2] = chkGrpFightUseHeal.Checked;
            Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 3] = chkGrpFightUseOther.Checked;
            Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 4] = chkGrpFightUseBear.Checked;
            #endregion

            Bot.Settings.PlayLoto = chkPlayLoto.Checked;
            Bot.Settings.PlayKubovich = chkPlayKubovich.Checked;
            Bot.Settings.maxKubovichRotations = cboxKubovich.SelectedIndex + 1;
            Bot.Settings.BuyFishki = chkBuyFishki.Checked;
            Bot.Settings.BuyFishkiAllways = rbFishkiAllways.Checked;
            Bot.Settings.FishkiAmount = numBuyFishkiAmount.Value;

            Bot.Settings.GoPyramid = chkPyramid.Checked;
            Bot.Settings.BlockThimbles = chkBlockThimbles.Checked;
            Bot.Settings.maxPyramidPrice = numMaxPyramidPrice.Value;
            Bot.Settings.minPyramidAmount = numPyramidAmount.Value;
            Bot.Settings.maxPyramidSell = numPyramidWanted.Value;

            if (Bot.Settings.CarPrize != cboxCarPrize.SelectedIndex + 1) { Bot.Me.Automobile.LastDT = DateTime.Now; Bot.Me.Automobile.Stop = false; } //При смене максимальной бомбёжки, скидываем стоппер.
            Bot.Settings.UseCar = chkCar.Checked;
            Bot.Settings.CarPrize = cboxCarPrize.SelectedIndex + 1;
            Bot.Settings.UseSpecialCar = chkUseSpecialCar.Checked;
            Bot.Settings.SpecialCar = cboxSpecialCar.SelectedIndex + 1;

            Bot.Settings.TrainMe = chkTrainMe.Checked;
            Bot.Settings.TrainMeHealth = chkTrainHealth.Checked;
            Bot.Settings.maxTrainMeHealth = numMaxTrainHealth.Value;
            Bot.Settings.TrainMeStrength = chkTrainSrength.Checked;
            Bot.Settings.maxTrainMeStrength = numMaxTrainSrength.Value;
            Bot.Settings.TrainMeDexterity = chkTrainDexterity.Checked;
            Bot.Settings.maxTrainMeDexterity = numMaxTrainDexterity.Value;
            Bot.Settings.TrainMeEndurance = chkTrainEndurance.Checked;
            Bot.Settings.maxTrainMeEndurance = numMaxTrainEndurance.Value;
            Bot.Settings.TrainMeCunning = chkTrainCunning.Checked;
            Bot.Settings.maxTrainMeCunning = numMaxTrainCunning.Value;
            Bot.Settings.TrainMeAttentiveness = chkTrainAttentiveness.Checked;
            Bot.Settings.maxTrainMeAttentiveness = numMaxTrainAttentiveness.Value;
            Bot.Settings.TrainMeCharisma = chkTrainCharisma.Checked;
            Bot.Settings.maxTrainMeCharisma = numMaxTrainCharisma.Value;
            
            Bot.Settings.OpenPrizeBox = chkOpenPrizeBox.Checked;
            Bot.Settings.OpenReturnBox = chkOpenReturnBox.Checked;
            Bot.Settings.BuySafe = chkBuySafe.Checked;
            Bot.Settings.BuyMajor = chkBuyMajor.Checked;
            Bot.Settings.Quest = chkQuest.Checked;
            Bot.Settings.QuestEndMoney = rbQuestEndMoney.Checked;
            Bot.Settings.QuestFillTonusBottle = chkQuestFillTonusBottle.Checked;
            Bot.Settings.QuestFillTonusPlus = chkQuestFillTonusPlus.Checked;
            Bot.Settings.FeedTaborPet = chkFeedTaborPet.Checked;
            Bot.Settings.GetMetroWarPrize = chkMetroWarPrize.Checked;

            Bot.Settings.BuyMonaTicketTooth = chkMonaTicketTooth.Checked;
            Bot.Settings.minTeeth = numMonaMinTeeth.Value;
            Bot.Settings.BuyMonaTicketStar = chkMonaTicketStar.Checked;
            Bot.Settings.minStars = numMonaMinStars.Value;

            Bot.Settings.GoGroupFightChaos = chkGoGroupFightChaos.Checked;
            Bot.Settings.GoGroupFightOre = chkGoGroupFightOre.Checked;
            Bot.Settings.GoGroupFightMafia = chkGoGroupFightMafia.Checked;
            Bot.Settings.MafiaUseLicence = chkMafiaUseLicence.Checked;

            if (Bot.Settings.maxOilLvl < cboxOilLvl.SelectedIndex + 1 || chkOilUseOhara.Checked) { Bot.Me.OilHunting.Val = 0; Bot.Me.OilHunting.Stop = false; } //При смене уровня прохождения вентилей, скидываем стоппер.
            Bot.Settings.GoOil = chkGoOil.Checked;
            Bot.Settings.maxOilLvl = cboxOilLvl.SelectedIndex + 1;
            Bot.Settings.OilIgnoreTimeout = chkOilIgnoreTimeout.Checked;
            Bot.Settings.UseSnikersOil = chkUseSnikersOil.Checked;
            Bot.Settings.GoOilLenin = chkGoOilLenin.Checked;
            if (Bot.Settings.maxOilLeninLvl < cboxOilLeninLvl.SelectedIndex + 1 || chkOilUseOhara.Checked) { Bot.Me.OilLeninHunting.Defeats = 0; Bot.Me.OilLeninHunting.Stop = false; } //При смене уровня прохождения вентилей, скидываем стоппер.
            Bot.Settings.maxOilLeninLvl = cboxOilLeninLvl.SelectedIndex + 1;
            Bot.Settings.OilLeninLeaveNoKey = chkOilLeninLeaveNoKey.Checked;
            Bot.Settings.OilLeninLeaveNoElement = chkOilLeninLeaveNoElement.Checked;
            Bot.Settings.OilLeninLeaveNoBox = chkOilLeninLeaveNoBox.Checked;
            Bot.Settings.OilLeninRobinHood = chkOilLeninRobinHood.Checked;
            Bot.Settings.OilLeninIronHead = chkOilLeninIronHead.Checked;
            Bot.Settings.OilLeninSyncRats = chkSyncOilLenin.Checked;
            Bot.Settings.OffsetSyncOilLenin = numOffsetSyncOilLenin.Value;
            Bot.Settings.maxOilLeninDice = numMaxOilLeninDice.Value;
            Bot.Settings.maxOilDefeats = numMaxOilDefeats.Value;
            Bot.Settings.OilUseOhara = chkOilUseOhara.Checked;
            #region Матрица использования предметов в ленинопроводе (Мултидименсиональные эррэи не сереализируются=()
            if (Bot.Settings.UseOilLeninItems == null) Bot.Settings.UseOilLeninItems = new bool[25]; //5 видов * 5 стенок
            Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 0] = chkUseItemOilLeninFightLvl8.Checked;
            Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 1] = chkUseItemOilLeninFightLvl17.Checked;
            Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 2] = chkUseItemOilLeninFightLvl26.Checked;
            Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 3] = chkUseItemOilLeninFightLvl29.Checked;
            Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 4] = chkUseItemOilLeninFightLvl30.Checked;
            #endregion

            if (Bot.Settings.AddClan != chkAddClan.Checked) { Bot.Me.ClanWarInfo.NextDT = DateTime.Now; Bot.Me.ClanWarInfo.Now = false; }            
            Bot.Settings.AddClan = chkAddClan.Checked;            
            Bot.Settings.FarmClan = chkFarmClan.Checked;
            Bot.Settings.RemoveEnemy = chkRemoveEnemy.Checked;
            Bot.Settings.ClanWars = chkClanWars.Checked;
            Bot.Settings.Berserker = chkBerserker.Checked;
            Bot.Settings.UseSnikersEnemy = chkUseSnikersEnemy.Checked;

            Bot.Settings.Lampofob = rbHateLamp.Checked;

            Bot.Settings.UseRestartMemory = chkRestartByMemory.Checked;
            Bot.Settings.maxRestartMemory = numMaxRestartMemory.Value;
            Bot.Settings.RestartHidden = chkRestartHidden.Checked;
            Bot.Settings.RestartDoping = chkRestartDoping.Checked;
            Bot.Settings.CheckForUpdate = chkCheckForUpdate.Checked;

            Bot.Settings.ServerURL = (string)cboxServer.SelectedItem;

            if (cboxWerewolf.SelectedIndex == 1)
            {
                #region Оборотень
                Bot.Settings.UseWerewolf = true;
                Bot.Settings.WerewolfOpponent = (clsBot.Opponent)cboxOpponent.SelectedIndex;
                Bot.Settings.minWerewolfLvl = numAlleyMinLvl.Value;
                Bot.Settings.maxWerewolfLvl = numAlleyMaxLvl.Value;
                #endregion
            }
            else
            {                
                #region Агенты и Персонаж
                Bot.Settings.UseWerewolf = false;
                if (chkUseAgent.Checked) 
                {
                    Bot.Settings.AgentOpponent = (clsBot.Opponent)cboxOpponent.SelectedIndex;
                    Bot.Settings.minAgentLvl = numAlleyMinLvl.Value;
                    Bot.Settings.maxAgentLvl = numAlleyMaxLvl.Value;
                }                
                else               
                {           
                    Bot.Settings.AlleyOpponent = (clsBot.Opponent)cboxOpponent.SelectedIndex;
                    Bot.Settings.minAlleyLvl = numAlleyMinLvl.Value;
                    Bot.Settings.maxAlleyLvl = numAlleyMaxLvl.Value;
                } 
                #endregion
            }
            Bot.Settings.WerewolfLevel = cboxWerewolfLevel.SelectedIndex + 1;
            Bot.Settings.WerewolfPrice = cboxWerewolfPrice.SelectedIndex;

            Bot.Settings.UseWearSet = chkUseWearSet.Checked;
            Bot.Settings.ReadPrivateMessages = chkReadPrivateMessages.Checked;
            Bot.Settings.BuildTurel = chkBuildTurel.Checked;
            Bot.Settings.ReadLogs = chkReadLogs.Checked;
            Bot.Settings.PigProtection = chkPigProtecttion.Checked;

            Bot.Settings.PreferZefir = chkDopingZefir.Checked;
            Bot.Settings.PreferShokoZefir = chkDopingShokoZefir.Checked;
            Bot.Settings.PreferPryanik = chkDopingPryanik.Checked;
            Bot.Settings.AllowCoctailAdv = chkDopingAllowCoctailAdv.Checked;
            Bot.Settings.AllowPartBilet = chkDopingAllowPartBilet.Checked;
            Bot.Settings.NoSauceNoProblem = chkDopingNoSauceNoProblem.Checked;
            Bot.Settings.NoCandyNoProblem = chkDopingNoCandyNoProblem.Checked;
            Bot.Settings.NoCoctailNoProblem = chkDopingNoCoctailNoProblem.Checked;

            Bot.Settings.RepairMobile = chkRepairMobile.Checked;
            Bot.Settings.SellRepairMobile = chkSellRepairMobile.Checked;

            Bot.Settings.UseMaxDuels = chkUseMaxDuels.Checked;
            Bot.Settings.maxDuels = numMaxDuels.Value;
            Bot.Settings.UseDuelTimes = chkUseDuelTimes.Checked;
            Bot.Settings.StartDuelsDT = dtStartDuels.Value;
            Bot.Settings.StopDuelsDT = dtStopDuels.Value;

            Bot.Settings.BuyBankSafe = chkBuyBankSafe.Checked;
            Bot.Settings.UseBankDeposit = chkBankDeposit.Checked;
            Bot.Settings.DepositMoney = numBankDeposit.Value * 1000;

            Bot.Settings.PlayAzazella25 = chkAzazella25.Checked;
            Bot.Settings.PlayAzazella75 = chkAzazella75.Checked;
            Bot.Settings.minAzazellaGold = numMinAzazellaGold.Value;
            Bot.Settings.AzazellaFastPlay = chkAzazellaFastPlay.Checked;
            Bot.Settings.AzazellaTreasure = chkAzazellaTreasure.Checked;
            Bot.Settings.AzazellaTreasureChance = numAzazellaTreasureChance.Value;

            Bot.Settings.UseAFK = chkUseAFK.Checked;
            Bot.Settings.AFKCahnce = numAFKChance.Value;
            Bot.Settings.AFCTime = numAFKTime.Value;

            Bot.Settings.UseCookCoctail = chkUseCoctailCook.Checked;
            #region Матрица варения коктейлей
            if (Bot.Settings.CookCoctailType == null) Bot.Settings.CookCoctailType = new decimal[6]; //6 видов
            Bot.Settings.CookCoctailType[cboxCoctailCookType.SelectedIndex] = numCoctailCookAmount.Value;
            if (Bot.Settings.CookCoctailSpecials == null) Bot.Settings.CookCoctailSpecials = new int[48]; //6 видов * (4 типа + 4 кол.-во компонента)
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 0] = cboxCoctailCookIceCream.SelectedIndex;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 1] = cboxCoctailCookPiece.SelectedIndex;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 2] = cboxCoctailCookStraw.SelectedIndex;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 3] = cboxCoctailCookUmbrella.SelectedIndex;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 4] = (int)numCoctailCookIceCream.Value;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 5] = (int)numCoctailCookPeace.Value;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 6] = (int)numCoctailCookStraw.Value;
            Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 7] = (int)numCoctailCookUmbrella.Value;
            #endregion
            Bot.Settings.TotalFruitsProRecipe = numTotalFruitsProRecipe.Value;
            Bot.Settings.UseMaxFruitProRecipe = chkUseMaxFruitProRecipe.Checked;
            Bot.Settings.MaxFruitProRecipe = numMaxFruitProRecipe.Value;
            Bot.Settings.UseMinFruitIgnoreAmount = chkUseMinFruitIgnoreAmount.Checked;
            Bot.Settings.MinFruitIgnoreAmount = numMinFruitIgnoreAmount.Value;
            Bot.Settings.SellBadCoctail = chkSellBadCoctail.Checked;

            Bot.PrivSettings.Proxy = mtxtProxy.Text;
            Bot.PrivSettings.ProxyUserName = txtProxyUserName.Text;
            Bot.PrivSettings.ProxyPassword = txtProxyPassword.Text;
            Bot.Settings.UseProxy = chkProxy.Checked;
            Bot.WBEx.SetProxyServer(Bot.Settings.UseProxy ? Bot.PrivSettings.Proxy : null);

            if (Bot.Settings.MaxIEVersion != numMaxIEVersion.Value) clsWBEx.EmulateIEMode(ctrMainBrowser, (int)numMaxIEVersion.Value);
            Bot.Settings.MaxIEVersion = numMaxIEVersion.Value;
            Bot.Settings.GagIE = numGagIE.Value;

            Bot.Me.Events.ShutdownDT = dtlstShutDown.Value;
            Bot.Me.Events.ShutdownRelease = chkShutDown.Checked; //необходимо отрубить комп по таску?          

            Bot.Settings.GetReturnBonus = chkGetReturnBonus.Checked;

            Bot.Settings.SendTrucks = chkSendTrucks.Checked;
            Bot.Settings.TrucksCheckInterval = numTrucksCheckInterval.Value;
            Bot.Settings.TrucksMinPowerPoints = numTrucksMinPowerPoints.Value;
            Bot.Settings.Trucks = new clsBot.TruckSettings[12];
            for (int i = 0; i < 12; i++)
            {
                Bot.Settings.Trucks[i].Send = ((CheckBox)tblTrucks.GetControlFromPosition(0, 3 + i)).Checked;
                Bot.Settings.Trucks[i].Enhancings = new int[6];
                for (int j = 0; j < 6; j++)
                    Bot.Settings.Trucks[i].Enhancings[j] = ((Button)tblTrucks.GetControlFromPosition(2 + j, 3 + i)).ImageIndex;
            }

            Bot.SaveSettings();
        }
        private void LoadSettings()
        {
            if (File.Exists("BotSettings.xml"))
            {
                Bot.LoadSettings();

                txtBotName.Text = Bot.PrivSettings.BotName;
                txtEmail.Text = Bot.PrivSettings.Email;
                txtPassword.Text = Bot.PrivSettings.Password;

                numHealMe50.Value = Bot.Settings.HealMe50;
                numHealMe100.Value = Bot.Settings.HealMe100;
                numAmountHealMe.Value = Bot.Settings.AmountHealMe;
                chkBuyHealMe.Checked = Bot.Settings.BuyHealMe;
                numHealPet50.Value = Bot.Settings.HealPet50;
                numHealPet100.Value = Bot.Settings.HealPet100;
                chkBuyHealPet.Checked = Bot.Settings.BuyHealPet;
                cboxSetWarPetType.SelectedIndex = Bot.Settings.SetWarPetType;
                chkHealTrauma.Checked = Bot.Settings.HealTrauma;
                chkHealInjuredSlot.Checked = Bot.Settings.HealInjuredSlot;

                chkHC.Checked = Bot.Settings.GoHC;
                numMinHCLvl.Value = Enumerable.Range(-1, 4).Contains<int>((int)Bot.Settings.minHCLvl) ? Bot.Settings.minHCLvl : -1;
                numMaxHCLvl.Value = Enumerable.Range(-1, 4).Contains<int>((int)Bot.Settings.maxHCLvl) ? Bot.Settings.maxHCLvl : 2;
                numStatDiff.Value = Bot.Settings.minHCStatDiff;
                dtStartHC.Value = Bot.Settings.StartHC;
                dtStopHC.Value = Bot.Settings.StopHC;
                chkHCMember.Checked = Bot.Settings.HCMember; 
                chkHCUseTorture.Checked = Bot.Settings.HCUseTorture;
                chkHCRevenge.Checked = Bot.Settings.HCRevenge;
                numHCRevengeMaxMoney.Value = Bot.Settings.HCRevengeMaxMoney;

                chkMetro.Checked = Bot.Settings.GoMetro;
                chkSearchRat.Checked = Bot.Settings.SearchRat;
                rbBuyMPick.Checked = Bot.Settings.BuyMpick;
                rbBuyRPick.Checked = Bot.Settings.BuyRpick;
                chkBuyHelmet.Checked = Bot.Settings.BuyHelmet;
                chkBuyCounter.Checked = Bot.Settings.BuyCounter;
                chkAttackRat.Checked = Bot.Settings.AttackRat;
                numMaxRatLvl.Value = Bot.Settings.maxRatLvl;
                numMaxRatDefeats.Value = Bot.Settings.maxRatDefeats;
                numMaxSearchRatLvl.Value = Bot.Settings.maxSearchRatLvl;
                
                chkSearchRatLeaveNoKey.Checked = Bot.Settings.SearchRatLeaveNoKey;
                chkSearchRatLeaveNoElement.Checked = Bot.Settings.SearchRatLeaveNoElement;
                chkSearchRatLeaveNoBox.Checked = Bot.Settings.SearchRatLeaveNoBox;
                chkSearchRatIgnoreAll.Checked = Bot.Settings.SearchRatIgnoreAll;
                chkSearchRatRobinHood.Checked = Bot.Settings.SearchRatRobinHood;
                chkSearchRatBambula.Checked = Bot.Settings.SearchRatBambula;
                numMaxSearchRatDefeats.Value = Bot.Settings.maxSearchRatDefeats;
                chkSearchRatUseOhara.Checked = Bot.Settings.SearchRatUseOhara;
                chkUseRatFastSearch.Checked = Bot.Settings.UseRatFastSearch;
                numRatFastSearch.Value = Bot.Settings.RatFastSearch == 0 ? 25 : Bot.Settings.RatFastSearch;
                rbRatFastSearchHoney.Checked = Bot.Settings.RatFastSearchHoney;
                #region Матрица использования предметов в крысиных стенках
                if (Bot.Settings.UseRatItems != null)
                {
                    if(Bot.Settings.UseRatItems.Count<bool>() < 35) Array.Resize<bool>(ref Bot.Settings.UseRatItems, 35); //5 видов * 7 стенок
                    chkUseItemRatFightLvl5.Checked = Bot.Settings.UseRatItems[0];
                    chkUseItemRatFightLvl10.Checked = Bot.Settings.UseRatItems[1];
                    chkUseItemRatFightLvl15.Checked = Bot.Settings.UseRatItems[2];
                    chkUseItemRatFightLvl20.Checked = Bot.Settings.UseRatItems[3];
                    chkUseItemRatFightLvl25.Checked = Bot.Settings.UseRatItems[4];
                    chkUseItemRatFightLvl30.Checked = Bot.Settings.UseRatItems[5];
                    chkUseItemRatFightLvl35.Checked = Bot.Settings.UseRatItems[6]; 
                }                               
                #endregion

                numPlayThimbles.Value = Bot.Settings.PlayThimbles / 1000;
                numExchangeBankMoney.Value = Bot.Settings.ExchangeBankMoney == 0 ? 100 : Bot.Settings.ExchangeBankMoney / 1000;
                numMinThimbles.Value = Bot.Settings.minThimblesMoney;
                chkThimblesImmunity.Checked = Bot.Settings.ThimblesImmunity;
                chkThimblesTicket.Checked = Bot.Settings.UseThimblesTicket;
                chkUseBank.Checked = Bot.Settings.UseBank; 

                chkUseVictims.Checked = Bot.Settings.UseVictims;
                numAddVictim.Value = Bot.Settings.AddVictim;
                numDeleteVictim.Value = Bot.Settings.DeleteVictim;
                chkUseOnlyHomelessVictims.Checked = Bot.Settings.UseOnlyHomelessVictims;

//                chkAttackNPC.Checked = Bot.Settings.AttackNPC;
                chkUseAgent.Checked = Bot.Settings.UseAgent;
                chkMrPlushkin.Checked = Bot.Settings.MrPlushkin;
                chkUseHomeless.Checked = Bot.Settings.UseHomeless;

                chkGoPVPFight.Checked = Bot.Settings.GoPVPFight;
//                chkGoNPCFight.Checked = Bot.Settings.GoNPCFight;
//                Bot.GrpFight.Val = (chkGoPVPFight.Checked ? 1 : 0) + (chkGoNPCFight.Checked ? 2 : 0); // 0-> Выкл., 1-> PVP, 2-> NPC, 3-> Все.
                chkGoPVPInstantly.Checked = Bot.Settings.GoPVPInstantly;
                numGoPVPInstantlyOffset.Value = Bot.Settings.GoPVPInstantlyOffset;
                chkSovetBuyAgitator.Checked = Bot.Settings.SovetBuyAgitator;

                chkPatrol.Checked = Bot.Settings.GoPatrol;
                cboxPatrol.SelectedIndex = Bot.Settings.PatrolType;
                numPatrolTime.Value = Bot.Settings.PatrolTime;

                numPayPolice.Value = Bot.Settings.PayPoliceAt;
                rbPolicePay.Checked = Bot.Settings.PayPolice;
                rbPoliceWait.Checked = Bot.Settings.WaitPolice;

                chkPetriki.Checked = Bot.Settings.MakePetriki;
                cboxPetrikiBonus.SelectedIndex = Bot.Settings.PetrikiBonus;
                numMinPetrikiMoney.Value = Bot.Settings.minPetrikiMoney;
                numMinPetrikiOre.Value = Bot.Settings.minPetrikiOre;
                chkFactory.Checked = Bot.Settings.GoFactory;
                numFactoryChainCount.Value = Bot.Settings.FactoryChainCount;
                cboxFactoryRang.SelectedIndex = Bot.Settings.FactoryRang;
                numMinFactoryMoney.Value = Bot.Settings.minFactoryMoney;
                numMinFactoryOre.Value = Bot.Settings.minFactoryOre;

                chkMC.Checked = Bot.Settings.GoMC;
                numMCWorkTime.Value = Bot.Settings.MCWorkTime;

                numSDWorkTime.Value = Bot.Settings.SDWorkTime;
                numSDPlayThimbles.Value = Bot.Settings.SDThimblesMoney / 1000;

                chkTrainWarPet.Checked = Bot.Settings.TrainWarPet;
                cboxTrainWarPetType.SelectedIndex = Bot.Settings.TrainWarPetType - 1;
                numMaxTrainPetFocus.Value = Bot.Settings.maxTrainPetFocus;
                chkTrainPetFocus.Checked = Bot.Settings.TrainPetFocus;
                numMaxTrainPetLoyality.Value = Bot.Settings.maxTrainPetLoyality;
                chkTrainPetLoyality.Checked = Bot.Settings.TrainPetLoyality;
                numMaxTrainPetMass.Value = Bot.Settings.maxTrainPetMass;
                chkTrainPetMass.Checked = Bot.Settings.TrainPetMass;
                chkTrainRunPet.Checked = Bot.Settings.TrainRunPet;
                cboxTrainRunPetType.SelectedIndex = Bot.Settings.TrainRunPetType - 1;
                numMaxTrainPetAcceleration.Value = Bot.Settings.maxTrainPetAcceleration;
                chkTrainRunPetAcceleration.Checked = Bot.Settings.TrainPetAcceleration;
                numMaxTrainPetSpeed.Value = Bot.Settings.maxTrainPetSpeed;
                chkTrainRunPetSpeed.Checked = Bot.Settings.TrainPetSpeed;
                numMaxTrainPetEndurance.Value = Bot.Settings.maxTrainPetEndurance;
                chkTrainRunPetEndurance.Checked = Bot.Settings.TrainPetEndurance;
                numMaxTrainPetDexterity.Value = Bot.Settings.maxTrainPetDexterity;
                chkTrainRunPetDexterity.Checked = Bot.Settings.TrainPetDexterity;
                chkUseTrainWhip.Checked = Bot.Settings.UseTrainWhip;
                chkUseRunPet.Checked = Bot.Settings.UseRunPet;
                numMinTrainPetMoney.Value = Bot.Settings.minTrainPetMoney;
                numMinTrainPetOre.Value = Bot.Settings.minTrainPetOre;
                numMinTrainPetOil.Value = Bot.Settings.minTrainPetOil;                

                chkWantedPlayThimbles.Checked = Bot.Settings.WantedPlayThimbles;
                numWantedPlayThimbles.Value = Bot.Settings.minWantedPlayThimbles / 1000;
                chkWantedGoMC.Checked = Bot.Settings.WantedGoMC;

                chkGoClanFight.Checked = Bot.Settings.GoClanFight;
                numClanMeFightHP.Value = Bot.Settings.minClanMeFightHp;
                numClanPetFightHP.Value = Bot.Settings.minClanPetFightHp;
                chkClanLastMin.Checked = Bot.Settings.ClanLastMin;
                chkAutoFightSlots.Checked = Bot.Settings.UseAutoFightBagSlots;

                #region Матрица использования предметов в стенках
                if (Bot.Settings.UseGrpFightItems != null)
                {
                    if (Bot.Settings.UseGrpFightItems.Count<bool>() < 30) Array.Resize<bool>(ref Bot.Settings.UseGrpFightItems, 30); //5 видов * 6 стенок (Хаот, Руда, Клан, NPC, Пахан, Резерв)
                    chkGrpFightUseBomb.Checked = Bot.Settings.UseGrpFightItems[0];
                    chkGrpFightUseChees.Checked = Bot.Settings.UseGrpFightItems[1];
                    chkGrpFightUseHeal.Checked = Bot.Settings.UseGrpFightItems[2];
                    chkGrpFightUseOther.Checked = Bot.Settings.UseGrpFightItems[3];
                    chkGrpFightUseBear.Checked = Bot.Settings.UseGrpFightItems[4];
                }
                #endregion

                chkPlayLoto.Checked = Bot.Settings.PlayLoto;
                chkPlayKubovich.Checked = Bot.Settings.PlayKubovich;
                cboxKubovich.SelectedIndex = Bot.Settings.maxKubovichRotations -1;
                chkBuyFishki.Checked = Bot.Settings.BuyFishki;
                rbFishkiAllways.Checked = Bot.Settings.BuyFishkiAllways;
                numBuyFishkiAmount.Value = Bot.Settings.FishkiAmount;
                
                chkPyramid.Checked = Bot.Settings.GoPyramid;
                chkBlockThimbles.Checked = Bot.Settings.BlockThimbles;
                numMaxPyramidPrice.Value = Bot.Settings.maxPyramidPrice;
                numPyramidAmount.Value = Bot.Settings.minPyramidAmount;
                numPyramidWanted.Value = Bot.Settings.maxPyramidSell;

                chkCar.Checked = Bot.Settings.UseCar;
                cboxCarPrize.SelectedIndex = Bot.Settings.CarPrize - 1;
                chkUseSpecialCar.Checked = Bot.Settings.UseSpecialCar;
                cboxSpecialCar.SelectedIndex = Bot.Settings.SpecialCar -1;

                chkTrainMe.Checked = Bot.Settings.TrainMe;
                chkTrainHealth.Checked = Bot.Settings.TrainMeHealth;
                numMaxTrainHealth.Value = Bot.Settings.maxTrainMeHealth;
                chkTrainSrength.Checked = Bot.Settings.TrainMeStrength;
                numMaxTrainSrength.Value = Bot.Settings.maxTrainMeStrength;
                chkTrainDexterity.Checked = Bot.Settings.TrainMeDexterity;
                numMaxTrainDexterity.Value = Bot.Settings.maxTrainMeDexterity;
                chkTrainEndurance.Checked = Bot.Settings.TrainMeEndurance;
                numMaxTrainEndurance.Value = Bot.Settings.maxTrainMeEndurance;
                chkTrainCunning.Checked = Bot.Settings.TrainMeCunning;
                numMaxTrainCunning.Value = Bot.Settings.maxTrainMeCunning;
                chkTrainAttentiveness.Checked = Bot.Settings.TrainMeAttentiveness;
                numMaxTrainAttentiveness.Value = Bot.Settings.maxTrainMeAttentiveness;
                chkTrainCharisma.Checked = Bot.Settings.TrainMeCharisma;
                numMaxTrainCharisma.Value = Bot.Settings.maxTrainMeCharisma;

                chkOpenPrizeBox.Checked = Bot.Settings.OpenPrizeBox;
                chkOpenReturnBox.Checked = Bot.Settings.OpenReturnBox;
                chkBuySafe.Checked = Bot.Settings.BuySafe;
                chkBuyMajor.Checked = Bot.Settings.BuyMajor;
                chkQuest.Checked = Bot.Settings.Quest;
                rbQuestEndMoney.Checked = Bot.Settings.QuestEndMoney;
                chkQuestFillTonusBottle.Checked = Bot.Settings.QuestFillTonusBottle;
                chkQuestFillTonusPlus.Checked = Bot.Settings.QuestFillTonusPlus;
                chkFeedTaborPet.Checked = Bot.Settings.FeedTaborPet;
                chkMetroWarPrize.Checked = Bot.Settings.GetMetroWarPrize;

                chkMonaTicketTooth.Checked = Bot.Settings.BuyMonaTicketTooth;
                numMonaMinTeeth.Value = Bot.Settings.minTeeth;
                chkMonaTicketStar.Checked = Bot.Settings.BuyMonaTicketStar;
                numMonaMinStars.Value = Bot.Settings.minStars;

                chkGoGroupFightChaos.Checked = Bot.Settings.GoGroupFightChaos;
                chkGoGroupFightOre.Checked = Bot.Settings.GoGroupFightOre;
                chkGoGroupFightMafia.Checked = Bot.Settings.GoGroupFightMafia;
                chkMafiaUseLicence.Checked = Bot.Settings.MafiaUseLicence;

                chkGoOil.Checked = Bot.Settings.GoOil;
                cboxOilLvl.SelectedIndex = Bot.Settings.maxOilLvl - 1;
                chkOilIgnoreTimeout.Checked = Bot.Settings.OilIgnoreTimeout;
                chkUseSnikersOil.Checked = Bot.Settings.UseSnikersOil;
                chkGoOilLenin.Checked = Bot.Settings.GoOilLenin;
                cboxOilLeninLvl.SelectedIndex = Bot.Settings.maxOilLeninLvl - 1;
                chkOilLeninLeaveNoKey.Checked = Bot.Settings.OilLeninLeaveNoKey;
                chkOilLeninLeaveNoElement.Checked = Bot.Settings.OilLeninLeaveNoElement;
                chkOilLeninLeaveNoBox.Checked = Bot.Settings.OilLeninLeaveNoBox;
                chkOilLeninRobinHood.Checked = Bot.Settings.OilLeninRobinHood;
                chkOilLeninIronHead.Checked = Bot.Settings.OilLeninIronHead;
                chkSyncOilLenin.Checked = Bot.Settings.OilLeninSyncRats;
                numOffsetSyncOilLenin.Value = Bot.Settings.OffsetSyncOilLenin;
                numMaxOilLeninDice.Value = Bot.Settings.maxOilLeninDice == 0 ? 1 : Bot.Settings.maxOilLeninDice;
                numMaxOilDefeats.Value = Bot.Settings.maxOilDefeats;
                chkOilUseOhara.Checked = Bot.Settings.OilUseOhara;
                #region Матрица использования предметов в ленинопроводе
                if (Bot.Settings.UseOilLeninItems != null)
                {
                    if (Bot.Settings.UseOilLeninItems.Count<bool>() < 25) Array.Resize<bool>(ref Bot.Settings.UseOilLeninItems, 25); //5 видов * 5 стенок
                    chkUseItemOilLeninFightLvl8.Checked = Bot.Settings.UseOilLeninItems[0];
                    chkUseItemOilLeninFightLvl17.Checked = Bot.Settings.UseOilLeninItems[1];
                    chkUseItemOilLeninFightLvl26.Checked = Bot.Settings.UseOilLeninItems[2];
                    chkUseItemOilLeninFightLvl29.Checked = Bot.Settings.UseOilLeninItems[3];
                    chkUseItemOilLeninFightLvl30.Checked = Bot.Settings.UseOilLeninItems[4];
                }
                #endregion
    
                chkAddClan.Checked = Bot.Settings.AddClan;
                chkFarmClan.Checked = Bot.Settings.FarmClan;
                chkRemoveEnemy.Checked = Bot.Settings.RemoveEnemy;
                chkClanWars.Checked = Bot.Settings.ClanWars;
                chkBerserker.Checked = Bot.Settings.Berserker;
                chkUseSnikersEnemy.Checked = Bot.Settings.UseSnikersEnemy;

                rbHateLamp.Checked = Bot.Settings.Lampofob;

                chkRestartByMemory.Checked = Bot.Settings.UseRestartMemory;
                numMaxRestartMemory.Value = Bot.Settings.maxRestartMemory;
                chkRestartHidden.Checked = Bot.Settings.RestartHidden;
                chkRestartDoping.Checked = Bot.Settings.RestartDoping;
                chkCheckForUpdate.Checked = Bot.Settings.CheckForUpdate;
                #region Загрузка допингов.
                if (Bot.Settings.RestartDoping) //Загрузка допингов только если запуск был произведён без аргументов!
                {
                    #region Загрука ленинопроводных допингов, и сохранение для последующей визуализации
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrOilLeninDoping, "OilLenin.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай ленино-допинг, пожалуйста!");;
                    DopingStatus.OilLeninDoping = new clsDoping[lstDoping.Items.Count];
                    lstDoping.Items.CopyTo(DopingStatus.OilLeninDoping, 0);
                    #endregion
                    #region Загрука крысиных допингов, и сохранение для последующей визуализации
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrRatDoping, "Rat.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай крысо-допинг, пожалуйста!");;
                    DopingStatus.RatDoping = new clsDoping[lstDoping.Items.Count];
                    lstDoping.Items.CopyTo(DopingStatus.RatDoping, 0);
                    #endregion
                    #region Загрузка нормальных допингов и визуализация
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrUsualDoping, "Usual.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай обыденный-допинг, пожалуйста!");;
                    #endregion                    
                }
                cboxDopingArt.SelectedIndex = 0; //Последний видимый список допингов, был нормальный список
                DopingStatus.LastDopingArt = 0; //Последний видимый список допингов, был нормальный список
                #endregion

                cboxServer.SelectedItem = Bot.Settings.ServerURL == null ? "www.moswar.ru" : Bot.Settings.ServerURL;

                if (Bot.Settings.UseWerewolf)
                {
                    #region Оборотень
                    cboxWerewolf.SelectedIndex = 1;
                    numAlleyMinLvl.Value = Bot.Settings.minWerewolfLvl;
                    numAlleyMaxLvl.Value = Bot.Settings.maxWerewolfLvl;
                    #endregion
                }
                else
                {
                    #region Агенты и Персонаж
                    cboxWerewolf.SelectedIndex = 0;
                    if (Bot.Settings.UseAgent)
                    {
                        cboxOpponent.SelectedIndex = (int)Bot.Settings.AgentOpponent;
                        numAlleyMinLvl.Value = Bot.Settings.minAgentLvl;
                        numAlleyMaxLvl.Value = Bot.Settings.maxAgentLvl;
                    }
                    else 
                    {
                        cboxOpponent.SelectedIndex = (int)Bot.Settings.AlleyOpponent;
                        numAlleyMinLvl.Value = Bot.Settings.minAlleyLvl;
                        numAlleyMaxLvl.Value = Bot.Settings.maxAlleyLvl;
                    }                    
                    #endregion
                }
                cboxWerewolfLevel.SelectedIndex = Bot.Settings.WerewolfLevel - 1;
                cboxWerewolfPrice.SelectedIndex = Bot.Settings.WerewolfPrice;                

                chkUseWearSet.Checked = Bot.Settings.UseWearSet;
                #region Загрузка сетов.
                clsWearSets.Load(ref Bot.ArrWearSet, "Set.dat"); //Загрузка сетов!
                #endregion
                chkReadPrivateMessages.Checked = Bot.Settings.ReadPrivateMessages;
                chkBuildTurel.Checked = Bot.Settings.BuildTurel;
                chkReadLogs.Checked = Bot.Settings.ReadLogs;
                chkPigProtecttion.Checked = Bot.Settings.PigProtection;

                chkDopingZefir.Checked = Bot.Settings.PreferZefir;
                chkDopingShokoZefir.Checked = Bot.Settings.PreferShokoZefir;
                chkDopingPryanik.Checked = Bot.Settings.PreferPryanik;
                chkDopingAllowCoctailAdv.Checked = Bot.Settings.AllowCoctailAdv;
                chkDopingAllowPartBilet.Checked = Bot.Settings.AllowPartBilet;
                chkDopingNoSauceNoProblem.Checked = Bot.Settings.NoSauceNoProblem;
                chkDopingNoCandyNoProblem.Checked = Bot.Settings.NoCandyNoProblem;
                chkDopingNoCoctailNoProblem.Checked = Bot.Settings.NoCoctailNoProblem;

                chkRepairMobile.Checked = Bot.Settings.RepairMobile;
                chkSellRepairMobile.Checked = Bot.Settings.SellRepairMobile;

                chkUseMaxDuels.Checked = Bot.Settings.UseMaxDuels;
                numMaxDuels.Value = Bot.Settings.maxDuels;
                chkUseDuelTimes.Checked = Bot.Settings.UseDuelTimes;
                dtStartDuels.Value = Bot.Settings.StartDuelsDT;
                dtStopDuels.Value = Bot.Settings.StopDuelsDT; ;

                chkBuyBankSafe.Checked = Bot.Settings.BuyBankSafe;
                chkBankDeposit.Checked = Bot.Settings.UseBankDeposit;
                numBankDeposit.Value = Bot.Settings.DepositMoney / 1000;

                chkAzazella25.Checked = Bot.Settings.PlayAzazella25;
                chkAzazella75.Checked = Bot.Settings.PlayAzazella75;
                numMinAzazellaGold.Value = Bot.Settings.minAzazellaGold;
                chkAzazellaFastPlay.Checked = Bot.Settings.AzazellaFastPlay;
                chkAzazellaTreasure.Checked = Bot.Settings.AzazellaTreasure;
                numAzazellaTreasureChance.Value = Bot.Settings.AzazellaTreasureChance;

                chkUseAFK.Checked = Bot.Settings.UseAFK;
                numAFKChance.Value = Bot.Settings.AFKCahnce;
                numAFKTime.Value = Bot.Settings.AFCTime;

                chkUseCoctailCook.Checked = Bot.Settings.UseCookCoctail;
                #region Матрица варения коктейлей
                if (Bot.Settings.CookCoctailType != null)
                {
                    if (Bot.Settings.CookCoctailType.Count<decimal>() < 6) Array.Resize<decimal>(ref Bot.Settings.CookCoctailType, 6); //6 видов
                    numCoctailCookAmount.Value = Bot.Settings.CookCoctailType[cboxCoctailCookType.SelectedIndex];
                }
                if (Bot.Settings.CookCoctailSpecials != null)
                {
                    if (Bot.Settings.CookCoctailSpecials.Count<int>() < 48) Array.Resize<int>(ref Bot.Settings.CookCoctailSpecials, 48); //6 видов * (4 типа + 4 кол.-во компонента)
                    cboxCoctailCookIceCream.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 0];
                    cboxCoctailCookPiece.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 1];
                    cboxCoctailCookStraw.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 2];
                    cboxCoctailCookUmbrella.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 3];
                    numCoctailCookIceCream.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 4];
                    numCoctailCookPeace.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 5];
                    numCoctailCookStraw.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 6];
                    numCoctailCookUmbrella.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 7];
                }                
                #endregion
                numTotalFruitsProRecipe.Value = Bot.Settings.TotalFruitsProRecipe == 0 ? 160 : Bot.Settings.TotalFruitsProRecipe;
                chkUseMaxFruitProRecipe.Checked = Bot.Settings.UseMaxFruitProRecipe;
                numMaxFruitProRecipe.Value = Bot.Settings.MaxFruitProRecipe == 0 ? 45 : Bot.Settings.MaxFruitProRecipe;
                chkUseMinFruitIgnoreAmount.Checked = Bot.Settings.UseMinFruitIgnoreAmount;
                numMinFruitIgnoreAmount.Value = Bot.Settings.MinFruitIgnoreAmount == 0 ? 3500 : Bot.Settings.MinFruitIgnoreAmount;
                chkSellBadCoctail.Checked = Bot.Settings.SellBadCoctail;

                mtxtProxy.Text = Bot.PrivSettings.Proxy;
                txtProxyUserName.Text = Bot.PrivSettings.ProxyUserName;
                txtProxyPassword.Text = Bot.PrivSettings.ProxyPassword;
                chkProxy.Checked = Bot.Settings.UseProxy;
                Bot.WBEx.SetProxyServer(Bot.Settings.UseProxy ? Bot.PrivSettings.Proxy : null);

                numMaxIEVersion.Value = Bot.Settings.MaxIEVersion < 7 ? 11 : Bot.Settings.MaxIEVersion;
                numGagIE.Value = Bot.Settings.GagIE < 30 ? 60 : Bot.Settings.GagIE; 

                if (Bot.Me.Events.ShutdownRelease) //необходимо отрубить комп по таску?
                {
                    dtlstShutDown.Value = Bot.Me.Events.ShutdownDT;
                    chkShutDown.Checked = Bot.Me.Events.ShutdownRelease; 
                }           

                chkGetReturnBonus.Checked = Bot.Settings.GetReturnBonus;

                if (Bot.Settings.TrucksCheckInterval != 0)
                {
                    chkSendTrucks.Checked = Bot.Settings.SendTrucks;
                    numTrucksCheckInterval.Value = Bot.Settings.TrucksCheckInterval;
                    numTrucksMinPowerPoints.Value = Bot.Settings.TrucksMinPowerPoints;
                    for (int i = 0; i < 12; i++)
                    {
                        ((CheckBox)tblTrucks.GetControlFromPosition(0, 3 + i)).Checked = Bot.Settings.Trucks[i].Send;
                        for (int j = 0; j < 6; j++)
                            ((Button)tblTrucks.GetControlFromPosition(2 + j, 3 + i)).ImageIndex = Bot.Settings.Trucks[i].Enhancings[j];
                    }
                }
            }

            UpdateSettings();
        }

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmBrowser_Load(object sender, EventArgs e)
        {
            lblUserMessage.Text = "";
            lblVersion.Text = "Version: " + Application.ProductVersion;            
            btnTest.Visible = System.Diagnostics.Debugger.IsAttached; //Показывать кнопку тест, если из дебагера            

            lblSettingsCaption.Text = "";

            cboxSetWarPetType.SelectedIndex = 0;
            cboxOpponent.SelectedIndex = 2;
            cboxPatrol.SelectedIndex = 0;
            cboxPetrikiBonus.SelectedIndex = 4;
            cboxRatGrpFightUseItems.SelectedIndex = 0;            
            cboxGrpFightUseItems.SelectedIndex = 0;
            cboxExpGrpFightType.SelectedIndex = 0;
            cboxExpGrpFightSlot1.SelectedIndex = 0;
            cboxExpGrpFightSlot2.SelectedIndex = 0;
            cboxExpGrpFightSlot3.SelectedIndex = 0;
            cboxExpGrpFightSlot4.SelectedIndex = 0;
            cboxExpGrpFightSlot5.SelectedIndex = 0;
            cboxExpGrpFightSlot6.SelectedIndex = 0;
            cboxTrainWarPetType.SelectedIndex = 0;
            cboxTrainRunPetType.SelectedIndex = 0;            
            cboxCarPrize.SelectedIndex = 0;
            cboxSpecialCar.SelectedIndex = 0;
            cboxKubovich.SelectedIndex = 0;
            cboxOilLeninLvl.SelectedIndex = 0;
            cboxOilLvl.SelectedIndex = 0;
            cboxOilLeninGrpFightUseItems.SelectedIndex = 0;
            cboxServer.SelectedIndex = 0;
            cboxFactoryRang.SelectedIndex = 0;
            cboxWerewolf.SelectedIndex = 0;
            cboxWerewolfLevel.SelectedIndex = 0;
            cboxWerewolfPrice.SelectedIndex = 0;
            cboxWearSet.SelectedIndex = 0;
            cboxCoctailCookType.SelectedIndex = 0;
            cboxCoctailCookIceCream.SelectedIndex = 0;
            cboxCoctailCookPiece.SelectedIndex = 0;
            cboxCoctailCookStraw.SelectedIndex = 0;
            cboxCoctailCookUmbrella.SelectedIndex = 0;
            cboxExpGrpFightPlanerArt.SelectedIndex = 0;
            Bot.MainWB = ctrMainBrowser;
            Bot.HelpWB[0] = ctrHelpBrowser1;
            Bot.HelpWB[1] = ctrHelpBrowser2;
            Bot.TS = statusStrip;
            Bot.LBBlackWanted = lstBlackWanted;
            Bot.LBHistory = lstHistory;
            Bot.LUserMessage = lblUserMessage;
            Bot.FrmMainhWnd = this.Handle;
            Bot.MyMainForm = this;                       

            BotThread = new Thread(new ThreadStart(Bot.StartBot));

            Bot.HCThread[0] = new Thread(new ThreadStart(StartMultiThread));
            Bot.HCThread[1] = new Thread(new ThreadStart(StartMultiThread));

            STimer.Interval = 1000;
            STimer.Elapsed += new ElapsedEventHandler(STimer_Tick);
            STimer.SynchronizingObject = this;
            
            #region ToolTips
            Tip.AutoPopDelay = 30000;

            Tip.SetToolTip(cboxWearSet, "Описание сетов:\nКрысолов - костюм охотника в крысопроводе\nХаризматик - используется в патруле и шаурме\nОктябренок - костюм для дуэлей на ленинопроводе\nД'артаньян - костюм для стенок в ленино-крысо-проводах\nПовстанец - костюм для стенок противостояния\nСтандартный - костюм на все остальные случаи жизни\nНудист - костюм в котором знаменитые лампофобы проводят 98% игрового времени.");
            #endregion
            #region IntelliLock
            txtRegCode.Text = IntelliLock.Licensing.HardwareID.GetHardwareID(true, true, true, true, true, false);
            switch (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.LicenseStatus)
            {
                case IntelliLock.Licensing.LicenseStatus.Licensed: txtLicensStatus.Text = "Катаемся, лицензия куплена!"; txtLicensStatus.ForeColor = System.Drawing.Color.DarkGreen; ; break;
                case IntelliLock.Licensing.LicenseStatus.Deactivated: txtLicensStatus.Text = "Лицензия деактивирована!"; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.EvaluationExpired: txtLicensStatus.Text = "Лицензия истекла!"; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.EvaluationMode: txtLicensStatus.Text = IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDate >= DateTime.Today ? "Колеса годны, до: " + IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDate.ToShortDateString() : "Демоверсия."; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.HardwareNotMatched: txtLicensStatus.Text = "Несовпадение железа."; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.InvalidSignature: txtLicensStatus.Text = "Ошибка сигнатур."; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.LicenseFileNotFound: txtLicensStatus.Text = "Файл не найден."; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
                case IntelliLock.Licensing.LicenseStatus.ServerValidationFailed: txtLicensStatus.Text = "Не пройдена верификация."; txtLicensStatus.ForeColor = System.Drawing.Color.Red; ; break;
            }
            if (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.LicenseStatus == IntelliLock.Licensing.LicenseStatus.EvaluationMode)
            {
                if (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDate_Enabled)
                {
                    if (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDate < DateTime.Today) MessageBox.Show("Период \"Бета-тестирования\" окончен.");
                }
                else
                    MessageBox.Show(
                        IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDays_Enabled ? "Демоверсия истекает через: "
                        + (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDays - IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.ExpirationDays_Current)
                        + " дней.\r\n\r\n" : ""
                        + (IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.Executions_Enabled ? "Вы использовали " + IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.Executions_Current + " из "
                        + IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.Executions + " запусков, по "
                        + IntelliLock.Licensing.EvaluationMonitor.CurrentLicense.Runtime + " минут." : ""));
            }
            #endregion            
            #region Старт в Трэй?
            Bot.MeInTray =  Environment.GetCommandLineArgs().Contains("-tray");
            #endregion

            /// <summary>
            /// Любая навигация разрешена только после этой инициализации, иначе IProtectFocus не будет работать!
            /// </summary>            
            #region Proxy Authentification + IProtectFocus
            clsWBEx.IOleObject MainIOleObj = ctrMainBrowser.ActiveXInstance as clsWBEx.IOleObject;
            MainIOleObj.SetClientSite(this as clsWBEx.IOleClientSite);
            ctrMainBrowser.Navigate("about:blank");

            #region Задержка при старте
            //Неизвестно почему но иногда нужно для нормальной загрузки WebControl
            DateTime StartUp = DateTime.Now.AddSeconds(2);
            while (DateTime.Now < StartUp)
            {
                Application.DoEvents();
            }
            #endregion

            clsWBEx.IOleObject Help1IOleObj = ctrHelpBrowser1.ActiveXInstance as clsWBEx.IOleObject;
            Help1IOleObj.SetClientSite(this as clsWBEx.IOleClientSite);
            ctrHelpBrowser1.Navigate("about:blank");
            
            clsWBEx.IOleObject Help2IOleObj = ctrHelpBrowser2.ActiveXInstance as clsWBEx.IOleObject;
            Help2IOleObj.SetClientSite(this as clsWBEx.IOleClientSite);
            ctrHelpBrowser2.Navigate("about:blank");
            #endregion
            #region Отключение картинок в вспомогательных бровзерах
            SetWBDownloadManager(ctrHelpBrowser1, false, false);
            SetWBDownloadManager(ctrHelpBrowser2, false, false);
            #endregion
            #region NavigateError + DownloadComplete
            //Response codes: http://msdn.microsoft.com/en-us/library/aa384325(v=vs.85).aspx
            ((SHDocVw.WebBrowser)ctrMainBrowser.ActiveXInstance).NavigateError += new SHDocVw.DWebBrowserEvents2_NavigateErrorEventHandler(ctrMainBrowser_NavigateError);
            //((SHDocVw.WebBrowser)ctrHelpBrowser1.ActiveXInstance).NavigateError += new SHDocVw.DWebBrowserEvents2_NavigateErrorEventHandler(ctrHelpBrowser1_NavigateError);
            //((SHDocVw.WebBrowser)ctrHelpBrowser2.ActiveXInstance).NavigateError += new SHDocVw.DWebBrowserEvents2_NavigateErrorEventHandler(ctrHelpBrowser2_NavigateError);
            ((SHDocVw.WebBrowser)ctrMainBrowser.ActiveXInstance).DownloadComplete += new SHDocVw.DWebBrowserEvents2_DownloadCompleteEventHandler(ctrMainBrowser_DownloadComplete);
            #endregion
            LoadExpertSettings();
            LoadSettings();

            Text = Bot.PrivSettings.BotName;
            TrayIcon.Text = Text;

            #region Дополнительный твикинг ИЕ через реестр
            clsWBEx.EmulateIEMode(ctrMainBrowser, (int)numMaxIEVersion.Value);
            clsWBEx.SetMaxIEConnections(ctrMainBrowser, (int)Bot.Expert.MaxWebSockets);
            clsWBEx.ShutDownIENavigationSound(ctrMainBrowser);
            #endregion

            //#################################### Proxy Authentification test
            //string s = CredentialCache.DefaultNetworkCredentials.UserName + CredentialCache.DefaultNetworkCredentials.Password;
            //string credentialBase64String = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes("proxyUser:proxyPassword"));
            //string Headers = string.Format("Proxy-Authorization: Basic {0}{1}", credentialBase64String, Environment.NewLine);
            //frmMain.NavigateURL(ctrMainBrowser, (string)cboxServer.SelectedItem, null, null, Headers); 
            //####################################                    

        }

        private void frmMain_Shown(object sender, EventArgs e) //Проверка входных параметров.
        {
            #region Эксперт-настройки групповых боёв
            if (!clsMTask.Load(lstExpGrpFightPlaner, cboxExpGrpFightPlanerEvent, cboxExpGrpFightPlanerType, cboxExpGrpFightPlanerArt, ref Bot.GrpFightTaskManager, "GrpFight.planer")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай эксперт-настройки групповых боёв, пожалуйста!");
            #endregion
            String[] Args = Environment.GetCommandLineArgs();
            foreach (string Arg in Args)
            {
                switch (Arg)
                {
                    case "-recovery":
                        if (File.Exists("Recovery.xml"))
                        {
                            clsAppRestartManager.LoadRecovery(ref Bot.Me);
                            File.Delete("Recovery.xml");                            
                        }
                        goto case "-start";
                    case "-start":  btnStart_Click(null, EventArgs.Empty); break;
                    case "-hide": Hide(); break;
                    case "-tray": break; //Обрабатывается в загрузке формы, ибо здесь уже слишком поздно!
                    case "-doping":
                        if (!Bot.Settings.RestartDoping) //Если есть эта галочка, допинги уже были загруженны
                        {
                            #region Загрука ленинопроводных допингов, и сохранение для последующей визуализации
                            if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrOilLeninDoping, "OilLenin.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай ленино-допинг, пожалуйста!");
                            DopingStatus.OilLeninDoping = new clsDoping[lstDoping.Items.Count];
                            lstDoping.Items.CopyTo(DopingStatus.OilLeninDoping, 0);
                            #endregion
                            #region Загрука крысиных допингов, и сохранение для последующей визуализации
                            if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrRatDoping, "Rat.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай крысо-допинг, пожалуйста!");
                            DopingStatus.RatDoping = new clsDoping[lstDoping.Items.Count];
                            lstDoping.Items.CopyTo(DopingStatus.RatDoping, 0);
                            #endregion
                            #region Загрузка нормальных допингов и визуализация
                            if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrUsualDoping, "Usual.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай обыденный-допинг, пожалуйста!");
                            cboxDopingArt.SelectedIndex = 0; //Последний видимый список допингов, был нормальный список 
                            DopingStatus.LastDopingArt = 0; //Последний видимый список допингов, был нормальный список                            
                            #endregion                             
                        }
                        break;
                    case "-debug":
                        chkDebugMode.Checked = true;
                        break;
                }
            }            
            #region Навигация
            frmMain.NavigateURL(ctrMainBrowser, (string)cboxServer.SelectedItem);
            #endregion
            #region CheckForUpdate
            if (Bot.Settings.CheckForUpdate)
                CheckForUpdate();
            #endregion
        }
        private void frmBrowser_FormClosing(object sender, FormClosingEventArgs e)
        {
            STimer.Stop();
            if (BotThread.IsAlive) BotThread.Abort();
            if (Bot.HCThread[0].IsAlive) Bot.HCThread[0].Abort();
            if (Bot.HCThread[1].IsAlive) Bot.HCThread[1].Abort();
        }

        private void STimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            #region Проверка наличия обновлений
            if (Bot.Settings.CheckForUpdate && LastUpdateCheckDT < DateTime.Now) CheckForUpdate();
            #endregion
            #region Круговое верчение сообшений
            if (LastMessageUpdateDT < DateTime.Now)
            {
                string[] arrString = lblUserMessage.Text.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                if (arrString.Length >= 2) //Есть вообще что крутить? (2 и более строки)
                {
                    string sRet = string.Empty;
                    for (int i = 1; i < arrString.Count(); i++)
                    {
                        sRet += arrString[i] + Environment.NewLine + Environment.NewLine;
                    }
                    sRet += arrString[0];
                    lblUserMessage.Text = sRet;
                }                                
                LastMessageUpdateDT = DateTime.Now.AddSeconds(10);
            }
            #endregion
            lblUserMessage.BackColor = lblUserMessage.Text.StartsWith(" ") ? Color.Red : Color.Black; //Начинается пробелом, выводим сообщение красным!
            if (lblUserMessage.Text != string.Empty) AutoApdateMessage.Visible = true;

            if (LastCheckDT < DateTime.Now) 
            {                
                Tip.SetToolTip(statusStrip,
                    ("Я: " + Bot.Me.Player.Name + " [" + Bot.Me.Player.Level + "]" + " - " + Bot.Me.Player.Fraction + (Bot.Me.Clan.Name != null ? " из клана: " + Bot.Me.Clan.Name : "") + "\n\n")
                    + ("Провёл драк за последние 24 часа: " + (Bot.Me.ArrDuelsDT == null ? 0 : Bot.Me.ArrDuelsDT.Count<DateTime>()) + "\n\n")
                    + (!Bot.Me.Major.LastDT.Equals(new DateTime()) ? "Я мажор до: " + Bot.Me.Major.LastDT + "\n\n" : "")
                    + (Bot.Settings.UseAgent ? "Агенты:\nОкончание лицензии: " + Bot.Me.AgentHunting.StartDT + "\nСброс поражений: " + Bot.Me.AgentHunting.LastDT.AddDays(1).Date + "\nПоражений: " + Bot.Me.AgentHunting.Val + "\nСтоп: " + Bot.Me.AgentHunting.Stop + "\n\n" : "")
                    + (Bot.Settings.UseWerewolf ? "Оборотень в погонах:\nОкончание лицензии: " + Bot.Me.WerewolfHunting.StartDT + "\nСброс поражений: " + Bot.Me.WerewolfHunting.LastDT.AddDays(1).Date + "\nПоражений: " + Bot.Me.WerewolfHunting.Val + "\nСтоп: " + Bot.Me.WerewolfHunting.Stop + "\n\n" : "")
                    + (Bot.Settings.SearchRat ? "Крысопровод:\nУровень: " + Bot.Me.RatHunting.Lvl + "\nСледующий поход в: " + Bot.Me.RatHunting.NextDT + "\nОбвал в: " + Bot.Me.RatHunting.RestartDT + "\nПоражений: " + Bot.Me.RatHunting.Defeats + "\nСтоп: " + Bot.Me.RatHunting.Stop + "\n\n" : "")
                    + (Bot.Settings.GoOilLenin ? "Нефтепровод:\nУровень: " + Bot.Me.OilLeninHunting.Lvl + "\nСледующий поход в: " + Bot.Me.OilLeninHunting.NextDT + "\nОбвал в: " + Bot.Me.OilLeninHunting.RestartDT + "\nПоражений: " + Bot.Me.OilLeninHunting.Defeats + "\nСтоп: " + Bot.Me.OilLeninHunting.Stop + "\n\n" : "")
                    + (Bot.Settings.GoOil ? "Старый нефтепровод:\nСброс поражений: " + Bot.Me.OilHunting.LastDT.AddDays(1).Date + "\nПоражений: " + Bot.Me.OilHunting.Val + "\nСтоп: " + Bot.Me.OilHunting.Stop + "\n\n" : "")
                    + (Bot.Settings.AttackRat ? "Крысы из копания:\nСброс поражений: " + Bot.Me.Rat.LastDT.AddDays(1).Date + "\nПоражений: " + Bot.Me.Rat.Val + "\nСтоп: " + Bot.Me.Rat.Stop + "\n\n" : "")
                    + (Bot.Me.ClanWarInfo.Now ? "Кланвар:\nЯ участвую: " + (Bot.Me.ClanWarInfo.WarStep > 0) + "\nЭтап: " + Bot.Me.ClanWarInfo.WarStep + (Bot.Me.ClanWarInfo.Pacifism != null ? "\nПацифизм: " + Bot.Me.ClanWarInfo.Pacifism[0].Start + "-" + Bot.Me.ClanWarInfo.Pacifism[0].Stop + " и " + Bot.Me.ClanWarInfo.Pacifism[1].Start + "-" + Bot.Me.ClanWarInfo.Pacifism[1].Stop : "") + (Bot.Me.ClanWarInfo.WarStep == 2 ? "\nСледующая стенка в: " + Bot.Me.ClanWarInfo.NextDT : "") + "\n\n" : "")
                    + (Bot.Settings.GoGroupFightChaos || Bot.Settings.GoGroupFightOre || Bot.Settings.GoClanFight ? "Стенки:\nСледующая планируемая: " + Bot.GrpFight.NextFightType + "\nСтарт в: " + Bot.GrpFight.NextFightDT + "\n\n" : "")
                    + (Bot.Settings.Quest ? "Квесты:\nСтоп: " + Bot.Me.Events.StopQuest + "\n\n" : "")
                    + (Bot.Settings.GoPatrol ? "Патруль:\nПоследняя проверка: " + Bot.Me.Patrol.LastDT + "\nСтоп: " + Bot.Me.Patrol.Stop + "\n\n" : "")
                    + (Bot.Settings.GoMC ? "Шаурбургерс:\nПоследняя проверка: " + Bot.Me.MC.LastDT + "\nСтоп: " + Bot.Me.MC.Stop + "\n\n" : "")
                    + (Bot.Me.Trauma.Stop ? "Травма:\nСпадёт в: " + Bot.Me.Trauma.LastDT + "\n\n" : "")
                    + (Bot.Settings.GetMetroWarPrize ? "Метровар:\nПроверка доступности жетонов: " + Bot.Me.MetroWarPrizeDT + "\n\n" : "")
                    + "Данные считаны: " + LastCheckDT
                    );
                LastCheckDT = DateTime.Now.AddSeconds(30);
                Bot.RestartBotInstance();
            }
            if (BotThread.ThreadState == System.Threading.ThreadState.Stopped) //Бот споткнулся
            {
                Bot.UpdateStatus("! " + DateTime.Now + " Меня уронили, я споткнулся?!? Почему я на полу?!? -Включаю источник дополнительной энергии!");
                BotThread = new Thread(new ThreadStart(Bot.StartBot));
                BotThread.Name = "MainBotThread";
                BotThread.Start();
            }
            #region Shutdown
            if (Bot.Me.Events.ShutdownRelease && Bot.Me.Events.ShutdownDT <= DateTime.Now)
            {
                switch (BotThread.Name)
                {
                    case "Shutdown":
                        #region Прошло более 30 минут со времени старта функции Shutdown?
                        if (Bot.Me.Events.ShutdownDT.AddMinutes(30) <= DateTime.Now) //Прошло более 30 минут со времени старта функции Shutdown, что то пошло не так вырубаем комп.
                        {
                            clsExitWindows.ShutDown();
                            Bot.Me.Events.ShutdownRelease = false;
                        }
                        break;
                        #endregion                        
                    default:
                        #region Время вырубать комп?
                        switch (BotThread.ThreadState)
                        {
                            case System.Threading.ThreadState.Unstarted:
                            case (System.Threading.ThreadState)32: //Running не получается использовать напрямую ибо тут используется Битмаска
                                BotThread.Abort();
                                break;
                            case System.Threading.ThreadState.Aborted:
                                BotThread = new Thread(new ThreadStart(Bot.Shutdown));
                                BotThread.Name = "Shutdown";
                                BotThread.Start();
                                break;
                        }
                        break;
                        #endregion
                }                
            }
            #endregion            
            #region Спрятать/показать бота
            if (Keyboard.IsKeyDown(Key.RightCtrl) && Keyboard.IsKeyDown(Key.RightShift))
            {
                if (Keyboard.IsKeyDown(Key.H)) this.Visible = false;
                if (Keyboard.IsKeyDown(Key.S)) this.Visible = true;
            }
            #endregion
        }

        private void CheckForUpdate()
        {
            try
            {
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create("http://moswarbro.moy.su/version.txt");
                WebProxy webProxy = !Bot.Settings.UseProxy ? null :
                    new WebProxy(Regex.Replace(Bot.PrivSettings.Proxy, "( |^0|(?<=[.])0{1,2})|(?<=:)0{1,4}", ""), true) { UseDefaultCredentials = false, Credentials = new NetworkCredential(Bot.PrivSettings.ProxyUserName, Bot.PrivSettings.ProxyPassword) };
                httpRequest.Proxy = webProxy;
                httpRequest.Timeout = 30000;     // 30 secs
                httpRequest.KeepAlive = false;
                httpRequest.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)"; //(string)GetJavaVar(ctrMainBrowser, "navigator['userAgent']");
                HttpWebResponse webResponse = (HttpWebResponse)httpRequest.GetResponse();
                string CurVersion = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                FileVersionInfo FVI = FileVersionInfo.GetVersionInfo(Application.ExecutablePath);
                if (FVI.ProductVersion != CurVersion)
                {
                    AddLabelText(lblUserMessage, "Внимание! Доступна новая версия moswarBro: " + CurVersion);
                    AutoApdateMessage.Visible = true;
                    #region Tray
                    if (TrayIcon.Visible)
                    {
                        TrayIcon.BalloonTipIcon = ToolTipIcon.Info;
                        TrayIcon.BalloonTipTitle = "Семейное обновление:";
                        TrayIcon.BalloonTipText = "Новый Братишка " + CurVersion + ", ждёт тебя!";
                        TrayIcon.ShowBalloonTip(30000);
                    }
                    #endregion
                }
                else
                {
                    if (FVI.ProductVersion != FVI.FileVersion)
                    {
                        AddLabelText(lblUserMessage, "Ёлки палки ... да у тебя \"Beta-версия\", ты где её упер/ла то? =)");
                        AutoApdateMessage.Visible = true;
                    }
                }                          
            }
            catch
            {
                AddLabelText(lblUserMessage, " Внимание! Невозможно определить последнюю версию=("); //Начинается пробелом, выводим сообщение красным!
                AutoApdateMessage.Visible = true;
            }         
            LastUpdateCheckDT = DateTime.Now.AddHours(3);            
        }

        private void btnVictimHunting_Click(object sender, EventArgs e)
        {
            switch (btnVictimHunting.Text)
            {
                case "Поскакали!":
                    VictimHunting = true;
                    Bot.GoToPlace(Bot.HelpWB[0], clsBot.Place.Alley);
                    Bot.GoToPlace(Bot.HelpWB[1], clsBot.Place.Alley);

                    Bot.HCThread[0] = new Thread(new ThreadStart(StartMultiThread));
                    Bot.HCThread[1] = new Thread(new ThreadStart(StartMultiThread));
                    Bot.HCThread[0].Start();
                    Bot.HCThread[1].Start();

                    btnVictimHunting.Text = "Бррррр ... Стоп!";
                    do
                    {
                        if (NeedHeal) Bot.CheckHealthEx(99, Bot.Settings.HealMe100, Bot.Settings.HealPet50, Bot.Settings.HealPet100);
                        Application.DoEvents();
                    }
                    while ((Bot.HCThread[0].IsAlive && txtVictimURL1.Text != "") || (Bot.HCThread[1].IsAlive && txtVictimURL2.Text != ""));
                    VictimHunting = false;
                    btnVictimHunting.Text = "Поскакали!";
                    break;
                case "Бррррр ... Стоп!":
                    VictimHunting = false;
                    btnVictimHunting.Text = "Поскакали!";
                    break;
            }
        }
        private void StartMultiThread()
        {
            do
            {
                #region 1-th Victim
                if (Thread.CurrentThread.Equals(Bot.HCThread[0]))
                {
                    if (GetTextBoxText(txtVictimURL1) == "") Thread.Sleep(1000);
                    else
                    {
                        if (!MultithreadAttack(GetTextBoxText(txtVictimURL1))) frmMain.AddLabelText(lblTimeVictim1, DateTime.Now.ToString()); //Нашёл убитым?
                        return;
                    }
                }
                #endregion
                #region 2-th Victim
                if (Thread.CurrentThread.Equals(Bot.HCThread[1]))
                {
                    if (GetTextBoxText(txtVictimURL2) == "") Thread.Sleep(1000);
                    else
                    {
                        if (!MultithreadAttack(GetTextBoxText(txtVictimURL2))) frmMain.AddLabelText(lblTimeVictim2, DateTime.Now.ToString()); //Нашёл убитым?
                        return;
                    }
                }
                #endregion
            }
            while (true);
        }
        private bool MultithreadAttack(string URL)
        {
            Bot.BugReport("MultithreadAttack");

            Regex regex;
            Match match;
            WebBrowser WB = null;
            RadioButton RB = null;

            #region 1-th Victim
            if (Thread.CurrentThread.Equals(Bot.HCThread[0]))
            {
                WB = ctrHelpBrowser1;
                RB = rbAttackVictim1;
            }
            #endregion
            #region 2-th Victim
            if (Thread.CurrentThread.Equals(Bot.HCThread[1]))
            {
                WB = ctrHelpBrowser2;
                RB = rbAttackVictim2;
            }
            #endregion
            if (WB == null) return false; //Функция запущена не из вспомогательного потока, уходим!

        ReTry:
            if (!VictimHunting) return true;
            NeedHeal = Bot.IsHPLow(WB, 99, false); //Слишком мало жизней для драк, прервать для лечения            
            regex = new Regex(URL); //
            if (!regex.IsMatch(frmMain.GetDocumentURL(WB)))
            {
                #region Открытие странички
                frmMain.NavigateURL(WB, URL);
                Bot.IsWBComplete(WB);
                #endregion
            }
            if (regex.IsMatch(frmMain.GetDocumentURL(WB)))
            {
                string[] LifePkt = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#pers-player-info .life\").text()"), "(?<=Жизни:)([0-9 /\\s])+").Value.Split('/');
                if (Convert.ToDouble(LifePkt[0]) < Convert.ToDouble(LifePkt[1]) / 100) return false; //Нашёл убитым?                
                #region Достаточно ли у игрока жизней для нападения?
                if (Convert.ToDouble(LifePkt[0]) / Convert.ToDouble(LifePkt[1]) * 100 < 35)
                {
                    Thread.Sleep(1000);
                }
                else frmMain.NavigateURL(WB, RB.Checked && !NeedHeal ? URL.Replace("player", "alley/attack") : URL);
                #endregion
            
            TimeMashine:
                Bot.IsWBComplete(WB);
                #region Аттака проведена успешно?
                regex = new Regex("(?<Fight>/alley/fight/([0-9])+/)|(?<Quest>/quest/)|(?<Login>" + Bot.Settings.ServerURL + "/$)|(?<Player>" + URL +")");
                match = regex.Match(frmMain.GetDocumentURL(WB));
                if (RB.Checked ? match.Groups["Player"].Success : false) //Попытка, клацнуть машинкой времени
                {
                    if (Regex.IsMatch(frmMain.GetDocument(WB).GetElementById("content").InnerText, "Воспользоваться")) //Похоже у меня есть машина времени, и стоит ей воспользоваться
                    {
                        frmMain.InvokeScript(WB, "alleyAttack", new object[4] { Regex.Match(URL, "([0-9])+").Value, "1", "0", "1" }); //Запуск скрипта атаки в алее с передачей данных.
                        goto TimeMashine;
                    }
                }
                if (match.Groups["Fight"].Success) //Битва проведена, стоп!
                {
                    Bot.AnalyseFight(WB); //побили успешно!                
                    return true; //
                }
                if (match.Groups["Login"].Success) 
                    Bot.WebLogin(WB); //
                #endregion
            }
            goto ReTry; //Лупасить, пока не обнаружим слишком мало жизней или удачное нападение!
        }         

        private void lstAny_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground(); //Прорисовывание заднего фона
            Brush itemBrush;

            if (e.Index == -1) return;

            switch (((ListBox)sender).Items[e.Index].ToString()[0]) //Выбор цвета
            {
                case '#': itemBrush = Brushes.Black; break;
                case '$':
                case '*': itemBrush = Brushes.DarkGreen; break;
                case '~': itemBrush = Brushes.DarkOrchid; break;
                case '!': itemBrush = Brushes.Red; break;
                case '-':
                case '?': itemBrush = Brushes.Orange; break;
                case '@': itemBrush = Brushes.Brown; break;
                case '©': itemBrush = Brushes.Blue; break;
                default: itemBrush = Brushes.Black; break;
            }
            e.Graphics.DrawString(((ListBox)sender).Items[e.Index].ToString(), e.Font, itemBrush, e.Bounds, StringFormat.GenericDefault);
            // e.DrawFocusRectangle();
        }
        
        private void Browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {            
            try
            {
                if (e.Url == (IsMultiFrame((WebBrowser)sender) ? ((WebBrowser)sender).Document.Window.Frames[0].Url : ((WebBrowser)sender).Url)) 
                {
                    if (Bot.DebugMode) Bot.BugReport("* Document Completed");
                    if (!((WebBrowser)sender).Tag.Equals("Error")) 
                    {
                        if (e.Url != new Uri("about:blank")) Bot.SetAjaxTrap((WebBrowser)sender); //ставим ловушку для ловления Ajax
                        ((WebBrowser)sender).Tag = "Ready";
                    }
                }
            }                
            catch (System.UnauthorizedAccessException) //Закидывает сюда, если вдруг происходит кросс скриптинг (меняется домен! например при варке коктейлей!)
            {
                Bot.BugReport("CrossDomainError");
                ((WebBrowser)sender).Document.Window.Frames[0].Navigate("http://" + Bot.Settings.ServerURL); //Режим с чатом: Для загрузки через фрэйм нужны ссылки типа: http:\\/alley/ 
            }
        }
        private void Browser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            //((WebBrowser)sender).Tag = "Navigated";
        }
        private void ctrMainBrowser_NavigateError(object pDisp, ref object URL, ref object Frame, ref object StatusCode, ref bool Cancel)
        {
           // int RespCode = Convert.ToInt32(StatusCode); //504 - Vse slomalos'
            Bot.UpdateStatus("! " + DateTime.Now + " Внимание ошибка загрузки №: " + StatusCode);
            ctrMainBrowser.Tag = "Error";
        }
        private void ctrMainBrowser_DownloadComplete()
        {            
            if (ctrMainBrowser.Tag.Equals("Loading") || ctrMainBrowser.Tag.Equals("Ajax"))
            {
                ctrMainBrowser.Tag = "Ready";
                if (Bot.DebugMode) Bot.BugReport("* Download Completed");                
            }
        }

        private void btnSaveSet_Click(object sender, EventArgs e)
        {
            bool NeedRestart = btnStart.Text == "Завершить погром!"; //Запоминаем был ли запущен бот, при сохранении
            if (NeedRestart) btnStart_Click(btnStart, EventArgs.Empty); //Останавливаем бота.
            Bot.IsWBComplete(ctrMainBrowser);
            Bot.GoToPlace(ctrMainBrowser, clsBot.Place.Player);
            clsWearSets.Save(ctrMainBrowser, ref Bot.ArrWearSet, cboxWearSet.SelectedIndex, "Set.dat");
            Bot.UpdateStatus("- " + DateTime.Now + " Настройки сетов удачно сохранены.");                        
            if (NeedRestart) btnStart_Click(btnStart, EventArgs.Empty); //Перезапускаем бота.
        }
        private void btnLoadSet_Click(object sender, EventArgs e)
        {
            bool NeedRestart = btnStart.Text == "Завершить погром!"; //Запоминаем был ли запущен бот, при сохранении
            if (NeedRestart) btnStart_Click(btnStart, EventArgs.Empty); //Останавливаем бота.
            if (clsWearSets.Load(ref Bot.ArrWearSet, "Set.dat"))
            {
                Bot.UpdateStatus("- " + DateTime.Now + " Сеты удачно загружены.");                
                Bot.WearSet(ctrMainBrowser, Bot.ArrWearSet, cboxWearSet.SelectedIndex);

            }
            else Bot.UpdateStatus("! " + DateTime.Now + " Сеты не найдены!");          
            if (NeedRestart) btnStart_Click(btnStart, EventArgs.Empty); //Перезапускаем бота.
        }

        #region Doping
        private void DopingArt_Changed(object sender, EventArgs e)
        {
            #region Запоминание текущего состояния списка допингов
            switch (DopingStatus.LastDopingArt)
            {
                case 0:
                    DopingStatus.NormalDoping = new clsDoping[lstDoping.Items.Count];
                    lstDoping.Items.CopyTo(DopingStatus.NormalDoping, 0);
                    break;
                case 1:
                    DopingStatus.RatDoping = new clsDoping[lstDoping.Items.Count];
                    lstDoping.Items.CopyTo(DopingStatus.RatDoping, 0);
                    break;
                case 2:
                    DopingStatus.OilLeninDoping = new clsDoping[lstDoping.Items.Count];
                    lstDoping.Items.CopyTo(DopingStatus.OilLeninDoping, 0);
                    break;
            }
            #endregion
            lstDoping.Items.Clear();
            cboxDopingEvent.Items.Clear();
            if (DopingStatus.LastDopingArt == 2) 
            {
                numRatSyncLvlDoping.Visible = false;
                cboxDopingType.Items.RemoveAt(1);
                cboxDopingType.Items.RemoveAt(0);
                cboxDopingType.Width = 220;
                cboxDopingType.Refresh();
            } 
            #region Востановление текущего состояния списка допингов
            switch (((ComboBox)sender).SelectedIndex)
            {
                case 0:             
                    if (DopingStatus.NormalDoping != null) lstDoping.Items.AddRange(DopingStatus.NormalDoping);
                    cboxDopingEvent.Items.Insert(0, "Применить в:");
                    cboxDopingEvent.Items.Insert(1, "Противостояние");
                    cboxDopingEvent.Items.Insert(2, "Стоппер: Крысиный [Копание]");
                    cboxDopingEvent.Items.Insert(3, "Стоппер: Нефтянной [Миша 2%]");
                    cboxDopingEvent.Items.Insert(4, "Стоппер: Агентный");
                    cboxDopingEvent.Items.Insert(5, "Старт: ОК");                    
                    cboxDopingEvent.Items.Insert(6, "Всегда");
                    DopingStatus.LastDopingArt = 0;                    
                    break;
                case 1:
                    if (DopingStatus.RatDoping != null) lstDoping.Items.AddRange(DopingStatus.RatDoping);
                    cboxDopingEvent.Items.Insert(0, "Применить перед крысой:");                    
                    DopingStatus.LastDopingArt = 1;
                    break;
                case 2:
                    if (DopingStatus.OilLeninDoping != null) lstDoping.Items.AddRange(DopingStatus.OilLeninDoping);
                    cboxDopingEvent.Items.Insert(0, "Применить перед вентилем:");
                    cboxDopingType.Items.Insert(0, "Нападать не ранее крысы:");
                    cboxDopingType.Items.Insert(1, "Использовать партбилеты");
                    DopingStatus.LastDopingArt = 2;
                    break;
            }
            #endregion
            
            btnDopingRemove.Enabled = false;
        }
        private void lstDoping_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnDopingRemove.Enabled = true;
        }
        private void cboxDoppingEvent_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboxDopingEvent.SelectedIndex != -1)
            {
                cboxDopingType.SelectedIndex = -1;
                if (cboxDopingEvent.SelectedIndex == 0) //Специальные ивенты
                {
                    switch (cboxDopingArt.SelectedIndex)
                    {
                        case 0:  //Применить в:
                            dtDoping.Visible = true;
                            numLvlDoping.Visible = false;
                            cboxDopingEvent.Width = 165;
                            break;
                        case 1: //Применить перед крысой:                            
                        case 2: //Применить перед вентилем:
                           dtDoping.Visible = false;
                           numLvlDoping.Visible = true;
                           cboxDopingEvent.Width = 175;
                           break;
                    }
                }
                else
                {
                    dtDoping.Visible = false;
                    numLvlDoping.Visible = false;
                    cboxDopingEvent.Width = 220;
                }         
                cboxDopingEvent.Refresh();                
            }
        }
        private void cboxDoppingType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboxDopingType.SelectedIndex != -1)
            {
                dtDoping.Visible = false;
                numLvlDoping.Visible = false;
                cboxDopingEvent.Width = 220;                
                cboxDopingEvent.SelectedIndex = -1;
                cboxDopingEvent.Refresh();
                if (cboxDopingArt.SelectedIndex == 2 && cboxDopingType.SelectedIndex == 0)
                {
                    cboxDopingType.Width = 175;
                    numRatSyncLvlDoping.Visible = true;
                }
                else
                {
                    cboxDopingType.Width = 220;
                    numRatSyncLvlDoping.Visible = false;
                }
                cboxDopingType.Refresh();
            }
        }
        private void btnDoppingAdd_Click(object sender, EventArgs e)
        {
            int Index = lstDoping.SelectedIndex == -1 ? lstDoping.Items.Count : lstDoping.SelectedIndex; //Всунуть элемент в конец или перед маркировкой!
            if (cboxDopingEvent.SelectedIndex != -1)
            {                                                                                 //Нападать при определённом уровне доступно
                lstDoping.Items.Insert(Index, new clsDoping(1, cboxDopingEvent.SelectedIndex + (cboxDopingArt.SelectedIndex == 0 ? 1 : 0), "* " + cboxDopingEvent.SelectedItem + (numLvlDoping.Visible || dtDoping.Visible ? " " + (numLvlDoping.Visible ? numLvlDoping.Value.ToString() : dtDoping.Value.ToString("HH:mm")) : "")));
            }
            if (cboxDopingType.SelectedIndex != -1)
            {                                                                                   //Уровень синхронизации крыс доступен
                lstDoping.Items.Insert(Index, new clsDoping(2, cboxDopingType.SelectedIndex + (cboxDopingArt.SelectedIndex == 2 ? 0 : 2), numRatSyncLvlDoping.Visible ? "~ " + cboxDopingType.SelectedItem + " " + numRatSyncLvlDoping.Value : "- " + cboxDopingType.SelectedItem));
            }
            lstDoping.SelectedIndex = -1;
        }
        private void btnDoppingRemove_Click(object sender, EventArgs e)
        {
            switch (cboxDopingArt.SelectedIndex)
            { 
                case 0:
                    clsDoping.Remove(lstDoping, ref Bot.Me.ArrUsualDoping);
                    break;
                case 1:
                    clsDoping.Remove(lstDoping, ref Bot.Me.ArrRatDoping);
                    break;
                case 2:
                    clsDoping.Remove(lstDoping, ref Bot.Me.ArrOilLeninDoping);
                    break;
            }          
            btnDopingRemove.Enabled = false;
        }
        private void btnDoppingSave_Click(object sender, EventArgs e)
        {            
            switch (cboxDopingArt.SelectedIndex)
            { 
                case 0:
                    clsDoping.Save(lstDoping, ref Bot.Me.ArrUsualDoping, "Usual.doping");
                    break;
                case 1:
                    clsDoping.Save(lstDoping, ref Bot.Me.ArrRatDoping, "Rat.doping");
                    break;
                case 2:
                    clsDoping.Save(lstDoping, ref Bot.Me.ArrOilLeninDoping, "OilLenin.doping");
                    break;
            }   
        }
        private void btnDopingLoad_Click(object sender, EventArgs e)
        {
            switch (cboxDopingArt.SelectedIndex)
            { 
                case 0:
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrUsualDoping, "Usual.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай обыденный-допинг, пожалуйста!");;
                    break;
                case 1:
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrRatDoping, "Rat.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай крысо-допинг, пожалуйста!");;
                    break;
                case 2:
                    if (!clsDoping.Load(lstDoping, cboxDopingEvent, cboxDopingType, cboxDopingArt, ref Bot.Me.ArrOilLeninDoping, "OilLenin.doping")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай ленино-допинг, пожалуйста!");;
                    break;            
            }
        }
        #endregion

        #region GrpTaskManager
        private void cboxExpGrpFightPlanerArt_SelectedIndexChanged(object sender, EventArgs e)
        {
            for (int i = 0; i <= cboxExpGrpFightPlanerArt.SelectedIndex; i++)
            {
                if (TaskManagerStatus.TaskManager == null) TaskManagerStatus.TaskManager = new clsTaskManager();
                if (TaskManagerStatus.TaskManager.Arts == null || TaskManagerStatus.TaskManager.Arts.Count() <= i)
                {
                    Array.Resize(ref TaskManagerStatus.TaskManager.Arts, TaskManagerStatus.TaskManager.Arts == null ? 1 : TaskManagerStatus.TaskManager.Arts.Count() + 1);
                    TaskManagerStatus.TaskManager.Arts[TaskManagerStatus.TaskManager.Arts.Count() - 1] = new clsTManagerArt { Tasks = new clsMTask[0] };
                }                    
            }
            TaskManagerStatus.TaskManager.Arts[TaskManagerStatus.LastTaskManagerArt] = new clsTManagerArt { Tasks = new clsMTask[lstExpGrpFightPlaner.Items.Count] };
            lstExpGrpFightPlaner.Items.CopyTo(TaskManagerStatus.TaskManager.Arts[TaskManagerStatus.LastTaskManagerArt].Tasks, 0);
            if (cboxExpGrpFightPlanerArt.SelectedIndex != TaskManagerStatus.LastTaskManagerArt)
            {
                lstExpGrpFightPlaner.Items.Clear();
                TaskManagerStatus.LastTaskManagerArt = cboxExpGrpFightPlanerArt.SelectedIndex;
                if (TaskManagerStatus.TaskManager.Arts[TaskManagerStatus.LastTaskManagerArt].Tasks.Count() == 0)
                {
                    if (!clsMTask.Load(lstExpGrpFightPlaner, cboxExpGrpFightPlanerEvent, cboxExpGrpFightPlanerType, cboxExpGrpFightPlanerArt, ref Bot.GrpFightTaskManager, "GrpFight.planer")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай обыденный-допинг, пожалуйста!");
                }
                else lstExpGrpFightPlaner.Items.AddRange(TaskManagerStatus.TaskManager.Arts[TaskManagerStatus.LastTaskManagerArt].Tasks);
            }        
        }
        private void lstExpGrpFightPlaner_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnExpGrpFightPlanerRemove.Enabled = true;
        } 
        private void cboxExpGrpFightPlanerType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboxExpGrpFightPlanerType.SelectedIndex != -1) 
            {
                cboxExpGrpFightPlanerEvent.SelectedIndex = -1;
                dtExpGrpFightStart.Visible = true;
                dtExpGrpFightStop.Visible = true;
            }
        }        
        private void cboxExpGrpFightPlanerEvent_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboxExpGrpFightPlanerEvent.SelectedIndex != -1) 
            {
                cboxExpGrpFightPlanerType.SelectedIndex = -1;
                dtExpGrpFightStart.Visible = cboxExpGrpFightPlanerEvent.SelectedIndex == 1;
                dtExpGrpFightStop.Visible = cboxExpGrpFightPlanerEvent.SelectedIndex == 1;
            } 
        }

        private void btnExpGrpFightPlanerAdd_Click(object sender, EventArgs e)
        {
            int Index = lstExpGrpFightPlaner.SelectedIndex == -1 ? lstExpGrpFightPlaner.Items.Count : lstExpGrpFightPlaner.SelectedIndex; //Всунуть элемент в конец или перед маркировкой!
            if (cboxExpGrpFightPlanerType.SelectedIndex != -1)
            {                                                                       //Неделя не начинается с воскресенья, посему нужно это манипулирование
                lstExpGrpFightPlaner.Items.Insert(Index, new clsMTask(1, cboxExpGrpFightPlanerType.SelectedIndex == 6 ? 0 : cboxExpGrpFightPlanerType.SelectedIndex + 1, "* " + cboxExpGrpFightPlanerType.SelectedItem));               
            }
            if (cboxExpGrpFightPlanerEvent.SelectedIndex != -1)
            {                                                                                   
                lstExpGrpFightPlaner.Items.Insert(Index, new clsMTask(2, cboxExpGrpFightPlanerEvent.SelectedIndex == 0 ? 0 : 1, "- " + (cboxExpGrpFightPlanerEvent.SelectedIndex == 0 ? cboxExpGrpFightPlanerEvent.SelectedItem : "Участвовать с " + dtExpGrpFightStart.Value.ToString("HH:mm") + " до " + dtExpGrpFightStop.Value.ToString("HH:mm"))));
            }
            lstExpGrpFightPlaner.SelectedIndex = -1;
        }
        private void btnExpGrpFightPlanerRemove_Click(object sender, EventArgs e)
        {
            clsMTask.Remove(lstExpGrpFightPlaner, cboxExpGrpFightPlanerArt, ref Bot.GrpFightTaskManager);
        }
        private void btnExpGrpFightPlanerSave_Click(object sender, EventArgs e)
        {
            clsMTask.Save(lstExpGrpFightPlaner, cboxExpGrpFightPlanerArt, ref Bot.GrpFightTaskManager, "GrpFight.planer");
        }
        private void btnExpGrpFightPlanerLoad_Click(object sender, EventArgs e)
        {
            if (!clsMTask.Load(lstExpGrpFightPlaner, cboxExpGrpFightPlanerEvent, cboxExpGrpFightPlanerType, cboxExpGrpFightPlanerArt, ref Bot.GrpFightTaskManager, "GrpFight.planer")) MessageBox.Show("Шеф, тебе бы врачём работать, шрифт вообше нечитаем! -Переделай обыденный-допинг, пожалуйста!");
        }
        #endregion


        private void chkUseOhara_CheckedChanged(object sender, EventArgs e)
        {
            if (sender.Equals(chkSearchRatUseOhara))
            {
                lblSearchRatMaxDefeats.Enabled = !((CheckBox)sender).Checked;
                numMaxSearchRatDefeats.Enabled = !((CheckBox)sender).Checked;
            }
            if (sender.Equals(chkOilUseOhara))
            {
                lblOilMaxDefeats.Enabled = !((CheckBox)sender).Checked;
                numMaxOilDefeats.Enabled = !((CheckBox)sender).Checked;
            }
        }

        private void cboxSpecialGrpFightUseItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((ComboBox)sender).Equals(cboxRatGrpFightUseItems) && Bot.Settings.UseRatItems != null)
            {
                chkUseItemRatFightLvl5.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 0];
                chkUseItemRatFightLvl10.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 1];
                chkUseItemRatFightLvl15.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 2];
                chkUseItemRatFightLvl20.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 3];
                chkUseItemRatFightLvl25.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 4];
                chkUseItemRatFightLvl30.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 5];
                chkUseItemRatFightLvl35.Checked = Bot.Settings.UseRatItems[cboxRatGrpFightUseItems.SelectedIndex * 7 + 6];
            }
            if (((ComboBox)sender).Equals(cboxOilLeninGrpFightUseItems) && Bot.Settings.UseOilLeninItems != null)
            {
                chkUseItemOilLeninFightLvl8.Checked = Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 0];
                chkUseItemOilLeninFightLvl17.Checked = Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 1];
                chkUseItemOilLeninFightLvl26.Checked = Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 2];
                chkUseItemOilLeninFightLvl29.Checked = Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 3];
                chkUseItemOilLeninFightLvl30.Checked = Bot.Settings.UseOilLeninItems[cboxOilLeninGrpFightUseItems.SelectedIndex * 5 + 4];   
            }         
        }
        private void cboxGrpFight_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Bot.Settings.UseGrpFightItems != null)
            {
                chkGrpFightUseBomb.Checked = Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 0];
                chkGrpFightUseChees.Checked = Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 1];
                chkGrpFightUseHeal.Checked = Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 2];
                chkGrpFightUseOther.Checked = Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 3];
                chkGrpFightUseBear.Checked = Bot.Settings.UseGrpFightItems[cboxGrpFightUseItems.SelectedIndex * 5 + 4];
            }
            if (!(chkGrpFightUseBomb.Enabled =! ((ComboBox)sender).SelectedItem.Equals("Пахан"))) chkGrpFightUseBomb.Checked = false;
        }
        private void cboxExpGrpFightType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Bot.Expert.FightSlotItemTypes != null) 
            {
                cboxExpGrpFightSlot1.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 0];
                cboxExpGrpFightSlot2.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 1];
                cboxExpGrpFightSlot3.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 2];
                cboxExpGrpFightSlot4.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 3];
                cboxExpGrpFightSlot5.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 4];
                cboxExpGrpFightSlot6.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 5];
                cboxExpGrpFightPet.SelectedIndex = Bot.Expert.FightSlotItemTypes[cboxExpGrpFightType.SelectedIndex * 7 + 6];
            }            
        } 

        private void cboxTrainWarPetType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (((ComboBox)sender).SelectedIndex)
            {
                case 10: //Котёнок "Гаф"
                case 11: //Чёрный дог                
                    numMaxTrainPetFocus.Maximum = 700;
                    numMaxTrainPetLoyality.Maximum = 700;
                    numMaxTrainPetMass.Maximum = 700;
                    break;
            default:
                    numMaxTrainPetFocus.Maximum = 500;
                    numMaxTrainPetLoyality.Maximum = 500;
                    numMaxTrainPetMass.Maximum = 500;
                    break;
            }            
        }

        private void cboxPetrikiBonus_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (((ComboBox)sender).SelectedIndex)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                    lblPetrikiBonusPriceSymbol.Visible = false;
                    lblPetrikiBonusPrice.Visible = false;
                    lblPetrikiBonusPrice.Text = "0";
                    break;
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    lblPetrikiBonusPriceSymbol.Visible = true;
                    lblPetrikiBonusPrice.Visible = true;
                    lblPetrikiBonusPrice.Text = "1";
                    break;
                case 10:
                    lblPetrikiBonusPriceSymbol.Visible = true;
                    lblPetrikiBonusPrice.Visible = true;
                    lblPetrikiBonusPrice.Text = "6";
                    break;
            }            
        }

        private void cboxWerewolf_SelectedIndexChanged(object sender, EventArgs e)
        {
            #region Новая цена покупки оборотня
            if (sender.Equals(cboxWerewolfPrice))
            {
                if (cboxWerewolfPrice.SelectedIndex == 0)
                {
                    cboxWerewolfPrice.Width = 70;
                    pboxWerewolfPower.Width = 12;
                    pboxWerewolfPower.Left = 184;
                }
                else
                {
                    cboxWerewolfPrice.Width = 50;
                    pboxWerewolfPower.Width = cboxWerewolfPrice.SelectedIndex * 12;
                    pboxWerewolfPower.Left = 190 - cboxWerewolfPrice.SelectedIndex * 6;
                }                
                cboxWerewolfPrice.Refresh();
                pboxWerewolfPower.Refresh();
            }
            #endregion
            #region Тип нападения, я / оборотень.
            if (sender.Equals(cboxWerewolf))
            { 
                if (cboxWerewolf.SelectedIndex == 1)
                {
                    #region Оборотень
                    lblWerewolf.Visible = true;
                    cboxOpponent.SelectedIndex = (int)Bot.Settings.WerewolfOpponent;

                    numAlleyMinLvl.Minimum = -5;
                    numAlleyMinLvl.Maximum = 5;
                    numAlleyMinLvl.Value = Bot.Settings.minWerewolfLvl;

                    numAlleyMaxLvl.Minimum = -5;
                    numAlleyMaxLvl.Maximum = 5;
                    numAlleyMaxLvl.Value = Bot.Settings.maxWerewolfLvl;

                    chkUseAgent.Checked = false;
                    chkUseAgent.Enabled = false;
                    #endregion
                }
                else
                {
                    #region Персонаж
                    lblWerewolf.Visible = false;
                    cboxOpponent.SelectedIndex = (int)Bot.Settings.AlleyOpponent;

                    numAlleyMinLvl.Minimum = 1;
                    numAlleyMinLvl.Maximum = 50;
                    numAlleyMinLvl.Value = Bot.Settings.minAlleyLvl; 

                    numAlleyMaxLvl.Minimum = 1;
                    numAlleyMaxLvl.Maximum = 50;
                    numAlleyMaxLvl.Value = Bot.Settings.maxAlleyLvl;

                    chkUseAgent.Checked = Bot.Settings.UseAgent;
                    chkUseAgent.Enabled = true;
                    #endregion
                }
            } 
            #endregion
            #region Тип нападения, Аллея / Агенты.
            if (sender.Equals(chkUseAgent))
            {
                if (chkUseAgent.Checked)
                {
                    #region Агенты
                    cboxOpponent.SelectedIndex = (int)Bot.Settings.AgentOpponent;
                    numAlleyMinLvl.Value = Bot.Settings.minAgentLvl;
                    numAlleyMaxLvl.Value = Bot.Settings.maxAgentLvl;

                    chkMrPlushkin.Checked = Bot.Settings.MrPlushkin;
                    chkMrPlushkin.Enabled = true;
                    #endregion
                }
                else
                {
                    #region Персонаж
                    if (cboxWerewolf.SelectedIndex == 0) //Не оборотень!
                    {
                        cboxOpponent.SelectedIndex = (int)Bot.Settings.AlleyOpponent;
                        numAlleyMinLvl.Value = Bot.Settings.minAlleyLvl;
                        numAlleyMaxLvl.Value = Bot.Settings.maxAlleyLvl;

                        chkMrPlushkin.Checked = false;
                        chkMrPlushkin.Enabled = false;
                    }                   
                    #endregion
                }
            }
            #endregion
        }

        private void btnAddToBlackWanted_Click(object sender, EventArgs e)
        {
            int i = lstBlackWanted.Items.Count;
            if (txtAddToBlackWanted.Text != "") lstBlackWanted.Items.Add(i); lstBlackWanted.Items[i] = txtAddToBlackWanted.Text;
        }
        private void btnRemoveBlackWanted_Click(object sender, EventArgs e)
        {
            lstBlackWanted.Items.Clear();
        }

        private void chkAddClan_CheckedChanged(object sender, EventArgs e)
        {
            switch (chkAddClan.Checked)
            {
                case false:
                    chkFarmClan.Enabled = false;
                    chkFarmClan.Checked = false;
                    chkClanWars.Enabled = false;
                    chkClanWars.Checked = false;
                    chkBerserker.Enabled = false;
                    chkBerserker.Checked = false;
                    chkRemoveEnemy.Enabled = false;
                    chkRemoveEnemy.Checked = false;
                    break;
                case true:
                    chkFarmClan.Enabled = true;
                    chkClanWars.Enabled = true;
                    chkBerserker.Enabled = true;
                    chkRemoveEnemy.Enabled = true;
                    break;
            }
        }
        private void lnk_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            e.Link.Visited = true;
            if (sender.Equals(lnkEmail)) System.Diagnostics.Process.Start("mailto:moswar.bot@gmail.com");
            if (sender.Equals(lnkPicUrl)) System.Diagnostics.Process.Start("http://icons.yootheme.com");
            if (sender.Equals(lnkWeb)) System.Diagnostics.Process.Start("http://www.moswarbro.moy.su");
        }
        private void chkDebugMode_CheckedChanged(object sender, EventArgs e)
        {
            Bot.DebugMode = ((CheckBox)sender).Checked;
        }
        private void Controls_Click(object sender, EventArgs e) 
        {
            if (sender.Equals(btnHideAutoUpdateMessage)) { AutoApdateMessage.Visible = false; lblUserMessage.Text = string.Empty; } //Прячем и стираем прошлые уведомления, пользователь уже с ними ознакомлен!
            if (sender.Equals(lblUserMessage)) System.Diagnostics.Process.Start("http://www.moswarbro.moy.su/index/skachat_moswarbro/0-6");
            #region CoctailCook
            if (sender.Equals(cboxCoctailCookType)) 
            {
                if (Bot.Settings.CookCoctailSpecials != null)
                {
                    cboxCoctailCookIceCream.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 0];
                    cboxCoctailCookPiece.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 1];
                    cboxCoctailCookStraw.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 2];
                    cboxCoctailCookUmbrella.SelectedIndex = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 3];
                    numCoctailCookIceCream.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 4] == 0 ? 1 : Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 4];
                    numCoctailCookPeace.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 5] == 0 ? 1 : Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 5];
                    numCoctailCookStraw.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 6] == 0 ? 1 : Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 6];
                    numCoctailCookUmbrella.Value = Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 7] == 0 ? 1 : Bot.Settings.CookCoctailSpecials[cboxCoctailCookType.SelectedIndex * 8 + 7];
                }
                if (Bot.Settings.CookCoctailType != null) numCoctailCookAmount.Value = Bot.Settings.CookCoctailType[cboxCoctailCookType.SelectedIndex];
            }
            #endregion
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            switch (btnStart.Text)
            {
                case "Начать погром!":
                    btnStart.Text = "Завершить погром!";
                    Icon = Moswar.Properties.Resources.BotStarted;
                    TrayIcon.Icon = Moswar.Properties.Resources.BotStarted;
                    if (sender != null || Bot.Me.Events.SessionStartDT == new DateTime()) Bot.Me.Events.SessionStartDT = DateTime.Now; //Зпуск произведён человеком или первый раз через командную строку.
                    STimer.Start();
                    if (BotThread.ThreadState == System.Threading.ThreadState.Aborted || BotThread.ThreadState == System.Threading.ThreadState.AbortRequested) BotThread = new Thread(new ThreadStart(Bot.StartBot));
                    BotThread.Name = "MainBotThread";
                    BotThread.Start();                                                                                                       
                    break;
                case "Завершить погром!":
                    btnStart.Text = "Начать погром!";
                    Icon = Moswar.Properties.Resources.BotStopped;
                    TrayIcon.Icon = Moswar.Properties.Resources.BotStopped;
                    STimer.Stop();
                    BotThread.Interrupt(); //Уничтожаем зависимости от таймеров внутри
                    BotThread.Abort(); //Убиваем поток!
                    Bot.UpdateStatus("© " + DateTime.Now + " Ай-ай Сэр! Стою вкопался, кругом ведь враги!");
                    break;
            }
        }

       private void OilType_CheckedChanged(object sender, EventArgs e)
        {
            if (sender.Equals(chkGoOil))
                chkGoOilLenin.Enabled = !((CheckBox)sender).Checked;
            if (sender.Equals(chkGoOilLenin))
                chkGoOil.Enabled = !((CheckBox)sender).Checked;
            CheckGroupBox_CheckedChanged(sender, e);
        }

        private void CheckIP(object sender, EventArgs e)
        {
            string sIP = "";
            foreach (Match match in Regex.Matches(((MaskedTextBox)sender).Text.Replace(" ", ""), "([0-9])+(?<IP>[.:])?"))
            {
                sIP += new string('0', (match.Groups["IP"].Success ? 4 : 5) - match.Value.Length) + match.Value;
            }
            ((MaskedTextBox)sender).Text = sIP;
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            UpdateExpertSettings();
            UpdateSettings();
        }
        private void btnBug_Click(object sender, EventArgs e)
        {
            Bot.BugReport(null, true, "", ctrMainBrowser);
        }
        private void btnTest_Click(object sender, EventArgs e)
        {
            Bot.Test();
//            string s = ctrMainBrowser.Document.Window.Frames[1].Document.GetElementById("messages").InnerHtml;
            //ctrMainBrowser.Navigate("http://www.whatismyip.com/");
            //ctrMainBrowser.Navigate("http://whatsmyuseragent.com/");
        }

        //###################### MIN BUTTON OVVERIDING ##########################################
        //Для сворачивания в трей например.
        private enum WM_STATE { Restored, Minimized, Maximized };
        private const int WM_SIZE = 0x0005;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xf020;
        private const int SC_RESTORE = 0xF120;        
        protected override void WndProc(ref Message MSG)
        {
            switch (MSG.Msg)
            { 
                case WM_SYSCOMMAND:
                    switch ((int)MSG.WParam)
                    {
                        case SC_MINIMIZE:                            
                            Bot.MeInTray = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift); //Пересохраняем куда будем сворачиваться в трей или таскбар! (Далее работает через ивент WM_SIZE)                            
                            break;
                        case SC_RESTORE:
                            break;  // MSG.Result = IntPtr.Zero; return;
                    }
                    break;
                case WM_SIZE:
                    switch ((int)MSG.WParam)
                    { 
                        case (int)WM_STATE.Restored:
                            break;
                        case (int)WM_STATE.Minimized:
                            if (Bot.MeInTray)
                            {
                                MSG.Result = IntPtr.Zero;
                                this.Visible = false;
                                this.TrayIcon.Visible = true;
                                return;
                            }
                            break;
                        case (int)WM_STATE.Maximized:
                            break;
                    }
                    break;
            }
            base.WndProc(ref MSG);
        }
        private void TrayIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this.Visible = true;
        } 
       
        //###################### MULTITHREADING FUNCTIONS ####################################### 
        //Реализация (Webbrowser) www.wikiencyclopedia.net/DotNET/8.0/untmp/whidbey/REDBITS/ndp/fx/src/WinForms/Managed/System/WinForms/WebBrowser.cs/1/WebBrowser.cs
        //Proxy с авторизацией:   www.journeyintocode.com/2013/08/c-webbrowser-control-proxy.html
        // http://blogs.technet.com/b/srd/archive/2009/04/03/the-mshtml-host-security-faq-part-ii-of-ii.aspx
 
        public static bool IsMultiFrame(WebBrowser WB)
        {
            bool bRet = false;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { bRet = frmMain.IsMultiFrame(WB); };
                WB.Invoke(inv);
            }
            else
            { 
                bRet = WB.Document != null ? WB.Document.Window.Frames.Count > 1 : false;
            }
            return bRet;
        }
        public static void InvokeMember(WebBrowser WB, HtmlElement H, string methodName)
        {
            WB.Tag = "Loading"; H.InvokeMember(methodName);
        }
        public static bool IsBusy(WebBrowser WB)
        {
            bool bRet = false;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { bRet = frmMain.IsBusy(WB); };
                WB.Invoke(inv);
            }
            else
            { bRet = WB.IsBusy || WB.Document == null; }
            return bRet;
        }
        public static bool IsComplete(WebBrowser WB)
        {
            bool bRet = false;
          
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { bRet = frmMain.IsComplete(WB); };
                WB.Invoke(inv);
                if (!bRet) Thread.Sleep(10); //Страничка ещё не загружена, подождать 10 мс.
            }
            else
            {
                try
                {
                    object Info = GetJavaVar(WB, "document.readyState") ?? "loading";
                    bRet = Info.Equals("interactive") || Info.Equals("complete");
                }
                catch 
                {
                    bRet = false;
                }                
            }
            return bRet;    
        }
        public static string GetState(WebBrowser WB)
        {
            string sRet = null;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { sRet = frmMain.GetState(WB); };
                WB.Invoke(inv);
            }
            else
            { sRet = WB.ReadyState.ToString(); }
            return sRet;
        }
        public static IntPtr GetHandle(WebBrowser WB)
        {
            IntPtr sRet = IntPtr.Zero;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { sRet = frmMain.GetHandle(WB); };
                WB.Invoke(inv);
            }
            else
            { sRet = WB.Handle; }
            return sRet;
        }
        public static IntPtr GetHandle(Form FRM)
        {
            IntPtr sRet = IntPtr.Zero;
            if (FRM.InvokeRequired)
            {
                MethodInvoker inv = delegate() { sRet = frmMain.GetHandle(FRM); };
                FRM.Invoke(inv);
            }
            else
            { sRet = FRM.Handle; }
            return sRet;
        }
        public static HtmlDocument GetDocument(WebBrowser WB, int FrameIndex = 0)
        {
            HtmlDocument htmlRet = null;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { htmlRet = frmMain.GetDocument(WB, FrameIndex); };
                WB.Invoke(inv);                
            }
            else
            { htmlRet = IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document; } //0 фрэйм ибо чат открывает в первом фрейме и эта шняга мешает нормальной работе!
            return htmlRet;
        }
        public static string GetDocumentText(WebBrowser WB, int FrameIndex = 0)
        {
            string sRet = "";
            if (WB.InvokeRequired)
            {
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Bot.Settings.GagIE);
                MethodInvoker inv = delegate() { sRet = frmMain.GetDocumentText(WB, FrameIndex); };
                while (sRet == "" && DateTime.Now < MonitorDT) //Если, что придёться уронить, иначе можем зависнуть!
                {
                    WB.Invoke(inv);
                    if (sRet == "") Thread.Sleep(200); //Не удалось в прошлый раз? делаем мелкую паузу перед новой попыткой!
                }
            }
            else
            {
                try 
                {
                    sRet = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).Body.InnerText; //0 фрэйм ибо чат открывает в первом фрейме и эта шняга мешает нормальной работе!
                }
                catch { }                 
            }                 
            return sRet;
        }
        public static string GetDocumentHtmlText(WebBrowser WB, int FrameIndex = 0)
        {
            string sRet = "";
            if (WB.InvokeRequired)
            {
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Bot.Settings.GagIE);
                MethodInvoker inv = delegate() { sRet = frmMain.GetDocumentHtmlText(WB, FrameIndex); };
                while (sRet == "" && DateTime.Now < MonitorDT) //Если, что придёться уронить, иначе можем зависнуть!
                {
                    WB.Invoke(inv);
                    if (sRet == "") Thread.Sleep(200); //Не удалось в прошлый раз? делаем мелкую паузу перед новой попыткой!
                }
            }
            else
            {
                try 
                {
                    sRet = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).Body.InnerHtml; //0 фрэйм ибо чат открывает в первом фрейме и эта шняга мешает нормальной работе!
                }
                catch { }                
            } 
            return sRet;
        }
        public static string GetDocumentHtmlTextEx(WebBrowser WB, int FrameIndex = 0) 
        {
            string sRet = "";            
            if (WB.InvokeRequired)
            {
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Bot.Settings.GagIE);
                MethodInvoker inv = delegate() { sRet = frmMain.GetDocumentHtmlTextEx(WB, FrameIndex); };
                while (sRet == "" && DateTime.Now < MonitorDT) //Если, что придёться уронить, иначе можем зависнуть!
                {
                    WB.Invoke(inv);
                    if (sRet == "") Thread.Sleep(200); //Не удалось в прошлый раз? делаем мелкую паузу перед новой попыткой!
                }                    
            }
            else
            {
                try
                {
                    HtmlDocument htmlDocument = IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document;
                    sRet = Bot.WBEx.GetHTMLSource(htmlDocument, Encoding.UTF8);
                }
                catch { }
            }
            return sRet;
        }        
        public static string GetDocumentURL(WebBrowser WB, int FrameIndex = 0)   
        {
            string sRet = "";
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { sRet = frmMain.GetDocumentURL(WB, FrameIndex); };
                WB.Invoke(inv);
            }
            else
            {
                try
                {
                    sRet = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).Url.ToString(); //0 фрэйм ибо чат открывает в первом фрейме и эта шняга мешает нормальной работе!
                    if (GetJavaVar(WB, "AngryAjax.turned").Equals("1") && (WB.Version.Major < 10 || Bot.Settings.MaxIEVersion < 10)) //Включена быстрая загрузка страниц!
                    {
                        sRet = Regex.Replace(sRet, "(?<=" + Bot.Settings.ServerURL + ")([^#])+#", "", RegexOptions.IgnoreCase);        
                    }                  
                }
                catch { }
            }
            return sRet;
        }        
        public static HtmlElement[] GetElementsById(WebBrowser WB, string Id, int FrameIndex = 0)
        {
            HtmlElement[] HcRet = null;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { HcRet = frmMain.GetElementsById(WB, Id, FrameIndex); };
                WB.Invoke(inv);
            }
            else
            { 
                HtmlElement HtmlEl;
                do
                {
                    HtmlEl = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).GetElementById(Id); //0 фрэйм ибо чат открывает в первом фрейме и эта шняга мешает нормальной работе!
                    if (HtmlEl != null)
                    {
                        Array.Resize<HtmlElement>(ref HcRet, (HcRet == null ? 1 : HcRet.Count<HtmlElement>() + 1));
                        HcRet[HcRet.Count<HtmlElement>() - 1] = HtmlEl;
                        HtmlEl.Id = Id + "_old";                        
                    }
                }
                while (HtmlEl != null);
                if (HcRet != null) foreach (HtmlElement H in HcRet) { H.Id = Id; } //Возвращаю старые Id на место!
            }
            return HcRet;
        }
        public static void InvokeScript(WebBrowser WB, string Script, int FrameIndex = 0)
        {            
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.InvokeScript(WB, Script, FrameIndex); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                if (Bot.DebugMode) Bot.UpdateStatus("Script: " + Script);
                WB.Tag = "Loading";
                (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript(Script); 
            } 
        }
        public static object InvokeScript(WebBrowser WB, string Script, object[] Args, int FrameIndex = 0)
        {
            object oRet = null;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { oRet = frmMain.InvokeScript(WB, Script, Args, FrameIndex); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                if (Bot.DebugMode) Bot.UpdateStatus("Script: " + Script);
                WB.Tag = "Loading";
                oRet = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript(Script, Args);  
            } 
            return oRet;
        }
        public static object GetJavaVar(WebBrowser WB, string Name, int FrameIndex = 0)
        {
            object oRet = null;
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { oRet = frmMain.GetJavaVar(WB, Name, FrameIndex); };
                WB.Invoke(inv);
            }
            else oRet = (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript("eval", new object[] { Name });
            return oRet;        
        }
        public static void SetJavaVar(WebBrowser WB, string Name, string NewValue, int FrameIndex = 0)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { SetJavaVar(WB, Name, NewValue, FrameIndex); };
                WB.Invoke(inv);
            }
            else (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript("eval", new object[] { Name + "=" + NewValue });            
        }
        public static void NavigateURL(WebBrowser WB, string URL, int FrameIndex = 0)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.NavigateURL(WB, URL, FrameIndex); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                WB.Tag = "Loading";
                if (Bot.DebugMode) Bot.UpdateStatus(URL);
                if (URL.Contains(Bot.Settings.ServerURL) && !URL.EndsWith(Bot.Settings.ServerURL) && (GetJavaVar(WB, "AngryAjax.turned") ?? "0").Equals("1")) //Включена быстрая загрузка страниц! (Проверка на null для охоты в ОК например, когда загрузка начинается не с странички мосвара)
                {                    
                    (IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript("eval", new object[] { "AngryAjax.goToUrl('" + Regex.Replace(URL, "(http://)?" + Bot.Settings.ServerURL, "") + "');" });                
                }
                else
                {
                    if (IsMultiFrame(WB)) WB.Document.Window.Frames[FrameIndex].Navigate(Regex.Replace(URL, "(http://)?" + Bot.Settings.ServerURL + "/?", "http:\\")); //Режим с чатом: Для загрузки через фрэйм нужны ссылки типа: http:\\/alley/              
                    else WB.Navigate(URL); //Обычный режим
                }                
            } 
        }
        public static void NavigateURL(WebBrowser WB, string URL, string PostData, string addHeaders = null, int FrameIndex = 0)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.NavigateURL(WB, URL, PostData, addHeaders, FrameIndex); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                WB.Tag = "Loading";
                byte[] PostBytes = Encoding.UTF8.GetBytes(PostData);
                addHeaders += (addHeaders == null ? "" : Environment.NewLine) + "Content-Type: application/x-www-form-urlencoded" + Environment.NewLine;
                if (IsMultiFrame(WB) && WB.Document.Window.Frames[FrameIndex].Name != null) URL = Regex.Replace(URL, "(http://)?" + Bot.Settings.ServerURL + "/?", "http:\\"); //Эта манипуляция нужна только для мультифреймов, к томуже под IE7 почемуто не видит имени игрового фрэйма.             
                WB.Navigate(URL, IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Name : null, PostBytes, addHeaders);                           
            }
        }
        public static void RefreshURL(WebBrowser WB, string ServerURL, int FrameIndex = 0)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.RefreshURL(WB, ServerURL, FrameIndex); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                string URL = GetDocumentURL(WB);
                if (Regex.IsMatch(URL, ServerURL)) frmMain.NavigateURL(WB, URL, FrameIndex);
                else Reconnect(WB, ServerURL);                
            }
        }
        public static void Reconnect(WebBrowser WB, string URL)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.Reconnect(WB, URL); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                WBStop(WB);
                WB.Navigate(URL);
            }
        }
        public static void WBStop(WebBrowser WB)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.WBStop(WB); };
                WB.Invoke(inv);
                Thread.Sleep(100);
            }
            else
            {
                WB.Stop();
                do
                {
                    Application.DoEvents();
                } while (WB.IsBusy);
                WB.Navigate("about:blank");
            }
        }
        public static void GoBack(WebBrowser WB) 
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.GoBack(WB); };
                WB.Invoke(inv);
            }
            else
            {
                WB.Tag = "Loading";
                WB.GoBack();
            } 
        }
        public static void ScrollTo(WebBrowser WB, int X, int Y)
        {
            if (WB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.ScrollTo(WB, X, Y); };
                WB.Invoke(inv);
            }
            else  (IsMultiFrame(WB) ? WB.Document.Window.Frames[0] : WB.Document.Window).ScrollTo(X, Y);
        }
        public static string GetTextBoxText(TextBox TB)
        {
            string sRet = null;
            if (TB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { sRet = frmMain.GetTextBoxText(TB); };
                TB.Invoke(inv);
            }
            else
            { sRet = TB.Text; }
            return sRet;
        }
        public static void InsertListItem(ListBox LB, int ItemNr, string Item)
        {
            if (LB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.InsertListItem(LB, ItemNr, Item); };
                LB.Invoke(inv);
            }
            else
            { LB.Items.Insert(ItemNr, Item); if (LB.Items.Count >= 300) LB.Items.RemoveAt(LB.Items.Count - 1); }        
        }
        public static void AddListItem(ListBox LB, string Item)
        {
            if (LB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.AddListItem(LB, Item); };
                LB.Invoke(inv);
            }
            else
            { LB.Items.Add(Item); }   
        }
        public static void RemoveListItem(ListBox LB, string Item)
        {
            if (LB.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.RemoveListItem(LB, Item); };
                LB.Invoke(inv);
            }
            else
            { LB.Items.Remove(Item); }
        }
        public static void ToolStripAddText(ToolStrip TS, int ItemNr, string ItemText) 
        {
            if (TS.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.ToolStripAddText(TS, ItemNr, ItemText); };
                TS.Invoke(inv);
            }
            else
            { TS.Items[ItemNr].Text = ItemText; }  
        }
        public static void AddLabelText(Label L, string Text)
        {
            if (L.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.AddLabelText(L, Text); };
                L.Invoke(inv);
            }
            else
            { L.Text = Text; } 
        }
        public static void AddButtonText(Button B, string Text)
        {
            if (B.InvokeRequired)
            {
                MethodInvoker inv = delegate() { frmMain.AddButtonText(B, Text); };
                B.Invoke(inv);
            }
            else
            { B.Text = Text; }
        }

        private void treeViewSettings_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string PanelName = e.Node.Name.Replace("Node", "pnl");
            lblSettingsCaption.Text = "";
            foreach (Control ctrl in pnlContainer.Controls)
            {
                if (ctrl.Name == PanelName)
                {
                    lblSettingsCaption.Text = e.Node.Text;
                    ctrl.Visible = true;
                }
                else
                    ctrl.Visible = false;
            }
        }

        private void CheckGroupBox_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            GroupBox groupBox = (GroupBox)checkBox.Parent;
            foreach (Control ctrl in groupBox.Controls)
            {
                if (ctrl != checkBox)
                    ctrl.Enabled = checkBox.Checked;
            }
        }

        private void btnFunctions_Click(object sender, EventArgs e)
        {
            btnFunctions.ContextMenuStrip.Show(btnFunctions, new Point(0, 0), ToolStripDropDownDirection.AboveRight);
        }

        private void MenuItemOpenURL_Click(object sender, EventArgs e)
        {
            string URL = "";
            if (InputBox.Show("Переход на заданный URL", "Введите URL:", ref URL) == DialogResult.OK && URL != "")
                ctrMainBrowser.Navigate(URL);
        }

        private void MenuItemRunJSCmd_Click(object sender, EventArgs e)
        {
            string cmd = "";
            if (InputBox.Show("Выполнение команды JavaScript", "Введите команду:", ref cmd) == DialogResult.OK && cmd != "")
                InvokeScript(Bot.MainWB, "eval", new object[] { cmd });
        }

        private void MenuItemCheckIP_Click(object sender, EventArgs e)
        {
            //ctrMainBrowser.Navigate("e:\\Xaot-Limit.htm");
            ctrMainBrowser.Navigate("http://www.tell-my-ip.com/");
        }

        private void MenuItemEnterAuctionBet_Click(object sender, EventArgs e)
        {
            InvokeScript(Bot.MainWB, "eval", new object[] {@"
                $('td.mybet .plus_bet').after('<input type=""text"" value="""" size=""3"" class=""moswared manual_bet"">');
                $('td.mybet .manual_bet').bind('change keyup', function() {
			        $(this).parent().find('.button.make_bet .f .c .med .value').text($(this).val());
			        $(this.parentNode).find('.field-bid').val($(this).val())
                });
            "});
        }

        private void MenuItemSendGiftFix_Click(object sender, EventArgs e)
        {
            InvokeScript(Bot.MainWB, "eval", new object[] {@"
function checkName() {
	var inputRow = $('.find-player input');
	if (inputRow.val() == '') {
		return;
	}
	$.post(""/shop/playerexists/"" + encodeURIComponent(inputRow.val()), function(data){
		if (data == 1) {
			receiverChecked = true;
			$('#receiver-comment').slideDown();
			$("".find-player .error"").hide();
			$("".find-player .success"").show();
		} else {
			receiverChecked = false;
			$("".find-player .error"").show();
			$("".find-player .success"").hide();
		}
		controlButtons();
	});
}
            "});
        }

        private void btnTruckEnhancing_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            btn.ImageIndex = btn.ImageIndex == 2 ? 0 : btn.ImageIndex + 1;
        }

        private void btnTruckEnhancingCol_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            TableLayoutPanel tbl = (TableLayoutPanel)btn.Parent;
            int col = tbl.GetColumn(btn);
            Button btn1 = (Button)tbl.GetControlFromPosition(col, 3);
            btn1.ImageIndex = btn1.ImageIndex == 2 ? 0 : btn1.ImageIndex + 1;
            for (int i = 1; i < 12; i++)
                ((Button)tbl.GetControlFromPosition(col, 3 + i)).ImageIndex = btn1.ImageIndex;
        }
    }

    class InputBox
    {
        /// <summary>
        /// Displays a dialog with a prompt and textbox where the user can enter information
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="promptText">Dialog prompt</param>
        /// <param name="value">Sets the initial value and returns the result</param>
        /// <returns>Dialog result</returns>
        public static DialogResult Show(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
    }
}
