using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Globalization;
using System.Threading;
using System.Timers;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Runtime.InteropServices;

namespace Moswar
{
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]
    public class clsBot
    {
        [DllImport("wininet.dll", SetLastError = true)]
        static extern bool InternetGetCookieEx(string pchURL, string pchCookieName, StringBuilder pchCookieData, ref uint pcchCookieData, int dwFlags, IntPtr lpReserved);
        const int INTERNET_COOKIE_HTTPONLY = 0x00002000;         
 
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wparam, IntPtr lparam);
        private int WM_MOUSEMOVE = 0x0200;


        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_HIDE = 0;
        private const int SW_FORCEMINIMIZE = 11;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_SHOWMINNOACTIVE = 7;
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        private const int LWA_ALPHA = 0x2;


        //Используется для создания и чтения структур из XML файлов.
        private static XmlRootAttribute XmlRoot = new XmlRootAttribute("Settings:");
        private static XmlSerializer XmlSettingsSerializer = new XmlSerializer(typeof(stcSettings), XmlRoot);
        private static XmlSerializer XmlExpertSerializer = new XmlSerializer(typeof(stcExpert), XmlRoot);

        private object LockWriteLog = new object();

        private struct stcLastBuy
        {
            public ShopItems LastSI;            
            public DateTime LastDT;
            public int Counter;
        }

        private struct stcIgnore
        {
            public bool Timeout;
            public bool PVPAttack;
        }

        private struct stcFightItem
        {
            public string ID;
            public int WorkTurns;
            public int TillTurn;
        }        

        public enum FightItemType { None, FixedHeal, ProcHeal, Cheese, FixedBomb, ProcBomb, Helmet, Spring, Shield }

        private struct stcBagFightItem
        {
            public string Title;
            public string ItemID;
            public FightItemType ItemType;
            public int TotalCount;
            public int TimedCount;
            public DateTime LastDT;

            public override string ToString()
            {
                return "Предмет=" + Title + ", ID=" + ItemID + ", Тип=" + ItemType +
                    ", Всего=" + TotalCount + ", Временных=" + TimedCount + ", Срок=" + LastDT;
            }

            /* Определяет и возвращает тип предмета по тексту всплывающей подсказки */
            public static FightItemType DetermineType(string Info)
            {
                Match match = Regex.Match(Info,
                    "Мин. урон по врагам: (?<Bomb>([0-9])+(?<PrcBomb>%)?)" +
                    "|(?<Cheese>Призыв крысомах в групповой бой)" +
                    "|Жизни: (?<Heal>([0-9+])+(?<PrcHeal>%)?)" +
                    "|(?<Helmet>Защита от урона)" +
                    "|(?<Spring>Отражает урон)" +
                    "|(?<Shield>Уменьшение урона от гранат)"
                    );
                if (match.Groups["PrcHeal"].Success) return FightItemType.ProcHeal;
                if (match.Groups["Heal"].Success) return FightItemType.FixedHeal;
                if (match.Groups["Cheese"].Success) return FightItemType.Cheese;
                if (match.Groups["PrcBomb"].Success) return FightItemType.ProcBomb;
                if (match.Groups["Bomb"].Success) return FightItemType.FixedBomb;
                if (match.Groups["Helmet"].Success) return FightItemType.Helmet;
                if (match.Groups["Spring"].Success) return FightItemType.Spring;
                if (match.Groups["Shield"].Success) return FightItemType.Shield;
                return FightItemType.None;
            }
        }

        public struct stcCoctail
        {
            public string CoctailName;
            public int MissingAmount;
            public bool Ignore;
        }

        private Thread ChatThread;
        private DateTime ReadLogsDT;
        private stcLastBuy LastBuy; //Блокировщик от массовых покупок
        private stcIgnore Ignore;
        public Thread[] HCThread = new Thread[2];        
        public decimal[,] TrainWarPetNeed = new decimal[3, 3]; //[Нацеленность, Преданность, Массивность], [деньги, руда, нефть].
        public decimal[,] TrainRunPetNeed = new decimal[4, 4]; //[Ускорение, Скорость, Выносливость, Ловкость], [деньги, руда, нефть, медальки].
        public decimal[,] TrainMeNeed = new decimal[7, 2];     //[], [тек. характеристика, деньги].
        public stcPlayer[] vsPlayer = new stcPlayer[2];
        public clsWearSets.stcSet[] ArrWearSet;
        public stcGrpFight GrpFight; // 0-> Выкл., 1-> PVP, 2-> NPC, 3-> Все.
        public clsTaskManager.stcTaskManagerEx GrpFightTaskManager;
        public stcPlayerEx Me;
        public stcSettings Settings;
        public stcExpert Expert;
        public stcBug Bug;
        public stcFactoryUpgrade ChainUpgrade;
        public stcRepairMobile RepairMobile;
        public WebBrowser MainWB;
        public WebBrowser[] HelpWB = new WebBrowser[2]; //3 Уже не вытягивает, часто виснет!
        public ToolStrip TS;
        public ListBox LBBlackWanted;
        public ListBox LBHistory;
        public Label LUserMessage;
        public clsWBEx WBEx = new clsWBEx();
        public IntPtr FrmMainhWnd;
        public Form MyMainForm;
        public bool DebugMode;
        public bool MeInTray;
        private enum ContactAction { AddPlayer, AddClan, DeletePlayer, DeleteClan, DeleteAll }
        private enum ContactType { Victim, Enemy }
        private enum AgentAction { Pay, Check }
        private enum PigProtectionAction { Pay, Check }
        private enum WerewolfAction { Pay, Check }
        private enum ImmunAction { Mona, Tooth, Duels }
        private enum StopTimeoutType { GrpFight, RatHunting, OilLenin, Mafia, All }
        public enum Place { Phone, Player, Alley, Stash, Square, Arbat, Berezka, Casino, Pyramid, Bank, Home, Trainer, Police, Huntclub, Nightclub, Shop, Metro, Factory, Shaurburgers, Tverskaya, Petrun, Petarena, Desert, Clan, Turret, Automobile, Sovet, Oil, Gorbushka, Camp, Metrowar, URL, Mobile, Bear, Settings };
        public enum Opponent { Equal, Strong, Weak, Enemy, Victim, Major, EnemyEx, NPC, Agent, Werewolf };
        public enum ShopItems { Me100, Me50, Gum1, Gum2, Gum3, Gum4, Gum5, Gum6, Gum1Ex, Gum2Ex, Gum3Ex, Gum4Ex, Gum5Ex, Gum6Ex, Gum1Adv, Gum2Adv, Gum3Adv, Gum4Adv, Gum5Adv, Gum6Adv, Pyani, Tvorog, Coctail1, Coctail2, Coctail3, Coctail4, Coctail5, Coctail6, Vitamin, NovajaZhizn, Barjomi, AquaDeminerale, Snikers, WeakNPC1, WeakNPC2, WeakNPC3, Valujki, ValujkiAdv, GasMask, Respirator, Tea1, Shoko1, Tea4, Shoko4, Tea7, Shoko7, Tea10, Shoko10, Tea15, Shoko15, CandyExp, CandyAntiExp, Pet100, Pet50, Pick, Counter, Helmet, Mona_Ticket, Bank_Ticket, Safe, Chain, HealPlus, HealPrc, Chees, GranadePlus, GranadePrc, Spring, Helm, Shild }; //Ex - 15%; Adv - За нефть.
        public enum MetroAction { Dig, SearchRat, Game, Check }
        public enum PoliceAction { Pay, Relations, Check }
        public enum FactoryAction { Petriki, UpdateChain }
        public enum GroupFightAction { Fight, Check }
        public enum GroupFightType { Chaos, Ore, Clan, PVP, Mafia, Rat, Oil, Group, All }
        public enum PatrolAction { Patrol, Check }
        public enum OilAction { LeninFight, Fight, OilTower }
        public enum TimeOutAction { All, NoTask, Free, Busy, Blocked }
        public enum PetAction { SetWarPet, TrainWarPet, Run, TrainRunPet }
        public enum MCAction { Work, Check }
        public enum ClanWarAction { Check, Tooth, Pacifism }
        public enum CasinoAction { Kubovich, Slots, Loto, BuyFishki, SellFishki }
        public enum PyramidAction { Buy, Sell, Check }
        public enum BankAction { Exchange, BuySafe, Deposit }
        public enum AutomobileAction { Check, Fuel, Taxi, Ride }
        public enum DopingAction { Use, Check, CheckTime }
        public enum SafeAction { Buy, Check }
        public enum TraumaAction { Heal, Check }
        public enum SovetAction { SellPoints, Patriot, Vote }
        public enum MajorAction { Buy, Check }
        public enum NextTimeout { Patrol, Metro, Rat, Atack, Fight, Chain, OilLeninFight }
        public enum TaborPetAction { Feed, Check }
        public enum SovetAgitatorAction { Buy, Check }
        public enum MobilePhoneAction { CheckBattery, MafiaFight, MafiaTrade, Repair, ReadLogs }
        public enum CoctailAction { GetRecipe, CheckRecipe, Cook, CheckMissing }
        public enum CoctailType { Health, Strength, Dexterity, Resistance, Intuition, Attention, None }

        public struct stcPlayerInfo
        {
            public string URL;
            public string Id;
            public string Name;
            public string Level;
            public string Fraction;
        }

        public struct stcPlayer
        {
            public int[] Health;
            public int[] Strength;
            public int[] Dexterity;
            public int[] Endurance;
            public int[] Cunning;
            public int[] Attentiveness;
            public int[] Charisma;
            public int Level;
            public Boolean Steroids;
            public string[] LifePkt;
            public string[] Energy;
            public string Fraction;
            public string Name;
            public string URL;
        }

        public struct stcPlayerEx
        {
            public stcCarRide CarRide;
            public stcPlayer Player;
            public stcClan Clan;
            public stcWarPet WarPet;
            public stcRunPet RunPet;
            public DateTime OilTowerDT;
            public DateTime MetroWarPrizeDT;            
            public bool Wanted;
            public stcWallet Wallet;

            public stcPerk PerkAntiLamp;

            public stcStop Bear;
            public stcStop Trauma;
            public stcStop Police;
            public stcStop Patrol;
            public stcStop MC;
            public stcStop Rat;
            public stcStop OilHunting;
            public stcStop NPCHunting;                        
            public stcStop Kubovich;
            public stcStop Loto;
            public stcStop Fishki;
            public stcStop Automobile;
            public stcStop Safe;
            public stcStop PigProtection;
            public stcStop Major;
            public stcStop QuestFillTonus;
               
            public stcClanWar ClanWarInfo;
            public stcBankDeposit BankDeposit;
            public stcSet SetInfo;
            public stcSovet SovetInfo;
            public stcBuyAction TaborPet;
            public stcBuyAction SovetAgitator;
            public stcAzazella Azazella;
            public stcTurret Turret;

            public stcMonya Thimbles;
            public stcPyramid Pyramid;
            public stcPetriki Petriki;
            public stcHCHunting HCHunting;
            public stcStopEx AgentHunting;
            public stcStopEx WerewolfHunting;
            public stcRatHunting RatHunting;
            public stcOilLeninHunting OilLeninHunting;
            public stcCoctailInfo CocktailRecipe;

            public clsDoping.stcDopingEx[] ArrUsualDoping;
            public clsDoping.stcDopingEx[] ArrRatDoping;
            public clsDoping.stcDopingEx[] ArrOilLeninDoping;

            public DateTime[] ArrDuelsDT;

            public stcEvents Events;
            public stcStatus Status;
        }

        public struct stcWallet
        {
            public int Money;
            public int Oil;
            public int Ore;
            public int Honey;
            public int Badge;
            public int Star;
            public int Mobile;
            public int WhiteTooth;
            public int GoldTooth;
            public int PetGold;
            public int PowerGold;
        }

        public struct stcStatus
        {
            public long iM;
            public long iR;
            public long iN;
            public long iL;
            public long iB;            
            public long iT;
        }

        public struct stcEvents
        {
            public DateTime SessionStartDT;
            public DateTime ShutdownDT;
            public DateTime NextItemCheckDT;
            public DateTime NextSetWarPetDT;
            public DateTime NextSlotInjuredDT;
            public DateTime NextFightItemCheckDT;
            public DateTime NextAFK;
            public bool StopQuest;
            public bool ShutdownRelease;
        }

        public struct stcCar
        {
            public int ID;
            public int Lvl;
            public int Model;
            public bool Reserved;
            public TimeSpan RideTime;
            public DateTime Timeout;
        }

        [Serializable]
        public struct stcCarRide
        {
            public int Helicopter;
            public stcCar[] Cars;
            public DateTime[] RideTimeout;
        }

        public struct stcWarPet
        {
            public string[] LifePkt; //string[,] LifePkt;
            public DateTime TrainTimeOutDT;
            public decimal Focus;
            public decimal Loyality;
            public decimal Mass;
        }

        public struct stcRunPet
        {
            public DateTime TrainTimeOutDT;
            public DateTime RunTimeOutDT;
            public bool Doping;
            public DateTime DopingDT;
            public stcStop FreeRuns;
            public int Tonus;
            public int Lvl;
            public decimal Acceleration;
            public decimal Speed;
            public decimal Endurance;
            public decimal Dexterity;
        }
        
        public struct stcPerk
        {
            public bool On;
            public DateTime SwitchOffDT;
        }

        public struct stcFactoryUpgrade
        {
            public bool Release;
            public int Rang;
            public int[] Points;
            public bool Stop;
        }

        public struct stcRepairMobile
        {
            public DateTime NextDT;
            public int[] ID;
        }

        public struct stcStop
        {
            public int Val;
            public DateTime LastDT;
            public bool Stop;
        }

        public struct stcStopEx
        {
            public int Val;
            public DateTime LastDT;
            public DateTime StartDT;
            public bool Stop;
        }

        public struct stcRatHunting
        {
            public int Defeats;
            public int Lvl;
            public DateTime RestartDT;
            public DateTime NextDT;
            public bool Stop;
        }

        public struct stcOilLeninHunting
        {
            public int Defeats;
            public int Lvl;
            public string FightType;
            public DateTime RestartDT;
            public DateTime NextDT;
            public bool AllowPartBilet;
            public bool Stop;
        }

        public struct stcHCHunting 
        {
            public bool Search;
            public int Victims;
            public DateTime LastDT;
            public bool Stop;
        }

        public struct stcMonya
        {
            public int Val;
            public DateTime LastDT;
            public DateTime StartDT;
            public DateTime BankStartDT;
            public bool Detected;
            public string[] Matrix;
            public bool Stop;
        }

        public struct stcPyramid 
        {
            public int Price;
            public DateTime RestartDT;
            public bool Done;
            public bool BlockMonya;
        }

        public struct stcPetriki
        {
            public int Money;
            public int Ore;
            public int Bonus;
            public int NeedCoffee;
            public int NeedHoney;
            public int BlackBook;           
            public DateTime LastDT;
            public DateTime RestartDT;
        }

        public struct stcClanImmun //TimeSpan не сериализируется=(
        {
            public string Start;
            public string Stop;
        }

        public struct stcClan
        {
            public string Name;
            public string URL;
        }

        public struct stcClanWar
        {
            public bool MyWar;
            public stcClan[] vsClan;
            public stcClan EnemyClan;
            public int WarStep;
            public DateTime NextDT;
            public stcClanImmun[] Pacifism;
            public bool Now;
        }

        public struct stcGrpFight
        {            
            public int Val; // 0-> Выкл., 1-> PVP, 2-> NPC, 3-> Все.
            public string Price; //Tugriki:500 ибо цены на разных уровнях на разные типы боев разные!
            public GroupFightType NextFightType;
            public DateTime NextFightDT;            
            public DateTime ChaosStartDT;
            public DateTime ClanStartDT;
            public DateTime PVPStartDT;            
            public DateTime OreStartDT;
            public DateTime NextCheckDT; //Нужно, только чтоб не ходить кругами на аллею, когда совершенно нечего делать!
            public stcMafia Mafia;
            public bool Waiting;
        }
        public struct stcMafia
        {
            public DateTime LastCheckDT;
            public DateTime NextFightDT;
            public DateTime LastFightDT;
            public bool FightFound;
        }

        public struct stcSovet
        {
            public DateTime Patriot;
            public DateTime LastVoting;
            public bool Stop;
        }

        public struct stcBuyAction
        {
            public int PriceTugriki;
            public int PriceOre;
            public int PriceOil;
            public int PriceMed;
            public DateTime LastDT;
        }

        public struct stcAzazella
        {
            public bool EnoughGold;
            public int TreasurePrc;
            public DateTime NextDT;            
            public DateTime PlayTillDT;
        }

        public struct stcTurret
        {
            public int PriceTugriki;
            public int PriceOre;
            public int PriceOil;
            public int PriceMed;
            public DateTime LastDT;
        }

        public struct stcBankDeposit 
        {
            public int MyMoney;
            public DateTime SafeTillDT;
            public DateTime StartDT;
        }        

        public struct stcSet
        {
            public DateTime LastDT;
            public int LastSetIndex;
        }

        public struct EatDopingInfo
        {
            public string BoxID;
            public string[] BtnID;
            public string BlockID;            
            public string PicName;
        }

        public struct stcCoctailComponent
        {
            public string Fruit;
            public int RecipeAmount;
            public int StorageAmmount;
            public bool Use;
        }

        public struct stcSpecialCoctailComponent
        {
            public string Name;
            public int StorageAmmount;
        }

        public struct stcCoctailInfo
        {            
            public stcSpecialCoctailComponent[] SpecialComponent;
            public stcCoctailComponent[] Component;
            public int RecipeTotalFruitsAmount;
            public DateTime LastCook;
            public DateTime LastCheck;
            public bool Wrong;
        }        

        public struct stcBug
        {
            public int Nr;
            public string Info;
            public bool Logging;
        }
        
        [Serializable()]
        public struct stcSettings
        {
            public string BotName;
            public string Email;
            public string Password;
            public decimal HealMe50;
            public decimal HealMe100;
            public decimal AmountHealMe;
            public bool BuyHealMe;
            public decimal HealPet50;
            public decimal HealPet100;
            public bool BuyHealPet;
            public int SetWarPetType;
            public bool HealTrauma;
            public bool HealInjuredSlot;
            public bool GoHC;
            public decimal minHCLvl;
            public decimal maxHCLvl;
            public decimal minHCStatDiff;
            public DateTime StartHC;
            public DateTime StopHC;
            public bool HCMember;
            public bool HCUseTorture;
            public bool HCRevenge;
            public decimal HCRevengeMaxMoney;
            public bool GoMetro;
            public bool BuyMpick;
            public bool BuyRpick;
            public bool BuyHelmet;
            public bool BuyCounter;
            public bool SearchRat;
            public bool AttackRat; //Нападать на крысомаху, если поймалась при копке.
            public decimal maxRatLvl;
            public decimal maxRatDefeats;
            public decimal maxSearchRatLvl;            
            public bool SearchRatLeaveNoKey;
            public bool SearchRatLeaveNoElement;
            public bool SearchRatLeaveNoBox;
            public bool SearchRatIgnoreAll;            
            public bool SearchRatRobinHood;
            public bool SearchRatBambula;
            public decimal maxSearchRatDefeats;
            public bool SearchRatUseOhara;
            public bool UseRatFastSearch;
            public decimal RatFastSearch;
            public bool RatFastSearchHoney;
            public bool[] UseRatItems;
            public decimal PlayThimbles;
            public decimal minThimblesMoney;
            public bool ThimblesImmunity;
            public bool UseThimblesTicket;
            public bool UseBank;
            public decimal ExchangeBankMoney;
            public bool UseThimblesTrick;
            public decimal ThimblesTrickChance;
            public bool UseVictims;
            public decimal AddVictim;
            public decimal DeleteVictim;
            public bool UseOnlyHomelessVictims;
            public Opponent AlleyOpponent;
            public decimal minAlleyLvl;
            public decimal maxAlleyLvl;
            public bool AttackNPC;
            public bool MrPlushkin;
            public bool UseHomeless;
            public bool GoPVPFight;
            public bool GoNPCFight;
            public bool GoPVPInstantly;
            public decimal GoPVPInstantlyOffset;
            public bool SovetBuyAgitator;
            public bool GoPatrol;
            public decimal PatrolTime;
            public int PatrolType;
            public bool PayPolice;
            public decimal PayPoliceAt;
            public bool WaitPolice;
            public bool MakePetriki;
            public int PetrikiBonus;
            public decimal minPetrikiMoney;
            public decimal minPetrikiOre;
            public bool GoFactory;
            public decimal FactoryChainCount;
            public int FactoryRang;
            public decimal minFactoryMoney;
            public decimal minFactoryOre;
            public bool GoMC;
            public decimal minMoneyMC;
            public decimal MCAfterOnline;
            public decimal maxMCWorkTime;
            public decimal MCWorkTime;
            public decimal SDWorkTime;
            public decimal SDThimblesMoney;
            public bool TrainWarPet;
            public int TrainWarPetType;
            public bool TrainPetFocus;
            public decimal maxTrainPetFocus;
            public bool TrainPetLoyality;
            public decimal maxTrainPetLoyality;
            public bool TrainPetMass;
            public decimal maxTrainPetMass;
            public bool TrainRunPet;
            public int TrainRunPetType;
            public bool TrainPetAcceleration;
            public decimal maxTrainPetAcceleration;
            public bool TrainPetSpeed;
            public decimal maxTrainPetSpeed;
            public bool TrainPetEndurance;
            public decimal maxTrainPetEndurance;
            public bool TrainPetDexterity;
            public decimal maxTrainPetDexterity;
            public bool UseTrainWhip;
            public decimal minTrainPetMoney;
            public decimal minTrainPetOre;
            public decimal minTrainPetOil;
            public bool UseRunPet;
            public bool WantedPlayThimbles;
            public decimal minWantedPlayThimbles;
            public bool WantedGoMC;
            public bool GoClanFight;
            public decimal minClanMeFightHp;
            public decimal minClanPetFightHp;
            public bool ClanLastMin;
            public bool[] UseGrpFightItems;
            public bool UseAutoFightBagSlots;
            public bool PlayLoto;
            public bool PlayKubovich;
            public int maxKubovichRotations;
            public bool BuyFishki;            
            public bool BuyFishkiAllways;
            public decimal FishkiAmount;
            public bool GoPyramid;
            public bool BlockThimbles;
            public decimal maxPyramidPrice;
            public decimal minPyramidAmount;
            public decimal maxPyramidSell;
            public bool UseCar;
            public int CarPrize;
            public bool UseSpecialCar;
            public int SpecialCar;
            public bool TrainMe;
            public bool TrainMeHealth;
            public decimal maxTrainMeHealth;
            public bool TrainMeStrength;
            public decimal maxTrainMeStrength;
            public bool TrainMeDexterity;
            public decimal maxTrainMeDexterity;
            public bool TrainMeEndurance;
            public decimal maxTrainMeEndurance;
            public bool TrainMeCunning;
            public decimal maxTrainMeCunning;
            public bool TrainMeAttentiveness;
            public decimal maxTrainMeAttentiveness;
            public bool TrainMeCharisma;
            public decimal maxTrainMeCharisma;
            public bool SovetVote;
            public bool OpenPrizeBox;
            public bool BuySafe;
            public bool BuyMajor;
            public bool Quest;
            public bool QuestEndMoney;
            public bool QuestFillTonusBottle;
            public bool QuestFillTonusPlus;
            public bool FeedTaborPet;
            public bool BuyMonaTicketTooth;
            public decimal minTeeth;
            public bool BuyMonaTicketStar;
            public decimal minStars;
            public bool GoGroupFightChaos;
            public bool GoGroupFightOre;
            public bool GoGroupFightMafia;
            public bool MafiaUseLicence;
            public bool GetOil;
            public bool GoOil;            
            public int maxOilLvl;
            public bool OilIgnoreTimeout;
            public bool UseSnikersOil;
            public bool GoOilLenin;
            public int maxOilLeninLvl;
            public bool OilLeninLeaveNoKey;
            public bool OilLeninLeaveNoElement;
            public bool OilLeninLeaveNoBox;
            public bool OilLeninRobinHood;
            public bool OilLeninIronHead;
            public bool OilLeninSyncRats;
            public decimal OffsetSyncOilLenin;
            public bool[] UseOilLeninItems;
            public decimal maxOilLeninDice;
            public decimal maxOilDefeats;
            public bool OilUseOhara;
            public bool AddClan;
            public bool FarmClan;
            public bool RemoveEnemy;
            public bool ClanWars;
            public bool Berserker;
            public bool UseSnikersEnemy;
            public bool Lampofob;

            public bool UseRestartMemory;
            public decimal maxRestartMemory;
            public bool RestartHidden;
            public bool RestartDoping;
            public bool CheckForUpdate;

            public string ServerURL;

            public bool UseWerewolf;
            public int WerewolfDrogType;
            public int WerewolfLevel;
            public int WerewolfPrice;
            public Opponent WerewolfOpponent;
            public decimal minWerewolfLvl;
            public decimal maxWerewolfLvl;

            public bool UseAgent;
            public Opponent AgentOpponent;
            public decimal minAgentLvl;
            public decimal maxAgentLvl;

            public bool GetMetroWarPrize;
            public bool UseWearSet;
            public bool DoNotReadPrivateMessages;
            public bool BuildTurel;
            public bool ReadLogs;
            public bool PigProtection;

            public bool PreferZefir;
            public bool PreferShokoZefir;
            public bool AllowCoctailAdv;
            public bool AllowPartBilet;
            public bool NoSauceNoProblem;
            public bool NoCandyNoProblem;

            public bool RepairMobile;
            public bool SellRepairMobile;

            public bool UseMaxDuels;
            public decimal maxDuels;
            public bool UseDuelTimes;
            public DateTime StartDuelsDT;
            public DateTime StopDuelsDT;

            public bool BuyBankSafe;
            public bool UseBankDeposit;
            public decimal DepositMoney;

            public bool PlayAzazella25;
            public bool PlayAzazella75;
            public decimal minAzazellaGold;
            public bool AzazellaFastPlay;
            public bool AzazellaTreasure;
            public decimal AzazellaTreasureChance;

            public bool UseAFK;
            public decimal AFKCahnce;
            public decimal AFCTime;

            public bool UseCookCoctail;
            public decimal[] CookCoctailType;
            public int[] CookCoctailSpecials;
            public bool UseMaxFruitProRecipe;
            public decimal MaxFruitProRecipe;
            public decimal TotalFruitsProRecipe;
            public bool UseMinFruitIgnoreAmount;
            public decimal MinFruitIgnoreAmount;
            public bool SellBadCoctail;
 
            public string Proxy;
            public string ProxyUserName;
            public string ProxyPassword;
            public bool UseProxy;

            public decimal MaxIEVersion;
            public decimal GagIE;
        }
        
        #region Настройки эксперт
        [Serializable()]
        public struct stcExpert
        {
            public decimal ProxyMin;
            public decimal ProxyMax;
            public decimal GoToMin;
            public decimal GoToMax;
            public decimal UseTimeout;
            public decimal AnalyseFightMin;
            public decimal AnalyseFightMax;
            public decimal QuestFruitNr;
            public decimal QuestMoneyNr;
            public bool QuestNotAll;
            public bool QuestIgnoreBonus;
            public bool QuestUseAllTonusBottle;
            public decimal RevengerPrc;
            public bool DoNotProofTorture;
            public bool DoNotUseIron;
            public bool DoNotUseAntiSafe;
            public bool DoNotUseAngleGrinder;
            public bool DoNotEatPelmeni;
            public bool DoNotLoadImage;
            public bool DoNotLoadActiveX;
            public decimal MaxWebSockets;
            public bool[] BuyFightItemType; //0 -> резерв, 1-> лечение, 2-> сыр (покачто купить невозможно), 3-> гранаты+, 4-> франаты%, 5-> пружина, 6-> каска, 7-> щит
            public decimal MaxBuyFightItemAmount;
            public int[] FightSlotItemTypes;
            public bool BuyMoreThenOneGranade;
        } 
        #endregion
        #region Конструктор
        public clsBot()
        {
            Bug.Logging = true; //Логгирование ошибок.
            #region Инициализация
            Settings.GagIE = 60;            
            Settings.minAlleyLvl = 1;            
            Settings.maxAlleyLvl = 50;
            Settings.AlleyOpponent = Opponent.Weak;
            Settings.minWerewolfLvl = -5;
            Settings.maxWerewolfLvl = 5;
            Settings.WerewolfLevel = 1;
            Settings.WerewolfOpponent = Opponent.Weak;
            Settings.minAgentLvl = 1;
            Settings.maxAgentLvl = 50;
            Settings.AgentOpponent = Opponent.Strong;
            GrpFight.Price = "tugriki:0";

            Settings.StartDuelsDT = DateTime.Now.Date;
            Settings.StopDuelsDT = DateTime.Now.Date.Add(new TimeSpan(23, 59, 59));
            Settings.StartHC = DateTime.Now.Date;
            Settings.StopHC = DateTime.Now.Date.Add(new TimeSpan(23, 59, 59));


            Me.MC.Stop = true;
            Me.SetInfo.LastSetIndex = -1;
            #endregion
            Me.ClanWarInfo.vsClan = new stcClan[3]; //Максимальное количество одновременных кланвойн.
            //[0]-> Голые статы с регалиями,  [1]-> Статы в одежде;
            Me.Player.Health = new int[2];
            Me.Player.Strength = new int[2];
            Me.Player.Dexterity = new int[2];
            Me.Player.Endurance = new int[2];
            Me.Player.Cunning = new int[2];
            Me.Player.Attentiveness = new int[2];
            Me.Player.Charisma = new int[2];
            vsPlayer[0].Health = new int[2];
            vsPlayer[0].Strength = new int[2];
            vsPlayer[0].Dexterity = new int[2];
            vsPlayer[0].Endurance = new int[2];
            vsPlayer[0].Cunning = new int[2];
            vsPlayer[0].Attentiveness = new int[2];
            vsPlayer[0].Charisma = new int[2];
            vsPlayer[1].Health = new int[2];
            vsPlayer[1].Strength = new int[2];
            vsPlayer[1].Dexterity = new int[2];
            vsPlayer[1].Endurance = new int[2];
            vsPlayer[1].Cunning = new int[2];
            vsPlayer[1].Attentiveness = new int[2];
            vsPlayer[1].Charisma = new int[2];
            if (!Directory.Exists("BuG-Report")) Directory.CreateDirectory("BuG-Report");
        }
        #endregion

        #region Help Functions

        public struct stcPetState
        {
            public int type;
            public int maxState;
        }
        enum stcPetType
        {
            War,
            Run
        }

        private stcPetState getPetInformation(stcPetType petType)
        {
            stcPetState data = new stcPetState();

            if (petType == stcPetType.War)
            {
                data.type = 0;
                data.maxState = 500;

                switch (Settings.SetWarPetType)
                {
                    case 1:
                        #region Попугайчик
                        data.type = 3;
                        break;
                        #endregion
                    case 2:
                        #region Кошечка
                        data.type = 1;
                        break;
                        #endregion
                    case 3:
                        #region Чихуахуа
                        data.type = 2;
                        break;
                        #endregion
                    case 4:
                        #region Доберман
                        data.type = 4;
                        break;
                        #endregion
                    case 5:
                        #region Овчарка
                        data.type = 7;
                        break;
                        #endregion
                    case 6:
                        #region Ротвейлер
                        data.type = 8;
                        break;
                        #endregion
                    case 7:
                        #region Мраморный дог
                        data.type = 21;
                        break;
                        #endregion
                    case 8:
                        #region Волк
                        data.type = 22;
                        break;
                        #endregion
                    case 9:
                        #region Лев
                        data.type = 80;
                        break;
                        #endregion
                    case 10:
                        #region Пантера
                        data.type = 23;
                        break;
                        #endregion
                    case 11:
                        #region Котёнок "Гаф"
                        data.type = 24;
                        data.maxState = 700;
                        break;
                        #endregion
                    case 12:
                        #region Чёрный дог
                        data.type = 26;
                        data.maxState = 700;
                        break;
                        #endregion
                    case 13:
                        #region Хомячок
                        data.type = 25;
                        data.maxState = 700;
                        break;
                        #endregion
                }
            }
            else
            {
                data.maxState = 750;

                switch (Settings.TrainRunPetType)
                {
                    case 1:
                        #region Кошечка
                        data.type = 19;
                        break;
                        #endregion
                    case 2:
                        #region Собачка
                        data.type = 20;
                        break;
                        #endregion
                    case 3:
                        #region Белочка
                        data.type = 18;
                        break;
                        #endregion
                    case 4:
                        #region Енот
                        data.type = 17;
                        break;
                        #endregion
                    case 5:
                        #region Лиса
                        data.type = 16;
                        break;
                        #endregion
                    case 6:
                        #region Волк
                        data.type = 15;
                        break;
                        #endregion
                    case 7:
                        #region Медведь
                        data.type = 13;
                        break;
                        #endregion
                    case 8:
                        #region Тигр
                        data.type = 14;
                        break;
                        #endregion
                    case 9:
                        #region Страус
                        data.type = 11;
                        break;
                        #endregion
                    case 10:
                        #region Кенгуру
                        data.type = 12;
                        break;
                        #endregion
                    case 11:
                        #region Единорог
                        data.type = 9;
                        break;
                        #endregion
                    case 12:
                        #region Пегас
                        data.type = 10;
                        break;
                        #endregion
                }
            }

            return data;
        }

        private void QuickPageLoading(bool bValue)
        {
            GoToPlace(MainWB, Place.Settings);
            frmMain.GetDocument(MainWB).GetElementById("ajax_on").SetAttribute("checked", bValue ? "True" : "");
            Wait(300, 1000);
            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[1], "submit");
            IsWBComplete(MainWB);
            if (!bValue) UpdateStatus("! " + DateTime.Now + " Для работы в режиме \"Быстрой загрузки страниц\" необходимо наличие и включённая эмуляция IE11!");
            UpdateMessageInfo(" Внимание был " + (bValue ? "включен" : "выключен") + " Режим быстрой загрузки страниц\" для оптимизации работы, согласно вашим настройкам!", true);
        }
        private void WriteLogFile(string Comment)
        {
            lock (LockWriteLog)
            {
                StreamWriter SW = new StreamWriter("BuG-Report\\History " + DateTime.Now.ToShortDateString() + ".txt", true, Encoding.Unicode);
                SW.WriteLine(Comment);
                SW.Close();
            }            
        }
        private stcWallet GetResources(WebBrowser WB, string[] ArrHtmlInfo, bool Wallet = false)
        {
            Match match;
            stcWallet Ret = new stcWallet();
            foreach (string Info in ArrHtmlInfo)
            {
                match = Regex.Match(Info, "((?<Star>star)|(?<Badge>badge)|(?<Oil>neft)|(?<Money>tugriki)|(?<Mobile>mobila)|(?<WTooth>tooth-white)|(?<GTooth>tooth-golden)|(?<PetGold>pet-golden.*)|(?<Ore>ruda)|(?<PowerGold>power.*)|(?<Honey>med))\"?>(?<Price>([0-9,])+)<");
                if (match.Groups["Money"].Success) { Ret.Money = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Ore"].Success) { Ret.Ore = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Oil"].Success) { Ret.Oil = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Honey"].Success) { Ret.Honey = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Star"].Success) { Ret.Star = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Badge"].Success) { Ret.Badge = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["Mobile"].Success) { Ret.Mobile = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["WTooth"].Success) { Ret.WhiteTooth = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["GTooth"].Success) { Ret.GoldTooth = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["PetGold"].Success) { Ret.PetGold = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
                if (match.Groups["PowerGold"].Success) { Ret.PowerGold = Convert.ToInt32(match.Groups["Price"].Value.Replace(",", "")); continue; }
            }
            if (Wallet) //Этих данных нет в берёзке!
            {
                UpdateMyInfo(WB);
                Ret.Money = Me.Wallet.Money;
                Ret.Ore = Me.Wallet.Ore;
                Ret.Oil = Me.Wallet.Oil;
                Ret.Honey = Me.Wallet.Honey;
            }
            return Ret;
        }
        private void GetPVPStats(WebBrowser WB) //Extract stats (Me vs Player Window)
        {
            BugReport("GetPVPStats");

            Regex regex;
            MatchCollection matches;

            IsWBComplete(WB);
            HtmlElement HtmlEl = frmMain.GetDocument(WB).GetElementById("content").GetElementsByTagName("H3")[0];

            regex = new Regex("arrived|resident|npc"); //Горожанин
            matches = regex.Matches(HtmlEl.InnerHtml);
            vsPlayer[0].Fraction = matches.Count == 2 ? matches[1].Value : "unknown";

            regex = new Regex("/player/([0-9])*/"); //URL
            matches = regex.Matches(HtmlEl.InnerHtml);
            Me.Player.URL = Settings.ServerURL + matches[0].Value;
            vsPlayer[0].URL = matches.Count == 2 ? Settings.ServerURL + matches[1].Value : null; //У NPC нет ссылок, игнорим!

            regex = new Regex("(?<Name>([^[\r\n])+)[[](?<Lvl>([0-9])+)[]]"); //Name + Level
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Name = matches[0].Groups["Name"].Value;
            vsPlayer[0].Name = matches[1].Groups["Name"].Value;
            Me.Player.Level = Convert.ToInt32(matches[0].Groups["Lvl"].Value);
            vsPlayer[0].Level = Convert.ToInt32(matches[1].Groups["Lvl"].Value);

            HtmlEl = frmMain.GetDocument(WB).GetElementById("content");
            regex = new Regex("(?<=Здоровье).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Health[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Health[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Сила).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Strength[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Strength[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Ловкость).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Dexterity[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Dexterity[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Выносливость).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Endurance[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Endurance[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Хитрость).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Cunning[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Cunning[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Внимательность).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Attentiveness[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Attentiveness[1] = Convert.ToInt32(matches[1].Value);

            regex = new Regex("(?<=Харизма).([0-9])*");
            matches = regex.Matches(HtmlEl.InnerText);
            Me.Player.Charisma[1] = Convert.ToInt32(matches[0].Value);
            vsPlayer[0].Charisma[1] = Convert.ToInt32(matches[1].Value);

            Me.Player.LifePkt = new string[2] { frmMain.GetDocument(WB).GetElementById("currenthp").InnerText, frmMain.GetDocument(WB).GetElementById("maxhp").InnerText };
            vsPlayer[0].LifePkt = new string[2] { Convert.ToString(10 * vsPlayer[0].Health[1] + 4 * vsPlayer[0].Endurance[1]), Convert.ToString(10 * vsPlayer[0].Health[1] + 4 * vsPlayer[0].Endurance[1]) }; //Невозможно извлечь? -высчитываем...
        }
        private void GetMyLife(WebBrowser WB)
        {
            BugReport("GetMyLife");

        ReTry:
            IsWBComplete(WB); //Проверка на клик-клик и прочее.
            try
            {
                //Извлекаем мои жизни.
                string[] LifePkt = new string[2]; //Инициализация
                LifePkt[0] = frmMain.GetDocument(WB).GetElementById("currenthp").InnerText;
                LifePkt[1] = frmMain.GetDocument(WB).GetElementById("maxhp").InnerText;

                if (LifePkt[0] != "" & LifePkt[1] != "") { Me.Player.LifePkt = new string[2] { LifePkt[0], LifePkt[1] }; return; }
            }
            catch
            {
                UpdateStatus("! " + DateTime.Now + " Проблема: - Не могу извлечь мои жизни!");
            }
            AnalysePlace(WB);
            goto ReTry;
        }        
        private void Wait(int MinMilliSeconds = 0, int MaxMilliSeconds = 0, String S = "")
        {
            //BugReport("Wait-MS");

            Random WaitMs = new Random();
            int Interval = WaitMs.Next(MinMilliSeconds, MaxMilliSeconds);
            DateTime DT = DateTime.Now.AddMilliseconds(Interval);
            Match match = Regex.Match(S, "^.(?= )"); //Это сообщение должно иметь иную приоритетность?
            if (S != "") UpdateStatus((match.Success ? match.Value : "#") + " " + DateTime.Now + (match.Success ? S.Replace(match.Value, "") : S) + DT.ToString("HH:mm:ss")); //Удаляем начало спец сообщения, оно уже есть в match, чтоб вставить дату.

            if (MainWB.InvokeRequired) //Функция запушена из вспомогательного потока.
            {
                Thread.Sleep(Interval);
            }
            else //Функция запушена из основного потока.
            {
                while (DateTime.Now <= DT)
                {
                    Application.DoEvents();
                }
            }
        }
        private void Wait(TimeSpan TS, String S = "", TimeOutAction TA = TimeOutAction.Blocked)
        {
            BugReport("Wait-TS : " + TS);

            DateTime DT = DateTime.Now;

            #region Уже нечего ждать, время истекло?
            if (TS < new TimeSpan()) return;
            #endregion
            
            DT += TS;
            Match match = Regex.Match(S, "^.(?= )"); //Это сообщение должно иметь иную приоритетность?
            if (S != "") UpdateStatus((match.Success ? match.Value : "#") + " " + DateTime.Now + (match.Success ? S.Replace(match.Value, "") : S) + DT.ToString("HH:mm:ss")); //Удаляем начало спец сообщения, оно уже есть в match, чтоб вставить дату.

            while (DateTime.Now <= DT)
            {
                if (MainWB.InvokeRequired) //Функция запушена из вспомогательного потока.
                {
                    Thread.Sleep(2000);
                    UseTimeOut(TA); //Передаём унаследованное действие
                }
                else //Функция запушена из основного потока.
                {
                    Application.DoEvents();
                }
            }
        }
        private bool TimeToGoGrpFight(GroupFightType GFT = GroupFightType.All, DateTime NextDT = new DateTime(), string Price = "tugriki:0", int InMinutes = 0)
        {
            BugReport("TimeToGoGrpFight");            
            
            Match match = Regex.Match(Price, "(?<Unit>(tugriki|ruda|neft|med)):(?<Cost>([0-9])+)");
            DateTime ServerDT = GetServerTime(MainWB);
            //Новый заход с новой дракой или нашёл драку которую можно провести до зарегестрированной драки или зарегистрирован хаос, переписываем более важной дракой.  
            if (NextDT != new DateTime() && (ServerDT > GrpFight.NextFightDT || (NextDT > ServerDT && (NextDT.AddMinutes(10) < GrpFight.NextFightDT || GrpFight.NextFightType == GroupFightType.Chaos)))) //Сохраняем время будующей драки!
            {
                GrpFight.NextFightType = GFT;
                GrpFight.Price = Price; //Запоминаем цену участия в бое.
                GrpFight.NextFightDT = NextDT; //Пора заносить новое время?                
            }
            
            UpdateMyInfo(MainWB);

            return ServerDT < GrpFight.NextFightDT && ServerDT.AddMinutes(InMinutes + 3) >= GrpFight.NextFightDT && (GFT == GroupFightType.All || GrpFight.NextFightType == GFT) &&
                   (
                       (match.Groups["Unit"].Value == "tugriki" && Me.Wallet.Money >= Convert.ToInt32(match.Groups["Cost"].Value)) 
                    || (match.Groups["Unit"].Value == "ruda" && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value))
                    || (match.Groups["Unit"].Value == "neft" && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value))
                   );
        }
        private bool TimeToStopAtack(NextTimeout NT, StopTimeoutType STT = StopTimeoutType.All)
        {
            BugReport("TimeToStopAtack");

            int Timeout = 0;
            
            switch (NT)
            {
                case NextTimeout.Chain:
                    Timeout = (int)Settings.FactoryChainCount;
                    break;
                case NextTimeout.Patrol:
                    Timeout = (int)Settings.PatrolTime;
                    break;
                case NextTimeout.Metro:
                    Timeout = 10;
                    break;
                case NextTimeout.Atack:
                    Timeout = (Me.Major.LastDT > GetServerTime(MainWB) ? 5 : 15);
                    break;
                case NextTimeout.Rat:
                    Timeout = 10 + (Me.Major.LastDT > GetServerTime(MainWB) ? 5 : 15); //Спуск в метро + Таймаут после нападения на крыску!
                    break;
                case NextTimeout.Fight:
                    switch (GrpFight.NextFightType)
                    {
                        case GroupFightType.Clan:
                            Timeout = 0; //Клановых стенок, ооочень мало, посему стараемся зайти в каждую!
                            break;
                        case GroupFightType.PVP:
                            Timeout = Me.RatHunting.Lvl > 25 ? 5 : 7; //Примерное 5-7 минут нужно для драки в PVP.
                            break;
                        case GroupFightType.Ore:
                            Timeout = 12;  //Примерное 12 минут нужно для драки в рудной стенке.
                            break;
                        case GroupFightType.Group:
                        case GroupFightType.Chaos:
                        default: Timeout = 7;  //Примерное 7 минут нужно для драки в хаоте.
                            break;
                    }                    
                    break;
                case NextTimeout.OilLeninFight:
                    Timeout = 10;
                    break;
            }

            return (   ((STT == StopTimeoutType.GrpFight || STT == StopTimeoutType.All) && TimeToGoGrpFight(GroupFightType.All, new DateTime(), GrpFight.Price, (NT == NextTimeout.Chain || NT == NextTimeout.Metro || NT == NextTimeout.Patrol || NT == NextTimeout.Rat) ? Timeout : 0))
                    || ((STT == StopTimeoutType.RatHunting || STT == StopTimeoutType.All) && Settings.SearchRat && Settings.SearchRatIgnoreAll && DateTime.Now.AddMinutes(Timeout) >= Me.RatHunting.NextDT && (!Me.RatHunting.Stop || DateTime.Now.AddMinutes(Timeout) >= Me.RatHunting.RestartDT))
                    || ((STT == StopTimeoutType.OilLenin || STT == StopTimeoutType.All) && Settings.GoOilLenin && DateTime.Now.AddMinutes(Timeout) >= Me.OilLeninHunting.RestartDT && !Me.OilLeninHunting.RestartDT.Equals(new DateTime()))
                    || ((STT == StopTimeoutType.Mafia || STT == StopTimeoutType.All) && Settings.GoGroupFightMafia && GrpFight.Mafia.FightFound)
                   ) && !Me.Trauma.Stop;
        }           
        private bool WaitDrugEated(WebBrowser WB, HtmlElement HtmlEl)
        {
            BugReport("WaitDrugEated");

            object[] Obj = new object[2];

            Match match = Regex.Match(HtmlEl.Parent.InnerHtml, "(?<=data-id=\"?)([0-9])+"); //data-id=\"92464957\"
            if (match.Success)
            {
                Obj[0] = frmMain.GetJavaVar(WB, "moswar.items['" + match.Value + "'].btn['0'].innerText");
                if (((string) Obj[0]).Contains("уже")) return true; //Уже использовано, незачем продолжать.; //Выпить / Съесть

                Obj[0] = frmMain.GetJavaVar(WB, "moswar.items['" + match.Value + "'].count['0'].innerText"); //Запоминаем количество до поедания                
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                frmMain.InvokeMember(WB, HtmlEl, "click");  // HtmlEl.InvokeMember("click");              
                do
                {
                    IsWBComplete(WB, 1000, 1500);
                    Obj[1] = frmMain.GetJavaVar(WB, "moswar.items['" + match.Value + "']" + (Obj[0] == null ? "" : ".count['0'].innerText")); //Для коктейлей проверяем наявность самого эллемента, а не количества
                    if (MonitorDT < DateTime.Now) return false; //Время отведённое под объедание истекло, покушать не удалось!
                }
                while (Obj[1] != null && (Obj[0] == null ? true : Obj[1].Equals(Obj[0]))); //Количество изменилось, совсем не осталось? (для коктэйлей только совсем не осталось!)
                return true;
            }
            return false;
        }
        private bool CheckImmun(ImmunAction IA)
        {
            IsWBComplete(MainWB);
            switch (IA)
            {
                case ImmunAction.Duels:
                    BugReport("CheckImmun.Duels");
                    DateTime ServerDT = GetServerTime(MainWB);
                    DateTime LastDT = ServerDT;
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith("/phone/duels/" + LastDT.ToString("yyyyMMdd") + "/"))
                    {
                        frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/");
                        IsWBComplete(MainWB);
                    }                    
                    int PageNr = 2;
                    do
                    {
                        if (frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("table").Count > 1) //Есть дуэли?
                        {
                            #region Сбор информации на страничке
                            foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("table")[1].GetElementsByTagName("tr"))
                            {
                                if (H.InnerHtml == null) break;
                                Match match = Regex.Match(H.InnerHtml, "date\"?>(?<Time>([0-9. :])+)<([\\s\\S])+(Вы напали на|Вы вступили в схватку с)"); //Выискиваем все наши нападения

                                if (match.Success)
                                {
                                    LastDT = Convert.ToDateTime(match.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                                    if (LastDT > ServerDT.AddDays(-1))
                                    {
                                        Array.Resize<DateTime>(ref Me.ArrDuelsDT, (Me.ArrDuelsDT == null ? 1 : Me.ArrDuelsDT.Count<DateTime>() + 1));
                                        Me.ArrDuelsDT[Me.ArrDuelsDT.Count<DateTime>() - 1] = Convert.ToDateTime(match.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                                    }
                                    else break;
                                }
                            }
                            #endregion
                        }                        
                        #region Листание страничек
                        string[] PageInfo = GetArrClassHtml(MainWB, "$(\"#content .block-rounded .num\");", "innerText");
                        if (PageInfo.Count<string>() == 0 || Convert.ToInt32(PageInfo[PageInfo.Count<string>() - 1]) < PageNr)
                        {
                            //frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/" + (Me.ArrDuelsDT == null ? LastDT : Me.ArrDuelsDT[Me.ArrDuelsDT.Count<DateTime>() - 1]).AddDays(-1).ToString("yyyyMMdd") + "/");
                            LastDT = LastDT.AddDays(-1);
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/" + LastDT.ToString("yyyyMMdd") + "/");                            
                            PageNr = 2; //Последняя страницка, переходим на прошлый день!
                        }
                        else frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/" + LastDT.ToString("yyyyMMdd") + "/" + PageNr++ + "/"); //else frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/" + (Me.ArrDuelsDT == null ? LastDT : Me.ArrDuelsDT[Me.ArrDuelsDT.Count<DateTime>() - 1]).ToString("yyyyMMdd") + "/" + PageNr++ + "/"); //
                        IsWBComplete(MainWB);
                        #endregion
                    } while (LastDT.Date > ServerDT.AddDays(-2).Date); //Это уже позавчера, выходим!
                    break;
                case ImmunAction.Mona:
                    BugReport("CheckImmun.Mona");
                    foreach (HtmlElement HtmlEl in frmMain.GetDocument(MainWB).GetElementById("personal").GetElementsByTagName("A"))
                    {
                        if (Regex.IsMatch(HtmlEl.OuterHtml, "Новые дуэли") || Me.Thimbles.StartDT == new DateTime()) //При первом старте тоже парсить, не важно есть ли новая дуэль!
                        {
                            Me.Thimbles.StartDT = GetServerTime(MainWB); //Инициализация, если не будет найдено нападений или не включен иммунитет, можно сразу сливать!
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/duels/");
                            IsWBComplete(MainWB);
                            #region Нет проведённых дуэлей?
                            if (frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("table").Count == 1) return false;
                            #endregion
                            #region Сбор информации на страничке
                            foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("table")[1].GetElementsByTagName("tr"))
                            {
                                if (H.InnerHtml == null) break;
                                Match match = Regex.Match(H.InnerHtml, "date\"?>(?<Time>([0-9. :])+)<([\\s\\S])+(href[=]\"/player/(?<Id>([0-9])+)/\">(?<Name>([^<])+)|(?<Name>Оборотень в погонах)).*[[](?<Lvl>([0-9])+)[]].*напал на вас"); //
                                if (match.Success) //Обнаружил нападение на меня!
                                {
                                    #region Иммунитет
                                    if (Settings.ThimblesImmunity)
                                    {
                                        Me.Thimbles.StartDT = Convert.ToDateTime(match.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(44);
                                        UpdateStatus("~ " + DateTime.Now + " Обнаружен иммунитет до: " + DateTime.Now.Add(Me.Thimbles.StartDT - GetServerTime(MainWB)).ToString("HH:mm:ss") + " (Московское время: " + Me.Thimbles.StartDT.ToString("HH:mm:ss") + ")");
                                    }                                    
                                    #endregion
                                    #region Заказ в ОК
                                    stcPlayerInfo PI = new stcPlayerInfo { Name = match.Groups["Name"].Value, Level = match.Groups["Lvl"].Value, Id = match.Groups["Id"].Value };
                                    if (PI.Id != "" && Settings.HCRevenge) //Пробуем заказать в ОК?
                                    {
                                        string URL = "/huntclub/revenge/" + PI.Id + "/"; //Ссылка на заказ игрока в ОК (Без указания сервера ибо, при мултифрэйме выглядит так: www.moswar.eu//huntclub/revenge/4521/)
                                        frmMain.NavigateURL(MainWB, Settings.ServerURL + URL);
                                        IsWBComplete(MainWB);
                                        if (frmMain.GetDocumentURL(MainWB).Contains(URL)) 
                                        {
                                            int RevengeCost = Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("hunting-order-form-cost-tugriki").InnerText.Replace(",",""));
                                            UpdateMyInfo(MainWB);
                                            if (frmMain.GetDocument(MainWB).GetElementById("nickname-error").InnerText == null && RevengeCost <= Settings.HCRevengeMaxMoney && Me.Wallet.Money > RevengeCost)
                                            {
                                                frmMain.InvokeMember(MainWB, frmMain.GetElementsById(MainWB, "form-submit")[1], "click"); //Нажимаем кнопку заказа игрока! nickname-error
                                                IsWBComplete(MainWB);
                                                if (frmMain.GetElementsById(MainWB, "alert-title")[1].InnerText.Contains("Wanted!")) UpdateStatus("@ " + DateTime.Now + " Сдал " + PI.Name + "[" + PI.Level + "] в ОК на сосиски!");                                                
                                            }
                                        }
                                    }
                                    #endregion
                                    return true;
                                }
                            }
                            #endregion
                            break;
                        }
                    }                    
                    break;
                case ImmunAction.Tooth:
                    BugReport("CheckImmun.Tooth");
                    if (Me.ClanWarInfo.Pacifism != null)
                    {
                        TimeSpan CurrentTime = GetServerTime(MainWB).TimeOfDay;
                        foreach (stcClanImmun CI in Me.ClanWarInfo.Pacifism)
                        {
                            if (IsTimeInTimespan(TimeSpan.Parse(CI.Start), TimeSpan.Parse(CI.Stop), CurrentTime)) return true; //Сейчас время ненападения
                        }
                    }
                    break;
            }
            return false;
        }       
        private bool LevelUP(WebBrowser WB)
        {
            BugReport("LevelUP");

            HtmlElement HtmlEl;

            HtmlEl = frmMain.GetDocument(WB).GetElementsByTagName("Form")[0];
            if (HtmlEl.InnerText.Contains("Вперед, к новым победам!")) { frmMain.InvokeMember(WB, HtmlEl, "submit"); IsWBComplete(WB); Me.Player.Level++; return true; }
            return false;
        }
        private bool Safe(SafeAction SA)
        {
            BugReport("Safe");

            switch (SA)
            {
                case SafeAction.Check:
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith(Settings.ServerURL + "/home/")) GoToPlace(MainWB, Place.Home);
                    
                    HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementsByTagName("Table")[0].GetElementsByTagName("td")[1].GetElementsByTagName("IMG");
                    if (HC.Count >= 1) //Есть ли картинка сейфа?
                    {
                        Me.Safe.LastDT = GetServerTime(MainWB).AddHours(1); //Инициализация, если не получится считать время годности сейфа
                        Match match = Regex.Match(ReadToolTip(MainWB, HC[0]), "(?<=Годен до: )([0-9 .:])+"); //Сохраняет: 8000 монет|Годен до: 26.03.2012 11:37"
                        if (match.Success) Me.Safe.LastDT = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                      }
                    else //сейфа нет
                    {
                        UpdateMyInfo(MainWB);
                        if (Settings.BuySafe && Me.Wallet.Ore >= 24) return Safe(SafeAction.Buy);
                    }
                    Me.Safe.Stop = true; //Больше не нужно проверять.
                    break;
                case SafeAction.Buy:
                    if (BuyItems(MainWB, ShopItems.Safe))
                    {
                        Me.Safe.LastDT = GetServerTime(MainWB).AddHours(1); //Инициализация, если не получится считать время годности сейфа
                        Safe(SafeAction.Check);
                    } 
                    else return false; //Не удалось купить видимо мало ресурсов ...
                    break;
            }
            return true;
        }
        private bool FeedTaborPet(TaborPetAction TPA)
        {
            BugReport("TaborPet");

            switch (TPA)
            { 
                case TaborPetAction.Check:
                    GoToPlace(MainWB, Place.Camp);                    
                    HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("divAutoIncome");
                    if (HtmlEl.Style == null || Regex.IsMatch(HtmlEl.Style, "display: block", RegexOptions.IgnoreCase)) //Блок виден, только тогда, когда собачка накормленна (FIX: ибо сейчас там стоит дата из будующего, хотя собака не кормленна)
                    {
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("spanAutoTill");
                        if (HtmlEl != null) Me.TaborPet.LastDT = Convert.ToDateTime(HtmlEl.InnerText, CultureInfo.CreateSpecificCulture("ru-RU")).AddDays(1); //Срок оплаты показывается по день включительно, соответсвено истекает в 00:00 следующего дня.
                        if (Me.TaborPet.LastDT >= GetServerTime(MainWB))
                        {
                            UpdateStatus("* " + DateTime.Now + " «Гречка» клянчил мёд, но он же собака а не медведь, видимо ещё сыт.");
                            return true; //Порядок, время следуюшей оплаты известно.
                        }                    
                    }                    
                    if (Settings.FeedTaborPet) return FeedTaborPet(TaborPetAction.Feed);
                    break;
                case TaborPetAction.Feed:
                    UpdateMyInfo(MainWB);
                    #region Считываем цену кормёжки
                    foreach (Match match in Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("divAutoPrice").InnerHtml, "class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)"))
                    {
                        switch (match.Groups["Unit"].Value)
                        { 
                            case "tugriki":
                                Me.TaborPet.PriceTugriki = Convert.ToInt32(match.Groups["Cost"].Value);
                                break;
                            case "ruda":
                                Me.TaborPet.PriceOre = Convert.ToInt32(match.Groups["Cost"].Value);
                                break;
                            case "oil":
                                Me.TaborPet.PriceOil = Convert.ToInt32(match.Groups["Cost"].Value);
                                break;
                            case "med":
                                Me.TaborPet.PriceMed = Convert.ToInt32(match.Groups["Cost"].Value);
                                break;                
                        } 
                    }
                    #endregion
                    if (Me.TaborPet.PriceMed == 0 && Me.TaborPet.PriceTugriki <= Me.Wallet.Money && Me.TaborPet.PriceOre <= Me.Wallet.Ore && Me.TaborPet.PriceOil <= Me.Wallet.Oil)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Подкормил «Гречку», сел ждать дивиденды.");
                        frmMain.GetDocument(MainWB).GetElementById("buttonAutoGold").InvokeMember("onclick");
                        IsAjaxCompleteEx(MainWB, "spanAutoTill"); //Появление срока годности. //IsWBComplete(MainWB);
                        return FeedTaborPet(TaborPetAction.Check);
                    }                    
                    break;
            }
            return false;
        }
        private bool SovetBuyAgitator(SovetAgitatorAction SAA)
        {
            BugReport("SovetBuyAgitator");

            Match match;

            switch (SAA)
            { 
              case  SovetAgitatorAction.Check:
                    GoToPlace(MainWB, Place.Sovet, "/career");
                    HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("divAutoIncome");
                    if (HtmlEl != null && (match = Regex.Match(HtmlEl.InnerText,"(?<=Активно до: )([0-9.])+")).Success) Me.SovetAgitator.LastDT = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddDays(1); //Срок оплаты показывается по день включительно, соответсвено истекает в 00:00 следующего дня.
                    if (Me.SovetAgitator.LastDT >= GetServerTime(MainWB))
                    {
                        UpdateStatus("* " + DateTime.Now + " Братва! Работаем, агитируем, мёд не клянчим, не расслабляемся, всё ведь уплачено!");
                        return true; //Порядок, время следуюшей оплаты известно.
                    }
                    if (frmMain.GetDocument(MainWB).GetElementById("divAutoPrice") == null)
                    {
                        UpdateStatus("# " + DateTime.Now + " Агитаторы похоже на обеде, зайду позднее!");
                        Me.SovetAgitator.LastDT = GetServerTime(MainWB).AddMinutes(new Random().Next(40,180));
                        return false;
                    }
                    if (Settings.SovetBuyAgitator) return SovetBuyAgitator(SovetAgitatorAction.Buy);
                  break;
                case SovetAgitatorAction.Buy:
                  UpdateMyInfo(MainWB);
                  #region Считываем цену агитирования
                  foreach (Match m in Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("divAutoPrice").InnerHtml, "class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)"))
                  {
                      switch (m.Groups["Unit"].Value)
                      {
                          case "tugriki":
                              Me.SovetAgitator.PriceTugriki = Convert.ToInt32(m.Groups["Cost"].Value);
                              break;
                          case "ruda":
                              Me.SovetAgitator.PriceOre = Convert.ToInt32(m.Groups["Cost"].Value);
                              break;
                          case "oil":
                              Me.SovetAgitator.PriceOil = Convert.ToInt32(m.Groups["Cost"].Value);
                              break;
                          case "med":
                              Me.SovetAgitator.PriceMed = Convert.ToInt32(m.Groups["Cost"].Value);
                              break;
                      }
                  }
                  #endregion
                  if (Me.SovetAgitator.PriceMed == 0 && Me.SovetAgitator.PriceTugriki <= Me.Wallet.Money && Me.SovetAgitator.PriceOre <= Me.Wallet.Ore && Me.SovetAgitator.PriceOil <= Me.Wallet.Oil)
                  {
                      UpdateStatus("@ " + DateTime.Now + " Мальчики, вот бабло... рассредоточились, проталкиваем мою кандидатуру!");
                      frmMain.GetDocument(MainWB).GetElementById("buttonAutoGold").InvokeMember("onclick");
                      IsAjaxCompleteEx(MainWB, "divAutoIncome"); //Появление срока годности. //IsWBComplete(MainWB);
                      return SovetBuyAgitator(SovetAgitatorAction.Check);
                  }
                  break;
            }
            return true;
        }
        private void PlayAzazella()
        {
            BugReport("PlayAzazella");

            GoToPlace(MainWB, Place.Camp, "/gypsy");
            #region Вычисление максимального таймаута
            TimeSpan TSTimeout = new TimeSpan();
            TimeSpan.TryParse(frmMain.GetDocument(MainWB).GetElementById("timeout").InnerText, out TSTimeout); //Запоминаем сколько ещё продлится таймаут                        
            if (TimeToStopAtack(Settings.GoPatrol && !Me.Patrol.Stop ? NextTimeout.Patrol : (Settings.GoMetro && !Me.Rat.Stop ? NextTimeout.Metro : NextTimeout.Atack), StopTimeoutType.RatHunting))
            {
                TSTimeout = ((TSTimeout > Me.RatHunting.NextDT - DateTime.Now) ? TSTimeout : Me.RatHunting.NextDT - DateTime.Now).Subtract(new TimeSpan(0, 0, 20 + (Settings.UseWearSet ? 25 : 0))); //Ждать либо (до конца текущего таймаута либо до крысиного) + 20 секунд резервируем под поедание крысиных допингов и 25 секунд переодевание               
            }
            Me.Azazella.PlayTillDT = DateTime.Now.Add(TSTimeout).AddSeconds(-10); //Играть максимум до этого времени!
            #endregion
        ReTry:
            IsWBComplete(MainWB);
            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/gypsy/")) return;
            object Info = frmMain.GetJavaVar(MainWB, "$(\"#content .log-wrapper .amulet-reward\").text()");
            if ((string)Info == "") //игра уже началась!
            {
                frmMain.GetJavaVar(MainWB, "Gypsy.feelLucky();");
                IsWBComplete(MainWB);
                if (Settings.AzazellaFastPlay) frmMain.NavigateURL(MainWB, Settings.ServerURL + "/camp/gypsy/");
                else Wait(7000, 8000);               
                goto ReTry;
            }
            if (Convert.ToInt32(Info) > Settings.minAzazellaGold && !Me.Azazella.EnoughGold) 
            {
                UpdateStatus("$ " + DateTime.Now + " Ванга, погадай мне! Вот твоё золотишко!");
                Me.Azazella.EnoughGold = true; //Запоминаем, что денег хватало, если прибежим на след. паузе доигрывать...
            }            

            if (Me.Azazella.EnoughGold)
            {
                //Учитываем % падения метеорита
                Me.Azazella.TreasurePrc = (frmMain.GetDocument(MainWB).GetElementById("divMeteorProgress").Style != "display: none;")
                    ? Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#divMeteorProgress .percent\").attr(\"style\")"), "([0-9])+").Value)
                    : 0;                
                Me.Azazella.NextDT = Settings.AzazellaTreasure ? DateTime.Now.AddSeconds((double)(Settings.AzazellaTreasureChance - Me.Azazella.TreasurePrc) * 5) : DateTime.Now.AddSeconds(30);

                if (Settings.AzazellaTreasure && Me.Azazella.NextDT > DateTime.Now) 
                {
                    UpdateStatus("@ " + DateTime.Now + " Не Ванга, вертай всё в зад! Рано ещё, метеорит вот только на " + Me.Azazella.TreasurePrc + "% показался!");
                    return; //Играть ещё рановато, слишком низкий %.
                }                

                if (DateTime.Now >= Me.Azazella.PlayTillDT && !Settings.AzazellaTreasure) //Не давать сбежать, если шанс падения метеорита более 95% 
                {
                    UpdateStatus("@ " + DateTime.Now + " Блин время поджало... может ещё забегу!");
                    return;                
                }

                if (Settings.AzazellaTreasure) UpdateStatus("$ " + DateTime.Now + " Ванга, поспеши! Шанс падения то уже " + Me.Azazella.TreasurePrc + "%, а это уже не шутка!");

                if (Settings.PlayAzazella75 && Convert.ToInt32(Info) > 75) 
                {
                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("startGameButton1"), "click"); //Играем за 75 золота
                    goto ReTry;
                }
                if (Settings.PlayAzazella25 && Convert.ToInt32(Info) > 25)
                {
                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("startGameButton0"), "click"); //Играем за 25 золота
                    goto ReTry;
                }               
            }
            Me.Azazella.EnoughGold = false; //Всё, золота слишком мало!
            Me.Azazella.NextDT = DateTime.Now.AddHours(6); //Запоминаем, время когда кончилось золото, зайдём не ранее чем через 6 часов!       
        }
        private bool PigProtection(PigProtectionAction PPA)
        {
            BugReport("PigProtection");            

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/home/")) GoToPlace(MainWB, Place.Home);
            switch (PPA)
            { 
                case PigProtectionAction.Check:
                    Match match;
                    Me.PigProtection.Stop = true; //Больше не нужно проверять.
                    foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementsByTagName("form"))
                    {
                        if (H.InnerText != null && (match = Regex.Match(H.InnerText, "Защита от подарков с негативными эффектами включена до(?<Date>([0-9 .:])+)[.]")).Success) 
                        {
                            Me.PigProtection.LastDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(1);
                            UpdateStatus("# " + DateTime.Now + " Внимание: обнаружил защиту от зайцев, кто такие не знаю, но работает до: " + Me.PigProtection.LastDT + " (Московское время)");
                            return true;
                        } 
                    }
                    if (Me.PigProtection.LastDT < GetServerTime(MainWB) && Settings.PigProtection) PigProtection(PigProtectionAction.Pay);                   
                    break;
                case PigProtectionAction.Pay:
                    HtmlElement HtmlEl;
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("anti-form");
                    if (HtmlEl != null)
                    {
                        UpdateMyInfo(MainWB);
                        if (HtmlEl.GetElementsByTagName("button").Count == 2 && Me.Wallet.Honey >= 10)
                        {
                            UpdateStatus("@ " + DateTime.Now + " Не знаю кто такие эти \"зайцы\", но раз просили позаботится о защите, забочусь!");
                            frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click");
                            IsWBComplete(MainWB, 1000, 1500);
                            Me.PigProtection.LastDT = GetServerTime(MainWB).AddMinutes(15); //Это время должно будет быть ереписано функцией Check, если всё ок! 
                            PigProtection(PigProtectionAction.Check);
                        }
                    }
                    else Me.PigProtection.LastDT = GetServerTime(MainWB).AddMinutes(15);
                    break;            
            }
            return false;
        }
        private bool Major(MajorAction MA)
        {
            switch (MA)
            { 
                case MajorAction.Check:
                    GoToPlace(MainWB, Place.Stash);
                    Match match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .stash-major\").text()"), "Ваш статус мажора закончится(([\\s])+)?(?<Time>([0-9 .:])+)[.]");
                    if (match.Groups["Time"].Success) Me.Major.LastDT = Convert.ToDateTime(match.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                    Me.Major.Stop = true; //Больше не нужно проверять.                   
                    return match.Groups["Time"].Success;
                case MajorAction.Buy:
                    UpdateMyInfo(MainWB);
                    int iH = Me.Wallet.Honey;
                    if (Me.Wallet.Honey >= (Me.Major.LastDT > GetServerTime(MainWB) ? 17 : 22))
                    {
                        GoToPlace(MainWB, Place.Stash, "/becomemajor", false);
                        #region Проверка, получилась ли закупка
                        if (!Major(MajorAction.Check))
                        {
                            UpdateMyInfo(MainWB);
                            if (iH > Me.Wallet.Honey)
                            {
                                UpdateStatus("! " + DateTime.Now + " Не рассказывайте мне сказок..., я платил за мажора, значит я им буду!");
                                Me.Major.LastDT = GetServerTime(MainWB).AddDays(7);
                            }
                        }
                        #endregion
                    }
                    return true;
            }
            return true;
        }
        private void Torture(bool Use)
        {
            BugReport("Torture");

            HtmlElement HtmlEl;

            if (!Expert.DoNotProofTorture)
            {
                IsWBComplete(MainWB);
                if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/player/$")) GoToPlace(MainWB, Place.Player);
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-antisafe-btn"); //Паяльник
                if (HtmlEl != null && Regex.Match(HtmlEl.OuterHtml, "(?<=checkbox.*)([0-9])(?=[&]quot)").Value == (Use && !Expert.DoNotUseAntiSafe ? "0" : "1"))  //checkbox&quot;,&quot;v&quot;:&quot;0&quot;
                {                    
                    frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon/" + HtmlEl.GetAttribute("data-id") + "/\", type: \"post\", data: {\"inventory\": " + HtmlEl.GetAttribute("data-id") + ", \"unlocked\": " + (Use ? "1" : "0") + "}, dataType: \"json\"});");                   
                    Wait(1000, 1500);
                }
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-utjug-btn"); //Утюг
                if (HtmlEl != null && Regex.Match(HtmlEl.OuterHtml, "(?<=checkbox.*)([0-9])(?=[&]quot)").Value == (Use && !Expert.DoNotUseIron ? "0" : "1"))  //checkbox&quot;,&quot;v&quot;:&quot;0&quot;
                {                    
                    frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon/" + HtmlEl.GetAttribute("data-id") + "/\", type: \"post\", data: {\"inventory\": " + HtmlEl.GetAttribute("data-id") + ", \"unlocked\": " + (Use ? "1" : "0") + "}, dataType: \"json\"});");
                    Wait(1000, 1500);
                }
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-angle_grinder-btn"); //Болгарка
                if (HtmlEl != null && Regex.Match(HtmlEl.OuterHtml, "(?<=checkbox.*)([0-9])(?=[&]quot)").Value == (Use && !Expert.DoNotUseAngleGrinder ? "0" : "1"))  //checkbox&quot;,&quot;v&quot;:&quot;0&quot;
                {                    
                    frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon/" + HtmlEl.GetAttribute("data-id") + "/\", type: \"post\", data: {\"inventory\": " + HtmlEl.GetAttribute("data-id") + ", \"unlocked\": " + (Use ? "1" : "0") + "}, dataType: \"json\"});");
                    Wait(1000, 1500);
                }
            }            
        }
        private void IsAjaxCompleteEx(WebBrowser WB, string WaitForId, bool ShowId = true)
        {
            BugReport("IsAjaxCompleteEx");

            DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
            do //Ожидаю пока ajax обновит контекст
            {
                if (WB.InvokeRequired) Thread.Sleep(200);
                else Application.DoEvents();
            }
            while ((ShowId ? (frmMain.GetDocument(WB).GetElementById(WaitForId) == null) : frmMain.GetDocument(WB).GetElementById(WaitForId) != null) && MonitorDT > DateTime.Now);
            if (MonitorDT < DateTime.Now) UpdateStatus("! " + DateTime.Now + " Внимание: Ajax timeout detected!");
            WB.Tag = "Ready";
        }
        private void _IsAjaxComplete(WebBrowser WB, int WaitMinMs = 0, int WaitMaxMs = 0)
        {
            BugReport("IsAjaxComplete");

            string Info; //(string)frmMain.GetJavaVar(WB, "document.readyState")

            DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
            do
            {
                if (!WB.InvokeRequired) Application.DoEvents();
                Info = (string)frmMain.GetJavaVar(WB, "$(\".loading-top\").attr(\"style\");");
            }
            while (!frmMain.IsBusy(WB) && Info != null && Info.Contains("none") && MonitorDT > DateTime.Now);
            if (DebugMode) BugReport("* Ajax Loading Started");
            
            do
            {
                if (WB.InvokeRequired) Thread.Sleep(100);
                else Application.DoEvents();
                Info = (string)frmMain.GetJavaVar(WB, "$(\".loading-top\").attr(\"style\");");                
            }
            while ((frmMain.IsBusy(WB) || Info == null || Info.Contains("block")) && MonitorDT > DateTime.Now);
            if (DebugMode) BugReport("* Ajax Loading Completed");

            if (MonitorDT < DateTime.Now) UpdateStatus("! " + DateTime.Now + " Внимание: Ajax timeout detected!");
            WB.Tag = "Ready";
            Wait(WaitMinMs, WaitMaxMs);
        }
        private bool Agent(AgentAction AA)
        {
            BugReport("Agent"); 
            
            GoToPlace(MainWB, Place.Police);

            #region Есть Агенты?
            HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("aktivist");
            if (HtmlEl == null) //Побои Агентов завершены!
            {
                Me.AgentHunting.LastDT = GetServerTime(MainWB);
                Me.AgentHunting.StartDT = Me.AgentHunting.LastDT.AddDays(1).Date;                
                Me.AgentHunting.Stop = true;
                return false;
            }
            #endregion
            switch (AA)
            {
                case AgentAction.Check:
                    UpdateMyInfo(MainWB);
                    Regex regex = new Regex("Ваша корочка народного активиста действительна до (?<DT>([0-9.: ])+)[.]"); //Ваша корочка народного активиста действительна до 26.12.2011 17:24
                    Match match = regex.Match(frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[4].InnerText);
                    if (match.Success)
                    {
                        Me.AgentHunting.StartDT = Convert.ToDateTime(match.Groups["DT"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Время когда придётся продлить побои Агентов!
                        Me.AgentHunting.Stop = false;
                        return true;
                    }
                    else
                    {
                        if ((Me.Player.Level >= 10 & Me.Wallet.Oil >= 60) || (Me.Player.Level < 10 & Me.Wallet.Ore >= 12)) return Agent(AgentAction.Pay); //Побои Агентов завершены!
                        else { Me.AgentHunting.StartDT.AddHours(1); Me.AgentHunting.LastDT = GetServerTime(MainWB); Me.AgentHunting.Stop = true; } //Сейчас нет ресурсов, перепроверить через час!
                    }
                    break;
                case AgentAction.Pay:
                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("agent"), "click");
                    IsWBComplete(MainWB);
                    return Agent(AgentAction.Check);
            }
            return false;
        }
        private bool Werewolf(WerewolfAction WA)
        {
            BugReport("Werewolf");

            GoToPlace(MainWB, Place.Police);

            Match match;
            HtmlElementCollection HC;

            switch (WA)
            {
                case WerewolfAction.Check:
                    UpdateMyInfo(MainWB);
                    HC = frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[2].GetElementsByTagName("Button");
                    HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("werewolf");
                    if (HtmlEl != null)
                    {
                        match = Regex.Match(HtmlEl.InnerText, "([0-9:])+");
                        string[] Time = match.Value.Split(':'); //HH:mm:ss
                        //Время когда придётся продлить побои Оборотнем!
                        Me.WerewolfHunting.StartDT = GetServerTime(MainWB).Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.                        
                        Me.WerewolfHunting.Stop = false;
                        #region Продлеваем ещё на час, если бегаем не за погоны!
                        if (Settings.UseWerewolf && Settings.WerewolfPrice != 0 && Me.WerewolfHunting.StartDT < GetServerTime(MainWB).AddMinutes(20) && Me.Wallet.Honey >= 19)
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[2].GetElementsByTagName("Button")[1], "onclick"); //Покупаем / переходим к оборотню
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                        }
                        #endregion
                        return true;
                    }
                    else
                    {                       
                        //Погон или 19 мёда одна звезда + за каждую последующую еше по 9 мёда.
                        if (Me.Player.Level >= 5 && (HC.Count == 2) || (Settings.WerewolfPrice != 0 && Me.Wallet.Honey >= Settings.WerewolfPrice * 10 + Settings.WerewolfPrice * 9)) return Werewolf(WerewolfAction.Pay); //Покупка оборотня!                            
                        else { Me.WerewolfHunting.Stop = Me.WerewolfHunting.StartDT <= GetServerTime(MainWB); Me.WerewolfHunting.StartDT = GetServerTime(MainWB).AddHours(1); } //Сейчас нет ресурсов, перепроверить через час!
                    }
                    break;
                case WerewolfAction.Pay:
                    Me.Player.Level = Convert.ToInt32((string)frmMain.GetJavaVar(MainWB, "player['level']")); //Считываем мой настоящий уровень, ибо я сейчас всё ещё могу быть оборотнем!
                    frmMain.GetDocument(MainWB).GetElementById("werewolfLevel").SetAttribute("value", (Me.Player.Level - Settings.WerewolfLevel).ToString()); //Устанавливаем требуемый уровень оборотня.
                    
                    HC = frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[2].GetElementsByTagName("Button");
                    if (Settings.WerewolfPrice == 0 && HC.Count != 2) return false; //На всякий пожарный ещё раз убеждаемся, что у нас есть погон перед покупкой оборотня!

                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[2].GetElementsByTagName("Button")[0], "onclick"); //Покупаем / переходим к оборотню
                    IsWBComplete(MainWB); //IsAjaxComplete(MainWB);

                    if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/werewolf/$")) GoToPlace(MainWB, Place.Police, "/werewolf"); //Если не забросило на страничку с оборотнем, заходим сами!

                    Me.WerewolfHunting.Val = Regex.Matches((string)frmMain.GetJavaVar(MainWB, "$(\"#content .borderdata\").html()"), "icon-star-filled").Count; //Нужно проверить
                    #region Покупка нужного уровня оборотня
                    for (int Count = Me.WerewolfHunting.Val; Count < Settings.WerewolfPrice; Count++)
                    {
                        frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("Button")[1], "onclick"); //Покупаем / переходим к оборотню
                        IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                    }
                    #endregion
                    Me.WerewolfHunting.Val = Regex.Matches((string)frmMain.GetJavaVar(MainWB, "$(\"#content .borderdata\").html()"), "icon-star-filled").Count; //Нужно проверить                                    
                    return Werewolf(WerewolfAction.Check);                    
            }
            return false;
        }       
        private void HCStartMultiThread()
        {
            do
            {
                #region 1-th Victim
                if (Thread.CurrentThread.Equals(HCThread[0]))
                {
                    if (vsPlayer[0].URL == null) Thread.Sleep(100);
                    else { HCMultithreadAttack(vsPlayer[0].URL); vsPlayer[0].URL = null; }
                }
                #endregion
                #region 2-th Victim
                if (Thread.CurrentThread.Equals(HCThread[1]))
                {
                    if (vsPlayer[1].URL == null) Thread.Sleep(100);
                    else { HCMultithreadAttack(vsPlayer[1].URL); vsPlayer[1].URL = null; }
                }
                #endregion
            }
            while (Me.HCHunting.Search);
        }
        private bool HCMultithreadAttack(string URL)
        {
            BugReport("HC-Attack");

            Regex regex;
            Match match;
            int AttackRetries = 0;
            WebBrowser WB = null;
            stcPlayer Player = new stcPlayer();

            #region 1-th Victim
            if (Thread.CurrentThread.Equals(HCThread[0]))
            {
                WB = HelpWB[0];
                Player = vsPlayer[0];
            }
            #endregion
            #region 2-th Victim
            if (Thread.CurrentThread.Equals(HCThread[1]))
            {
                WB = HelpWB[1];
                Player = vsPlayer[1];
            }
            #endregion
            if (WB == null) return false; //Функция запущена не из вспомогательного потока, уходим!

        ReTry:
            if (AttackRetries > 15) return false;
            #region Слишком мало жизней для драк, прервать для лечения
            if (Me.HCHunting.Search ? IsHPLow(WB, 99, false) : false)
            {
                UpdateStatus("@ " + DateTime.Now + " Чёт голова разболелась ..., похоже мне в неё настучали, пойду глотну ка таблЭток.");
                Me.HCHunting.Search = false; //Стоп
                return false;
            }
            #endregion
            #region Открытие странички игрока и извлечение статов
            if (AttackRetries == 0 || !frmMain.GetDocumentURL(WB).Contains(URL)) //При ретраях не загружать и не считать заново
            {
                frmMain.NavigateURL(WB, URL);
                IsWBComplete(WB);
                regex = new Regex("/player/([0-9])+"); //
                if (regex.IsMatch(frmMain.GetDocumentURL(WB))) GetPStats(WB, ref Player);
                else return false; //Теоретически не может такого быть!
            }
            else Player.LifePkt = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#pers-player-info .life\").text()"), "(?<=Жизни:)([0-9 /\\s])+").Value.Split('/'); //При повторном заходе, только сверяем жизни перед нападением            
            #endregion
            #region Достаточно ли у игрока жизней для нападения?
            if (Convert.ToDouble(Player.LifePkt[0]) / Convert.ToDouble(Player.LifePkt[1]) * 100 < 30) //Можно начинать бить от 35%, посему накрайняк пробуем дождаться
            {
                frmMain.RemoveListItem(LBBlackWanted, Player.Name); //Убрать из чёрного списка, его уже побили в след раз проверю!               
                return false;
            }
            #endregion
            if (AttackRetries == 0) //При ретраях не загружать и не считать заново
            {
                if (IsPlayerWeak(ref Player, Convert.ToInt32(Settings.minHCStatDiff), true))
                    frmMain.NavigateURL(WB, URL.Replace("player", "alley/attack"));
                else return false; //Покинуть функцию и не удалять из черного списка, слишком силён!
            }
            else frmMain.NavigateURL(WB, URL.Replace("player", "alley/attack"));

            IsWBComplete(WB);
            #region Аттака проведена успешно?
            regex = new Regex("(?<Fight>/alley/fight/([0-9])+/)|(?<Player>/player/([0-9])+/)|(?<Quest>/quest/)|(?<Login>" + Settings.ServerURL + "/$)");
            match = regex.Match(frmMain.GetDocumentURL(WB));
            if (match.Groups["Fight"].Success)
            {
                Me.HCHunting.Search = false; //Битва проведена, стоп!
                if (AnalyseFight(WB)) frmMain.RemoveListItem(LBBlackWanted, Player.Name); //Удаление из черного списка, побили успешно!                
                return true; //
            }
            if (match.Groups["Player"].Success) { AttackRetries++; goto ReTry; }
            if (match.Groups["Quest"].Success) return LevelUP(WB); //Похоже получил уровень!
            if (match.Groups["Login"].Success) { WebLogin(WB); }
            #endregion
            return false;
        }
        private void Contact(WebBrowser WB, ContactAction CA, ContactType CT, string ClanName = "", stcPlayerInfo[] PI = null)
        {
            BugReport("Contact");

            Regex regex;
            HtmlElement HtmlEl;            
            string URL, CType, Txt;
            int iMaxPage, iCurrPage;

            switch (CT)
            {
                case ContactType.Victim:
                    CType = "victim";
                    URL = Settings.ServerURL + "/phone/contacts/victims/";
                    Txt = "кормушки!";
                    break;
                case ContactType.Enemy:
                    CType = "enemy";
                    URL = Settings.ServerURL + "/phone/contacts/enemies/";
                    Txt = "враги!";
                    break;
                default:
                    URL = "";
                    CType = "";
                    Txt = "";
                    break;
            }

            switch (CA)
            {
                case ContactAction.AddPlayer:
                    Wait(5000, 10000); //Симуляция ожидания перед добавлением в контакт.
                    frmMain.NavigateURL(WB, PI[0].URL.Replace("player", "phone/contacts/add"));
                    IsWBComplete(WB, 1000, 1500);
                    //frmMain.GetDocument(WB).All["info"].SetAttribute("value", "gad"); //Добавить комментарий. 
                    frmMain.GetDocument(WB).All["type"].SetAttribute("value", CType);
                    frmMain.InvokeMember(WB, frmMain.GetDocument(WB).All["contactForm"], "submit");
                    IsWBComplete(WB, 500, 1500);
                    UpdateStatus("~ " + DateTime.Now + " Игрок: " + PI[0].Name + "[" + PI[0].Level + "] - " + PI[0].URL + " добавлен в " + Txt);
                    break;
                case ContactAction.AddClan:
                    #region Это первый вход?
                    if (frmMain.GetDocumentURL(WB).IndexOf(URL) == -1) //Это первый вход?
                    {
                        GoToPlace(WB, Place.Phone, "/contacts");
                        if (CType != "victim") { frmMain.NavigateURL(WB, URL); IsWBComplete(WB); } //Жертвы начальная страничка, ко всему иному сначала нужно перейти!
                    }
                    #endregion
                    frmMain.GetDocument(WB).GetElementById("contactClan").SetAttribute("value", ClanName);
                    frmMain.InvokeMember(WB, frmMain.GetDocument(WB).All["contactForm"], "submit");
                    IsWBComplete(WB, 1000, 2000);
                    UpdateStatus("@ " + DateTime.Now + " Клан: " + ClanName + " добавлен в " + Txt);
                    break;
                case ContactAction.DeletePlayer:
                    #region Это первый вход?
                    if (frmMain.GetDocumentURL(WB).IndexOf(URL) == -1) //Это первый вход?
                    {
                        GoToPlace(WB, Place.Phone, "/contacts");
                        if (CType != "victim") { frmMain.NavigateURL(WB, URL); IsWBComplete(WB); } //Жертвы начальная страничка, ко всему иному сначала нужно перейти!
                    }
                    #endregion

                    iMaxPage = 1; //Инициализация, оставшиеся страницы будут считаны уже после удаления
                    for (iCurrPage = 1; iCurrPage <= iMaxPage; iCurrPage++)
                    {
                        bool PlayerFound = false;
                        if (frmMain.GetDocumentURL(WB).IndexOf(URL + iCurrPage) == -1) //Это первый вход?
                        {
                            frmMain.NavigateURL(WB, URL + iCurrPage); //Переходим от странички к страничке...                            
                        }
                        IsWBComplete(WB, 500, 2000); //Симуляция ожидания перед удалением из контакта.
                        if (frmMain.GetDocument(WB).GetElementsByTagName("TABLE").Count < 4) break; //Проверяю, есть ли вообще таблица с никами игроков?

                        HtmlEl = frmMain.GetDocument(WB).GetElementsByTagName("TABLE")[3];
                        foreach (stcPlayerInfo PlayerInfo in PI) //Перебор всех игроков из списка
                        {
                            foreach (HtmlElement H in HtmlEl.GetElementsByTagName("INPUT"))
                            {
                                if (Regex.IsMatch(H.OuterHtml, "value=\"?" + PlayerInfo.Id)) //Это галочка искомого игрока?
                                {
                                    PlayerFound = true; //Минимум одна галочка была поставленна
                                    UpdateStatus("~ " + DateTime.Now + " Игрок: " + PlayerInfo.Name + "[" + PlayerInfo.Level + "] - " + PlayerInfo.URL + " удалён из " + Txt);
                                    H.InvokeMember("click"); //Ставим галочку маркировки для удаления
                                    Wait(100, 300);
                                    break;
                                }
                            }
                        }
                        if (PlayerFound)
                        {
                            HtmlEl.GetElementsByTagName("button")[0].InvokeMember("click"); //Удалить отмеченные контакты
                            IsWBComplete(WB, 500, 1000);
                            iCurrPage--; //На этой страничке были удалены игроки, произошел сдвиг, снова проверяем!
                        }
                        HtmlEl = frmMain.GetDocument(WB).GetElementById("content");
                        //Если 1-ый элемент с конца стрелка -> страничка не одна!
                        if (HtmlEl.All[HtmlEl.All.Count - 1].GetAttribute("className") == "arrow") iMaxPage = Convert.ToInt32(HtmlEl.All[HtmlEl.All.Count - 3].InnerText);  //3-ий эллемент с конца -> кол-во страниц. 
                    }              
                    break;
                case ContactAction.DeleteClan:                    
                    #region Это первый вход?
                    if (frmMain.GetDocumentURL(WB).IndexOf(URL) == -1) //Это первый вход?
                    {
                        GoToPlace(WB, Place.Phone, "/contacts");
                        if (CType != "victim") { frmMain.NavigateURL(WB, URL); IsWBComplete(WB); } //Жертвы начальная страничка, ко всему иному сначала нужно перейти!
                    }
                    #endregion

                    regex = new Regex("title[=]\"?" + ClanName + "\"? src.*href[=]\"/player/(?<Id>([0-9])+)/\"[>](?<Name>.*)[<]/a", RegexOptions.IgnoreCase); //В названии некоторых кланов есть " " в некоторых нет..                   

                    iMaxPage = 1; //Инициализация, оставшиеся страницы будут считаны уже после удаления
                    for (iCurrPage = 1; iCurrPage <= iMaxPage; iCurrPage++)
                    {
                        bool PlayerFound = false;
                        frmMain.NavigateURL(WB, URL + iCurrPage); //Переходим от странички к страничке...                     
                        IsWBComplete(WB, 500, 2000); //Симуляция ожидания перед удалением из контакта.
                        if (frmMain.GetDocument(WB).GetElementsByTagName("TABLE").Count < 4) break; //Проверяю, есть ли вообще таблица с никами игроков?

                        if (ClanName != null) UpdateStatus("@ " + DateTime.Now + " Начинаю зачистку клана \"" + ClanName + "\"");

                        HtmlEl = frmMain.GetDocument(WB).GetElementsByTagName("TABLE")[3];
                        MatchCollection matches = regex.Matches(HtmlEl.InnerHtml); //Ищем есть ли игроки с этого клана?
                        foreach (Match m in matches)
                        {
                            foreach (HtmlElement H in HtmlEl.GetElementsByTagName("INPUT"))
                            {
                                if (H.OuterHtml.Contains("value=" + m.Groups["Id"].Value)) //Это галочка искомого игрока?
                                {
                                    PlayerFound = true; //Минимум одна галочка была поставленна
                                    H.InvokeMember("click"); //Ставим галочку маркировки для удаления
                                    Wait(100, 300);
                                    break;
                                }
                            }
                        }
                        if (PlayerFound)
                        {
                            HtmlEl.GetElementsByTagName("button")[0].InvokeMember("click"); //Удалить отмеченные контакты
                            IsWBComplete(WB, 500, 1000);
                            iCurrPage--; //На этой страничке были удалены игроки, произошел сдвиг, снова проверяем!
                        }
                        HtmlEl = frmMain.GetDocument(WB).GetElementById("content");
                        //Если 1-ый элемент с конца стрелка -> страничка не одна!
                        if (HtmlEl.All[HtmlEl.All.Count - 1].GetAttribute("className") == "arrow") iMaxPage = Convert.ToInt32(HtmlEl.All[HtmlEl.All.Count - 3].InnerText);  //3-ий эллемент с конца -> кол-во страниц. 
                    }              
                    break;
                case ContactAction.DeleteAll:
                    GoToPlace(WB, Place.Phone, "/contacts");
                    if (CType != "victim") { frmMain.NavigateURL(WB, URL); IsWBComplete(WB); } //Жертвы начальная страничка, ко всему иному сначала нужно перейти!
                    if (frmMain.GetDocument(WB).GetElementsByTagName("TABLE").Count >= 4) //Проверяю, есть ли вообще таблица с никами игроков?
                    {
                        frmMain.InvokeMember(WB, frmMain.GetDocument(WB).GetElementById("clear-list"), "submit");
                        IsWBComplete(WB, 1000, 1500);
                    }
                    break;
            }
        }
        private void CheckCollections(WebBrowser WB, ref stcPlayer P) //Para kolekcij nedodelana ... zdu logi
        {
            BugReport("CheckCollections");

            string sRegex = "Ожерелье из выбитых зубов|Значок шерифа M10|«Оленьи рога» M10|Золотая монета M10|Руль «Enzo Ferrari» M10|Телефонная будка M10|Аквариум M10|Щелкунчик M10";
            sRegex += "|Золотые слитки M10|Нефтекачка M10|Спутник M10|Счастливая рулетка M10|Шашечки M10|Золотой бюст Вождя М10|«Пиковая дама» М10|Кубок чемпиона М10|Старая деревянная дрезина М10";
            sRegex += "|Конституция РФ М10|Макет Нерезиновая-Сити М10|Старое радио М10|Золотая цепуха М10|Боксерская груша М10|Оскар М10|Вымпел М10|Букварь М10|Тюремная Роба М10|Элегантный коктейль М10";
            sRegex += "|Двуглавый орел М10|Золотой сейф М10|Рубин Аль-Хорезми М10|Зеленый мир! М10|Монетка с Фейерабендом М10|Нашивка РЖД М10|Глобус М10|Кисти М10|Копилка М10|Теплый ламповый фотоаппарат М10";
            sRegex += "|Магический шар М10|Модель спирали ДНК М10|Ваучер М10|Бляха начальника охраны М10|Бабочка судьбы М10|Ручной медведь М10|Осколок метеорита М10|Герб Великой Страны М10";

            #region Процентные ставки
            Regex regex = new Regex(sRegex);
            MatchCollection matches = regex.Matches(frmMain.GetDocumentHtmlTextEx(WB));
            if (matches.Count != 0)
            {
                stcPlayer x = new stcPlayer();
                x.Health = new int[1];
                x.Strength = new int[1];
                x.Dexterity = new int[1];
                x.Endurance = new int[1];
                x.Cunning = new int[1];
                x.Attentiveness = new int[1];
                x.Charisma = new int[1];
                for (int i = 0; i != matches.Count; i++)
                {
                    switch (matches[i].Value)
                    {
                        case "Ожерелье из выбитых зубов":
                            x.Health[0] += P.Health[0] / 20;
                            x.Strength[0] += P.Strength[0] / 20;
                            x.Dexterity[0] += P.Dexterity[0] / 20;
                            x.Endurance[0] += P.Endurance[0] / 20;
                            x.Cunning[0] += P.Cunning[0] / 20;
                            x.Attentiveness[0] += P.Attentiveness[0] / 20;
                            x.Charisma[0] += P.Charisma[0] / 20;
                            sRegex = Regex.Replace(sRegex, "Ожерелье из выбитых зубов[|]", "");
                            break;
                        case "Значок шерифа M10":
                            x.Endurance[0] += P.Endurance[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Значок шерифа M10[|]", "");
                            break;
                        case "«Оленьи рога» M10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Strength[0] += P.Strength[0] / 25;
                            sRegex = Regex.Replace(sRegex, "«Оленьи рога» M10[|]", "");
                            break;
                        case "Золотая монета M10":
                            x.Cunning[0] += P.Cunning[0] / 25;
                            x.Attentiveness[0] += P.Attentiveness[0] / 50;
                            x.Charisma[0] += 24;
                            sRegex = Regex.Replace(sRegex, "Золотая монета M10[|]", "");
                            break;
                        case "Руль «Enzo Ferrari» M10":
                            x.Dexterity[0] += P.Dexterity[0] / 50;
                            x.Endurance[0] += P.Endurance[0] / 50;
                            x.Attentiveness[0] += P.Attentiveness[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Руль «Enzo Ferrari» M10[|]", "");
                            break;
                        case "Телефонная будка M10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Attentiveness[0] += P.Attentiveness[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Телефонная будка M10[|]", "");
                            break;
                        case "Аквариум M10":
                            x.Attentiveness[0] += P.Attentiveness[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Аквариум M10[|]", "");
                            break;
                        case "Щелкунчик M10":
                            x.Cunning[0] += P.Cunning[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Щелкунчик M10[|]", "");
                            break;
                        case "Золотые слитки M10":
                            x.Cunning[0] += P.Cunning[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Золотые слитки M10[|]", "");
                            break;
                        case "Нефтекачка M10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Strength[0] += P.Strength[0] / 50;
                            x.Dexterity[0] += P.Dexterity[0] / 50;
                            x.Endurance[0] += P.Endurance[0] / 50;
                            x.Cunning[0] += P.Cunning[0] / 50;
                            x.Attentiveness[0] += P.Attentiveness[0] / 50;
                            x.Charisma[0] += P.Charisma[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Нефтекачка M10[|]", "");
                            break;
                        case "Спутник M10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Strength[0] += P.Strength[0] / 50;
                            x.Dexterity[0] += P.Dexterity[0] / 50;
                            x.Endurance[0] += P.Endurance[0] / 50;
                            x.Cunning[0] += P.Cunning[0] / 50;
                            x.Attentiveness[0] += P.Attentiveness[0] / 50;
                            x.Charisma[0] += P.Charisma[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Спутник M10[|]", "");
                            break;
                        case "Счастливая рулетка M10":
                            x.Health[0] += P.Health[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Счастливая рулетка M10[|]", "");
                            break;
                        case "Шашечки M10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Strength[0] += P.Strength[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Шашечки M10[|]", "");
                            break;
                        case "Золотой бюст Вождя М10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Endurance[0] += P.Endurance[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Золотой бюст Вождя М10[|]", "");
                            break;
                        case "«Пиковая дама» М10":
                            x.Endurance[0] += P.Endurance[0] / 33;
                            sRegex = Regex.Replace(sRegex, "«Пиковая дама» М10[|]", "");
                            break;
                        case "Кубок чемпиона М10":
                            x.Cunning[0] += P.Cunning[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Кубок чемпиона М10[|]", "");
                            break;
                        case "Старая деревянная дрезина М10":
                            x.Health[0] += P.Health[0] / 33;
                            x.Endurance[0] += P.Endurance[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Старая деревянная дрезина М10[|]", "");
                            break;
                        case "Конституция РФ М10":
                            x.Health[0] += P.Health[0] / 50;
                            x.Strength[0] += P.Strength[0] / 33;
                            x.Dexterity[0] += P.Dexterity[0] / 50;
                            x.Endurance[0] += P.Endurance[0] / 50;
                            x.Charisma[0] += P.Charisma[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Конституция РФ М10[|]", "");
                            break;
                        case "Макет Нерезиновая-Сити М10":
                            x.Health[0] += P.Health[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Макет Нерезиновая-Сити М10[|]", "");
                            break;
                        case "Старое радио М10":
                            x.Strength[0] += P.Strength[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Старое радио М10[|]", "");
                            break;
                        case "Золотая цепуха М10":
                            x.Strength[0] += P.Strength[0] / 33;
                            x.Cunning[0] += P.Cunning[0] / 33;
                            x.Attentiveness[0] += P.Attentiveness[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Золотая цепуха М10[|]", "");
                            break;
                        case "Боксерская груша М10":
                            x.Strength[0] += P.Strength[0] / 33;
                            x.Dexterity[0] += P.Dexterity[0] / 33;
                            x.Cunning[0] += P.Cunning[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Боксерская груша М10[|]", "");
                            break;
                        case "Оскар М10":
                            x.Health[0] += x.Health[0] / 100;
                            x.Attentiveness[0] += x.Attentiveness[0] / 100;
                            x.Charisma[0] += x.Charisma[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Оскар М10[|]", "");
                            break;
                        case "Вымпел М10":
                            x.Health[0] += x.Health[0] / 50;
                            x.Endurance[0] += x.Endurance[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Вымпел М10[|]", "");
                            break;
                        case "Букварь М10":
                            x.Strength[0] += x.Strength[0] / 50;
                            x.Attentiveness[0] += x.Attentiveness[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Букварь М10[|]", "");
                            break;
                        case "Тюремная Роба М10":
                            x.Health[0] += x.Health[0] / 100;
                            x.Strength[0] += x.Strength[0] / 100;
                            x.Cunning[0] += x.Cunning[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Тюремная Роба М10[|]", "");
                            break;
                        case "Элегантный коктейль М10":
                            x.Health[0] += x.Health[0] / 50;
                            x.Charisma[0] += x.Charisma[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Элегантный коктейль М10[|]", "");
                            break;
                        case "Двуглавый орел М10":
                            x.Dexterity[0] += x.Dexterity[0] / 100;
                            x.Strength[0] += x.Strength[0] / 100;
                            x.Charisma[0] += x.Charisma[0] / 20;
                            sRegex = Regex.Replace(sRegex, "Двуглавый орел М10[|]", "");
                            break;
                        case "Золотой сейф М10":
                            x.Strength[0] += x.Strength[0] / 50;
                            x.Dexterity[0] += x.Dexterity[0] / 33;
                            x.Cunning[0] += x.Cunning[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Золотой сейф М10[|]", "");
                            break;
                        case "Рубин Аль-Хорезми М10":
                            x.Health[0] += x.Health[0] / 50;
                            x.Strength[0] += x.Strength[0] / 100;
                            x.Dexterity[0] += x.Dexterity[0] / 100;
                            x.Cunning[0] += x.Cunning[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Рубин Аль-Хорезми М10[|]", "");
                            break;
                        case "Зеленый мир! М10":
                            x.Dexterity[0] += x.Dexterity[0] / 50;
                            x.Cunning[0] += x.Cunning[0] / 50;
                            x.Charisma[0] += x.Charisma[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Зеленый мир! М10[|]", "");
                            break;
                        case "Монетка с Фейерабендом М10":
                            x.Health[0] += x.Health[0] / 100;
                            x.Strength[0] += x.Strength[0] / 100;
                            x.Endurance[0] += x.Endurance[0] / 100;
                            x.Dexterity[0] += x.Dexterity[0] / 100;
                            x.Cunning[0] += x.Cunning[0] / 100;
                            x.Attentiveness[0] += x.Attentiveness[0] / 100; 
                            x.Charisma[0] += x.Charisma[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Монетка с Фейерабендом М10[|]", "");
                            break;
                        case "Нашивка РЖД М10":
                            x.Health[0] += x.Health[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Нашивка РЖД М10[|]", "");
                            break;
                        case "Глобус М10":
                            x.Endurance[0] += x.Endurance[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Глобус М10[|]", "");
                            break;
                        case "Кисти М10":
                            x.Strength[0] += x.Strength[0] / 33;
                            x.Attentiveness[0] += x.Attentiveness[0] / 50;
                            x.Dexterity[0] += x.Dexterity[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Кисти М10[|]", "");
                            break;
                        case "Копилка М10":
                            x.Cunning[0] += x.Cunning[0] / 33;
                            x.Health[0] += x.Health[0] / 50;
                            x.Endurance[0] += x.Endurance[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Копилка М10[|]", "");
                            break;
                        case "Фирменный знак М10":
                            x.Health[0] += x.Health[0] / 50;
                            x.Attentiveness[0] += x.Attentiveness[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Фирменный знак М10[|]", "");
                            break;
                        case "Теплый ламповый фотоаппарат М10":
                            x.Dexterity[0] += x.Dexterity[0] / 50;
                            x.Attentiveness[0] += x.Attentiveness[0] / 33;
                            P.Charisma[0] += 25;
                            sRegex = Regex.Replace(sRegex, "Теплый ламповый фотоаппарат М10[|]", "");
                            break;
                        case "Магический шар М10":
                            x.Cunning[0] += x.Cunning[0] / 25;
                            x.Attentiveness[0] += x.Attentiveness[0] / 25;
                            x.Charisma[0] += x.Charisma[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Магический шар М10[|]", "");
                            break;
                        case "Модель спирали ДНК М10":
                            x.Health[0] += x.Health[0] / 25;
                            x.Strength[0] += x.Strength[0] / 50;
                            x.Endurance[0] += x.Endurance[0] / 25;
                            sRegex = Regex.Replace(sRegex, "Модель спирали ДНК М10[|]", "");
                            break;
                        case "Ваучер М10":
                            x.Cunning[0] += x.Cunning[0] / 33;
                            x.Charisma[0] += x.Charisma[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Ваучер М10[|]", "");
                            break;
                        case "Бляха начальника охраны М10":
                            x.Endurance[0] += x.Endurance[0] / 50;
                            x.Attentiveness[0] += x.Attentiveness[0] / 50;
                            sRegex = Regex.Replace(sRegex, "Бляха начальника охраны М10[|]", "");
                            break;
                        case "Бабочка судьбы М10":
                            x.Dexterity[0] += x.Dexterity[0] / 50;
                            x.Attentiveness[0] += x.Attentiveness[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Бабочка судьбы М10[|]", "");
                            break;
                        case "Ручной медведь М10":
                            x.Strength[0] += x.Strength[0] / 50;
                            x.Endurance[0] += x.Endurance[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Ручной медведь М10[|]", "");
                            break;
                        case "Осколок метеорита М10":
                            x.Health[0] += x.Health[0] / 33;
                            x.Strength[0] += x.Strength[0] / 33;
                            x.Endurance[0] += x.Endurance[0] / 33;
                            x.Dexterity[0] += x.Dexterity[0] / 33;
                            x.Cunning[0] += x.Cunning[0] / 33;
                            x.Attentiveness[0] += x.Attentiveness[0] / 33;
                            sRegex = Regex.Replace(sRegex, "Осколок метеорита М10[|]?", "");
                            break;
                        case "Герб Великой Страны М10":
                            x.Health[0] += x.Health[0] / 33;                            
                            x.Cunning[0] += x.Cunning[0] / 33;
                            x.Attentiveness[0] += x.Attentiveness[0] / 100;
                            sRegex = Regex.Replace(sRegex, "Герб Великой Страны М10[|]?", "");
                            break;

                    }
                }
                P.Health[0] += x.Health[0];
                P.Strength[0] += x.Strength[0];
                P.Dexterity[0] += x.Dexterity[0];
                P.Endurance[0] += x.Endurance[0];
                P.Cunning[0] += x.Cunning[0];
                P.Attentiveness[0] += x.Attentiveness[0];
                P.Charisma[0] += x.Charisma[0];
            }
            #endregion
            
            sRegex = Regex.Replace(sRegex, " (M|М)10", ""); //Коллекции у которых не нашлось М10 ("М" Латинская, в первых коллекциях, "М" Русская в последних коллекциях)

            sRegex += (sRegex != "" ? "|" : "") + "Консоль Тетрис|Веник|Светофор|Штурвал|Арфа|Морская ракушка|Электрочайник|Матрёшка|Ёлка|Че Гевара|Лидерская трибуна";
            sRegex = "(?<Name>(" + sRegex + "))( .(?<Rang>[0-9]))?";
  
            #region Обычные ставки
            regex = new Regex(sRegex);
            matches = regex.Matches(frmMain.GetDocumentHtmlTextEx(WB));
            for (int i = 0; i < matches.Count; i++)
            {
                int x;
                switch (matches[i].Groups["Name"].Value)
                {
                    case "Консоль Тетрис":
                        P.Attentiveness[0] += 7;
                        P.Attentiveness[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Нашивка РЖД":
                        P.Health[0] += 7;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Оскар":
                        P.Charisma[0] += 7;
                        P.Charisma[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        i++; //В описании самой Регалии снова упоминается "золотой сейф" он следущий в матчез, перепрыгиваем!
                        break;
                    case "Веник":
                        P.Dexterity[0] += 7;
                        P.Dexterity[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Светофор":
                        P.Endurance[0] += 7;
                        P.Endurance[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Штурвал":
                        P.Cunning[0] += 7;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Арфа":
                        P.Cunning[0] += 7;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Глобус":
                        P.Endurance[0] += 7;
                        P.Endurance[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Морская ракушка":
                        P.Attentiveness[0] += 7;
                        P.Attentiveness[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Значок шерифа":
                        P.Endurance[0] += 14;
                        P.Endurance[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "«Оленьи рога»":
                        P.Strength[0] += 14;
                        P.Strength[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Вымпел":
                        P.Health[0] += 14;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Золотая монета":
                        P.Cunning[0] += 5; P.Charisma[0] += 14;
                        P.Charisma[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0; 
                        break;
                    case "Электрочайник":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        break;
                    case "Руль «Enzo Ferrari»":
                        P.Dexterity[0] += 14;
                        P.Dexterity[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Телефонная будка":
                        P.Strength[0] += 7;
                        P.Strength[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Аквариум":
                        P.Attentiveness[0] += 7;
                        P.Attentiveness[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Щелкунчик":
                        P.Dexterity[0] += 7;
                        P.Dexterity[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Матрёшка":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        break;
                    case "Золотые слитки":
                        P.Cunning[0] += 14;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Букварь":
                        P.Attentiveness[0] += 7;
                        P.Attentiveness[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Ёлка":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        break;
                    case "Нефтекачка":
                        P.Health[0] += 3; P.Dexterity[0] += 3; P.Strength[0] += 3; P.Endurance[0] += 3; P.Cunning[0] += 3; P.Attentiveness[0] += 3; P.Charisma[0] += 3;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x / 2;
                            P.Strength[0] += x / 2 + (x % 2);
                            P.Dexterity[0] += x / 2;
                            P.Endurance[0] += x / 2;
                            P.Cunning[0] += x / 2 + (x % 2);
                            P.Attentiveness[0] += x / 2 + (x % 2);
                            P.Charisma[0] += x / 2;
                        }                        
                        break;
                    case "Спутник":
                        P.Health[0] += 3; P.Dexterity[0] += 3; P.Strength[0] += 3; P.Endurance[0] += 3; P.Cunning[0] += 3; P.Attentiveness[0] += 3; P.Charisma[0] += 3;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x / 2;
                            P.Strength[0] += x / 2 + (x % 2);
                            P.Dexterity[0] += x / 2;
                            P.Endurance[0] += x / 2;
                            P.Cunning[0] += x / 2 + (x % 2);
                            P.Attentiveness[0] += x / 2 + (x % 2);
                            P.Charisma[0] += x / 2;
                        }
                        break;
                    case "Счастливая рулетка":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        break;
                    case "Шашечки":
                        P.Strength[0] += 7;
                        P.Strength[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Тюремная роба":
                        P.Health[0] += 1; P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 1; P.Attentiveness[0] += 1; P.Charisma[0] += 1;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value) / 2;
                            P.Health[0] += x / 2 + (x % 2);
                            P.Strength[0] += x / 2 + (x % 2);
                            P.Dexterity[0] += x / 2 + (x % 2);
                            P.Endurance[0] += x / 2;
                            P.Cunning[0] += x / 2;
                            P.Attentiveness[0] += x / 2;
                            P.Charisma[0] += x / 2;
                        }
                        break;
                    case "Че Гевара":
                        P.Health[0] += 1; P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 1; P.Attentiveness[0] += 1; P.Charisma[0] += 1;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value) / 2;
                            P.Health[0] += x / 2 + (x % 2);
                            P.Strength[0] += x / 2 + (x % 2);
                            P.Dexterity[0] += x / 2 + (x % 2);
                            P.Endurance[0] += x / 2;
                            P.Cunning[0] += x / 2;
                            P.Attentiveness[0] += x / 2;
                            P.Charisma[0] += x / 2;
                        }
                        break;
                    case "Кисти":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x;
                            P.Strength[0] += x;
                            P.Dexterity[0] += x;
                            P.Endurance[0] += x;
                            P.Cunning[0] += x;
                            P.Attentiveness[0] += x;
                            P.Charisma[0] += x;
                        }
                        break;
                    case "Копилка":
                        P.Health[0]++; P.Dexterity[0]++; P.Strength[0]++; P.Endurance[0]++; P.Cunning[0]++; P.Attentiveness[0]++; P.Charisma[0]++;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x;
                            P.Strength[0] += x;
                            P.Dexterity[0] += x;
                            P.Endurance[0] += x;
                            P.Cunning[0] += x;
                            P.Attentiveness[0] += x;
                            P.Charisma[0] += x;
                        }
                        break;
                    case "Элегантный коктейль":
                        P.Cunning[0] += 7;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Двуглавый орел":
                        P.Health[0] += 7;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0; 
                        break;
                    case "Золотой бюст Вождя":
                        P.Endurance[0] += 7; P.Health[0] += 7;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Endurance[0] += x;
                            P.Health[0] += x;
                        }
                        break;
                    case "«Пиковая дама»":
                        P.Endurance[0] += 15;
                        P.Endurance[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Кубок чемпиона":
                        P.Cunning[0] += 10;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Золотой сейф":
                        P.Dexterity[0] += 5;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Dexterity[0] += 3; P.Cunning[0] += 5; break;
                                case 2: P.Dexterity[0] += 7; P.Cunning[0] += 9; break;
                                case 3: P.Dexterity[0] += 11; P.Cunning[0] += 14; break;
                                case 4: P.Dexterity[0] += 15; P.Cunning[0] += 18; break;
                                case 5: P.Dexterity[0] += 17; P.Cunning[0] += 22; break;
                                case 6: P.Strength[0] += 5; P.Dexterity[0] += 20; P.Cunning[0] += 25; break;
                                case 7: P.Strength[0] += 7; P.Dexterity[0] += 22; P.Cunning[0] += 27; break;
                                case 8: P.Strength[0] += 10; P.Dexterity[0] += 24; P.Cunning[0] += 30; break;
                                case 9: P.Strength[0] += 15; P.Dexterity[0] += 27; P.Cunning[0] += 32; break;
                            }
                        }
                        i++; //В описании самой Регалии снова упоминается "золотой сейф" он следущий в матчез, перепрыгиваем!
                        break;
                    case "Теплый ламповый фотоаппарат":
                        P.Dexterity[0] += 6; P.Attentiveness[0] += 14;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Dexterity[0] += x * 4;
                            P.Attentiveness[0] += x * 4;
                        }
                        break;
                    case "Старая деревянная дрезина":
                        P.Health[0] += 10; P.Endurance[0] += 10;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x * 2;
                            P.Endurance[0] += x * 2;
                        }
                        break;
                    case "Зеленый мир!":
                        P.Dexterity[0] += 10; P.Cunning[0] += 10; P.Charisma[0] += 10;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Dexterity[0] += x * 2;
                            P.Cunning[0] += (x / 2) * 4;
                            P.Charisma[0] += x * 2;
                        }
                        break;
                    case "Лидерская трибуна":
                        P.Attentiveness[0] += 10; P.Charisma[0] += 17;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Attentiveness[0] += x;
                            P.Charisma[0] += x * 2;
                        }
                        break;
                    case "Конституция РФ":
                        P.Strength[0] += 15;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Strength[0] += 3; P.Health[0] += 10; break;
                                case 2: P.Strength[0] += 6; P.Health[0] += 10; P.Dexterity[0] += 10; break;
                                case 3: P.Strength[0] += 9; P.Health[0] += 10; P.Dexterity[0] += 10; P.Endurance[0] += 10; break;
                                case 4: P.Strength[0] += 12; P.Health[0] += 12; P.Dexterity[0] += 12; P.Endurance[0] += 12; P.Charisma[0] += 5; break;
                                case 5: P.Strength[0] += 15; P.Health[0] += 15; P.Dexterity[0] += 15; P.Endurance[0] += 15; P.Charisma[0] += 10; break;
                                case 6: P.Strength[0] += 18; P.Health[0] += 18; P.Dexterity[0] += 18; P.Endurance[0] += 18; P.Charisma[0] += 15; break;
                                case 7: P.Strength[0] += 21; P.Health[0] += 21; P.Dexterity[0] += 21; P.Endurance[0] += 21; P.Charisma[0] += 20; break;
                                case 8: P.Strength[0] += 24; P.Health[0] += 24; P.Dexterity[0] += 24; P.Endurance[0] += 24; P.Charisma[0] += 25; break;
                                case 9: P.Strength[0] += 27; P.Health[0] += 27; P.Dexterity[0] += 27; P.Endurance[0] += 27; P.Charisma[0] += 27; break;
                            }
                        }
                        break;
                    case "Макет Нерезиновая-Сити":
                        P.Health[0] += 10;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        i++; //В описании самой Регалии снова упоминается "Макет Нерезиновая-Сити" он следущий в матчез, перепрыгиваем!
                        break;
                    case "Старое радио":
                        P.Strength[0] += 10;
                        P.Strength[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Монетка с Фейерабендом":
                        P.Dexterity[0] += 8;
                        P.Dexterity[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0; 
                        break;
                    case "Фирменный знак":
                        P.Health[0] += 7;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Золотая цепуха":
                        P.Strength[0] += 10; P.Cunning[0] += 10; P.Attentiveness[0] += 10;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Strength[0] += x * 2;
                            P.Cunning[0] += x * 2;
                            P.Attentiveness[0] += x * 2;
                        }
                        i++; //В описании самой Регалии снова упоминается "Золотая цепуха" он следущий в матчез, перепрыгиваем!
                        break;
                    case "Боксерская груша":
                        P.Strength[0] += 7; P.Dexterity[0] += 7; P.Cunning[0] += 7;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Strength[0] += 1; P.Dexterity[0] += 1; P.Cunning[0] += 1; break;
                                case 2: P.Strength[0] += 3; P.Dexterity[0] += 3; P.Cunning[0] += 3; break;
                                case 3: P.Strength[0] += 5; P.Dexterity[0] += 5; P.Cunning[0] += 5; break;
                                case 4: P.Strength[0] += 8; P.Dexterity[0] += 8; P.Cunning[0] += 8; break;
                                case 5: P.Strength[0] += 11; P.Dexterity[0] += 11; P.Cunning[0] += 11; break;
                                case 6: P.Strength[0] += 15; P.Dexterity[0] += 15; P.Cunning[0] += 15; break;
                                case 7: P.Strength[0] += 19; P.Dexterity[0] += 19; P.Cunning[0] += 19; break;
                                case 8: P.Strength[0] += 23; P.Dexterity[0] += 23; P.Cunning[0] += 23; break;
                                case 9: P.Strength[0] += 28; P.Dexterity[0] += 28; P.Cunning[0] += 28; break;
                            }
                        }
                        i++; //В описании самой Регалии снова упоминается "Золотая цепуха" он следущий в матчез, перепрыгиваем!
                        break;
                    case "Магический шар":
                        P.Cunning[0] += 5; P.Attentiveness[0] +=5; P.Charisma[0] +=5;
                        P.Cunning[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        P.Attentiveness[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        P.Charisma[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Модель спирали ДНК":
                        P.Health[0] += 3; P.Strength[0] += 1; P.Endurance[0] +=3;
                        P.Health[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        P.Strength[0] += matches[i].Groups["Rang"].Success ? Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        P.Endurance[0] += matches[i].Groups["Rang"].Success ? 2 * Convert.ToInt32(matches[i].Groups["Rang"].Value) : 0;
                        break;
                    case "Ваучер":
                        P.Cunning[0] += 5; P.Charisma[0] += 5;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Cunning[0] += x / 2 * 3 + x % 2;
                            P.Charisma[0] += x / 2 * 3 + x % 2 * 2;
                        }
                        break;
                    case "Бляха начальника охраны":
                        P.Endurance[0] += 2;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Endurance[0] += 1; P.Attentiveness[0] += 1; break;
                                case 2: P.Endurance[0] += 1; P.Attentiveness[0] += 3; break;
                                case 3: P.Endurance[0] += 3; P.Attentiveness[0] += 3; break;
                                case 4: P.Endurance[0] += 3; P.Attentiveness[0] += 4; break;
                                case 5: P.Endurance[0] += 5; P.Attentiveness[0] += 6; break;
                                case 6: P.Endurance[0] += 7; P.Attentiveness[0] += 7; break;
                                case 7: P.Endurance[0] += 7; P.Attentiveness[0] += 9; break;
                                case 8: P.Endurance[0] += 10; P.Attentiveness[0] += 9; break;
                                case 9: P.Endurance[0] += 10; P.Attentiveness[0] += 12; break;
                            }
                        }
                        break;
                    case "Бабочка судьбы":
                        P.Attentiveness[0] += 7;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Dexterity[0] += 5; P.Attentiveness[0] += 2; break;
                                case 2: P.Dexterity[0] += 7; P.Attentiveness[0] += 3; break;
                                case 3: P.Dexterity[0] += 8; P.Attentiveness[0] += 5; break;
                                case 4: P.Dexterity[0] += 10; P.Attentiveness[0] += 6; break;
                                case 5: P.Dexterity[0] += 11; P.Attentiveness[0] += 8; break;
                                case 6: P.Dexterity[0] += 13; P.Attentiveness[0] += 9; break;
                                case 7: P.Dexterity[0] += 15; P.Attentiveness[0] += 11; break;
                                case 8: P.Dexterity[0] += 17; P.Attentiveness[0] += 13; break;
                                case 9: P.Dexterity[0] += 19; P.Attentiveness[0] += 22; break;
                            }
                        }                   
                        break;
                    case "Ручной медведь":
                        P.Strength[0] += 8; P.Endurance[0] += 8;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Strength[0] += 1; P.Endurance[0] += 2; break;
                                case 2: P.Strength[0] += 2; P.Endurance[0] += 4; break;
                                case 3: P.Strength[0] += 4; P.Endurance[0] += 5; break;
                                case 4: P.Strength[0] += 6; P.Endurance[0] += 7; break;
                                case 5: P.Strength[0] += 7; P.Endurance[0] += 9; break;
                                case 6: P.Strength[0] += 9; P.Endurance[0] += 10; break;
                                case 7: P.Strength[0] += 10; P.Endurance[0] += 12; break;
                                case 8: P.Strength[0] += 12; P.Endurance[0] += 13; break;
                                case 9: P.Strength[0] += 14; P.Endurance[0] += 14; break;
                            }
                        }                        
                        break;
                    case "Осколок метеорита":
                        P.Health[0] += 1; P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 1; P.Attentiveness[0] += 1;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            switch (Convert.ToInt32(matches[i].Groups["Rang"].Value))
                            {
                                case 1: P.Dexterity[0] += 1; P.Attentiveness[0] += 1; break;
                                case 2: P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Attentiveness[0] += 1; break;
                                case 3: P.Health[0] += 1; P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 1; P.Attentiveness[0] += 1; break;
                                case 4: P.Health[0] += 2; P.Dexterity[0] += 1; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 1; P.Attentiveness[0] += 1; break;
                                case 5: P.Health[0] += 2; P.Dexterity[0] += 2; P.Strength[0] += 1; P.Endurance[0] += 1; P.Cunning[0] += 2; P.Attentiveness[0] += 2; break;
                                case 6: P.Health[0] += 2; P.Dexterity[0] += 2; P.Strength[0] += 2; P.Endurance[0] += 2; P.Cunning[0] += 2; P.Attentiveness[0] += 2; break;
                                case 7: P.Health[0] += 2; P.Dexterity[0] += 2; P.Strength[0] += 3; P.Endurance[0] += 3; P.Cunning[0] += 2; P.Attentiveness[0] += 2; break;
                                case 8: P.Health[0] += 3; P.Dexterity[0] += 2; P.Strength[0] += 3; P.Endurance[0] += 3; P.Cunning[0] += 3; P.Attentiveness[0] += 2; break;
                                case 9: P.Health[0] += 3; P.Dexterity[0] += 3; P.Strength[0] += 3; P.Endurance[0] += 3; P.Cunning[0] += 3; P.Attentiveness[0] += 3; break;
                            }
                        }                        
                        break;
                    case "Герб Великой Страны":
                        P.Health[0] += 6; P.Cunning[0] += 6; P.Attentiveness[0] += 1;
                        if (matches[i].Groups["Rang"].Success)
                        {
                            x = Convert.ToInt32(matches[i].Groups["Rang"].Value);
                            P.Health[0] += x * 2; 
                            P.Cunning[0] += x * 2; 
                            P.Attentiveness[0] += x;
                        }
                        break;
                }
            }
            #endregion
        }
        private void HideMeFromHC()
        {
            BugReport("HideMeFromHC");            
            
            UpdateMyInfo(MainWB);
            UpdateStatus("@ " + DateTime.Now + " Похоже скакалка на сегодня сломалась ... нас заказали!");
            Me.Wanted = true;
            do
            {
                UseTimeOut(TimeOutAction.Free);
                if (Settings.WantedGoMC) MC(MCAction.Work, Settings.MCWorkTime);
                CheckHealthEx(0, 0, Settings.HealPet50, Settings.HealPet100); //Проверка всё ли ешё заказан? и по 0% дабы меня не лечил, только пэта!
            } while (Me.Wanted && Settings.WantedGoMC);
        }        
        private void CheckForDayPrize() //OK
        {
            BugReport("CheckForDayPrize");

            if (!frmMain.GetDocumentURL(MainWB).EndsWith(Settings.ServerURL + "/alley/")) GoToPlace(MainWB, Place.Alley);

            switch (GetServerTime(MainWB).DayOfWeek) //Сундуки бывают в среду, четверг, пятницу.
            {
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday:
                case DayOfWeek.Friday:
                    {                                                                                                                                  //button class="button disabled" disabled="" onclick="alleySovetTakeDayPrize('duel');
                        Regex regex = new Regex("(?<Disable>disabled(=\"\")?)?.onclick[=]\"alleySovetTakeDayPrize[(]((?<Duel>'duel')|(?<Group>'group'))+[)]"); //class="button disabled" onclick="alleySovetTakeDayPrize('group');
                        MatchCollection matches = regex.Matches(frmMain.GetDocumentHtmlText(MainWB)); //Проверка статуса кнопки "Бонус", активна -> забрать приз!                         
                        foreach (Match match in matches)
                        {
                            string Prize = "";
                            if (!match.Groups["Disable"].Success && match.Groups["Duel"].Success) Prize = "duel";
                            if ((!match.Groups["Disable"].Success && match.Groups["Group"].Success)) Prize = "group";
                            #region Забираем вознаграждение, если можем.
                            if (!Prize.Equals(""))
                            {
                                frmMain.InvokeScript(MainWB, "alleySovetTakeDayPrize", new object[] { Prize } );
                                IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            }
                            #endregion
                        }
                    }
                    break;
            }
        }
        private void CheckForPrizeBox() //OK
        {
            BugReport("CheckForPrizeBox");
            UpdateStatus("* " + DateTime.Now + " Открываю сундучки");

            Match match;

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player);
            string[] ArrInfo = GetArrClassHtml(MainWB, "$(\"#content .equipment-cell .object-thumbs .object-thumb\");", "innerHTML");
            string[][] Box = new string[6][];
            string[][] Key = new string[6][];
            #region Сбор информации о сундуках и ключах в багажнике
            foreach (string Info in ArrInfo)
            {
                match = Regex.Match(Info, "(?<OldBox>inventory-box(_b)?([0-9])+-btn)|(?<NewBox>inventory-new_box([0-9])+-btn)|(?<FruitBox>inventory-box_fruit([a-z_])*-btn)|(?<CampBoss>inventory-campboss_superbox_([0-9])+-btn)|(?<MetroBox>inventory-rat_box_(?<RatSize>([0-9])+)-btn)|(?<EpicBox>inventory-epic_rat_box_([0-9])+-btn)|(?<LeninBox>inventory-box_lenin_(?<LeninSize>([0-9])+)-btn)|(?<SovetBox>inventory-box_sovet_fight_(?<SovetSize>(s|m|l))-btn)|(?<RaenBox>inventory-box_sovet_fight_r([0-9])+-btn)|((?<MetroKey>box_metro_)?key((?<KeyType>[0-9])|(?<SovetKey>_sovet_fight))?.png.+data-id=\"?(?<ID>([0-9])+)\"?)");
                //                match = Regex.Match(Info, "(?<OldBox>inventory-box(_b)?([0-9])+-btn)|(?<NewBox>inventory-new_box([0-9])+-btn)|(?<FruitBox>inventory-box_fruit([a-z_])*-btn)|(?<CampBoss>inventory-campboss_superbox_([0-9])+-btn)|(?<MetroBox>inventory-rat_box_(?<RatSize>([0-9])+)-btn)|(?<EpicBox>inventory-epic_rat_box_([0-9])+-btn)|(?<LeninBox>inventory-box_lenin_(?<LeninSize>([0-9])+)-btn)|((?<MetroKey>box_metro_)?key(?<KeyType>[0-9])?.png.+data-id=\"?(?<ID>([0-9])+)\"?)");

                int Type = -1;
                if (match.Groups["FruitBox"].Success || match.Groups["CampBoss"].Success || match.Groups["RaenBox"].Success) Type = 0;
                if (match.Groups["OldBox"].Success || match.Groups["KeyType"].Value == "1") Type = 1;
                if (match.Groups["NewBox"].Success || match.Groups["KeyType"].Value == "2") Type = 2;
                if (match.Groups["LeninBox"].Success || match.Groups["KeyType"].Value == "3") Type = 3;
                if (match.Groups["EpicBox"].Success || match.Groups["MetroBox"].Success || match.Groups["MetroKey"].Success) Type = 4;
                if (match.Groups["SovetBox"].Success || match.Groups["SovetKey"].Success) Type = 5;
                if (Type != -1)
                {
                    if (match.Groups["KeyType"].Success || match.Groups["MetroKey"].Success || match.Groups["SovetKey"].Success)
                    {
                        Array.Resize<string>(ref Key[Type], Key[Type] == null ? 1 : Key[Type].Count<string>() + 1);
                        Key[Type][Key[Type].Count<string>() - 1] = match.Groups["ID"].Value;
                    }
                    else
                    {
                        Array.Resize<string>(ref Box[Type], Box[Type] == null ? 1 : Box[Type].Count<string>() + 1);
                        Box[Type][Box[Type].Count<string>() - 1] = match.Value;
                    }
                }
            }
            #endregion
            #region Открытие сундучков
            for (int i = 0; i < Box.Count<string[]>(); i++)
            {
                if (Box[i] != null && (i == 0 || Key[i] != null)) //Есть сундуки и ключи (при i=0 не нужны) этого вида?
                {  
                    int TempKey;
                    int KeyCount;
                    
                    switch (i)
                    {
                        case 0: //Ключи не нужны
                            KeyCount = Box[i].Count<string>(); 
                            break;
                        case 2: //эти ключи не складываются
                            KeyCount = Key[i].Count<string>();
                            break;
                        default: //иные ключи в стопках
                            KeyCount = Convert.ToInt32(((string)frmMain.GetJavaVar(MainWB, "m.items['" + Key[i][0] + "'].count[0].innerText")).Replace("#", ""));
                            break;
                    }
                    if (i != 0)
                    {
                        match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "m.items['" + Key[i][0] + "'].info.content"), "(?<Count>([0-9])+) шт. до (?<Date>([0-9 .:])+)");
                        TempKey = match.Success ? (Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")) - GetServerTime(MainWB) < new TimeSpan(2, 0, 0) ? Convert.ToInt32(match.Groups["Count"].Value) : 0) : 0;
                    }
                    else TempKey = 0;
                    
                    foreach (string sBox in Box[i])
                    {
                        if ((i == 3 || i == 4)) //Сундуки с ленинопровода и охоты на крыс
                        {
//                            if ((sBox.Contains("1-btn") || (i == 3 && !Me.OilLeninHunting.Stop) || (i == 4 && !Me.RatHunting.Stop)) && TempKey <= 0) break; //Пошли мелкие сундучки + открывать только при горящих ключах или законченной охоте!
                        }                        
                        if (KeyCount > 0)
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById(sBox), "click"); //Всё в порядке, вскрываем!
                            IsWBComplete(MainWB, 2000, 3500);
                            TempKey--; //Сначала, наверняка был потрачен ключик с таймером
                            KeyCount--; //Уменьшаем общее количество ключей
                        }
                        else break; //Больше нет ключей к таким сундучкам                        
                    }
                }
            }
            #endregion            
        }
        private void CheckMetroWarPrize() //OK
        {
            BugReport("CheckMetroWarPrize");

            DateTime ServerDT = GetServerTime(MainWB);
            DateTime NextDT = new DateTime(); //
            HtmlElement HtmlEl;
            Match match;

            if (ServerDT.DayOfWeek == DayOfWeek.Thursday || ServerDT.DayOfWeek == DayOfWeek.Friday)
            {
                GoToPlace(MainWB, Place.Metrowar, "/clan", false);
                if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/clan/$")) return; //Уходим, если не получилось зайти!

                UpdateStatus("# " + DateTime.Now + " С криком: \"Граждане пассажиры, оплачиваем проезд\" - я ворвался в метро!");

                for (int i = 0; i < frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("button").Count; i++)
                {
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("button")[i];
                    match = Regex.Match(HtmlEl.InnerText, "(?<Ready>)Собрать|(?<Timer>(([0-9:])+){6,})");
                    if (match.Success)
                    {
                        if (match.Groups["Ready"].Success)
                        {
                            UpdateStatus("$ " + DateTime.Now + " Я, как настоящий тимуровец, не смог не помочь облегчить карманы Бабки-Юрьевны из будки.");
                            frmMain.InvokeMember(MainWB, HtmlEl, "onclick");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            --i; //Снова воспользоваться этой же кнопкой для анализа
                        }
                        if (match.Groups["Timer"].Success) NextDT = GetServerTime(MainWB).Add(TimeSpan.Parse(match.Groups["Timer"].Value));
                    }
                    if (Me.MetroWarPrizeDT <= ServerDT) Me.MetroWarPrizeDT = NextDT;
                    else Me.MetroWarPrizeDT = Me.MetroWarPrizeDT > NextDT ? NextDT : Me.MetroWarPrizeDT;
                }
                if (NextDT == new DateTime()) //Сегодня четверг и видимо у меня вообще нет станций в метро)
                {
                    UpdateStatus("* " + DateTime.Now + " Я был у цели, когда из толпы явился ОН, Михалыч с бодуна ..., его люлей мне хватит на неделю!");
                    Me.MetroWarPrizeDT = ServerDT.AddDays(7).Date;
                }
            }
            else Me.MetroWarPrizeDT = ServerDT.AddDays(1).Date; //Сегодня ещё рановато заглянуть завтра.

            Me.MetroWarPrizeDT = Me.MetroWarPrizeDT.AddMinutes(new Random().Next(10, 61)); //Добавляем случайный фактор опоздания, ибо это не важно когда забрать жетоны, на это действие даётся 4 часа!
        }
        private void CheckForUsableItems()
        {
            BugReport("CheckForUsableItems");
            
            Match match;
            MatchCollection matches;            
            TimeSpan MaxTS = new TimeSpan(new Random().Next(2, 7), 0, 0);
            DateTime ServerDT = GetServerTime(MainWB);
            int RepeatTimes;

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player);           
            string[] ArrInfo = GetArrClassHtml(MainWB, "$(\"#content .equipment-cell .object-thumbs .object-thumb\");", "innerHTML");

            RepeatTimes = 0; //Обнуление
            foreach (string Info in ArrInfo) //black_accounting.png
            {
                matches = Regex.Matches(Info, "(?<MafiaBook>black_accounting.png).+data-id=\"?(?<ID>([0-9])+)\"?");
                if (matches.Count > 0) //Найдены чёрные бухгалтерии
                {
                    foreach (Match m in matches) //Чёрные бухгалтерии могут истекать не одновременно!
                    {
                        match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "m.items['" + m.Groups["ID"].Value + "'].info.content"), "(?<Count>([0-9])+) шт. до (?<Date>([0-9 .:])+)");
                        RepeatTimes += match.Success ? (Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")) - ServerDT < MaxTS ? Convert.ToInt32(match.Groups["Count"].Value) : 0) : 0;
                    }
                    for (int i = 0; i < RepeatTimes; i++)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Похоже эта чёрная бухгалтерия почти потеряла актуальность, бегу оформлять!");
                        MobilePhone(MobilePhoneAction.MafiaTrade);
                    }
                    break; //Выходим, нет смысла дальше листать
                }                
            }

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player); 
            RepeatTimes = 0; //Обнуление
            HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-stat_stimulator-btn"); //Пельмешка
            if (HtmlEl != null && !Expert.DoNotEatPelmeni)
            {
                matches = Regex.Matches((string)frmMain.GetJavaVar(MainWB, "m.items['" + HtmlEl.GetAttribute("data-id") + "'].info.content"), "(?<Count>([0-9])+) шт. до (?<Date>([0-9 .:])+)");
                foreach (Match m in matches)
                {
                    RepeatTimes += Convert.ToDateTime(m.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")) - ServerDT < MaxTS ? Convert.ToInt32(m.Groups["Count"].Value) : 0;
                }
                for (int i = 0; i < RepeatTimes; i++)
                {
                    UpdateStatus("@ " + DateTime.Now + " Ой а пельмешки то почти протухли, да пофиг, съем под водку!");
                    WaitDrugEated(MainWB, HtmlEl); 
                }       
            }
            Me.Events.NextItemCheckDT = ServerDT.Add(new TimeSpan(1, new Random().Next(0, 30), new Random().Next(0, 60)));
        }

        private void CheckBagFightItems(GroupFightType GFT)
        {
            BugReport("CheckBagFightItems");
            UpdateStatus("@ " + DateTime.Now + " Похоже руками махать придется, пойду проверю-ка я карманы!");

            object Info;
            Match match;
            MatchCollection matches;

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player);

            #region Достаем из настроек список предметов, которые надо взять с собой
            List<FightItemType> ArrItemType = new List<FightItemType>();
            for (int i = 0; i < 6; i++)
                ArrItemType.Add((FightItemType)Expert.FightSlotItemTypes[(int)GFT * 7 + i]);
            BugReport("@ Согласно настройкам нам нужно заполнить слоты следующими типами предметов: " + String.Join(", ", ArrItemType));
            #endregion

            #region Определяем текущее состояние боевых слотов
            List<stcBagFightItem> SlotFightItems = new List<stcBagFightItem>();
            HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("slots-groupfight-place").GetElementsByTagName("img");
            int SlotCount = GetArrClassCount(MainWB, "$(\"#slots-groupfight-place .object-thumb .padding[title!='Дополнительный карман']\")");
            BugReport("@ Текущее состояние боевых слотов:");
            for (int i = 0; i < SlotCount; i++)
            {
                stcBagFightItem Item = new stcBagFightItem();
                if (i >= HC.Count) //Пустой слот?
                {
                    SlotFightItems.Add(Item);
                    BugReport("* Слот " + (i + 1) + ": Пустой");
                    continue;
                }

                match = Regex.Match(HC[i].OuterHtml, "data-id=\"(?<ID>_([0-9])+)");
                string SlotItemID = match.Groups["ID"].Value;
                Item.ItemID = SlotItemID.Replace("_", "");
                Item.TotalCount = Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "m.items['" + SlotItemID + "'].count['0'].innerText"), "([0-9])+").Value);
                Item.Title = (string)frmMain.GetJavaVar(MainWB, "m.items['" + SlotItemID + "'].info.title");
                Info = frmMain.GetJavaVar(MainWB, "m.items['" + SlotItemID + "'].info.content");
                Item.ItemType = stcBagFightItem.DetermineType((string)Info);
                match = Regex.Match((string)Info, "Срок годности: (?<Date>([0-9 .:])+)");
                if (match.Success)
                {
                    Item.LastDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                    Item.TimedCount = Item.TotalCount; //Временные предметы!
                }

                SlotFightItems.Add(Item);
                BugReport("* Слот " + (i + 1) + ": " + Item);
            }
            #endregion

            #region Составляем список боевых предметов, которые есть в наличии
            HtmlElement BagFightItemsElem = null;
            foreach (HtmlElement Elem in frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("div"))
            {
                if (Elem.GetAttribute("htab") == "fight" && Elem.GetAttribute("rel") == "fight")
                {
                    BagFightItemsElem = Elem;
                    break;
                }
            }

            List<stcBagFightItem> BagFightItems = new List<stcBagFightItem>();
            BugReport("@ Известные боевые предметы, которые есть в наличии:");
            foreach (HtmlElement Elem in BagFightItemsElem.GetElementsByTagName("img"))
            {
                stcBagFightItem Item = new stcBagFightItem();
                match = Regex.Match(Elem.OuterHtml, "data-id=\"(?<ID>([0-9])+)");
                Item.ItemID = match.Groups["ID"].Value;
                Item.TotalCount = Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "m.items['" + Item.ItemID + "'].count['0'].innerText"), "([0-9])+").Value);
                Item.Title = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Item.ItemID + "'].info.title");
                Info = frmMain.GetJavaVar(MainWB, "m.items['" + Item.ItemID + "'].info.content");
                Item.ItemType = stcBagFightItem.DetermineType((string)Info);
                if (Item.ItemType == FightItemType.None) 
                    continue; // Пропускаем неизвестный предмет
                matches = Regex.Matches((string)Info, "((?<Count>([0-9])+) шт. до (?<Date>([0-9 .:])+))|Срок годности: (?<Date>([0-9 .:])+)"); //Когда все предметы пропадают в один день может быть без количества!
                foreach (Match m in matches)
                {
                    if (Item.LastDT == new DateTime()) Item.LastDT = Convert.ToDateTime(m.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                    Item.TimedCount += m.Groups["Count"].Success ? Convert.ToInt32(m.Groups["Count"].Value) : Item.TotalCount;
                }

                // Если этот предмет также есть в слотах, добавляем информацию оттуда
                stcBagFightItem SlotItem = SlotFightItems.Find(It => It.ItemID == Item.ItemID);
                if (SlotItem.ItemID != null)
                {
                    Item.TotalCount += SlotItem.TotalCount;
                    Item.TimedCount += SlotItem.TimedCount;
                    Item.LastDT = SlotItem.LastDT;
                }

                BagFightItems.Add(Item);
                BugReport("* " + Item);
            }

            #region Добавляем предметы, которые остались только в слотах и которых нет в инвентаре
            foreach (stcBagFightItem Item in SlotFightItems)
            {
                // Пропускаем пустые слоты и неизвестные предметы
                if (Item.ItemID == null || Item.ItemType == FightItemType.None)
                    continue;
                // Добавляем предмет из слотов только если его еще нет в инвентаре
                if (!BagFightItems.Exists(It => It.ItemID == Item.ItemID))
                {
                    BagFightItems.Add(Item);
                    BugReport("* " + Item);
                }
            }
            #endregion
            #endregion

            #region Укорачиваем список желаемых предметов под количество доступных слотов
            if (ArrItemType.Count() > SlotFightItems.Count()) ArrItemType = ArrItemType.GetRange(0, SlotFightItems.Count());
            #endregion

            #region Составляем список предметов, которые мы возьмем с собой
            List<stcBagFightItem> TakeItems = new List<stcBagFightItem>();
            foreach (FightItemType ItemType in ArrItemType)
            {
                if (ItemType == FightItemType.None)
                    continue;
                stcBagFightItem OldItem = new stcBagFightItem();
                foreach (stcBagFightItem NewItem in BagFightItems)
                {
                    // Сравниваем типы предметов
                    switch (ItemType)
                    {
                        // Фиксированная и процентная еда взаимозаменяема
                        case FightItemType.FixedHeal:
                        case FightItemType.ProcHeal:
                            if (NewItem.ItemType != FightItemType.FixedHeal && NewItem.ItemType != FightItemType.ProcHeal)
                                continue;
                            break;

                        // Фиксированные и процентные грены взаимозаменяемы
                        case FightItemType.FixedBomb:
                        case FightItemType.ProcBomb:
                            if (NewItem.ItemType != FightItemType.FixedBomb && NewItem.ItemType != FightItemType.ProcBomb)
                                continue;
                            break;

                        // Тип остальных предметов должен в точности совпадать
                        default:
                            if (NewItem.ItemType != ItemType)
                                continue;
                            break;
                    }

                    // Если предмет уже запланирован для взятия в другой слот, пропускаем его
                    if (TakeItems.Contains(NewItem))
                        continue;

                    // Если у нас еще нет кандидата, сразу берем предмет
                    if (OldItem.ItemID == null)
                    {
                        OldItem = NewItem;
                        continue;
                    }

                    // Если предметы не являются сыром и количество одного из предметов меньше трех
                    // и не совпадает с количеством другого предмета, берем тот предмет, которого больше
                    if ((NewItem.TotalCount < 3 || OldItem.TotalCount < 3) &&
                        NewItem.TotalCount != OldItem.TotalCount && ItemType != FightItemType.Cheese)
                    {
                        if (NewItem.TotalCount > OldItem.TotalCount)
                            OldItem = NewItem;
                        continue;
                    }

                    // Если один предмет со сроком годности, а другой - без срока, то берем
                    // предмет со сроком годности
                    if (OldItem.LastDT == new DateTime() && NewItem.LastDT != new DateTime())
                    {
                        OldItem = NewItem;
                        continue;
                    }
                    if (OldItem.LastDT != new DateTime() && NewItem.LastDT == new DateTime())
                        continue;

                    // Если оба предмета со сроком годности, то берем тот, у которого срок меньше
                    if (OldItem.LastDT != new DateTime())
                    {
                        if (NewItem.LastDT < OldItem.LastDT)
                            OldItem = NewItem;
                        continue;
                    }

                    // Если тип нового и старого предмета не совпадает (для еды и грен), то берем
                    // предмет с наиболее точным типом
                    if (OldItem.ItemType != NewItem.ItemType)
                    {
                        if (NewItem.ItemType == ItemType)
                            OldItem = NewItem;
                        continue;
                    }

                    // Если дошли до этого места, значит новый предмет ничем не лучше старого
                    // и мы его пропускаем
                }

                // Если подходящего предмета так и не нашли, запоминаем тип нужного предмета
                if (OldItem.ItemID == null)
                {
                    UpdateStatus("@ " + DateTime.Now + " Предметов с типом " + ItemType + " нет в наличии");
                    OldItem.ItemType = ItemType;
                }
                TakeItems.Add(OldItem);
            }

            if (DebugMode)
            {
                BugReport("@ Список предметов, которые нужно взять с собой:");
                foreach (stcBagFightItem Item in TakeItems)
                    BugReport("* " + Item);
            }
            #endregion

            #region Убираем из слотов ненужные предметы
            foreach (stcBagFightItem Item in SlotFightItems)
            {
                // Пропускаем пустые слоты и слоты, в которых уже находятся нужные предметы
                if (Item.ItemID == null || TakeItems.Exists(It => It.ItemID == Item.ItemID))
                    continue;

                // Убираем предмет из слота
                UpdateStatus("@ " + DateTime.Now + " Выкладываю " + Item.Title);
                frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon-group/" + "_" + Item.ItemID + "/\", type: \"post\", data: {\"unlocked\": 0, \"inventory\": " + Item.ItemID + "}, dataType: \"json\"});");
                IsWBComplete(MainWB);
            }
            #endregion

            #region Берем в слоты нужные предметы
            foreach (stcBagFightItem Item in TakeItems)
            {
                // Пропускаем предметы, которых нет в наличии, и предметы, которые уже находятся в слотах
                if (Item.ItemID == null || SlotFightItems.Exists(It => It.ItemID == Item.ItemID))
                    continue;

                // Берем предмет в слот
                UpdateStatus("@ " + DateTime.Now + " Беру " + Item.Title);
                frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon-group/" + Item.ItemID + "/\", type: \"post\", data: {\"unlocked\": 1, \"inventory\": " + Item.ItemID + ", \"previousItemId\": " + 0 + "}, dataType: \"json\"});");
                IsWBComplete(MainWB);
            }
            #endregion

            #region Закупка недостающих средств!
            bool bRet = false;
/*            foreach (stcBagFightItem BestItem in TakeItems)
            {
                if (Expert.BuyFightItemType[(int)BestItem.ItemType]) //Разрешено докупать?
                {
                    List<string> ItemToBuy = new List<string>();
                    if (BestItem.ItemID == null && BestItem.TotalCount == 0) //Нужно купить новый вид с учётом того, что уже в багаже!
                    {
                        ItemToBuy = TakeItems.Where(item => item.ItemID != null && item.ItemType == BestItem.ItemType).ToList<stcBagFightItem>().ConvertAll<string>(item => item.Title);
                        ItemToBuy.Insert(0, "!");
                    }
                    else //Докупаем?
                    {
                        if (
                            (BestItem.ItemType == FightItemType.FixedHeal
                             || BestItem.ItemType == FightItemType.ProcHeal
                             || BestItem.ItemType == FightItemType.FixedBomb
                             || BestItem.ItemType == FightItemType.ProcBomb
                             || BestItem.ItemType == FightItemType.Helmet
                             || BestItem.ItemType == FightItemType.Spring
                             || BestItem.ItemType == FightItemType.Shield
                            ) && BestItem.TotalCount < 3 && !BestItem.Title.Contains("Ультра")
                           )
                            ItemToBuy.Add(BestItem.Title);
                    }
                    //Устанавливаем какой тип нужно докупить, так как индексация типов начинается с 1, то 1-1 = 0 и равняется лечению,
                    //таким образом сохранится работоспособнсть при внедрении новых элементов в массив ShopItems
                    if (ItemToBuy.Count == 1 || (ItemToBuy.Count > 1 && Expert.BuyMoreThenOneGranade))
                    {
                        bRet |= BuyItems(MainWB, (int)BestItem.ItemType - 1 + ShopItems.HealPlus, ItemToBuy.ToArray());
                        Me.Events.NextFightItemCheckDT = DateTime.Now.AddMinutes(10);
                    }
                }
            }*/
            #endregion

            GoToPlace(MainWB, Place.Player);
            if (bRet) CheckBagFightItems(GFT); //Докупал какие-то вещи, кладем их в багажник!

            UpdateStatus("@ " + DateTime.Now + " Боевые слоты проверены");
        }

        private void Sovet(SovetAction SA) //OK
        {
            BugReport("CheckForWeekPrize");
                        
            HtmlElement HtmlEl;
            Regex regex;
            Match match;

            GoToPlace(MainWB, Place.Sovet);
            try //Не проиграла ли наша сторона?
            {
                switch (SA)
                {
                    case SovetAction.Patriot:
                        #region Патриот
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[0];
                        match = Regex.Match(HtmlEl.InnerText, "(?<=Вы патриот до: )([0-9. :])+"); //Вы патриот до: 10.09.2012 08:37
                        if (match.Success) Me.SovetInfo.Patriot = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Я патриот до?                    
                        else //Продление патриота
                        {
                            UpdateMyInfo(MainWB);
                            HtmlEl = HtmlEl.GetElementsByTagName("Button")[0];
                            if (HtmlEl.InnerText.Contains("Стать патриотом") & Me.Wallet.Ore >= 14) frmMain.InvokeMember(MainWB, HtmlEl, "click");
                            IsWBComplete(MainWB);
                        }
                        #endregion
                        return;
                    case SovetAction.Vote:                        
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[1];
                        match = Regex.Match(HtmlEl.InnerText, "(?<=[[]Сегодня[]] )([\\w :«»])+");
                        switch (match.Value)
                        {
                            case "ПН: день выбора советников":
                                #region Выбор советников (неважный пункт)
                                if (frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[3].GetElementsByTagName("button").Count < 1) { Me.SovetInfo.LastVoting = GetServerTime(MainWB).AddMinutes(30); return; } //Уже проголосовал и больше нет кнопки? (Что-то пошло не так, зайдём через 30 минут ещё разок)
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[3].GetElementsByTagName("button")[0]; //Внести взнос.
                                if (HtmlEl.InnerText == "Голосовать")
                                {
                                    regex = new Regex(@"(?<=(?m)^1\.).*(?=\[([0-9])*\])"); //Выдираем игрока из списка за которых голосуют вида 1.Имя(Кандидата)
                                    match = regex.Match(frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[10].InnerText);
                                    if (match.Success)
                                    {
                                        frmMain.GetDocument(MainWB).All["nickname"].SetAttribute("value", match.Value);
                                        frmMain.GetDocument(MainWB).All["money"].SetAttribute("value", "100");
                                        Wait(2000, 3000); //Пауза перед нажатием кнопки.
                                        frmMain.InvokeMember(MainWB, HtmlEl, "click");
                                        IsWBComplete(MainWB);
                                    }
                                    else { Me.SovetInfo.LastVoting = GetServerTime(MainWB).AddMinutes(30); return; } //"Кандидатов нет. Стань первым!" Зайдём ещё через 30 минут.
                                }
                                #endregion
                                break;
                            case "ВТ: день выбора района для атаки":
                                #region Вабор района (неважный пункт)
                                GoToPlace(MainWB, Place.Sovet, "/map");
                                if (frmMain.GetDocument(MainWB).GetElementsByTagName("button").Count < 1) { Me.SovetInfo.LastVoting = GetServerTime(MainWB).AddMinutes(30); return; } //Уже проголосовал и больше нет кнопки? (Что-то пошло не так, зайдём через 30 минут ещё разок)
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[3];
                                regex = new Regex("(?<=name>)([^<])+"); //Выдираем, раён за который отдано больше всего голосов
                                regex = new Regex("(?<=value=)([0-9])+" + "(?=>" + regex.Match(HtmlEl.InnerHtml).Value + ")"); //Выискиваем value этого раёна в выпадаюшем списке
                                HtmlEl = frmMain.GetDocument(MainWB).All["station"];
                                match = regex.Match(HtmlEl.InnerHtml); //выбранный большинством раён
                                if (match.Success)
                                {
                                    HtmlEl.SetAttribute("value", match.Value); //Подставляем выбранный большинством раён
                                    frmMain.GetDocument(MainWB).All["money"].SetAttribute("value", "100");
                                    Wait(500, 1000); //Пауза перед нажатием кнопки.
                                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("button")[0], "click");
                                    IsWBComplete(MainWB, 300, 500);
                                }
                                else { Me.SovetInfo.LastVoting =GetServerTime(MainWB).AddMinutes(30); return; } //В случае проблемы. Зайдём ещё через 30 минут.
                                #endregion
                                break;
                            case "СР: день дуэлей":
                            case "ЧТ: день дуэлей":
                                {
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("amount-" + (Me.Player.Fraction == "resident" ? "r" : "a")); //Продаём очки фракции игрока
                                    if (HtmlEl == null)
                                    {
                                        #region Наша фракция не скупает очки, пробуем продать противнику?
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("amount-" + (Me.Player.Fraction == "resident" ? "a" : "r")); //пробуем продать противнику
                                        if (HtmlEl != null) UpdateStatus("@ " + DateTime.Now + " Похоже введён план \"разори противника\", посопутствую.");
                                        else return; //Похоже, не одна фракция не скупает.
                                        #endregion
                                    }
                                    if (Convert.ToInt32(HtmlEl.GetAttribute("value")) >= 80) //Насобирал уже слишком много Золотых очков? Имеет смысл продать заранее часть, чтоб было куда доберать!
                                    {
                                        HtmlEl.SetAttribute("value", "40");
                                        frmMain.InvokeMember(MainWB, HtmlEl.Parent.Parent.GetElementsByTagName("button")[0], "click");
                                        UpdateStatus("* " + DateTime.Now + " Вот! - Унитаз себе золотой заколотишь, продам ка я 40 золотых очков!");
                                    }                                    
                                }                                                                
                                break;
                            case "ПТ: день «стенок»":
                                if (GetServerTime(MainWB).TimeOfDay < new TimeSpan(23, 45, 0)) //Проверить в пятницу начиная с 23:45! и слить все очки!
                                {
                                    #region Ставим метку забежать вечерком!
                                    Me.SovetInfo.LastVoting = GetServerTime(MainWB).Date + new TimeSpan(23, 45, 0);
                                    return;
                                    #endregion
                                }
                                else
                                {
                                    #region Пора заберать бонусы, продавать золотые очки!
                                    int MyPoints, BonusPoints;
                                    for (int i = 0; i < 20; i++) //Забираем бонусы пока, дают=) Не более 20 попыток учитываем попытки на обмен очков!
                                    {
                                        IsWBComplete(MainWB);
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[4];
                                        if (HtmlEl.InnerText.Contains("Приходите на следующей неделе!")) break; //На этой неделе, все бонусы уже забрал!
                                        HtmlEl = HtmlEl.GetElementsByTagName("button")[0];
                                        if (HtmlEl.GetAttribute("classname") == "button") //Кнопка забрать бонус
                                        {
                                            #region Забираю бонусы
                                            UpdateStatus("* " + DateTime.Now + " Забираю кокс и прочие припасы с занычки.");
                                            frmMain.InvokeMember(MainWB, HtmlEl, "onclick");
                                            Wait(500, 1500);
                                            #endregion
                                        }
                                        else //Кнопку нажать уже нельзя, возможно я уже забрал все бонусы? + пробуем продать золотые очки
                                        {
                                            #region Продажа имеющихся золотых очков
                                            IsWBComplete(MainWB);
                                            regex = new Regex("активность: (?<MyPoints>([0-9])+) из (?<BonusPoints>([0-9])+)");
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[4];
                                            match = regex.Match(HtmlEl.InnerText); //Определям уже набранное количество очков
                                            MyPoints = Convert.ToInt32(match.Groups["MyPoints"].Value);
                                            BonusPoints = Convert.ToInt32(match.Groups["BonusPoints"].Value);

                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("amount-" + (Me.Player.Fraction == "resident" ? "r" : "a")); //Продаём очки фракции игрока
                                            if (HtmlEl == null)
                                            {
                                                #region Наша фракция не скупает очки, пробуем продать противнику?
                                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("amount-" + (Me.Player.Fraction == "resident" ? "a" : "r")); //пробуем продать противнику
                                                if (HtmlEl != null) UpdateStatus("@ " + DateTime.Now + " Похоже введён план \"разори противника\", посопутствую.");
                                                else return; //Похоже, не одна фракция не скупает.
                                                #endregion
                                            }
                                            if (MyPoints < BonusPoints & Convert.ToInt32(HtmlEl.GetAttribute("value")) * Me.Player.Level + MyPoints >= BonusPoints) //Если продать золотые очки, можно ещё один бонус прихватить, делаем!
                                            {                                              //Теоретически округление и прочее не нужно, только если сменился уровень игрока!
                                                HtmlEl.SetAttribute("value", Math.Ceiling((double)(BonusPoints - MyPoints) / Me.Player.Level).ToString("######")); //Math.Ceiling-> Округление вверх, "######" -> Срезает цифры после запятой
                                                frmMain.InvokeMember(MainWB, HtmlEl.Parent.Parent.GetElementsByTagName("button")[0], "click");
                                                UpdateStatus("@ " + DateTime.Now + " Кассир, -принимай мои золотые очки, часы и всякий хлам.");
                                            }
                                            else break; //Всё, продажа больШе ничего не принесёт!
                                            #endregion
                                        }
                                    }
                                    #endregion
                                }                                
                                break;
                            case "Выходные":
                                #region Пора заберать бонусы
                                for (int i = 0; i < 10; i++) //Забираем бонусы пока, дают=) (если не успел, забыл в пятницу) 10 попыток, призов меньше!
                                {
                                    IsWBComplete(MainWB);
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[4];
                                    if (HtmlEl.InnerText.Contains("Приходите на следующей неделе!")) break; //На этой неделе, все бонусы уже забрал!
                                    HtmlEl = HtmlEl.GetElementsByTagName("button")[0];
                                    if (HtmlEl.GetAttribute("classname") == "button")
                                    {
                                        UpdateStatus("* " + DateTime.Now + " Забираю кокс и прочие припасы с занычки.");
                                        frmMain.InvokeMember(MainWB, HtmlEl, "onclick");
                                        Wait(500, 1500);
                                    }
                                    else break; //Кнопку нажать уже нельзя, возможно я уже забрал все бонусы?
                                }                                    
                                #endregion
                                break;
                       }
                       break;
                }                
            }
            catch { UpdateStatus("! " + DateTime.Now + " Дохтур, посмотри меня: Голосование проигнорировано!"); }
            Me.SovetInfo.Stop = true; //Уже проголосовал, стоп на сегодня!
            Me.SovetInfo.LastVoting = GetServerTime(MainWB).Date;
        }
        private bool CheckQuest(string URL = null /*Рекурсивный вызов*/)
        {
            BugReport("CheckQuest");

            HtmlElement HtmlEl;
            Regex regex;
            Match match;
            bool Status = false; //Задание не выполнено (для рекурсивных входов, если рекурсивный квэст дороже основного)
            int QE, iL = 0, iM = 0;

            if (URL == null) //Рекурсивные входы сюда не попадут!
            {
                GoToPlace(MainWB, Place.Player);
                UpdateStatus("# " + DateTime.Now + " С криками \"Эй начальник, чем сегодня запряжёшь?!?\" - поломился квэстить.");
            }

            //([a-z])*)*job/(?<jobNr>([0-9])+)/|(?<=')/nightclub/jobs/(?=';)
            regex = new Regex("(/([\\w/])+job/(?<jobNr>([0-9])+)/)|(?<=')/nightclub/jobs/(?=')"); //Выдераем ссылку на новый или старый Квэст для 2ого и 3ого прохода
//            match = regex.Match(URL == null ? frmMain.GetDocument(MainWB).GetElementById("statistics-accordion").InnerHtml : URL);             
            match = regex.Match(URL == null ? "'/nightclub/jobs/'" : URL); //Выполняем задания по порядку на 3 прохода сразу
            if (match.Success)
            {
                frmMain.NavigateURL(MainWB, Settings.ServerURL + ((!Expert.QuestNotAll || URL != null) ? match.Value : "/nightclub/jobs/")); //Загружаем страничку с Квэстом
                UpdateMyInfo(MainWB); //Обновление данных о тонусе и ожидание завершения загрузки странички.

                if (frmMain.GetDocumentURL(MainWB).EndsWith("/nightclub/jobs/"))
                {
                    #region Квэсты закончились, идём по 2 или 3 ему кругу?
                    int i;                    
                    string[] ArrInfo;
                    string BonusQuestURL = "";
                    #region Определение поля деятельности 2-3 круг или бонусная линейка!
                    int QLines = GetArrClassCount(MainWB, "$(\"#content .jobs-map .block-rounded\");");
                    bool BonusLine = !Enumerable.Range(0, Me.Player.Level > 10 ? 10 : Me.Player.Level).Contains(QLines) && !Expert.QuestIgnoreBonus;
                    if (BonusLine) //Обнаружена бонусная линейка квестов!
                    {
                        ArrInfo = GetArrClassHtml(MainWB, "$(\"#content .jobs-map .block-rounded:eq(" + (QLines - 1) + ") .padding\");", "innerHTML");
                    }
                    else //Бонусная линейка не найдена или отключена!
                    {
                        ArrInfo = GetArrClassHtml(MainWB, "$(\"#content .jobs-map .block-rounded:not(:eq(" + (Me.Player.Level > 10 ? 10 : Me.Player.Level) + ")) .padding\");", "innerHTML");
                    }
                    #endregion
                    regex = new Regex("(?<=href=\")([\\w/.])+job/(?<jobNr>([0-9])+)/(?=\"(.*\"?(?<Stars>stars-[0-9])\"?)?(.*\"?percent.*>(?<Prc>([0-9])+)%<)?)"); //href="/bank/job/157/"><i class="hover-area"></i></a><i class="icon img" style="background:url('/@/images/obj/jobs/bank_giulio.jpg')"></i><i class="stars-1"></i><span class="percent">50%</span>
                    for (i = 0; i < ArrInfo.Count<string>(); i++)
                    { 
                        match = regex.Match(ArrInfo[i]);
                        if (!Expert.QuestNotAll && match.Success && match.Groups["Stars"].Success && match.Groups["Prc"].Success) break; //Не доделанный квест 2-3 круга или делать определённый!
                        if ((!Expert.QuestNotAll || BonusLine) && match.Success && match.Groups["Prc"].Success && Convert.ToInt32(match.Groups["Prc"].Value) < 100 && !(new int[] { 33, 34, 223, 224, 225}).Contains<int>(Convert.ToInt32(match.Groups["jobNr"].Value))) break; //Не доделанный квест 2-3 круга, похоже на праздничный квэст.
                        if (match.Groups["jobNr"].Success && Convert.ToDecimal(match.Groups["jobNr"].Value) == (Settings.QuestEndMoney ? Expert.QuestMoneyNr : Expert.QuestFruitNr)) BonusQuestURL = match.Value;
                    }                    
                    if ((!Expert.QuestNotAll || BonusLine) && i != ArrInfo.Count<string>()) //есть не доделанные квесты 2-3 круга?
                    {
                        frmMain.NavigateURL(MainWB, Settings.ServerURL + match.Value); //Загружаем страничку с Квэстом
                        IsWBComplete(MainWB);
                    }
                    else
                    {
                        #region Зарабатывание денег/фруктов на последнем самом дорогом квэсте, ибо всё остальное уже выполнено!
                        if (BonusQuestURL != "")
                        {
                            while (CheckQuest(Settings.ServerURL + BonusQuestURL)) ; //(Settings.QuestEndMoney ? "/alley/job/192/" : "/casino/job/63/")
                            return true;
                        }
                        else 
                        {
                            UpdateStatus("! " + DateTime.Now + " Немогу найти где тут вход на это задание, сорри но в этот лабиринт я больше ни ногой!");
                            Me.Events.StopQuest = true; //что-то не так, невозможно выполнять квэсты!
                            return false;
                        }                        
                        #endregion
                    }
                    #endregion
                }                

                regex = new Regex("((?<=class=\"?tonus\"?>)([0-9])+(?=<))"); //Извлекаем цену нажатия кнопки
                QE = Convert.ToInt32(regex.Match(frmMain.GetDocument(MainWB).GetElementById("job-" + match.Groups["jobNr"].Value).InnerHtml).Value);

                while (Convert.ToInt32(Me.Player.Energy[0]) >= QE && (frmMain.GetDocument(MainWB).GetElementById("job-percent-num-" + match.Groups["jobNr"].Value).InnerText != "100%" || URL != null))
                {
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("job-button-" + match.Groups["jobNr"].Value);
                    if (HtmlEl != null)
                    {
                        frmMain.InvokeMember(MainWB, HtmlEl, "onclick");
                        IsWBComplete(MainWB, 1500, 2000); //IsAjaxComplete(MainWB); 
                        if (Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("currenttonus").InnerText) >= Convert.ToInt32(Me.Player.Energy[0]))
                        {
                            //Что-то пошло не так...
                            regex = new Regex("(?<=(Для добычи предмета выполни задание|Предмет можно получить в задании).*href[=]\").*./(?=\"[>])", RegexOptions.IgnoreCase);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("job-block-place");
                            if (regex.IsMatch(HtmlEl.InnerHtml)) //Не хватает детальки?
                            {
                                UpdateStatus("# " + DateTime.Now + " И снова меня послали, за деталью...");
                                if (!CheckQuest(regex.Match(HtmlEl.InnerHtml).Value)) return false; //В рекурсивном квэсте что-то пошло не так, нет смысла пробовать основной!
                                else frmMain.NavigateURL(MainWB, Settings.ServerURL + match.Value); //Необходимо, ибо нужно вернутся к выполнению основного!
                            }
                            else return false;  //Действительно не хватает детальки -> погнали рекурсивно, нет -> уходим.
                        }
                        else
                        {
                            IsAjaxCompleteEx(MainWB, "job-results-" + match.Groups["jobNr"].Value); //Ожидаю пока ajax обновит контекст
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("job-results-" + match.Groups["jobNr"].Value);

                            regex = new Regex("(?<=expa\"?[>])([0-9])*(?=[<])"); //Получил лампы?
                            if (regex.IsMatch(HtmlEl.InnerHtml)) iL = Convert.ToInt32(regex.Match(HtmlEl.InnerHtml).Value);

                            regex = new Regex("(?<=tugriki\"?[>])([0-9])*(?=[<])"); //Получил деньги?                        
                            if (regex.IsMatch(HtmlEl.InnerHtml)) iM = Convert.ToInt32(regex.Match(HtmlEl.InnerHtml).Value);

                            UpdateStatus("$ " + DateTime.Now + " Блеа, я крассавчеГ, я пионЭр! - Задание выполнено Сэр!" + ((iM + iL) > 0 ? " Нарылось: " : ""), iM, 0, 0, iL);

                            IsAjaxCompleteEx(MainWB, "job-percent-num-" + match.Groups["jobNr"].Value); //Ожидаю пока ajax обновит контекст
                            if (frmMain.GetDocument(MainWB).GetElementById("job-percent-num-" + match.Groups["jobNr"].Value).InnerText == "100%" & URL == null) //Задание выполнено!
                            {
                                IsWBComplete(MainWB);
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-text");
                                UpdateStatus("$ " + DateTime.Now + " Ооопапулечки \"Бонус\", какбы намутилися: ", regex.IsMatch(HtmlEl.InnerHtml) ? Convert.ToInt32(regex.Match(HtmlEl.InnerHtml).Value) : 0);
                                #region Основной квэст пройден, осталась энергия? Перезаход без ожидания востановления тонуса.
                                //Это основной квэст и у нас достаточно энергии пройти ещё разок, значит мы достигли 100% (прийдя из рекурсивного) и можем попробывать наши силы в следующем квэсте не дожидаясь востановления тонуса!
                                if (Convert.ToInt32(Me.Player.Energy[0]) >= QE) Status = CheckQuest();
                                #endregion
                            }
                            URL = null; //Делаем null, чтоб прервать выполнение при рекурсивном квесте и снова пробнуть основной. Тем самым рекурсивный будет запущен только раз.
                        }
                        Status = true; //Задание выполнено успешно                        
                        UpdateMyInfo(MainWB);
                        #region Нужно пополнить тонус?
                        if (Convert.ToInt32(Me.Player.Energy[0]) == 0 && Settings.QuestFillTonusBottle)
                        {
                            GoToPlace(MainWB, Place.Player);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-restoretonus-btn"); //Банка с тонусом
                            if (HtmlEl != null && (Expert.QuestUseAllTonusBottle || frmMain.GetJavaVar(MainWB, "m.items['" + HtmlEl.GetAttribute("data-id") + "'].mf['0'].innerText") != null)
                                && WaitDrugEated(MainWB, HtmlEl)) //Уже тикает время или разрешено пить все?
                            {
                                UpdateStatus("* " + DateTime.Now + " Глотнул из синей бутылки так, что аж тонус из уха полез.");                                
                            }
                        }
                        if (frmMain.GetDocumentURL(MainWB).EndsWith(Settings.ServerURL + "/player/")) //Пробовал выпить банку? - Вернуть назад к квестам!
                        {
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + match.Value); //возврашаем на страничку с квэстом!                                
                            UpdateMyInfo(MainWB);
                        }
                        if (Convert.ToInt32(Me.Player.Energy[0]) == 0 && Settings.QuestFillTonusPlus && !Me.QuestFillTonus.Stop && TonusMePlus())
                        {
                            Me.QuestFillTonus.Stop = true;
                            Me.QuestFillTonus.LastDT = GetServerTime(MainWB); //На сегодня пополнений больше нет.                            
                            Me.Player.Energy[0] = Me.Player.Energy[1]; //Если всё прошло удачно, нет смысла снова считывать мои данные ..., тонус полностью востановлен.
                        }
                        #endregion
                    }
                    else return false; //Не нашёл кнопку которую нужно нажимать в квесте!
                }
                #region Сохранение необходимого для моментального востановления тонуса енергии.
                Me.QuestFillTonus.Val = QE; //Запоминаем сколько нужно было энергии для выполненя последнего квэста.                
                #endregion
            }
            return Status; //Здесь насколько успешен был рекурсивный квэст, основной не интересен! 
        }
        private TimeSpan MobilePhone(MobilePhoneAction MPA)
        {
            HtmlElement HtmlEl;
            TimeSpan TSTimeout = new TimeSpan();

            #region Не дорос до мобильника?
            if (Me.Player.Level < 9 && MPA != MobilePhoneAction.ReadLogs)
            {
                UpdateStatus("@ " + DateTime.Now + " Маловат я еще мобилу тягать, пойду иное потягаю...");
                return new TimeSpan(2, 0, 0); //У меня походу нет телефона, заглянуть через 2 часа.
            }
            #endregion

            switch (MPA)
            {
                case MobilePhoneAction.ReadLogs:
                    Random Rnd = new Random();
                    frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/logs/");
                    IsWBComplete(MainWB);
                    TSTimeout = new TimeSpan(0, Rnd.Next(15, 60), Rnd.Next(60));
                    break;
                case MobilePhoneAction.CheckBattery:
                    BugReport("MobilePhone.CheckBattery");
                    #region Проверка заряда батареи для драк с паханом
                    if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 0); //Одеваем стандартный прикид, иначе может не быть телефона
                    GoToPlace(MainWB, Place.Mobile);
                    Wait(2000, 2500); //После перехода на страничку мобильника на заднем плане идёт POST запрос, ожидаем завершения.
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("phone");
                    if (HtmlEl != null && !HtmlEl.GetAttribute("classname").Contains("turned-on"))
                    {
                        UpdateStatus("@ " + DateTime.Now + " Ой-вэй, никто не видел моей мобильник?!? -А не твоих ли это рук дело Босс?");
                        TSTimeout = new TimeSpan(2, 0, 0); //У меня походу нет телефона, заглянуть через 2 часа.
                    }
                    else //У меня есть телефон
                    {
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("battery-indicator");
                        if (HtmlEl != null)
                        {
                            if (HtmlEl.InnerText.Contains("0 из 4")) TimeSpan.TryParse(Regex.Match(HtmlEl.InnerText, "(?<=[(]).*(?=[)])").Value, out TSTimeout); //Закончилась батарея, следующий раз не ранее чем через хх:хх
                        }                        
                        else 
                        {
                            UpdateStatus("@ " + DateTime.Now + " Босс ты гурман! Мобильник в кармане, а номер в чемодане? Не дорогой ли выходит кирпич?");
                            TSTimeout = new TimeSpan(2, 0, 0); //У меня есть телефон но нет номера..., заглянуть через 2 часа.
                        }
                    }
                    #endregion
                    break;
                case MobilePhoneAction.Repair:
                    BugReport("MobilePhone.Repair");
                    #region Сбор мобильных телефонов
                    RepairMobile.ID = null; //Обнуление информации о мобильных телефонах.

                    GoToPlace(MainWB, Place.Gorbushka);
                    object info = frmMain.GetJavaVar(MainWB, "$(\"#content .phone-list\").html()");
                    if (info != DBNull.Value)
                    {
                        MatchCollection matches = Regex.Matches((string)info, "(?<=data-id=(\")?)([0-9])+");
                        foreach (Match m in matches)
                        {
                            GoToPlace(MainWB, Place.Gorbushka, "/" + m.Value);
                            #region Переход к включению + включение телефона (если необходимо) + список телефонов на продажу
                            if (frmMain.GetDocument(MainWB).GetElementById("phone_" + m.Value) == null)
                            {                                
                                frmMain.InvokeScript(MainWB, "eval", new object[] { "AngryAjax.goToUrl('/phone/call/setPhone/" + m.Value + "');" });
                                IsWBComplete(MainWB);
                                #region Включение телефона + список телефонов на продажу.
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("phoneOnForm");
                                if (HtmlEl != null)
                                {
                                    frmMain.InvokeMember(MainWB, HtmlEl, "submit");
                                    IsWBComplete(MainWB);                                    
                                }
                                else
                                {
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("pincode");
                                    if (HtmlEl != null)
                                    {
                                        HtmlEl.InnerText = new Random().Next(1000, 10000).ToString();
                                        while ((HtmlEl = frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("button")[0]) != null && HtmlEl.InnerText.Contains("Ввести пин-код"))
                                        {
                                            UpdateStatus("@ " + DateTime.Now + " Вспоминая мастер-класс по хакингу великой Лены Александровны, я приступил к подбору комбинации!");
                                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/call/pincode/", "ajax=1&phone_id=" + m.Value, "Referer: http://" + Settings.ServerURL + "/phone/call/setPhone/" + m.Value + "/");
                                            Wait(1000, 1500);
                                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/phone/call/setPhone/" + m.Value + "/"); //frmMain.AngryAjax(MainWB, "phone/call/setPhone/" + m.Value); //frmMain.GoBack(MainWB);                                            
                                            IsWBComplete(MainWB);
                                        }
                                        if (frmMain.GetDocument(MainWB).GetElementById("table_phone_number") != null) UpdateStatus("@ " + DateTime.Now + " Ядрён-Батон! Получилось! Сломал! - Не не телефон, пин код...");
                                    }
                                    else 
                                    {
                                        #region Создаём список полностью собранных телефонов.
                                        info = frmMain.GetJavaVar(MainWB, "m.items['" + m.Value + "'].info.title");
                                        if (info != null && Regex.IsMatch((string)info, "(Мутарола|Телефон «(Неубиваемый|There is no Spoon)»)")
                                            && !Regex.IsMatch((string)frmMain.GetJavaVar(MainWB, "m.items['" + m.Value + "'].info.content"), "Номер телефона: ([0-9])+")) //только дешёвые и безномерные телефоны
                                        {
                                            Array.Resize<int>(ref RepairMobile.ID, RepairMobile.ID == null ? 1 : RepairMobile.ID.Count<int>() + 1);
                                            RepairMobile.ID[RepairMobile.ID.Count<int>() - 1] = Convert.ToInt32(m.Value);
                                        }
                                        #endregion
                                    }                                                                       
                                } 
                                #endregion
                                frmMain.InvokeScript(MainWB, "eval", new object[] { "AngryAjax.goToUrl('/tverskaya/gorbushka/" + m.Value + "');" }); //Телефон включён, переходим к поиску запчастей. 
                                IsWBComplete(MainWB);                                                               
                            }
                            #endregion

                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("phone_" + m.Value);
                            while (HtmlEl != null && HtmlEl.GetElementsByTagName("button").Count == 2)
                            {
                                if (HtmlEl.GetElementsByTagName("button")[0].InnerText.Contains("бесплатно"))
                                {
                                    #region Бесплатный поиск необходимой детали.
                                    frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click");
                                    IsWBComplete(MainWB, 600, 1000); //IsAjaxComplete(MainWB, 600, 1000);
                                    #endregion
                                }
                                else
                                {
                                    #region Считывание таймера бесплатного поиска.
                                    info = frmMain.GetJavaVar(MainWB, "$(\"#phone_" + m.Value + " .holders .timer\").text()");
                                    if (info != null && TimeSpan.TryParse((string)info, out TSTimeout))
                                    {
                                        if (RepairMobile.NextDT < DateTime.Now.Add(TSTimeout)) RepairMobile.NextDT = DateTime.Now.Add(TSTimeout);
                                        break; //найден таймаут, нет необходимости дальше мусолить этот телефон!
                                    }
                                    else break; //2 кнопки нет бесплатно и нет таймера, выходим. (на всякий пожарный)
                                    #endregion
                                }
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("phone_" + m.Value); //Деталь могла быть найдена, переходим к поиску следующей.
                            }
                        }
                    }
                    if (RepairMobile.NextDT < DateTime.Now) RepairMobile.NextDT = DateTime.Now.AddHours(2); //Нет телефонов для апгрэйда, проверить позже или докупить.
                    #region Продажа ненужных телефонов.
                    if (Settings.SellRepairMobile && RepairMobile.ID != null) 
                    {
                        GoToPlace(MainWB, Place.Shop, "/section/mine");
                        foreach (int ID in RepairMobile.ID)
                        {
                            Wait(3000, 6000); //Создаём иллюзию поиска ненужных телефонов в багаже перед удалением
                            object[] Args = new object[3] { ID, "/shop/section/mine/", 1 };
                            frmMain.InvokeScript(MainWB, "shopSellItem", Args);  //shopSellItem('118468851', '/shop/section/mine/', 1);
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                        }
                    }
                    #endregion
                    #endregion
                    break;
                case MobilePhoneAction.MafiaTrade:
                    BugReport("MobilePhone.MafiaTrade");
                    #region Обмен чёрных бухгалтерий на бонусы
                    GoToPlace(MainWB, Place.Mobile);
                    frmMain.GetDocument(MainWB).GetElementById("app-desktop").GetElementsByTagName("img")[1].InvokeMember("click"); //Переходим к обменнику! (Без этого кнопка не срабатывает)
                    IsWBComplete(MainWB, 2000, 2500);
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("app-trade");
                    if (HtmlEl.Style != null && Regex.IsMatch(HtmlEl.Style, "display: block", RegexOptions.IgnoreCase)) //Форма с кнопкой начать обмен?
                    {
                        frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click");
                        IsWBComplete(MainWB, 2000, 2500); //IsAjaxComplete(MainWB, 2000, 2500); //Тут только аяксом проходит
                    }
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("app-trade2");
                    if (HtmlEl.Style == null) Me.Petriki.BlackBook = 0; //HtmlEl.Style == null -> Начать бой! Книжек больше нет!
                    if (HtmlEl.Style != null && Regex.IsMatch(HtmlEl.Style, "display: block", RegexOptions.IgnoreCase)) //Форма обмена?
                    {
                        #region Получим ли чашки кофе?
                        foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("app-trade2").GetElementsByTagName("img"))
                        {
                            if (H.GetAttribute("title").Contains("Чашка с кофе")) { Me.Petriki.NeedCoffee -= 3; break; }
                        }
                        #endregion
                        frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click");
                        IsWBComplete(MainWB, 500, 1000); //Тут вроде нормально проходит.
                    } 
                    #endregion
                    break;
            }            
            return TSTimeout;
        }
        private void RechargeFightBear()
        {
            if (!Me.Bear.Stop || Me.Bear.Val == 0) //Ешё не проверял или пока небыло медведя.
            {
                GoToPlace(MainWB, Place.Home);
                foreach (string Text in GetArrClassHtml(MainWB, "$(\"#content .home-medicine .button\");", "innerText"))
                {
                    if (Text.Contains("Мишка")) { Me.Bear.Val = 1; break; } //Медведь найден!
                }
                Me.Bear.Stop = true; //Проверка на наличие медведя пройдена.               
            }
            if (Me.Bear.Val == 1) //У меня есть медведь!
            {
                if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/(player|home)/$")) GoToPlace(MainWB, Place.Home); //Если не в багаже и не в хате симулируем переход в хату для перехода к медведю.
                GoToPlace(MainWB, Place.Bear);
                /*   //Определение таймаута между боями у медведя, пока незнаю зачем можно использвать!
                object Info = frmMain.GetJavaVar(MainWB, "$(\"#companion-state .cooldown .red\").text();");
                if (Info != DBNull.Value) Me.Bear.LastDT = DateTime.Now.Add(TimeSpan.Parse((string)Info)); //У медведика ещё таймаут использования в бою!
                */
                if (Convert.ToInt32(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("bear_battery").InnerText, "([0-9])+").Value) < 20) //Батарейка пуста или всего 10% если скажем небыло батареек!
                {
                    UpdateStatus("@ " + DateTime.Now + " Вставлю ка я медведю ... батарейки, почему у китайцев всё через это место, а?");
                    frmMain.InvokeScript(MainWB, "bearCharge"); //Заряжаем медведика.                    
                    IsWBComplete(MainWB);
                    if (Convert.ToInt32(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("bear_battery").InnerText, "([0-9])+").Value) < 20 //Видимо нет батареек
                        && Me.Bear.LastDT < DateTime.Now)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Эээне, не фурычит ... нужны новые, полежи я к тебе ешё загляну!");
                        Me.Bear.LastDT = DateTime.Now.AddHours(2); //Проверить наличие батарейки через 2 часа!
                    }
                }
                if (Me.Bear.LastDT < DateTime.Now) Me.Bear.LastDT = DateTime.Now.AddHours(4); //Заряжен, проверить наличие батарейки через 4 часа! (При использовании время будет откоректированно!)
                frmMain.GetDocument(MainWB).GetElementById("pet-scratch").All[0].InvokeMember("onclick");
                UpdateStatus("# " + DateTime.Now + " Почесал мохнатого, а чё прикольненько!");
            }
            else Me.Bear.LastDT = DateTime.Now.AddHours(15); //Даже если у меня на след. секунде появиться медведь для проведения 5 драк, нужно 15 часов!
        }
        private void BuildTurret()
        {
            if (GoToPlace(MainWB, Place.Turret))
            {
                #region Инициализация
                Me.Turret = new stcTurret();
                #endregion
                foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("button"))
                {
                    MatchCollection matches = Regex.Matches(H.InnerHtml, "Искать –([\\s\\S])+class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)");
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                        {
                            switch (match.Groups["Unit"].Value)
                            {
                                case "tugriki":
                                    Me.Turret.PriceTugriki = Convert.ToInt32(match.Groups["Cost"].Value);
                                    break;
                                case "ruda":
                                    Me.Turret.PriceOre = Convert.ToInt32(match.Groups["Cost"].Value);
                                    break;
                                case "oil":
                                    Me.Turret.PriceOil = Convert.ToInt32(match.Groups["Cost"].Value);
                                    break;
                                case "med":
                                    Me.Turret.PriceMed = Convert.ToInt32(match.Groups["Cost"].Value);
                                    break;
                            }
                        }
                        UpdateMyInfo(MainWB);
                        if (Me.Turret.PriceMed == 0 && Me.Turret.PriceTugriki <= Me.Wallet.Money && Me.Turret.PriceOre <= Me.Wallet.Ore && Me.Turret.PriceOil <= Me.Wallet.Oil)
                        {
                            UpdateStatus("@ " + DateTime.Now + " Ого да тут толпа деталь от турели ищет, и я поищу, вдруг я найду а?");
                            frmMain.InvokeMember(MainWB, H, "click");
                            IsWBComplete(MainWB);
                            Me.Turret.LastDT = GetServerTime(MainWB);
                        }
                        break;
                    }                    
                }
                if (Me.Turret.Equals(new stcTurret())) Me.Turret.LastDT = GetServerTime(MainWB).Date.AddHours(new Random().Next(25, 29)); //Уже кликали, переносим на следующий день.
            }
        }
        private bool IsGrpFightTimePermission(GroupFightType GFT)
        {
            bool bRet = true;
            DateTime DT = GetServerTime(MainWB);
            if (GrpFightTaskManager.Arts != null && GrpFightTaskManager.Arts.Count() > (int)GFT && GrpFightTaskManager.Arts[(int)GFT].Tasks != null)
            {
                for (int i = 0; i < GrpFightTaskManager.Arts[(int)GFT].Tasks.Count(); i++)
                {
                    if (GrpFightTaskManager.Arts[(int)GFT].Tasks[i].DoW == DT.DayOfWeek)
                    {
                        for (int k = 0; k < GrpFightTaskManager.Arts[(int)GFT].Tasks[i].StartTS.Count(); k++) //Если есть хоть одно указание временного интервала проверить его, и вернуть false, если не подошло!
                        {
                            bRet = false; //Обнаружено задание на данный день! Обязательно проверить время!
                            bRet |= IsTimeInTimespan(GrpFightTaskManager.Arts[(int)GFT].Tasks[i].StartTS[k], GrpFightTaskManager.Arts[(int)GFT].Tasks[i].StopTS[k], DT.TimeOfDay);
                            if (bRet) return true; //Был найден диапозон в котором разрешенно, всё в порядке!
                        }
                    }
                }
            }
            return bRet;
        }
        private bool CatchHTMLBug(WebBrowser WB)
        {
            if (frmMain.GetDocumentURL(WB) == "about:blank") return false;
            else 
            {
                if (frmMain.GetDocumentText(WB) != null) //Так как работа со многими потоками, из за асинхронности, навигация может начаться, кокраз в момент обработки страницы!
                {
                    #region В игру были введены новые правила, нужно согласиться?
                    if (frmMain.GetDocument(WB).GetElementById("licence_agree") != null)
                    {
                        frmMain.GetDocument(WB).GetElementById("agree").SetAttribute("checked", "true");
                        UpdateStatus("! " + DateTime.Now + " Соглашаюсь с новыми правилами, без них не пускають!");
                        frmMain.GetDocument(WB).GetElementsByTagName("form")[0].InvokeMember("submit");
                        return true;
                    }
                    #endregion
                    #region Я поймал травму?
                    HtmlElement[] HC = frmMain.GetElementsById(WB, "alert-text");
                    if (HC != null)
                    {
                        foreach (HtmlElement H in HC)
                        {
                            if (H.InnerText.Contains("Вы не можете драться, пока у вас есть травмы.)"))
                            {
                                string URL = frmMain.GetDocumentURL(WB);
                                Me.Trauma.Stop = Trauma(TraumaAction.Check, true);
                                frmMain.NavigateURL(WB, URL);
                                return true;
                            }
                        }
                    }
                    #endregion
                    return false;
                }
            }            
            return true;
        }
        private string ReadToolTip(WebBrowser WB, HtmlElement HtmlEl)
        {
            bool Minimized = IsIconic(FrmMainhWnd); //Определяем состояние бота.
            bool Hidden = IsWindowVisible(FrmMainhWnd); //Определяем состояние бота.
            #region Находим hWnd для "Internet Explorer_Server"
            IntPtr IEptr = frmMain.GetHandle(WB);
            IEptr = FindWindowEx(IEptr, IntPtr.Zero, "Shell Embedding", null);
            IEptr = FindWindowEx(IEptr, IntPtr.Zero, "Shell DocObject View", null);
            IEptr = FindWindowEx(IEptr, IntPtr.Zero, "Internet Explorer_Server", null);
            #endregion                        
            #region Бот свёрнут, необходимо развернуть?
            if (Minimized) 
            {
                SetWindowLong(FrmMainhWnd, GWL_EXSTYLE, GetWindowLong(FrmMainhWnd, GWL_EXSTYLE) ^ WS_EX_LAYERED); //добавляем окну атрибут прозрачности (^ -> XOR)
                SetLayeredWindowAttributes(FrmMainhWnd, 0, 0, LWA_ALPHA); //Устанавливаем прозрачность = 0%
                ShowWindow(FrmMainhWnd, SW_SHOWNOACTIVATE); //Бот свёрнут, необходимо развернуть
            }
            #endregion
            #region Высчитываем координаты HtmlEl относительно "Internet Explorer_Server"
            int x = HtmlEl.OffsetRectangle.Width / 2, y = HtmlEl.OffsetRectangle.Height / 2; //Инициализация
            while (HtmlEl.OffsetParent != null) 
            {
                x += HtmlEl.OffsetRectangle.X;
                y += HtmlEl.OffsetRectangle.Y;
                HtmlEl = HtmlEl.OffsetParent;
            }
            #endregion
            #region Проверяем и выставляем поле видимости элемента в бровзере.
            HtmlEl = frmMain.GetDocument(WB).GetElementsByTagName("HTML")[0]; 
            int OffsetX = HtmlEl.ScrollRectangle.X; int OffsetY = HtmlEl.ScrollRectangle.Y;            
            if (x < OffsetX || OffsetX + WB.Width < x) OffsetX = x - WB.Width / 2;
            if (y < OffsetY || OffsetY + WB.Height < y) OffsetY = y - WB.Height / 2;
            frmMain.ScrollTo(WB, OffsetX, OffsetY);
            x -= frmMain.GetDocument(WB).GetElementsByTagName("HTML")[0].ScrollRectangle.X; //Корректировка на скроллер X
            y -= frmMain.GetDocument(WB).GetElementsByTagName("HTML")[0].ScrollRectangle.Y;  //Корректировка на скроллер Y
            #endregion

            SendMessage(IEptr, WM_MOUSEMOVE, (IntPtr)0x0, (IntPtr)(int)((x & 0xFFFF) | (y << 16)));
            #region Бот был свёрнут, необходимо упрятать?
            if (Minimized) 
            {
                ShowWindow(FrmMainhWnd, SW_SHOWMINNOACTIVE); //Бот был свёрнут, необходимо упрятать
                SetWindowLong(FrmMainhWnd, GWL_EXSTYLE, GetWindowLong(FrmMainhWnd, GWL_EXSTYLE) ^ WS_EX_LAYERED); //добавляем окну атрибут прозрачности (^ -> XOR)
                SetLayeredWindowAttributes(FrmMainhWnd, 0, 255, LWA_ALPHA); //Устанавливаем прозрачность = 100%
            }
            #endregion
            HtmlEl = frmMain.GetDocument(WB).GetElementById("tooltipHolder");
            return HtmlEl == null ? "" : HtmlEl.InnerHtml; //Если окошко свёрнуто, то считать не получается
        }
        private void ReadChat()
        {
            while (true)
            {
                IsWBComplete(MainWB);
                if (frmMain.IsMultiFrame(MainWB))
                {
                    MatchCollection matches = Regex.Matches(frmMain.GetDocument(MainWB, 1).GetElementById("messages").InnerHtml, " с Паханом (?<Lvl>([0-9])+) уровня, он начнется в (?<StartDT>([0-9- :])+)"); // с Паханом 20 уровня, он начнется в 2014-01-25 14:36:00
                    if (matches.Count > 0)
                    {
                        DateTime StartDT = Convert.ToDateTime(matches[matches.Count - 1].Groups["StartDT"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                        DateTime ServerDT = GetServerTime(MainWB);
                        if (Me.Player.Level == Convert.ToInt32(matches[matches.Count - 1].Groups["Lvl"].Value) && ServerDT < StartDT && GrpFight.Mafia.LastFightDT < StartDT)
                        {
                            UpdateStatus("@ " + DateTime.Now + " \"Дятел\" из чата сообщает о начале организованной атаки на пахана в " + StartDT.TimeOfDay + " (Московское время)");
                            GrpFight.Mafia.LastFightDT = StartDT;
                            if (GrpFight.Mafia.NextFightDT < StartDT) //Заносить начало драки только, если у меня есть информация, что ещё есть аккумулятор!
                            {
                                GrpFight.Mafia.FightFound = true;
                                GrpFight.Mafia.NextFightDT = StartDT;
                                GrpFight.Mafia.LastCheckDT = DateTime.Now.AddSeconds(30); //Найден бой, мониторим когда наберётся народ!
                            }                       
                        }
                    }  
                }
                Thread.Sleep(10000);               
            }           
        }
        /// <param name="AjaxClassPath">Путь вида "$(\"#ID .ClassName\");"</param>
        private int GetArrClassCount(WebBrowser WB, string AjaxClassPath)
        {
            BugReport("GetArrClassCount");
           
            IsWBComplete(WB);
            frmMain.GetJavaVar(WB, "var $ArrClass = " + AjaxClassPath);
            return (int)(frmMain.GetJavaVar(WB, "$ArrClass.length") ?? 0);
        }
        /// <param name="AjaxClassPath">Путь вида "$(\"#ID .ClassName\");"</param>
        /// <param name="Attr">Атрибут: innerHTML, innerText или getAttribute(\"xxx\")</param>
        private string[] GetArrClassHtml(WebBrowser WB, string AjaxClassPath, string Attr)
        {
            BugReport("GetArrClassHtml");

            IsWBComplete(WB);
            frmMain.GetJavaVar(WB, "var $ArrClass = " + AjaxClassPath); //var $ArrClass = $(\"#fightGroupForm .fight-log .helmet\");
            string[] sArrRet = new string[(int)(frmMain.GetJavaVar(WB, "$ArrClass.length") ?? 0)];
            for (int i = 0; i < sArrRet.Count<string>(); i++)
            {               
                sArrRet[i] = (string)frmMain.GetJavaVar(WB, "$ArrClass[" + i + "]." + Attr);
            }
            return sArrRet;
        }
        private dynamic[] GetArrClass(WebBrowser WB, string AjaxClassPath)
        {
            BugReport("GetArrClassHtml");

            IsWBComplete(WB);
            frmMain.GetJavaVar(WB, "var $ArrClass = " + AjaxClassPath); //var $ArrClass = $(\"#fightGroupForm .fight-log .helmet\");
            dynamic[] sArrRet = new dynamic[frmMain.GetJavaVar(WB, "$ArrClass.length") != null ? (int)frmMain.GetJavaVar(WB, "$ArrClass.length") : 0];
            for (int i = 0; i < sArrRet.Count<dynamic>(); i++)
            {
                sArrRet[i] = frmMain.GetJavaVar(WB, "$ArrClass[" + i + "]");
            }
            return sArrRet;
        }
        #endregion
        #region Main Functions
        public void SetAjaxTrap(WebBrowser WB, bool SetTrap = true)
        {         
          frmMain.SetJavaVar(WB, "$.ajaxSettings.beforeSend", "function() {if (!noAjaxLoader)$('.loading-top').show();" + (SetTrap ? "window.external.AjaxCallBack(\"" + WB.Name + "\",\"Loading\");" : "") + " }");
          frmMain.SetJavaVar(WB, "$.ajaxSettings.complete", "function() {$('.loading-top').hide(); " + (SetTrap ? "window.external.AjaxCallBack(\"" + WB.Name + "\",\"Ready\");" : "") + "}");            
        }
        public void AjaxCallBack(string WBName, string status)
        {            
            if (DebugMode) BugReport("* AjaxCallBack: " + status);
            MyMainForm.Controls.Find(WBName, true)[0].Tag = status; //Находим нужный контрол в коллекции и перезаписываем состояние бровзера
        }
        public bool IsTimeInTimespan(TimeSpan StartTime, TimeSpan StopTime, TimeSpan CurrentTime)
        {
            if (StartTime > StopTime) //23:40-9:40
            {
                if (CurrentTime > StartTime || CurrentTime < StopTime) return true; //Время в диапозоне!
            }
            if (StartTime < StopTime) //9:40-23:40; 
            {
                if (CurrentTime > StartTime && CurrentTime < StopTime) return true; //Время в диапозоне!
            }
            return false;
        }        
        public void UpdateStatus(string S, int M = 0, int R = 0, int N = 0, int L = 0, int B = 0, int T = 0) //OK 
        {
            Me.Status.iM += M; Me.Status.iR += R; Me.Status.iN += N; Me.Status.iL += L; Me.Status.iB += B; Me.Status.iT += T; //Save new info

            if (M != 0) S += Math.Abs(M) + " тугрик/ов, ";
            if (R != 0) S += Math.Abs(R) + " руду/ы, ";
            if (N != 0) S += Math.Abs(N) + " нефть/и, ";
            if (B != 0) S += Math.Abs(B) + " жетон/а, ";
            if (T != 0) S += Math.Abs(T) + " мобилку";
            if (S.EndsWith(", ")) S = S.Remove(S.Length - 2) + "."; //Удаляем лишние ", " если нужно.
            frmMain.InsertListItem(LBHistory, 0, S); //Записываем текст в историю.
            WriteLogFile(S); //Записываем текст в лог файл.

            frmMain.ToolStripAddText(TS, 0, "Статус:");
            frmMain.ToolStripAddText(TS, 1, S);
            frmMain.ToolStripAddText(TS, 2, "Монет: " + Me.Status.iM);
            frmMain.ToolStripAddText(TS, 3, "Руды: " + Me.Status.iR);
            frmMain.ToolStripAddText(TS, 4, "Нефти: " + Me.Status.iN);
            frmMain.ToolStripAddText(TS, 5, "Ламп: " + Me.Status.iL);
            frmMain.ToolStripAddText(TS, 6, "Жетонов: " + Me.Status.iB);
            frmMain.ToolStripAddText(TS, 7, "Мобилок: " + Me.Status.iT);
        }
        /// <summary>
        /// Функция обновления оповещения в верхушке бота
        /// </summary>
        /// <param name="Message">Новое сообщение (начинается с пробела -> будет выведено красным)</param>
        /// <param name="Add">True ->Добавить, False -> Удалить</param>
        public void UpdateMessageInfo(string Message, bool Add)
        {
            frmMain.AddLabelText(LUserMessage, Add ? LUserMessage.Text + Environment.NewLine + Environment.NewLine + Message : Regex.Replace(LUserMessage.Text, "(\r\n)*" + Message, ""));
        }
        public bool WebLogin(WebBrowser WB) //OK
        {
            BugReport("WebLogin");

            IsWBComplete(WB);
            try
            {
                if (Settings.Email == "" || Settings.Password == "")                   
                {
                    if (frmMain.GetDocumentURL(WB).EndsWith(Settings.ServerURL + "/")) 
                    {
                        UpdateMessageInfo(" Вход в игру невозможен, ввиду отсутствия данных о Логин-Пароле!", true);
                        while (Settings.Email == "" || Settings.Password == "")
                        {
                            Wait(45000, 60000, "! Явка провалена, без логина не пускают, сижу курю бамбук до :");
                        }
                        return WebLogin(WB);
                    }
                    else UpdateMessageInfo(" Не сохранены данные Логин-Пароля, вход в игру будет невозможен!", true);                  
                } 
                else 
                {
                    if (frmMain.GetDocumentURL(WB).EndsWith(Settings.ServerURL + "/"))
                    {
                        UpdateStatus("@ " + DateTime.Now + " Бл@, снова фэйс контроль? - Вот мой логин, вот мой пароль, я войду?");
                        frmMain.GetDocument(WB).GetElementById("login-email").InnerText = Settings.Email;
                        frmMain.GetDocument(WB).GetElementById("login-password").InnerText = Settings.Password;
                        frmMain.InvokeMember(WB, frmMain.GetDocument(WB).Forms[0], "submit");
                        IsWBComplete(WB, 3000, 5000);
                        return frmMain.GetDocumentURL(WB).EndsWith("/player/");
                    }
                    return true;
                }
            }
            catch
            {
                frmMain.NavigateURL(WB, Settings.ServerURL);
            }            
            return false;
        }
        public DateTime GetServerTime(WebBrowser WB) //OK
        {
            //BugReport("GetServerTime");            

            DateTime ServerDT = new DateTime();
            do //Дабы дать возможность броузеру обработать время! 
            {
                IsWBComplete(WB);
                HtmlElement HtmlEl = frmMain.GetDocument(WB).GetElementById("servertime");
                if (HtmlEl != null)
                {
                    ServerDT = new DateTime(1970, 1, 1).AddSeconds(Convert.ToInt64(HtmlEl.GetAttribute("rel")) + 3 * 60 * 60); // Серверное (Московское) время опережает UTC на 3 часа
                }
                else AnalysePlace(WB);
            }
            while (ServerDT == new DateTime());
            return ServerDT;
        }        
        public bool IsWBComplete(WebBrowser WB, int WaitMinMs = 0, int WaitMaxMs = 0) //OK 
        {
            #region Инициализация
            int Retries = 0; //
            #endregion
            do
            {
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                while (!frmMain.IsComplete(WB) || !WB.Tag.Equals("Ready")) // WB.Tag.Equals("Loading") || WB.Tag.Equals("Error")
                {                    
                    if (WB.InvokeRequired) Thread.Sleep(50);
                    else Application.DoEvents();
                    #region Reconnect?
                    if (WB.Tag.Equals("Error") || MonitorDT < DateTime.Now || Retries == 9)
                    {
                        Retries += (Retries < 9) ? 1 : 0; //тоже может генерировать реконнект!
                        if (WB.Tag.Equals("Error")) Wait(500, 10000, " Активирована задержка до: ");
                        #region 3x Reconnect?
                        if (Retries % 3 == 0)
                        {
                            if (Retries % 9 == 0) RestartBotInstance(true); //Перезапуск братца!                             
                            UpdateStatus("! " + DateTime.Now + " Всё, устал, так и попу порвать можно! Пробую слабительное.");
                            frmMain.Reconnect(WB, Settings.ServerURL); //Проблема с соединением!
                        }
                        else 
                        {
                            UpdateStatus("! " + DateTime.Now + " Вынимаю затычку из задницы, тужусь!");
                            frmMain.RefreshURL(WB, Settings.ServerURL);
                        }
                        #endregion
                        MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                    }
                    #endregion
                }
                if (MonitorDT < DateTime.Now) { MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE); Retries++; }
            }
            while (CatchHTMLBug(WB)); //Проверка на ошибки.
            #region Proxy?
            if (Settings.UseProxy) { WaitMinMs += (int)Expert.ProxyMin; WaitMaxMs += (int)Expert.ProxyMin; };
            #endregion
            Wait(WaitMinMs, WaitMaxMs);

            #region Нас приглашают в группу для спуска в подземку?
            HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-groups-invited");
            if (HtmlEl != null)
            {
                Regex regex = new Regex("Игрок (.+) приглашает");
                MatchCollection matches = regex.Matches(HtmlEl.InnerText);
                UpdateStatus("@ " + DateTime.Now + " " + matches[0].Value + " меня в подземку. Спасибо, как-нибудь в другой раз...");

                foreach (HtmlElement elem in HtmlEl.GetElementsByTagName("div"))
                {
                    if (elem.InnerText == "Отклонить")
                    {
                        frmMain.InvokeMember(MainWB, elem, "click");
                        IsWBComplete(MainWB);
                        break;
                    }
                }
            }
            #endregion

            return true;
        }
        public void IsWBCompleteEx(WebBrowser WB)
        {
            #region Инициализация
            int Retries = 0; //
            #endregion
            do
            {
                DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                while (!frmMain.IsComplete(WB) || (!WB.Tag.Equals("Ready") && !WB.Tag.Equals("PreReady")))
                {
                    if (WB.InvokeRequired) Thread.Sleep(50);
                    else Application.DoEvents();
                    #region Reconnect?
                    if (WB.Tag.Equals("Error") || MonitorDT < DateTime.Now || Retries == 9)
                    {
                        Retries += (Retries < 9) ? 1 : 0; //тоже может генерировать реконнект!
                        if (WB.Tag.Equals("Error")) Wait(500, 10000, " Активирована задержка до: ");
                        #region 3x Reconnect?
                        if (Retries % 3 == 0)
                        {
                            if (Retries % 9 == 0) RestartBotInstance(true); //Перезапуск братца!                             
                            UpdateStatus("! " + DateTime.Now + " Всё, устал, так и попу порвать можно! Пробую слабительное.");
                            frmMain.Reconnect(WB, Settings.ServerURL); //Проблема с соединением!
                        }
                        else
                        {
                            UpdateStatus("! " + DateTime.Now + " Вынимаю затычку из задницы, тужусь!");
                            frmMain.RefreshURL(WB, Settings.ServerURL);
                        }
                        #endregion
                        MonitorDT = DateTime.Now.AddSeconds(25);
                    }
                    #endregion
                }
                if (MonitorDT < DateTime.Now) { MonitorDT = DateTime.Now.AddSeconds(25); Retries++; }
            }
            while (CatchHTMLBug(WB)); //Проверка на ошибки.
            Wait(2000, 3000);
        }
        public bool IsTimeout(WebBrowser WB, bool Analyse = false, bool WaitTimer = true, string S = "", TimeOutAction TA = TimeOutAction.Busy) //OK
        {
            BugReport("IsTimeout");

            HtmlElement HtmlEl;
        ReTry:
            IsWBComplete(WB); //Пауза чтоб дать возможность бровзеру обработать считанные данные
            #region Анализ времяпровождения таймаута
            object Info = frmMain.GetJavaVar(WB, "$(\"#personal .bubble\").html()");
            if (Analyse && Info != DBNull.Value) //DBNull ибо при ненахождении возврашается класс а не просто строка.
            {
                Match match = Regex.Match((string)Info, "[>](?<Text>([\\w ])+)[<](.*[>](?<Timeout>([0-9:])+))?"); //Извлекаем информацию о том где мы и чем заняты.
                switch (match.Groups["Text"].Value)
                {
                    case "На показе":
                        break;
                    case "В Шаурбургерсе":
                        MC(MCAction.Check);
                        break;
                    case "Ожидание боя":                        
                        #region Переодевание
                        if (Settings.UseWearSet) WearSet(WB, ArrWearSet, 0);

                        #endregion                        
                        if (match.Groups["Timeout"].Success)
                        {
                            if (TimeSpan.Parse(match.Groups["Timeout"].Value) > new TimeSpan(0, 15, 0)) goto ReTry; //Считанное время более 15 минут при ожидании драки? -хм...
                            Wait(TimeSpan.Parse(match.Groups["Timeout"].Value)," Ухты, драчка намечается, загляну ка я на огонёк в: ", TimeOutAction.Busy); //Записан в бой, просто используем таймаут до начала боя.
                        }
                        goto case "В Записи";
                    case "В Записи":
                        #region Выбивание странички с дракой
                        do
                        {
                            Wait(5000, 10000);
                            GoToPlace(WB, Place.Alley, "", false); //
                            Info = frmMain.GetJavaVar(WB, "$(\"#personal .bubble\").html()");                             
                        } while (Info != DBNull.Value && Regex.Match((string) Info, "[>]([\\w ])+[<]").Value == "В Записи"); //Всё ещё ожидаем начала боя?
                        #endregion
                        GroupFight(GroupFightAction.Fight);
                        break;                    
                    case "Спуск в метро":
                    case "Ожидание в метро":                    
                    case "Поиск крысомах":
                    case "Игра с Моней Шацом":
                        Metro(MetroAction.Check);
                        break;
                    case "В патруле":
                        Patrol(PatrolAction.Check);
                        break;
                    case "В бункере":
                        break;
                    case "Задержан за бои":
                        Police(PoliceAction.Check);
                        break;                   
                    default:
                        #region Найден неизвестный до сели Баббл, создаем его описание и делаем по старинке.
                        StreamWriter SW = new StreamWriter("Bubble.txt");
                        SW.WriteLine(Info);
                        SW.Close();                       
                        
/*
                        switch (match.Groups["Area"].Value)
                        {
                            case "metro": Metro(MetroAction.Check); break; //Больше не занят в Метро, снова к дракам!
                            case "shaurburgers": MC(MCAction.Check); break; //Больше не занят в MC, снова к дракам!
                            case "police": Police(PoliceAction.Check); break; //Больше не в Милиции, снова к дракам!
                            case "fight": GroupFight(GroupFightAction.Fight); break;
                            case "alley": Patrol(PatrolAction.Check); break; //Не патрулируем! снова к дракам!
                        }
*/
                        #endregion
                        break;
                }
            }          
            #endregion 
         
            IsWBComplete(WB); //Пауза чтоб дать возможность бровзеру обработать считанные данные
            HtmlEl = frmMain.GetDocument(WB).GetElementById("timeout"); 
            TimeSpan TS = new TimeSpan();
            TimeSpan.TryParse(HtmlEl.InnerText, out TS);

            if (TS != new TimeSpan() && HtmlEl.Style != "display: none;" && (TS < new TimeSpan(0, 20, 0) || Info != DBNull.Value)) //Убеждаемся, что таймаут менее 20 минут или бабл, - нас наёбывают!
            {
                if (TS > new TimeSpan(0, 20, 0)) BugReport("", true, "Timeout", MainWB);
                if (WaitTimer)
                {
                    Wait(TS, S, TA);
                    return false;
                }
                if (S != "") UpdateStatus("# " + DateTime.Now + S + DateTime.Now.Add(TS).ToString("HH:mm:ss"));
                return true;
            }
            return false;
        }
        public void UseTimeOut(TimeOutAction TA = TimeOutAction.Busy) //OK
        {
            DateTime ServerDT;
            Match match;

            #region Одевание стандартного (защитного сэта)
            if (Settings.UseWearSet && TA != TimeOutAction.Blocked) WearSet(MainWB, ArrWearSet,  Settings.Lampofob ? 6 : 0); //стандартный сет!
            #endregion
            #region Отключение режима быстрой загрузки страничек!
            if ((frmMain.GetJavaVar(MainWB, "AngryAjax.turned") ?? "0").Equals("1") && (MainWB.Version.Major < 11 || Settings.MaxIEVersion < 11)) QuickPageLoading(false);
            #endregion
            #region Блокировка активного таймаута (Запретить патруль, копание в метро, хаос).
            Ignore.Timeout = (Settings.OilIgnoreTimeout && !Me.OilHunting.Stop && Settings.GoOil) || (Settings.Lampofob && Me.PerkAntiLamp.On && !Me.NPCHunting.Stop) || (Settings.UseWerewolf && !Me.WerewolfHunting.Stop);
            #endregion

            switch (TA)
            {
                case TimeOutAction.All:
                    BugReport("UseTimeOut.All");
                    Me.MC.Stop = true; //Разрешаем использование Иммунитета у мони, ибо както выбрались из циклов работы MC (тут только если что-то происходило с циклами или инициализация)
                    if (GrpFight.NextCheckDT < DateTime.Now) GroupFight(GroupFightAction.Check, GroupFightType.All); //На всякий случай проверяю не вишу ли во время таймаута в какой нибудь записи в стенку!
                    #region ClanWar + Police + Thimbles + Major + Fitness + Automobile + Pyramid + Casino + Pet + Sovet + Factory + Quest + OilLenin
                        UseTimeOut(TimeOutAction.Free);
                        #endregion

                        bool bRet = false;
                        if (!Ignore.Timeout) //Блокировка активного таймаута (Запретить патруль, копание в метро, хаос).
                        {
                            #region Инициализация
                            ServerDT = GetServerTime(MainWB);
                            #endregion
                            if ((!Settings.GoMetro || Me.Rat.Stop || Me.Rat.LastDT.AddMinutes(30) < ServerDT) && (!Settings.SearchRat || Me.RatHunting.Stop)) //Блокировка MC и Патруля во время драк с крысомахами!
                            {
                                #region MC min money or MC after xx Hours online + Attack + UseTimeOut(Free)

                                if (Settings.GoMC && Me.Wallet.Money <= Settings.minMoneyMC & Me.Player.Level >= 2) MC(clsBot.MCAction.Work, Settings.MCWorkTime); //Нужно ходить в MC, если мало денег?
                                if (DateTime.Now >= Me.Events.SessionStartDT.AddHours(Convert.ToInt32(Settings.MCAfterOnline)) && Me.Player.Level >= 2)
                                {   //Так, как сюда заходит уже по истечению макс времени онлайн, то его тоже нужнно учитывать, ибо обнуление только в конце!
                                    UpdateStatus("# " + DateTime.Now + " УУУУУУаах *зевая* засиделся я тут с вами, схожу ка в MC!");
                                    Me.MC.LastDT = DateTime.Now.AddHours(Convert.ToInt32(Settings.maxMCWorkTime));
                                    while (DateTime.Now < Me.MC.LastDT)
                                    {
                                        Me.MC.Stop = false; //Макдачу, блокируем использование Иммунитета у мони.
                                        MC(clsBot.MCAction.Work, Settings.MCWorkTime);                                    
                                        #region Проводим драку между походами в MC
                                        #region Охота на крысомах
                                        if (Me.RatHunting.RestartDT < DateTime.Now) { Me.RatHunting.Defeats = 0; Me.RatHunting.Stop = false; } //Охота обновляется каждые 24 часа
                                        if (Settings.SearchRat && !Me.RatHunting.Stop && Me.RatHunting.NextDT < DateTime.Now) Metro(MetroAction.SearchRat);
                                        #endregion
                                        #region Ленинопровод
                                        if (Me.OilLeninHunting.RestartDT < DateTime.Now) { Me.OilLeninHunting.Defeats = 0; Me.OilLeninHunting.Stop = false; } //Охота обновляется каждые 24 часа
                                        if (Settings.GoOilLenin && !Me.OilLeninHunting.Stop && Me.OilLeninHunting.NextDT < DateTime.Now) Oil(OilAction.LeninFight); 
                                        #endregion
                                        if (CheckHealthEx(99, Settings.HealMe100, Settings.HealPet50, Settings.HealPet100) && !Me.Trauma.Stop)
                                        {
                                            if (Me.ClanWarInfo.WarStep == 1 & Settings.RemoveEnemy & !CheckImmun(ImmunAction.Tooth)) ClanWar(ClanWarAction.Tooth); //Стадия выбивания зубов, и стоит галочка?
                                            #region Использовать орудия пыток только если будем бить жертв!
                                            if (Settings.AlleyOpponent == Opponent.Victim && (Settings.UseAgent ? Me.AgentHunting.Stop : true) &&
                                                !(Me.ClanWarInfo.Now && Settings.AddClan && (Settings.FarmClan || (Me.ClanWarInfo.WarStep == 1 ? !CheckImmun(ImmunAction.Tooth) : false)))
                                                ) Torture(true);
                                            else Torture(false);
                                            #endregion                                            
                                            #region Переодевание
                                            if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 0);
                                            #endregion
                                            Attack(Settings.AlleyOpponent, Settings.minAlleyLvl, Settings.maxAlleyLvl);
                                        }
                                        #endregion
                                        UseTimeOut(TimeOutAction.Free);
                                    }
                                    Me.MC.Stop = true; //Работы в Шаурбургесе закончены, разрешаем использование Иммунитета у мони.
                                    Me.Events.SessionStartDT = DateTime.Now; //Закончились работы в Шаурбургесе, начинается новый отсчет драк.
                                }
                                #endregion
                            }
                            #region Patrol
                            if (Me.Patrol.LastDT.Date != GetServerTime(MainWB).Date) Me.Patrol.Stop = false;  //Обнуление!
                            if (Settings.GoPatrol && !Me.Patrol.Stop && !TimeToStopAtack(NextTimeout.Patrol)) bRet = Patrol(PatrolAction.Patrol); //Иду в патруль? (Больше не нападаем на крыс или последняя была более 30 минут назад!)
                            #endregion
                            #region Metro
                            if (Me.Rat.LastDT.Date != GetServerTime(MainWB).Date) { Me.Rat.Val = 0; Me.Rat.Stop = false; } //Обнуление побегов от крыс в метро!
                            if (Settings.GoMetro && !Me.Rat.Stop && Me.Player.Level >= 4 && !bRet //Закончился патруль и стоит галка бегать в метро?
                                && !TimeToStopAtack(GetServerTime(MainWB) > Me.Rat.LastDT.AddHours(2) ? NextTimeout.Metro : NextTimeout.Rat) // Драки в ближайшее время не наблюдается + Сосредоточены на охоте на крыс, и уже пошли обычные крыски? Не ходим на обычных не тратим инструменты
                                ) bRet = Metro(MetroAction.Dig); //После обычного копания таймер избегает в функции метро, а если напал на крысомаху то повторяем проверяя поход к моне!
                            #endregion
                        }
                        if (!bRet)
                        {
                            if (TimeToGoGrpFight(GroupFightType.Chaos)) UseTimeOut(TimeOutAction.NoTask); //На случай, если нужно вбежать в хаос не во время таймаута!
                            IsTimeout(MainWB, false, true, " Эээх скукотища, список заданий закончен, валяю дурака до: ", TimeOutAction.NoTask); //Небыл ни в Метро ни в Патруле? - Пробуем войти в хаот или тупо ожидаем истичения таймаута!                                                                    
                        }                                             
                    break;
                case TimeOutAction.NoTask:
                    BugReport("UseTimeOut.NoTask");
                    #region ClanWar + PVP/NPC Fight + OreFight + Police + Thimbles + Safe + Major + Fitness + Automobile + Pyramid + Casino + Pet + Sovet + Factory + Quest
                    UseTimeOut(TimeOutAction.Free);
                    #endregion
                    #region Chaos Fight
                    ServerDT = GetServerTime(MainWB);
                    UpdateMyInfo(MainWB); //Проверяем бабки, уровень полиции итд.
                    match = Regex.Match(GrpFight.Price, "(?<Unit>(tugriki|ruda|neft|med)):(?<Cost>([0-9])+)");
                    if (Settings.GoGroupFightChaos && !Ignore.Timeout && GrpFight.ChaosStartDT <= ServerDT //Блокировка активного таймаута (Запретить патруль, копание в метро, хаос и рудные бои).
                        && (GrpFight.PVPStartDT >= ServerDT.AddMinutes(30) || GrpFight.PVPStartDT.Date != ServerDT.Date) //Блокировка хаос боев во время противостояния!
                        && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.RatHunting) //Не опоздаем ли к крысам
                        && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.OilLenin) //Не опоздаем ли к октябрятам
                        && ( //есть ресурс на оплату входа?
                               (match.Groups["Unit"].Value == "tugriki" && Me.Wallet.Money >= Convert.ToInt32(match.Groups["Cost"].Value))
                            || (match.Groups["Unit"].Value == "ruda" && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value))
                            || (match.Groups["Unit"].Value == "neft" && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value))
                           )
                       ) GroupFight(GroupFightAction.Check, GroupFightType.Chaos); 
                    #endregion                    
                    break;
                case TimeOutAction.Free:
                    BugReport("UseTimeOut.Free");
                    UpdateMyInfo(MainWB); //Проверяем бабки, уровень полиции итд.
                    ServerDT = GetServerTime(MainWB);    
                    #region ClanWar Fight
                    if (Settings.GoClanFight && GrpFight.ClanStartDT <= ServerDT && Me.ClanWarInfo.NextDT <= ServerDT) ClanWar(ClanWarAction.Check);
                    #endregion                    
                    #region PVP
                    if (Settings.GoPVPFight && GrpFight.PVPStartDT <= ServerDT && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.RatHunting) && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.OilLenin)) GroupFight(GroupFightAction.Check, GroupFightType.PVP);
                    #endregion                    
                    #region Ore Fight
                    match = Regex.Match(GrpFight.Price, "(?<Unit>(tugriki|ruda|neft|med)):(?<Cost>([0-9])+)");
                    if (Settings.GoGroupFightOre && !Ignore.Timeout && GrpFight.OreStartDT <= ServerDT //Блокировка активного таймаута (Запретить патруль, копание в метро, хаос и рудные бои).
                        && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.RatHunting) //Не опоздаем ли к крысам
                        && !TimeToStopAtack(NextTimeout.Fight, StopTimeoutType.OilLenin) //Не опоздаем ли к октябрятам
                        && ( //есть ресурс на оплату входа?
                               (match.Groups["Unit"].Value == "tugriki" && Me.Wallet.Money >= Convert.ToInt32(match.Groups["Cost"].Value))
                            || (match.Groups["Unit"].Value == "ruda" && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value))
                            || (match.Groups["Unit"].Value == "neft" && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value))
                           )
                       ) GroupFight(GroupFightAction.Check, GroupFightType.Ore); 
                    #endregion
                    #region Mafia
                    if (Settings.GoGroupFightMafia && Me.Player.Level >= 9 && GrpFight.Mafia.LastCheckDT < DateTime.Now) GroupFight(GroupFightAction.Check, GroupFightType.Mafia);
                    #endregion
                    #region Police
                    if (Me.Police.LastDT <= ServerDT) //Ещё не истекли связи, неважно что могли поменять на взятки или ожидание
                    {
                        if (!Settings.WaitPolice) //Проверка не выставлен, ли режим ожидания?
                        {
                            if (Settings.PayPolice && Me.Police.Val >= Settings.PayPoliceAt) Me.Police.Stop = !Police(PoliceAction.Check);
                            if (!Settings.PayPolice) Me.Police.Stop = !Police(PoliceAction.Relations);
                        }
                        else //Выбран режим ожидания.
                        {
                            if (Me.Police.Val >= Settings.PayPoliceAt) Me.Police.Stop = true; //Уровень слишком высок? - Прекрашаем драки.                              
                        }
                        if (Me.Police.Val == -5) Me.Police.Stop = false; //Уровень упал до -5? -Можно снова драться
                    }                    
                    #endregion
                    #region Thimbles
                    //Проверяем каждые 2 часа + резет в 00:00, быть может мы смогли надыбать билетик.
                    if (ServerDT >= Me.Thimbles.LastDT.AddHours(2) || Me.Thimbles.LastDT.Date != ServerDT.Date) Me.Thimbles.Stop = false;
                    if ((Settings.ThimblesImmunity || Settings.HCRevenge) && Me.MC.Stop) CheckImmun(ImmunAction.Mona); //Проверяем иммунитет от нападений только тут дабы во время МС всегда ходил к моне/банк.
                    if (!Me.Thimbles.Stop && Me.Player.Level >= 5 && ServerDT > Me.Thimbles.StartDT &&
                        ((Me.Wallet.Money >= Settings.PlayThimbles + Settings.minThimblesMoney && (!Settings.GoPyramid || !Me.Pyramid.BlockMonya || !Settings.BlockThimbles) && (Me.BankDeposit.StartDT >= ServerDT.AddHours(2) || !Settings.UseBankDeposit))
                         || (Me.Wallet.Money >= Settings.minWantedPlayThimbles + Settings.minThimblesMoney && Me.Wanted && Settings.WantedPlayThimbles))
                        )
                    {
                        if (!Settings.UseBankDeposit && Me.BankDeposit.MyMoney > 0 && Me.BankDeposit.StartDT < ServerDT) Bank(BankAction.Deposit); //Банк отключён но в нём ещё есть деньги? -Забираем перед сливом.
                        if (!Settings.UseBank || Me.Wallet.Money < Settings.ExchangeBankMoney + Settings.minThimblesMoney || Me.Thimbles.BankStartDT > ServerDT || !Bank(BankAction.Exchange))
                        {
                            Metro(MetroAction.Game); //Пора играть с Моней?
                        }                       
                    }                                                    
                    #endregion                                           
                    #region Major + Fitness + Taxi + Pyramid + Casino + Pet + Sovet + Factory + Quest
                    UseTimeOut(TimeOutAction.Busy); //Пора тренировать Пэта или варить петрики?
                    #endregion
                    #region OilLenin
                    if (Settings.GoOilLenin && !Me.Trauma.Stop && !IsTimeout(MainWB, false, false) && !Me.OilLeninHunting.Stop && Me.OilLeninHunting.NextDT < DateTime.Now) Oil(OilAction.LeninFight); //Симуляция таймаута когда его на самом деле нет?
                    #endregion
                    break;
                case TimeOutAction.Busy:
                    BugReport("UseTimeOut.Busy");
                    UpdateMyInfo(MainWB); //Проверяем бабки, уровень полиции итд.
                    ServerDT = GetServerTime(MainWB);
                    #region Проверяем наличие тикающих предметов в багаже.
                    if (Me.Events.NextItemCheckDT < ServerDT) CheckForUsableItems();
                    #endregion
                    #region Выбитый слот
                    if (Settings.HealInjuredSlot && Me.Wallet.Ore >= 30 && Me.Events.NextSlotInjuredDT < DateTime.Now) CheckHealthEx(0, 0, Settings.HealPet50, Settings.HealPet100); //Пробуем вылечить выбитый слот!
                    #endregion
                    #region Werewolf
                    if (Settings.UseWerewolf && Settings.WerewolfPrice != 0 && Me.WerewolfHunting.StartDT != new DateTime() && Me.WerewolfHunting.StartDT <= ServerDT.AddMinutes(5)) Werewolf(WerewolfAction.Check);
                    #endregion
                    #region Major
                    if (!Me.Major.Stop) Major(MajorAction.Check); //Я небыл мажером, но похоже им стал, определил через аллею, необходимо считывание новых данных!
                    if (Settings.BuyMajor && Me.Major.LastDT < ServerDT.AddDays(1) && Me.Wallet.Honey >= (Me.Major.LastDT > ServerDT ? 17 : 22)) Major(MajorAction.Buy); //Продление мажора
                    #endregion
                    #region MetroWarPrize
                    if (Settings.GetMetroWarPrize && Me.Clan.Name != null && Me.MetroWarPrizeDT <= ServerDT) CheckMetroWarPrize();
                    #endregion
                    #region Fitness
                    if (Settings.TrainMe)
                    {
                        if ((Settings.TrainMeHealth & Settings.maxTrainMeHealth < TrainMeNeed[0, 0] & Me.Wallet.Money >= TrainMeNeed[0, 1])                    //Здоровье
                            || (Settings.TrainMeStrength & TrainMeNeed[1, 0] < Settings.maxTrainMeStrength & Me.Wallet.Money >= TrainMeNeed[1, 1])             //Сила
                            || (Settings.TrainMeDexterity & TrainMeNeed[2, 0] < Settings.maxTrainMeDexterity & Me.Wallet.Money >= TrainMeNeed[2, 1])           //Ловкость
                            || (Settings.TrainMeEndurance & TrainMeNeed[3, 0] < Settings.maxTrainMeEndurance & Me.Wallet.Money >= TrainMeNeed[3, 1])           //Выносливость
                            || (Settings.TrainMeCunning & TrainMeNeed[4, 0] < Settings.maxTrainMeCunning & Me.Wallet.Money >= TrainMeNeed[4, 1])               //Хитрость
                            || (Settings.TrainMeAttentiveness & TrainMeNeed[5, 0] < Settings.maxTrainMeAttentiveness & Me.Wallet.Money >= TrainMeNeed[5, 1])   //Внимательность
                            || (Settings.TrainMeCharisma & TrainMeNeed[6, 0] < Settings.maxTrainMeCharisma & Me.Wallet.Money >= TrainMeNeed[6, 1])             //Харизма
                            || (TrainMeNeed[0, 0] + TrainMeNeed[1, 0] + TrainMeNeed[2, 0] + TrainMeNeed[3, 0] + TrainMeNeed[4, 0] + TrainMeNeed[5, 0] + TrainMeNeed[6, 0] == 0) //Инициализация
                           ) { Fitness(); }
                    }
                    #endregion
                    #region Taxi
                    if (Settings.UseCar)
                    {                        
                        if (Me.Automobile.LastDT.Date != ServerDT.Date) Me.Automobile.Stop = false;
                        if (Me.Automobile.LastDT <= ServerDT & !Me.Automobile.Stop) Automobile(AutomobileAction.Taxi);
                    }
                    #endregion
                    #region Pyramid
                    if (Settings.GoPyramid)
                    {
                        if (Me.Pyramid.RestartDT <= ServerDT) Pyramid(PyramidAction.Check);
                        if (!Me.Pyramid.Done)
                        {
                            if (Me.Pyramid.Price >= Settings.maxPyramidSell && ServerDT.TimeOfDay > new TimeSpan(0, 6, 0)) Pyramid(PyramidAction.Sell);
                            UpdateMyInfo(MainWB);
                            if (Me.Pyramid.Price >= 100 && Me.Pyramid.Price <= Settings.maxPyramidPrice && Me.Wallet.Money >= Me.Pyramid.Price * Settings.minPyramidAmount) Pyramid(PyramidAction.Buy);
                        }                        
                    }
                    #endregion
                    #region Bank
                    if (Me.Player.Level >= 5) //Банк доступен только с 5ого уровня!
                    {
                        UpdateMyInfo(MainWB);
                        if (((Settings.UseBankDeposit && Me.Wallet.Money + Me.BankDeposit.MyMoney >= Settings.DepositMoney) || (!Settings.UseBankDeposit && Me.BankDeposit.MyMoney != 0)) && Me.BankDeposit.StartDT < ServerDT) Bank(BankAction.Deposit);
                        if ((Settings.ThimblesImmunity || Settings.HCRevenge) && Me.MC.Stop) CheckImmun(ImmunAction.Mona); //Проверяем иммунитет от нападений только тут дабы во время МС всегда ходил к моне/банк.
                        if (Settings.UseBank && Me.Wallet.Money >= Settings.ExchangeBankMoney + Settings.minThimblesMoney
                            && (!Settings.GoPyramid || !Me.Pyramid.BlockMonya || !Settings.BlockThimbles) && (Me.BankDeposit.StartDT >= ServerDT.AddHours(2) || !Settings.UseBankDeposit)
                            && ServerDT > Me.Thimbles.BankStartDT && ServerDT > Me.Thimbles.StartDT) Bank(BankAction.Exchange); //Пора менять бабки в банке?
                    }                                                
                    #endregion
                    #region Casino
                    Random WaitS = new Random();
                    #region Fishki
                    if (Me.Fishki.LastDT.Date != ServerDT.Date) Me.Fishki.Stop = false;
                    if (Settings.BuyFishki && Settings.BuyFishkiAllways && !Me.Fishki.Stop && Me.Fishki.LastDT <= ServerDT) Casino(CasinoAction.BuyFishki);
                    #endregion
                    #region Kubovich
                    if (Me.Kubovich.LastDT.Date != ServerDT.Date) Me.Kubovich.Stop = false;
                    if (Settings.PlayKubovich && !Me.Kubovich.Stop && Me.Kubovich.LastDT <= ServerDT) Casino(CasinoAction.Kubovich);
                    #endregion
                    #region Loto
                    if (Me.Loto.LastDT.Date != ServerDT.Date)
                    {
                        Me.Loto.Stop = false;
                        Me.Loto.LastDT = ServerDT.Hour > 22 ? ServerDT : ServerDT.AddMinutes(WaitS.Next(10, 75));
                    }
                    if (Settings.PlayLoto && !Me.Loto.Stop && Me.Loto.LastDT <= ServerDT) Casino(CasinoAction.Loto);
                    #endregion
                    #endregion
                    #region OilTower
                    if (Settings.GoOil && Settings.GetOil && Me.OilTowerDT < DateTime.Now) Oil(OilAction.OilTower);
                    #endregion
                    #region Pet
                    UpdateMyInfo(MainWB);
                    if (Settings.TrainWarPet && ServerDT >= Me.WarPet.TrainTimeOutDT //Пора тренировать Пэта?
                        && ((Settings.TrainPetFocus && Me.WarPet.Focus < Settings.maxTrainPetFocus && (TrainWarPetNeed[0, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[0, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[0, 1] == 0 ||Me.Wallet.Ore - TrainWarPetNeed[0, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[0, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[0, 2] >= Settings.minTrainPetOil))
                        || (Settings.TrainPetLoyality && Me.WarPet.Loyality < Settings.maxTrainPetLoyality && (TrainWarPetNeed[1, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[1, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[1, 1] == 0 || Me.Wallet.Ore - TrainWarPetNeed[1, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[1, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[1, 2] >= Settings.minTrainPetOil))
                        || (Settings.TrainPetMass && Me.WarPet.Mass < Settings.maxTrainPetMass && (TrainWarPetNeed[2, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[2, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[2, 1] == 0 || Me.Wallet.Ore - TrainWarPetNeed[2, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[2, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[2, 2] >= Settings.minTrainPetOil)))
                        ) { Petarena(PetAction.TrainWarPet); }

                    if (Settings.TrainRunPet && ServerDT >= Me.RunPet.TrainTimeOutDT //Пора тренировать Пэта?
                        && ((Settings.TrainPetAcceleration && Me.RunPet.Acceleration < Settings.maxTrainPetAcceleration && (TrainRunPetNeed[0, 0] == 0 || Me.Wallet.Money - TrainRunPetNeed[0, 0] >= Settings.minTrainPetMoney) && (TrainRunPetNeed[0, 1] == 0 || Me.Wallet.Ore - TrainRunPetNeed[0, 1] >= Settings.minTrainPetOre) && (TrainRunPetNeed[0, 2] == 0 || Me.Wallet.Oil - TrainRunPetNeed[0, 2] >= Settings.minTrainPetOil))
                        || (Settings.TrainPetSpeed && Me.RunPet.Speed < Settings.maxTrainPetSpeed && (TrainRunPetNeed[1, 0] == 0 || Me.Wallet.Money - TrainRunPetNeed[1, 0] >= Settings.minTrainPetMoney) && (TrainRunPetNeed[1, 1] == 0 || Me.Wallet.Ore - TrainRunPetNeed[1, 1] >= Settings.minTrainPetOre) && (TrainRunPetNeed[1, 2] == 0 || Me.Wallet.Oil - TrainRunPetNeed[1, 2] >= Settings.minTrainPetOil))
                        || (Settings.TrainPetEndurance && Me.RunPet.Endurance < Settings.maxTrainPetEndurance && (TrainRunPetNeed[2, 0] == 0 || Me.Wallet.Money - TrainRunPetNeed[2, 0] >= Settings.minTrainPetMoney) && (TrainRunPetNeed[2, 1] == 0 || Me.Wallet.Ore - TrainRunPetNeed[2, 1] >= Settings.minTrainPetOre) && (TrainRunPetNeed[2, 2] == 0 || Me.Wallet.Oil - TrainRunPetNeed[2, 2] >= Settings.minTrainPetOil))
                        || (Settings.TrainPetDexterity && Me.RunPet.Dexterity < Settings.maxTrainPetDexterity && (TrainRunPetNeed[3, 0] == 0 || Me.Wallet.Money - TrainRunPetNeed[3, 0] >= Settings.minTrainPetMoney) && (TrainRunPetNeed[3, 1] == 0 || Me.Wallet.Ore - TrainRunPetNeed[3, 1] >= Settings.minTrainPetOre) && (TrainRunPetNeed[3, 2] == 0 || Me.Wallet.Oil - TrainRunPetNeed[3, 2] >= Settings.minTrainPetOil)))
                       ) { Petarena(PetAction.TrainRunPet); }

                    if (Settings.UseRunPet & ServerDT >= Me.RunPet.RunTimeOutDT) Petarena(PetAction.Run); //Пора бегать пэтом?
                    #endregion
                    #region Sovet
                    /*
                    UpdateMyInfo(MainWB);
                    if (Me.SovetInfo.LastVoting.Date != ServerDT.Date)
                    {
                        Me.SovetInfo.Stop = false;
                        Me.SovetInfo.LastVoting = ServerDT.AddMinutes(ServerDT.Hour < 2 ? WaitS.Next(120, 180) : 0);
                    }
                    if (Me.SovetInfo.Patriot <= ServerDT & Settings.SovetVote) Sovet(SovetAction.Patriot);
                    if (Me.SovetInfo.LastVoting <= ServerDT & !Me.SovetInfo.Stop & Me.Resource.Money >= 100 & Settings.SovetVote) Sovet(SovetAction.Vote);
                    */
                    if (Settings.SovetBuyAgitator && Me.SovetAgitator.LastDT < ServerDT && Me.SovetAgitator.PriceTugriki <= Me.Wallet.Money && Me.SovetAgitator.PriceOre <= Me.Wallet.Ore && Me.SovetAgitator.PriceOil <= Me.Wallet.Oil) SovetBuyAgitator(SovetAgitatorAction.Check);
                    #endregion
                    #region Petriki + Factory
                    UpdateMyInfo(MainWB);
                    if (Settings.MakePetriki && (Me.Wallet.Money - Me.Petriki.Money) >= Settings.minPetrikiMoney && (Me.Wallet.Ore - Me.Petriki.Ore) >= Settings.minPetrikiOre && Me.Petriki.RestartDT <= DateTime.Now && Me.Player.Level >= 5) Factory(FactoryAction.Petriki); //Пора варить петрики?
                    #region Разрешение модернизировать цепочки
                    HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("timeout");                                                                      //Не модить во время караванов!
                    ChainUpgrade.Release = (Settings.GoFactory && !TimeToStopAtack(NextTimeout.Chain, StopTimeoutType.GrpFight) && ((HtmlEl.GetAttribute("href") != null && Regex.IsMatch(HtmlEl.GetAttribute("href"), "/metro/$|/alley/$") && Me.Patrol.Val == 0)  
                        || ((!Settings.GoPatrol || Me.Patrol.Stop) && (!Settings.GoMetro || Me.Rat.Stop)))
                        && HtmlEl.InnerText != null && TimeSpan.Parse(HtmlEl.InnerText) >= new TimeSpan(0, 0, Settings.FactoryChainCount > 8 ? 480 : (int)Settings.FactoryChainCount * 45));
                    #endregion
                    if (ChainUpgrade.Release && !ChainUpgrade.Stop && Me.Wallet.Money >= Settings.FactoryChainCount * 437 + Settings.minFactoryMoney && Me.Wallet.Ore >= Settings.FactoryChainCount * 12 + Settings.minFactoryOre && Me.Player.Level >= 7) Factory(FactoryAction.UpdateChain); //Mодернизировать цепочки?
                    #endregion
                    #region Quest
                    if (Me.QuestFillTonus.LastDT.Date != ServerDT.Date) Me.QuestFillTonus.Stop = false;
                    if (Settings.Quest && !Me.Events.StopQuest && Convert.ToInt32(Me.Player.Energy[0]) >= ((Settings.QuestFillTonusPlus && !Me.QuestFillTonus.Stop) || Settings.QuestFillTonusBottle ? Me.QuestFillTonus.Val : Convert.ToInt32(Me.Player.Energy[1]) / 2)) CheckQuest(); //При не мении 50% энергии делать Квэсты
                    #endregion
                    #region CookCoctail
                    if (Settings.UseCookCoctail && Me.CocktailRecipe.LastCook.AddHours(3) < ServerDT) CookCoctail(CoctailAction.CheckMissing);
                    #endregion
                    #region RepairMobilePhone
                    if (Settings.RepairMobile && RepairMobile.NextDT < DateTime.Now) MobilePhone(MobilePhoneAction.Repair);
                    #endregion
                    #region FeedTaborPet
                    if (Settings.FeedTaborPet && Me.Player.Level >= 9 && Me.TaborPet.LastDT < ServerDT && Me.TaborPet.PriceTugriki <= Me.Wallet.Money && Me.TaborPet.PriceOre <= Me.Wallet.Ore && Me.TaborPet.PriceOil <= Me.Wallet.Oil) FeedTaborPet(TaborPetAction.Check);
                    #endregion
                    #region Azazella
                    if ((Settings.PlayAzazella25 || Settings.PlayAzazella75) && Me.Azazella.NextDT < DateTime.Now && (Settings.AzazellaTreasure ? true : new TimeSpan(0, 1, 0) < DateTime.Now - Me.Azazella.PlayTillDT)) PlayAzazella();
                    #endregion
                    #region Safe
                    if (Settings.BuySafe && Me.Wallet.Ore >= 24 && Me.Safe.LastDT < ServerDT) Safe(SafeAction.Check);
                    #endregion
                    #region PigProtection
                    if (Settings.PigProtection && Me.PigProtection.LastDT < ServerDT && Me.Wallet.Honey >= 10) PigProtection(PigProtectionAction.Check);                    
                    #endregion
                    #region FightBear
                    if (!Me.Bear.Stop || DateTime.Now > Me.Bear.LastDT) RechargeFightBear(); //Заряжаем и проверяем наличие медведя.
                    #endregion
                    #region Turret
                    UpdateMyInfo(MainWB);
                    if (Settings.BuildTurel && Me.Turret.LastDT < ServerDT && Me.Turret.PriceMed == 0 && Me.Turret.PriceTugriki < Me.Wallet.Money && Me.Turret.PriceOre < Me.Wallet.Ore && Me.Turret.PriceOil < Me.Wallet.Oil) BuildTurret();
                    #endregion
                    #region ReadLogs
                    if (Settings.ReadLogs && ReadLogsDT < DateTime.Now) ReadLogsDT = DateTime.Now.Add(MobilePhone(MobilePhoneAction.ReadLogs)); 
                    #endregion
                    #region AFK
                    if (Settings.UseAFK && Me.Events.NextAFK < DateTime.Now)
                    {
                        if (Settings.AFKCahnce >= new Random().Next(0, 100)) Wait(new TimeSpan(0, new Random().Next(0, (Int32)Settings.AFCTime), new Random().Next(0, 60)), "~ Делаю вид, типа ты ушёл за чайком, а я без тебя играть боюсь! Жду до: ", TimeOutAction.Blocked); 
                        Me.Events.NextAFK = DateTime.Now.AddMinutes(10);
                    } 
                    #endregion
                    break;
                case TimeOutAction.Blocked:
                    BugReport("UseTimeOut.Blocked");
                    break;
            }
        }
        public string ExpandCollapsedNumber(string str)
        {
            double factor = str.Contains("k") ? 1000 : str.Contains("M") ? 1000000 : 1;
            double num = Convert.ToDouble(str.Replace("k", "").Replace("M", ""));
            return Convert.ToString(Convert.ToInt32(num * factor));
        }
        public void UpdateMyInfo(WebBrowser WB) //OK
        {
            BugReport("UpdateMyInfo");

            int UpdateTries = 0;

        ReTry:
            #region Уже более 3х раз возникли проблемы? обновляем страничку!
            if (UpdateTries >= 3) { frmMain.RefreshURL(MainWB, Settings.ServerURL); UpdateTries = 0; } //
            #endregion
            IsWBComplete(WB); //Проверка на клик-клик и прочее.

            try
            {
                //Извлекаем мои жизни.
                Me.Player.LifePkt = new string[2]; //Инициализация
                Me.Player.LifePkt[0] = frmMain.GetDocument(WB).GetElementById("currenthp").InnerText;
                Me.Player.LifePkt[1] = frmMain.GetDocument(WB).GetElementById("maxhp").InnerText;

                //Извекаем тонус
                Me.Player.Energy = new string[2]; //Инициализация
                Me.Player.Energy[0] = frmMain.GetDocument(WB).GetElementById("currenttonus").InnerText;
                Me.Player.Energy[1] = frmMain.GetDocument(WB).GetElementById("maxenergy").InnerText;

                if (Me.Player.LifePkt[0] == null || Me.Player.LifePkt[1] == null || Me.Player.Energy[0] == null || Me.Player.Energy[1] == null)
                {
                    Wait(50, 1000, "! Я стал бессмертен?!? - Не верю, отставлю ка я бутылку до: ");
                    AnalysePlace(WB);
                    goto ReTry;
                }

                //Извлекаем тугрики, руду, нефть
                object Info;
                #region Money
                //Деньги считываем, именно таким образом, ибо тут видно полное количество а не округлённое буквой "к"
                Info = frmMain.GetJavaVar(WB, "$(\"#personal .tugriki-block\").attr(\"title\")");
                Me.Wallet.Money = (Info == DBNull.Value || Info == null) ? 0 : Convert.ToInt32(((string)Info).Replace("Монет: ", ""));
                #endregion
                #region Ore
                //Руда считываем, именно таким образом, ибо тут видно полное количество а не округлённое буквой "к"
                Info = (string)frmMain.GetJavaVar(WB, "$(\"#personal .ruda-block\").attr(\"title\")");
                Me.Wallet.Ore = (Info == DBNull.Value || Info == null) ? 0 : Convert.ToInt32(((string)Info).Replace("Руды: ", ""));
                #endregion
                #region Oil
                //Нефть считываем, именно таким образом, ибо тут видно полное количество а не округлённое буквой "к"
                Info = (string)frmMain.GetJavaVar(WB, "$(\"#personal .neft-block\").attr(\"title\")");
                Me.Wallet.Oil = (Info == DBNull.Value || Info == null) ? 0 : Convert.ToInt32(((string)Info).Replace("Нефти: ", ""));
                #endregion
                #region Honey
                //Мед считываем, именно таким образом, ибо тут видно полное количество а не округлённое буквой "к"
                Info = (string)frmMain.GetJavaVar(WB, "$(\"#personal .med-block\").attr(\"title\")");
                Me.Wallet.Honey = (Info == DBNull.Value || Info == null) ? 0 : Convert.ToInt32(((string)Info).Replace("Меда: ", ""));
                #endregion
                #region Розыск в милиции
                Info = (string)frmMain.GetJavaVar(WB, "$(\"#personal .wanted .percent\").attr(\"style\")");
                Me.Police.Val = Convert.ToInt32(Regex.Match((string)Info, "([0-9])+").Value) / 10 - 5;
                #endregion
                return;
            }
            catch
            {
                UpdateTries++;
                Wait(500, 1000, "! Меня словно на куски порвало, склеиваюсь до: ");
            }
            AnalysePlace(WB);
            goto ReTry;
        }
        public bool AnalyseFight(WebBrowser WB, int M = 0, int R = 0, int N = 0)
        {
            BugReport("AnalyseFight");
            
            Match match;
            stcPlayerInfo PI = new stcPlayerInfo();
            bool bRet;
            int iM = 0, iR = 0, iN = 0, iL = 0, iB = 0, iT = 0;
            int TryNr = 0;

        ReTry:
            try 
            {
                IsWBComplete(WB, (Int32)Expert.AnalyseFightMin, (Int32)Expert.AnalyseFightMax); //На всякий случай, вдруг всё сломалось.
                string URL = frmMain.GetDocumentURL(WB);
                #region Что то не так?
                switch (frmMain.GetDocument(WB).GetElementById("content").GetAttribute("classname"))
                {
                    case "fight":
                        if (frmMain.GetDocument(WB).GetElementById("content").InnerText == "Это было давно\r\nи не правда") return true; //Проблемы на серваке, когда неправильно генерирует ссылки на проведённые поединки!
                        break;
                    case "fight-group":
                        UpdateStatus("@ " + DateTime.Now + " Ого, да вас тут целая бригада, подходи по одному!");
                        if (GroupFight(GroupFightAction.Fight))
                        {
                            if (M + R + N > 0) UpdateStatus("$ " + DateTime.Now + " Непогано я тут кулаками помахал, с полу подобрал: ", M, R, N);
                            return true;
                        }
                        else return false;
                    default:
                        BugReport("", true, "AnalyseFight", WB);
                        UpdateStatus("! " + DateTime.Now + " Чёэто?!? Я уже и кулаки размял, а жертва то куда пропала?");
                        return true;
                }
                #endregion

                match = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#content .fighter1 .user\").text()"), "(?<Name>([^[])+)[[](?<Lvl>([0-9])+)[]]");
                Me.Player.Name = match.Groups["Name"].Value;
                Me.Player.Level = Convert.ToInt32(match.Groups["Lvl"].Value);

                match = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#content .fighter2 .user\").html()"), "(?<Fraction>arrived|resident|npc).*(?<URL>/player/(?<ID>([0-9])+)/)");
                if (match.Success)
                {
                    PI.Fraction = match.Groups["Fraction"].Value;
                    PI.URL = Settings.ServerURL + match.Groups["URL"].Value;
                    PI.Id = match.Groups["ID"].Value;
                }
                match = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#content .fighter2 .user\").text()"), "(?<Name>([^[])+)[[](?<Lvl>([0-9])+)[]]");
                PI.Name = match.Groups["Name"].Value;
                PI.Level = match.Groups["Lvl"].Value;                

                //HtmlEl = frmMain.GetDocument(WB).GetElementById("fight-log"); //25.11.2o11 Исчезло...
                match = Regex.Match((string)frmMain.GetJavaVar(WB, "$(\"#content .result\").text()"), "(?<=Победитель:([\\s])+)([^[\\t])+(?= [[])");

                //MessageBox.Show(Me.Player.Name + "[" + Me.Player.Level + "]\r\n" + PI.Name + "[" + PI.Level + "]\r\n" + Me.Player.Name.Length + "\r\n" + match.Value.Length + "Succsess: " + match.Success + "\r\nMatch: " + match.Value + "Win: " + (match.Value == Me.Player.Name));

                if (match.Value == Me.Player.Name) //Я победил
                {
                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .tugriki\");", "innerText"))
                    {
                        iM += Convert.ToInt32(sWin.Replace(",", ""));
                    }

                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .ruda\");", "innerText"))
                    {
                        iR += Convert.ToInt32(sWin.Replace(",", ""));
                    }

                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .neft\");", "innerText"))
                    {
                        iN += Convert.ToInt32(sWin.Replace(",", ""));
                    }

                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .expa\");", "innerText"))
                    {
                        iL += Convert.ToInt32(sWin);
                    }

                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .badge\");", "innerText"))
                    {
                        iB += Convert.ToInt32(sWin);
                    }

                    iT = GetArrClassCount(WB, "$(\"#content .result .mobila\");");

                    UpdateStatus("$ " + DateTime.Now + " В битве '" + Me.Player.Name + "[" + Me.Player.Level + "] vs " + PI.Name + "[" + PI.Level + "]', честно конфисковал: " + ((iM + M + iR + R + iN + N + iB + iT) == 0 ? "\"Куй Моржовый\", пойду наверное повешусь!" : ""), iM + M, iR + R, iN + N, iL, iB, iT);
                    #region Считывание жизней пэта после боя
                    object obj;
                    #region Ожидание окончания боя
                    Wait(1000, 1500);
                    frmMain.InvokeScript(WB, "fightForward"); //Симулирую нажатие кнопки конец боя
                    Wait(1000, 1500);
                    #endregion
                    obj = frmMain.GetJavaVar(WB, "lifes['2']"); //Жизни моего пэта
                    Me.WarPet.LifePkt = ((string)obj == "0/" || obj == null) ? null : obj.ToString().Split('/'); // "0/" - Питомца, нет

                    obj = frmMain.GetJavaVar(WB, "lifes['3']"); //Жизни вражеского пэта
                    string[] vsPetLife = ((string)obj == "0/" || obj == null) ? null : obj.ToString().Split('/'); // "0/" - Питомца, нет
                    if (vsPetLife == null ? false : vsPetLife[0] == "0") UpdateStatus("! " + DateTime.Now + " Братиш ..., также нельзя, ты снова сожрал чужого питомца!");
                    WB.Tag = "Ready"; //Вызванный скрипт не обновляет страницы, помогаем.
                    #endregion

                    if (PI.URL != null) //URL равен null только у НПЦ!
                    {
                        #region Выбил зуб?
                        string Info = (string)frmMain.GetJavaVar(WB, "$(\"#content .result\").html()");
                        if (Regex.IsMatch(Info, "выбивает зуб"))
                        {
                            if (Settings.RemoveEnemy) Contact(WB, ContactAction.DeletePlayer, ContactType.Enemy, null, new stcPlayerInfo[] { PI });
                            #region Поедание сникерса
                            if (Settings.UseSnikersEnemy && IsTimeout(WB, false, false))
                            {
                                frmMain.NavigateURL(WB, Settings.ServerURL + "/player");
                                IsWBComplete(WB);
                                EatDrog(WB, ShopItems.Snikers);
                            }
                            #endregion
                        }
                        #endregion
                        #region Используется список жертв?
                        else
                        {
                            if (Settings.UseVictims && (!Settings.UseOnlyHomelessVictims || PI.Fraction == "npc")) //Список жертв вносить всех или только бомжей?
                            {
                                if (iM >= Convert.ToInt32(Settings.AddVictim)) Contact(WB, ContactAction.AddPlayer, ContactType.Victim, null, new stcPlayerInfo[] { PI });
                                if (iM <= Convert.ToInt32(Settings.DeleteVictim)) Contact(WB, ContactAction.DeletePlayer, ContactType.Victim, null, new stcPlayerInfo[] { PI });
                            }
                        }
                        #endregion
                    }
                    bRet = true;
                }
                else //Проиграл / Ничья
                {
                    foreach (string sWin in GetArrClassHtml(WB, "$(\"#content .result .tugriki\");", "innerText")) //Проиграл тугриков!
                    {
                        iM -= Convert.ToInt32(sWin.Replace(",", ""));
                    }

                    UpdateStatus("! " + DateTime.Now + " В битве '" + Me.Player.Name + "[" + Me.Player.Level + "] vs " + PI.Name + "[" + PI.Level + "]', " + (match.Success ? "тупо проебал: " : "глупейшая ничья!"), iM);
                    if (Settings.UseVictims && PI.URL != null) Contact(WB, ContactAction.DeletePlayer, ContactType.Victim, null, new stcPlayerInfo[] { PI }); //URL равен null только у НПЦ!
                    bRet = false;
                }
                #region Лечение пэта после драки
                if (Me.WarPet.LifePkt != null)
                {
                    double dLifePrc = Convert.ToDouble(Me.WarPet.LifePkt[0]) / Convert.ToDouble(Me.WarPet.LifePkt[1]) * 100;
                    if (dLifePrc < Convert.ToDouble(Settings.HealPet100))
                    {
                        UpdateStatus("! " + DateTime.Now + " Иб@ть, братишка не умирай! Уже лечу к ветеринару!");
                        frmMain.NavigateURL(WB, Settings.ServerURL + "/player");
                        IsWBComplete(WB);
                        EatDrog(WB, ShopItems.Pet100);
                    }
                    else if (dLifePrc < Convert.ToDouble(Settings.HealPet50))
                    {
                        UpdateStatus("@ " + DateTime.Now + " Ооо братиш, смотрю пора к ветеринару!");
                        frmMain.NavigateURL(WB, Settings.ServerURL + "/player");
                        IsWBComplete(WB);
                        EatDrog(WB, ShopItems.Pet50);
                    }
                }
                #endregion
                #region Ведение контроля счётчика драк
                if (Me.ArrDuelsDT == null) Me.ArrDuelsDT = new DateTime[] { GetServerTime(WB) };
                else 
                {
                    DateTime[] ArrDT = new DateTime[Me.ArrDuelsDT.Count<DateTime>() + 1]; //Создаём массив на 1 элемент больше прежнего, чтоб вставить новое время
                    ArrDT[0] = GetServerTime(WB); //Заносим новое время последней драки.
                    Array.ConstrainedCopy(Me.ArrDuelsDT, 0, ArrDT, 1, Me.ArrDuelsDT.Count<DateTime>()); //Переносим массив времён за позицию новой драки в новом массиве
                    Me.ArrDuelsDT = ArrDT; //Пересохраняем
                }                
                #endregion
                return bRet;
            }
            catch 
            {
                if (TryNr < 3) 
                {
                    TryNr++;
                    goto ReTry;
                }
                else 
                {
                    UpdateStatus("! " + DateTime.Now + " Я уже и очки накинул, один чёрт не вижу кто кого тут покусал!");
                    return true; //Не получаеться проанализировать драку, исходим из того, что всё пучком!
                }
            }            
        }
        public bool AnalysePlace(WebBrowser WB)
        {
            BugReport("AnalysePlace");

            IsWBComplete(WB); //На всякий пожарный, когда человек мешает боту например.
            string URL = frmMain.GetDocumentURL(WB);

            Regex regex = new Regex("^res:/|" + Settings.ServerURL + "/$|#logout|/player/|/quest/|/alley/|/metro/|/thimble/|/shaurburgers/|/police/|/fight/|/phone/|/petrun/race/|/huntclub/wanted/|/bunker/|/closed.html|tell-my-ip.com");
            switch (regex.Match(URL).Value)
            {
                case "tell-my-ip.com": return GoToPlace(WB, Place.Player);
                case "#logout":
                case "moswar.mail.ru":
                case "www.moswar.eu/":
                case "www.moswar.net/":
                case "www.moswar.ru/": return WebLogin(WB); //Автологин, если выбросило
                case "/phone/":                
                case "/petrun/race/":
                case "/huntclub/wanted/":
                case "/player/": return true;
                case "/quest/": return LevelUP(WB);
                case "/alley/": return !IsTimeout(WB, true); //Патруль, нельзя ничего делать пока время тикает
                case "/thimble/":
                case "/metro/": return Metro(MetroAction.Check);
                case "/shaurburgers/": return !IsTimeout(WB, true);
                case "/police/": return Police(PoliceAction.Check);
                case "/fight/": GroupFight(GroupFightAction.Fight); return true; //Груповой или хаоточеский бой
                case "/bunker/": Wait(60000, 180000, " Ого, начальник как глубоко же ты закопался, я тебя снаружи подожду до: "); return GoToPlace(WB, Place.Player); //Мы в бункере
                case "/closed.html": UpdateStatus("! " + DateTime.Now + " Идёт обновление... небоись Начальство, мимо меня не проскочит!"); do { Wait(60000, 120000); IsWBComplete(WB); } while (frmMain.GetDocumentURL(WB).Contains("/closed.html")); return true; //Сервер закрыт, для обновления или тех. работы.
                case "res:/": Wait(60000, 180000, "! Проблемы интернет соединения, жду до: "); frmMain.NavigateURL(WB, Regex.Match(URL, "(?<=#).*").Value); return true; //Пробуем открыть страничку, что не открылась!
                default: UpdateStatus("? " + DateTime.Now + " Нихрена не пойму, где же я нахожусь? URL-> " + URL); Wait(60000, 180000, " Незнаю что делать, отсиживаюсь до: "); return GoToPlace(WB, Place.Player);
            }
        }
        public void GetMyStats(WebBrowser WB) //OK
        {
            BugReport("GetMyStats");

            GoToPlace(WB, Place.Player);           
            
            Me.Clan.Name = (string)frmMain.GetJavaVar(WB, "$(\"#content .clan-icon\").attr(\"title\")");
            object Info = frmMain.GetJavaVar(WB, "$(\"#content .user\").html()"); //<i title="Понаехавший" class="arrived"></i><a href="/clan/3074/"><img title="7 элемент" class="clan-icon" src="/@images/clan/clan_3074_ico.png"></a><a href="/player/354768/">Козыръ</a><span class="level">[19]</span>
            Me.Clan.URL = Me.Clan.Name != null ? "http://" + Settings.ServerURL + Regex.Match((string)Info, "/clan/([0-9])+/").Value : null;
            Me.Player.URL = "http://" + Settings.ServerURL + Regex.Match((string)Info, "/player/([0-9])+/").Value;

            frmMain.NavigateURL(WB, Me.Player.URL);
            GetPStats(WB, ref Me.Player);
        }
        public void GetPStats(WebBrowser WB, ref stcPlayer P) //Extract stats Naked (Player Window)
        {
            BugReport("GetPStats");

            Regex regex;
            Match match;
            object Info;

            IsWBComplete(WB); //Обязательно иначе бывают казусы!

            Info = frmMain.GetJavaVar(WB, "$(\"#pers-player-info .user\").html()");
            
            regex = new Regex("/player/([0-9])+/"); //URL
            P.URL = Settings.ServerURL + regex.Match((string)Info);

            regex = new Regex("(?<=class=\"?)(arrived|resident)"); //Фракция
            match = regex.Match((string)Info);
            P.Fraction = match.Value;

            Info = frmMain.GetJavaVar(WB, "$(\"#pers-player-info .user\").text()");

            regex = new Regex("(?<Name>([^[])+)[[](?<Lvl>([0-9])+)[]]"); //Name + Level
            match = regex.Match((string)Info);
            P.Name = match.Groups["Name"].Value;
            P.Level = Convert.ToInt32(match.Groups["Lvl"].Value);

            

            HtmlElement HtmlEl = frmMain.GetDocument(WB).GetElementById("stats-accordion");
            P.Health[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Здоровье)([0-9])+").Value);
            P.Strength[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Сила)([0-9])+").Value);
            P.Dexterity[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Ловкость)([0-9])+").Value);
            P.Endurance[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Выносливость)([0-9])+").Value);
            P.Cunning[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Хитрость)([0-9])+").Value);
            P.Attentiveness[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Внимательность)([0-9])+").Value);
            P.Charisma[0] = Convert.ToInt32(Regex.Match(HtmlEl.InnerText, "(?<=Харизма)([0-9])+").Value);

            CheckCollections(WB, ref P);
            
            Info = frmMain.GetJavaVar(WB, "$(\"#pers-player-info .life\").text()");
            regex = new Regex("(?<=Жизни:)([0-9 /\\s])+");
            match = regex.Match((string)Info);
            P.LifePkt = match.Value.Split('/');

            P.Steroids = Convert.ToInt32(P.LifePkt[1]) > (P.Health[0] * 10 + P.Endurance[0] * 4) * 3.5; //Одетым, жизней в 1.5 раза более чем раздетым?
        }
        public bool IsPlayerWeak(ref stcPlayer vsP, int minDiff, bool CompareNackedStats)
        {
            BugReport("IsPlayerWeak");

            int i = CompareNackedStats ? 0 : 1;
            int[] StatSum = new int[2] // StatSum[0] -> Сумма моих статов, StatSum[1] -> Сумма статов врага
            {
                Me.Player.Health[i] + Me.Player.Strength[i] + Me.Player.Dexterity[i] + Me.Player.Endurance[i] + Me.Player.Cunning[i] + Me.Player.Attentiveness[i],
                vsP.Health[i] + vsP.Strength[i] + vsP.Dexterity[i] + vsP.Endurance[i] + vsP.Cunning[i] + vsP.Attentiveness[i]
            };

            return (Convert.ToInt32(Me.Player.LifePkt[0]) >= Convert.ToInt32(vsP.LifePkt[0])) && (Convert.ToInt32(Me.Player.LifePkt[1]) * 1.1 >= Convert.ToInt32(vsP.LifePkt[1])) && (vsP.Steroids ? (StatSum[0] * 0.85 - StatSum[1]) : (StatSum[0] - StatSum[1] - minDiff)) >= 0;
        }        
        public bool GoToPlace(WebBrowser WB, Place P, string SubPlace = "", bool chkURL = true) //OK
        {
            BugReport("GoToPlace");

            string Place2Go = "";

            Regex regex = new Regex(Settings.ServerURL + "/$|/(desert|trainer|police|shop|shaurburgers|metro|factory|petrun|petarena|sovet|gorbushka|neftlenin|neft|huntclub|nightclub|casino|berezka|pyramid|turret|bank|automobile/car|automobile/ride|call)/");
            Match match = regex.Match(frmMain.GetDocumentURL(WB));
            if (match.Value == Settings.ServerURL + "/") WebLogin(WB); //Автологин, если выбросило
            switch (P)
            {
                case Place.Phone: Place2Go = "phone" + (Settings.DoNotReadPrivateMessages ? "/logs/" : ""); break;
                case Place.Settings: Place2Go = "settings"; break;
                case Place.Player: Place2Go = "player"; break;
                case Place.Clan: Place2Go = "clan/profile"; break;
                case Place.Alley: Place2Go = "alley"; break;
                case Place.Square: Place2Go = "square"; break;
                case Place.Stash: Place2Go = "stash"; break;
                case Place.Home: Place2Go = "home"; break;
                case Place.Arbat: Place2Go = "arbat"; break;
                case Place.Tverskaya: Place2Go = "tverskaya"; break;
                case Place.Camp: Place2Go = "camp"; break;
                case Place.Bear: Place2Go = "home/bear"; break;
                case Place.Mobile: Place2Go = "phone/call"; if (match.Value != "/call/") GoToPlace(WB, Place.Phone); break;
                case Place.Desert: Place2Go = "desert"; if (match.Value != "/desert/") GoToPlace(WB, Place.Alley); break;
                case Place.Trainer: Place2Go = "trainer"; if (match.Value != "/trainer/") GoToPlace(WB, Place.Player); break;
                case Place.Police: Place2Go = "police"; if (match.Value != "/police/") GoToPlace(WB, Place.Square); break;
                case Place.Shop: Place2Go = "shop"; if (match.Value != "/shop/") GoToPlace(WB, Place.Square); break;
                case Place.Shaurburgers: Place2Go = "shaurburgers"; if (match.Value != "/shaurburgers/") GoToPlace(WB, Place.Square); break;
                case Place.Metro: Place2Go = "metro"; if (match.Value != "/metro/") GoToPlace(WB, Place.Square); break;
                case Place.Factory: Place2Go = "factory"; if (match.Value != "/factory/") GoToPlace(WB, Place.Square); break;
                case Place.Nightclub: Place2Go = "nightclub"; if (match.Value != "/nightclub/") GoToPlace(WB, Place.Square); break;
                case Place.Petrun: Place2Go = "petrun"; if (match.Value != "/petrun/") GoToPlace(WB, Place.Tverskaya); break;
                case Place.Petarena: Place2Go = "petarena"; if (match.Value != "/petarena/") GoToPlace(WB, Place.Petrun); break;
                case Place.Sovet: Place2Go = "sovet"; if (match.Value != "/sovet/") GoToPlace(WB, Place.Tverskaya); break;
                case Place.Gorbushka: Place2Go = "tverskaya/gorbushka" + SubPlace; SubPlace = ""; if (match.Value != "/gorbushka/") GoToPlace(WB, Place.Tverskaya); break;
                case Place.Oil: Place2Go = "neft"; if (match.Value != "/neft/" && match.Value != "/neftlenin/") GoToPlace(WB, Place.Tverskaya); break;
                case Place.Huntclub: Place2Go = "huntclub"; if (match.Value != "/huntclub/") GoToPlace(WB, Place.Arbat); break;
                case Place.Casino: Place2Go = "casino"; if (match.Value != "/casino/") GoToPlace(WB, Place.Arbat); break;
                case Place.Berezka: Place2Go = "berezka"; if (match.Value != "/berezka/") GoToPlace(WB, Place.Arbat); break;
                case Place.Pyramid: Place2Go = "pyramid"; if (match.Value != "/pyramid/") GoToPlace(WB, Place.Arbat); break;
                case Place.Bank: Place2Go = "bank"; if (match.Value != "/bank/") GoToPlace(WB, Place.Arbat); break;
                case Place.Automobile: Place2Go = "automobile" + SubPlace; SubPlace = ""; if (match.Value != "/automobile/car/" || match.Value != "/automobile/ride/") GoToPlace(WB, Place.Home); break;
                case Place.Metrowar: Place2Go = "metrowar"; if (match.Value != "/metrowar/") GoToPlace(WB, Place.Metro); break;
                case Place.Turret: if (Me.Clan.URL == null) return false; Place2Go = "turret" + Regex.Match(Me.Clan.URL, "/([0-9])+"); if (match.Value != "/turret/") GoToPlace(WB, Place.Clan); break;
                case Place.URL: Place2Go = SubPlace; SubPlace = ""; break;
            }
        ReTry:
            if (DebugMode) BugReport("~ GoTo-" + Place2Go);            
            string URL = Settings.ServerURL + (Place2Go.StartsWith("/") ? "" : "/") + Place2Go;

            frmMain.NavigateURL(WB, URL + (URL.EndsWith("/") ? "" : "/")); //Распознавание в методе навигации

            IsWBComplete(WB, (Int32)Expert.GoToMin, (Int32)Expert.GoToMax);
            
            if (!frmMain.GetDocumentURL(WB).Contains(URL) && chkURL) //Неполучилось перейти на желаемую страничку
            {                
                if (AnalysePlace(WB)) goto ReTry; //Выполняем условия зависания, дабы продвинутся дальше
                else return false;
            }

            #region Суслик
            if (Place2Go == "alley" && frmMain.GetDocument(WB).GetElementById("event_suslik") != null)
            {
                BugReport("", true, "Suslik", WB);
                UpdateStatus("$ " + DateTime.Now + "Опа, Суслег, живой! А ну дафай сюда компенсацию!");
                frmMain.InvokeScript(WB, "eval", new object[] { "Alley.Suslik.getReward()" });
                IsWBComplete(WB);
                BugReport("", true, "Suslik2", WB);
            }
            #endregion

            if (Place2Go == "phone/logs/") Place2Go = "phone"; //Проверка не отсюда ли Settings.DoNotReadPrivateMessages, галочка может быть как раз отключена посему проверяем сам переход!
            if (SubPlace != "") { Place2Go += SubPlace; SubPlace = ""; goto ReTry; }
            return true;
        }
        public bool Attack(Opponent O, decimal minLvl, decimal maxLvl)
        {
            BugReport("Atack-O");

            Regex regex;
            string OppType = "";
            DateTime DT;
            DateTime ServerDT;
            int AttackRetries = 0;

            #region Check Bonus
            CheckForDayPrize();
            #endregion

            ServerDT = GetServerTime(MainWB);

            #region Я стал мажером?
            if (Regex.IsMatch(frmMain.GetDocument(MainWB).GetElementById("searchLevelForm").InnerText, "Искать противника") && ServerDT > Me.Major.LastDT) { Me.Major.Stop = false; Me.Patrol.Stop = false; } //Внести данные о мажоре, после аллеи и перепроверить патруль.
            #endregion
            #region Фарм/выбивание зубов + Агресивный мод во время кланвойны?
            if (Me.ClanWarInfo.Now & Settings.AddClan & (Settings.FarmClan || (Me.ClanWarInfo.WarStep == 1 ? !CheckImmun(ImmunAction.Tooth) : false)) && (Settings.UseAgent ? Me.AgentHunting.Stop : true)) O = Opponent.Enemy; //Агенты важнее зубов
            #region Агресивный мод выбивания зубов!
            if (O == Opponent.Enemy & Me.ClanWarInfo.WarStep == 1 & !CheckImmun(ImmunAction.Tooth) & Settings.Berserker) O = Opponent.EnemyEx;
            #endregion
            #endregion
            #region Атаковать Агентов? + Блокировка нападений на Агентов в дни с NPC
            if (frmMain.GetDocument(MainWB).GetElementById("searchNpcForm") != null) { Me.AgentHunting.LastDT = ServerDT; Me.AgentHunting.Stop = true; } //Блокировка нападений на агентов в дни с NPC
            if (Settings.UseAgent && !Me.AgentHunting.Stop && !Ignore.PVPAttack) 
            {
                if (ServerDT <= Me.AgentHunting.StartDT || Agent(AgentAction.Check)) //Продление лицензии охоты на агентов
                {
                    maxLvl = Settings.maxAgentLvl; minLvl = Settings.minAgentLvl; O = Settings.AgentOpponent; //Настройки нападения на Агентов
                } 
                if (Settings.AgentOpponent == Opponent.Strong && Me.AgentHunting.Val >= 2) { maxLvl = Me.Player.Level; minLvl = maxLvl; O = Opponent.Equal; } //Меняем условия нападения, когда больше не можем бить сильных агентов. 
            }
            //При стоппере: Менять условия нападения больше не нужно, ибо стандартно передаются аллейные
            #endregion
            #region Атаковать NPC во время противостояния?
            if (Settings.AttackNPC && !Me.NPCHunting.Stop && O != Opponent.EnemyEx && (Settings.Lampofob ? Me.PerkAntiLamp.On : true) && frmMain.GetDocument(MainWB).GetElementById("searchNpcForm") != null) { O = Opponent.NPC; minLvl = Me.Player.Level; maxLvl = Me.Player.Level; }
            if (frmMain.GetDocument(MainWB).GetElementById("searchNpcForm") == null) { Me.NPCHunting.Stop = true; Me.NPCHunting.LastDT = ServerDT; } //Сегодня нет боев с NPC, можно бегать в ОК
            #endregion
            #region Агент + Мажер блокировка ночных битв.
            if (Ignore.PVPAttack && O != Opponent.NPC) return false;
            #endregion
            #region Атаковать Оборонтем? + Продление/Определение Оборотня
            if ((Settings.UseWerewolf && Me.WerewolfHunting.StartDT <= ServerDT.AddMinutes(Settings.WerewolfPrice == 0 ? 0 : 20))   //Покупка или заблаговременное  оборотня при покупке не за погоны!
                || (frmMain.GetDocument(MainWB).GetElementById("alley-search-werewolf") != null && Me.WerewolfHunting.Stop)         //Я вдруг стал оборотнем?
               ) Werewolf(WerewolfAction.Check);
            if (Settings.UseWerewolf && !Me.WerewolfHunting.Stop) //Я могу нападать оборотнем 
            {
                Me.Player.Level = Convert.ToInt32((string)frmMain.GetJavaVar(MainWB, "player['level']")); //Считываем мой настоящий уровень, ибо я сейчас всё ещё могу быть оборотнем!
                minLvl = Me.Player.Level - Settings.WerewolfLevel + Settings.minWerewolfLvl;
                maxLvl = Me.Player.Level - Settings.WerewolfLevel + Settings.maxWerewolfLvl;
                O = Settings.WerewolfOpponent;
                Torture(true); //Включаем болгарки!
            }
            //При стоппере: Менять условия нападения больше не нужно, ибо стандартно передаются аллейные
            #endregion
            #region Время мониторинга Жертв (Средства пытки, включаются до Атаки)
            if (O == Opponent.Victim) DT = DateTime.Now.AddMinutes(2);
            #endregion
            #region Время мониторинга Берсеркера или поиска Агентов (Часть 1)
            DT = (O == Opponent.EnemyEx || (Settings.UseAgent & !Me.AgentHunting.Stop)) ? DateTime.Now.AddMinutes(10) : DateTime.Now; //При включенном берсеркере до 10 минут подряд пытаться найти соперника, затем бить слабых.
            #endregion
        ReTry:
            if (frmMain.GetDocument(MainWB).GetElementById("searchForm") == null) GoToPlace(MainWB, Place.Alley);
            if (IsHPLow(MainWB, 99, false))
            {
                if (CheckHealthEx(99, Settings.HealMe100, Settings.HealPet50, Settings.HealPet100)) goto ReTry;
                else { UseTimeOut(TimeOutAction.All); return false; }
            }
            switch (O)
            {
                case Opponent.Equal: OppType = "equal"; break;
                case Opponent.Strong: OppType = "strong"; break;
                case Opponent.Major: //На всякий случай, если выбран мажор, а мажорства больШе нет!
                case Opponent.Weak: OppType = "weak"; break;
                case Opponent.EnemyEx:
                case Opponent.Enemy: OppType = "enemy"; break;
                case Opponent.Victim: OppType = "victim"; break;
            }
            #region Атаковать Агентов? (Часть 2)
            if (Settings.UseAgent && !Me.AgentHunting.Stop && DT > DateTime.Now && O != Opponent.EnemyEx) O = Opponent.Agent; //Поиск агентов производится, по уже заданным ранее настройкам (по первому поиску, при поиске в стиле Opponent.EnemyEx, скорее всего идёт речь о произведённом переключении, не стоит возвращать агентов!).
            #endregion
            #region Нападения собой или Оборотнем?
            HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alley-search-werewolf");
            if (Settings.UseWerewolf && !Me.WerewolfHunting.Stop && HtmlEl != null) O = Opponent.Werewolf; //Я всё ещё оборотень!
            else HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alley-search-myself"); //Собой 
            #endregion
            #region Поиск соперника
            if (O == Opponent.NPC)
            {
                frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("searchNpcForm"), "submit");
            }
            else
            {
                regex = new Regex("Искать противника"); //Проверяем настройки, а не O дабы обеспечить поиск мажёром при поиске агентов
                if ((O == Opponent.Major || (O == Opponent.Agent && Settings.AgentOpponent == Opponent.Major) || (O == Opponent.Werewolf && Settings.WerewolfOpponent == Opponent.Major)) && regex.IsMatch(frmMain.GetDocument(MainWB).GetElementById("searchLevelForm").InnerText)) //Мажор и кнопка на месте?
                {
                    HtmlEl.All["minlevel"].SetAttribute("value", minLvl.ToString());
                    HtmlEl.All["maxlevel"].SetAttribute("value", maxLvl.ToString());
                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("searchLevelForm" + (O == Opponent.Werewolf ? "Werewolf" : "")), "submit");
                }
                else
                {
                    HtmlEl.All["type"].SetAttribute("value", OppType);
                    frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("searchForm" + (O == Opponent.Werewolf ? "Werewolf" : "")), "submit");
                }
            }
            #endregion
            UpdateMyInfo(MainWB); //Выполняет роль: IsWBComplete + считывание текущих средств.
            #region Остался на алее?
            if (frmMain.GetDocument(MainWB).GetElementById("searchForm") != null) //Если во время моего нападения напали на меня, то слишком мало жизней чтоб дратся ... посему снова бросает на алею!
            {
                if (O == Opponent.Victim && DT <= DateTime.Now) { Torture(false); O = Opponent.Weak; } //Выключаем использование средств пытки, переходим на слабых и продолжаем
                if (O == Opponent.Enemy || (O == Opponent.EnemyEx & DT <= DateTime.Now)) O = Settings.AlleyOpponent.Equals(Opponent.Major) ? Opponent.Major : Opponent.Weak; //Пробовал напасть из списка, но увы никого не нашло!
                if ((O == Opponent.Agent && GetServerTime(MainWB) >= Me.AgentHunting.StartDT && !Agent(AgentAction.Check)) || //Попытка продлить нападения на агентов!
                    (O == Opponent.Werewolf && GetServerTime(MainWB).AddMinutes(Settings.WerewolfPrice == 0 ? 1 : 5) >= Me.WerewolfHunting.StartDT && !Werewolf(WerewolfAction.Check))) //Попытка продлить нападения на оборотня!
                {
                    #region Меняем условия нападения, когда больше не можем бить агентов или оборотнем.
                    if (O == Opponent.Werewolf && Settings.AlleyOpponent != Opponent.Victim) Torture(false); //Выключаем болгарки и прочие, если атаковал оборотнем!
                    maxLvl = Settings.maxAlleyLvl;
                    minLvl = Settings.minAlleyLvl;
                    O = Settings.AlleyOpponent;
                    if (Me.ClanWarInfo.Now && Settings.AddClan && (Settings.FarmClan || (Me.ClanWarInfo.WarStep == 1 ? !CheckImmun(ImmunAction.Tooth) : false)))
                    {
                        #region Переключаемся на врагов!
                        O = Opponent.Enemy;
                        if (Me.ClanWarInfo.WarStep == 1 && !CheckImmun(ImmunAction.Tooth) && Settings.Berserker)
                        {
                            O = Opponent.EnemyEx;
                            DT = DateTime.Now.AddMinutes(10); //При включенном берсеркере до 10 минут подряд пытаться найти соперника, затем бить слабых.
                        }
                        #endregion
                    }
                    #endregion  
                }                
                if (Me.Wallet.Money == 0 || IsTimeout(MainWB, true, false)) return false; //Закончились деньги, таймаут? ... невозможно напасть...
                if (TimeToStopAtack(NextTimeout.Atack)) { UpdateStatus("@ " + DateTime.Now + " Драка отменяется иначе на встречу опоздаю, а у меня приказ свыше!"); return false; } //Необходимо остановить нападение!
                goto ReTry;
            }
            #endregion
            do
            {
                SetAjaxTrap(MainWB, true);
                GetPVPStats(MainWB);
                GetMyLife(MainWB);
                #region Ссылка с кнопки "Атаки"
                regex = new Regex("(?<PVP>alleyAttack[(](?<Id>([0-9])+), (?<Force>[0-9]), (?<Werewolf>[0-9])[)])|(?<NPC>/alley/attack([^\"])+)");  //www.moswar.ru/alley/attack-npc2/NPC_UNCLESAM/
                Match match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("content").InnerHtml);
                string AttackURL = match.Groups["PVP"].Success ? "alleyAttack" : Settings.ServerURL + match.Value; //При PVP сохраняем название функции, которой в последствии будут переданы аргументы.
                #endregion               
                #region Нападение или продолжение поиска!
                switch (O)
                {                    
                    case Opponent.Agent:
                    case Opponent.NPC:
                        if (O == Opponent.Agent && DT <= DateTime.Now)
                        {
                            //Точно такойже код в если не удалось продлить Агентов или Оборотня чуть выше, через GoTo не получается
                            #region Меняем условия нападения, когда больше не можем бить агентов.
                            maxLvl = Settings.maxAlleyLvl;
                            minLvl = Settings.minAlleyLvl;
                            O = Settings.AlleyOpponent;
                            if (Me.ClanWarInfo.Now && Settings.AddClan && (Settings.FarmClan || (Me.ClanWarInfo.WarStep == 1 ? !CheckImmun(ImmunAction.Tooth) : false)))
                            {
                                #region Переключаемся на врагов!
                                O = Opponent.Enemy;
                                if (Me.ClanWarInfo.WarStep == 1 && !CheckImmun(ImmunAction.Tooth) && Settings.Berserker)
                                {
                                    O = Opponent.EnemyEx;
                                    DT = DateTime.Now.AddMinutes(10); //При включенном берсеркере до 10 минут подряд пытаться найти соперника, затем бить слабых.
                                }
                                #endregion
                            }
                            #endregion
                            goto ReTry;
                        }
                        //NPC: При 0 поражений -> Взяточники, 1 -> Рейдеры, 2-> Риэлторы
                        if ((vsPlayer[0].Level < minLvl || vsPlayer[0].Level > maxLvl || vsPlayer[0].URL != null) || (O == Opponent.NPC && !vsPlayer[0].Name.Contains(Me.NPCHunting.Val == 0 ? "Взяточник" : Me.NPCHunting.Val == 1 ? "Рейдер" : "Риэлтор")))
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/search/again/"); //Устраиваем перебор, это либо не агент, либо слишком сильный!
                        else
                        {
                            if (match.Groups["PVP"].Success)
                            {
                                frmMain.InvokeScript(MainWB, AttackURL, new object[3] { match.Groups["Id"].Value, match.Groups["Force"].Value, match.Groups["Werewolf"].Value }); //этого тут в принципе быть не может, но оставил мало ли вдруг что-то подредактируют.
                                #region Обработка Ajax
                                IsWBComplete(MainWB);
                                MainWB.Tag = "Ajax";
                                #endregion
                            } 
                            else frmMain.NavigateURL(MainWB, AttackURL);
                        }
                        break;
                    case Opponent.Werewolf:
                        if (vsPlayer[0].Level < minLvl || vsPlayer[0].Level > maxLvl || !IsPlayerWeak(ref vsPlayer[0], Me.Player.Level * 5, false)) //Устраиваем перебор, может следуюший слабее
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/search/again/");                        
                        else
                        {
                            if (match.Groups["PVP"].Success)
                            {
                                frmMain.InvokeScript(MainWB, AttackURL, new object[3] { match.Groups["Id"].Value, match.Groups["Force"].Value, match.Groups["Werewolf"].Value });
                                #region Обработка Ajax
                                IsWBComplete(MainWB);
                                MainWB.Tag = "Ajax";
                                #endregion
                            }                                
                            else frmMain.NavigateURL(MainWB, AttackURL);
                        }
                        break;
                    case Opponent.Enemy:
                    case Opponent.Victim:
                    case Opponent.EnemyEx:
                        //Проверка слабее ли игрок?
                        if (!IsPlayerWeak(ref vsPlayer[0], Me.Player.Level * 5, false)) //Устраиваем перебор, может следуюший слабее
                        {
                            if (AttackRetries >= 20) 
                            {
                                if (O == Opponent.Victim) Torture(false); //Выключаем паяльники прочие, если атаковал жертв!
                                O = Settings.AlleyOpponent.Equals(Opponent.Major) ? Opponent.Major : Opponent.Weak;
                                goto ReTry;
                            }
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/search/again/");
                            AttackRetries++;
                        }
                        else
                        {
                            if (match.Groups["PVP"].Success) 
                            {
                                frmMain.InvokeScript(MainWB, AttackURL, new object[3] { match.Groups["Id"].Value, match.Groups["Force"].Value, match.Groups["Werewolf"].Value });
                                #region Обработка Ajax
                                IsWBComplete(MainWB);
                                MainWB.Tag = "Ajax";
                                #endregion
                            }
                            else frmMain.NavigateURL(MainWB, AttackURL);
                        }
                        break;
                    case Opponent.Major:
                    case Opponent.Equal:
                    case Opponent.Strong:
                    case Opponent.Weak:
                        if (vsPlayer[0].Level < minLvl || vsPlayer[0].Level > maxLvl || (Settings.UseHomeless && vsPlayer[0].Fraction != "npc") || (O == Opponent.Major && !IsPlayerWeak(ref vsPlayer[0], Me.Player.Level * 5, false)))
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/search/again/"); //Проверка слабее ли игрок? (Только для мажоров)
                        else 
                        {
                            if (match.Groups["PVP"].Success)
                            {
                                frmMain.InvokeScript(MainWB, AttackURL, new object[3] { match.Groups["Id"].Value, match.Groups["Force"].Value, match.Groups["Werewolf"].Value });
                                #region Обработка Ajax
                                IsWBComplete(MainWB);
                                MainWB.Tag = "Ajax";
                                #endregion
                            } 
                            else frmMain.NavigateURL(MainWB, AttackURL);
                        }
                        break;
                }                 
                #endregion                
                IsWBComplete(MainWB);               
                #region Что-то пошло не так?
                if (frmMain.GetDocument(MainWB).GetElementById("searchForm") != null //Лаг на серваке, снова закинуло на алею!
                    || frmMain.GetDocument(MainWB).GetElementById("content").GetAttribute("classname") == "pers enemy" //Если попалась ссылка с игроком, значит на него напали до меня и сервак снова выделывается!
                    ) goto ReTry;
                string URL = frmMain.GetDocumentURL(MainWB);
                match = Regex.Match(URL, "/((?<OK>alley/(search|fight))|(?<LevelUP>quest))/");
                if (match.Groups["LevelUP"].Success) { LevelUP(MainWB); return true; } //Похоже получил уровень!
                if (!match.Groups["OK"].Success) { AnalysePlace(MainWB); return false; } //Я гдето застрял?                 
                #endregion
                if (TimeToStopAtack(NextTimeout.Atack)) { UpdateStatus("@ " + DateTime.Now + " Драка отменяется иначе на встречу опоздаю, а у меня приказ свыше!"); return false; } //Необходимо остановить нападение!
            } while (frmMain.GetDocument(MainWB).GetElementById("timer-block") == null);
            #region Анализ боя
            switch (O)
            {
                case Opponent.Agent:
                    Me.AgentHunting.Val += AnalyseFight(MainWB) ? 0 : 1;
                    Me.AgentHunting.LastDT = GetServerTime(MainWB);                    
                    #region Пора включать стоппер?
                    if (Settings.AgentOpponent == Opponent.Strong)
                    {
                        Me.AgentHunting.Stop = Me.AgentHunting.Val >= 4; //Инициализация, если есть еше агент допинг, перепишем стоппер. (2x от сильных + 2x равных)
                        if (Me.ArrUsualDoping != null)
                        {
                            foreach (clsDoping.stcDopingEx Doping in Me.ArrUsualDoping)
                            {
                                if (Doping.Event == clsDoping.DopingEvent.Agent && !Doping.Done) //Только при отсутствии или уже сьеденном Агент стопере спускаемся на равных.
                                {
                                    Me.AgentHunting.Stop = Me.AgentHunting.Val >= 2; //Симулируем стопер, до того как скатимся на равных дабы дать шанс ещё побить сильных.
                                    break;
                                }
                            }
                        }                        
                    }
                    else Me.AgentHunting.Stop = Me.AgentHunting.Val >= 2; //При поиске отличном от сильных, допускается лишь 2 поражения.
                    #endregion                    
                    break;
                case Opponent.NPC:
                    Me.NPCHunting.Val += AnalyseFight(MainWB) ? 0 : 1;
                    Me.NPCHunting.LastDT = GetServerTime(MainWB);
                    Me.NPCHunting.Stop = Me.NPCHunting.Val > 2; //При 0 поражений -> Взяточники, 1 -> Рейдеры, 2-> Риэлторы 3-> Exit.
                    break;
                default:
                    AnalyseFight(MainWB);
                    break;
            }
            #endregion
            return true;
        }
        public bool Attack(WebBrowser WB, string sNick) //Poisk Protivnika po niku NOK dobavit' proverku poluchilos' li?
        {
            BugReport("Atack-N");

            int AttackRetrys = 0;
            Regex regex;

        ReTry:
            if (frmMain.GetDocument(WB).GetElementById("searchForm") == null) GoToPlace(WB, Place.Alley);
            if (AttackRetrys < 3 & Me.HCHunting.Search) //
            {
                frmMain.GetDocument(WB).GetElementById("nick").SetAttribute("value", sNick);
                frmMain.InvokeMember(WB, frmMain.GetDocument(WB).GetElementById("searchNameForm"), "submit");
                IsWBComplete(WB);
                if (frmMain.GetDocument(WB).GetElementById("searchForm") != null) //Если во время моего нападения напали на меня, то слишком мало жизней чтоб дратся ... посему снова бросает на алею!
                {
                    AttackRetrys++;
                    goto ReTry;
                }
                GetPVPStats(WB);
                //Проверка слабее ли игрок?
                if (IsPlayerWeak(ref vsPlayer[0], Convert.ToInt32(Settings.minHCStatDiff), false))
                {
                    frmMain.NavigateURL(WB, vsPlayer[0].URL.Replace("player", "alley/attack"));
                    #region Удалось напасть?
                    IsWBComplete(WB);
                    regex = new Regex("(/fight/)");
                    if (regex.IsMatch(frmMain.GetDocumentURL(WB))) //Попался!
                    {
                        Me.HCHunting.Search = false;
                        AnalyseFight(WB);
                    }
                    #endregion
                }
            }
            WB.Tag = "Ready"; //Не удалось напасть, бросаем ищем иного!
            return Me.HCHunting.Search == true;
        }
        public bool GroupFight(GroupFightAction GFA, GroupFightType GFT = GroupFightType.Clan)
        {
            DateTime ServerDT = GetServerTime(MainWB);
            DateTime NextFight = new DateTime();
            DateTime MonitorDT;
            HtmlElement HtmlEl;
            Match match;
            object Info;

            switch (GFA)
            {
                case GroupFightAction.Check:
                    switch (GFT)
                    {
                        case GroupFightType.Chaos:
                            BugReport("Chaos Fight");
                            #region Chaos
                            GoToPlace(MainWB, Place.Alley);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("chaoticfight-form");
                            if (HtmlEl == null ? false : HtmlEl.GetElementsByTagName("button").Count >= 1) //Уже есть кнопка для входа? 
                            {
                                #region Вычисление цены и времени старта следующей стенки!
                                match = Regex.Match(HtmlEl.GetElementsByTagName("button")[0].InnerHtml, "class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)");
                                ServerDT = GetServerTime(MainWB);
                                NextFight = Convert.ToDateTime(ServerDT.ToString("dd.MM.yyyy HH:00:00"), CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes((ServerDT.Minute / 15 + 1) * 15); //Высчитываю начало следующего хаота
                                GrpFight.ChaosStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не рефрешил аллею заходя в хаос, снова и снова!
                                #endregion

                                if (IsGrpFightTimePermission(GroupFightType.Chaos) && TimeToGoGrpFight(GroupFightType.Chaos, NextFight, match.Groups["Unit"].Value + ":" + match.Groups["Cost"].Value))
                                {
                                    if (CheckHealthEx(Settings.minClanMeFightHp - 1, Settings.minClanMeFightHp - 51, Settings.minClanPetFightHp - 1, Settings.minClanPetFightHp - 51))
                                    {
                                        if (Settings.UseAutoFightBagSlots) CheckBagFightItems(GroupFightType.Chaos);
                                        GoToPlace(MainWB, Place.Alley);
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("chaoticfight-form");
                                        if (HtmlEl.GetElementsByTagName("button").Count >= 1)
                                        {
                                            HtmlEl.GetElementsByTagName("button")[0].InvokeMember("click"); //Записаться в бой!
                                            UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул в хаот-стенку*");
                                            IsWBComplete(MainWB, 2000, 5000);
                                            return GroupFight(GroupFightAction.Check, GroupFightType.All);
                                        }
                                        else UpdateStatus("@ " + DateTime.Now + " Ээээх, опоздал я в хаос-стенку...");
                                    }
                                    else UpdateStatus("@ " + DateTime.Now + " Поход в хаос-стенку проигнорирован, из-за слабости в уровне живота.");
                                }
                            }
                            else //Похоже Хаосы на сергодня закончились?
                            {
                                match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("chaoticfight").NextSibling.InnerText, "(?<=Приходите после )([0-9. :])+(?=[.])");
                                if (match.Success) GrpFight.ChaosStartDT = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(1); //Сохраняем, с какого момента можно снова бегать в хаосы
                            }
                            #endregion
                            break;
                        case GroupFightType.Ore:
                            BugReport("Ore");
                            #region Ore
                            GoToPlace(MainWB, Place.Alley);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("levelfight-form");
                            if (HtmlEl == null ? false : HtmlEl.GetElementsByTagName("button").Count == 1) //Уже есть кнопка для входа?
                            {
                                #region Вычисление цены и времени старта следующей стенки!
                                match = Regex.Match(HtmlEl.GetElementsByTagName("button")[0].InnerHtml, "class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)");
                                ServerDT = GetServerTime(MainWB);
                                NextFight = Convert.ToDateTime(ServerDT.ToString("dd.MM.yyyy HH:00:00"), CultureInfo.CreateSpecificCulture("ru-RU")).AddHours(1); //Высчитываю начало следующей рудной
                                GrpFight.OreStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не рефрешил аллею заходя в рудную, снова и снова!
                                #endregion

                                if (IsGrpFightTimePermission(GroupFightType.Ore) && TimeToGoGrpFight(GroupFightType.Ore, NextFight, match.Groups["Unit"].Value + ":" + match.Groups["Cost"].Value))
                                {
                                    if (CheckHealthEx(Settings.minClanMeFightHp - 1, Settings.minClanMeFightHp - 51, Settings.minClanPetFightHp - 1, Settings.minClanPetFightHp - 51))
                                    {
                                        if (Settings.UseAutoFightBagSlots) CheckBagFightItems(GroupFightType.Ore);
                                        GoToPlace(MainWB, Place.Alley);
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("levelfight-form").GetElementsByTagName("button")[0];
                                        HtmlEl.InvokeMember("click"); //Записаться в бой!
                                        UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул в рудную-стенку*");
                                        IsWBComplete(MainWB, 2000, 5000);
                                        return GroupFight(GroupFightAction.Check, GroupFightType.All);
                                    }
                                    else UpdateStatus("@ " + DateTime.Now + " Поход в рудную-стенку проигнорирован, из-за слабости в уровне живота.");
                                }
                            }
                            else GrpFight.OreStartDT = Convert.ToDateTime(GetServerTime(MainWB).ToString("dd.MM.yyyy HH:00:00"), CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(46); //Высчитываю начало следующей рудной         
                            #endregion
                            break;
                        case GroupFightType.Clan:
                            BugReport("Clan Fight");
                            #region Clan
                            GoToPlace(MainWB, Place.Alley);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("flag-form");
                            if (HtmlEl == null ? false : HtmlEl.GetElementsByTagName("button").Count == 1) //Уже есть кнопка для нападения? 
                            {
                                #region Вычисление времени старта следующей стенки!
                                NextFight = Me.ClanWarInfo.NextDT.AddMinutes(15); //Начало следующей клан стенки
                                GrpFight.ClanStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не бегал кругами в клан и алею, снова и снова!
                                #endregion

                                if (IsGrpFightTimePermission(GroupFightType.Clan) && TimeToGoGrpFight(GroupFightType.Clan, NextFight))
                                {
                                    if (Settings.UseAutoFightBagSlots) CheckBagFightItems(GroupFightType.Clan);
                                    #region Ожидание последней минуты?
                                    if (Settings.ClanLastMin)
                                    {
                                        UpdateStatus("@ " + DateTime.Now + " Ожидание последней минуты, перед клан-стенкой.");
                                        while (GetServerTime(MainWB).Minute < 59)
                                        {
                                            Thread.Sleep(1000);
                                        }
                                    }
                                    #endregion
                                    if (CheckHealthEx(Settings.minClanMeFightHp - 1, Settings.minClanMeFightHp - 51, Settings.minClanPetFightHp - 1, Settings.minClanPetFightHp - 51))
                                    {
                                        GoToPlace(MainWB, Place.Alley);
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("flag-form").GetElementsByTagName("button")[0];
                                        HtmlEl.InvokeMember("click"); //Записаться в бой!
                                        UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул в клан-стенку*");
                                        IsWBComplete(MainWB, 2000, 5000);
                                        return GroupFight(GroupFightAction.Check, GroupFightType.All);
                                    }
                                    else UpdateStatus("@ " + DateTime.Now + " Поход в клан-стенку проигнорирован, из-за слабости в уровне живота.");
                                }
                            }
                            #endregion
                            break;
                        case GroupFightType.PVP:
                            BugReport("PVP Fight");
                            #region PVP
                            ServerDT = GetServerTime(MainWB);                           
                            if (Enumerable.Range(1, 5).Contains((int)ServerDT.AddMinutes(3).DayOfWeek))
                            {
                                GoToPlace(MainWB, Place.Sovet, "/map");
                                foreach (string HTML in GetArrClassHtml(MainWB, "$(\"#content .areas .progress .button\")", "outerHTML"))
                                {
                                    match = Regex.Match(HTML, "SovetPage.actionGetWeeklyReward[(]([0-9])+[)];");
                                    if (match.Success)
                                    {
                                        UpdateStatus("@ " + DateTime.Now + " Бродил я тут по району ... когда смотрю мой чемоданчик!");
                                        frmMain.GetJavaVar(MainWB, match.Value);
                                        IsWBComplete(MainWB);
                                    }
                                }
                                Info = frmMain.GetJavaVar(MainWB, "$(\"#content .council-citymap-place .description4\").text()");
                                match = Regex.Match((string)Info, "(?<Now>([0-9])+) из (?<Max>([0-9])+)");
                                if (match.Success && Convert.ToInt32(match.Groups["Now"].Value) < Convert.ToInt32(match.Groups["Max"].Value))
                                {
                                    Info = frmMain.GetJavaVar(MainWB, "$(\"#content .council-citymap-info-wrap .button\").attr(\"class\")");
                                    NextFight = (string)Info == "button" && !Settings.GoPVPInstantly ? ServerDT.AddMinutes(1) : Convert.ToDateTime(ServerDT.ToString("dd.MM.yyyy HH:00:00"), CultureInfo.CreateSpecificCulture("ru-RU")).AddHours(1); //Высчитываю начало следующей PVP
                                    GrpFight.PVPStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не рефрешил аллею заходя в рудную, снова и снова!

                                    if (IsGrpFightTimePermission(GroupFightType.PVP) && TimeToGoGrpFight(GroupFightType.PVP, NextFight, "ruda:10"))
                                    {                                        
                                        //string AreaId = Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .areas\").html()"), "class=\"?icon region(?<ID>([0-9])+)([\\s\\S])+class=\"?time").Groups["ID"].Value;

                                        if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 5);                                        
                                        if (Settings.UseAutoFightBagSlots) CheckBagFightItems(GroupFightType.PVP);
                                        Dopings(ref Me.ArrUsualDoping, DopingAction.Check);

                                        Wait(NextFight.AddSeconds((double)Settings.GoPVPInstantlyOffset - 30) - GetServerTime(MainWB), "@ Отдыхаю перед стенкой противостояния до: ");
                                        if (CheckHealthEx(Settings.minClanMeFightHp - 1, Settings.minClanMeFightHp - 51, Settings.minClanPetFightHp - 1, Settings.minClanPetFightHp - 51))
                                        {
                                            #region Не успеваю к указанной секунде?
                                            if (Settings.GoPVPInstantly && NextFight.AddSeconds((double)Settings.GoPVPInstantlyOffset) < GetServerTime(MainWB))
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Прости Начальство, видимо часы подвели, не успеваю я на эту PVP стенку!"); //
                                                Convert.ToDateTime(ServerDT.ToString("dd.MM.yyyy HH:00:00"), CultureInfo.CreateSpecificCulture("ru-RU")).AddHours(1);
                                                return false;
                                            }
                                            #endregion

                                            Wait(NextFight.AddSeconds((double)Settings.GoPVPInstantlyOffset - (double)(Expert.GoToMax / 1000) - 3) - GetServerTime(MainWB), "@ Готовлюсь до: "); //-3s Сдвиг для уточнения!
                                            
                                            UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул в PVP-стенку*");
                                            MonitorDT = DateTime.Now.AddSeconds((int)Settings.GagIE);
                                            do
                                            {
                                                frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/");
                                                IsWBComplete(MainWB);
                                                Info = frmMain.GetJavaVar(MainWB, "$(\".info .button\").html()");
                                                if (Info != DBNull.Value)
                                                {
                                                    frmMain.GetJavaVar(MainWB, Regex.Match((string)Info, "Alley.joinMetroFight(.*);").Value);
                                                    IsWBComplete(MainWB, 50, 150);
                                                }                                        
                                            }
                                            while (frmMain.GetJavaVar(MainWB, "$(\"#content .fightdesc.fightjoined\").html()") == DBNull.Value && MonitorDT > DateTime.Now);                                
                                                                                        
                                            do
                                            {
                                                GoToPlace(MainWB, Place.Alley); //Драка начнётся через AnalysePlace
                                                Wait(8000, 15000);
                                            }
                                            while (frmMain.GetJavaVar(MainWB, "$(\"#content .fightdesc.fightjoined\").html()") != DBNull.Value);
                                        }
                                        else UpdateStatus("@ " + DateTime.Now + " Поход в PVP-стенку проигнорирован, из-за слабости в уровне живота.");
                                    }
                                    break;
                                }                                
                            }
                            GrpFight.PVPStartDT = ServerDT.Date.Add(new TimeSpan(23, 57, 0)); //Сюда доходит только в субботу воскресенье или если уже провёл свои 30 боёв!
                            #endregion                                                       
                            break;
                        case GroupFightType.Group: //Больше нету!
                            BugReport("Group Fight");
                            #region Group OLD
                            /*
                            GoToPlace(MainWB, Place.Alley);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("groupfight").NextSibling;
                            //Коренные vs NPC
                            match = Regex.Match(HtmlEl.InnerText, "(?<Fraction>(Понаехавшие|Коренные)) vs ((?<PVP>(Понаехавшие|Коренные))|(?<NPC>NPC))");
                            //Сегодня проходят Бои! 
                            if ((GrpFight.Val == 1 & match.Groups["PVP"].Success) || (GrpFight.Val == 2 & match.Groups["NPC"].Success) || (GrpFight.Val == 3 & match.Success)) //GrpFight: 0-> Выкл., 1-> PVP, 2-> NPC, 3-> Все.
                            {
                                #region Сейчас должна драться иная фракция с NPC?
                                if (match.Groups["NPC"].Success && match.Groups["Fraction"].Value == (Me.Player.Fraction == "arrived" ? "Коренные" : "Понаехавшие")) 
                                {
                                    GrpFight.GroupStartDT = GetServerTime(MainWB).AddMinutes(50); //У меня есть минимум 50-60 минут запаса!
                                    break;
                                }
                                #endregion

                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("groupfight").Parent.All["levelfight-form"];                                
                                if (HtmlEl == null ? false : HtmlEl.GetElementsByTagName("button").Count >= 1) //Уже есть кнопка для нападения?
                                {
                                    #region Вычисление времени старта следующей стенки!
                                    match = Regex.Match(HtmlEl.GetElementsByTagName("button")[0].InnerText, "([0-9])+[:]([0-9])+"); //Записаться на бой в 10:30
                                    if (match.Success) 
                                    {
                                        NextFight = Convert.ToDateTime(match.Value); //Начало следующей стенки
                                        GrpFight.GroupStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не рефрешил аллею заходя в груповой, снова и снова!
                                    }
                                    else //Скорее всего, это кнопки вмешаться в бой
                                    {
                                        UpdateStatus("! " + DateTime.Now + " Дохтур, посмотри меня: Меня не пускают в бои противостояния!");
                                        GrpFight.GroupStartDT = GetServerTime(MainWB).AddMinutes(5); //Иногда бывает, что сервер не показывает, когда начало след драки
                                        return false;
                                    }
                                    #endregion

                                    if (TimeToGoGrpFight(GroupFightType.Group, NextFight)) 
                                    {
                                        if (CheckHealthEx(Settings.minClanMeFightHp - 1, Settings.minClanMeFightHp - 51, Settings.minClanPetFightHp - 1, Settings.minClanPetFightHp - 51))
                                        {
                                            GoToPlace(MainWB, Place.Alley);
                                            frmMain.GetDocument(MainWB).GetElementById("groupfight").Parent.All["levelfight-form"].GetElementsByTagName("button")[0].InvokeMember("click"); //Записаться в бой!
                                            UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул в NPC/PVP-стенку*");
                                            IsWBComplete(MainWB, 2000, 5000);
                                            return GroupFight(GroupFightAction.Check, GroupFightType.All);                                                                                    
                                        }
                                        else UpdateStatus("@ " + DateTime.Now + " Поход в стенку проигнорирован, из-за слабости в уровне живота.");
                                    }                                    
                                }
                                else //Кнопки ещё нет
                                {
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("groupfight").Parent; //Запись на бой начнется в 10:20
                                    match = Regex.Match(HtmlEl.InnerText == null ? "" : HtmlEl.InnerText, "(?<Join>Вмешаться в бой)|(?<Time>([0-9])+[:]([0-9])+)");
                                    if (match.Groups["Time"].Success)
                                    {
                                        NextFight = Convert.ToDateTime(match.Value).AddMinutes(10); //Начало следующей стенки = начало записи + 10 минут
                                        GrpFight.GroupStartDT = NextFight.AddMinutes(-3); //Для, того чтоб Бот не рефрешил аллею заходя в груповой, снова и снова!
                                        TimeToGoGrpFight(GroupFightType.Group, NextFight); //Резервируем время для драки
                                    }
                                    if (match.Groups["Join"].Success) //Вмешка 
                                    {
                                        GrpFight.GroupStartDT = GetServerTime(MainWB).AddMinutes(5); //Я пока ещё не доделал вмешки, посему просто перепроверить через 5 минут
                                    }
                                    if (!match.Success) GrpFight.GroupStartDT = GetServerTime(MainWB).AddMinutes(5); //Иногда бывает, что сервер не показывает, когда начало след драки                                                                       
                                }
                            }
                            else GrpFight.GroupStartDT = GetServerTime(MainWB).Date.AddDays(GetServerTime(MainWB).TimeOfDay > new TimeSpan(9, 20, 0) ? 1 : 0).AddHours(9).AddMinutes(20); //Пробнуть сегодня/завтра в 9:20 утра.                                                                                            
                            */
                            #endregion
                            break;
                        case GroupFightType.Mafia:
                            BugReport("Group Mafia");
                            #region Mafia
                            GoToPlace(MainWB, Place.Alley);
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("phoneboss").NextSibling;
                            match = Regex.Match(HtmlEl.InnerText, "((?<Time>([0-9:])+)([\\s])+?Записано:|(?<Started>В бою уже участвуют)) (?<Checked>([0-9])+)/20 человек");
                            if (match.Success && (Settings.MafiaUseLicence || !match.Groups["Started"].Success)) //Обнаружен бой, и в нём уже записано более 5 человек!
                            {
                                GrpFight.Mafia.FightFound = true;
                                #region Обнаружен бой, и в нём уже записано более 5 человек?
                                if (Convert.ToInt32(match.Groups["Checked"].Value) >= 5)
                                {
                                    TimeSpan TSTimeout = new TimeSpan();
                                    TimeSpan.TryParse(match.Groups["Time"].Value, out TSTimeout); //Запоминаем сколько ещё продлится ожидание боя

                                    if (IsGrpFightTimePermission(GroupFightType.Mafia) && (!IsGrpFightTimePermission(GroupFightType.Clan) || !TimeToGoGrpFight(GroupFightType.Clan, new DateTime(), GrpFight.Price, TSTimeout.Minutes + 15)))
                                    {
                                        GrpFight.Mafia.NextFightDT = GetServerTime(MainWB).Add(MobilePhone(MobilePhoneAction.CheckBattery)); //Ещё раз перепроверяю заряд батарейки, наличие телефона
                                        #region NEW CODE + BATTERY RECHARGE
                                        if (IsHPLow(MainWB, 100, false) ? (HealMePlus() ? true : CheckHealthEx(99, 49, 0, 0)) : true) //Лечить в любом варианте до 100%, жизни пэта не важны на пахане!
                                        {
                                            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/call/")) GoToPlace(MainWB, Place.Mobile); //Бегал лечиться?
                                            frmMain.GetDocument(MainWB).GetElementById("app-desktop").GetElementsByTagName("img")[0].InvokeMember("click"); //Переходим к списку драк! (Без этого кнопка не срабатывает)
                                            IsWBComplete(MainWB, 2000, 2500);
                                        ReTry:
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("app-messages").GetElementsByTagName("button")[match.Groups["Started"].Success ? 2 : 1]; //Войти лицензией/записаться на бой!
                                            HtmlEl.InvokeMember("click");
                                            IsWBComplete(MainWB, 500, 1000);
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-text");
                                            if (HtmlEl != null && HtmlEl.InnerText.EndsWith("Вы хотите зарядить его с помощью батарей?"))
                                            {
                                                frmMain.InvokeMember(MainWB, HtmlEl.Parent.GetElementsByTagName("button")[0], "click");
                                                IsWBComplete(MainWB);
                                                GrpFight.Mafia.NextFightDT = GetServerTime(MainWB).Add(MobilePhone(MobilePhoneAction.CheckBattery)); //Ещё раз перепроверяю заряд батарейки, наличие телефона
                                                goto ReTry;
                                            }
                                            if (GrpFight.Mafia.NextFightDT < GetServerTime(MainWB))
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Поооосторонись! *рванул на Пахана*");
                                                if (Settings.UseAutoFightBagSlots && TSTimeout > new TimeSpan(0, 1, 0)) CheckBagFightItems(GroupFightType.Mafia);
                                                IsWBComplete(MainWB, 2000, 5000);
                                                return GroupFight(GroupFightAction.Check, GroupFightType.All);
                                            }
                                            else 
                                            {
                                                GrpFight.Mafia.FightFound = false; //Нет заряда/аккумулятора, забыть о драке!
                                                GrpFight.Mafia.LastCheckDT = DateTime.Now.Add(MobilePhone(MobilePhoneAction.CheckBattery));
                                            } 
                                        }
                                        #endregion
                                    }
                                }
                                else GrpFight.Mafia.LastCheckDT = DateTime.Now.AddSeconds(Convert.ToInt32(match.Groups["Checked"].Value) >= 5 ? 10 : 30); //Найден бой, мониторим когда наберётся народ!
                                #endregion
                            }
                            else //Бой пока не найден, заглянуть позже.
                            {
                                GrpFight.Mafia.FightFound = false;
                                #region Бой прозевали, или его просто пока не было!
                                GrpFight.Mafia.NextFightDT = GetServerTime(MainWB).AddMinutes(-1);
                                GrpFight.Mafia.LastCheckDT = DateTime.Now.AddMinutes(6);
                                #endregion
                            }  
                            #endregion
                            break;
                        case GroupFightType.All:
                            BugReport("All Fight");
                            #region All
                            Info = frmMain.GetJavaVar(MainWB, "$(\"#personal .bubble\").html()");
                            if (Info != DBNull.Value && Regex.IsMatch((string)Info, "[>](В Записи|Ожидание боя)[<]")) //DBNull ибо при ненахождении возврашается класс а не просто строка.
                            {
                                IsTimeout(MainWB, true); 
                                return true; //Была драка!
                            }                                         
                            GrpFight.NextCheckDT = DateTime.Now.AddSeconds(30); //Запоминаем время последней глобальной проверки, чтоб не частить сюда.
                            #endregion
                            break;
                    }
                    return false;
                case GroupFightAction.Fight:
                    #region Fight
                    BugReport("Fight");
                    MonitorDT = DateTime.Now.AddMinutes(1); //Первый мониторинг 60, остальные по 40 секунд во время ходов
                    MatchCollection matches;                                   
                    bool ItemUsed;
                    stcFightItem[] FightItems = new stcFightItem[7]; //[0]Лечение, [1]Сыр, [2]Гранаты +, [3]Гранаты %, [4]Каска, [5]Пружина, [6]Щит, [x] -> ItemID
                    string vs;
                    int Turn = 0;
                    string NPC = ""; //Тип NPC, которым сейчас проверяем комбинацию.
                    bool[] SearchOpp = { false, false, false }; //Инициализация нового поиска комбо у пахана
                    string[] Rupor = { null, null, null }; //Инициализация Рупора
                    int CurrOpp = -1; //Индекс комбинацию к которому пытаемся подобрать!
                    double[] Weakness;
                    int FightNr = 0;                    

                    IsWBComplete(MainWB);
                    #region Убеждаюсь в том, что страничка боя сейчас открыта и идёт бой!
                    if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/fight/"))
                    {
                        frmMain.NavigateURL(MainWB, Settings.ServerURL + "/alley/"); //Обновляем страничку!
                        IsWBComplete(MainWB);
                        if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/fight/")) return false;
                    }                    
                    #endregion
                                        
                    match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .group2\").text()"), "Группа (Крысомах|Коммунистов)|Понаехавшие|Коренные|Левые|Правые|Захватчики|Мафия|Охрана банка|Коммунисты");
                    switch (match.Value)
                    {
                        case "Группа Крысомах":  //Стенка с крысомахами!
                            vs = "Крысомахи";
                            #region В крысиных драках вычесляем уровень спуска в метро.
                            matches = Regex.Matches(GetArrClassHtml(MainWB, "$(\"#fightGroupForm .group\");", "innerHTML")[1], "(?<=id=\"?)fighter(?<ID>([0-9])+)-life"); //Колличество противников
                            foreach (Match m in matches)
                            {
                                match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById(m.Value).Parent.Parent.InnerText, "(?<=[[])([0-9])+(?=[]])");
                                if (match.Success ? FightNr < Convert.ToInt32(match.Value) : false) FightNr = Convert.ToInt32(match.Value); //Ищем самую большую крысомаху
                            }
                            switch (FightNr) //По уровню самой большой крысомахи определяем уровень спуска в метро.
                            {
                                default: FightNr = 0; break;
                                case 6: FightNr = 1; break;
                                case 7: FightNr = 2; break;
                                case 10: FightNr = 3; break;
                                case 11: FightNr = 4; break;
                                case 12: FightNr = 5; break;
                                case 15: FightNr = 6; break;
                                case 99: vs = "Королева крысомах"; break;
                            }
                            #endregion
                        break;
                        case "Коренные":
                        case "Понаехавшие":
                            vs = Enumerable.Range(1,5).Contains((int)ServerDT.DayOfWeek) ? "PVP" : "Руда";                            
                            break;
                        case "Левые":
                        case "Правые":
                            vs = "Хаос";                            
                            break;
                        case "Захватчики":
                            vs = "NPC";                            
                            break;
                        case "Мафия":
                            vs = "Мафия";                            
                            break;
                        case "Охрана банка":
                            vs = "Охрана банка";
                            break;
                        case "Коммунисты":
                            vs = "Ленин";
                            break;
                        case "Группа Коммунистов":
                            vs = "Ленинопровод";
                            #region В ленинопроводе вычисляем уровень стенки. 
                            matches = Regex.Matches(GetArrClassHtml(MainWB, "$(\"#fightGroupForm .group\");", "innerHTML")[1], "(?<=id=\"?)fighter(?<ID>([0-9])+)-life"); //Колличество противников
                            foreach (Match m in matches)
                            {
                                match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById(m.Value).Parent.Parent.InnerText, "(?<=[[])([0-9])+(?=[]])");
                                if (match.Success) FightNr += Convert.ToInt32(match.Value); //Суммируем уровни всех противников
                            }
                            switch (FightNr) //По сумме левелов определяем уровень стенки.
                            {
                                default: FightNr = 0; break;    //1 стенка 3 перса уровни 1, 2, 3
                                case 16: FightNr = 1; break;    //2 сетнка 4 перса уровни 3, 4, 4, 5
                                case 23: FightNr = 2; break;    //3 стенка 5 персов уровни 3, 3, 5, 5, 7
                                case 31: FightNr = 3; break;    //4 стенка 5 персов уровни 5, 5, 7, 7, 7
                                case 138: FightNr = 4; break;   //5 стенка 6 персов уровни 7, 7, 8, 8, 9 и вождь(99)
                            }
                            #endregion
                            break;
                        default :
                            vs = "Клан";
                            break;
                    }
                   
                    if (frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .me\").html()") == DBNull.Value) return false; //Видимо кто-то открыл страничку драки, но я в ней не участвую!
                    #region Считываем мои жизни на случай, если это старт бота
                    Me.Player.LifePkt = ((string)frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .me .life\").text()")).Split('/');
                    #endregion

                    while (IsWBComplete(MainWB, 500, 1000) && ((string)frmMain.GetJavaVar(MainWB, "$(\"#content\").attr(\"class\")") ?? "fight-group") == "fight-group") //Это всё ещё драка? или я уже сбежал и нахожусь на персонаже или ещё где?
                    {
                        //Используется функция .hide(), она делает атрибут style = display: none; инициализационное же значение null,
                        //функция .show() возвращает видимость и делает style = display: block; -> итог при null и  display: block; можно делать ход!                        
                        if (frmMain.GetDocument(MainWB).GetElementById("fight-actions") != null //Можно делать ход? (Кнопка видна пока ход ешё не сделан и покачто она только одна.)
                            && frmMain.GetJavaVar(MainWB, "$(\"#fight-actions .button:eq(0)\").html()") != DBNull.Value
                            && (string)frmMain.GetJavaVar(MainWB, "$(\"#fight-actions .button:eq(0)\").attr(\"style\")") != "display: none;"
                           )
                        {
                            #region Инициализация
                            for (int i = 0; i < FightItems.Count<stcFightItem>(); i++) FightItems[i].ID = null; //[0]Лечение, [1]Сыр, [2]Гранаты +, [3]Гранаты %, [4]Каска, [5]Пружина, [6]Щит, [x] -> ItemID
                            MonitorDT = DateTime.Now.AddSeconds(40); //При каждом ходе ставим мониторинг                           
                            Int64[] vsGroup = { 0, -1, 0, 0, 0 }; //0-> Самое мелкое HP, 1-> ID Игрока для аттаки, 2-> Сумма недобитых HP, 3-> Кол-во недобитых противников, 4-> Количество уже перебранных противников, с подходящими для нападения параметрами
                            Int64[] myGroup = { 0, -1, 0, 0 }; //0-> Самое мелкое HP, 1-> ID Игрока для защиты, 2-> Сумма недобитых HP, 3-> Кол-во недобитых напарников
                            ItemUsed = false;
                            #endregion
                            #region Что у меня с собой?
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("fightGroupForm");
                            if (HtmlEl != null && HtmlEl.InnerHtml != null)
                            {
                                matches = Regex.Matches(HtmlEl.InnerHtml, "(?<=id=\"?)use-(?<ID>([0-9])+)");
                                foreach (Match m in matches)
                                {
                                    Info = frmMain.GetJavaVar(MainWB, "m.items['" + m.Groups["ID"].Value + "'].info.title");
                                    bool Ultra = Regex.IsMatch((string)Info, "[[]Ультра[]]");

                                    Info = frmMain.GetJavaVar(MainWB, "m.items['" + m.Groups["ID"].Value + "'].info.content");
                                    match = Regex.Match((string)Info, "(?<Heal>Жизни:([ 0-9+%])+)|Мин. урон по врагам: ((?<PrcBomb>([0-9])+%)|(?<Bomb>([0-9])+))|(?<Cheese>Призыв крысомах в групповой бой)|(?<Helmet>Защита от урона)|(?<Spring>Отражает урон)|(?<Shield>Уменьшение урона от гранат)");
                                    if (match.Groups["Heal"].Success) FightItems[0].ID = m.Value;
                                    if (match.Groups["Cheese"].Success) FightItems[1].ID = m.Value;
                                    if (match.Groups["Bomb"].Success && FightItems[2].ID == null) FightItems[2].ID = m.Value; //Ультра со сроком годности самая первая, упорядочивается мосваром
                                    if (match.Groups["PrcBomb"].Success && FightItems[3].ID == null) FightItems[3].ID = m.Value; //Ультра со сроком годности самая первая, упорядочивается мосваром
                                    if (match.Groups["Helmet"].Success) { FightItems[4].ID = m.Value; FightItems[4].WorkTurns = Convert.ToInt32(Regex.Match((string)Info, "(?<=Защита от урона на: )[0-9](?= хода)").Value); }
                                    if (match.Groups["Spring"].Success) FightItems[5].ID = m.Value;
                                    if (match.Groups["Shield"].Success) { FightItems[6].ID = m.Value; FightItems[6].WorkTurns = Convert.ToInt32(Regex.Match((string)Info, "(?<=Действует ходов: )[0-9]").Value); }
                                }
                            }
                            #endregion
                            #region Считываем мои жизни
                            Me.Player.LifePkt = ((string)frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .me .life\").text()")).Split('/');
                            Me.Player.LifePkt[0] = ExpandCollapsedNumber(Me.Player.LifePkt[0]);
                            Me.Player.LifePkt[1] = ExpandCollapsedNumber(Me.Player.LifePkt[1]);
                            double MyLifePrc = Convert.ToDouble(Me.Player.LifePkt[0]) / Convert.ToDouble(Me.Player.LifePkt[1]) * 100;
                            #endregion
                            #region Считываем чат
                            //string[] Chat = GetArrClassHtml(MainWB, "$(\"#messages .p\").slice(" + ChatMsgCount + ");", "innerText"); //Считываем только новые, необработанные прошлыми циклами сообщения!
                            //ChatMsgCount += Chat.Count(); //Актуализируем колличество уже прочитаных сообщений.
                            string[] Chat = GetArrClassHtml(MainWB, "$(\"#messages .p\");", "innerText"); //Прошлые сообщения сами исчезают...
                            #endregion
                            #region Бьем пахана?
                            if (vs == "Мафия")
                            {
                                #region Пытаемся прочитать рупор!
                                Info = frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .rupor\").text()");
                                if ((string)Info != "") { Array.Resize(ref Chat, Chat.Count() + 1); Chat[Chat.Count() - 1] = (string)Info; } //Добавляем рупор последней строкой чата и проводим сканирование комбо!

                                foreach (string Message in Chat.Where(Message => Message != null))
                                {
                                    match = Regex.Match(Message, "^([\\s])*(?<o1>(якудза|я|z|госдеп|г|u|чоп|ч|x))( |-|[+])?((?<o2>(якудза|я|z|госдеп|г|u|чоп|ч|x))( |-|[+])?)?(?<o3>(якудза|я|z|госдеп|г|u|чоп|ч|x))?", RegexOptions.IgnoreCase);
                                    if (match.Success)
                                    {
                                        for (int i = 0; i < 3; i++)
                                        {
                                            switch (match.Groups["o" + (i + 1)].Value.ToLower())
                                            {
                                                case "я":
                                                case "якудза":
                                                case "z":
                                                    Rupor[i] = "Якудза";
                                                    break;
                                                case "г":
                                                case "госдеп":
                                                case "u":
                                                    Rupor[i] = "Госдеп";
                                                    break;
                                                case "ч":
                                                case "чоп":
                                                case "x":
                                                    Rupor[i] = "Чоповец";
                                                    break;
                                            }
                                        }
                                    }
                                }
                                #endregion
                                #region Бьем пахана
                                Info = frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .hyper-kick.p9\").html()"); //Супер-удар готов?
                                if (Info != DBNull.Value) { NPC = "Пахан"; } //Бьем пахана!
                                #endregion
                                else
                                #region Поиск цели и выбор комбо
                                {
                                    Info = frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .super-combination\").html()");
                                    if (MyLifePrc != 0 && Info != DBNull.Value) //Я то ещё жив + Скала супер удара ещё на месте?
                                    {
                                        matches = Regex.Matches((string)Info, "option( (?<Enemy>yakudza|gosdep|chop)?|(?<Status>target)?)+");   //"option (?<Opponent>yakudza|gosdep|chop)?[ ]?(?<Status>target|undefined)?"
                                        for (int i = 0; i < 3; i++)
                                        {
                                            if (matches[i].Groups["Status"].Success) //Это цель!
                                            {
                                                #region Подбор комбинации, проверка успешности прошлого хода
                                                if (CurrOpp >= i) //Прошлым ходом, явно попал не по тому и скинул комбинацию в начало или не попал вовсе. 
                                                {
                                                    Info = frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .attack_i\").html()"); //Проверяем кого я бил прошлым ходом и удачно ли?                                                   
                                                    if (i == 0 && Info != DBNull.Value) //если сбил комбинацию то начинаю с начала, иначе может быть повторный заход по знакомой комбинации до места поиска!
                                                    {
                                                        match = Regex.Match((string)Info, "бьёт([\\s\\S])+(?<NPC>Якудза|Госдеп|Чоповец)");
                                                        #region Обманули в рупоре ?
                                                        if (match.Groups["NPC"].Value == Rupor[CurrOpp]) { Rupor[CurrOpp] = null; if (DebugMode) BugReport("* Рупор обман: " + (CurrOpp + 1) + " не " + Rupor[CurrOpp]); }
                                                        #endregion
                                                        switch (match.Groups["NPC"].Value) //Пробовали прошлым ходом, нужен следующий
                                                        {
                                                            case "Якудза": //Пробовали прошлым ходом, нужен следующий
                                                                SearchOpp[0] = true;
                                                                break;
                                                            case "Госдеп": //Пробовали прошлым ходом, нужен следующий
                                                                SearchOpp[1] = true;
                                                                break;
                                                            case "Чоповец": //Пробовали прошлым ходом, нужен следующий
                                                                SearchOpp[2] = true;
                                                                break;
                                                        }
                                                    }
                                                }
                                                if (CurrOpp < i) { SearchOpp = new bool[] { false, false, false }; CurrOpp = i; } //Инициализация нового поиска + Текущий индекс в комбинации (Когда сбиваемся с комбинации, нельзя терять результаты)
                                                #endregion
                                                if (matches[i].Groups["Enemy"].Success) //Искать не нужно, комбо известено
                                                {
                                                    #region Комбо известено
                                                    switch (matches[i].Groups["Enemy"].Value)
                                                    {
                                                        case "yakudza":
                                                            NPC = "Якудза";
                                                            break;
                                                        case "gosdep":
                                                            NPC = "Госдеп";
                                                            break;
                                                        case "chop":
                                                            NPC = "Чоповец";
                                                            break;
                                                    }
                                                    #endregion
                                                }
                                                else //Ищем комбинацию
                                                {
                                                    #region Ищем комбинацию
                                                    if (Rupor[i] != null) NPC = Rupor[i];
                                                    else
                                                    {   //кого ещё не опробовали последний.
                                                        if (!SearchOpp[2]) NPC = "Чоповец"; //Ещё не пробовал, не попал прошлым ходом                                                            
                                                        if (!SearchOpp[1]) NPC = "Госдеп";  //Ещё не пробовал, не попал прошлым ходом
                                                        if (!SearchOpp[0]) NPC = "Якудза";  //Ещё не пробовал, не попал прошлым ходом
                                                    }
                                                    #endregion
                                                }
                                                break; //Оппонент найден нет смысла искать далее
                                            }
                                        }
                                    }
                                }
                                #endregion
                            }
                            #endregion
                            #region Поиск самого слабого + Считываем суммарно недобитое HP всех оставшихся союзников
                            #region Я ещё жив, добавляем меня и мои жизни.
                            myGroup[2] = Convert.ToInt32(Me.Player.LifePkt[0]);
                            myGroup[3] = GetArrClassCount(MainWB, "$(\"#fightGroupForm .group:first .alive\");"); //Считываем количество живых игроков на моей стороне, включая меня! (:first первую группу ибо в ней я и союзники!)
                            #endregion
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("fightGroupForm");
                            if (HtmlEl != null)
                            {
                                matches = Regex.Matches(HtmlEl.InnerHtml, "(?<=id=\"?)defence-(?<ID>([0-9])+)");
                                foreach (Match m in matches) //Выискиваем самого слабого игрока на данный момент + недобитое HP всех оставшихся противников!
                                {
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById(m.Value.Replace("defence-", "fighter") + "-life");
                                    if (HtmlEl != null) //У сбежавшего из боя Омоновца нет жизней!
                                    {
                                        string[] HP = HtmlEl.InnerText.Contains("%") ? new string[] { ((int)(Convert.ToInt32(Me.Player.LifePkt[1]) * Expert.RevengerPrc / 100 * Convert.ToInt32(HtmlEl.InnerText.Replace("%", "")) / 100)).ToString(), ((int)(Convert.ToInt32(Me.Player.LifePkt[1]) * Expert.RevengerPrc / 100)).ToString() } : HtmlEl.InnerText.Split('/'); //Выдираем жизни игрока если мститель делаем будто у него столько же жизней сколько и у меня!
                                        HP[0] = ExpandCollapsedNumber(HP[0]);
                                        HP[1] = ExpandCollapsedNumber(HP[1]);
                                        if (HP[0] != "0") //Ещё живой
                                        {
                                            if (myGroup[0] == 0 || Convert.ToInt32(HP[0]) < myGroup[0]) //Слабее прошлого?
                                            {
                                                myGroup[0] = Convert.ToInt32(HP[0]);
                                                myGroup[1] = Convert.ToInt32(m.Groups["ID"].Value);
                                            }
                                            myGroup[2] += Convert.ToInt32(HP[0]); //Суммируем недобитые жизни союзников
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region Поиск самого слабого + Считываем суммарно недобитое HP всех оставшихся противников
                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("fightGroupForm");
                            if (HtmlEl != null)
                            {
                                matches = Regex.Matches(HtmlEl.InnerHtml, "(?<=id=\"?)attack-(?<ID>([0-9])+)");
                                foreach (Match m in (vs == "Мафия" ? matches.OfType<Match>().Reverse() : matches.OfType<Match>())) //Выискиваем самого слабого игрока на данный момент + недобитое HP всех оставшихся противников!
                                {
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById(m.Value.Replace("attack-", "fighter") + "-life");
                                    if (HtmlEl != null) //У сбежавшего из боя Омоновца нет жизней!
                                    {
                                        string[] HP = HtmlEl.InnerText.Contains("%") ? new string[] { ((int)(Convert.ToInt32(Me.Player.LifePkt[1]) * Expert.RevengerPrc / 100 * Convert.ToInt32(HtmlEl.InnerText.Replace("%", "")) / 100)).ToString(), ((int)(Convert.ToInt32(Me.Player.LifePkt[1]) * Expert.RevengerPrc / 100)).ToString() } : HtmlEl.InnerText.Split('/'); //Выдираем жизни игрока если мститель делаем будто у него столько же жизней сколько и у меня!
                                        HP[0] = ExpandCollapsedNumber(HP[0]);
                                        HP[1] = ExpandCollapsedNumber(HP[1]);
                                        if (HP[0] != "0") //Ещё живой
                                        {
                                            switch (vs)
                                            {
                                                case "Мафия":
                                                    #region Пахан
                                                    if (vsGroup[4] < 2 && frmMain.GetDocument(MainWB).GetElementById(m.Value).NextSibling.InnerText.Contains(NPC))
                                                    {
                                                        vsGroup[1] = Convert.ToInt32(m.Groups["ID"].Value); //Сохраняем ID кого собрались бить, если есть более живые, поменяем на них, нет, бьем кто есть!
                                                        vsGroup[4] += Convert.ToDouble(HP[0]) / Convert.ToDouble(HP[1]) > 0.5 ? 1 : 0; //Нашли более живого?
                                                    }
                                                    #endregion
                                                    break;
                                                default:
                                                    #region Иные бои
                                                    if (vsGroup[0] == 0 || (Convert.ToInt32(HP[0]) < vsGroup[0] && Convert.ToDouble(HP[0]) / Convert.ToDouble(HP[1]) > ((Settings.Lampofob && !Regex.IsMatch(vs, "Ленинопровод|Крысомахи")) ? 0.3 : 0)))
                                                    {
                                                        vsGroup[0] = Convert.ToInt32(HP[0]);
                                                        vsGroup[1] = Convert.ToInt32(m.Groups["ID"].Value);
                                                    }
                                                    #endregion
                                                    break;
                                            }
                                            vsGroup[2] += Convert.ToInt32(HP[0]); //Суммируем недобитые жизни противников
                                            vsGroup[3]++; //Ведём отчёт ещё живых                                       
                                        }
                                    }
                                }
                            }
                            #endregion
                            #region Вычисление текущего хода
                            Turn = Convert.ToInt32("0" + Regex.Match((string)(frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .fight-log h4\").text()") ?? ""), "(?<=Ход )([0-9])+").Value);
                            #endregion
                            if (MyLifePrc > 0) //Я то ещё жив?
                            {
                                #region Использовать предметы?
                                switch (vs)
                                {
                                    case "Крысомахи":
                                        Weakness = new double[] { Convert.ToDouble(vsGroup[2]) / Convert.ToDouble(Me.Player.LifePkt[0]), Convert.ToDouble(vsGroup[2]) / Convert.ToDouble(Me.Player.LifePkt[1]) }; //Во сколько раз HP оставшееся у противника превосходит моё максимальное HP                                       
                                        #region Лечение
                                        if (FightItems[0].ID != null && Settings.UseRatItems[0 + FightNr] && MyLifePrc <= 20 & Weakness[1] <= 4)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[0].ID).InvokeMember("click"); //Ставим галочку, готовимся лечиться!
                                            UpdateStatus("# " + DateTime.Now + (" Принимаю \"Аспирин\", никому не разбегаться!"));
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Иное [Щит]
                                        if (FightItems[6].ID != null && Settings.UseRatItems[21 + FightNr] && FightItems[6].TillTurn <= Turn && Weakness[0] >= 2 && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[6].ID).InvokeMember("click"); //Ставим галочку, готовимся ставить щит!
                                            UpdateStatus("# " + DateTime.Now + " Прикроюсь ка я щитом, вдруг они с камнями пришли!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            FightItems[6].TillTurn = Turn + FightItems[6].WorkTurns; //Подсчёт хода, до которого я буду под защитой
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Медведь
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("useabl-3");
                                        if (HtmlEl != null && Settings.UseRatItems[28 + FightNr] && !ItemUsed)
                                        {
                                            HtmlEl.InvokeMember("click"); //Ставим галочку, готовимся выпускать медведя!
                                            Me.Bear.LastDT = DateTime.Now.AddHours(2); //Медведь будет не доступен ещё 3 часа, но после 2х часов, смотрим не нужна ли подзарядка!
                                            UpdateStatus("# " + DateTime.Now + " Медведь, выходи! Хватит хобот сосать, пора клиентоФ ломать!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Крысомаха
                                        if (FightItems[1].ID != null && Settings.UseRatItems[14 + FightNr] && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[1].ID).InvokeMember("click"); //Ставим галочку, готовимся выпускать крысу!
                                            UpdateStatus("# " + DateTime.Now + " Знакомьтесь, мой ручной Кинг-Конг ... -Крысюлька ФАаас!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Метание гранат, если ещё не лечился или проделывал что-то иное
                                        if (Settings.UseRatItems[7 + FightNr] && (FightItems[3].ID != null || FightItems[2].ID != null) && Weakness[0] >= 2 && !ItemUsed) //[2]Гранаты +, [3]Гранаты %, сначала сильные гранаты, добиваем более слабыми!
                                        {
                                            string[] HP = (myGroup[1] == -1) ? new string[2] { "0", "100" } : frmMain.GetDocument(MainWB).GetElementById("fighter" + myGroup[1] + "-life").InnerText.Split('/'); //Выдираем напарника (крысы)!   
                                            if (Convert.ToDouble(HP[0]) / Convert.ToDouble(HP[1]) <= 0.4) //Использовать гранаты, если жизни напарника упали ниже 40%
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[FightItems[3].ID != null ? 3 : 2].ID).InvokeMember("click"); //Ставим галочку, готовимся метать!
                                                UpdateStatus("# " + DateTime.Now + (" Метаю всякую хрень в толпу!"));
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        break;
                                    case "Ленинопровод":
                                        Weakness = new double[] { Convert.ToDouble(vsGroup[2]) / Convert.ToDouble(Me.Player.LifePkt[0]), Convert.ToDouble(vsGroup[2]) / Convert.ToDouble(Me.Player.LifePkt[1]) }; //Во сколько раз HP оставшееся у противника превосходит моё максимальное HP
                                        #region Лечение
                                        if (FightItems[0].ID != null && Settings.UseOilLeninItems[0 + FightNr] && MyLifePrc <= (FightNr < 4 ? 20 : 40) && (Weakness[1] <= 4 || myGroup[3] > 0))
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[0].ID).InvokeMember("click"); //Ставим галочку, готовимся лечиться!
                                            UpdateStatus("# " + DateTime.Now + (" Принимаю \"Аспирин\", никому не разбегаться!"));
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Иное [Щит]
                                        if (FightItems[6].ID != null && Settings.UseOilLeninItems[15 + FightNr] && FightItems[6].TillTurn <= Turn && Weakness[0] >= 2 && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[6].ID).InvokeMember("click"); //Ставим галочку, готовимся ставить щит!
                                            UpdateStatus("# " + DateTime.Now + " Прикроюсь ка я щитом, вдруг они с камнями пришли!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            FightItems[6].TillTurn = Turn + FightItems[6].WorkTurns; //Подсчёт хода, до которого я буду под защитой
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Медведь
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("useabl-3");
                                        if (HtmlEl != null && Settings.UseOilLeninItems[20 + FightNr] && !ItemUsed)
                                        {
                                            HtmlEl.InvokeMember("click"); //Ставим галочку, готовимся выпускать медведя!
                                            Me.Bear.LastDT = DateTime.Now.AddHours(2); //Медведь будет не доступен ещё 3 часа, но после 2х часов, смотрим не нужна ли подзарядка!
                                            UpdateStatus("# " + DateTime.Now + " Медведь, выходи! Хватит хобот сосать, пора клиентоФ ломать!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Крысомаха
                                        if (FightItems[1].ID != null && Settings.UseOilLeninItems[10 + FightNr] && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[1].ID).InvokeMember("click"); //Ставим галочку, готовимся выпускать крысу!
                                            UpdateStatus("# " + DateTime.Now + " Знакомьтесь, мой ручной Кинг-Конг ... -Крысюлька ФАаас!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Метание гранат, если ещё не лечился или проделывал что-то иное
                                        if (Settings.UseOilLeninItems[5 + FightNr] && (FightItems[3].ID != null || FightItems[2].ID != null) && Weakness[0] >= 2 && !ItemUsed) //[2]Гранаты +, [3]Гранаты %, сначала сильные гранаты, добиваем более слабыми!
                                        {
                                            string[] HP = (myGroup[1] == -1) ? new string[2] { "0", "100" } : frmMain.GetDocument(MainWB).GetElementById("fighter" + myGroup[1] + "-life").InnerText.Split('/'); //Выдираем напарника (крысы)!   
                                            if (Convert.ToDouble(HP[0]) / Convert.ToDouble(HP[1]) <= 0.6) //Использовать гранаты, если жизни напарника упали ниже 40%
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[FightItems[3].ID != null ? 3 : 2].ID).InvokeMember("click"); //Ставим галочку, готовимся метать!
                                                UpdateStatus("# " + DateTime.Now + (" Метаю всякую хрень в толпу!"));
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        break;
                                    case "Хаос":
                                    case "PVP":
                                    case "Руда":
                                    case "Клан":
                                        Weakness = new double[] { Convert.ToDouble(vsGroup[2]) / Convert.ToDouble(myGroup[2]), Convert.ToDouble(vsGroup[3]) / Convert.ToDouble(myGroup[3]) }; //Во HP сколько нашей группы больше противника, во сколько % наших больше
                                        #region Пружина до начала драки, если я самый слабый!
                                        if (FightItems[5].ID != null && Turn == 1 && Convert.ToInt32(Me.Player.LifePkt[0]) <= myGroup[0] && Weakness[1] > 0.8 //Я самый слабый? (Не по ID, ибо меня нет в списке defence)
                                             && ((Settings.UseGrpFightItems[3] && vs == "Хаос") || (Settings.UseGrpFightItems[8] && vs == "Руда") || (Settings.UseGrpFightItems[13] && vs == "Клан") || (Settings.UseGrpFightItems[18] && vs == "PVP"))
                                           )
                                        {
                                            if (FightItems[5].ID != null && MyLifePrc <= 35 && Weakness[1] < 1.3)
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[5].ID).InvokeMember("click"); //Ставим галочку, готовимся использовать пружину!
                                                UpdateStatus("# " + DateTime.Now + (" Надеваю \"Шиповку\", подходи кто смелый!"));
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        #region Иное [Каска]
                                        if (((Settings.UseGrpFightItems[3] && vs == "Хаос") || (Settings.UseGrpFightItems[8] && vs == "Руда") || (Settings.UseGrpFightItems[13] && vs == "Клан") || (Settings.UseGrpFightItems[18] && vs == "PVP"))
                                           && FightItems[4].ID != null && FightItems[4].TillTurn <= Turn && MyLifePrc <= 35 && (Weakness[0] < 1.2 || Weakness[1] < 0.9) && !ItemUsed) //теже параметры что и для лечения
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[4].ID).InvokeMember("click"); //Ставим галочку, готовимся ставить щит!
                                            UpdateStatus("# " + DateTime.Now + " Надеваю \"Кепочку\", я неуязвим!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            FightItems[4].TillTurn = Turn + FightItems[4].WorkTurns; //Подсчёт хода, до которого я буду под защитой
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Лечение
                                        if (!ItemUsed && ((Settings.UseGrpFightItems[2] && vs == "Хаос") || (Settings.UseGrpFightItems[7] && vs == "Руда") || (Settings.UseGrpFightItems[12] && vs == "Клан") || (Settings.UseGrpFightItems[17] && vs == "PVP"))
                                            && FightItems[0].ID != null && MyLifePrc <= 35 && (Weakness[0] < 1.2 || Weakness[1] < 0.9) && FightItems[4].TillTurn - Turn <= 1) //На случай если появятся шлемы скрывающие больше чем на 2 хода!
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[0].ID).InvokeMember("click"); //Ставим галочку, готовимся лечиться!
                                            UpdateStatus("# " + DateTime.Now + (" Принимаю \"Аспирин\", никому не разбегаться!"));
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Иное [Пружина/Щит]
                                        if (!ItemUsed && ((Settings.UseGrpFightItems[3] && vs == "Хаос") || (Settings.UseGrpFightItems[8] && vs == "Руда") || (Settings.UseGrpFightItems[13] && vs == "Клан") || (Settings.UseGrpFightItems[18] && vs == "PVP"))
                                           && (FightItems[5].ID != null || FightItems[6].ID != null) && Weakness[0] < 1.4)
                                        {
                                            if (FightItems[5].ID != null && MyLifePrc <= 35 && Weakness[1] < 1.3)
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[5].ID).InvokeMember("click"); //Ставим галочку, готовимся использовать пружину!
                                                UpdateStatus("# " + DateTime.Now + (" Надеваю \"Шиповку\", подходи кто смелый!"));
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                            if (!ItemUsed && FightItems[6].ID != null && FightItems[6].TillTurn <= Turn && vsGroup[3] >= 2)
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[6].ID).InvokeMember("click"); //Ставим галочку, готовимся ставить щит!
                                                UpdateStatus("# " + DateTime.Now + " Прикроюсь ка я щитом, вдруг они с камнями пришли!");
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                FightItems[6].TillTurn = Turn + FightItems[6].WorkTurns; //Подсчёт хода, до которого я буду под защитой
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        #region Крысомаха
                                        if (FightItems[1].ID != null && !ItemUsed && ((Settings.UseGrpFightItems[1] && vs == "Хаос") || (Settings.UseGrpFightItems[6] && vs == "Руда") || (Settings.UseGrpFightItems[11] && vs == "Клан") || (Settings.UseGrpFightItems[16] && vs == "PVP")))
                                        {
                                            if (Turn > 3) // KFan
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[1].ID).InvokeMember("click"); //Ставим галочку, готовимся выпускать крысу!
                                                UpdateStatus("# " + DateTime.Now + " Знакомьтесь, мой ручной Кинг-Конг ... -Крысюлька ФАаас!");
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        #region Медведь
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("useabl-3");
                                        if (HtmlEl != null && !ItemUsed && ((Settings.UseGrpFightItems[4] && vs == "Хаос") || (Settings.UseGrpFightItems[9] && vs == "Руда") || (Settings.UseGrpFightItems[14] && vs == "Клан") || (Settings.UseGrpFightItems[19] && vs == "PVP")))
                                        {
                                            HtmlEl.InvokeMember("click"); //Ставим галочку, готовимся выпускать медведя!
                                            Me.Bear.LastDT = DateTime.Now.AddHours(2); //Медведь будет не доступен ещё 3 часа, но после 2х часов, смотрим не нужна ли подзарядка!
                                            UpdateStatus("# " + DateTime.Now + " Медведь, выходи! Хватит хобот сосать, пора клиентоФ ломать!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Метание гранат
                                        if (!ItemUsed && ((Settings.UseGrpFightItems[0] && vs == "Хаос") || (Settings.UseGrpFightItems[5] && vs == "Руда") || (Settings.UseGrpFightItems[10] && vs == "Клан") || (Settings.UseGrpFightItems[15] && vs == "PVP"))
                                            && vsGroup[1] != -1 && (FightItems[2].ID != null || FightItems[3].ID != null) && (Weakness[0] < 1.4 || Weakness[1] < 1.2)) //[2]Гранаты +, [3]Гранаты % сначала слабые гранаты, экономим сильные! (если есть хоть один противник)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[FightItems[2].ID != null ? 2 : 3].ID).InvokeMember("click"); //Ставим галочку, готовимся метать!
                                            UpdateStatus("# " + DateTime.Now + (" Метаю всякую хрень в толпу!"));
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        break;
                                    case "Мафия":
                                        if (!Settings.GoGroupFightMafia) goto case "Ленин";
                                        #region Иное [Каска/Пружина]
                                        if ((FightItems[4].ID != null || FightItems[5].ID != null) && Settings.UseGrpFightItems[23] && MyLifePrc <= 35 && FightItems[4].TillTurn <= Turn) //не использовать, если ещё работает каска!
                                        {
                                            if (FightItems[4].ID != null && ((FightItems[5].ID == null && Turn % 5 != 1 && Turn % 5 != 2) || Turn % 5 == 4 || Turn % 5 == 0)) //Использовать каски, если нет пружин на любом кроме первых 2х ходов, или если есть пружины то за ход до обстрела/во врема обстрела
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[4].ID).InvokeMember("click"); //Ставим галочку, готовимся использовать каску!
                                                UpdateStatus("# " + DateTime.Now + " Надеваю \"Кепочку\", я неуязвим!");
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                FightItems[4].TillTurn = Turn + FightItems[4].WorkTurns; //Подсчёт хода, до которого я буду под защитой
                                                ItemUsed = true;
                                            }
                                            if (FightItems[5].ID != null && Turn % 5 != 1 && Turn % 5 != 2 && !ItemUsed)
                                            {
                                                frmMain.GetDocument(MainWB).GetElementById(FightItems[5].ID).InvokeMember("click"); //Ставим галочку, готовимся использовать пружину!
                                                UpdateStatus("# " + DateTime.Now + " Надеваю \"Шиповку\", подходи кто смелый!");
                                                Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                                ItemUsed = true;
                                            }
                                        }
                                        #endregion
                                        #region Лечение
                                        if (FightItems[0].ID != null && Settings.UseGrpFightItems[22] && MyLifePrc <= 30 && FightItems[4].TillTurn <= Turn && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[0].ID).InvokeMember("click"); //Ставим галочку, готовимся лечиться!
                                            UpdateStatus("# " + DateTime.Now + (" Принимаю \"Аспирин\", никому не разбегаться!"));
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Крысомаха
                                        if (FightItems[1].ID != null && Settings.UseGrpFightItems[21] && vsGroup[3] / myGroup[3] > 2 && !ItemUsed)
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById(FightItems[1].ID).InvokeMember("click"); //Ставим галочку, готовимся выпускать крысу!
                                            UpdateStatus("# " + DateTime.Now + " Знакомьтесь, мой ручной Кинг-Конг ... -Крысюлька ФАаас!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        #region Медведь
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("useabl-3");
                                        if (HtmlEl != null && Settings.UseGrpFightItems[24] && vsGroup[3] / myGroup[3] > 2 && !ItemUsed)
                                        {
                                            HtmlEl.InvokeMember("click"); //Ставим галочку, готовимся выпускать медведя!
                                            Me.Bear.LastDT = DateTime.Now.AddHours(2); //Медведь будет не доступен ещё 3 часа, но после 2х часов, смотрим не нужна ли подзарядка!
                                            UpdateStatus("# " + DateTime.Now + " Медведь, выходи! Хватит хобот сосать, пора клиентоФ ломать!");
                                            Wait(200, 500); //IsWBComplete(MainWB, 200, 500); //Ожидание + симуляция игрока.
                                            ItemUsed = true;
                                        }
                                        #endregion
                                        break;
                                    case "Ленин":
                                    case "Королева крысомах":
                                        #region Просто не мешаем играть!
                                        UpdateStatus("@ " + DateTime.Now + " Ого, начальство походу рубится, посижу ка тихохонько погляжу...");
                                        do
                                        {
                                            IsWBComplete(MainWB);
                                            if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/fight/(([0-9])+)?")) return true; //Видимо драку проводили в не бровзера бота.
                                            foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("fightGroupForm").GetElementsByTagName("H3"))
                                            {
                                                if (H.InnerText.Contains("Результат боя")) return true;
                                            }
                                            if (MainWB.InvokeRequired) Thread.Sleep(30000);
                                            else Application.DoEvents();
                                        } while (true);
                                        #endregion
                                }
                                #endregion
                                #region Атака противника (Не использовал ничего!)
                                if (!ItemUsed && vsGroup[1] != -1) //Ничего не использовал, просто бьем ногой!
                                {
                                    frmMain.GetDocument(MainWB).GetElementById("attack-" + vsGroup[1]).InvokeMember("click");
                                    Wait(200, 500);
                                }
                                #endregion
                            }
                            else UpdateStatus("# " + DateTime.Now + " " + ((vs == "Ленинопровод" || vs == "Крысомахи") && myGroup[3] > 0 ? "Похоже меня поломали, вот лежу, наблюдаю ..., без меня ведь не закончат!" : "Я ещё слишком мал умирать, пинайте его ..., мне пора!"));
                            //Делаем ход.
                            //Подтверждение завершения хода -> frmMain.GetDocument(MainWB).InvokeScript("groupFightMakeStep");
                            //Покинуть бой -> frmMain.GetDocument(MainWB).InvokeScript("groupFightExit"); 

                            if ((vs == "Ленинопровод" || vs == "Крысомахи") && MyLifePrc == 0 && myGroup[3] > 0) //Я уже умер, но кто-то из моей команды ещё жив, дожидаемся конца стенки!
                            {
                                #region Создаём иллюзию, что я ещё жив, за меня сражаются мои напарники
                                MainWB.Tag = "Ajax"; //Ожидание конца хода, похоже некоторые бои уже перевели на Аякс
                                #endregion
                            }
                            else //Порядок можно делать ход или не дожидаясь конца покинуть стенку!
                            {
                                if (frmMain.GetDocument(MainWB).GetElementById("fight-actions") != null //Можно делать ход? (Кнопка видна пока ход ешё не сделан и покачто она только одна.)
                                    && frmMain.GetJavaVar(MainWB, "$(\"#fight-actions .button:eq(0)\").html()") != DBNull.Value
                                    && (string)frmMain.GetJavaVar(MainWB, "$(\"#fight-actions .button:eq(0)\").attr(\"style\")") != "display: none;"
                                   )
                                {
                                    //frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click"); //Сабмит формы по хорошему можно делать прямо по форме! 
                                    if (MyLifePrc > 0) frmMain.InvokeScript(MainWB, "groupFightMakeStep");
                                    else frmMain.InvokeScript(MainWB, "groupFightExit");
                                    IsWBComplete(MainWB);
                                    #region Обработка Ajax
                                    if ((frmMain.GetJavaVar(MainWB, "AngryAjax.turned") ?? "0").Equals("1")) MainWB.Tag = "Ajax"; //Ожидание конца хода                                 
                                    #endregion
                                }                               
                            }
                        }
                        #region Ход затянулся?
                        if (MonitorDT <= DateTime.Now) { frmMain.NavigateURL(MainWB, Settings.ServerURL + "/player/"); MonitorDT = DateTime.Now.AddSeconds(40); }  //Видимо ход затянулся, обновляем страничку!
                        #endregion
                        
                        IsWBComplete(MainWB);                        
                        if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/") && frmMain.GetJavaVar(MainWB, "$(\"#fightGroupForm .result\").html()") != DBNull.Value) //Всё, бой закончен
                        {
                            #region Махинация, ибо - после некоторых завершенных боев всё ещё остаётся таймер и можем попасть в ловушку!
                            bool bRet = GetArrClassCount(MainWB, "$(\"#fightGroupForm .group:first .alive\");") > 0;
                            GoToPlace(MainWB, Place.Player);                            
                            return bRet;
                            #endregion
                        }                        
                    }
                    break;
                    #endregion
            }
            return false;
        }
        public bool Police(PoliceAction PA) //NOK proverit' kogda net deneg ili rudy dl'a pokupki!
        {
            BugReport("Police");

            switch (PA)
            {
                case PoliceAction.Pay:
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Money >= 50 * Me.Player.Level)
                    {
                        frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("fine-form"), "submit"); //23.12.12 Обнаружены изменения в оплате взятками, теперь вот так.
                        UpdateMyInfo(MainWB);
                        return Me.Police.Val < Settings.PayPoliceAt; 
                    }                   
                    return false; //Нехватило денег откупится!
                case PoliceAction.Relations:
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Ore >= 20)
                    { GoToPlace(MainWB, Place.Police, "/relations", false); return Police(PoliceAction.Check); } //chKURL -> false  потому-что в итоге остаюсь на страничке POLICE!
                    return false; //Нехватило руды для наладки отношений!
                case PoliceAction.Check:
                    GoToPlace(MainWB, Place.Police);
                    UpdateMyInfo(MainWB); //Считываем текуший розыск в полиции
                    //
                    Match match = Regex.Match(frmMain.GetDocument(MainWB).Forms[1].InnerText, "(?<=Связи налажены до )([\\w :])+"); //Связи налажены до 19 марта 2012 18:41
                    if (match.Success) Me.Police.LastDT = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                    if (!match.Success && !Settings.WaitPolice && !Settings.PayPolice) return Police(PoliceAction.Relations); //Связи ешё не налажены, но должны быть налажены!
                    if (!match.Success && !Settings.WaitPolice && Settings.PayPolice && Me.Police.Val >= Settings.PayPoliceAt) return Police(clsBot.PoliceAction.Pay);
                    //
                    match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("content").InnerText, "(?<=Вас задержали за драки и отпустят через:)([0-9 :])+");
                    if (match.Success) //HtmlEl.All[9].InnerText.IndexOf("Вас задержали за драки и отпустят через:") != -1 & HtmlEl.All[10].GetAttribute("ClassName") == "timer"
                    {
                        string[] Time = match.Value.Split(':'); //HH:mm:ss //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                        Wait(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2])), " Сижу в милиции! -Пью чай, ем плюшки до: ", TimeOutAction.Blocked); //Сюда можно попасть только, если уже прошел проверку шагом выше о продлении связей!                        
                    }
                    break;
            }
            return true; //Всё в порядке
        }
        public bool MC(MCAction MA, decimal WT = 1) //OK
        {
            BugReport("MC");

            HtmlElement HtmlEl;
            #region Переодевание
            if (MA == MCAction.Work && Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 2);
            #endregion
            GoToPlace(MainWB, Place.Shaurburgers);
            switch (MA)
            {
                case MCAction.Work:
                    if (!MC(MCAction.Check))
                    {
                        if (Me.Wanted) UpdateStatus("! " + DateTime.Now + " Пора играть в прятки...");
                        HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("time").GetElementsByTagName("option"); //Проверка, есть ли необходимое для действия время.
                        frmMain.GetDocument(MainWB).GetElementById("time").SetAttribute("value", Convert.ToInt32(HC[HC.Count - 1].GetAttribute("value")) >= WT ? WT.ToString() : HC[HC.Count - 1].GetAttribute("value"));
                        frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("workForm"), "submit");
                        IsWBComplete(MainWB);
                        if (Thread.CurrentThread.Name == "MainBotThread") MC(MCAction.Check); //Не проделывать при Shutdown
                    }
                    return true;
                case MCAction.Check:
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("shaurma");
                    if (HtmlEl != null)
                    {
                        if (HtmlEl.InnerText == null)
                        {
                            UpdateStatus("! " + DateTime.Now + " Ааааааай, чуть ни ёпнулся ... чертовы лаги!");
                            Wait(new TimeSpan(Convert.ToInt32(Settings.MCWorkTime),0,0), " Устроился на работу в Шаурбургерс до: ", TimeOutAction.Blocked);
                        }
                        else Wait(TimeSpan.Parse(HtmlEl.InnerText), " Устроился на работу в Шаурбургерс до: ", TimeOutAction.Blocked); //Wait(Convert.ToDateTime(HtmlEl.InnerText), " Устроился на работу в Шаурбургерс до: ", TimeOutAction.Blocked);

                        Random rndWait = new Random();
                        Wait(new TimeSpan(0, rndWait.Next(1, 20), rndWait.Next(0, 60)), " Активирована задержка до: ", TimeOutAction.Blocked);
                        return true;
                    }
                    return false;
            }
            return true;
        }
        public void Factory(FactoryAction FA) //OK
        {
            BugReport("Factory");
                        
            Match match;
            HtmlElement HtmlEl;

            switch (FA)
            { 
                case FactoryAction.Petriki:
                    #region Нужен бонус? Смотрим сколько у нас с собой чёрных бухгалтерий!
                    if (Settings.PetrikiBonus > 0)
                    {
                        #region Инициализация
                        Me.Petriki.BlackBook = 0;  //
                        Me.Petriki.NeedCoffee = 0; //
                        Me.Petriki.NeedHoney = 0;  //
                        #endregion
                        if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player);
                        string[] ArrInfo = GetArrClassHtml(MainWB, "$(\"#content .equipment-cell .object-thumbs .object-thumb\");", "innerHTML");

                        foreach (string Info in ArrInfo) //black_accounting.png
                        {
                            match = Regex.Match(Info, "(?<MafiaBook>black_accounting.png).+data-id=\"?(?<ID>([0-9])+)\"?");
                            if (match.Success) //Найдены чёрные бухгалтерии
                            {
                                Me.Petriki.BlackBook = Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "m.items['" + match.Groups["ID"] + "'].count['0'].innerText"), "([0-9])+").Value);
                                break; //Выходим, нет смысла дальше листать
                            }                      
                        }
                    }
                    #endregion
                ReTry:
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith("/factory/")) GoToPlace(MainWB, Place.Factory);
                    
                    #region Приобретение бонуса за кофе
                    while ((Me.Petriki.Bonus = GetArrClassCount(MainWB, "$(\"#content .coffee-boost .cup5\");")) < Settings.PetrikiBonus && Me.Petriki.NeedCoffee <= 0 && Me.Petriki.NeedHoney <= Me.Wallet.Honey)
                    { 
                        UpdateMyInfo(MainWB);
                        HtmlEl = frmMain.GetDocument(MainWB).Forms[1].GetElementsByTagName("button")[0];
                        Me.Petriki.NeedCoffee = GetArrClassCount(MainWB, "$(\"#content .labcoffee.none\")");
                        #region Вычисление цены в мёде=)
                        switch (Me.Petriki.Bonus)
                        { 
                            case 4:
                                Me.Petriki.NeedHoney = 1;
                                break;
                            case 9:
                                Me.Petriki.NeedHoney = 5;
                                break;
                            default:
                                Me.Petriki.NeedHoney = 0;
                                break;
                        }
                        #endregion
                        if (Me.Petriki.NeedCoffee <= 0 && HtmlEl.InnerText.Contains("Угостить") && Me.Wallet.Honey >= Me.Petriki.NeedHoney) //5->Кофе, 1-> Мёд
                        {
                            UpdateStatus("@ " + DateTime.Now + " Мужички, редбулла не было, принёс кофеёк!");
                            frmMain.InvokeMember(MainWB, HtmlEl, "click");
                            IsWBComplete(MainWB, 1000, 1500);
                        }
                        if (!frmMain.GetDocumentURL(MainWB).EndsWith("/factory/")) GoToPlace(MainWB, Place.Factory);      
                    }
                    if (Settings.PetrikiBonus > Me.Petriki.Bonus && Me.Wallet.Honey >= Me.Petriki.NeedHoney)
                    {
                        for (int i = Me.Petriki.BlackBook; i > 0; i--)
                        {
                            if (Me.Petriki.BlackBook > 0 && Me.Petriki.NeedCoffee > 0 && Me.Petriki.NeedCoffee - 3 * i <= 0) 
                            {
                                UpdateStatus("# " + DateTime.Now + " Хлопцы не спать! -Я побежал \"чёрные книжки\" на \"чёрный кофе\" менять!");
                                MobilePhone(MobilePhoneAction.MafiaTrade);
                            }
                            else break; //Один чёрт не хватит чашек кофе, не стоит менять последнюю бухгалтерию!
                        }
                        if (Me.Petriki.NeedCoffee <= 0) goto ReTry;
                    }                    
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith("/factory/")) GoToPlace(MainWB, Place.Factory);                   
                    #endregion                    
                    #region Вычисление цены варения петриков учитывая бонус=)
                    Me.Petriki.Ore = 5 * (Me.Petriki.Bonus + 1 + Me.Petriki.Bonus / 5);
                    Me.Petriki.Money = 500 * (Me.Petriki.Bonus + 1 + Me.Petriki.Bonus / 5);
                    UpdateMyInfo(MainWB);
                    if ((Me.Wallet.Money - Me.Petriki.Money < Settings.minPetrikiMoney) || (Me.Wallet.Ore - Me.Petriki.Ore < Settings.minPetrikiOre)) return; //Считываем текущую цену на варение петриков, и если не хватает ресурсов просто покидаем функцию
                    #endregion

                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("petriksprocess");
                    if (HtmlEl == null) //Это таймер, соответственно имеется, только во время производства.
                    {
                        frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).Forms[0], "submit");
                        IsWBComplete(MainWB, 1000, 1500);
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("petriksprocess"); //Снова загружаю ссылку на эллемент, так как после перезагрузки странички (фрейма) прошлая ссылка неработоспособна!
                        UpdateStatus("* " + DateTime.Now + (HtmlEl == null ? " С криками \"Эээкскюзми, я Блатной!\", сварил Нанопетрики в не очереди." : " Запущено производство Нанопетриков, ожидаются в: " + DateTime.Now.Add(TimeSpan.Parse(HtmlEl.InnerText)).ToString("HH:mm:ss")));
                        if (HtmlEl == null) goto ReTry; //Сварил в не очереди, повторяем!
                    }
                    Me.Petriki.RestartDT = (HtmlEl == null ? DateTime.Now : DateTime.Now.Add(TimeSpan.Parse(HtmlEl.InnerText) > new TimeSpan(2, 0, 0) ? new TimeSpan(0, 0, 0) : TimeSpan.Parse(HtmlEl.InnerText)).AddMinutes(1)); //Добавлаем 1 минуту, ибо петрики падают не сразу...
                    break;
                case FactoryAction.UpdateChain:
                    int[] ArrUpItems = null;
                    int[] ArrSellItems = null;                    
                    GoToPlace(MainWB, Place.Factory,"/mf");
                    match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .factory-mf-skill\").text()"), "Навык мф.:([\\s])*(?<Points>([0-9/])+)([\\s])*Звание: (?<Rang>[А-Я]([а-я ])+)"); //Такой Regex из-за долбанного IE7, в нем Ранг пишется в плотную с остальным текстом в частности с словом "Вместе"
                    #region Points
                    string[] Points = match.Groups["Points"].Value.Split('/');                    
                    ChainUpgrade.Points = new int[2] { Convert.ToInt32(Points[0]), Convert.ToInt32(Points[1]) };
                    #endregion
                    #region Rang
                    switch (match.Groups["Rang"].Value)
                    {
                        case "Стажер": ChainUpgrade.Rang = 0;
                            break;
                        case "Студент": ChainUpgrade.Rang = 1;
                            break;
                        case "Криворучкин": ChainUpgrade.Rang = 2;
                            break;
                        case "Разбиратель": ChainUpgrade.Rang = 3;
                            break;
                        case "Юннат": ChainUpgrade.Rang = 4;
                            break;
                        case "Эникейщик": ChainUpgrade.Rang = 5;
                            break;
                        case "Подмастерье": ChainUpgrade.Rang = 6;
                            break;
                        case "Слесарь": ChainUpgrade.Rang = 7;
                            break;
                        case "Самоделкин": ChainUpgrade.Rang = 8;
                            break;
                        case "Испытатель": ChainUpgrade.Rang = 9;
                            break;
                        case "Подрядчик": ChainUpgrade.Rang = 10;
                            break;
                        case "Аппаратчик": ChainUpgrade.Rang = 11;
                            break;
                        case "Наладчик": ChainUpgrade.Rang = 12;
                            break;
                        case "Перворазрядник": ChainUpgrade.Rang = 13;
                            break;
                        case "Кандидат в мастера": ChainUpgrade.Rang = 14;
                            break;
                        case "Мастер": ChainUpgrade.Rang = 15;
                            break;
                        case "Конструктор": ChainUpgrade.Rang = 16;
                            break;
                        case "Инженер": ChainUpgrade.Rang = 17;
                            break;
                        case "Специалист": ChainUpgrade.Rang = 18;
                            break;
                        case "Перфекционист": ChainUpgrade.Rang = 19;
                            break;
                        case "Профессионал": ChainUpgrade.Rang = 20;
                            break;
                        case "Золотые руки": ChainUpgrade.Rang = 21;
                            break;
                        case "Эксперт": ChainUpgrade.Rang = 22;
                            break;
                        case "Рационализатор": ChainUpgrade.Rang = 23;
                            break;
                        case "Левша": ChainUpgrade.Rang = 24;
                            break;
                        case "Ювелир": ChainUpgrade.Rang = 25;
                            break;
                        case "Кулибин": ChainUpgrade.Rang = 26;
                            break;
                        case "Изобретатель": ChainUpgrade.Rang = 27;
                            break;
                        case "Великий изобретатель": ChainUpgrade.Rang = 28;
                            break;
                    }
                    if (ChainUpgrade.Rang >= Settings.FactoryRang) //Выставелнный ранг достигнут, заканчиваем моддинг.
                    { 
                        ChainUpgrade.Stop = true;
                        return;
                    }
                    #endregion                    
                    #region Сбор информации о цепочках в багаже
                    foreach (string ItemId in GetArrClassHtml(MainWB, "$(\"#equipment-accordion img[src$='accessory7.png']\")", "getAttribute(\"data-id\")"))
                    {                        
                        match = Regex.Match(ItemId, "([[](?<MF>([0-9])+)[]])");
                        if ((!match.Success || Convert.ToInt32(match.Groups["MF"].Value) < 3))
                        {
                            Array.Resize<int>(ref ArrUpItems, (ArrUpItems == null ? 1 : ArrUpItems.Count<int>() + 1));
                            ArrUpItems[ArrUpItems.Count<int>() - 1] = Convert.ToInt32(ItemId);
                        }
                        else
                        {
                            Array.Resize<int>(ref ArrSellItems, (ArrSellItems == null ? 1 : ArrSellItems.Count<int>() + 1));
                            ArrSellItems[ArrSellItems.Count<int>() - 1] = Convert.ToInt32(ItemId);
                        }                       
                    }
                    #endregion
                    
                    if (ArrUpItems == null)
                    {
                        UpdateStatus("* " + DateTime.Now + " Пойду-ка я вспомню уроки труда, чтоли ...");
                        bool bRet = false; //Получилось купить хоть одну цепочку?
                        for (int i = 0; i < Settings.FactoryChainCount; i++)
                        {
                            if (BuyItems(MainWB, ShopItems.Chain)) bRet = true;
                            else break; //Не смог купить цепочку? нет смысла пробовать купить ещё  
                        }
                        if (bRet) goto case FactoryAction.UpdateChain; //Новое сканировение багажа, вычисление рангов очков итд...
                        else return; //Не получилось купить не одной цепочки?
                    }
                    else //Есть цепочки готовые к модификации
                    {
                        foreach (int ID in ArrUpItems)
                        {
                            UpdateStatus("# " + DateTime.Now + " Эгэгэй следующая цепочка, следующая, ишь как я разошёлся!");
                            #region Модификации
                            GoToPlace(MainWB, Place.Factory, "/mf-item/" + ID);
                            int CurrMF = Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .compare .mf\").html()"), "(?<=M)([0-9])+").Value);
                            for (int i = CurrMF; i < 3; i++)
                            {
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[0].GetElementsByTagName("Button")[0];
                                match = Regex.Match(HtmlEl.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<");
                                UpdateMyInfo(MainWB);
                                if (Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value) + Settings.minFactoryOre)
                                {
                                    frmMain.InvokeMember(MainWB, HtmlEl, "click");
                                    IsWBComplete(MainWB, 500, 1000);
                                    #region Проверка на получение нового звания, может больше уже и не надо!
                                    ChainUpgrade.Points[0]++;
                                    if (ChainUpgrade.Points[0] == ChainUpgrade.Points[1]) goto case FactoryAction.UpdateChain; //Получено новое звание, необходимо проверить всё заново
                                    #endregion
                                }
                                else break; //Нехватает ресурсов для дальнейших модификаций, выходим!
                            }                            
                            #endregion
                            #region Цепочки проаппаные полностью, подлежашие продаже!
                            if (Convert.ToInt32(Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .compare .mf\").html()"), "(?<=M)([0-9])+").Value) >= 3)
                            {
                                Array.Resize<int>(ref ArrSellItems, (ArrSellItems == null ? 1 : ArrSellItems.Count<int>() + 1));
                                ArrSellItems[ArrSellItems.Count<int>() - 1] = ID;
                            }
                            else break; //на эту уже видимо не хватило ресурсов, не имеет смысл крутить дальше
                            #endregion
                        }
                        if (ArrSellItems != null)
                        {
                            UpdateStatus("* " + DateTime.Now + " Эх, устал я по наковальне стучать, пойду-ка продам мой хлам.");
                            #region Продажа цепочек
                            GoToPlace(MainWB, Place.Shop, "/section/mine");
                            foreach (int ID in ArrSellItems)
                            {
                                Wait(3000, 6000); //Создаём иллюзию поиска ненужных цепочек в багаже перед удалением
                                object[] Args = new object[3] { ID, "/shop/section/mine/", 1 };
                                frmMain.InvokeScript(MainWB, "shopSellItem", Args);  //shopSellItem('118468851', '/shop/section/mine/', 1);
                                IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            }
                            #endregion
                        }              
                    }
                break;
            }            
        }        
        public bool Patrol(PatrolAction PA) //OK
        {
            BugReport("Patrol");

            HtmlElement HtmlEl;                        

            #region Инициализация
            Me.Patrol.Val = 0; //Каравана нет.
            #endregion            
            GoToPlace(MainWB, Place.Alley);
            if (frmMain.GetDocument(MainWB).GetElementById("alley-patrol-button") != null || frmMain.GetDocument(MainWB).GetElementById("leave-patrol-button") != null) //Есть ли ещё время патруля? (По кнопкам начать и улизнуть)
            {
                #region Патруль
                if (frmMain.GetDocument(MainWB).GetElementById("patrolbar") == null) //Я сейчас не патрулирую?
                {
                    switch (PA)
                    {
                        case PatrolAction.Check: return false;
                        case PatrolAction.Patrol:
                            #region Переодевание
                            if (Settings.UseWearSet)
                            {
                                WearSet(MainWB, ArrWearSet, 2);
                                GoToPlace(MainWB, Place.Alley);
                            }
                            #endregion
                            if (frmMain.GetDocument(MainWB).GetElementById("patrolForm").All[1].InnerText.Contains("свои районы")) //Проверка, не малыш ли ещё, могу выбирать раёны?? У малышей "улицы".
                            {
                                #region Поиск нужного для патруля региона!
                                string CurrRegion;
                                string DoRegionID;
                                string[] ArrRegion;
                                string[] MyArrRegion = GetArrClassHtml(MainWB, "$(\".regions-choose ul li\")", "getAttribute(\"data-metro-id\")");
                                #region Инизиализация
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("region");
                                CurrRegion = HtmlEl == null ? "0" : HtmlEl.GetAttribute("value"); //У меня есть хоть один район на выбор?)
                                DoRegionID = CurrRegion;
                                #endregion
                                if (MyArrRegion.Count() != 0) //Вообще есть из чего выбирать?
                                {
                                    #region Матрица регионов для выбранного типа патруля.
                                    #region Info
                                    /*
                                 * Кремлевский
                                 * Звериный
                                 * Вокзальный
                                 * Винно-заводский
                                 * Монеточный
                                 * Небоскреб-сити
                                 * Промышленный
                                 * Телевизионный
                                 * Базарный
                                 * Парковый
                                 * Спальный
                                 * Дворцовый
                                 * Газовый
                                 * Научный
                                 * Причальный
                                 * Водоохранный
                                 * Лосинск
                                 * Внучатово
                                 */
                                    #endregion
                                    switch (Settings.PatrolType)
                                    {
                                        case 0: //Руда 2, 7, 13, 8
                                            ArrRegion = new string[] { "2", "7", "13", "8", MyArrRegion[new Random().Next(0, MyArrRegion.Count())] };
                                            break;
                                        case 1: //Нефть 5, 15, 12
                                            ArrRegion = new string[] { "5", "15", "12", MyArrRegion[new Random().Next(0, MyArrRegion.Count())] };
                                            break;
                                        case 2: //Деньги 6, 16, 11, 17
                                            ArrRegion = new string[] { "6", "16", "11", "17", MyArrRegion[new Random().Next(0, MyArrRegion.Count())] };
                                            break;
                                        case 3: //Жетоны 1, 3, 18, 9
                                            ArrRegion = new string[] { "1", "3", "18", "9", MyArrRegion[new Random().Next(0, MyArrRegion.Count())] };
                                            break;
                                        case 4: //Партбилеты 1, 4, 14, 10
                                            ArrRegion = new string[] { "1", "4", "14", "10", MyArrRegion[new Random().Next(0, MyArrRegion.Count())] };
                                            break;
                                        default:
                                            ArrRegion = new string[] { };
                                            break;
                                    }
                                    #endregion
                                    #region Поиск наиболее выгодного для патруля региона.
                                    for (int i = 0; i < ArrRegion.Count(); i++)
                                    {
                                        if (MyArrRegion.Contains(ArrRegion[i])) { DoRegionID = ArrRegion[i]; break; }
                                    }
                                    #endregion
                                    #region Листание регионов
                                    while (DoRegionID != CurrRegion)
                                    {
                                        frmMain.GetDocument(MainWB).GetElementById("region-choose-arrow-right").InvokeMember("click");
                                        IsWBComplete(MainWB, 300, 1000);
                                        CurrRegion = frmMain.GetDocument(MainWB).GetElementById("region").GetAttribute("value");
                                    }
                                    #endregion                               
                                }                               
                                #endregion
                            }
                            #region Старт патруля
                            HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("time").GetElementsByTagName("option"); //Проверка, есть ли необходимое для действия время.
                            frmMain.GetDocument(MainWB).GetElementById("time").SetAttribute("value", Convert.ToInt32(HC[HC.Count - 1].GetAttribute("value")) >= Settings.PatrolTime ? Settings.PatrolTime.ToString() : HC[HC.Count - 1].GetAttribute("value"));
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("patrolForm"), "submit");
                            #endregion
                            break;
                    }
                }                
                IsWBComplete(MainWB);
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("patrolForm").All["Patrol"];
                TimeSpan TS = new TimeSpan();
                TimeSpan.TryParse(HtmlEl.InnerText, out TS);
                Me.Patrol.LastDT = GetServerTime(MainWB).Add(TS); //Временно запоминаем время окончания патруля
                UpdateStatus("# " + DateTime.Now + " Купил свисток, патрулирую до: " + DateTime.Now.Add(TS).ToString("HH:mm:ss")); //Выводим текст о патруле

                #region Караван
                if (frmMain.GetDocument(MainWB).GetElementById("patrolForm").InnerText.Contains("Ваши действия привлекли уличного мага Девида Блейна."))
                {
                    Me.Patrol.Val = 1; //Караван                    
                    if (Settings.PatrolTime < 30) Wait(TS - new TimeSpan(0,2,0), " Похоже я заметил караванчег, ныряю в зассаду до: ", TimeOutAction.Busy); //За 2 минуты до конца пробуем ограбить караван!
                    else UpdateStatus("# " + DateTime.Now + " Нырнув в засаду, точно просплю! Махмуд, деньги на стол!");
                    #region Переодевание
                    if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 2);
                    #endregion
                    GoToPlace(MainWB, Place.Desert, "/rob", false); //Грабим Караван
                    if (Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/player/")) UpdateStatus("! " + DateTime.Now + " Караван на сегодня отменяется: Админо-верблюды запутали меня в трёх кустах."); //Видимо до это был релогин, кнопка с караваном исчезла=(
                    else //Всё в порядке, грабим караван!
                    {
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("span")[0];
                        Match match = Regex.Match(HtmlEl.InnerText, "([0-9])+");
                        if (match.Success) UpdateStatus("$ " + DateTime.Now + " Выскакиваю из кустов, хватаю одного, второго, третьего верблюда и убегаю прихватив: ", Convert.ToInt32(match.Value)); //Ограбил караван!
                        else UpdateStatus("* " + DateTime.Now + " Выскакиваю из кустов, а там снова кусты, и снова, и снова?!? -Шеф, ну ты понял!"); //Караван упущен
                    }
                    Me.Patrol.Val = 0; //Каравана больше нет.
                }
                #endregion

                //Пока идёт патруль, ожидаем в режиме частичной блокировки UseTimeout
                Wait(Me.Patrol.LastDT - GetServerTime(MainWB), "", TimeOutAction.Busy); //Прождать последние 2 минуты после ограбления или полный патруль, если время патруля не позволяет грабить караваны в конце.
                
                IsTimeout(MainWB, false, true, "", TimeOutAction.Free); //Ожидание окончания патруля + ожидаем в режиме почти полного использования UseTimeout
                Wait(1000, 30000, " Активирована задержка до: ");
                #endregion
                return true;
            }
            Me.Patrol.LastDT = GetServerTime(MainWB);
            Me.Patrol.Stop = true;
            return false;
        }
        public bool Bank(BankAction BA)
        {
            Match match;
            
            GoToPlace(MainWB, Place.Bank);

            switch (BA)
            { 
                case BankAction.Exchange:
                    BugReport("Bank.Exchange");
                    #region Обмен тугриков в руду.
                    UpdateStatus("# " + DateTime.Now + " Ох наличности же у меня собралось, закажу ка я грузовик, отвезу в банк!");
                 ReTry:
                    UpdateMyInfo(MainWB);
                    frmMain.GetDocument(MainWB).GetElementById("ruda_count").InnerText = ((int)(Me.Wallet.Money - Settings.minThimblesMoney) / 750).ToString();
                    string strExchanged = "";
                    foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("button"))
                    {
                        if (H.InnerText.Contains("Обменять — 1 сертификат"))
                        {
                            frmMain.InvokeMember(MainWB, H, "click");
                            IsWBComplete(MainWB, 800, 1800);
                            strExchanged = (string)frmMain.GetJavaVar(MainWB, "$(\"#alert-text .ruda\").text()"); //Считываю полученную руду, если всё прошло успешно!
                            break;
                        }
                    }
                    if (strExchanged == "руды" || strExchanged == "")
                    {
                        UpdateStatus("@ " + DateTime.Now + " Сертификат им понимаешь подавай, тьфу на вас!");
                        //Сертификатов больше нет, но можно докупать?
                        if ((Settings.BuyMonaTicketTooth || Settings.BuyMonaTicketStar) && BuyItems(MainWB, ShopItems.Bank_Ticket)) //Можно покупать сертификаты и удачно приобрёл? - повторить попытку обмена!
                        {
                            GoToPlace(MainWB, Place.Bank);
                            goto ReTry;
                        }
                        else 
                        {
                            Me.Thimbles.BankStartDT = GetServerTime(MainWB).AddHours(2);
                            return false;
                        }
                    }
                    else UpdateStatus("$ " + DateTime.Now + " Порядок - финанс обменян, получено: ", 0, Convert.ToInt32(strExchanged));
                    #endregion
                    break;
                case BankAction.BuySafe:
                    BugReport("Bank.BuySafe");
                    #region Покупка ячейки в банке.
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Ore >= 14)
                    {
                        foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementsByTagName("button"))
                        {
                            if (H.InnerText.Contains("Завести ячейку") && Settings.BuyBankSafe)
                            {
                                UpdateStatus("# " + DateTime.Now + " Пошептавшись с банкиром, обзавёлся банковской-ячейкой.");
                                frmMain.InvokeMember(MainWB, H, "click");
                                IsWBComplete(MainWB);
                                return true; //Ячейка в банке куплена!
                            }
                        }                                                
                    }
                    return false; //Нет руды на покупку ячейки!
                    #endregion
                case BankAction.Deposit:
                    BugReport("Bank.Deposit");
                    #region Вклад/Сбор денег из банковской ячейки.
                    HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementsByTagName("form");
                    match = Regex.Match(HC[0].InnerText, "Ваша ячейка открыта до: (?<Date>([0-9.: ])+)");
                    if (match.Success || Bank(BankAction.BuySafe)) Me.BankDeposit.SafeTillDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                    else 
                    {
                        Me.BankDeposit.SafeTillDT = GetServerTime(MainWB); //Ячейки у меня нет, не проверять больше.
                        return false; //нет ячейки в банке, уходим.
                    }

                Check:
                    UpdateMyInfo(MainWB);
                    if (frmMain.GetDocumentURL(MainWB).EndsWith("/Bank/")) return false;
                    match = Regex.Match((string)frmMain.GetJavaVar(MainWB, "$(\"#content .bank-deposit .now\").text()"), "Сейчас на счету: (?<Money>([0-9,])+)([\\s\\S])*Можно забрать: (?<Date>([0-9.: ])+)");
                    if (match.Success)
                    {
                        Me.BankDeposit.MyMoney = Convert.ToInt32(match.Groups["Money"].Value.Replace(",", ""));
                        Me.BankDeposit.StartDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(new Random().Next(1, 7));
                    }
                    else Me.BankDeposit.MyMoney = 0; //Нет денег в депозите.
                    foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementsByTagName("button"))
                    {
                        if (((H.InnerText.Contains("Сделать вклад на сумму") || H.InnerText.Contains("Забрать")) && Me.Wallet.Money + Me.BankDeposit.MyMoney >= Settings.DepositMoney && Settings.UseBankDeposit) || (H.InnerText.Contains("Забрать") && !Settings.UseBankDeposit))
                        {
                            UpdateStatus("$ " + DateTime.Now + (H.InnerText.Contains("Забрать") ? " Забираю остатки бабла из банка, пока совсем не разворовали!" : " Я в банке, делаю вклад!"));
                            frmMain.InvokeMember(MainWB, H, "click");
                            goto Check;
                        }
                    }                      
                    #endregion
                    break; 
                default: return true;
            }
            return true;  
        }
        public void HunterClub(bool MultiPageSearch = true) //OK
        {
            BugReport("HunterClub");

            Regex regex;
            Match match;
            HtmlElement HtmlEl;
            
            int Lvl, i = 0, iCurrPage, iMaxPage;
            HCThread[0] = new Thread(new ThreadStart(HCStartMultiThread));
            HCThread[0].Name = "HCThread[0]";
            HCThread[1] = new Thread(new ThreadStart(HCStartMultiThread));
            HCThread[1].Name = "HCThread[1]";

            #region Check Bonus
            CheckForDayPrize();
            #endregion

            GoToPlace(MainWB, Place.Huntclub);
            frmMain.NavigateURL(HelpWB[0], Settings.ServerURL + "/alley/");
            IsWBComplete(HelpWB[0]);
            frmMain.NavigateURL(HelpWB[1], Settings.ServerURL + "/alley/");
            IsWBComplete(HelpWB[1]);

            #region Продлить членство?
            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("hunter-activate");
            if (Settings.HCMember) //Необходимо проверять членство?
            {
                regex = new Regex(@"(?<=Вы член клуба до: )([0-9\w :])+"); //Вы член клуба до: 19 марта 2012 18:41
                match = regex.Match(HtmlEl.InnerText);
                DateTime DTMember = match.Success ? Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU")) : GetServerTime(MainWB);

                if (DTMember.AddHours(-1) <= GetServerTime(MainWB)) //Членство в клубе закончилось или заканчивается в ближайший час, продливаем.
                {
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Ore >= 14)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Вношу охотничий взнос, без него ружжо не дають!");
                        frmMain.InvokeMember(MainWB, HtmlEl, "submit");
                        IsWBComplete(MainWB);
                    }
                    else //Блокируем походы в ОК на сегодня.
                    {
                        UpdateStatus("@ " + DateTime.Now + " Черт ... где же деньги на патроны?!? Походу, дома бумажник оставил!");
                        Me.HCHunting.LastDT = GetServerTime(MainWB);
                        Me.HCHunting.Stop = true;
                        return;
                    }
                }
            }
            #endregion

            //Проверяю, не закончились ли на сегодня заказы?
            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("hunter-activate"); //Если продлевал заказ, то необходимо обновить ссылку.
            regex = new Regex("(?<=Можно выполнить заказов: )([0-9])+");
            UpdateStatus("# " + DateTime.Now + " Взял ружьё, иду в ОК пиписками мерятся!");
            Me.HCHunting.Victims = Convert.ToInt32(regex.Match(HtmlEl.InnerText).Value);
            Me.HCHunting.Search = Me.HCHunting.Victims > 0; //Начинаем атаку иль больше не можем?
            if (!Me.HCHunting.Search) //На сегодня уже всё
            {
                Me.HCHunting.Stop = true;
                UpdateStatus("# " + DateTime.Now + " *Пряча хобот в штаны...* -Ах, линейки на сегодня закончились!");
            }
            else Dopings(ref Me.ArrUsualDoping, DopingAction.Check);

            //Инициализация
            DateTime StoptDT = DateTime.Now.AddMinutes(10);
            Me.Player.Level = Convert.ToInt32((string)frmMain.GetJavaVar(MainWB, "player['level']")); //Считываем мой настоящий уровень!
            vsPlayer[0].URL = null;
            vsPlayer[1].URL = null;            

            while (Me.HCHunting.Search)
            {
                frmMain.NavigateURL(MainWB, Settings.ServerURL + "/huntclub/wanted/");
                IsWBComplete(MainWB); //Проверка на клик-клик и прочее.

                iMaxPage = 1; //Инициализация
                i = frmMain.GetDocument(MainWB).GetElementById("content").All.Count;
                if (MultiPageSearch & frmMain.GetDocument(MainWB).GetElementById("content").All[i - 5].GetAttribute("className") == "arrow") //Если 5-ый элемент с конца стрелка -> страничка не одна!
                { iMaxPage = Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("content").All[i - 7].InnerText); } //7-ой эллемент с конца -> кол-во страниц. 
                for (iCurrPage = 1; iCurrPage <= iMaxPage; iCurrPage++)
                {
                    #region Один из 2х Бровзеров свободен?
                    while (vsPlayer[0].URL != null & vsPlayer[1].URL != null) { Thread.Sleep(100); }  //Application.DoEvents(); -> Подставлять когда тестируюсь иначе с Мэйн поток слипит^^
                    #endregion
                    frmMain.NavigateURL(MainWB, Settings.ServerURL + "/huntclub/wanted/page/" + iCurrPage); //Переходим от страничке к страничке...
                    IsWBComplete(MainWB); //Проверка на клик-клик и прочее.                    
                    if (IsTimeout(MainWB, false, false)) { Me.HCHunting.Search = false; return; } //Таймаут или необходимо остановить нападение!
                    if (StoptDT < DateTime.Now && vsPlayer[0].URL == null && vsPlayer[1].URL == null) { UpdateStatus("@ " + DateTime.Now + " Охота утомляет... -сделаю паузу, съем твикс!"); Me.HCHunting.Search = false; UseTimeOut(TimeOutAction.All); return; }
                    if (TimeToStopAtack(NextTimeout.Atack)) { UpdateStatus("@ " + DateTime.Now + " Драка отменяется иначе на встречу опоздаю, а у меня приказ свыше!"); Me.HCHunting.Search = false; return; } //Необходимо остановить нападение!

                    HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("tr");
                    regex = new Regex("href=\"(?<URL>/player/([0-9])+/)\"[>](?<Name>([^<])+).*level\"?>[[](?<Lvl>([0-9])+)");
                    if (HC[2].InnerText != "Подходящих для вас заказов нет.") //Есть ли вообще заказы?
                    {
                        #region Перебор в ОК
                        for (int x = 2; x < HC.Count; x++) //Начиная со 2 элемента идут данные о игроках
                        {
                            if (!Me.HCHunting.Search) break; //Стоп?
                            #region Инициализация
                            match = null;
                            #endregion
                            if (HC[x].GetElementsByTagName("div")[0].InnerText.Contains("Атаковать") ? (match = regex.Match(HC[x].InnerHtml)).Success : false) //Выдирать имена левел и прочее, только при наличии кнопки Атаки!
                            {
                                Lvl = Convert.ToInt32(match.Groups["Lvl"].Value);
                                if (Me.Player.Level + Settings.minHCLvl <= Lvl && Lvl <= Me.Player.Level + Settings.maxHCLvl && !LBBlackWanted.Items.Contains(match.Groups["Name"].Value))
                                {
                                    frmMain.AddListItem(LBBlackWanted, match.Groups["Name"].Value);

                                    #region Один из 2х Бровзеров свободен?
                                    while (vsPlayer[0].URL != null && vsPlayer[1].URL != null) { Application.DoEvents(); } //Thread.Sleep(50); } //Application.DoEvents(); -> Подставлять когда тестируюсь иначе с Мэйн поток слипит^^
                                    #endregion

                                    if (vsPlayer[0].URL == null)
                                    {
                                        vsPlayer[0].URL = Settings.ServerURL + match.Groups["URL"].Value;
                                        if (HCThread[0].ThreadState == ThreadState.Stopped) HCThread[0] = new Thread(new ThreadStart(HCStartMultiThread));
                                        if (HCThread[0].ThreadState == ThreadState.Unstarted) HCThread[0].Start();
                                    }
                                    else if (vsPlayer[1].URL == null)
                                    {
                                        vsPlayer[1].URL = Settings.ServerURL + match.Groups["URL"].Value;
                                        if (HCThread[1].ThreadState == ThreadState.Stopped) HCThread[1] = new Thread(new ThreadStart(HCStartMultiThread));
                                        if (HCThread[1].ThreadState == ThreadState.Unstarted) HCThread[1].Start();
                                    }                                    
                                }
                            }
                        }
                        #endregion
                    }
                }
            }
            Me.HCHunting.LastDT = GetServerTime(MainWB);
        }
        public bool Metro(MetroAction MA)
        {
            HtmlElement HtmlEl;
            Regex regex;
            Match match;

            switch (MA)
            {
                case MetroAction.Dig:
                    BugReport("Metro.Dig");
                    #region Проверяем причиндалы для похода в метро.
                    if (Settings.OpenPrizeBox) CheckForPrizeBox(); //Проверка на наличие ключей и сундуков.
                    else GoToPlace(MainWB, Place.Player);

                    regex = new Regex("(underground)(2|5)[.]png"); //"Кирка|Отбойный молоток"
                    if (!regex.IsMatch(frmMain.GetDocumentHtmlText(MainWB)) & (Settings.BuyMpick || Settings.BuyRpick)) BuyItems(MainWB, ShopItems.Pick); //Покупаем кирку за 1500 тугриков / за 2 руды
                    regex = new Regex("(underground)(1|4)[.]png"); //"Шахтерская каска|Титановая каска"
                    if (!regex.IsMatch(frmMain.GetDocumentText(MainWB)) & Settings.BuyHelmet) BuyItems(MainWB, ShopItems.Helmet); //Покупаем шахтёрскую каску за 500 тугриков
                    regex = new Regex("underground3[.]png"); //"Счетчик Гейгера"
                    if (!regex.IsMatch(frmMain.GetDocumentText(MainWB)) & Settings.BuyCounter) BuyItems(MainWB, ShopItems.Counter); //Покупаем счётчик Гейгера за 500 тугриков
                    #endregion
                    if (!Metro(MetroAction.Check)) //нет ли старого недовыполненого задания?                     
                    {
                        frmMain.InvokeScript(MainWB, "metroWork");
                        IsWBComplete(MainWB); //IsAjaxComplete(MainWB); //Ожидаю пока ajax обновит контекст
                        Metro(MetroAction.Check);
                    }
                    return true; //Бот что-то проделал в метро! (Какое-то прошлое задание)
                case MetroAction.SearchRat:
                    BugReport("Metro.SearchRat");

                    // Settings.ServerURL + "/metro/holidayreset/"; //Резет крысоспуска!
                    Metro(MetroAction.Check); //нет ли старого недовыполненого задания?
                    UpdateStatus("# " + DateTime.Now + " Я у берлоги, Шеф! Холодно тут ..., но я уже согрел штаны со страху.");
                    #region Bonus
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("content-no-rat").GetElementsByTagName("form")[0];
                    if (HtmlEl.GetElementsByTagName("button")[0].GetAttribute("classname") != "button disabled")
                    {
                        UpdateStatus("@ " + DateTime.Now + " Вот хвосты, гоните чемоданчеГ!");                       
                        frmMain.InvokeMember(MainWB, HtmlEl, "submit");
                        IsWBComplete(MainWB);
                    }
                    #endregion
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("timer-rat-fight");                        
                    if (HtmlEl == null)
                    {
                        #region Всё, больше нет крысомах, всех перебил!
                        regex = new Regex("Вы сможете продолжить битву с крысомахами через: (?<Timeout>([0-9:])+)");
                        match = regex.Match(frmMain.GetDocumentText(MainWB));
                        if (match.Success) 
                        {
                            UpdateStatus("@ " + DateTime.Now + " А берлога то пуста, кругом лишь одни трупы ..., вот я проказник ...");
                            Me.RatHunting.RestartDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);
                            Me.RatHunting.NextDT = Me.RatHunting.RestartDT.AddMinutes(1); //Следующее наподение, только после обвала!
                            Me.RatHunting.Lvl = 36;
                            Me.RatHunting.Stop = true;
                            UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ПРОЙДЕН, до обвала осталось: " + match.Groups["Timeout"].Value);
                        }
                        #endregion
                    }
                    else
                    {
                        #region Ещё есть крысомахи
                        regex = new Regex("До обвала осталось (?<Timeout>([0-9:])+)");
                        match = regex.Match(HtmlEl.InnerText);
                        if (match.Success) Me.RatHunting.RestartDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);

                        regex = new Regex("Спуск на (?<RatLvl>([0-9])+) уровень\\s+(?<Timeout>([0-9:])+)?");
                        match = regex.Match(HtmlEl.InnerText);
                        if (match.Groups["Timeout"].Success) Me.RatHunting.NextDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);                       
                        Me.RatHunting.Lvl = Convert.ToInt32(match.Groups["RatLvl"].Value);
                        if (match.Groups["RatLvl"].Success) Me.RatHunting.Stop |= Settings.maxSearchRatLvl < Me.RatHunting.Lvl;

                        #region Синхронизация обвалов крысопровода и ленинопровода.
                        if (Settings.OilLeninSyncRats && Settings.GoOilLenin && Me.RatHunting.Lvl <= 1)
                        {
                            Me.RatHunting.RestartDT = DateTime.Now; //Необходимо если нет иной инфромации о прошлом обвале!
                            if (Me.OilLeninHunting.RestartDT == new DateTime()) Oil(OilAction.LeninFight);
                            if (!IsTimeInTimespan(new TimeSpan(0, 0, (int)Settings.OffsetSyncOilLenin - 30, 0), new TimeSpan(0, 0, (int)Settings.OffsetSyncOilLenin + 30, 0), DateTime.Now.AddHours(24) - Me.OilLeninHunting.RestartDT))
                            {
                                Me.RatHunting.RestartDT = DateTime.Now.Add(Me.OilLeninHunting.RestartDT - DateTime.Now.AddHours(24)).Add(new TimeSpan(0, -(int)Settings.OffsetSyncOilLenin, 0));
                                Me.RatHunting.NextDT = Me.RatHunting.RestartDT;
                                if (Me.RatHunting.NextDT > DateTime.Now) UpdateStatus("@ " + DateTime.Now + " Пытаюсь синхронизировать ленино-крысопровод, скоро буду!");
                            }
                        }
                        #endregion

                        #region Заход на крыс между Макдольнадсами
                        if (Me.MC.Stop == false && Me.RatHunting.Lvl > 10) return false; //Зашел сюда в перерывах Макдональдса, не давать кушать допы, дерёмся только с мелкими крысами!
                        #endregion

                        #region Быстрый спуск
                        if (Settings.UseRatFastSearch && Settings.RatFastSearch <= Me.RatHunting.Lvl && match.Groups["Timeout"].Success)
                        {
                            if (!Settings.RatFastSearchHoney || (Settings.RatFastSearchHoney && Me.Wallet.Honey >= 35 - Me.RatHunting.Lvl))
                            {
                                frmMain.InvokeScript(MainWB, "elevatorToRatBy" + (Settings.RatFastSearchHoney ? "Honey" : "HuntclubBadge")); //elevatorToRatByHuntclubBadge()
                                IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                #region Допинг + Переодевание + Карманы!
                                IsTimeout(MainWB, true, false); //Не даём сьесть допинги, если пользователь успел записаться в драку например. (сначала драка, затем вернёмся перекусить и напасть на крысу.)                        
                                if (!Dopings(ref Me.ArrRatDoping, DopingAction.Check))
                                {
                                    UpdateStatus("@ " + DateTime.Now + " Трезвым я против Крысомахи[" + Me.RatHunting.Lvl + "] не пойду, схожу лучше штаны простирну!");
                                    Me.RatHunting.Stop = true;
                                    UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ОСТАНОВЛЕН на уровне " + Me.RatHunting.Lvl + ": нет нужных допингов");
                                    return false;
                                }
                                if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, Me.RatHunting.Lvl % 5 == 0 ? 4 : 1); //Одеваем крысиный сет!
                                if (Settings.UseAutoFightBagSlots && Me.RatHunting.Lvl % 5 == 0) CheckBagFightItems(GroupFightType.Rat);                      
                                #endregion
                                if (IsHPLow(MainWB, 100) ? (HealMePlus() ? true : CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100)) : true) //Лечить в любом варианте до 100%
                                {
                                    if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/metro/")) GoToPlace(MainWB, Place.Metro); //Оказался не в метро? Ел допинг или был в драке?
                                    frmMain.InvokeScript(MainWB, "metroTrackRat");
                                    IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                    return Metro(MetroAction.Check);
                                }
                                else return false;
                            }
                            else //Нехватит мёда пробежать всех крыс?
                            {
                                Me.RatHunting.Stop = true;
                                UpdateStatus("! " + DateTime.Now + " Шеф, мёда до финиша у нас нехватает, я бросаю это гиблое дело!");
                                UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ОСТАНОВЛЕН на уровне " + Me.RatHunting.Lvl + ": не хватает меда");
                                return false;
                            }
                        }
                        #endregion

                        //Прибежал слишком рано, но если не дождусь пролечу следующую возможность
                        if (TimeToStopAtack(NextTimeout.Atack, StopTimeoutType.RatHunting)) Wait(Me.RatHunting.NextDT - DateTime.Now.AddSeconds(20 + (Settings.UseWearSet ? 25 : 0)), " Вот блин, обед у них! А по моим соломенным ..., ладно, прогуляюсь до: ", TimeOutAction.NoTask); //20 секунд резервируем под поедание допингов и 25 секунд на переодевание.
                        #endregion
                    }
                    if (Me.RatHunting.NextDT <= DateTime.Now.AddSeconds(20 + (Settings.UseWearSet ? 25 : 0)) && !Me.RatHunting.Stop) //20 секунд резервируем под поедание допингов и 25 секунд на переодевание.
                    {
                        #region Допинг + Переодевание
                        IsTimeout(MainWB, true, false); //Не даём сьесть допинги, если пользователь успел записаться в драку например. (сначала драка, затем вернёмся перекусить и напасть на крысу.)                        
                        if (!Dopings(ref Me.ArrRatDoping, DopingAction.Check))
                        {
                            UpdateStatus("@ " + DateTime.Now + " Трезвым я против Крысомахи[" + Me.RatHunting.Lvl + "] не пойду, схожу лучше штаны простирну!");
                            Me.RatHunting.Stop = true;
                            UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ОСТАНОВЛЕН на уровне " + Me.RatHunting.Lvl + ": нет нужных допингов");
                            return false;
                        }
                        if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, Me.RatHunting.Lvl % 5 == 0 ? 4 : 1); //Одеваем крысиный сет!
                        if (Settings.UseAutoFightBagSlots && Me.RatHunting.Lvl % 5 == 0) CheckBagFightItems(GroupFightType.Rat);                       
                        if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/metro/")) GoToPlace(MainWB, Place.Metro);
                        Wait(Me.RatHunting.NextDT - DateTime.Now, " Межуюсь у входа до: "); //Ожидаем до возможности начать бой
                        #endregion
                        if (IsHPLow(MainWB, 100) ? (HealMePlus() ? true : CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100)) : true) //Лечить в любом варианте до 100%
                        {
                            if (!Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/metro/")) GoToPlace(MainWB, Place.Metro);
                            frmMain.InvokeScript(MainWB, "metroTrackRat");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            Metro(MetroAction.Check);
                        }
                        else return false;
                    }                    
                    return true; //Бот что-то проделал в метро!
                case MetroAction.Game:
                    BugReport("Metro.Game");
                    if (!Metro(MetroAction.Check))
                    {
                        #region Игра с Моней в напёрстки
                        UpdateStatus("# " + DateTime.Now + (Me.Wanted ? " Сломя шею ломлюсь к Моне сливать бабло." : " Наведаюсь ка я к моему старому другу, Моне ..."));
                        regex = new Regex("(?<=Встреч с Моней на сегодня: )[0-9]");
                        Me.Thimbles.Val = Convert.ToInt32(regex.Match(frmMain.GetDocumentText(MainWB)).Value);
                        if (Me.Thimbles.Val != 0 || Settings.UseThimblesTicket)
                        {
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/thimble/start/");
                            Me.Thimbles.Stop = (!Metro(MetroAction.Check) && Me.Thimbles.Val == 0) ? true : false; //Билетиков к Моне больше нет?
                            if (Me.Thimbles.Stop && (Settings.BuyMonaTicketTooth || Settings.BuyMonaTicketStar)) //Билетиков к Моне больше нет, но можно докупать!
                            {
                                UpdateStatus("@ " + DateTime.Now + " Уважаемый, не с места! Я, только мигом за билетом слетаю!");
                                if (BuyItems(MainWB, ShopItems.Mona_Ticket)) //Пробуем раздобыть билетик
                                {
                                    GoToPlace(MainWB, Place.Metro);
                                    frmMain.NavigateURL(MainWB, Settings.ServerURL + "/thimble/start/");
                                    Me.Thimbles.Stop = (!Metro(MetroAction.Check) & Me.Thimbles.Val == 0) ? true : false; //Билетиков к Моне больше нет?
                                }
                            }
                            if (Me.Thimbles.Stop) UpdateStatus("! " + DateTime.Now + " Кассы закрыты, билеты распроданы, прячу деньги в трусы!");
                        }
                        else
                        {
                            UpdateStatus("! " + DateTime.Now + " Походы к моне закончились, по билетам нелзья, прячу деньги в трусы!");
                            Me.Thimbles.Stop = true;
                        }
                        Me.Thimbles.LastDT = GetServerTime(MainWB); //Запоминаем время последнего похода к Моне.
                        #endregion
                    }
                    return true; //Бот что-то проделал в метро! (Какое-то прошлое задание)
                case MetroAction.Check:
                    BugReport("Metro.Check");
                    regex = new Regex("(/metro/)|(/thimble/)");
                    IsWBComplete(MainWB, 500, 1000); //################2000, 3000
                    if (!regex.IsMatch(frmMain.GetDocumentURL(MainWB))) GoToPlace(MainWB, Place.Metro);
                    switch (regex.Match(frmMain.GetDocumentURL(MainWB)).Value)
                    {
                        case "/metro/": //Копание, крысомахи
                            #region Копание + крысомахи
                            {
                                if (Regex.IsMatch(frmMain.GetDocument(MainWB).GetElementById("welcome-no-rat").Style, "display: block", RegexOptions.IgnoreCase)) //Копание или пусто
                                {
                                    #region Копание шаг 1
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("metrodig"); //Метро шаг 1
                                    if (HtmlEl != null)
                                    {
                                        TimeSpan TS = TimeSpan.Parse(HtmlEl.InnerText);
                                        
                                        if (GetServerTime(MainWB) - Me.Rat.LastDT <= new TimeSpan(1, 0, 0)) //Автолечение перед крысой.
                                        {
                                            Wait(TS.Subtract(new TimeSpan(0, 0, 25)), " Начал раскопку, ожидаем до: "); //За 25 секунд до конца отправим бота проверить жизни...
                                            UpdateStatus("@ " + DateTime.Now + " Побежал проверю жизни, похоже будет драка...");
                                            CheckHealthEx(99, Settings.HealMe100, Settings.HealPet50, Settings.HealPet100, true); //Востанавливаемся перед крысой!
                                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/metro");
                                            IsWBComplete(MainWB);
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("metrodig");
                                            TS = HtmlEl == null ? new TimeSpan() : TimeSpan.Parse(HtmlEl.InnerText);
                                            Wait(TS.Add(new TimeSpan(0, 0, 5)), " Жизни проверил, пора отдышаться! Лузгаю семечки до: ", TimeOutAction.Blocked);
                                        }
                                        else
                                        {
                                            Wait(TS, " Начал раскопку, ожидаем до: ");
                                            Wait(500, 60000, " Активирована задержка до: ");
                                        }
                                    }
                                    IsWBComplete(MainWB);
                                    #endregion

                                    #region Обновляем страничку в метро ибо во время таймаута мог утопать в иное место!
                                    GoToPlace(MainWB, Place.Metro);
                                    #endregion
                                 
                                    #region Копание шаг 2
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("content-no-rat");
                                    if (HtmlEl != null)
                                    {   //regex используется для контроля: "не покинул ли бот метро", посему не перезаписываем эту переменную!
                                        if (Regex.IsMatch(HtmlEl.InnerText, "(?<=Шанс найти руду в этом месте: )([0-9])+[%]")) //GetElementById("ore_chance") пропадает при обновлении странички, обходим на всякий случай
                                        {   //0->Искать дальше, 1->Копать, 2->Выползти наружу
                                            if (HtmlEl.GetElementsByTagName("button").Count >= 3)
                                            {
                                                foreach (HtmlElement H in HtmlEl.GetElementsByTagName("button")) //Хрен знает по какой причине кнопка копать бывает на 1 и на 2 месте ...
                                                {
                                                    if (H.InnerText.Contains("Копать здесь"))
                                                    {
                                                        frmMain.InvokeMember(MainWB, H, "onclick"); //Альтернатива frmMain.InvokeScript(MainWB, "metroDig");
                                                        IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                                        break;
                                                    }
                                                }
                                                match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("content").InnerText, "(?<=Раскопка была успешной, вы нашли )[0-9]");
                                                UpdateStatus("* " + DateTime.Now + (match.Success ? " Нашёл руды: " : " Не нашёл руды."), 0, match.Success ? Convert.ToInt32(match.Value) : 0);
                                                //Использовать таймаут только, когда я уже прошел процедуры определения собственных параметров (Рестарт инстанции бота)
                                                if (IsTimeout(MainWB, false, false) & Me.Player.Name != null) //Проверяем нет ли таймаута, нет -> без ожидания продолжить.
                                                {
                                                    IsTimeout(MainWB, false, true, " Таймаут до: ", TimeOutAction.NoTask);
                                                    if (regex.IsMatch(frmMain.GetDocumentURL(MainWB))) Wait(500, 60000, " Активирована задержка до: "); //Весь таймаут провел в метро? симулируем ожидание!
                                                }
                                                return true;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                #region Крысомахи
                                if (Regex.IsMatch(frmMain.GetDocument(MainWB).GetElementById("welcome-rat").Style, "display: block", RegexOptions.IgnoreCase)) //Крысомахи
                                {
                                    int RatLvl;
                                    bool RatDig;
                                    bool Prize;
                                    bool Attack = false;
                                   
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("ratlevel");
                                    if (HtmlEl != null)
                                    {
                                        #region Крыса из копания
                                        RatDig = true;
                                        RatLvl = Convert.ToInt32(HtmlEl.InnerText);
                                        Attack = Settings.AttackRat && Settings.maxRatLvl >= RatLvl && !Me.Rat.Stop && !TimeToStopAtack(NextTimeout.Atack, StopTimeoutType.RatHunting) && !TimeToStopAtack(NextTimeout.Atack, StopTimeoutType.OilLenin);
                                        Me.Rat.LastDT = GetServerTime(MainWB);
                                        if (RatLvl > Settings.maxRatLvl) Me.Rat.Val = Convert.ToInt32(Settings.maxRatDefeats); //Нет смысла дальше копать и убегать от крысомах, пошли слишком большие и сильные.
                                        #endregion
                                    }
                                    else
                                    {
                                        #region Королева Крысомах + Крыса/ы из охоты
                                        RatDig = false;
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("welcome-rat");
                                        
                                        MatchCollection matches = Regex.Matches(HtmlEl.InnerText, "(Крысомаха[[](?<Lvl>([0-9])+)[]])|(Королева Крысомах)");
                                        if (matches.Count == 1 && matches[0].Value == "Королева Крысомах")
                                        {
                                            #region Королева Крысомах
                                            RatLvl = 99;
                                            UpdateStatus("@ " + DateTime.Now + " Ооообоже, это что ещё за огромная тварюка? Пора делать ноги!");
                                            Attack = false;
                                            #endregion
                                        }
                                        else
                                        {
                                            #region Крыса/ы из охоты
                                            #region Вычесление текущего уровня спуска в метро!
                                            if (matches.Count == 1) RatLvl = Convert.ToInt32(matches[0].Groups["Lvl"].Value);
                                            else //Груповой бой
                                            {
                                                switch (matches[0].Groups["Lvl"].Value)
                                                {
                                                    default: RatLvl = 5; break; //5
                                                    case "6": RatLvl = 10; break;
                                                    case "7": RatLvl = 15; break;
                                                    case "10": RatLvl = 20; break;
                                                    case "11": RatLvl = 25; break;
                                                    case "12": RatLvl = 30; break;
                                                    case "15": RatLvl = 35; break;
                                                }
                                            }
                                            #endregion
                                            #region Проверка, есть ли полезности?
                                            Prize = false; //Инициализация
                                            if (((RatLvl % 5 != 0 && !Settings.SearchRatLeaveNoKey) || (RatLvl % 5 == 0 && (RatLvl >= 20 || !Settings.SearchRatRobinHood))) && !Settings.SearchRatLeaveNoElement && !Settings.SearchRatLeaveNoBox && (RatLvl < 35 || !Settings.SearchRatBambula)) Prize = true;
                                            else
                                            {
                                                foreach (HtmlElement H in HtmlEl.GetElementsByTagName("IMG"))
                                                {
                                                    Prize = (Settings.SearchRatLeaveNoKey || RatLvl % 5 == 0 & RatLvl <= 15) && H.GetAttribute("src").Contains("box_metro_key");
                                                    Prize |= Settings.SearchRatLeaveNoElement && H.GetAttribute("src").Contains("collections/54");
                                                    Prize |= Settings.SearchRatLeaveNoBox && H.GetAttribute("src").Contains("box_metro.png"); //нужно, ибо иначе вместо сундуков может и ключи распознавать!
                                                    Prize |= Settings.SearchRatBambula && H.GetAttribute("src").Contains("trainer_weight");
                                                    if ((RatLvl % 5 == 0 && RatLvl <= 15 && Settings.SearchRatRobinHood && !H.GetAttribute("src").Contains("box_metro_key")) //Проходить стенки (до 15 включительно), только если за них дадут ключик! 
                                                        || (RatLvl == 35 && Settings.SearchRatBambula && !H.GetAttribute("src").Contains("trainer_weight") && Me.RatHunting.RestartDT > DateTime.Now.AddMinutes(30)) //Проходить только если найдена гиря.
                                                        ) Prize = false; 
                                                    if (Prize) break;
                                                }
                                            }
                                            #endregion
                                            Attack = Settings.SearchRat & Settings.maxSearchRatLvl >= RatLvl & !Me.RatHunting.Stop;
                                            if (Attack && !Prize) UpdateStatus("@ " + DateTime.Now + " Эх, тяжела жизнь \"Робингуда\", живи нищая Крысюлька.");
                                            Attack &= Prize; //В принципе всё ОК, для нападения есть ли приз?
                                            #endregion
                                        }                                        
                                        #endregion
                                        #region Запасное переодевание
                                        if (Attack && Settings.UseWearSet) WearSet(MainWB, ArrWearSet, Me.RatHunting.Lvl % 5 == 0 ? 4 : 1); //Одеваем крысиный сет!
                                        if (Settings.UseAutoFightBagSlots && Me.RatHunting.Lvl % 5 == 0 && frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) CheckBagFightItems(GroupFightType.Rat); //Раз уж был неверный сэт значит и карманы ещё не проверял до сели!
                                        if (!regex.IsMatch(frmMain.GetDocumentURL(MainWB))) GoToPlace(MainWB, Place.Metro);
                                        #endregion
                                    }
                                    
                                    if (Attack && !Me.Trauma.Stop)
                                    {
                                        if (RatDig)
                                        {
                                            IsTimeout(MainWB, false, true, " Вцепился в шею Крысомахи[" + RatLvl + "], таймаут до: ", TimeOutAction.Blocked); //Ожидаем окончания времени с последней драки.
                                            Wait(1000, 3000, " Активирована задержка до: ");                                            
                                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/metro"); //Обновляю страничку перед атакой, вдруг на меня кто успел напасть? 
                                            IsWBComplete(MainWB);
                                        }
                                        else UpdateStatus("# " + DateTime.Now + " Всем к стене, снять штаны и нагнуться! Я погнал на [" + RatLvl + "] уровень метро!");
                                        
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("welcome-rat");
                                        if (HtmlEl.GetElementsByTagName("button").Count >= 2)
                                        {
                                            if (IsHPLow(MainWB, RatDig ? 70 + RatLvl / Settings.maxRatLvl * 30 : 99) ? !HealMePlus() : false) //Мало жизней? Пробуем подлечиться.
                                            {
                                                #region Слишком мало жизней, лечение не удалось?
                                                if (IsHPLow(MainWB, 70 + RatLvl / Settings.maxRatLvl * 30 - 15)) //Недостаёт более 15% до минимально необходимых для драки жизней.
                                                {
                                                    UpdateStatus("#" + DateTime.Now + " Похоже на нас напал какойто блядун! Удираем от Крысомахи[" + RatLvl + "].");
                                                    //0->Напасть на монстра, 1->Мужественно убежать
                                                    frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[1], "onclick"); //Альтернатива frmMain.InvokeScript(MainWB, "metroLeave2");
                                                    IsWBComplete(MainWB, 500, 1000);
                                                    return true;
                                                }
                                                #endregion
                                                IsHPLow(MainWB, 70 + RatLvl / Settings.maxRatLvl * 30, true); //Ожидаем востановления ХП, ибо выпить не удастся (70% + до 30%, от уровня крысы)                                                   
                                            };
                                            #region Определяем ресурсы, которые получим за крысу после победы!
                                            string[] Resource = { (string)frmMain.GetJavaVar(MainWB, "$(\"#welcome-rat .tugriki\").text()"), (string)frmMain.GetJavaVar(MainWB, "$(\"#welcome-rat .ruda\").text()") };
                                            #endregion
                                            //0->Напасть на монстра, 1->Мужественно убежать
                                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("welcome-rat").GetElementsByTagName("button")[0], "onclick"); //Нападаем на крысомаху (Не через HtmlEl ибо, во время ожидания востановления жизней пользователь мог чегото наклацать...)
                                            if (!AnalyseFight(MainWB, Resource[0].Equals("") ? 0 : Convert.ToInt32(Resource[0]), Resource[1].Equals("") ? 0 : Convert.ToInt32(Resource[1]))) //При атаке крысы из охоты передаем ресурсы которые получим в случае победы.
                                            {
                                                if (RatDig) Me.Rat.Val++; //Если последняя крыса которая меня побила была более часа назад, скинуть счётчик крыс.
                                                else Me.RatHunting.Defeats += Settings.SearchRatUseOhara ? -Me.RatHunting.Defeats : 1; //Останавливаем побеги в метрополитен, если только не просили усердно наезжать Охарой.                                                                     
                                            }
                                        }
                                    }
                                    else //Убегаем от крысомахи 
                                    {
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("welcome-rat");
                                        if (HtmlEl.GetElementsByTagName("button").Count >= 2)
                                        {
                                            UpdateStatus("# " + DateTime.Now + (RatDig ? " Удираем от Крысомахи[" + RatLvl + "]." : " Вылажу с [" + RatLvl + "] уровня метро."));
                                            //0->Напасть на монстра, 1->Мужественно убежать
                                            frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[1], "onclick"); //Альтернатива frmMain.InvokeScript(MainWB, "metroLeave2");                                            
                                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                        }
                                        IsTimeout(MainWB, false, Me.Player.Name != null, " Таймаут до: ", TimeOutAction.NoTask); //Использовать таймаут только, когда я уже прошел процедуры определения собственных параметров (Рестарт инстанции бота)
                                    }
                                    #region Подводим итоги
                                    if (RatDig)
                                    {
                                        //Слишком много крыс которые нас побили / от которых мы удрали, стоп охота на крыс, стоп копание в метро!
                                        if (Me.Rat.Val >= Settings.maxRatDefeats || !Settings.AttackRat) Me.Rat.Stop = true;
                                    }
                                    else //Крысомахи из поиска
                                    {
                                        //Слишком много крыс которые нас побили, стоп поиск крыс!
                                        if ((Me.RatHunting.Defeats >= Settings.maxSearchRatDefeats) || !Settings.SearchRat)
                                        {
                                            Me.RatHunting.Stop = true;
                                            UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ОСТАНОВЛЕН на уровне " + RatLvl + ": слишком много поражений");
                                        }

                                        GoToPlace(MainWB, Place.Metro);
                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("timer-rat-fight");
                                        if (HtmlEl == null)
                                        {
                                            #region Всё, больше нет крысомах, всех перебил!
                                            regex = new Regex("Вы сможете продолжить битву с крысомахами через: (?<Timeout>([0-9:])+)");
                                            match = regex.Match(frmMain.GetDocumentText(MainWB));
                                            if (match.Success)
                                            {
                                                Me.RatHunting.RestartDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);
                                                Me.RatHunting.NextDT = Me.RatHunting.RestartDT.AddMinutes(1); //Следующее наподение, только после обвала!
                                                Me.RatHunting.Lvl = 36;
                                                Me.RatHunting.Stop = true;
                                                UpdateStatus("# " + DateTime.Now + " КРЫСОПРОВОД ПРОЙДЕН, до обвала осталось: " + match.Groups["Timeout"].Value);
                                            }
                                            #endregion
                                        }
                                        else
                                        {
                                            #region Ещё есть крысомахи
                                            regex = new Regex("До обвала осталось (?<Timeout>([0-9:])+)");
                                            match = regex.Match(HtmlEl.InnerText);
                                            if (match.Success) Me.RatHunting.RestartDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);

                                            regex = new Regex("Спуск на (?<RatLvl>([0-9])+) уровень\\s+(?<Timeout>([0-9:])+)?");
                                            match = regex.Match(HtmlEl.InnerText);
                                            if (match.Groups["Timeout"].Success) Me.RatHunting.NextDT = DateTime.Now + TimeSpan.Parse(match.Groups["Timeout"].Value);
                                            if (match.Groups["RatLvl"].Success) Me.RatHunting.Lvl = Convert.ToInt32(match.Groups["RatLvl"].Value);
                                            Me.RatHunting.Stop |= Settings.maxSearchRatLvl < Me.RatHunting.Lvl;
                                            #region Пора производить быстрый спуск?
                                            if (!Me.RatHunting.Stop && Settings.UseRatFastSearch && Settings.RatFastSearch <= Me.RatHunting.Lvl) Metro(MetroAction.SearchRat);
                                            #endregion                                   
                                            #endregion
                                        }                                                                                
                                    }
                                    #endregion
                                    return true;
                                }
                                #endregion                                
                            }
                            #endregion
                            return false;
                        case "/thimble/": //Игра в наперстки с моней
                            #region Напёрстки
                            {
                                int ThimbleID;

                            RePlay:
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("naperstki-left");
                                if (HtmlEl != null)
                                {
                                    while (frmMain.GetDocument(MainWB).GetElementById("naperstki-left").InnerText != "0") //Остались ли ешё попытки для игры?
                                    {                                        
                                        do //Выискиваем неоткрытый до сели напёрсток!
                                        {   //Во сколько напёрстков играем?                                            
                                            ThimbleID = new Random().Next(0, frmMain.GetDocument(MainWB).GetElementById("thimble8") == null ? 2 : 9);
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("thimble" + ThimbleID);
                                        } while (HtmlEl.GetAttribute("ClassName") != "icon thimble-closed-active"); //Напёрсток ещё не открыт                                        

                                        DateTime MonitorDT = DateTime.Now.AddSeconds(30);
                                        do //Открываем, пока не подтвердится открытие!
                                        {
                                            #region Мониторинг для напёрстков
                                            if (MonitorDT < DateTime.Now)
                                            {
                                                UpdateStatus("! " + DateTime.Now + " Эй шайтан, я твоя труба шатал, напёрсток то дай выбрать!");
                                                MonitorDT = DateTime.Now.AddSeconds(30);
                                                frmMain.NavigateURL(MainWB, Settings.ServerURL + "/player/"); //Рефреш не помогает, нужно более глобальное обновление странички!                                                
                                                IsWBComplete(MainWB);
                                            }
                                            #endregion
                                            if (frmMain.GetDocument(MainWB) != null) frmMain.GetDocument(MainWB).GetElementById("thimble" + ThimbleID).InvokeMember("click");                                       
                                            IsWBComplete(MainWB, 300, 700);
                                        } while (frmMain.GetDocument(MainWB).GetElementById("thimble" + ThimbleID).GetAttribute("ClassName") == "icon thimble-closed-active");
                                    }
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("naperstki-ruda");
                                    UpdateStatus("$ " + DateTime.Now + (HtmlEl.InnerText != "0" ? " Угадано: " : " Ничего не угадал. "), 0, Convert.ToInt32(HtmlEl.InnerText));
                                }
                                UpdateMyInfo(MainWB);
                                decimal Money = Me.Wallet.Money - Settings.minThimblesMoney;
                                int PlayThimbles = Money > 1500 ? 9 : (Money > 500 ? 2 : 0); //Выбор игры у мони
                                switch (PlayThimbles)
                                {
                                    case 2:
                                    case 9:
                                        if (Settings.UseThimblesTrick & !Me.Thimbles.Detected) //Лавочку закрыли?
                                        #region Мухлёж у Мони
                                        {
                                            HttpWebRequest webReq;
                                            HttpWebResponse webResp;
                                            string URL = "http://" + Settings.ServerURL + "/";
                                            uint CookieSize = 1024;
                                            StringBuilder CookieData = new StringBuilder((int)CookieSize);
                                            CookieContainer CookieCont = new CookieContainer();

                                            InternetGetCookieEx(URL, null, CookieData, ref CookieSize, INTERNET_COOKIE_HTTPONLY, IntPtr.Zero);
                                            CookieCont.SetCookies(new Uri(URL), CookieData.ToString().Replace(';', ','));
                                            System.Net.ServicePointManager.Expect100Continue = false; //Убираем эту хрень в ПОСТ запросах

                                            webReq = (HttpWebRequest)HttpWebRequest.Create(URL + "thimble/play/" + PlayThimbles + "/0/");
                                            webReq.Method = "POST";
                                            webReq.Credentials = CredentialCache.DefaultCredentials;
                                            webReq.Headers.Add("Pragma: no-cache");
                                            webReq.Accept = "application/json, text/javascript, */*; q=0.01";
                                            webReq.Headers.Add("Accept-Encoding: gzip, deflate");
                                            webReq.Headers.Add("Accept-Language: " + CultureInfo.CurrentCulture.IetfLanguageTag);
                                            webReq.ContentLength = 6; //ajax=1
                                            webReq.ContentType = "application/x-www-form-urlencoded";
                                            //webReq.Proxy = Settings.Proxy; 
                                            webReq.AllowAutoRedirect = true;
                                            webReq.Referer = URL + "thimble/";
                                            webReq.Headers.Add("X-Requested-With: XMLHttpRequest");
                                            webReq.CookieContainer = CookieCont;
                                            byte[] PostDataBytes = Encoding.UTF8.GetBytes("ajax=1");
                                            webReq.UserAgent = (string)frmMain.GetJavaVar(MainWB, "navigator['userAgent']");
                                            webReq.GetRequestStream().Write(PostDataBytes, 0, PostDataBytes.Length);
                                            webResp = (HttpWebResponse)webReq.GetResponse();
                                            string response = new StreamReader(webResp.GetResponseStream(), Encoding.GetEncoding(1251)).ReadToEnd();
                                            if (response != null)
                                            {
                                                //{"alerts":false,"wallet":{"money":4698,"ore":"3033","honey":114,"oil":"71398"},"guesses":3,"info":{"d":[{"r":0,"s":0},{"r":1,"s":0},{"r":1,"s":0},{"r":1,"s":0},{"r":0,"s":0},{"r":1,"s":0},{"r":1,"s":0},{"r":1,"s":0},{"r":0,"s":0}],"g":0,"r":0,"m":1},"error":0}
                                                regex = new Regex("(?<={\"r\":)[0-9]");
                                                MatchCollection matches = regex.Matches(response);
                                                Array.Resize<string>(ref Me.Thimbles.Matrix, matches.Count);
                                                for (int i = 0; i < matches.Count; i++) Me.Thimbles.Matrix[i] = matches[i].Value; //Заполняем напёрсточную-матрицу                                                
                                                Me.Thimbles.Detected = (matches.Count != PlayThimbles); //В ответе сервера не найден массив ответов, или их неверное число? похоже лавочку закрыли, не светиться!
                                                frmMain.RefreshURL(MainWB, Settings.ServerURL);
                                            }
                                        }
                                        #endregion //Лавочку закрыли!
                                        else frmMain.NavigateURL(MainWB, Settings.ServerURL + "/thimble/play/" + PlayThimbles + "/"); 
                                        IsWBComplete(MainWB, 300, 700);
                                        goto RePlay;
                                    default:
                                        frmMain.NavigateURL(MainWB, Settings.ServerURL + "/thimble/leave/");
                                        IsWBComplete(MainWB, 2000, 3000);
                                        break;
                                }                                
                            }
                            #endregion
                            return true;
                        default: return false;
                    }
            }
            return false;
        } //OK       
        public void Petarena(PetAction PA)
        {
            BugReport("Petarena");

            Regex regex;
            Match match;
            MatchCollection matches;
            HtmlElement HtmlEl;
            int Offset = 0;
            int MaxStat = 0;
            int UsePetType = 0;  

            switch (PA)
            {
                case PetAction.SetWarPet:
                case PetAction.TrainWarPet:
                    #region Установка/Тренировка боевого пэта
                    GoToPlace(MainWB, Place.Petarena);
                    regex = new Regex("(/obj/pets/(?<PetType>([0-9])+)-[0-9].png)([^'])+[']/petarena(?<URL>/train/([0-9])+)/"); //.*/r/n.*/petarena(?<URL>/train/([0-9])*.)/
                    matches = regex.Matches(frmMain.GetElementsById(MainWB, "equipment-accordion")[0].InnerHtml); //0-> Боевые питомцы, 1-> Беговые питомцы
                    #region Определение типа боевого питомца
                    UsePetType = getPetInformation(stcPetType.War).type;
                    MaxStat = getPetInformation(stcPetType.War).maxState;
                    #endregion
                    foreach (Match m in matches)
                    {
                        if (m.Groups["PetType"].Value.Equals(UsePetType))
                        {
                            GoToPlace(MainWB, Place.Petarena, m.Groups["URL"].Value); //Переходим в тренажерный зал выбранного питомца.
                            break; //Нужный питомец уже найден!
                        }    
                    }
                    #region Нет или не найден нужный питомец?
                    if (frmMain.GetDocumentURL(MainWB).EndsWith("/petarena/"))
                    {
                        if (PA == PetAction.SetWarPet) Me.Events.NextSetWarPetDT = GetServerTime(MainWB).AddDays(1); //Невозможно установить этого питомца как основного
                        if (PA == PetAction.TrainWarPet) Me.WarPet.TrainTimeOutDT = GetServerTime(MainWB).AddHours(2);
                        return;
                    }
                    #endregion
                    #region У питомца ещё травма, тренировки отменяются.
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("restore");
                    if (HtmlEl != null)
                    {
                        string[] Time = HtmlEl.InnerText.Split(':'); //HH:mm:ss
                        if (PA == PetAction.SetWarPet) 
                        {
                            UpdateStatus("@ " + DateTime.Now + " Фигасе, моего пушистика крокодилы похоже покусали, пускай пока отдохнёт!");
                            Me.Events.NextSetWarPetDT = GetServerTime(MainWB).Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.TimeSpan TS = TimeSpan.ParseExact(HtmlEl.InnerText, "hh:mm:ss", CultureInfo.CreateSpecificCulture("ru-RU"));
                        }
                        if (PA == PetAction.TrainWarPet) 
                        {
                            UpdateStatus("@ " + DateTime.Now + " Фигасе, моего пушистика крокодилы похоже покусали, тренировки откладываются!");
                            Me.WarPet.TrainTimeOutDT = GetServerTime(MainWB).Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.TimeSpan TS = TimeSpan.ParseExact(HtmlEl.InnerText, "hh:mm:ss", CultureInfo.CreateSpecificCulture("ru-RU"));                    
                        }                        
                        return;
                    }
                    #endregion
                    #region Устанавливаем боевого питомца
                    if (PA == PetAction.SetWarPet)
                    {
                        UpdateStatus("# " + DateTime.Now + " Вуаля, мой четвероногий снова со мной, теперь я спокоен, надеюсь шеф не заметил!");
                        frmMain.InvokeScript(MainWB, "petarenaSetActive", new object[] { Regex.Match(frmMain.GetDocumentURL(MainWB), "([0-9])+").Value, "'battle'"});
                        IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                    }
                    #endregion
                    #region Тренировка боевого пэта
                    if (PA == PetAction.TrainWarPet)
                    {
                        regex = new Regex("tugriki\"?>(?<Money>([0-9,])+)?([^=])+(=\"?ruda\"?>(?<Ore>([0-9,])+))?([^=])+(=\"?neft\"?>(?<Oil>([0-9,])+))?"); //Выдираем ресурсы необходимые под прокачку
                    WarPetUpdate:
                        UpdateMyInfo(MainWB);
                        TimeSpan TSTimeout = new TimeSpan(); //Обнуление таймаута (ибо может быть сохранен прошлый до кнута)            
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train");
                        if (HtmlEl != null) TimeSpan.TryParse(frmMain.GetDocument(MainWB).GetElementById("train").InnerText, out TSTimeout); //Таймаут между обучениями?
                        Offset = frmMain.GetDocument(MainWB).GetElementById("trainpanel").GetElementsByTagName("button").Count;

                        //Нацеленность
                        Me.WarPet.Focus = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Нацеленность)([0-9])+").Value);
                        if (Me.WarPet.Focus < MaxStat)
                        {
                            match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml); //frmMain.GetDocument(MainWB).GetElementById("train-focus-btn").InnerHtml                            
                            TrainWarPetNeed[0, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[0, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[0, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                            if (TSTimeout == new TimeSpan() && Settings.TrainPetFocus && Me.WarPet.Focus < Settings.maxTrainPetFocus && (Me.Wallet.Money - TrainWarPetNeed[0, 0] >= Settings.minTrainPetMoney || TrainWarPetNeed[0, 0] == 0) && (Me.Wallet.Ore - TrainWarPetNeed[0, 1] >= Settings.minTrainPetOre || TrainWarPetNeed[0, 1] == 0) && (Me.Wallet.Oil - TrainWarPetNeed[0, 2] >= Settings.minTrainPetOil || TrainWarPetNeed[0, 2] == 0))
                            {
                                frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InvokeMember("onclick");
                                IsAjaxCompleteEx(MainWB, "train");
                                UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу нацеленность.");
                                //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                                goto WarPetUpdate;
                            }
                            Offset++; //Эта кнопка уже была!
                        }

                        //Преданность
                        Me.WarPet.Loyality = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Преданность)([0-9])+").Value);
                        if (Me.WarPet.Loyality < MaxStat)
                        {
                            match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml);
                            TrainWarPetNeed[1, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[1, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[1, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                            if (TSTimeout == new TimeSpan() && Settings.TrainPetLoyality && Me.WarPet.Loyality < Settings.maxTrainPetLoyality && (Me.Wallet.Money - TrainWarPetNeed[1, 0] >= Settings.minTrainPetMoney || TrainWarPetNeed[1, 0] == 0) && (Me.Wallet.Ore - TrainWarPetNeed[1, 1] >= Settings.minTrainPetOre || TrainWarPetNeed[1, 1] == 0) && (Me.Wallet.Oil - TrainWarPetNeed[1, 2] >= Settings.minTrainPetOil || TrainWarPetNeed[1, 2] == 0))
                            {
                                frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InvokeMember("onclick");
                                IsAjaxCompleteEx(MainWB, "train");
                                UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу преданность.");
                                //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                                goto WarPetUpdate;
                            }
                            Offset++; //Эта кнопка уже была!
                        }

                        //Массивность
                        Me.WarPet.Mass = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Массивность)([0-9])+").Value);
                        if (Me.WarPet.Mass < MaxStat)
                        {
                            match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml); //frmMain.GetDocument(MainWB).GetElementById("train-focus-btn").InnerHtml                            
                            TrainWarPetNeed[2, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[2, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                            TrainWarPetNeed[2, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                            if (TSTimeout == new TimeSpan() && Settings.TrainPetMass && Me.WarPet.Mass < Settings.maxTrainPetMass && (Me.Wallet.Money - TrainWarPetNeed[2, 0] >= Settings.minTrainPetMoney || TrainWarPetNeed[2, 0] == 0) && (Me.Wallet.Ore - TrainWarPetNeed[2, 1] >= Settings.minTrainPetOre || TrainWarPetNeed[2, 1] == 0) && (Me.Wallet.Oil - TrainWarPetNeed[2, 2] >= Settings.minTrainPetOil || TrainWarPetNeed[2, 2] == 0))
                            {
                                frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InvokeMember("onclick");
                                IsAjaxCompleteEx(MainWB, "train");
                                UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу массивность.");
                                //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                                goto WarPetUpdate;
                            }
                        }
                        #region Все статы по max? + Использование кнута
                        if (Me.WarPet.Focus == MaxStat & Me.WarPet.Loyality == MaxStat & Me.WarPet.Mass == MaxStat)
                        {
                            UpdateStatus("@ " + DateTime.Now + " Босс, этоже крокодил а не питомец, мне его даже гладить то страшно!");
                            Me.WarPet.TrainTimeOutDT = GetServerTime(MainWB).AddHours(2);
                        }
                        else
                        {
                            HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("trainpanel").GetElementsByTagName("button");
                            if (HC.Count >= 1 && HC[HC.Count - 1].InnerText.Contains("Использовать кнут")) //Использую, ибо пару раз видел кнут без кнопки с мёдом! (Вообще нет кнопок! -> Твой питомец сегодня слишком много тренировался и хочет отдохнуть.)
                            {
                                #region Использование кнута
                                if (TSTimeout >= new TimeSpan(3, 59, 0) && Settings.UseTrainWhip
                                    && ((Settings.TrainPetFocus && Me.WarPet.Focus < Settings.maxTrainPetFocus && (TrainWarPetNeed[0, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[0, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[0, 1] == 0 || Me.Wallet.Ore - TrainWarPetNeed[0, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[0, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[0, 2] >= Settings.minTrainPetOil))
                                        || (Settings.TrainPetLoyality && Me.WarPet.Loyality < Settings.maxTrainPetLoyality && (TrainWarPetNeed[1, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[1, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[1, 1] == 0 || Me.Wallet.Ore - TrainWarPetNeed[1, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[1, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[1, 2] >= Settings.minTrainPetOil))
                                        || (Settings.TrainPetMass && Me.WarPet.Mass < Settings.maxTrainPetMass && (TrainWarPetNeed[2, 0] == 0 || Me.Wallet.Money - TrainWarPetNeed[2, 0] >= Settings.minTrainPetMoney) && (TrainWarPetNeed[2, 1] == 0 || Me.Wallet.Ore - TrainWarPetNeed[2, 1] >= Settings.minTrainPetOre) && (TrainWarPetNeed[2, 2] == 0 || Me.Wallet.Oil - TrainWarPetNeed[2, 2] >= Settings.minTrainPetOil))
                                       )
                                    ) //Использование кнута
                                {
                                    UpdateStatus("# " + DateTime.Now + " Малыш, пряники закончились, пробую кнутом.");
                                    HC[HC.Count - 1].InvokeMember("onclick"); //Кнопка "использовать кнут"
                                    IsAjaxCompleteEx(MainWB, "train", false); //Ожидаем пока пропадёт таймер.
                                    Wait(1000, 1500); //Задержка для обновления моих ресурсов!
                                    goto WarPetUpdate;
                                }
                                #endregion
                            }
                            Me.WarPet.TrainTimeOutDT = GetServerTime(MainWB).Add(TSTimeout); //Кнутов или ресурсов нет, запоминаем следующий таймаут
                        }
                        #endregion                                           
                    }
                    #endregion
                    //Пора почесать питомца?=)
                    if (Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("pet-tonus").InnerText, "([0-9])+").Value) < 25)
                    {
                        frmMain.GetDocument(MainWB).GetElementById("pet-scratch").All[0].InvokeMember("onclick");
                        UpdateStatus("# " + DateTime.Now + " Почесал питомца, нахватался блох.");
                    }
                    #endregion
                    break;                    
                case PetAction.TrainRunPet:
                    #region Тренировка бегового пэта
                    GoToPlace(MainWB, Place.Petarena);
                    regex = new Regex("(/obj/pets/(?<PetType>([0-9])+)-[0-9].png)([^'])+[']/petarena(?<URL>/train/([0-9])+/arena)/"); //.*/r/n.*/petarena(?<URL>/train/([0-9])*.)/
                    matches = regex.Matches(frmMain.GetElementsById(MainWB, "equipment-accordion")[1].InnerHtml); //0-> Боевые питомцы, 1-> Беговые питомцы
                    #region Определение типа бегового питомца
                    UsePetType = getPetInformation(stcPetType.Run).type;
                    MaxStat = getPetInformation(stcPetType.Run).maxState;
                    #endregion 
                    foreach (Match m in matches)
                    {
                        if (m.Groups["PetType"].Value.Equals(UsePetType)) 
                        {
                            GoToPlace(MainWB, Place.Petarena, m.Groups["URL"].Value); //Переходим в тренажерный зал выбранного питомца.
                            break; //Нужный питомец уже найден!
                        } 
                    }
                    #region Нет или не найден нужный питомец?
                    if (Regex.IsMatch(frmMain.GetDocumentURL(MainWB), "/petarena/$"))
                    {
                        Me.RunPet.RunTimeOutDT = GetServerTime(MainWB).AddHours(2);
                        return;
                    }
                    #endregion                    
                    regex = new Regex("tugriki\"?>(?<Money>([0-9,])+)?([^=])+(=\"?ruda\"?>(?<Ore>([0-9,])+))?([^=])+(=\"?neft\"?>(?<Oil>([0-9,])+))?([^=])+(=\"?pet-golden\"?>(?<Medal>([0-9,])+))?"); //Выдираем ресурсы необходимые под прокачку
                RunPetUpdate:
                    Offset = 0;
                    UpdateMyInfo(MainWB);
                    int Medal = Convert.ToInt32((string)frmMain.GetJavaVar(MainWB, "$(\"#content .pet-golden.counter\").text()")); //Считывание кол-ва медалек.

                    //Ускорение
                    Me.RunPet.Acceleration = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Ускорение)([0-9])+").Value);
                    if (Me.RunPet.Acceleration < MaxStat)
                    {
                        match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml);
                        TrainRunPetNeed[0, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[0, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[0, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[0, 3] = match.Groups["Medal"].Success ? Convert.ToDecimal(match.Groups["Medal"].Value.Replace(",", "")) : 0;

                        if (Settings.TrainPetAcceleration && Me.RunPet.Acceleration < Settings.maxTrainPetAcceleration && (Me.Wallet.Money - TrainRunPetNeed[0, 0] >= Settings.minTrainPetMoney || TrainRunPetNeed[0, 0] == 0) && (Me.Wallet.Ore - TrainRunPetNeed[0, 1] >= Settings.minTrainPetOre || TrainRunPetNeed[0, 1] == 0) && (Me.Wallet.Oil - TrainRunPetNeed[0, 2] >= Settings.minTrainPetOil || TrainRunPetNeed[0, 2] == 0) && Medal >= TrainRunPetNeed[0, 3])
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset], "onclick");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу Ускорение.");
                            //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                            goto RunPetUpdate;
                        }
                        Offset++; //Эта кнопка уже была!
                    }

                    //Скорость
                    Me.RunPet.Speed = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Скорость)([0-9])+").Value);
                    if (Me.RunPet.Speed < MaxStat)
                    {
                        match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml);
                        TrainRunPetNeed[1, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[1, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[1, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[1, 3] = match.Groups["Medal"].Success ? Convert.ToDecimal(match.Groups["Medal"].Value.Replace(",", "")) : 0;

                        if (Settings.TrainPetSpeed && Me.RunPet.Speed < Settings.maxTrainPetSpeed && (Me.Wallet.Money - TrainRunPetNeed[1, 0] >= Settings.minTrainPetMoney || TrainRunPetNeed[1, 0] == 0) && (Me.Wallet.Ore - TrainRunPetNeed[1, 1] >= Settings.minTrainPetOre || TrainRunPetNeed[1, 1] == 0) && (Me.Wallet.Oil - TrainRunPetNeed[1, 2] >= Settings.minTrainPetOil || TrainRunPetNeed[1, 2] == 0) && Medal >= TrainRunPetNeed[1, 3])
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset], "onclick");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу Скорость.");
                            //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                            goto RunPetUpdate;
                        }
                        Offset++; //Эта кнопка уже была!
                    }

                    //Выносливость
                    Me.RunPet.Endurance = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Выносливость)([0-9])+").Value);
                    if (Me.RunPet.Endurance < MaxStat)
                    {
                        match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml);
                        TrainRunPetNeed[2, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[2, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[2, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[2, 3] = match.Groups["Medal"].Success ? Convert.ToDecimal(match.Groups["Medal"].Value.Replace(",", "")) : 0;

                        if (Settings.TrainPetEndurance && Me.RunPet.Endurance < Settings.maxTrainPetEndurance && (Me.Wallet.Money - TrainRunPetNeed[2, 0] >= Settings.minTrainPetMoney || TrainRunPetNeed[2, 0] == 0) && (Me.Wallet.Ore - TrainRunPetNeed[2, 1] >= Settings.minTrainPetOre || TrainRunPetNeed[2, 1] == 0) && (Me.Wallet.Oil - TrainRunPetNeed[2, 2] >= Settings.minTrainPetOil || TrainRunPetNeed[2, 2] == 0) && Medal >= TrainRunPetNeed[2, 3])
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset], "onclick");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу Выносливость.");
                            //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                            goto RunPetUpdate;
                        }
                        Offset++; //Эта кнопка уже была!
                    }

                    //Ловкость
                    Me.RunPet.Dexterity = Convert.ToDecimal(Regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.InnerText, "(?<=Ловкость)([0-9])+").Value);
                    if (Me.RunPet.Dexterity < MaxStat)
                    {
                        match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset].InnerHtml);
                        TrainRunPetNeed[3, 0] = match.Groups["Money"].Success ? Convert.ToDecimal(match.Groups["Money"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[3, 1] = match.Groups["Ore"].Success ? Convert.ToDecimal(match.Groups["Ore"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[3, 2] = match.Groups["Oil"].Success ? Convert.ToDecimal(match.Groups["Oil"].Value.Replace(",", "")) : 0;
                        TrainRunPetNeed[3, 3] = match.Groups["Medal"].Success ? Convert.ToDecimal(match.Groups["Medal"].Value.Replace(",", "")) : 0;

                        if (Settings.TrainPetDexterity && Me.RunPet.Dexterity < Settings.maxTrainPetDexterity && (Me.Wallet.Money - TrainRunPetNeed[3, 0] >= Settings.minTrainPetMoney || TrainRunPetNeed[3, 0] == 0) && (Me.Wallet.Ore - TrainRunPetNeed[3, 1] >= Settings.minTrainPetOre || TrainRunPetNeed[3, 1] == 0) && (Me.Wallet.Oil - TrainRunPetNeed[3, 2] >= Settings.minTrainPetOil || TrainRunPetNeed[3, 2] == 0) && Medal >= TrainRunPetNeed[3, 3])
                        {
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("trainpanel").Parent.GetElementsByTagName("button")[Offset], "onclick");
                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                            UpdateStatus("@ " + DateTime.Now + " Подкачал питомцу Ловкость.");
                            //Wait(1500, 2000); //Задержка для обновления моих ресурсов!
                            goto RunPetUpdate;
                        }
                    }
                    #region Все статы по max?
                    if (Me.RunPet.Acceleration == MaxStat && Me.RunPet.Speed == MaxStat && Me.RunPet.Endurance == MaxStat && Me.RunPet.Dexterity == MaxStat)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Босс, за этим \"бегуном\" разве только человек-молния угонится, я пасс!");
                        Me.RunPet.TrainTimeOutDT = GetServerTime(MainWB).AddHours(2);
                    }
                    #endregion
                    #region Не хватает медалек?
                    if ((!Settings.TrainPetAcceleration || Me.RunPet.Acceleration == 750 || TrainRunPetNeed[0, 3] > Medal)
                        && (!Settings.TrainPetSpeed || Me.RunPet.Speed == 750 || TrainRunPetNeed[1, 3] > Medal)
                        && (!Settings.TrainPetEndurance || Me.RunPet.Endurance == 750 || TrainRunPetNeed[2, 3] > Medal)
                        && (!Settings.TrainPetDexterity || Me.RunPet.Dexterity == 750 || TrainRunPetNeed[3, 3] > Medal)
                       ) Me.RunPet.TrainTimeOutDT = GetServerTime(MainWB).AddHours(2);
                    #endregion
                    #endregion
                    break;
                case PetAction.Run:
                    #region Бега питомцев
                    String UsePetTypeName = null;

                    GoToPlace(MainWB, Place.Petrun);
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("grayhound-tickets-num");
                    if (HtmlEl == null) return; //Аях, не дал перейти на нужную страничку?
                    int RunTickets = Convert.ToInt32(HtmlEl.InnerText);
                    DateTime ServerDT = GetServerTime(MainWB);
                    if (RunTickets > 0) //Ешё есть билеты для забегов?
                    {
                        if (frmMain.GetDocument(MainWB).GetElementById("race-select") != null) //Можно выбирать питомца для забега? (он сейчас не бежит)
                        {
                            #region Инициализация
                            Me.RunPet.Tonus = -1; //Если не найдём нужного бегового Пета, тонус останется несчитанным.
                            #endregion
                            #region Определение типа бегового питомца
                            switch (Settings.TrainRunPetType)
                            {
                                case 1:
                                    UsePetTypeName = "Кошечка";
                                    break;
                                case 2:
                                    UsePetTypeName = "Собачка";
                                    break;
                                case 3:
                                    UsePetTypeName = "Белочка";
                                    break;
                                case 4:
                                    UsePetTypeName = "Енот";
                                    break;
                                case 5:
                                    UsePetTypeName = "Лиса";
                                    break;
                                case 6:
                                    UsePetTypeName = "Волк";
                                    break;
                                case 7:
                                    UsePetTypeName = "Медведь";
                                    break;
                                case 8:
                                    UsePetTypeName = "Тигр";
                                    break;
                                case 9:
                                    UsePetTypeName = "Страус";
                                    break;
                                case 10:
                                    UsePetTypeName = "Кенгуру";
                                    break;
                                case 11:
                                    UsePetTypeName = "Единорог";
                                    break;
                                case 12:
                                    UsePetTypeName = "Пегас";
                                    break;
                            }
                            #endregion
                            matches = Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("race-select").InnerHtml, "value=\"?(?<PetID>([0-9])+)\"?>(?<PetType>([\\w])+)");
                            for (int i = 0; i < matches.Count; i++)
                            {
                                if (matches[i].Groups["PetType"].Value.Equals(UsePetTypeName))
                                {
                                    #region Считывание допингов
                                    if (Me.RunPet.DopingDT < GetServerTime(MainWB)) //Проверять допинг если не делал этого раньше!
                                    {
                                        string info = ReadToolTip(MainWB, frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("IMG")[i + 1]);
                                        match = Regex.Match(info, "[+](Ускорение|Выносливость|Ловкость|Скорость|ко всем характеристикам|Шуршащий мячик) до(?<Date>([0-9: -])+)");
                                        if (match.Success)
                                        {
                                            Me.RunPet.Doping = true;
                                            Me.RunPet.DopingDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                                        }
                                        else Me.RunPet.Doping = false;
                                    }
                                    #endregion

                                    Me.RunPet.Tonus = Convert.ToInt32(frmMain.GetJavaVar(MainWB, "pets['" + matches[i].Groups["PetID"].Value + "']['tonus_percent']"));
                                    int TimeNeed = (100 - Me.RunPet.Tonus + (RunTickets - 1 > 0 ? (RunTickets - 1) * 20 : 0)) * 3; //Нехватающий до 100% тонус + тонус необходимый при остальных забегах * 3 (Полное восстановление тонуса за 300 минут, 300min/100% = 3)
                                    if (Me.RunPet.Tonus == 100 || ServerDT.Date < ServerDT.AddMinutes(TimeNeed).Date || Me.RunPet.Doping) //Бегать только при полном тонусе или если на такие бега уже просто не хватит времени!
                                    {
                                        if (Me.RunPet.Tonus >= 20) //Минимальный тонус для забега
                                        {
                                            frmMain.GetDocument(MainWB).GetElementById("race-select").SetAttribute("value", matches[i].Groups["PetID"].Value); //Переключаю выбор на нужного питомца
                                            if (ServerDT > Me.RunPet.FreeRuns.LastDT) Me.RunPet.FreeRuns.Stop = false; //Обнуление
                                            if (Me.RunPet.Doping && Me.RunPet.FreeRuns.Stop) frmMain.GetDocument(MainWB).All["tickets-type"].InvokeMember("click");
                                            UpdateStatus("# " + DateTime.Now + (Me.RunPet.Doping && Me.RunPet.FreeRuns.Stop ? " Отдал билет, подтолкнул" : " Отправил") + " Пэта в бега, нашёптывая: \"Беги мой чемпион, беги\"!");
                                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("Form")[0], "submit");
                                            IsWBComplete(MainWB);
                                            #region Всё прошло ок?
                                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-text");
                                            if (HtmlEl == null ? false : HtmlEl.InnerText == "Вам нужен билет чтобы участвовать в забегах.")
                                            {
                                                if (Me.RunPet.Doping && !Me.RunPet.FreeRuns.Stop) UpdateStatus("@ " + DateTime.Now + " Не подведи меня \"обжора\", начинаю гонять малового по билетам!"); //Если Me.RunPet.FreeRuns.Stop то, уже и дополнительные билеты закончились, а то что мы видим от питомца из другой лиги!
                                                else 
                                                {
                                                    UpdateStatus("@ " + DateTime.Now + " К ноге! Усё, бобик сдох, завтра забеги продолжим!");
                                                    Me.RunPet.RunTimeOutDT = ServerDT.AddDays(1).Date; //Билеты закончились, зайти завтра
                                                }
                                                Me.RunPet.FreeRuns.Stop = true;
                                                Me.RunPet.FreeRuns.LastDT = ServerDT.AddDays(1).Date; //Билеты закончились, будут завтра
                                            }
                                            else Me.RunPet.RunTimeOutDT = ServerDT.AddMinutes(ServerDT.Date == ServerDT.AddMinutes(TimeNeed).Date ? 60 : 1); //Если времени достаточно ждем 100% тонуса, ежели нет забежим через минуту.
                                            #endregion
                                            GoToPlace(MainWB, Place.Player); //Необходимо покинуть страничку, иначе динамические обновления оной могут уронить бота
                                            break; //Нужный питомец уже отправлен в бега!
                                        }
                                        else //Нехватает тонуса для пробежки?
                                        {
                                            if (Me.RunPet.Doping) //Пьём баночку тонуса.
                                            {
                                                match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("content").InnerHtml, "tonus_up.png([^>])+data-id=(\")?(?<Id>([0-9])+)");
                                                if (match.Groups["Id"].Success) 
                                                {
                                                    UpdateStatus("# " + DateTime.Now + " Залил в бегуна редбулла, теперь он всем покажет!");
                                                    //PRIMER: $.post('/petrun/use_doping/'+petId+'/'+food_id+'/', {ajax: 1}, function(response))
                                                    frmMain.GetJavaVar(MainWB, "$.post(\"/petrun/use_doping/" + matches[i].Groups["PetID"].Value + "/" + match.Groups["Id"].Value + "/\", {ajax: 1});");                                                    
                                                    Wait(1500, 2000);
                                                    break; //Тонус пополнен, выходим не сдвигая старта!
                                                }                                                
                                            }
                                            Me.RunPet.RunTimeOutDT = ServerDT.AddMinutes((20 - Me.RunPet.Tonus) * 3); //Раз уж досюда прошёл, то остаётся мало времени для побегов при 100% тонусе
                                        } 
                                    }
                                    else Me.RunPet.RunTimeOutDT = ServerDT.AddMinutes((100 - Me.RunPet.Tonus) * 3); //Времени предостаточно, но нужно подождать пока станет 100% тонуса!
                                }
                            }
                            if (Me.RunPet.Tonus == -1) { Me.RunPet.RunTimeOutDT = ServerDT.AddDays(1).Date; UpdateStatus("@ " + DateTime.Now + " Шеф, сегодня первое апреля? Где мой беговой питомец?"); }
                        }
                        else Me.RunPet.RunTimeOutDT = ServerDT.AddMinutes(1); //Питомец сейчас походу и бежит....                       
                    }
                    else Me.RunPet.RunTimeOutDT = ServerDT.AddDays(1).Date; //Билеты закончились, зайти завтра
                    #endregion
                    break;
            }
        } //OK
        public void Fitness() //OK
        {
            BugReport("Fitness");

            Match match;
            HtmlElement HtmlEl;

            if (GoToPlace(MainWB, Place.Trainer)) //Nomer El+11 skol'ko nexvataet deneg, chtob ego prokochat'
            {
            ReTry:
                UpdateMyInfo(MainWB); //Считываем деньги на данный момент.

                //Здоровье
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-health");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Здоровье(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[0, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[0, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeHealth & TrainMeNeed[0, 0] < Settings.maxTrainMeHealth & Me.Wallet.Money >= TrainMeNeed[0, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Сила
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-strength");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Сила(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[1, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[1, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeStrength & TrainMeNeed[1, 0] < Settings.maxTrainMeStrength & Me.Wallet.Money >= TrainMeNeed[1, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Ловкость
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-dexterity");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Ловкость(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[2, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[2, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeDexterity & TrainMeNeed[2, 0] < Settings.maxTrainMeDexterity & Me.Wallet.Money >= TrainMeNeed[2, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Выносливость
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-resistance");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Выносливость(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[3, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[3, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeEndurance & TrainMeNeed[3, 0] < Settings.maxTrainMeEndurance & Me.Wallet.Money >= TrainMeNeed[3, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Хитрость
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-intuition");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Хитрость(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[4, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[4, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeCunning & TrainMeNeed[4, 0] < Settings.maxTrainMeCunning & Me.Wallet.Money >= TrainMeNeed[4, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Внимательность
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-attention");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Внимательность(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[5, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[5, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeAttentiveness & TrainMeNeed[5, 0] < Settings.maxTrainMeAttentiveness & Me.Wallet.Money >= TrainMeNeed[5, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }

                //Харизма
                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("train-charism");
                match = Regex.Match(HtmlEl.OffsetParent.InnerText, "Харизма(?<Value>([0-9])+)\\s+((?<NotEnough>Не хватает)|Повысить —) (?<Cost>([0-9,])+)");
                TrainMeNeed[6, 0] = Convert.ToInt64(match.Groups["Value"].Value);
                TrainMeNeed[6, 1] = Convert.ToInt64(match.Groups["Cost"].Value.Replace(",", "")) + (match.Groups["NotEnough"].Success ? Me.Wallet.Money : 0); //Когда нехватает, нужно добавить показываеммую сумму к моим деньгам!
                if (Settings.TrainMeCharisma & TrainMeNeed[6, 0] < Settings.maxTrainMeCharisma & Me.Wallet.Money >= TrainMeNeed[6, 1]) { frmMain.InvokeMember(MainWB, HtmlEl, "click"); goto ReTry; }
            }
        }
        public bool Oil(OilAction NA)
        {
            BugReport("Oil");

            #region Недорос до нефтекачки?
            if (Me.Player.Level < 10)
            {
                DateTime ServerDT = GetServerTime(MainWB);
                Me.OilTowerDT = DateTime.Now.AddDays(1);
                Me.OilHunting.LastDT = ServerDT;
                Me.OilHunting.Stop = true;
                if (NA == OilAction.Fight || NA == OilAction.OilTower)
                {
                    UpdateStatus("# " + DateTime.Now + " Уфф маловат я, руками много нефти не унесёшь!");
                    return false;
                }                
                if (NA == OilAction.LeninFight && Me.Player.Level < 7)
                {
                    Me.OilLeninHunting.NextDT = DateTime.Now.Add(ServerDT.AddDays(1).Date - ServerDT).AddMinutes(2);
                    Me.OilLeninHunting.RestartDT = Me.OilLeninHunting.NextDT;
                    Me.OilLeninHunting.Stop = true;
                    UpdateStatus("# " + DateTime.Now + " Уфф на Ленина не пойду, маловат я с дедом кувыркаться!");
                    return false;
                }               
            }
            #endregion

            int Points;
            int Offset;
            HtmlElement HtmlEl;
            
            GoToPlace(MainWB, Place.Oil);
 
            switch (NA)
            {
                case OilAction.OilTower:
                    if (frmMain.GetDocumentURL(MainWB).Contains("neftlenin")) return false; //Новый нефтепровод?
                    else
                    {
                        #region Я владею нефтекачкой?
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("oilprocesstake");
                        if (HtmlEl != null)
                        {
                            Random WaitM = new Random();
                            string[] Time = frmMain.GetDocument(MainWB).GetElementById("oilprocess").InnerText.Split(':'); //HH:mm:ss
                            if (Time != null) Me.OilTowerDT = GetServerTime(MainWB).Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]) + WaitM.Next(1, 180), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                            if (HtmlEl.GetAttribute("classname") == "button")
                            {
                                frmMain.GetDocument(MainWB).GetElementsByTagName("FORM")[0].InvokeMember("submit"); //Кнопка пока не работает=( Но Формы достаточно!
                                UpdateStatus("$ Матильда ..., да я Шейх ёбте! Забрал нефти: ", 0, 0, 200);
                                Wait(500, 1000);
                            }
                            return true;
                        }
                        #endregion
                        Me.OilTowerDT = DateTime.Now.AddDays(1); //У меня нет нефтекачки, быть может появится завтра
                        return false;
                    }                    
                case OilAction.Fight:
                    if (frmMain.GetDocumentURL(MainWB).Contains("neftlenin")) //Новый нефтепровод?
                    {
                        frmMain.GetJavaVar(MainWB, "$.post(\"/neftlenin/\", {\"ajax\": 1, \"action\": \"hideNeftLEnin\"});"); //Переходим на старый нефтепровод
                        Wait(1500, 2000);
                        return Oil(OilAction.Fight);
                    }
                    else
                    {
                        Offset = Oil(OilAction.OilTower) ? 1 : 0; //У меня есть нефтекачка?
                        UpdateStatus("# " + DateTime.Now + " Ку-ку, смазливые, нефти не будет? А если найду?");
                        MatchCollection matches = Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("pipeline-scroll").InnerHtml, "icon-locked pulp([0-9])+");

                        if (frmMain.GetDocument(MainWB).GetElementsByTagName("button").Count - Offset > 0) //Есть ешё с кем подраться? Fix:У меня появилась собственная нефтекачка?
                        {
                            #region Проверка нынешнего вентиля!
                            if (16 - matches.Count <= Settings.maxOilLvl) //Проверка на каком вентиле сейчас.
                            {
                                if (frmMain.GetDocument(MainWB).GetElementsByTagName("button")[0].InnerText.Contains("сразу")) //Иначе лупасим за мёд!
                                {
                                    UpdateStatus("@ " + DateTime.Now + " Опа, \"Папка\" детектед=)  -Пап, окуратнее я чуть мёд не прокакал!");
                                    return false;
                                }
                                frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementsByTagName("button")[0], "onclick"); //Нажатие кнопки Аттаки            
                                #region Анализ боя
                                if (!AnalyseFight(MainWB))
                                {
                                    Me.OilHunting.Val += Settings.OilUseOhara ? -Me.OilHunting.Val : 1; //Останавливаем побеги в нефтепровод, если только не просили усердно наезжать Охарой.
                                    Me.OilHunting.Stop = Me.OilHunting.Val >= Settings.maxOilDefeats; //Слишком много проигрышей -> стоп!
                                }
                                Me.OilHunting.LastDT = GetServerTime(MainWB);
                                #region Поедание тикающего сникерса
                                if (Settings.UseSnikersOil)
                                {
                                    GoToPlace(MainWB, Place.Player);
                                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("inventory-snikers-btn"); //Банка с тонусом
                                    if (HtmlEl != null && frmMain.GetJavaVar(MainWB, "m.items['" + HtmlEl.GetAttribute("data-id") + "'].mf['0'].innerText") != null) EatDrog(MainWB, ShopItems.Snikers); //Уже тикает время?
                                }                                
                                #endregion
                                #endregion
                                return true;
                            }
                            #endregion
                        }
                        else //Миша повержен, на сегодня хватит драчек!
                        {
                            UpdateStatus("@ " + DateTime.Now + " Кругом одни трупы ..., да я Рэмба!");
                            Me.OilHunting.LastDT = GetServerTime(MainWB);
                            Me.OilHunting.Stop = true;
                            return true;
                        }
                        UpdateStatus("@ " + DateTime.Now + " Эээ не, эт крутые, ещё нагнут под забором ...");
                        Me.OilHunting.LastDT = GetServerTime(MainWB);
                        Me.OilHunting.Stop = true;
                        return false;
                    }                    
                case OilAction.LeninFight:
                    #region Инфо о возможных переменных
                    /*
                    object Info;
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion"); //Подозрительность
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.step"); //Вентиль на котором сейчас нахожусь
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.pulp"); //Номер НПЦ с которым предстоит подраться, квэсты тут не считаются
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.partbilet"); //Количество партбилетов с собой
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel"); //Очков за дуэль
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.escape"); //Очков за новый поиск
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.reset"); //Минус очков за партбилеты
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.typeStep"); //Тип следующего ивента: m миссия, d бой, g групповой бой, b бой с Ленином
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.battle"); //1-> Уже выбито окошко с битвой
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.timer"); //Секунд до появления окошка с игрой в кости!
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.timerRestart"); //Секунд до рестарта!
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.game"); //1-> Игра в кости
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission"); //Номер костяшки, которую сейчас разыгрываем
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.gamble['1'].enemy"); //Число выброшенное врагом на костяшке
                    Info = frmMain.GetJavaVar(MainWB, "NeftLenin.data"); //Информация о выигрыше
                    Settings.ServerURL + "/neftlenin/holidayreset/"; //Резет ленинопровода!
                    */
                    #endregion
                    if (!frmMain.GetDocumentURL(MainWB).Contains("neftlenin")) //Старый нефтепровод?
                    {
                        DateTime ServerDT = GetServerTime(MainWB);
                        Me.OilLeninHunting.NextDT = DateTime.Now.Add(ServerDT.AddDays(1).Date - ServerDT).AddMinutes(2);
                        Me.OilLeninHunting.RestartDT = Me.OilLeninHunting.NextDT;
                        Me.OilLeninHunting.Stop = true;
                        return false;
                    }
                    else
                    {
                        UpdateStatus("# " + DateTime.Now + " Салют комсомолу и рабочему классу! Сейчас я отправлю вас назад в СССР...");
                        Me.OilLeninHunting.AllowPartBilet = false; //Разрешение использовать билеты, сколько понадобится для победы, может быть включено только допингом!
                        
                        Me.OilLeninHunting.Lvl = Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.step"));                        

                        #region Синхронизация обвалов крысопровода и ленинопровода.
                        if (Settings.OilLeninSyncRats && Me.OilLeninHunting.Lvl <= 1 && Settings.SearchRat && !IsTimeInTimespan(new TimeSpan(0, 0, (int)Settings.OffsetSyncOilLenin - 30, 0), new TimeSpan(0, 0, (int)Settings.OffsetSyncOilLenin + 30, 0), DateTime.Now.AddHours(24) - Me.RatHunting.RestartDT))
                        {
                            Me.OilLeninHunting.RestartDT = Me.RatHunting.RestartDT.AddMinutes((double)Settings.OffsetSyncOilLenin);
                            Me.OilLeninHunting.NextDT = Me.OilLeninHunting.RestartDT;
                            if (Me.OilLeninHunting.NextDT > DateTime.Now) UpdateStatus("@ " + DateTime.Now + " Пытаюсь синхронизировать ленино-крысопровод, скоро буду!");
                        }                       
                        #endregion

                        while (!Me.OilLeninHunting.Stop && Me.OilLeninHunting.NextDT < DateTime.Now)
                        {
                            #region Проверка нынешнего вентиля!
                            if (Me.OilLeninHunting.RestartDT < DateTime.Now) Me.OilLeninHunting.RestartDT = DateTime.Now.AddSeconds((int)frmMain.GetJavaVar(MainWB, "NeftLenin.timerRestart") + 30);
                            if (!Settings.AllowPartBilet || Me.OilLeninHunting.Lvl < Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.step"))) Me.OilLeninHunting.AllowPartBilet = false; //Разрешение использовать билеты, сколько понадобится для победы, может быть включено только допингом!
                            Me.OilLeninHunting.Lvl = Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.step"));
                            Me.OilLeninHunting.FightType = (string)frmMain.GetJavaVar(MainWB, "NeftLenin.typeStep");
                            if (Me.OilLeninHunting.Lvl <= Settings.maxOilLeninLvl) //Проверка на каком вентиле сейчас.
                            {
                                if (IsHPLow(MainWB, 100) ? (HealMePlus() ? true : CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100)) : true) //Лечить в любом варианте до 100%
                                {
                                    if (!Dopings(ref Me.ArrOilLeninDoping, DopingAction.Check)) { UpdateStatus("@ " + DateTime.Now + " Трезвым я против Дружинника[" + Me.OilLeninHunting.Lvl + "] не пойду, и не просите!"); Me.OilLeninHunting.Stop = true; return false; }
                                    if (Me.OilLeninHunting.NextDT > DateTime.Now) { UpdateStatus("@ " + DateTime.Now + " Эх, придётся попозже заглянуть, что за слово такое \"синхронизация\"..."); return false; }
                                    if (!frmMain.GetDocumentURL(MainWB).Contains("/neftlenin/")) GoToPlace(MainWB, Place.Oil);
                                }
                                else Me.OilLeninHunting.NextDT = DateTime.Now.AddMinutes(15); //Сейчас нет денег на лечение и запасов нет ... попробуем через 15 минут                                

                                switch (Me.OilLeninHunting.FightType)
                                {
                                    case "b":
                                        #region Ленин
                                        #endregion
                                    case "g":
                                    case "d":
                                        #region Драки
                                        if (IsTimeout(MainWB, false, false) || Me.Trauma.Stop) return false; //Не драться при таймауте и травме!
                                        #region Нажатие кнопки Аттаки
                                        if (frmMain.GetJavaVar(MainWB, "NeftLenin.battle").Equals(0))
                                        {
                                            if (IsHPLow(MainWB, 100) ? (HealMePlus() ? true : CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100)) : true) //Лечить в любом варианте до 100%
                                            {
                                                if (!frmMain.GetDocumentURL(MainWB).Contains("/neftlenin/")) GoToPlace(MainWB, Place.Oil);
                                                Offset = Me.OilLeninHunting.FightType.Equals("b") ? 1 : 0; //0-> Драки и Групповые, 1->Ленин, 2-> Квэст 
                                                frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("pipeline-scroll").GetElementsByTagName("button")[Offset], "click");
                                                IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                            }
                                            else Me.OilLeninHunting.NextDT = DateTime.Now.AddMinutes(15); //Сейчас нет денег на лечение и запасов нет ... попробуем через 15 минут
                                        }
                                        #endregion
                                        #region Проверка, есть ли полезности?
                                        bool Attack = false; //Инициализация
                                        bool Prize = !Settings.OilLeninLeaveNoKey && !Settings.OilLeninLeaveNoElement && !Settings.OilLeninLeaveNoBox; //Инициализация

                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("neftlenin_alert_" + frmMain.GetJavaVar(MainWB, "NeftLenin.typeStep")); //Окошко с наградой
                                        foreach (HtmlElement H in HtmlEl.GetElementsByTagName("IMG"))
                                        {
                                            Prize |= Settings.OilLeninLeaveNoKey && H.GetAttribute("src").Contains("key3");
                                            Prize |= Settings.OilLeninLeaveNoElement && H.GetAttribute("src").Contains("collections/71");
                                            Prize |= Settings.OilLeninLeaveNoBox && H.GetAttribute("src").Contains("box3");
                                            if (Prize) break;
                                        }                                        
                                        if (Settings.OilLeninRobinHood && Me.OilLeninHunting.Lvl > 28 && Regex.Matches(HtmlEl.InnerHtml, "key3|box35").Count != 2
                                            && Me.OilLeninHunting.RestartDT - DateTime.Now > new TimeSpan(0, (Me.OilLeninHunting.Lvl == 29 ? 4 : 2) * (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel") - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion") + (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion"), 0)) Prize = false; //Нет ключика или синего ящика?
                                        #endregion
                                        Attack = Settings.GoOilLenin && !Me.OilLeninHunting.Stop;
                                        if (Attack) //В принципе всё ОК, для нападения есть ли приз?
                                        {
                                            if (Prize)
                                            {
                                                Points = (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion") + (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel") - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion");
                                                if (Points <= 0) //Хватает, на проведение драки!
                                                {
                                                    Offset = Me.OilLeninHunting.FightType.Equals("d") ? 3 : 4; //Октябрёнка / Д'Артаньяна сет!                                           
                                                    #region При стенках переносим начало драки, если необходимо
                                                    if (Offset == 4 && (TimeToStopAtack(NextTimeout.OilLeninFight, StopTimeoutType.GrpFight) || TimeToStopAtack(NextTimeout.OilLeninFight, StopTimeoutType.RatHunting)))
                                                    {
                                                        Me.OilLeninHunting.NextDT = TimeToStopAtack(NextTimeout.OilLeninFight, StopTimeoutType.RatHunting) ? Me.RatHunting.NextDT : DateTime.Now.Add(GrpFight.Mafia.FightFound ? new TimeSpan(0, 2, 0) : GrpFight.NextFightDT - GetServerTime(MainWB)); //Ожидаем боя с паханом, тогда просто 2 минутки, там решится!
                                                        UpdateStatus("@ " + DateTime.Now + " Эх, придётся попозже заглянуть, братва, не разбегаемся!");
                                                        return false;
                                                    }
                                                    #endregion
                                                    #region Переодевание сетов
                                                    if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, Offset); //Одеваем Октябрёнка / Д'Артаньяна сет!
                                                    #endregion
                                                    #region Проверка боевого инвентаря
                                                    if (Settings.UseAutoFightBagSlots && Offset == 4) CheckBagFightItems(GroupFightType.Oil);
                                                    #endregion
                                                    if (IsHPLow(MainWB, 100) ? (HealMePlus() ? true : CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100)) : true) //Лечить в любом варианте до 100%
                                                    {
                                                        if (!frmMain.GetDocumentURL(MainWB).Contains("/neftlenin/")) GoToPlace(MainWB, Place.Oil);
                                                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("neftlenin_alert_" + frmMain.GetJavaVar(MainWB, "NeftLenin.typeStep")); //Окошко с наградой
                                                        #region Определяем ресурсы, которые получим за победу!
                                                        string[] Resource; //Только для групповых боев
                                                        if (Offset == 4) Resource = new string[] { ((string)frmMain.GetJavaVar(MainWB, "$(\"#" + HtmlEl.Id + " .tugriki\").text()") ?? "0"), ((string)frmMain.GetJavaVar(MainWB, "$(\"#" + HtmlEl.Id + " .ruda\").text()") ?? "0"), ((string)frmMain.GetJavaVar(MainWB, "$(\"#" + HtmlEl.Id + " .neft\").text()") ?? "0") };
                                                        else Resource = new string[] { "0", "0", "0"};
                                                        #endregion
                                                        UpdateStatus("# " + DateTime.Now + " Итак, дверь номер " + Me.OilLeninHunting.Lvl + ", я вхожу!");
                                                        frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[0], "click"); //Нападаем
                                                        if (!AnalyseFight(MainWB, Resource[0].Equals("") ? 0 : Convert.ToInt32(Resource[0]), Resource[1].Equals("") ? 0 : Convert.ToInt32(Resource[1]), Resource[2].Equals("") ? 0 : Convert.ToInt32(Resource[2]))
                                                            && !(Settings.OilLeninIronHead && Me.OilLeninHunting.Lvl == 28)) //При атаке коммуниста передаем ресурсы которые получим в случае победы.
                                                        {
                                                            Me.OilLeninHunting.Defeats += Settings.OilUseOhara ? -Me.OilLeninHunting.Defeats : 1; //Останавливаем побеги в ленинопровод, если только не просили усердно наезжать Охарой.
                                                            Me.OilLeninHunting.Stop = Me.OilLeninHunting.Defeats >= Settings.maxOilDefeats;
                                                        }
                                                        if (!Me.OilLeninHunting.Stop) GoToPlace(MainWB, Place.Oil); //Продолжаем драки!
                                                    }
                                                    else Me.OilLeninHunting.NextDT = DateTime.Now.AddMinutes(15); //Сейчас нет денег на лечение и запасов нет ... попробуем через 15 минут
                                                }
                                                else //Перебор очков для нападения
                                                {
                                                    if (((Me.OilLeninHunting.AllowPartBilet) || (Settings.OilLeninIronHead && Me.OilLeninHunting.Lvl == 28)) && Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.partbilet")) >= Convert.ToInt32(frmMain.GetJavaVar(MainWB, "$(\"#content .part-bilet\").text()")))
                                                    {
                                                        UpdateStatus("@ " + DateTime.Now + ((Settings.OilLeninIronHead && Me.OilLeninHunting.Lvl == 28) ? " Я тебя всё равно забодаю, лучше сам подвинься!" : " С дороги, я Генсек!"));
                                                        frmMain.GetJavaVar(MainWB, "NeftLenin.reset(2);");
                                                        IsWBComplete(MainWB, 500, 1000); //IsAjaxComplete(MainWB, 500, 1500);
                                                    }
                                                    else Me.OilLeninHunting.NextDT = DateTime.Now.AddSeconds((int)frmMain.GetJavaVar(MainWB, "NeftLenin.timerSuspisionDecrease") + (Points - 1) * 60); //Новое время, когда очки спадут для нападения
                                                }
                                            }
                                            else
                                            {
                                                UpdateStatus("# " + DateTime.Now + " Мде, а коммунисты то нынче обнищали ...");
                                                Points = (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion") + (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.escape") - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion");
                                                if (Points <= 0) //Хватает, на поиск нового патруля!
                                                {
                                                    frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[1], "click"); //Ищем иного
                                                    IsWBComplete(MainWB, 300, 1000); //IsAjaxComplete(MainWB, 300, 1000);
                                                }
                                                else //Перебор очков для Поиска нового
                                                {
                                                    Me.OilLeninHunting.NextDT = DateTime.Now.AddSeconds((int)frmMain.GetJavaVar(MainWB, "NeftLenin.timerSuspisionDecrease") + ((int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel") + Points - 1) * 60); //Новое время смены противника + возможной дуэли
                                                }
                                            }
                                        }
                                        #endregion
                                        break;
                                    case "m":
                                        #region Задание
                                        #region Нажатие кнопки Задания + "начала задания"
                                        if (frmMain.GetJavaVar(MainWB, "NeftLenin.timer") == null && frmMain.GetJavaVar(MainWB, "NeftLenin.game").Equals(0))
                                        {
                                            UpdateStatus("@ " + DateTime.Now + " Ооооо приключения, эт я люблю, дайте два!");
                                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("pipeline-scroll").GetElementsByTagName("button")[2], "click");
                                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                            #region Нажатие кнопки "начала задания"
                                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("neftlenin_alert_prem_first").GetElementsByTagName("button")[0], "click");
                                            IsWBComplete(MainWB); //IsAjaxComplete(MainWB);
                                            #endregion
                                        }
                                        #endregion
                                        #region Таймер до игры в кости
                                        if (frmMain.GetJavaVar(MainWB, "NeftLenin.timer") != null) Me.OilLeninHunting.NextDT = DateTime.Now.AddSeconds((int)frmMain.GetJavaVar(MainWB, "NeftLenin.timer"));
                                        #endregion
                                        #region Игра в кости
                                        if (frmMain.GetJavaVar(MainWB, "NeftLenin.game").Equals(1) && Me.OilLeninHunting.NextDT < DateTime.Now)
                                        {
                                            UpdateStatus("# " + DateTime.Now + " Ухты а за " + Me.OilLeninHunting.Lvl + " дверью казино ... поиграемс!");
                                            for (int i = 0; i <= 5; i++)
                                            {
                                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("neftlenin_alert_mission");
                                                object[] o = { frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission"), frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission"), frmMain.GetJavaVar(MainWB, "NeftLenin.gamble['" + frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") + "'].enemy"), frmMain.GetJavaVar(MainWB, "NeftLenin.price_gamble['" + frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") + "']") };
                                                if (!frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission").Equals(0) && //0 -> Уже проиграл=(
                                                    Convert.ToDecimal(frmMain.GetJavaVar(MainWB, "NeftLenin.gamble['" + frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") + "'].enemy")) <= Settings.maxOilLeninDice)
                                                {
                                                    if (i == 5) UpdateStatus("$ " + DateTime.Now + " Беру добро, пока мухлеж не вскрыли!"); //выиграл всё
                                                    else UpdateStatus("# " + DateTime.Now + " Бросаю кубик на стол ..., ибо гранаты забрали ...");
                                                    frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[(int)frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") - 1], "click"); //Играем в кости
                                                }
                                                else //Я проиграл или запрещено бросать при таких кубиках
                                                {
                                                    Points = (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion") + (int)frmMain.GetJavaVar(MainWB, "NeftLenin.price_gamble['" + frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") + "']") //Цена нажатия кнопки сбежать с задания
                                                           - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion");
                                                    if (Points <= 0)
                                                    {
                                                        i = 6; //Насильно заканчиваем цикл, иначе будут проблемы с пересчётом очков после сбегания!
                                                        UpdateStatus("# " + DateTime.Now + " Займусь ко я фитнессом, начну с упражнения \"рукивноги\" и бегом!");
                                                        frmMain.InvokeMember(MainWB, HtmlEl.GetElementsByTagName("button")[HtmlEl.GetElementsByTagName("button").Count - 1], "click"); //Убегаем
                                                    }
                                                    else //Перебор очков для убегания с миссии
                                                    {
                                                        if (Me.OilLeninHunting.AllowPartBilet && Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.partbilet")) >= Convert.ToInt32(frmMain.GetJavaVar(MainWB, "$(\"#content .part-bilet\").text()")))
                                                        {
                                                            while (Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.partbilet")) >= Convert.ToInt32(frmMain.GetJavaVar(MainWB, "$(\"#content .part-bilet\").text()")) && //Есть партбилеты?
                                                                (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion") - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion") < (int)frmMain.GetJavaVar(MainWB, "NeftLenin.price_gamble['" + frmMain.GetJavaVar(MainWB, "NeftLenin.stepMission") + "']") //Нужно скинуть подозрительность?
                                                                )
                                                            {
                                                                UpdateStatus("@ " + DateTime.Now + " Кости я уже покидал, пора партбилетами делиться!");
                                                                frmMain.GetJavaVar(MainWB, "NeftLenin.reset(2);");
                                                                IsWBComplete(MainWB, 500, 1500); //IsAjaxComplete(MainWB, 500, 1500);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            UpdateStatus("# " + DateTime.Now + " Ребят, я к вам позже загляну, не прощаюсь...");
                                                            Me.OilLeninHunting.NextDT = DateTime.Now.AddSeconds((int)frmMain.GetJavaVar(MainWB, "NeftLenin.timerSuspisionDecrease") + ((int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel") + Points - 1) * 60); //Новое время, когда очки спадут для нового поиска + дуэль
                                                        }
                                                        break;
                                                    }
                                                }
                                                IsWBComplete(MainWB, 2500, 4500); //IsAjaxComplete(MainWB, 2500, 4500);
                                            }
                                            GoToPlace(MainWB, Place.Oil); //Для обновления информации о вентилях, ибо после заданий вечные проблемки...
                                        }
                                        #endregion
                                        #endregion
                                        break;
                                }
                            }
                            else //Слишком сильные вентили, больше не ходим
                            {
                                if (Me.OilLeninHunting.Lvl == 31) Me.OilLeninHunting.NextDT = Me.OilLeninHunting.RestartDT; //Ленин повержен!
                                UpdateStatus("@ " + DateTime.Now + (Me.OilLeninHunting.Lvl == 31 ? " Эх, чудик в кЭпке какой-то помятый лежит... моя работа с бодуна!?!" : " Эээ не, эт крутые, ещё нагнут под забором ..."));
                                Me.OilLeninHunting.Stop = true;
                            }
                            #endregion
                        }
                        return true;
                    }                    
            }
            return false;
        } //OK?
        public void Casino(CasinoAction CA) //OK
        {
            BugReport("Casino");

            HtmlElement HtmlEl;

            switch (CA)
            {
                case CasinoAction.Kubovich:
                    GoToPlace(MainWB, Place.Casino, "/kubovich");
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementsByTagName("TABLE")[Settings.maxKubovichRotations <= 5 ? 1 : 2]; //В первой таблице цены до 5 игр, во второй от 6 до 10!
                    Regex regex = new Regex("(?<=([0-9])+[.]\\s*)([^\\s])+");
                    string sMax = regex.Matches(HtmlEl.InnerText)[(Settings.maxKubovichRotations <= 5 ? Settings.maxKubovichRotations : Settings.maxKubovichRotations - 5) - 1].Value;

                Rotate:
                    regex = new Regex("casino/kubovich/");
                    if (!regex.IsMatch(frmMain.GetDocumentURL(MainWB))) GoToPlace(MainWB, Place.Casino, "/kubovich"); //Кто-то мешает ... ?
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("push-ellow");
                    if (HtmlEl.GetAttribute("ClassName") == "button") //Желтый барабан
                    {
                        HtmlEl.InvokeMember("click");
                        UpdateStatus("@ " + DateTime.Now + " Потыкав Кубовича палицей, уговорил его на \"желтай\" барабан!");
                        IsWBComplete(MainWB, 500, 1000);
                    }

                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("push");
                    if (HtmlEl.GetAttribute("ClassName") == "button")
                    {
                        regex = new Regex("(?<= - )([0-9])*");
                        Me.Kubovich.Val = regex.Match(HtmlEl.InnerText).Value != "" ? Convert.ToInt32(regex.Match(HtmlEl.InnerText).Value) : 0; //Необходимо фишек, чтоб крутануть барабан!
                        if (Me.Kubovich.Val <= (sMax == "бесплатно" ? 0 : Convert.ToInt32(sMax))) //Ещё не докрутил до выставленного в настройках?
                        {
                            if (!Me.Fishki.Stop && Settings.BuyFishki && !Settings.BuyFishkiAllways && Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("fishki-balance-num").InnerText.Replace(",", "")) < Me.Kubovich.Val) //!Me.Fishki.Stop Дабы Бот не пытался второй раз купить фишки!
                            {
                                Casino(CasinoAction.BuyFishki); //Нехватает фишек на счету крутануть барабан?
                                GoToPlace(MainWB, Place.Casino, "/kubovich"); //Возвращаемся к Кубовичу.
                            }
                            if (Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("fishki-balance-num").InnerText.Replace(",", "")) >= Me.Kubovich.Val) //Фишек хватает крутим, ещё!
                            {
                                frmMain.GetDocument(MainWB).GetElementById("push").InvokeMember("click");
                                UpdateStatus("* " + DateTime.Now + " Раскрутил кубовича, на его барабан...");
                                IsWBComplete(MainWB, 20000, 30000); //задержка пока крутится барабан!
                                frmMain.RefreshURL(MainWB, Settings.ServerURL); //Необходимо, иначе нужно клацать кнопку ОК при получении приза!
                                IsWBComplete(MainWB);
                                goto Rotate; //Нужно снова крутануть?
                            }
                        }
                        Me.Kubovich.Stop = true; Me.Kubovich.LastDT = GetServerTime(MainWB); //На сегодня уже наигрался.
                    }
                    else //Кубович, ешё не пришел Кубович, ешё не пришел или уже обьелся.
                    {
                        Me.Kubovich.LastDT = GetServerTime(MainWB).AddHours(1); //Кубовича не застал, забежать через час!
                    }
                    break;
                case CasinoAction.Slots:
                    break;
                case CasinoAction.Loto:
                    GoToPlace(MainWB, Place.Casino, "/sportloto");
                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("button-prize-get");
                    if (HtmlEl != null) { HtmlEl.InvokeMember("click"); IsWBComplete(MainWB); }

                    HtmlEl = frmMain.GetDocument(MainWB).GetElementById("button-ticket-select");
                    if (HtmlEl.InnerText.IndexOf("Получить билетик - бесплатно") != -1)
                    {
                        HtmlEl.InvokeMember("click"); //Открываем поле для взятия билетика
                        IsWBComplete(MainWB, 300, 1000);
                        frmMain.GetDocument(MainWB).GetElementById("casino-sportloto-ticket-randomize").InvokeMember("click"); //Генерируем случайные числа.
                        IsWBComplete(MainWB, 300, 1000);
                        frmMain.GetDocument(MainWB).GetElementById("button-ticket-get").InvokeMember("click"); //Берём билетик.
                        UpdateStatus("* " + DateTime.Now + " Стырил, бесплатный билетик в Лото...");
                        IsWBComplete(MainWB, 300, 1000);
                    }
                    Me.Loto.Stop = true; Me.Loto.Val = 0; Me.Loto.LastDT = GetServerTime(MainWB); //Сегодня билетик уже взят.
                    break;
                case CasinoAction.BuyFishki:
                    GoToPlace(MainWB, Place.Casino);
                    frmMain.GetDocument(MainWB).GetElementById("stash-change-ore").SetAttribute("value", ((Settings.BuyFishkiAllways ? 200 : Settings.FishkiAmount) / 10).ToString()); //Ибо 1 руда -> 10 фишек!
                    frmMain.GetDocument(MainWB).GetElementById("button-change-ore").InvokeMember("click");
                    IsWBComplete(MainWB);
                    Me.Fishki.LastDT = GetServerTime(MainWB);
                    Me.Fishki.Stop = true;
                    //Вы успешно обменяли 20 на 200
                    //if (frmMain.GetDocument(MainWB).GetElementById("exchange-result-ore").InnerText == "Вы успешно обменяли 20 на 200");
                    break;
            }
        }
        public void Pyramid(PyramidAction PA)
        {
            BugReport("Pyramid");

            Match match;
            HtmlElement HtmlEl;           

            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/pyramid/")) GoToPlace(MainWB, Place.Pyramid);

            switch (PA)
            {
                case PyramidAction.Buy:
                    {
                        Me.Pyramid.Price = Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("pyramid_cost").InnerText.Replace(",", "")); //Цена покупок пирамидок.
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("buy_amount");
                        if (HtmlEl != null)
                        {
                            UpdateMyInfo(MainWB);
                            if ((Me.Wallet.Money / Me.Pyramid.Price) >= Settings.minPyramidAmount) //Вдруг на нас напали, ешё раз перепроверяем перед покупкой!
                            {
                                HtmlEl.SetAttribute("value", (Me.Wallet.Money / Me.Pyramid.Price).ToString());
                                frmMain.GetDocument(MainWB).GetElementById("pyramid-buy-cost-num").InvokeMember("click");
                                IsAjaxCompleteEx(MainWB, "pyramid-buy-cost-num", false);
                            }
                        }
                        break;
                    }
                case PyramidAction.Sell:
                    {
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("your_pyramids"); //Продавать пирамидки, только если они у меня вообще есть!                         
                        if (HtmlEl != null) //У меня есть пирамидки!
                        {
                            if (Convert.ToInt32(HtmlEl.InnerText) >= 1) //У меня их больше 1 штуки!
                            {
                                HtmlEl = frmMain.GetDocument(MainWB).GetElementById("pyramidbuttonsell");
                                if (HtmlEl != null)
                                {
                                    HtmlEl.InvokeMember("click");
                                    IsAjaxCompleteEx(MainWB, "pyramidbuttonsell", false);
                                }
                            }
                            else //Если у меня нет пирамидок, нечего пытаться их продать снова и снова!
                            {
                                Me.Pyramid.Done = true;
                                Me.Pyramid.RestartDT = GetServerTime(MainWB).Date.AddDays(1);
                            }
                        }
                        else Pyramid(PyramidAction.Check); //Видимо пирамида уже рухнула!
                        break;
                    }
                case PyramidAction.Check:
                    if (frmMain.GetDocument(MainWB).GetElementById("pyramid-crashed").Style != null) //Пирамида ешё не рухнула?
                    {
                        #region Инициализация
                        Me.Pyramid.Done = false;
                        Me.Pyramid.BlockMonya = Settings.BlockThimbles;
                        #endregion

                        Me.Pyramid.Price = Convert.ToInt32(frmMain.GetDocument(MainWB).GetElementById("pyramid_cost").InnerText.Replace(",", "")); //Цена покупок пирамидок.
                        if (Me.Pyramid.Price >= Settings.maxPyramidSell && GetServerTime(MainWB).TimeOfDay > new TimeSpan(0, 6, 0)) Pyramid(PyramidAction.Sell); //Розыск слишком велик, сливаем пирамидки!
                    }
                    else //Пирамида рухнула!
                    {
                        match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("pyramid-crashed").InnerText, "(?<=Новая пирамида стартует через )([0-9:])+");
                        Me.Pyramid.Done = true;
                        string[] Time = match.Value.Split(':'); //HH:mm:ss
                        Me.Pyramid.RestartDT = GetServerTime(MainWB).Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                        if (Me.Pyramid.RestartDT - GetServerTime(MainWB) > new TimeSpan(2, 0, 0)) Me.Pyramid.BlockMonya = false; //ещё более 2 часов до возможности заново считать цены, даём разрешение на слив денег Моне!
                        return; //Пирамида рухнула, уходим!
                    }
                    break;
            }
            //
            match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById("nextactiondt").InnerText, "([0-9:])+ ([0-9.])+");
            if (match.Success)
            {                
                if (PA == PyramidAction.Buy) UpdateStatus("$ " + DateTime.Now + " Накупил пирамидок, можно спать спокойно.");
                if (PA == PyramidAction.Sell) UpdateStatus("$ " + DateTime.Now + " Распродал ВСЕ пирамидки, можно спать спокойно.");
                Me.Pyramid.Done = true;
                Me.Pyramid.RestartDT = Convert.ToDateTime(match.Value, CultureInfo.CreateSpecificCulture("ru-RU"));
                if (Me.Pyramid.RestartDT - GetServerTime(MainWB) > new TimeSpan(2, 0, 0)) Me.Pyramid.BlockMonya = false; //ещё более 2 часов до возможности заново считать цены, даём разрешение на слив денег Моне!
            }
            else Me.Pyramid.RestartDT = GetServerTime(MainWB).TimeOfDay > new TimeSpan(0, 6, 0) ? GetServerTime(MainWB).AddDays(1).Date : GetServerTime(MainWB).AddMinutes(8); //Добавляем день, ибо сегодня цены уже были считаны или если был слишком рано то 10 минут!               
            if (Me.Pyramid.Price > Settings.maxPyramidPrice) Me.Pyramid.BlockMonya = false; //Слишом высокая цена, даём разрешение на слив денег Моне!
        }
        public bool Automobile(AutomobileAction AA, int RidePlace = 0)
        {
            BugReport("Automobile");

            int CarOffset;
            HtmlElement HtmlEl;
            string[] ArrInfo;
            Regex regex;
            Match match;
            MatchCollection matches;

            switch (AA)
            {
                case AutomobileAction.Check:
                    #region Check
                    #region Инициализация
                    Me.CarRide.Helicopter = 0; //У меня нет вертолётов
                    #endregion
                    if (!frmMain.GetDocumentURL(MainWB).Contains("automobile/ride/"))
                    {
                        GoToPlace(MainWB, Place.Automobile, "/ride", false); //Пробуем перейти к катанию машинок, но если машинок совсем нет...
                        if (!frmMain.GetDocumentURL(MainWB).EndsWith("automobile/ride/")) //машинок совсем нет...
                        {
                            Me.CarRide.Cars = null; //Машинок нет!
                            return false;
                        }
                    }
                    #region Сбор информации о локациях
                    matches = Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("content").InnerHtml, "direction-(?<Nr>([0-9])+)");
                    if (matches.Count > 0)                        
                    {
                        Array.Resize<DateTime>(ref Me.CarRide.RideTimeout, Convert.ToInt32((matches[matches.Count - 1]).Groups["Nr"].Value) + 1);
                        foreach (Match m in matches)
                        {
                            match = Regex.Match(frmMain.GetDocument(MainWB).GetElementById(m.Value).InnerHtml, "(?<=[>])([0-9:])+(?=[<])");
                            if (match.Success) //Локация на таймауте!
                            {
                                string[] Time = match.Value.Split(':'); //HH:mm:ss
                                Me.CarRide.RideTimeout[Convert.ToInt32(m.Groups["Nr"].Value)] = DateTime.Now.Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                            }
                            else Me.CarRide.RideTimeout[Convert.ToInt32(m.Groups["Nr"].Value)] = DateTime.Now.Date; //Локация свободна!
                        }
                    }
                    #endregion
                    #region Сбор информации о машинках
                    ArrInfo = GetArrClassHtml(MainWB, "$(\"#cars-trip-choose .object-thumb\");", "outerHTML");
                    if (ArrInfo.Count<string>() > 0)
                    {
                        Array.Resize<stcCar>(ref Me.CarRide.Cars, ArrInfo.Count<string>());
                        for (int i = 0; i < ArrInfo.Count<string>(); i++)
                        {                            
                            Me.CarRide.Cars[i].ID = Convert.ToInt32(Regex.Match(ArrInfo[i], "(?<=carid=\")([0-9])+(?=\")").Value);
                            Me.CarRide.Cars[i].Model = Convert.ToInt32(Regex.Match(ArrInfo[i], "(?<=model=\")([0-9])+(?=\")").Value);
                            Me.CarRide.Cars[i].Lvl = Convert.ToInt32(Regex.Match(ArrInfo[i], "(?<=level=\")([0-9])+(?=\")").Value);
                            match = Regex.Match(ArrInfo[i], "(?<=[>])([0-9:])+(?=[<])");
                            if (match.Success) //Машинка в сервис центре!
                            {
                                string[] Time = match.Value.Split(':'); //HH:mm:ss
                                Me.CarRide.Cars[i].Timeout = DateTime.Now.Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                            }
                            else Me.CarRide.Cars[i].Timeout = DateTime.Now.AddMinutes(-1); //Машинка свободна!
                            if (Enumerable.Range(41, 4).Contains(Me.CarRide.Cars[i].Model)) Me.CarRide.Helicopter++; //Запоминаем сколько у нас вертолётов, нужно учитывать при бомбёжке!
                        }
                        #region Резервировать машинку под поездки, и если да то какую?
                        DateTime ServerDT = GetServerTime(MainWB);
                        //Резервировать только если: катаемся и выбранная машинка совпала или не выбрана вовсе и либо через 7 часов понедельник либо уже понедельник и мы ещё не докотались!
                        if ((ServerDT.AddHours(7).DayOfWeek == DayOfWeek.Monday || (ServerDT.DayOfWeek == DayOfWeek.Monday && !Me.Automobile.Stop)) && Settings.UseCar)
                        {
                            for (int i = 0; i < Me.CarRide.Cars.Count<stcCar>(); i++)
                            {   //Нужная машинка найдена, или в случае когда нет специальной машинки, берём самую первую.
                                if (Me.CarRide.Cars[i].Reserved = !Settings.UseSpecialCar || (Settings.UseSpecialCar && Me.CarRide.Cars[i].Model == Settings.SpecialCar)) break;
                            }
                        }
                        #endregion                    
                        return true;
                    }
                    #endregion                  
                    #endregion
                    break;
                case AutomobileAction.Fuel:
                    #region Fuel
                    UpdateStatus("# " + DateTime.Now + " Опа ... бегу с канистрой на заправку!");
                    regex = new Regex("class=\"?(?<Unit>(ruda|neft))\"?[>](?<Cost>([0-9])+)");
                    foreach (HtmlElement H in frmMain.GetElementsById(MainWB, "alert-text"))
                    {
                        if (H.InnerText.Contains("С пустым баком далеко не уедешь.")) 
                        {
                            HtmlEl = H.Parent.All[2];
                            match = regex.Match(HtmlEl.InnerHtml);
                            if (match.Success)
                            {
                                UpdateMyInfo(MainWB);
                                if ((match.Groups["Unit"].Value == "ruda" ? Me.Wallet.Ore : Me.Wallet.Oil) >= Convert.ToInt32(match.Groups["Cost"].Value))
                                {
                                    HtmlEl.All[0].InvokeMember("onclick");
                                    Wait(1500, 2000);
                                    UpdateStatus("# " + DateTime.Now + " Да я \"карипскай колдун\" бензин нарисовался, отличненько.");
                                    return true;
                                }
                                else UpdateStatus("# " + DateTime.Now + " Нет финанса на горючее, неужели толкать придёться?!?");
                            }
                            break; //Это окошко о заправке, нет смысла искать дальше!
                        }
                    }             
                    #endregion
                    break;
                case AutomobileAction.Taxi:
                    #region Taxi
                    GoToPlace(MainWB, Place.Arbat);
                    regex = new Regex("(Садитесь за баранку).*.(попутчиков.)");
                    if (regex.IsMatch(frmMain.GetDocumentText(MainWB))) //Сегодня понедельник, катаем?
                    {
                        regex = new Regex("(?<=Бомбить - ).([0-9:])*"); //Этим можно считать время бомбёжки если таймер снизу не работает.
                        if (regex.IsMatch(frmMain.GetDocumentText(MainWB))) //Есть машина, можно ехать!
                        {
                            #region Поиск машинки для бомбёжки.
                            ArrInfo = GetArrClassHtml(MainWB, "$(\"#cars-trip-choose .object-thumb\");", "outerHTML");
                            #region Количество машинок изменилось?
                            if (Me.CarRide.Cars == null ? true : ArrInfo.Count<string>() != Me.CarRide.Cars.Count<stcCar>() - Me.CarRide.Helicopter) //На вертолётах нельзя бомбить, не учитываем их!
                            {
                                if (Automobile(AutomobileAction.Check)) //Перепроверяю количество машинок
                                {
                                    GoToPlace(MainWB, Place.Arbat);
                                }
                                else //Покачто совсем нет машинки, может быть будет через час?
                                {
                                    Me.Automobile.LastDT = GetServerTime(MainWB).AddHours(1);
                                    return false;
                                }
                            }
                            #endregion
                            #region Инизиализация
                            CarOffset = -1;
                            TimeSpan TS = new TimeSpan();
                            #endregion
                            for (int i = 0; i < ArrInfo.Count<string>(); i++)
                            {                                
                                for (int x = 0; x < Me.CarRide.Cars.Count<stcCar>(); x++)
                                {
                                    if (Me.CarRide.Cars[x].ID == Convert.ToInt32(Regex.Match(ArrInfo[i], "(?<=carid=\")([0-9])+(?=\")").Value))
                                    {
                                        Me.CarRide.Cars[x].RideTime = TimeSpan.Parse("00:" + Regex.Match(ArrInfo[i], "(?<=time=\")([0-9:])+(?=\")").Value);
                                        match = Regex.Match(ArrInfo[i], "(?<=[>])([0-9:])+(?=[<])");
                                        if (match.Success) //Машинка в сервис центре!
                                        {
                                            string[] Time = match.Value.Split(':'); //HH:mm:ss
                                            Me.CarRide.Cars[x].Timeout = DateTime.Now.Add(new TimeSpan(Convert.ToInt32(Time[0]), Convert.ToInt32(Time[1]), Convert.ToInt32(Time[2]))); //Иначе, если больше 24 часов, вылетает ошибка конвертирования.
                                        }
                                        else Me.CarRide.Cars[x].Timeout = DateTime.Now.AddMinutes(-1); //Машинка свободна!

                                        #region
                                        bool SpecialCar;
                                        switch (Settings.SpecialCar)
                                        {
                                            #region Чайка
                                            case 20:
                                                SpecialCar = Me.CarRide.Cars[x].Model == 24;
                                                break;
                                            #endregion
                                            #region Тигр
                                            case 21:
                                                SpecialCar = Me.CarRide.Cars[x].Model == 25;
                                                break;
                                            #endregion
                                            #region Новогодний грузовик
                                            case 29:
                                                SpecialCar = Me.CarRide.Cars[x].Model == 40;
                                                break;
                                            #endregion
                                            #region Конь
                                            case 30:
                                                SpecialCar = Me.CarRide.Cars[x].Model == 29;
                                                break;
                                            #endregion
                                            #region Броневик *-*****
                                            case 31:
                                                SpecialCar = Enumerable.Range(30, 5).Contains(Me.CarRide.Cars[x].Model);
                                                break;
                                            #endregion
                                            #region Эвакуатор
                                            case 32:
                                                SpecialCar = Me.CarRide.Cars[x].Model == 37;
                                                break;
                                            #endregion
                                            #region Де-Лориан *-**
                                            case 33:
                                                SpecialCar = Enumerable.Range(45, 2).Contains(Me.CarRide.Cars[x].Model);
                                                break;                                          
                                            #endregion                                           
                                            default:
                                                SpecialCar = Me.CarRide.Cars[x].Model == Settings.SpecialCar;
                                                break;
                                        }                                            
                                        #endregion

                                        if (Settings.UseSpecialCar && SpecialCar) //Кататься на определённой машинке, и она найдена!
                                        {
                                            Me.Automobile.Val = x; //Индекс машинки в массиве машинок на которой будем ездить.
                                            CarOffset = i; //Сохраняем индекс нужной нам машинки в массиве выбора!
                                            i = ArrInfo.Count<string>(); //Больше искать не нужно, помогаем закончить поиск!
                                            break; //Выходим, всё найдено!
                                        }                                    
                                        
                                        if ((TS == new TimeSpan() || TS > Me.CarRide.Cars[x].RideTime) && Me.CarRide.Cars[x].Timeout <= DateTime.Now) //Либо специальная машинка не выбрана, либо она попросту недоступна. (break в прошлой функции гарантирует поездку на выбраной машинке!)
                                        {
                                            Me.Automobile.Val = x; //Индекс машинки в массиве машинок на которой будем ездить.
                                            TS = Me.CarRide.Cars[x].RideTime; //Перезаписываем минимальное время поездки.
                                            CarOffset = i; //Сохраняем индекс нужной нам машинки в массиве выбора!                                          
                                        }
                                        break; //Раз уж дошагал до сюда, значит машинка была найдена и обработана, нет смысла искать её дальше
                                    }
                                }
                            }
                            if (CarOffset != -1) //Есть свободная машинка, способная совершить поездку?
                            {
                                #region Выбор машинки с подтверждением
                                foreach (HtmlElement H in frmMain.GetElementsById(MainWB, "alert-title")) //Ибо в последнее время информация о бонусах тоже под таким ИД вылазит!
                                {
                                    if (H.InnerText == "Выберите тачку для поездки")
                                    {
                                        H.Parent.GetElementsByTagName("IMG")[CarOffset].InvokeMember("click"); //выбираю лучшую возможную машинку для поездки.
                                        IsWBComplete(MainWB, 500, 1000);
                                    }
                                }
                                #endregion
                            }
                            //######################################################################################################                            
                            #endregion
                            #region Можно забирать бонус?
                            HtmlEl = frmMain.GetDocument(MainWB).Forms[1];
                            if (HtmlEl.GetElementsByTagName("button")[0].GetAttribute("classname") == "button" & HtmlEl.GetElementsByTagName("button")[0].InnerText.Contains("Бонус")) //Получаем приз!
                            {
                                frmMain.InvokeMember(MainWB, HtmlEl, "submit");
                                UpdateStatus("@ " + DateTime.Now + " Опачки чемоданчеГ, на заднем сидении нарылся=)");
                                IsWBComplete(MainWB);
                            }
                            #endregion
                            #region  Уже наездил желаемые баллы, хватит?
                            regex = new Regex("(?<=Баллов набрано: )(?<Points>([0-9])+) из ([0-9])+");
                            match = regex.Match((string)frmMain.GetJavaVar(MainWB,"$(\"#content .progress .num\").text()"));
                            
                            switch (Settings.CarPrize)
                            {
                                case 1: if (Convert.ToInt32(match.Groups["Points"].Value) >= 160) goto default;
                                    break;
                                case 2: if (Convert.ToInt32(match.Groups["Points"].Value) >= 400) goto default;
                                    break;
                                case 3: if (Convert.ToInt32(match.Groups["Points"].Value) >= 640) goto default;
                                    break;
                                case 4: if (Convert.ToInt32(match.Groups["Points"].Value) >= 960) goto default;
                                    break;
                                case 5: if (Convert.ToInt32(match.Groups["Points"].Value) >= 1320) goto default;
                                    break;
                                case 6: if (Convert.ToInt32(match.Groups["Points"].Value) >= 1750) goto default;
                                    break;
                                default: { Me.Automobile.LastDT = GetServerTime(MainWB).AddDays(1).Date; Me.Automobile.Stop = true; return false; }
                            }
                            #endregion
                            #region Сервисцентр?
                            if (Me.CarRide.Cars[Me.Automobile.Val].Timeout > DateTime.Now) //Машина всё ешё не доступна. 
                            {
                                UpdateStatus("! " + DateTime.Now + " Сел за руль, а колеса то спиздили! Жду до: " + Me.CarRide.Cars[Me.Automobile.Val].Timeout.ToString("HH:mm:ss"));
                                Me.Automobile.LastDT = GetServerTime(MainWB).Add(Me.CarRide.Cars[Me.Automobile.Val].Timeout - DateTime.Now);
                                return true;
                            }
                            #endregion
                            frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).Forms[0], "submit"); //Старт бомбёжки!
                            UpdateStatus("# " + DateTime.Now + " Ну, что красивая поехали кататься?");
                            IsWBComplete(MainWB);
                        }

                        #region Закончилось горючее, и всё ешё нужно покататься?
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-text");
                        if (HtmlEl != null && HtmlEl.InnerText.Contains("С пустым баком далеко не уедешь.")) //С пустым баком далеко не уедешь.
                        {
                            if (Automobile(AutomobileAction.Fuel)) return Automobile(AutomobileAction.Taxi); //Если удачно заправились, пробуем поехать заново. 
                            else Me.Automobile.LastDT = GetServerTime(MainWB).AddMinutes(30); //Не получилось заправить машину, попробуем через 30 минут!                                
                        }
                        #endregion

                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("cooldown");
                        if (HtmlEl != null) //Осуществляется поездка.
                        {
                            TimeSpan TS = new TimeSpan();
                            TS = TimeSpan.TryParse(HtmlEl.InnerText, out TS) ? TS : Me.CarRide.Cars[Me.Automobile.Val].RideTime;
                            UpdateStatus("# " + DateTime.Now + " УУУУм хорошенькая попалась..., покатаюсь до: " + DateTime.Now.Add(TS).ToString("HH:mm:ss")); //Convert.ToDateTime(frmMain.GetDocument(MainWB).GetElementById("cooldown").InnerText).TimeOfDay
                            Me.Automobile.LastDT = GetServerTime(MainWB).Add(TS); //Convert.ToDateTime(frmMain.GetDocument(MainWB).GetElementById("cooldown").InnerText).TimeOfDay                         
                            return true;
                        }
                    }
                    else //Сегодня не понедельник, стоп!
                    {
                        if (GetServerTime(MainWB).DayOfWeek == DayOfWeek.Monday)
                        {                            
                            if (Me.CarRide.Cars.Count<stcCar>() - Me.CarRide.Helicopter == 0)
                            {
                                UpdateStatus("@ " + DateTime.Now + "Шеф, у нас даже велосипеда походу нет! А на горбу я пассажира не повезу, даже не проси!");
                                Me.Automobile.LastDT = GetServerTime(MainWB).AddMinutes(30); //Понедельник, нет машинки!
                            }
                            else Me.Automobile.LastDT = GetServerTime(MainWB).AddMinutes(5); //Понедельник, пришли слишком рано...
                        }
                        else
                        {
                            Me.Automobile.LastDT = GetServerTime(MainWB).Date;
                            Me.Automobile.Stop = true;
                        }
                    }
                    #endregion
                    break;
                case AutomobileAction.Ride:
                    #region Ride
                    if (Automobile(AutomobileAction.Check))
                    {   //При заблокированной локации или если поездка уже совершена, текст начинается с "энтера"
                        HtmlEl = frmMain.GetDocument(MainWB).GetElementById("direction-" + RidePlace);
                        if (Me.CarRide.RideTimeout[RidePlace] < DateTime.Now && HtmlEl != null) //Поездка ещё не произведена?
                        {
                            #region Выбиваем MessageBox с машинками!
                            frmMain.GetJavaVar(MainWB, "var Car = $(\"#direction-" + RidePlace + " .car-choose-link\"); Car.click();");
                            IsAjaxCompleteEx(MainWB, "alert-title");                            
                            #endregion
                            #region Подбор машинки для поездки
                            CarOffset = -1;
                            stcCar Car;
                            switch (RidePlace)
                            {
                                #region Чайка
                                case 20:
                                    Car = new stcCar { Lvl = -1, Model = 24 };
                                    break;
                                #endregion
                                #region Тигр
                                case 21: 
                                    Car = new stcCar { Lvl = -1, Model = 25 };
                                    break;
                                #endregion
                                #region Новогодний грузовик
                                case 25: 
                                    Car = new stcCar { Lvl = -1, Model = 40 };
                                    break;
                                #endregion
                                #region Конь
                                case 26:
                                    Car = new stcCar { Lvl = -1, Model = 29 };
                                    break;
                                #endregion
                                #region Броневик *-*****
                                case 27:
                                case 28:
                                case 29:
                                case 30:
                                case 31:
                                    Car = new stcCar { Lvl = -1, Model = RidePlace + 3 }; //Бронивики начинаются с 27-31 поездки 30-34 модели.
                                    break;
                                #endregion                                
                                #region Эвакуатор
                                case 34: 
                                    Car = new stcCar { Lvl = -1, Model = 37 };
                                    break;
                                #endregion
                                #region Вертолёт «Борт №1»
                                case 37:
                                    Car = new stcCar { Lvl = -1, Model = 43 };
                                    break;
                                #endregion
                                #region Вертолёт «Черная акула»
                                case 38:
                                    Car = new stcCar { Lvl = -1, Model = 44 };
                                    break;
                                #endregion
                                #region Де-Лориан
                                case 39:
                                    Car = new stcCar { Lvl = -1, Model = 45 };
                                    break;
                                #endregion
                                #region Де-Лориан ускоренный
                                case 40:
                                    Car = new stcCar { Lvl = -1, Model = 46 };
                                    break;
                                #endregion
                                #region Все иные поездки
                                default:
                                    Car = new stcCar { Lvl = 1000, Model = 1000 };
                                    break;
                                #endregion
                            }
                            for (int i = 0; i < Me.CarRide.Cars.Count<stcCar>(); i++)
                            {   //Машинка может поехать по уровню, она не зарезервированна и является самой слабенькой для этой поездки или это специальная поездка тогда едем по модели машинки                                 
                                if (!Me.CarRide.Cars[i].Reserved && Me.CarRide.Cars[i].Timeout <= DateTime.Now && (Car.Lvl == -1 ? Car.Model == Me.CarRide.Cars[i].Model : Me.CarRide.Cars[i].Lvl >= RidePlace && Me.CarRide.Cars[i].Lvl <= Car.Lvl && Me.CarRide.Cars[i].Model < Car.Model)) 
                                {
                                    CarOffset = i;   //Запоминаем её индекс
                                    Car.Lvl = Me.CarRide.Cars[i].Lvl;  //Запоминаем уровень найденой машинки
                                    if (Car.Lvl != -1) Car.Model = Me.CarRide.Cars[i].Model;  //Запоминаем модель найденой машинки (не специальная поездка)
                                }  
                            }
                            if (CarOffset != -1) //Есть свободная машинка, способная совершить поездку или дефаульт (как например бронивик у него поездка 27 а Lvl 0)
                            {
                                #region Выбор машинки с подтверждением
                                foreach (HtmlElement H in frmMain.GetElementsById(MainWB, "alert-title")) //Ибо в последнее время информация о бонусах тоже под таким ИД вылазит!
                                {
                                    if (H.InnerText == "Выберите тачку для поездки") 
                                    {
                                        H.Parent.GetElementsByTagName("IMG")[CarOffset].InvokeMember("click"); //выбираю лучшую возможную машинку для поездки.                                     
                                        IsWBComplete(MainWB, 500, 1000);
                                    }          
                                }
                                #endregion                                                                
                            }
                            #endregion

                            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("direction-" + RidePlace).GetElementsByTagName("BUTTON")[0]; //Кнопка "поездка"
                            if (HtmlEl.GetAttribute("classname") == "button ride-button") //Можно поехать? (Если нет, то уже была произведена поездка!)
                            {
                                frmMain.InvokeMember(MainWB, HtmlEl, "click");
                                IsWBComplete(MainWB, 500, 1000);
                                #region Закончилось горючее, и всё ешё нужно покататься?
                                foreach (HtmlElement H in frmMain.GetElementsById(MainWB, "alert-text"))
                                {
                                    if (H.InnerText.Contains("С пустым баком далеко не уедешь.") && Automobile(AutomobileAction.Fuel)) return Automobile(AutomobileAction.Ride, RidePlace); //Если удачно заправились, пробуем поехать заново.
                                }                                
                                #endregion
                                return true; //Порядок, машинка уехала!
                            }
                        }
                        else UpdateStatus("@ " + DateTime.Now + " Шеф пиши - локация №" + RidePlace + " недоступна до: " + (Me.CarRide.RideTimeout[RidePlace] - DateTime.Now).ToString("hh\\:mm\\:ss") + "! Брось, я запомнил, но машинку запустить не могу!");
                    }
                    #endregion
                    break;
            }
            return false; //Если дошел - что-то было не так!
        }
        public bool IsHPLow(WebBrowser WB, decimal minHP, bool WaitForHP = false) //OK
        {
            BugReport("IsHPLow");

            int i = 0;

            GetMyLife(WB);
            minHP = minHP > 100 ? 100 : minHP; //Fix: когда вися на крысомахе пользователь меняет её максимальный уровень
            if (WaitForHP)
            {
                while ((Convert.ToDouble(Me.Player.LifePkt[0]) / Convert.ToDouble(Me.Player.LifePkt[1]) * 100) < Convert.ToDouble(minHP))
                {
                    if (i++ == 0) UpdateStatus("@" + DateTime.Now + " Ожидание востановления жизней перед боем.");
                    Wait(60000, 90000);
                    GetMyLife(WB);
                }
                return false; //Время истекло, или выход ибо бот был выключен! 
            }
            else return Convert.ToDouble(Me.Player.LifePkt[0]) / Convert.ToDouble(Me.Player.LifePkt[1]) * 100 < Convert.ToDouble(minHP);
        }
        public bool CheckHealthEx(decimal HealMe50 = 40, decimal HealMe100 = 10, decimal HealPet50 = 40, decimal HealPet100 = 10, bool LockHideMe = false) //OK
        {
            BugReport("CheckHealthEx");

            Match match;
            HtmlElement HtmlEl;
            HtmlElementCollection HC;
            double dLifePrc;
            bool[] bRet = { true, true }; //Инизиализация
            
          ReTry:
            IsWBComplete(MainWB);
            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player, "", false);
            if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) return false; //Видимо вишу на крыске, раз не смог перейти на страничку игрока.


            #region Извлекаем жизни пэта / проверка + замена любимого пэта.
            HC = frmMain.GetDocument(MainWB).GetElementsByTagName("tbody")[0].GetElementsByTagName("ul")[1].GetElementsByTagName("li"); //Информация о шмотках на мне + питомец
            match = Regex.Match(HC[HC.Count - 2].InnerHtml, "/pets/(?<PetType>([0-9])+)-[0-9].png"); //Именно этот питомец сейчас со мной (Картинка с боевым питомцем в предпоследнем элементе, последний беговой при условии показывать)
            if (!match.Success) match = Regex.Match(HC[HC.Count - 1].InnerHtml, "/pets/(?<PetType>([0-9])+)-[0-9].png"); //Именно этот питомец сейчас со мной (Картинка с питомцем в последнем элементе коллекции)
            #region Определение типа боевого питомца
                int UsePetType = getPetInformation(stcPetType.War).type;
            #endregion
            #region Проверка любимый ли с нами питомец?
            if (UsePetType != 0 && UsePetType != Convert.ToInt32(match.Groups["PetType"].Value) && Me.Events.NextSetWarPetDT < GetServerTime(MainWB))
            {
                Petarena(PetAction.SetWarPet);
                goto ReTry;
            }
            #endregion
            foreach (HtmlElement PetHtml in frmMain.GetDocument(MainWB).GetElementById("pet-accordion").GetElementsByTagName("img"))
            {
                if (PetHtml.GetAttribute("src").EndsWith(match.Value)) //Тот же самый питомец, что сейчас со мной?
                {
                    string PetInfo = (string)frmMain.GetJavaVar(MainWB, "m.items['" + PetHtml.GetAttribute("data-id") + "'].info.content");
                    match = Regex.Match(PetInfo, "(?<=Жизни: )([0-9/])+");
                    Me.WarPet.LifePkt = match.Success ? match.Value.Split('/') : null; //Быть может у меня есть только беговой питомец?
                    break;
                }
            }
            if (Me.WarPet.LifePkt != null) //
            {
                dLifePrc = Convert.ToDouble(Me.WarPet.LifePkt[0]) / Convert.ToDouble(Me.WarPet.LifePkt[1]) * 100;
                if (dLifePrc < Convert.ToDouble(HealPet100)) bRet[0] = EatDrog(MainWB, ShopItems.Pet100) ? true : EatDrog(MainWB, ShopItems.Pet50); //Нет больше 100% корма? Едим хотябы 50%
                else if (dLifePrc < Convert.ToDouble(HealPet50)) bRet[0] = EatDrog(MainWB, ShopItems.Pet50);  //Присваиваем значение возвращённое функцией
            }
            if (!bRet[0] && !Settings.BuyHealPet) UpdateStatus("! " + DateTime.Now + " Не смотри на меня так, мерзкое животное, мне запрещено покупать тебе корм!");
            #endregion
            #region Извлекаем мои жизни.
            GetMyLife(MainWB);
            dLifePrc = Convert.ToDouble(Me.Player.LifePkt[0]) / Convert.ToDouble(Me.Player.LifePkt[1]) * 100;            
            if (dLifePrc < Convert.ToDouble(HealMe100)) bRet[1] = EatDrog(MainWB, ShopItems.Me100) ? true : EatDrog(MainWB, ShopItems.Me50);  //Нет больше микстур? Попробуем сиропами. 
            else if (dLifePrc < Convert.ToDouble(HealMe50)) bRet[1] = EatDrog(MainWB, ShopItems.Me50);  //Присваиваем значение возвращённое функцией и значение прошлой функции
            if (!bRet[1] && !Settings.BuyHealMe) UpdateStatus("! " + DateTime.Now + " Рана видимо сама зарастёт, лечилки мне докупать запретили!");
            #endregion 
           
            #region Травма?
            Me.Trauma.Stop = Trauma(TraumaAction.Check);
            #endregion 
            #region Выбит слот?
            if (Settings.HealInjuredSlot)
            {
                foreach (string InjuredSlot in GetArrClassHtml(MainWB, "$(\"#content .injured\")", "className"))
                {
                    TimeSpan TS = new TimeSpan();
                    object Info = frmMain.GetJavaVar(MainWB, "$(\"#content ." + InjuredSlot.Replace(" ", ".") + " .time\").text()");
                    if (TimeSpan.TryParse((string)Info, out TS)) Me.Events.NextSlotInjuredDT = DateTime.Now.Add(TS - new TimeSpan(23, 0, 0)); //Время возможного седующего лечения!
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Ore >= 30 && TS < new TimeSpan(23, 0, 0)) //Час уже прошел, можно лечить!
                    {
                        UpdateStatus("@ " + DateTime.Now + " Ах была-не-была, пытаюсь востановить выбитый слот!");
                        //doTreatSlot(slot, treatType, dom)
                        frmMain.InvokeScript(MainWB, "doTreatSlot", new object[] { (string)frmMain.GetJavaVar(MainWB, "$(\"#content ." + InjuredSlot.Replace(" ", ".") + " .dashedlink\").attr('slot')"), "medicine_chest", frmMain.GetJavaVar(MainWB, "$(\"#content ." + InjuredSlot.Replace(" ", ".") + " .dashedlink\")") });
                        IsWBComplete(MainWB, 1000, 1500); //IsAjaxComplete(MainWB, 1000, 1500);
                    }
                    else UpdateStatus("@ " + DateTime.Now + " вылечиться пока не могу, загляну попозже к: " + Me.Events.NextSlotInjuredDT);
                }
            }            
            #endregion
            #region Действуют Антилампы?
            DateTime ServerDT = GetServerTime(MainWB);
            HtmlEl = frmMain.GetDocument(MainWB).GetElementById("perks-popup");
            foreach (HtmlElement H in HtmlEl.GetElementsByTagName("SPAN"))
            {
                if (Me.PerkAntiLamp.SwitchOffDT < ServerDT)
                {
                    match = Regex.Match(HtmlEl.InnerHtml, "sovet7.png([\\S\\s])+>(?<Time>([0-9:])+)<"); //<img src="/@/images/obj/perks/sovet7.png" tooltip="1" data-id=""><div class="time" endtime="1355416319" timer="11110">03:05:11</div>
                    if (match.Success)
                    {
                        Me.PerkAntiLamp.SwitchOffDT = ServerDT.Add(TimeSpan.Parse(match.Groups["Time"].Value));
                        UpdateStatus("@ " + DateTime.Now + (Settings.Lampofob ? " Ухты" : " Мл@") + ", кто-то свет в подъезде вкрутил.");
                        Me.PerkAntiLamp.On = true;
                        break;
                    }
                }
            }            
            if (Me.PerkAntiLamp.On && (HtmlEl == null || ServerDT >= Me.PerkAntiLamp.SwitchOffDT))
            {
                UpdateStatus("@ " + DateTime.Now + (Settings.Lampofob ? " Увы, но" : " Наконецто,") + " лампочка сгорела ...");
                Me.PerkAntiLamp.On = false;
            }
            #endregion  
            #region Я заказан в Клубе?
            if (frmMain.GetJavaVar(MainWB, "$(\"#content .hunting-report\").html()") != DBNull.Value)
            {
                if (!Me.Wanted && !LockHideMe) HideMeFromHC(); 
            }
            else Me.Wanted = false;
            #endregion
            return bRet[0] && bRet[1]; //Если, хоть одна функция сорвалась вернём false
        }
        public bool HealMePlus()
        {
            BugReport("HealMePlus");
            
            object info;
            DateTime MonitorDT = DateTime.Now.AddSeconds(Convert.ToDouble(Settings.GagIE));
            frmMain.GetJavaVar(MainWB, "var $Ret; $.get(\"/player/checkhp/\", function(data) { if (data['sirop'] || data['mikstura']) { $.post(\"/player/checkhp/\", {\"action\": \"restorehp\"}, function(data) { $Ret = data['result']; if (data['result'] != 0) { setHP(maxhp); }; }, \"json\"); } else { $.post(\"/player/restorehp/\", {\"action\": \"restorehp\"}, function(data) { $Ret = data['result']; if (data['result'] != 0) { setHP(maxhp); }; }, \"json\"); }});");
            MainWB.Tag = "Ajax";
            IsWBComplete(MainWB);
            do
            {
                if (MonitorDT < DateTime.Now) return false;
                Wait(1000, 1500);
                info = frmMain.GetJavaVar(MainWB, "$Ret");                
            }
            while (info == null && MonitorDT > DateTime.Now);            
            if (Convert.ToInt32(info) != 0)
            {
                UpdateStatus("* " + DateTime.Now + " Перебинтовался, сменил подгузники, я готов!");
                return true;
            }
            else return false;                       
        }
        public bool TonusMePlus()
        {
            BugReport("TonusMePlus");

            UpdateMyInfo(MainWB);
            frmMain.InvokeScript(MainWB, "jobShowTonusAlert");
            IsAjaxCompleteEx(MainWB, "alert-title");
            Wait(300, 600);
            foreach (HtmlElement H in frmMain.GetDocument(MainWB).GetElementsByTagName("button"))
            {
                Match match = Regex.Match(H.InnerHtml, "class=\"tonus\".*class=\"?(?<Unit>(tugriki|ruda|neft|med))\"?[>](?<Cost>([0-9])+)");
                if (match.Success && match.Groups["Unit"].Value == "ruda" && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value))
                {
                    frmMain.InvokeMember(MainWB, H, "click");
                   // H.InvokeMember("click"); //ajax иначе не сработает IsWBComplete
                    UpdateStatus("* " + DateTime.Now + " Заправился энергией, пойду снова горы вертеть!");
                    IsWBComplete(MainWB); //IsAjaxComplete(MainWB);                    
                    return true;
                }
                if (Expert.QuestUseAllTonusBottle && H.InnerText.Contains("«Тонус+»"))
                {
                    frmMain.InvokeMember(MainWB, H, "onclick");
                    // H.InvokeMember("click"); //ajax иначе не сработает IsWBComplete
                    UpdateStatus("* " + DateTime.Now + " Босс разрешил пить всё! Глотаю что под рукой и погнали!");
                    IsWBComplete(MainWB); //IsAjaxComplete(MainWB);                    
                    return true;
                }
            }
            frmMain.GetJavaVar(MainWB, "var $Alert = $(\"#alert-title\"); $($Alert).parents(\"div.alert\").remove();"); //Удаляем весь блок Alert, чтоб не мешал!                
            return false;
        }
        public bool BuyItems(WebBrowser WB, ShopItems SI, string[] Options = null) //NOK Nuzhno eshe proverit' vse li ID pravil'nye
        {
            BugReport("BuyItems");

            int Amount = 1;
            Match match;
            HtmlElement HtmlButton = null;

            UpdateMyInfo(WB); //Считываем финансовое положение

            #region Блокировка многократных покупок одинаковых вещей подряд!
            if (SI != ShopItems.Chain && DateTime.Now - LastBuy.LastDT < new TimeSpan(0, 5, 0) && SI == LastBuy.LastSI) LastBuy.Counter++; //Ислкючаем цепочки из контроля закупок, ибо их может понадобиться очень много.
            else LastBuy = new stcLastBuy { LastSI = SI, LastDT = DateTime.Now, Counter = 0 };
            if (LastBuy.Counter >= 3) { UpdateStatus("~ " + DateTime.Now + " Что-то я зачастил по магазинам ..., какбы сумками пукан не надорвать, хватит!"); return false; }
            #endregion

            switch (SI)
            {
                #region Шоко-чай
                case ShopItems.Tea1:
                    if (Me.Wallet.Money >= 100 && Me.Wallet.Ore >= 5 && Me.Player.Level >= 1)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_13"); break;
                    }
                    else return false;
                case ShopItems.Shoko1:
                    if (Me.Wallet.Money >= 100 && Me.Wallet.Ore >= 5 && Me.Player.Level >= 1)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_10"); break;
                    }
                    else return false;
                case ShopItems.Tea4:
                    if (Me.Wallet.Money >= 500 && Me.Wallet.Ore >= 9 && Me.Player.Level >= 4)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_14"); break;
                    }
                    else return false;
                case ShopItems.Shoko4:
                    if (Me.Wallet.Money >= 500 && Me.Wallet.Ore >= 9 && Me.Player.Level >= 4)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_11"); break;
                    }
                    else return false;
                case ShopItems.Tea7:
                    if (Me.Wallet.Money >= 1000 && Me.Wallet.Ore >= 9 && Me.Player.Level >= 7)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_15"); break;
                    }
                    else return false;
                case ShopItems.Shoko7:
                    if (Me.Wallet.Money >= 1000 && Me.Wallet.Ore >= 9 && Me.Player.Level >= 7)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_12"); break;
                    }
                    else return false;
                case ShopItems.Tea10:
                    if (Me.Wallet.Money >= 2500 && Me.Wallet.Ore >= 15 && Me.Wallet.Oil >= 25 && Me.Player.Level >= 10)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_16"); break;
                    }
                    else return false;
                case ShopItems.Shoko10:
                    if (Me.Wallet.Money >= 2500 && Me.Wallet.Ore >= 15 && Me.Wallet.Oil >= 25 && Me.Player.Level >= 10)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_17"); break;
                    }
                    else return false;
                case ShopItems.Tea15:
                    if (Me.Wallet.Money >= 5000 && Me.Wallet.Ore >= 40 && Me.Wallet.Oil >= 100 && Me.Player.Level >= 15)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_18"); break;
                    }
                    else return false;
                case ShopItems.Shoko15:
                    if (Me.Wallet.Money >= 5000 && Me.Wallet.Ore >= 40 && Me.Wallet.Oil >= 100 && Me.Player.Level >= 15)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("chocolates_19"); break;
                    }
                    else return false;
                #endregion
                case ShopItems.Valujki:
                    if (Me.Wallet.Ore >= 10 && Me.Player.Level >= 3)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("gift_valuyki"); break;
                    }
                    else return false;
                case ShopItems.ValujkiAdv:
                    if (Me.Wallet.Ore >= 40 && Me.Player.Level >= 10)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("gift_valuyki_he"); break;
                    }
                    else return false;
                case ShopItems.GasMask:
                    if (Me.Wallet.Ore >= 9 && Me.Player.Level >= 3)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("gift_protivogaz"); break;
                    }
                    else return false;
                case ShopItems.Respirator:
                    if (Me.Wallet.Ore >= 9 && Me.Player.Level >= 3)
                    {
                        GoToPlace(WB, Place.Shop, "/section/gifts");
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("gift_resperator"); break;
                    }
                    else return false;
                case ShopItems.Me50: //Сироп +50% жизни
                    if (Settings.BuyHealMe && Me.Wallet.Money >= 50)
                    {
                        GoToPlace(WB, Place.Shop);
                        Amount = Me.Wallet.Money / 50; //Высчитываем, сколько можно купить макс?
                        Amount = Convert.ToInt32(Amount > Settings.AmountHealMe ? Settings.AmountHealMe : Amount); //Если не можем купить столько, сколько просили... покупаем, сколько можно.
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("sirop"); break;
                    }
                    else return false;
                case ShopItems.Me100: //Микстура +100% жизни
                    if (Settings.BuyHealMe && Me.Wallet.Money >= 100)
                    {
                        GoToPlace(WB, Place.Shop);
                        Amount = Me.Wallet.Money / 100; //Высчитываем, сколько можно купить макс?
                        Amount = Convert.ToInt32(Amount > Settings.AmountHealMe ? Settings.AmountHealMe : Amount); //Если не можем купить столько, сколько просили... покупаем, сколько можно.
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("mikstura"); break;
                    }
                    else return false;
                case ShopItems.Pet50:
                    if (Settings.BuyHealPet && Me.Wallet.Ore >= 5)
                    {
                        GoToPlace(WB, Place.Shop, "/section/zoo"); HtmlButton = frmMain.GetDocument(WB).GetElementById("petfood_50"); break; //Собачий корм +50%
                    }
                    else return false;
                #region Gums
                case ShopItems.Gum1: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_health2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum2: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_strength2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum3: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_dexterity2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum4: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_resistance2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum5: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_intuition2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum6: //
                    if (Me.Wallet.Ore >= 3)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_attention2"))
                        {
                            match = Regex.Match(H.InnerHtml, "ruda\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за руду, самые дорогие!)
                            if (match.Success && Me.Wallet.Ore >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum1Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_health"); break;
                    }
                    else return false;
                case ShopItems.Gum2Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_strength"); break;
                    }
                    else return false;
                case ShopItems.Gum3Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_dexterity"); break;
                    }
                    else return false;
                case ShopItems.Gum4Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_resistance"); break;
                    }
                    else return false;
                case ShopItems.Gum5Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_intuition"); break;
                    }
                    else return false;
                case ShopItems.Gum6Ex: //
                    if (Me.Wallet.Ore >= 12)
                    {
                        GoToPlace(WB, Place.Shop); HtmlButton = frmMain.GetDocument(WB).GetElementById("gum_attention"); break;
                    }
                    else return false;
                case ShopItems.Gum1Adv: //
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_health2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum2Adv: //
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_strength2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum3Adv: //
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_dexterity2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum4Adv: //                    
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_resistance2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum5Adv: //
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_intuition2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                case ShopItems.Gum6Adv: //
                    if (Me.Player.Level >= 10 && Me.Wallet.Oil >= 25)
                    {
                        GoToPlace(WB, Place.Shop);
                        foreach (HtmlElement H in frmMain.GetElementsById(WB, "gum_attention2"))
                        {
                            match = Regex.Match(H.InnerHtml, "neft\"?>(?<Cost>([0-9])+)<"); //Появились жуйки за нефть/руду да и ещё в по разным ценам! (покупаем за нефть, самые дорогие!)
                            if (match.Success && Me.Wallet.Oil >= Convert.ToInt32(match.Groups["Cost"].Value)) { HtmlButton = H; break; }
                        }
                        break;
                    }
                    else return false;
                #endregion
                case ShopItems.Tvorog:
                    if (Me.Wallet.Honey >= 9 && Me.Player.Level >= 2)
                    {
                        GoToPlace(WB, Place.Shop);
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("tvorozhok"); break;
                    }
                    else return false;
                case ShopItems.Pyani:
                    if (Me.Wallet.Honey >= 9 && Me.Player.Level >= 2)
                    {
                        GoToPlace(WB, Place.Shop);
                        HtmlButton = frmMain.GetDocument(WB).GetElementById("pyani"); break;
                    }
                    else return false;
                case ShopItems.Pet100:
                    if (Settings.BuyHealPet && Me.Wallet.Ore >= 10)
                    {
                        GoToPlace(WB, Place.Shop, "/section/zoo"); HtmlButton = frmMain.GetDocument(WB).GetElementById("petfood_100"); break; //Собачий корм +100%
                    }
                    else return false;
                case ShopItems.Helmet:
                    if (Settings.BuyHelmet && Me.Wallet.Money >= 500)
                    {
                        GoToPlace(WB, Place.Shop, "/section/other"); HtmlButton = frmMain.GetDocument(WB).GetElementById("metro_helmet"); break;
                    }
                    else return false;
                case ShopItems.Counter:
                    if (Settings.BuyCounter && Me.Wallet.Money >= 500)
                    {
                        GoToPlace(WB, Place.Shop, "/section/other"); HtmlButton = frmMain.GetDocument(WB).GetElementById("metro_counter"); break;
                    }
                    else return false;
                case ShopItems.Pick:
                    if (Settings.BuyMpick && Me.Wallet.Money >= 1500)
                    {
                        GoToPlace(WB, Place.Shop, "/section/other"); HtmlButton = frmMain.GetElementsById(WB, "pick")[0]; break;
                    }
                    if (Settings.BuyRpick && Me.Wallet.Ore >= 2)
                    {
                        GoToPlace(WB, Place.Shop, "/section/other"); HtmlButton = frmMain.GetElementsById(WB, "pick")[1]; break;
                    }
                    return false;
                case ShopItems.Safe:
                    if (Settings.BuySafe && Me.Wallet.Ore >= 24)
                    {
                        GoToPlace(WB, Place.Shop, "/section/home"); HtmlButton = frmMain.GetDocument(WB).GetElementById("safe"); break;
                    }
                    return false;
                case ShopItems.Chain:
                    if (Me.Wallet.Money >= 437)
                    {
                        GoToPlace(WB, Place.Shop, "/section/jewellery"); HtmlButton = frmMain.GetDocument(WB).GetElementById("silver_chain"); break;
                    }
                    break;
                case ShopItems.Mona_Ticket:
                    GoToPlace(WB, Place.Berezka, "/section/mixed");
                    Me.Wallet = GetResources(WB, GetArrClassHtml(WB, "$(\"#content .borderdata [class]:visible\")", "outerHTML"), true);
                    if (Settings.BuyMonaTicketTooth)
                    {
                        stcWallet ItemPrice = GetResources(WB, GetArrClassHtml(WB, "$(\"#content #monya_ticket:eq(0) .c [class]:visible\")", "outerHTML"));
                        if (Me.Wallet.WhiteTooth - ItemPrice.WhiteTooth >= Settings.minTeeth) { HtmlButton = frmMain.GetElementsById(WB, "monya_ticket")[0]; break; }
                    }
                    if (Settings.BuyMonaTicketStar)
                    {
                        stcWallet ItemPrice = GetResources(WB, GetArrClassHtml(WB, "$(\"#content #monya_ticket.eq(1) .c [class]:visible\")", "outerHTML"));
                        if (Me.Wallet.Star - ItemPrice.Star >= Settings.minStars) { HtmlButton = frmMain.GetElementsById(WB, "monya_ticket")[1]; break; }
                    }
                    return false;
                case ShopItems.Bank_Ticket:
                    GoToPlace(WB, Place.Berezka, "/section/mixed");
                    Me.Wallet = GetResources(WB, GetArrClassHtml(WB, "$(\"#content .borderdata [class]:visible\")", "outerHTML"), true);
                    if (Settings.BuyMonaTicketTooth)
                    {
                        stcWallet ItemPrice = GetResources(WB, GetArrClassHtml(WB, "$(\"#content #ore_ticket:eq(1) .c [class]:visible\")", "outerHTML"));
                        if (Me.Wallet.WhiteTooth - ItemPrice.WhiteTooth >= Settings.minTeeth) { HtmlButton = frmMain.GetElementsById(WB, "ore_ticket")[1]; break; }
                    }
                    if (Settings.BuyMonaTicketStar)
                    {
                        stcWallet ItemPrice = GetResources(WB, GetArrClassHtml(WB, "$(\"#content #ore_ticket:eq(0) .c [class]:visible\")", "outerHTML"));
                        if (Me.Wallet.Star - ItemPrice.Star >= Settings.minStars) { HtmlButton = frmMain.GetElementsById(WB, "ore_ticket")[0]; break; }
                    }
                    return false;

                case ShopItems.HealPlus:
                case ShopItems.HealPrc:
                case ShopItems.GranadePlus:
                case ShopItems.GranadePrc:
                case ShopItems.Spring:
                case ShopItems.Helm:
                case ShopItems.Shild:
                    GoToPlace(WB, Place.Berezka);
                    #region Считываем мои ресурсы
                    Me.Wallet = GetResources(WB, GetArrClassHtml(WB, "$(\"#content .borderdata [class]:visible\")", "outerHTML"), true);
                    #endregion
                    #region Сбор информации о боевых предметах в берёзке и расчёт возможной покупки
                    foreach (string Info in GetArrClassHtml(WB, "$(\"#content .objects .object\")", "outerHTML"))
                    {
//                        match = Regex.Match(Info, "<h2>(?<Title>([\\s\\S])+)</h2>([\\s\\S])+((?<Heal>Жизни:([ 0-9+%])+)|Мин. урон по врагам: ((?<PrcBomb>([0-9])+%)|(?<Bomb>([0-9])+))|(?<Cheese>Призыв крысомах в групповой бой)|(?<Helmet>Защита от урона)|(?<Spring>Отражает урон)|(?<Shield>Уменьшение урона от гранат))([\\s\\S])+id=\"?amount_(?<ID>([\\S])+)\"?", RegexOptions.IgnoreCase);
                        match = Regex.Match(Info, "<h2>(?<Title>([\\s\\S])+)</h2>([\\s\\S])+(Жизни: ((?<PrcHeal>([0-9+])+%)|(?<Heal>([0-9+])+))|Мин. урон по врагам: ((?<PrcBomb>([0-9])+%)|(?<Bomb>([0-9])+))|(?<Cheese>Призыв крысомах в групповой бой)|(?<Helmet>Защита от урона)|(?<Spring>Отражает урон)|(?<Shield>Уменьшение урона от гранат))([\\s\\S])+Уровень:([\\s])*(?<ItemLvl>([0-9])+)([\\s\\S])+id=\"?amount_(?<ID>([\\w])+)\"?", RegexOptions.IgnoreCase);
                        if (
                            (  (match.Groups["Heal"].Success && SI == ShopItems.HealPlus)
                            || (match.Groups["PrcHeal"].Success && SI == ShopItems.HealPrc)
                            || (match.Groups["Bomb"].Success && SI == ShopItems.GranadePlus)
                            || (match.Groups["PrcBomb"].Success && SI == ShopItems.GranadePrc)
                            || (match.Groups["Helmet"].Success && SI == ShopItems.Helm)
                            || (match.Groups["Spring"].Success && SI == ShopItems.Spring)
                            || (match.Groups["Shield"].Success && SI == ShopItems.Shild)
                            ) && Convert.ToInt32(match.Groups["ItemLvl"].Value) <= Me.Player.Level && (Options[0].StartsWith("!") ? !Options.Contains(match.Groups["Title"].Value) : Options.Contains(match.Groups["Title"].Value))
                           )
                        {
                            stcWallet Price = GetResources(WB, GetArrClassHtml(WB, "$(\"#content #" + match.Groups["ID"].Value + ":eq(0) .c [class]:visible\")", "outerHTML"));
                            #region Подсчёт сколько можем купить!
                            int maxAmount;
                            Amount = 9;
                            Amount = Price.Money == 0 ? Amount : (maxAmount = Me.Wallet.Money / Price.Money) < Amount ? maxAmount : Amount;
                            Amount = Price.Ore == 0 ? Amount : (maxAmount = Me.Wallet.Ore / Price.Ore) < Amount ? maxAmount : Amount;
                            Amount = Price.Oil == 0 ? Amount : (maxAmount = Me.Wallet.Oil / Price.Oil) < Amount ? maxAmount : Amount;
                            Amount = Price.Honey == 0 ? Amount : (maxAmount = Me.Wallet.Honey / Price.Honey) < Amount ? maxAmount : Amount;
                            Amount = Price.Badge == 0 ? Amount : (maxAmount = Me.Wallet.Badge / Price.Badge) < Amount ? maxAmount : Amount;
                            Amount = Price.Mobile == 0 ? Amount : (maxAmount = Me.Wallet.Mobile / Price.Mobile) < Amount ? maxAmount : Amount;
                            Amount = Price.Star == 0 ? Amount : (maxAmount = Me.Wallet.Star / Price.Star) < Amount ? maxAmount : Amount;
                            Amount = Price.WhiteTooth == 0 ? Amount : (maxAmount = Me.Wallet.WhiteTooth / Price.WhiteTooth) < Amount ? maxAmount : Amount;
                            Amount = Price.GoldTooth == 0 ? Amount : (maxAmount = Me.Wallet.GoldTooth / Price.GoldTooth) < Amount ? maxAmount : Amount;
                            Amount = Price.PetGold == 0 ? Amount : (maxAmount = Me.Wallet.PetGold / Price.PetGold) < Amount ? maxAmount : Amount;
                            Amount = Price.PowerGold == 0 ? Amount : (maxAmount = Me.Wallet.PowerGold / Price.PowerGold) < Amount ? maxAmount : Amount;
                            #endregion

                            if (Price.Honey == 0 && Amount > 0)
                            {
                                HtmlButton = frmMain.GetElementsById(WB, match.Groups["ID"].Value)[0];
                                break;
                            }
                        }
                    }
                    #endregion                 
                    break;
                default: return false;
            }

            if (HtmlButton!= null && HtmlButton.GetAttribute("ClassName") == "button")
            {
                //Для того, чтоб не палить контору покупаем не по одной бутылке, а по возможности...
                if (Amount != 1) frmMain.GetDocument(WB).GetElementById("amount_" + HtmlButton.Id).InnerText = Amount.ToString(); //Устанавливаем желаемое значение            
                frmMain.InvokeMember(WB, HtmlButton.All[0], "click"); //Класс после кнопки, click  на кнопке не срабатывает!                
                if (frmMain.GetDocumentURL(WB).Contains("shop/section/gifts/")) //
                {
                    #region Оформление дарения подарка!
                    frmMain.GetDocument(WB).GetElementById("to-me").SetAttribute("checked", "true");
                    frmMain.GetDocument(WB).GetElementById("to-me").InvokeMember("onclick");
                    Wait(300, 1000);
                    frmMain.GetDocument(WB).GetElementById("present-form").GetElementsByTagName("button")[0].InvokeMember("onclick");
                    #endregion
                }
                IsWBComplete(WB); //IsAjaxComplete(WB);
                return (!frmMain.GetDocumentText(WB).Contains("У вас не хватает денег."));
            }
            return false;
        }
        public bool Trauma(TraumaAction TA, bool DetectedTrauma = false)
        {
            BugReport("Trauma");

            switch (TA)
            {
                case TraumaAction.Check:                  
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) GoToPlace(MainWB, Place.Player, "", false);
                    if (!frmMain.GetDocumentURL(MainWB).EndsWith("/player/")) return DetectedTrauma; //Видимо вишу на крыске, раз не смог перейти на страничку игрока.

                    if ((string)frmMain.GetJavaVar(MainWB, "$(\"#content .icon.injury-icon\").attr('class')") == "icon injury-icon") //Equal и прочее нельзя обычно тут null. Есть иконка с травмой, дальше не получается выковырять=(
                    {
                        //Травма||Вы получили травму из-за черезмерно частых боев и теперь не можете драться до 25 марта 2013 23:22.
                        Match match = Regex.Match(frmMain.GetDocumentHtmlTextEx(MainWB), "(не можете драться до)(?<Date>([\\s\\w:])+)[.]");
                        if (match.Success)
                        {
                            Me.Trauma.LastDT = Convert.ToDateTime(match.Groups["Date"].Value, CultureInfo.CreateSpecificCulture("ru-RU")).AddMinutes(1);
                        }
                        else
                        {
                            if (Me.Trauma.LastDT < GetServerTime(MainWB)) Me.Trauma.LastDT = GetServerTime(MainWB).AddHours(1).AddMinutes(-(Me.Major.LastDT > GetServerTime(MainWB) ? 5 : 15));
                        }
                        if (!Me.Trauma.Stop) UpdateStatus("! " + DateTime.Now + (match.Success ? " Фигасе, я себе коленку разбил посижу ко я на лавочке!" : "Вот изверги, до реанимации довели. Глаза не видят окончания травмы, выйду по самочуствию!"));
                        return Settings.HealTrauma ? !Trauma(TraumaAction.Heal) : true; //Травма осталась?
                    }                
                    return false; //Травмы нет!
                case TraumaAction.Heal:
                    UpdateMyInfo(MainWB);
                    if (Me.Wallet.Honey >= 5) //Есть мёд для лечения?
                    {
                        GoToPlace(MainWB, Place.Home, "/travma", false); //chKURL -> false, Потому-что в итоге остаюсь на страничке Home!
                        if (frmMain.GetDocument(MainWB).GetElementById("alert-title").InnerText != "Медовая ошибка")
                        {
                            UpdateStatus("@ " + DateTime.Now + " Наклеиваю пластырь на вавку и снова в драки!");
                            Me.Trauma.LastDT = new DateTime(); //Всё получилось, травмы больше нет!
                        }
                        GoToPlace(MainWB, Place.Player); //Возвращаемся дабы продолжить бесперебойное выполнение функции CheckHealthEx
                    }                    
                    return Me.Trauma.LastDT == new DateTime(); //Получилось?
            }
            return true; //Досюда не должно доходить никогда!
        }
        public void ClanWar(ClanWarAction CWA)
        {
            BugReport("ClanWar");

            Regex regex;
            Match match;
            MatchCollection matches;
            HtmlElement HtmlEl;

            switch (CWA)
            {
                case ClanWarAction.Check:
                   GoToPlace(MainWB, Place.Clan);
                   DateTime ServerDT = GetServerTime(MainWB);
                   if (frmMain.GetDocument(MainWB).GetElementById("clan-diplomacy-hint") != null) //Я в клане?
                   {
                       regex = new Regex("href=\"(?<ClanURL>.+)\"[>](?<ClanName>.+)[<]/a.*война", RegexOptions.IgnoreCase); //href="/clan/105/">Академия</A></SPAN><SPAN class=enemy> — война до 19 мар 2012 23:42</SPAN>
                       matches = regex.Matches(frmMain.GetDocument(MainWB).GetElementById("clan-diplomacy-hint").Parent.InnerHtml);
                       Me.ClanWarInfo.Now = matches.Count > 0; //Сохраняем информацию, о том, что война всё ешё продолжается!

                       if (Me.ClanWarInfo.Now) //Идёт война!
                       {                           
                           #region Новая война? (Не нужно ли добавить клан для фарма)
                           for (int i = 0; i < matches.Count; i++)
                           {
                               //Тут необходимо либо полностью очистить список кланов, либо убить определенный.
                               if (Me.ClanWarInfo.vsClan[i].Name != matches[i].Groups["ClanName"].Value.Replace("&amp;", "&") && Settings.AddClan && Settings.FarmClan) //Replace("&amp;", "&") для поимки кланов с подобным названием: • Брат за брата ©²º¹²
                               {
                                   Contact(MainWB, i == 0 ? (ContactAction.DeleteAll) : (ContactAction.DeleteClan), ContactType.Enemy, Me.ClanWarInfo.vsClan[i].Name);
                                   Contact(MainWB, ContactAction.AddClan, ContactType.Enemy, matches[i].Groups["ClanName"].Value.Replace("&amp;", "&")); //Заносим клан в враги! (непосредственный фарм в Attack)
                               }
                               Me.ClanWarInfo.vsClan[i].Name = matches[i].Groups["ClanName"].Value.Replace("&amp;", "&"); //Replace("&amp;", "&") для поимки кланов с подобным названием: • Брат за брата ©²º¹²
                               Me.ClanWarInfo.vsClan[i].URL = "http://" + Settings.ServerURL + matches[i].Groups["ClanURL"].Value;
                           }
                           #endregion

                           GoToPlace(MainWB, Place.Clan, "/warstats"); //Лезем смотреть на какой стадии война!
                           if (frmMain.GetDocument(MainWB).GetElementById("menu_result").GetAttribute("ClassName") == "button disabled") //Битва закончена!
                           {
                               if (frmMain.GetDocument(MainWB).GetElementById("menu_step2").GetAttribute("ClassName") != "button disabled") //Сейчас идут стенки.
                               {
                                   #region Инициализация
                                   Me.ClanWarInfo.NextDT = new DateTime();
                                   Me.ClanWarInfo.Pacifism = null;
                                   #endregion
                                   if (Settings.FarmClan && Me.ClanWarInfo.WarStep == 1) Contact(MainWB, ContactAction.AddClan, ContactType.Enemy, Me.ClanWarInfo.EnemyClan.Name); //Пришёл после выбивания зубов, многие были удалены ... передобавляем! 
                                   regex = new Regex("(?<=class=\"?fight-time\"?>)([0-9])*(?=[<])"); //class=fight-time>18<
                                   matches = regex.Matches(frmMain.GetDocumentHtmlText(MainWB));
                                   foreach (Match m in matches)
                                   {
                                       //Сразу после инизиализации запомнить значение которое меньше времени сейчас если, не найду больШего, значит это время нападения но уже следуюшего дня!
                                       if (Convert.ToInt32(m.Value) <= ServerDT.Hour && Me.ClanWarInfo.NextDT == new DateTime()) Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(Convert.ToInt32(m.Value) + 24).AddMinutes(-15);
                                       if (Convert.ToInt32(m.Value) > ServerDT.Hour) { Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(Convert.ToInt32(m.Value)).AddMinutes(-15); break; } //После первого нахождения нужно покинуть дальнейший поиск!
                                   }
                                   if (matches.Count >= 1)
                                   {
                                       Me.ClanWarInfo.WarStep = 2; //Война в разгаре, стенки.
                                       if (Me.ClanWarInfo.NextDT <= GetServerTime(MainWB)) GroupFight(GroupFightAction.Check, GroupFightType.Clan); //Пора драться?
                                   }
                                   else //Круглосуточный пацифизм
                                   {
                                       UpdateStatus("@ " + DateTime.Now + " О чёрт, да тут одни пацифисты блин собрались, драк не будет!"); 
                                       Me.ClanWarInfo.Now = false; //Война продолжается формально, стенок не будет
                                       Me.ClanWarInfo.WarStep = -1; //Война продолжается формально, стенок не будет
                                       Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(ServerDT.Hour + 5);
                                   }                                    
                                   return;
                               }
                               else //Нет клан стенок, выбиваем зубы?
                               {
                                   if (frmMain.GetDocument(MainWB).GetElementById("menu_step1").GetAttribute("ClassName") != "button disabled") //Уже как минимум можно выбивать зубы!
                                   {
                                       HtmlEl = frmMain.GetDocument(MainWB).GetElementById("clan-warstat1-table").GetElementsByTagName("TR")[0];
                                       matches = Regex.Matches(HtmlEl.InnerHtml, "href=\"(?<ClanURL>.+)\"[>](?<ClanName>.+)[<]/a", RegexOptions.IgnoreCase);
                                       if (matches.Count == 2) //Порядок, нашёл оба воюющих клана
                                       {
                                           #region Кто на кого напал?
                                           foreach (stcClan Clan in Me.ClanWarInfo.vsClan) //Replace("&amp;", "&") для поимки кланов с подобным названием: • Брат за брата ©²º¹²
                                           {                                              
                                               if (Clan.Name == matches[0].Groups["ClanName"].Value.Replace("&amp;", "&")) //Вражеский клан напал первым!
                                               {                                                   
                                                   Me.ClanWarInfo.EnemyClan.Name = Clan.Name;
                                                   Me.ClanWarInfo.EnemyClan.URL = Clan.URL;
                                                   Me.ClanWarInfo.MyWar = (Me.Clan.Name == matches[1].Groups["ClanName"].Value.Replace("&amp;", "&")); //Напали на меня или союзника?
                                                   break;
                                               }
                                               if (Clan.Name == matches[1].Groups["ClanName"].Value.Replace("&amp;", "&")) //На вражеский клан напали мы или союзник!
                                               {
                                                   Me.ClanWarInfo.EnemyClan.Name = Clan.Name;
                                                   Me.ClanWarInfo.EnemyClan.URL = Clan.URL;
                                                   Me.ClanWarInfo.MyWar = (Me.Clan.Name == matches[0].Groups["ClanName"].Value.Replace("&amp;", "&")); //Напали мы или союзник?
                                                   break;
                                               }
                                           }
                                           #endregion
                                           #region Если необходимо передобавляю воюющий клан + Определяем времена пацифизма
                                           if (Settings.AddClan && (Me.ClanWarInfo.MyWar || (Settings.ClanWars && matches[0].Groups["ClanName"].Value == Me.ClanWarInfo.EnemyClan.Name)) && Me.ClanWarInfo.WarStep != 1) //Добавляем клан, воюет мой клан или (я участвую в союзных войнах и на союзника напали) и я ещё тут не был!
                                           {
                                               #region Определяем времена пацифизма
                                               Me.ClanWarInfo.Pacifism = new stcClanImmun[2]; //Если наше время пацифизма также играет роль в боях союза добавить new stcClanImmun[MyWar ? 2 : 3]
                                               regex = new Regex("(?<=Время ненападения:\\s*)(?<Start>([0-9:])+) — (?<Stop>([0-9:])+)"); //Время ненападения:23:40 — 9:40                                                
                                               for (int i = 0; i < Me.ClanWarInfo.Pacifism.Count<stcClanImmun>(); i++)
                                               {
                                                   switch (i)
                                                   {
                                                       case 0:
                                                       case 1:
                                                           GoToPlace(MainWB, Place.URL, matches[i].Groups["ClanURL"].Value);    
                                                           break;
                                                       case 2:
                                                           GoToPlace(MainWB, Place.URL, Me.Clan.URL);    
                                                           break;
                                                   }
                                                   match = regex.Match(frmMain.GetDocument(MainWB).GetElementById("content").InnerText);
                                                   if (match.Success)
                                                   {
                                                       Me.ClanWarInfo.Pacifism[i].Start = match.Groups["Start"].Value;
                                                       Me.ClanWarInfo.Pacifism[i].Stop = match.Groups["Stop"].Value;
                                                   }
                                                   else  //Клан не покупал пацифизм!
                                                   {
                                                       Me.ClanWarInfo.Pacifism[i].Start = "00:00";
                                                       Me.ClanWarInfo.Pacifism[i].Stop = "00:00";
                                                   }
                                               }
                                               #endregion
                                               Contact(MainWB, ContactAction.DeleteAll, ContactType.Enemy);
                                               Contact(MainWB, ContactAction.AddClan, ContactType.Enemy, Me.ClanWarInfo.EnemyClan.Name); //Заносим клан в враги!
                                               Me.ClanWarInfo.WarStep = 1;
                                           }
                                           #endregion
                                       }
                                       else UpdateStatus("! " + DateTime.Now + " Дохтур, посмотри меня: Выбивание зубов проигнорировано!");
                                       Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(ServerDT.Hour + (ServerDT.Minute >= 45 ? 2 : 1)).AddMinutes(-15); //За 15 минут до след часа снова проверить!
                                       return;
                                   }
                                   else //Нет клан стенок и зубы пока тоже не выбиваем, проверить через 1 час!
                                   {
                                       Me.ClanWarInfo.WarStep = 0; //Война обьявлена, но боевые действия пока не ведутся.
                                       Me.ClanWarInfo.Pacifism = null;
                                       Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(ServerDT.Hour + 1).AddMinutes(2); //Все войны начинаются в хх:01 минуту
                                       return;
                                   }
                               }
                           }
                       }
                   }
                   Me.ClanWarInfo.Now = false; //Я даже не в клане
                   Me.ClanWarInfo.WarStep = -1; //Войной отсилы только пахнет в воздухе.
                   Me.ClanWarInfo.EnemyClan.Name = ""; //Я сейчас не с кем не воюю
                   Me.ClanWarInfo.EnemyClan.URL = "";  //Я сейчас не с кем не воюю
                   Me.ClanWarInfo.Pacifism = null;     //Я сейчас не с кем не воюю
                   Me.ClanWarInfo.NextDT = ServerDT.Date.AddHours(ServerDT.Hour + 5); //Пока даже не пахнет войной!                        
                   break;
                case ClanWarAction.Tooth:
                    ClanWar(ClanWarAction.Check); //Проверка текушего состояния войны и подготвка странички                    
                    if (Me.ClanWarInfo.WarStep == 1) //Всё ещё стадия выбивания зубов?
                    {
                        if (!frmMain.GetDocumentURL(MainWB).EndsWith(Settings.ServerURL + "/clan/profile/warstats/")) GoToPlace(MainWB, Place.Clan, "/warstats"); //Лезем смотреть на какой стадии война!
                        #region Фильтруем игроков с выбитыми зубами.

                        regex = new Regex("(?<ClanURL>/clan/([0-9])+/)\"[>](?<ClanName>.+)[<]/a", RegexOptions.IgnoreCase);
                        matches = regex.Matches(frmMain.GetDocument(MainWB).GetElementById("clan-warstat1-table").InnerHtml); //Выдираю имена воююших кланов.
                        if (matches.Count == 0) return; //Ничего не нашёл? Выходим. (Теоретически нереально)

                        int EnemyCount = 0;
                        stcPlayerInfo[] ArrPI = null;
                        string[] ArrEnemy = new string[0];
                        string[] EPattern = new string[2]; //[0]-> Референция на клан (номер), [1]-> Имя враждебного клана 

                        //Составляем критерий поиска вражеского клана.
                        if (Me.ClanWarInfo.EnemyClan.Name == matches[0].Groups["ClanName"].Value.Replace("&amp;", "&")) //Враги с лева
                        {
                            EPattern[0] = "1";
                            EPattern[1] = matches[0].Groups["ClanURL"].Value;
                        }
                        if (Me.ClanWarInfo.EnemyClan.Name == matches[1].Groups["ClanName"].Value.Replace("&amp;", "&")) //Враги с права
                        {
                            EPattern[0] = "2";
                            EPattern[1] = matches[1].Groups["ClanURL"].Value;
                        }

                        ArrEnemy = GetArrClassHtml(MainWB, "$('#clan-warstat1-table tr.user-logs[rel=clan" + EPattern[0] + "]');", "innerHTML"); //Считываем список врагов, включая союзников.
                        foreach (string Enemy in ArrEnemy)
                        {
                            match = Regex.Match(Enemy, EPattern[1] + ".*href=\"(?<URL>/player/(?<Id>([0-9])+)/)\"[>](?<Name>([^<])+).*level\"?>[[](?<Lvl>([0-9])+)"); //Выдираем вместе с кланом, ибо иначе в список попадут и союзники выбивающие нам зубы. При проверке, остался ли ещё кто живой они будут мешать!
                            if (match.Success)
                            {
                                EnemyCount++; //Счётчик непосредственных врагов, без учёта найденных союзников.
                                if (Regex.Match(Enemy, "Выбито персонажем " + Me.Player.Name).Success || Regex.Matches(Enemy, "tooth-black").Count == 3) //Я выбил зуб, или выбито все 3 зуба!
                                {
                                    Array.Resize<stcPlayerInfo>(ref ArrPI, (ArrPI == null ? 1 : ArrPI.Count<stcPlayerInfo>() + 1));
                                    ArrPI[ArrPI.Count<stcPlayerInfo>() - 1].URL = "http://" + Settings.ServerURL + match.Groups["URL"].Value;
                                    ArrPI[ArrPI.Count<stcPlayerInfo>() - 1].Name = match.Groups["Name"].Value;
                                    ArrPI[ArrPI.Count<stcPlayerInfo>() - 1].Level = match.Groups["Lvl"].Value;
                                    ArrPI[ArrPI.Count<stcPlayerInfo>() - 1].Id = match.Groups["Id"].Value;
                                }
                            }
                            else break; //Нет смысла перебирать далее, начались союзники!                    
                        }
                        if (ArrPI != null) Contact(MainWB, ContactAction.DeletePlayer, ContactType.Enemy, null, ArrPI); //Все зубы выбиты, удаляем из контакта.
                        #region Все зубы уже выбиты симулируем круглосуточный пацифизм!
                        if (ArrPI != null && EnemyCount == ArrPI.Count<stcPlayerInfo>())
                        {
                            Me.ClanWarInfo.Pacifism[0].Start = "00:00:00";
                            Me.ClanWarInfo.Pacifism[0].Stop = "24:00:00";
                        }
                        #endregion
                        #endregion
                    }
                    break;
            }
        }
        public bool Dopings(ref clsDoping.stcDopingEx[] ArrD, DopingAction DA, int Index = 0)
        {            
            BugReport("Dopings");

            HtmlElement[] HC;
            MatchCollection matches;
            Regex regex;
            DateTime DT = new DateTime();
            DateTime ServerDT;
            bool bRet = true; //Сьел допинг или есть не нужно было вовсе

            if (ArrD == null) return true; //Нет назначенных допингов?

            switch (DA)
            {
                case DopingAction.CheckTime:
                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = true; //Исходим из того, что все необходимые в списке допинги уже были сьедены, если нет перепишим.
                    Array.Sort(ArrD[Index].Items); //Сортируем допинги, сперва жвачки, затем машинки!
                    ArrD[Index].StopDT = new DateTime(); //Инициализация
                    #region Машинки
                    //От CarAll до Car40 , ибо машинки ездят на статы не по порядку.
                    if (Enumerable.Range((int)clsDoping.DopingType.CarAll, (int)clsDoping.DopingType.Car40).Contains((int)ArrD[Index].Items[ArrD[Index].Items.Count<clsDoping.DopingType>() - 1])) //Есть ли среди желаемых допингов машинки?
                    {                        
                        Automobile(AutomobileAction.Check);
                        foreach (clsDoping.DopingType DType in ArrD[Index].Items)
                        {
                            int RidePlace;
                            DT = new DateTime();
                            
                            switch (DType)
                            {
                                #region Все свободные машинки
                                case clsDoping.DopingType.CarAll:
                                   if ( Me.CarRide.RideTimeout.Count(TimeOut => TimeOut < DateTime.Now) > 0 &&
                                        Me.CarRide.Cars.Count(Car => !Car.Reserved && Car.Timeout < DateTime.Now) > 0
                                      ) clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    continue;
                                 #endregion
                                #region Здоровье
                                case clsDoping.DopingType.Car4:
                                    RidePlace = 4;
                                    break;
                                case clsDoping.DopingType.Car10:
                                    RidePlace = 10;
                                    break;
                                case clsDoping.DopingType.Car16:
                                    RidePlace = 16;
                                    break;
                                #endregion
                                #region Сила
                                case clsDoping.DopingType.Car6:
                                    RidePlace = 6;
                                    break;
                                case clsDoping.DopingType.Car12:
                                    RidePlace = 12;
                                    break;
                                case clsDoping.DopingType.Car18:
                                    RidePlace = 18;
                                    break;
                                #endregion
                                #region Ловкость
                                case clsDoping.DopingType.Car5:
                                    RidePlace = 5;
                                    break;
                                case clsDoping.DopingType.Car11:
                                    RidePlace = 11;
                                    break;
                                case clsDoping.DopingType.Car17:
                                    RidePlace = 17;
                                    break;
                                #endregion
                                #region Выносливость
                                case clsDoping.DopingType.Car2:
                                    RidePlace = 2;
                                    break;
                                case clsDoping.DopingType.Car8:
                                    RidePlace = 8;
                                    break;
                                case clsDoping.DopingType.Car14:
                                    RidePlace = 14;
                                    break;
                                #endregion
                                #region Хитрость
                                case clsDoping.DopingType.Car3:
                                    RidePlace = 3;
                                    break;
                                case clsDoping.DopingType.Car9:
                                    RidePlace = 9;
                                    break;
                                case clsDoping.DopingType.Car15:
                                    RidePlace = 15;
                                    break;
                                #endregion
                                #region Внимательность
                                case clsDoping.DopingType.Car1:
                                    RidePlace = 1;
                                    break;
                                case clsDoping.DopingType.Car7:
                                    RidePlace = 7;
                                    break;
                                case clsDoping.DopingType.Car13:
                                    RidePlace = 13;
                                    break;
                                #endregion  
                                #region Случайная характеристика 1
                                case clsDoping.DopingType.Car19:
                                    RidePlace = 19;
                                    break;
                                #endregion
                                #region Случайная характеристика [Чайка]
                                case clsDoping.DopingType.Car20:
                                    RidePlace = 20;
                                    break;
                                #endregion
                                #region Случайная характеристика [Тигр]
                                case clsDoping.DopingType.Car21:
                                    RidePlace = 21;
                                    break;
                                #endregion
                                #region Случайная характеристика 2
                                case clsDoping.DopingType.Car22:
                                    RidePlace = 22;
                                    break;
                                #endregion
                                #region Случайная характеристика 3
                                case clsDoping.DopingType.Car23:
                                    RidePlace = 23;
                                    break;
                                #endregion
                                #region Случайная характеристика 4
                                case clsDoping.DopingType.Car24:
                                    RidePlace = 24;
                                    break;
                                #endregion
                                #region Случайная характеристика [Новогодний грузовик]
                                case clsDoping.DopingType.Car25:
                                    RidePlace = 25;
                                    break;
                                #endregion
                                #region Случайная характеристика [Конь]
                                case clsDoping.DopingType.Car26:
                                    RidePlace = 26;
                                    break;
                                #endregion
                                #region Случайная характеристика [Броневик *-*****]
                                case clsDoping.DopingType.Car27:
                                case clsDoping.DopingType.Car28:
                                case clsDoping.DopingType.Car29:
                                case clsDoping.DopingType.Car30:
                                case clsDoping.DopingType.Car31:
                                    RidePlace = (int)DType - (int)clsDoping.DopingType.Car4 + 1; //Броневик*-*****
                                    break;
                                #endregion
                                #region  Случайная характеристика [Эвакуатор]
                                case clsDoping.DopingType.Car34:
                                    RidePlace = 34;
                                    break;
                                #endregion
                                #region Вертолёт 1
                                case clsDoping.DopingType.Car35:
                                    RidePlace = 35;
                                    break;
                                #endregion
                                #region Вертолёт 2
                                case clsDoping.DopingType.Car36:
                                    RidePlace = 36;
                                    break;
                                #endregion
                                #region Вертолёт «Борт №1»
                                case clsDoping.DopingType.Car37:
                                    RidePlace = 37;
                                    break;
                                #endregion
                                #region Вертолёт «Черная акула»
                                case clsDoping.DopingType.Car38:
                                    RidePlace = 38;
                                    break;
                                #endregion
                                #region Де-Лориан
                                case clsDoping.DopingType.Car39:
                                    RidePlace = 39;
                                    break;
                                #endregion
                                #region Де-Лориан ускоренный
                                case clsDoping.DopingType.Car40:
                                    RidePlace = 40;
                                    break;
                                #endregion

                                #region Маршрут не найден / Иные допинги
                                default: 
                                    RidePlace = -1;
                                    continue;
                                #endregion
                            }
                            if (Me.CarRide.Cars == null || RidePlace >= Me.CarRide.RideTimeout.Count<DateTime>() || Me.CarRide.RideTimeout[RidePlace] == new DateTime())
                            {
                                UpdateStatus("! " + DateTime.Now + " Шеф, пешком я туда не пойду, а такой машинки [" + RidePlace +  "] у нас походу нема!");
                                return false;
                            }
                            else
                            {
                                if (Me.CarRide.RideTimeout[RidePlace] < DateTime.Now)
                                {
                                    clsDoping.AlreadyEated[(int)DType] = false; //Можно запускать!
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                }
                                else
                                {
                                    clsDoping.AlreadyEated[(int)DType] = true; //Машинка уже запущена, или ещё не вернулась из поездки
                                    DT = GetServerTime(MainWB).Add(Me.CarRide.RideTimeout[RidePlace] - DateTime.Now);
                                }
                                if (ArrD[Index].Event == clsDoping.DopingEvent.Allways) ArrD[Index].StopDT = (DT < ArrD[Index].StopDT || ArrD[Index].StopDT == new DateTime()) ? DT : ArrD[Index].StopDT;
                                else ArrD[Index].StopDT = ArrD[Index].StopDT < DT ? DT : ArrD[Index].StopDT;
                            }
                        }                                            
                    }
                    #endregion                     
                    #region Партбилеты
                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.LeninTicket] = false; //Исхожу из того, что они не нужны, если что перезапишу!
                    if (ArrD[Index].Items.Contains<clsDoping.DopingType>(clsDoping.DopingType.LeninTicket))
                    {
                        clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Синхронизация не пройдена, блокируем пересчёт стоппера
                        clsDoping.AlreadyEated[(int)clsDoping.DopingType.LeninTicket] = false;
                        clsDoping.NeedToBuy[(int)clsDoping.DopingType.LeninTicket] = true;
                    }
                    #endregion
                    #region Синхронизация с крысиным уровнем
                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.RatSyncLvl] = false; //Исхожу из того, что она не нужна, если что перезапишу!
                    if (Me.RatHunting.Lvl > ArrD[Index].SyncLvl) clsDoping.AlreadyEated[(int)clsDoping.DopingType.RatSyncLvl] = true;
                    else
                    {
                        clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Синхронизация не пройдена, блокируем пересчёт стоппера
                        clsDoping.AlreadyEated[(int)clsDoping.DopingType.RatSyncLvl] = false;
                        clsDoping.NeedToBuy[(int)clsDoping.DopingType.RatSyncLvl] = true;
                        Me.OilLeninHunting.NextDT = Me.RatHunting.Stop ? DateTime.Now.AddMinutes(10) : (Me.RatHunting.NextDT.AddSeconds(5) < DateTime.Now ? DateTime.Now.AddSeconds(30) : Me.RatHunting.NextDT); //Передвигаем проверку вентиля после следующей крысы!
                    }
                    #endregion
                    #region Жвачки и прочее
                    #region Проверка есть ли допинг которые нуждаются в проверке через тултип.
                    bRet = false;
                    foreach (clsDoping.DopingType DType in ArrD[Index].Items)
                    {
                        if ((int)DType > (int)clsDoping.DopingType.Gum1)
                        {
                            bRet = true; //Обнаружил допинг, который нужно проверять!
                            break;
                        }
                    }
                    if (!bRet) return true; //Проверка пройдена, допинг не интересен, нужна синхронизация или партбилеты!
                    #endregion

                    GoToPlace(MainWB, Place.Player);                    
                    object ToolTipInfo = frmMain.GetJavaVar(MainWB, "m.items['affects'].tooltip['0'].innerText") ?? ""; //Нет тултипа с допингами, значит исходим, из того, что кушать нужно всё!

                    ServerDT = GetServerTime(MainWB);
                    regex = new Regex("((?<Doping>[+]([0-9])+)(?<Prc>[%])?)? до (?<Time>([0-9. :])+) — ((?<Ride>Поездка)|(?<Pyani>Пяни)|(?<Tvorog>Волшебный творожок)|(?<Vitamin>Витаминки)|(?<NovajaZhizn>Таблетки «Новая жизнь»)|(?<Barjomi>Бутылка «Бомжори»)|(?<AuqaDeminerale>Бутылка «Аква Деминерале»)|(?<Tea>Чай)|(?<Shoko>Конфета («((?<CandyExp>Умная)|(?<CandyAntiExp>Глупая))»)?|.*конфета|Батончик)|(Табаско «((?<NPC1>Огонек)|(?<NPC2>Обжорка)|(?<NPC3>Усилитель))»)|(?<Valujki>Валуйки[^ ])|(?<ValujkiAdv>Валуйки «Heavy Edition»)|(?<GasMask>Противогаз)|(?<Respirator>Респиратор)|((?<Coctail>Коктейль)|(.*)) (?<Gum>[+%](Здоровье|Сила|Ловкость|Выносливость|Хитрость|Внимательность)))"); //+5% до 29.01.2012 03:24 — Поездка На молодежную дискотеку в Южное Бутово
                    matches = regex.Matches((string)ToolTipInfo);
                    
                    foreach (clsDoping.DopingType DType in ArrD[Index].Items)
                    {
                        DT = new DateTime();
                        switch (DType) //Adv -> Нефтяные, Ex -> %-ные.
                        {
                            #region Здоровье
                            case clsDoping.DopingType.Gum1:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Здоровье" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1] = false; //Уже поел нет никакого смысла покупать новую в следующий раз закупимся.
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                   
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_health2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum1Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Здоровье" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Adv] = false; //Уже поел нет никакого смысла покупать новую в следующий раз закупимся.
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_health2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum1Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Здоровье" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Ex] = false; //Уже поел нет никакого смысла покупать новую в следующий раз закупимся.
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_health-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail1:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail1] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Здоровье")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail1] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail1] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_health-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "health.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail1] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail1]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Сила
                            case clsDoping.DopingType.Gum2:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Сила" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_strength2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum2Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Сила" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Adv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_strength2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum2Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Сила" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Ex] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_strength-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail2:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail2] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Сила")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail2] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail2] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_strength-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "strength.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail2] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail2]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Ловкость
                            case clsDoping.DopingType.Gum3:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Ловкость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_dexterity2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum3Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Ловкость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Adv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_dexterity2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum3Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                   
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Ловкость" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Ex] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_dexterity-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail3:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail3] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Ловкость")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail3] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail3] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_dexterity-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "dexterity.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail3] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail3]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Выносливость
                            case clsDoping.DopingType.Gum4:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                     
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Выносливость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_resistance2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum4Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Выносливость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Adv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_resistance2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum4Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Выносливость" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Ex] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_resistance-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail4:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail4] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Выносливость")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail4] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail4] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_resistance-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "resistance.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail4] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail4]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Хитрость
                            case clsDoping.DopingType.Gum5:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Хитрость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                    
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_intuition2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum5Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                     
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Хитрость" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Adv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_intuition2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum5Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                     
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Хитрость" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Ex] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_intuition-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail5:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail5] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Хитрость")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail5] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail5] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_intuition-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "intuition.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail5] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail5]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Внимательность
                            case clsDoping.DopingType.Gum6:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Внимательность" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_attention2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum6Adv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Adv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (!m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Внимательность" && !m.Groups["Coctail"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Adv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Adv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Adv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать? //Добавить проверку между обычными и нефтяными жуйками
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Adv] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_attention2-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Adv] = false;
                                            break;
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Gum6Ex:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Ex] = false; //Исхожу из того, что ещё не ел, если ел перезапишу                                    
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Prc"].Success && m.Groups["Gum"].Value == "+Внимательность" && Convert.ToInt32(m.Groups["Doping"].Value) >= 15) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Ex] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Ex]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Ex] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Ex] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-gum_attention-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                            Info = Regex.Match(Info, "(?<=[+])([0-9])+(?=%)").Value;
                                            if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info))
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Ex] = false;
                                                break;
                                            }
                                        }
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Coctail6:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail6] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Coctail"].Success && m.Groups["Gum"].Value == "+Внимательность")
                                    {
                                        if (m.Groups["Doping"].Success) clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail6] = true;
                                        else
                                        {
                                            DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); //Послевкусие, именно время его истечения и интересно.
                                            if (ArrD[Index].Event != clsDoping.DopingEvent.Allways || ArrD[Index].Event != clsDoping.DopingEvent.Timer)
                                            {
                                                UpdateStatus("@ " + DateTime.Now + " Чёртово послевкусие ..., незнаю что такое, но шеф говорил не пить!");
                                                return false;
                                            }
                                        }
                                        break;
                                    }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail6] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-shake_attention-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC) //Поиск разрешенных к использованию коктейлей
                                        {
                                            string Info = (string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                            if (Settings.AllowCoctailAdv || Info == "attention.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail6] = false;
                                                break;
                                            }
                                        }
                                    }
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.Coctail6]) return false; //Коктейль не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Пяни
                            case clsDoping.DopingType.Pyani:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Pyani] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Pyani"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Pyani] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Pyani]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Pyani] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Pyani] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-pyani-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Pyani] = false;
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Творожок
                            case clsDoping.DopingType.Tvorog:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tvorog] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tvorog"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tvorog] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tvorog]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tvorog] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tvorog] = true; //Исхожу из того, что нужно купить, если нет перезапишу
                                    HC = frmMain.GetElementsById(MainWB, "inventory-tvorozhok-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tvorog] = false;
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Валуйки
                            case clsDoping.DopingType.Valujki:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Valujki] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Valujki"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Valujki] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Valujki]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Valujki] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Valujki] = true;
                                }
                                break;
                            #endregion
                            #region Валуйки "Heavy Edition"
                            case clsDoping.DopingType.ValujkiAdv:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.ValujkiAdv] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["ValujkiAdv"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.ValujkiAdv] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.ValujkiAdv]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.ValujkiAdv] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.ValujkiAdv] = true;
                                }
                                break;
                            #endregion
                            #region Противогаз
                            case clsDoping.DopingType.GasMask:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.GasMask] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["GasMask"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.GasMask] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.GasMask]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.GasMask] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.GasMask] = true;
                                }
                                break;
                            #endregion
                            #region Респиратор
                            case clsDoping.DopingType.Respirator:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Respirator] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Respirator"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Respirator] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Respirator]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Respirator] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Respirator] = true;
                                }
                                break;
                            #endregion
                            #region Чай
                            case clsDoping.DopingType.Tea1:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea1] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tea"].Success && m.Groups["Doping"].Value == "+15") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea1] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea1]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea1] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea1] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_13-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s4-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s5-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s6-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Tea4:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea4] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tea"].Success && m.Groups["Doping"].Value == "+40") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea4] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea4]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea4] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea4] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_14-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m4-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m5-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m6-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Tea7:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea7] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tea"].Success && m.Groups["Doping"].Value == "+90") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea7] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea7]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea7] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea7] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_15-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l4-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l5-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l6-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Tea10:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea10] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tea"].Success && m.Groups["Doping"].Value == "+150") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea10] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea10]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea10] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea10] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_16-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl4-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl5-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl6-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Tea15:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea15] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Tea"].Success && m.Groups["Doping"].Value == "+300") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea15] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea15]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea15] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea15] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_18-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl4-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl5-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl6-btn") == null
                                        );
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Шоколад
                            case clsDoping.DopingType.Shoko1:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko1] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Shoko"].Success && m.Groups["Doping"].Value == "+15") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko1] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko1]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko1] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko1] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_10-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s1-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s2-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_s3-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Shoko4:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko4] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Shoko"].Success && m.Groups["Doping"].Value == "+40") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko4] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko4]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko4] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko4] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_11-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m1-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m2-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_m3-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Shoko7:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko7] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Shoko"].Success && m.Groups["Doping"].Value == "+90") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko7] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko7]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko7] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko7] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_12-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l1-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l2-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_l3-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Shoko10:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko10] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Shoko"].Success && m.Groups["Doping"].Value == "+150") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko10] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko10]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko10] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko10] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_17-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl1-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl2-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xl3-btn") == null
                                        );
                                    #endregion
                                }
                                break;

                            case clsDoping.DopingType.Shoko15:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko15] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Shoko"].Success && m.Groups["Doping"].Value == "+300") { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko15] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko15]) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko15] = false;
                                else
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko15] = frmMain.GetDocument(MainWB).GetElementById("inventory-chocolates_19-btn") == null &&
                                        (frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl1-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl2-btn") == null
                                        || frmMain.GetDocument(MainWB).GetElementById("inventory-chocolate_xxl3-btn") == null
                                        );
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Элексиры (витаминки, баржоми, соусы)
                            case clsDoping.DopingType.Vitamin:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Vitamin] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Vitamin"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Vitamin] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Vitamin] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-eliksir_hp50-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Vitamin] = false;
                                    else return false; //Витамин не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.NovajaZhizn:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.NovajaZhizn] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["NovajaZhizn"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.NovajaZhizn] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.NovajaZhizn] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-eliksir_ny2013-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.NovajaZhizn] = false;
                                    else return false; //Напиток не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.Barjomi:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.Barjomi] = false;
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["Barjomi"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.Barjomi] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.Barjomi] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-eliksir_critpower100-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.Barjomi] = false;
                                    else return false; //Напиток не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.AquaDeminerale:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.AquaDeminerale] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["AquaDeminerale"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.AquaDeminerale] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.AquaDeminerale] = true; //Исхожу из того, что нужно купить, если нет выход.
                                    HC = frmMain.GetElementsById(MainWB, "inventory-eliksir_critpower20-btn");
                                    if (HC != null) clsDoping.NeedToBuy[(int)clsDoping.DopingType.AquaDeminerale] = false;
                                    else return false; //Напиток не найден, купить не можем, допы не кушать!
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.WeakNPC1:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC1] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["NPC1"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC1] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC1] = frmMain.GetElementsById(MainWB, "inventory-eliksir_npc1-btn") == null; //Есть ли?
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC1] && !Settings.NoSauceNoProblem) return false; //Не разрешено игнорировать эти допинги и их нет ...
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.WeakNPC2:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC2] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["NPC2"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC2] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC2] = frmMain.GetElementsById(MainWB, "inventory-eliksir_npc2-btn") == null; //Есть ли?
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC2] && !Settings.NoSauceNoProblem) return false; //Не разрешено игнорировать эти допинги и их нет ...
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.WeakNPC3:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC3] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["NPC3"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC3] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC3] = frmMain.GetElementsById(MainWB, "inventory-eliksir_x2-btn") == null; //Есть ли?
                                    if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC3] && !Settings.NoSauceNoProblem) return false; //Не разрешено игнорировать эти допинги и их нет ...
                                    #endregion
                                }
                                break;
                            #endregion
                            #region Умная/глупая конфеты
                            case clsDoping.DopingType.CandyExp:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyExp] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["CandyExp"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyExp] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyExp] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                   
                                    HC = frmMain.GetElementsById(MainWB, "inventory-xpdoping-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            if ((string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp") == "lamp-pack.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyExp] = false;
                                                break;
                                            }
                                        }
                                        if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyExp] && !Settings.NoCandyNoProblem) return false; //Не разрешено игнорировать эти допинги и их нет ...
                                    }
                                    #endregion
                                }
                                break;
                            case clsDoping.DopingType.CandyAntiExp:
                                clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyAntiExp] = false; //Исхожу из того, что ещё не ел, если ел перезапишу
                                foreach (Match m in matches)
                                {
                                    if (m.Groups["CandyAntiExp"].Success) { clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyAntiExp] = true; DT = Convert.ToDateTime(m.Groups["Time"].Value, CultureInfo.CreateSpecificCulture("ru-RU")); break; }
                                }
                                if (DT == new DateTime()) //Допинг ещё не сьеден, проверяем наличие в багаже!
                                {
                                    clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length] = false; //Как минимум один допинг из списка, ешё не употреблён!
                                    #region Нету, нужно докупать?
                                    clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyAntiExp] = true; //Исхожу из того, что нужно купить, если нет перезапишу                                   
                                    HC = frmMain.GetElementsById(MainWB, "inventory-xpdoping-btn");
                                    if (HC != null)
                                    {
                                        foreach (HtmlElement H in HC)
                                        {
                                            if ((string)frmMain.GetJavaVar(MainWB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp") == "anti-lamp.png")
                                            {
                                                clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyAntiExp] = false;
                                                break;
                                            }
                                        }
                                        if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyAntiExp] && !Settings.NoCandyNoProblem) return false; //Не разрешено игнорировать эти допинги и их нет ...
                                    }
                                    #endregion
                                }
                                break;
                            #endregion
                        }
                        if (ArrD[Index].Event == clsDoping.DopingEvent.Allways || ArrD[Index].Event == clsDoping.DopingEvent.PVP)
                        {  //Always а также при PVP всегда держать под полным списком допингов, один спал -> перекусить его снова!
                            ArrD[Index].StopDT = (ArrD[Index].StopDT < ServerDT || DT < ArrD[Index].StopDT) ? DT : ArrD[Index].StopDT;
                        }
                        else
                        {  //Кушать допинг только тогда, когда закончилось действие последнего!
                            ArrD[Index].StopDT = ArrD[Index].StopDT < DT ? DT : ArrD[Index].StopDT;
                        }                           
                    }
                    #endregion
                    //При постоянной необходимости объедаться переносим старт так, чтоб постоянно быть под полным желаемым доппингом!
                    if (ArrD[Index].Event == clsDoping.DopingEvent.Allways) ArrD[Index].StartDT = (ArrD[Index].StartDT < ServerDT || ArrD[Index].StartDT > ArrD[Index].StopDT) ? ArrD[Index].StopDT.AddMinutes(1) : ArrD[Index].StartDT;                                      
                    return true; //Проверка пройдена, все необходимые допинги имеются или будут докуплены
                case DopingAction.Check:
                    #region Перед проверкой крысиных и ленинопроводных, проверяем обычные допинги.
                    if (ArrD.Equals(Me.ArrRatDoping) || ArrD.Equals(Me.ArrOilLeninDoping)) Dopings(ref Me.ArrUsualDoping, DopingAction.Check);
                    #endregion
                    for (int i = 0; i < ArrD.Count<clsDoping.stcDopingEx>(); i++)
                    {
                        ServerDT = GetServerTime(MainWB);
                        #region Допинг по времени, который нужно было бы съесть за долго до, переносим на завтра!
                        if (ArrD[i].Event == clsDoping.DopingEvent.Timer && ServerDT - ArrD[i].StartDT >= new TimeSpan(0, 30, 0)) ArrD[i].StartDT = ArrD[i].StartDT.AddDays(1);
                        #endregion
                        if (ArrD[i].StopDT <= ServerDT) ArrD[i].Done = false; //Можно снова кушать съеденные ранее по ивентам допинги
                        if (ArrD[i].StartDT < ServerDT) //Проверка, быть может, чегото нехватало и мы сдвинули старт или просто по таймеру или всегда
                        {
                            if (ArrD[i].Event == clsDoping.DopingEvent.EnemyLvl && !ArrD[i].Done &&
                                (   (ArrD.Equals(Me.ArrRatDoping) && Me.RatHunting.Lvl == ArrD[i].StartLvl && !Me.RatHunting.Stop) 
                                 || (ArrD.Equals(Me.ArrOilLeninDoping) && Me.OilLeninHunting.Lvl == ArrD[i].StartLvl && !Me.OilLeninHunting.Stop)
                               )) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if ((ArrD[i].Event == clsDoping.DopingEvent.Timer || ArrD[i].Event == clsDoping.DopingEvent.Allways) & !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if (ArrD[i].Event == clsDoping.DopingEvent.PVP && TimeToGoGrpFight(GroupFightType.PVP) && !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if (ArrD[i].Event == clsDoping.DopingEvent.Rat && Me.Rat.Stop && Me.Rat.Val > 0 && !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if (ArrD[i].Event == clsDoping.DopingEvent.Neft && Me.OilHunting.Stop && !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if (ArrD[i].Event == clsDoping.DopingEvent.Agent && Me.AgentHunting.Stop && !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                            if (ArrD[i].Event == clsDoping.DopingEvent.HC && !Me.HCHunting.Stop && Me.HCHunting.Victims >= 5 && Me.HCHunting.Search && !ArrD[i].Done) bRet &= Dopings(ref ArrD, DopingAction.Use, i);
                        }                                                 
                    }
                    return bRet;
                case DopingAction.Use:
                    if (ArrD[Index].Items == null) return true; //Пустой допинг в котором есть только ивент
                    #region Инициализация
                    bRet = false; //Удачно ли приняли хоть что-нибудь?
                    #endregion
                    if (Dopings(ref ArrD, DopingAction.CheckTime, Index)) //Проверка времён и наличия нужных допингов
                    {
                        if (clsDoping.AlreadyEated[Enum.GetValues(typeof(clsDoping.DopingType)).Length]) bRet = true; //Все допинги из списка, уже были сьедены заранее, просто пересчитываем стоперы.
                        else
                        {
                            #region Как минимум один допинг ещё не был использован, закупаемся и кушаем.
                            #region Ленинопровод и синхронизация не подтверждена, не даём кушать недостающие допинги!
                            if (clsDoping.NeedToBuy[(int)clsDoping.DopingType.RatSyncLvl]) return true; //Выходим без пометки, о съеденном допинге проверим после следующей крысы!
                            #endregion 
                            #region Закупка необходимых допингов сразу, чтоб не палиться
                            bRet = true; //Удачно ли приняли хоть что-нибудь?
                            foreach (clsDoping.DopingType DType in ArrD[Index].Items)
                            {
                                switch (DType) //Adv -> Нефтяные, Ex -> %-ные.
                                {
                                    #region Здоровье
                                    case clsDoping.DopingType.Gum1:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1]) bRet &= BuyItems(MainWB, ShopItems.Gum1);
                                        break;
                                    case clsDoping.DopingType.Gum1Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum1Adv);
                                        break;
                                    case clsDoping.DopingType.Gum1Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum1Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum1Ex);
                                        break;
                                    #endregion
                                    #region Сила
                                    case clsDoping.DopingType.Gum2:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2]) bRet &= BuyItems(MainWB, ShopItems.Gum2);
                                        break;
                                    case clsDoping.DopingType.Gum2Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum2Adv);
                                        break;
                                    case clsDoping.DopingType.Gum2Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum2Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum2Ex);
                                        break;
                                    #endregion
                                    #region Ловкость
                                    case clsDoping.DopingType.Gum3:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3]) bRet &= BuyItems(MainWB, ShopItems.Gum3);
                                        break;
                                    case clsDoping.DopingType.Gum3Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum3Adv);
                                        break;
                                    case clsDoping.DopingType.Gum3Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum3Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum3Ex);
                                        break;
                                    #endregion
                                    #region Выносливость
                                    case clsDoping.DopingType.Gum4:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4]) bRet &= BuyItems(MainWB, ShopItems.Gum4);
                                        break;
                                    case clsDoping.DopingType.Gum4Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum4Adv);
                                        break;
                                    case clsDoping.DopingType.Gum4Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum4Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum4Ex);
                                        break;
                                    #endregion
                                    #region Хитрость
                                    case clsDoping.DopingType.Gum5:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5]) bRet &= BuyItems(MainWB, ShopItems.Gum5);
                                        break;
                                    case clsDoping.DopingType.Gum5Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum5Adv);
                                        break;
                                    case clsDoping.DopingType.Gum5Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum5Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum5Ex);
                                        break;
                                    #endregion
                                    #region Внимательность
                                    case clsDoping.DopingType.Gum6:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6]) bRet &= BuyItems(MainWB, ShopItems.Gum6);
                                        break;
                                    case clsDoping.DopingType.Gum6Adv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Adv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Adv]) bRet &= BuyItems(MainWB, ShopItems.Gum6Adv);
                                        break;
                                    case clsDoping.DopingType.Gum6Ex:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Ex] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Gum6Ex]) bRet &= BuyItems(MainWB, ShopItems.Gum6Ex);
                                        break;
                                    #endregion
                                    #region Пяни
                                    case clsDoping.DopingType.Pyani:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Pyani] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Pyani]) bRet &= BuyItems(MainWB, ShopItems.Pyani);
                                        break;
                                    #endregion
                                    #region Творожок
                                    case clsDoping.DopingType.Tvorog:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tvorog] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tvorog]) bRet &= BuyItems(MainWB, ShopItems.Tvorog);
                                        break;
                                    #endregion
                                    #region Валуйки
                                    case clsDoping.DopingType.Valujki:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Valujki] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Valujki]) bRet &= BuyItems(MainWB, ShopItems.Valujki);
                                        break;
                                    #endregion
                                    #region Валуйки "Heavy Edition"
                                    case clsDoping.DopingType.ValujkiAdv:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.ValujkiAdv] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.ValujkiAdv]) bRet &= BuyItems(MainWB, ShopItems.ValujkiAdv);
                                        break;
                                    #endregion
                                    #region Противогаз
                                    case clsDoping.DopingType.GasMask:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.GasMask] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.GasMask]) bRet &= BuyItems(MainWB, ShopItems.GasMask);
                                        break;
                                    #endregion
                                    #region Респиратор
                                    case clsDoping.DopingType.Respirator:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Respirator] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Respirator]) bRet &= BuyItems(MainWB, ShopItems.Respirator);
                                        break;
                                    #endregion
                                    #region Чай
                                    case clsDoping.DopingType.Tea1:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea1] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea1]) bRet &= BuyItems(MainWB, ShopItems.Tea1);
                                        break;
                                    case clsDoping.DopingType.Tea4:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea4] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea4]) bRet &= BuyItems(MainWB, ShopItems.Tea4);
                                        break;
                                    case clsDoping.DopingType.Tea7:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea7] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea7]) bRet &= BuyItems(MainWB, ShopItems.Tea7);
                                        break;
                                    case clsDoping.DopingType.Tea10:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea10] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea10]) bRet &= BuyItems(MainWB, ShopItems.Tea10);
                                        break;
                                    case clsDoping.DopingType.Tea15:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea15] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Tea15]) bRet &= BuyItems(MainWB, ShopItems.Tea15);
                                        break;
                                    #endregion
                                    #region Шоколад
                                    case clsDoping.DopingType.Shoko1:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko1] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko1]) bRet &= BuyItems(MainWB, ShopItems.Shoko1);
                                        break;
                                    case clsDoping.DopingType.Shoko4:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko4] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko4]) bRet &= BuyItems(MainWB, ShopItems.Shoko4);
                                        break;
                                    case clsDoping.DopingType.Shoko7:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko7] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko7]) bRet &= BuyItems(MainWB, ShopItems.Shoko7);
                                        break;
                                    case clsDoping.DopingType.Shoko10:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko10] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko10]) bRet &= BuyItems(MainWB, ShopItems.Shoko10);
                                        break;
                                    case clsDoping.DopingType.Shoko15:
                                        if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko15] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.Shoko15]) bRet &= BuyItems(MainWB, ShopItems.Shoko15);
                                        break;
                                    #endregion
                                }
                            }
                            #endregion
                            if (bRet) //Закупка допингов прошла успешно, все допинги закуплены!
                            {
                                UpdateStatus("@ " + DateTime.Now + " Настало твоё время Валера! Ой мл@ не то, ... пожалуй перекушу!");
                                #region Партбилеты
                                if (!clsDoping.AlreadyEated[(int)clsDoping.DopingType.LeninTicket] && clsDoping.NeedToBuy[(int)clsDoping.DopingType.LeninTicket])
                                {
                                    if (Settings.AllowPartBilet) Me.OilLeninHunting.AllowPartBilet = true; //Разрешение использовать билеты, сколько понадобится для победы!
                                    else //Подозрительность на минимум 3 боя.
                                    {
                                        GoToPlace(MainWB, Place.Oil);
                                        while (Convert.ToInt32(frmMain.GetJavaVar(MainWB, "NeftLenin.partbilet")) >= Convert.ToInt32(frmMain.GetJavaVar(MainWB, "$(\"#content .part-bilet\").text()")) && //Есть партбилеты?
                                               (int)frmMain.GetJavaVar(MainWB, "NeftLenin.maxsuspicion") - (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicion") < 3 * (int)frmMain.GetJavaVar(MainWB, "NeftLenin.suspicionPrice.duel") //Нужно скинуть подозрительность?
                                              )
                                        {
                                            UpdateStatus("* " + DateTime.Now + " Сдаю макулатуру, скидываю подозрительность!");
                                            frmMain.GetJavaVar(MainWB, "NeftLenin.reset(2);");
                                            IsWBComplete(MainWB, 500, 1500); //IsAjaxComplete(MainWB, 500, 1500);
                                        }
                                    }                                                                        
                                }
                                #endregion
                                regex = new Regex("((/player/)$)");
                                if (!regex.IsMatch(frmMain.GetDocumentURL(MainWB))) GoToPlace(MainWB, Place.Player); //Что-то покупал, нужно снова на страничку игрока!                                
                                #region Поедание допингов
                                foreach (clsDoping.DopingType DType in ArrD[Index].Items)
                                {
                                    switch (DType) //Adv -> Нефтяные, Ex -> %-ные.
                                    {
                                        #region Все свободные машинки
                                        case clsDoping.DopingType.CarAll:
                                            for (int RidePlace = Me.CarRide.RideTimeout.Count() - 1; RidePlace >= 0; RidePlace--)
                                            {
                                                if (Me.CarRide.RideTimeout[RidePlace] != new DateTime() && Me.CarRide.RideTimeout[RidePlace] < DateTime.Now)
                                                {
                                                    Automobile(AutomobileAction.Ride, RidePlace); //bRet не переписваем всегда true, отправил, не отправил не важно!
                                                }
                                            }
                                            break;
                                        #endregion
                                        #region Здоровье
                                        case clsDoping.DopingType.Gum1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1] || EatDrog(MainWB, ShopItems.Gum1);
                                            break;
                                        case clsDoping.DopingType.Gum1Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Adv] || EatDrog(MainWB, ShopItems.Gum1Adv);
                                            break;
                                        case clsDoping.DopingType.Gum1Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum1Ex] || EatDrog(MainWB, ShopItems.Gum1Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail1] || EatDrog(MainWB, ShopItems.Coctail1);
                                            break;
                                        case clsDoping.DopingType.Car4:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car4] || Automobile(AutomobileAction.Ride, 4);
                                            break;
                                        case clsDoping.DopingType.Car10:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car10] || Automobile(AutomobileAction.Ride, 10);
                                            break;
                                        case clsDoping.DopingType.Car16:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car16] || Automobile(AutomobileAction.Ride, 16);
                                            break;
                                        #endregion
                                        #region Сила
                                        case clsDoping.DopingType.Gum2:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2] || EatDrog(MainWB, ShopItems.Gum2);
                                            break;
                                        case clsDoping.DopingType.Gum2Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Adv] || EatDrog(MainWB, ShopItems.Gum2Adv);
                                            break;
                                        case clsDoping.DopingType.Gum2Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum2Ex] || EatDrog(MainWB, ShopItems.Gum2Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail2:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail2] || EatDrog(MainWB, ShopItems.Coctail2);
                                            break;
                                        case clsDoping.DopingType.Car6:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car6] || Automobile(AutomobileAction.Ride, 6);
                                            break;
                                        case clsDoping.DopingType.Car12:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car12] || Automobile(AutomobileAction.Ride, 12);
                                            break;
                                        case clsDoping.DopingType.Car18:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car18] || Automobile(AutomobileAction.Ride, 18);
                                            break;
                                        #endregion
                                        #region Ловкость
                                        case clsDoping.DopingType.Gum3:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3] || EatDrog(MainWB, ShopItems.Gum3);
                                            break;
                                        case clsDoping.DopingType.Gum3Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Adv] || EatDrog(MainWB, ShopItems.Gum3Adv);
                                            break;
                                        case clsDoping.DopingType.Gum3Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum3Ex] || EatDrog(MainWB, ShopItems.Gum3Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail3:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail3] || EatDrog(MainWB, ShopItems.Coctail3);
                                            break;
                                        case clsDoping.DopingType.Car5:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car5] || Automobile(AutomobileAction.Ride, 5);
                                            break;
                                        case clsDoping.DopingType.Car11:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car11] || Automobile(AutomobileAction.Ride, 11);
                                            break;
                                        case clsDoping.DopingType.Car17:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car17] || Automobile(AutomobileAction.Ride, 17);
                                            break;
                                        #endregion
                                        #region Выносливость
                                        case clsDoping.DopingType.Gum4:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4] || EatDrog(MainWB, ShopItems.Gum4);
                                            break;
                                        case clsDoping.DopingType.Gum4Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Adv] || EatDrog(MainWB, ShopItems.Gum4Adv);
                                            break;
                                        case clsDoping.DopingType.Gum4Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum4Ex] || EatDrog(MainWB, ShopItems.Gum4Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail4:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail4] || EatDrog(MainWB, ShopItems.Coctail4);
                                            break;
                                        case clsDoping.DopingType.Car2:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car2] || Automobile(AutomobileAction.Ride, 2);
                                            break;
                                        case clsDoping.DopingType.Car8:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car8] || Automobile(AutomobileAction.Ride, 8);
                                            break;
                                        case clsDoping.DopingType.Car14:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car14] || Automobile(AutomobileAction.Ride, 14);
                                            break;
                                        #endregion
                                        #region Хитрость
                                        case clsDoping.DopingType.Gum5:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5] || EatDrog(MainWB, ShopItems.Gum5);
                                            break;
                                        case clsDoping.DopingType.Gum5Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Adv] || EatDrog(MainWB, ShopItems.Gum5Adv);
                                            break;
                                        case clsDoping.DopingType.Gum5Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum5Ex] || EatDrog(MainWB, ShopItems.Gum5Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail5:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail5] || EatDrog(MainWB, ShopItems.Coctail5);
                                            break;
                                        case clsDoping.DopingType.Car3:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car3] || Automobile(AutomobileAction.Ride, 3);
                                            break;
                                        case clsDoping.DopingType.Car9:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car9] || Automobile(AutomobileAction.Ride, 9);
                                            break;
                                        case clsDoping.DopingType.Car15:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car15] || Automobile(AutomobileAction.Ride, 15);
                                            break;
                                        #endregion
                                        #region Внимательность
                                        case clsDoping.DopingType.Gum6:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6] || EatDrog(MainWB, ShopItems.Gum6);
                                            break;
                                        case clsDoping.DopingType.Gum6Adv:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Adv] || EatDrog(MainWB, ShopItems.Gum6Adv);
                                            break;
                                        case clsDoping.DopingType.Gum6Ex:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Gum6Ex] || EatDrog(MainWB, ShopItems.Gum6Ex);
                                            break;
                                        case clsDoping.DopingType.Coctail6:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Coctail6] || EatDrog(MainWB, ShopItems.Coctail6);
                                            break;
                                        case clsDoping.DopingType.Car1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car1] || Automobile(AutomobileAction.Ride, 1);
                                            break;
                                        case clsDoping.DopingType.Car7:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car7] || Automobile(AutomobileAction.Ride, 7);
                                            break;
                                        case clsDoping.DopingType.Car13:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car13] || Automobile(AutomobileAction.Ride, 13);
                                            break;
                                        #endregion
                                        #region Пяни
                                        case clsDoping.DopingType.Pyani:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Pyani] || EatDrog(MainWB, ShopItems.Pyani);
                                            break;
                                        #endregion
                                        #region Творожок
                                        case clsDoping.DopingType.Tvorog:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tvorog] || EatDrog(MainWB, ShopItems.Tvorog);
                                            break;
                                        #endregion
                                        #region Чай
                                        case clsDoping.DopingType.Tea1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea1] || EatDrog(MainWB, ShopItems.Tea1);
                                            break;
                                        case clsDoping.DopingType.Tea4:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea4] || EatDrog(MainWB, ShopItems.Tea4);
                                            break;
                                        case clsDoping.DopingType.Tea7:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea7] || EatDrog(MainWB, ShopItems.Tea7);
                                            break;
                                        case clsDoping.DopingType.Tea10:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea10] || EatDrog(MainWB, ShopItems.Tea10);
                                            break;
                                        case clsDoping.DopingType.Tea15:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Tea15] || EatDrog(MainWB, ShopItems.Tea15);
                                            break;
                                        #endregion
                                        #region Шоколад
                                        case clsDoping.DopingType.Shoko1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko1] || EatDrog(MainWB, ShopItems.Shoko1);
                                            break;
                                        case clsDoping.DopingType.Shoko4:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko4] || EatDrog(MainWB, ShopItems.Shoko4);
                                            break;
                                        case clsDoping.DopingType.Shoko7:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko7] || EatDrog(MainWB, ShopItems.Shoko7);
                                            break;
                                        case clsDoping.DopingType.Shoko10:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko10] || EatDrog(MainWB, ShopItems.Shoko10);
                                            break;
                                        case clsDoping.DopingType.Shoko15:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Shoko15] || EatDrog(MainWB, ShopItems.Shoko15);
                                            break;
                                        #endregion
                                        #region Случайная характеристика 1
                                        case clsDoping.DopingType.Car19:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car19] || Automobile(AutomobileAction.Ride, 19);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Чайка]
                                        case clsDoping.DopingType.Car20:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car20] || Automobile(AutomobileAction.Ride, 20);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Тигр]
                                        case clsDoping.DopingType.Car21:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car21] || Automobile(AutomobileAction.Ride, 21);
                                            break;
                                        #endregion
                                        #region Случайная характеристика 2
                                        case clsDoping.DopingType.Car22:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car22] || Automobile(AutomobileAction.Ride, 22);
                                            break;
                                        #endregion
                                        #region Случайная характеристика 3
                                        case clsDoping.DopingType.Car23:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car23] || Automobile(AutomobileAction.Ride, 23);
                                            break;
                                        #endregion
                                        #region Случайная характеристика 4
                                        case clsDoping.DopingType.Car24:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car24] || Automobile(AutomobileAction.Ride, 24);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Новогодний грузовик]
                                        case clsDoping.DopingType.Car25:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car25] || Automobile(AutomobileAction.Ride, 25);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Конь]
                                        case clsDoping.DopingType.Car26:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car26] || Automobile(AutomobileAction.Ride, 26);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Броневик *-*****]
                                        case clsDoping.DopingType.Car27:
                                        case clsDoping.DopingType.Car28:
                                        case clsDoping.DopingType.Car29:
                                        case clsDoping.DopingType.Car30:
                                        case clsDoping.DopingType.Car31:
                                            bRet &= clsDoping.AlreadyEated[(int)DType - (int)clsDoping.DopingType.Car4 + 1] || Automobile(AutomobileAction.Ride, (int)DType - (int)clsDoping.DopingType.Car4 + 1);
                                            break;
                                        #endregion
                                        #region Случайная характеристика [Эвакуатор]
                                        case clsDoping.DopingType.Car34:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car34] || Automobile(AutomobileAction.Ride, 34);
                                            break;
                                        #endregion
                                        #region Вертолёт 1
                                        case clsDoping.DopingType.Car35:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car35] || Automobile(AutomobileAction.Ride, 35);
                                            break;
                                        #endregion
                                        #region Вертолёт 2
                                        case clsDoping.DopingType.Car36:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car36] || Automobile(AutomobileAction.Ride, 36);
                                            break;
                                        #endregion
                                        #region Вертолёт «Борт №1»
                                        case clsDoping.DopingType.Car37:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car37] || Automobile(AutomobileAction.Ride, 37);
                                            break;
                                        #endregion
                                        #region Вертолёт «Черная акула»
                                        case clsDoping.DopingType.Car38:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car38] || Automobile(AutomobileAction.Ride, 38);
                                            break;
                                        #endregion
                                        #region Де-Лориан
                                        case clsDoping.DopingType.Car39:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car39] || Automobile(AutomobileAction.Ride, 39);
                                            break;
                                        #endregion
                                        #region Де-Лориан ускоренный
                                        case clsDoping.DopingType.Car40:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Car40] || Automobile(AutomobileAction.Ride, 40);
                                            break;
                                        #endregion

                                        #region Элексиры (витаминки, баржоми)
                                        case clsDoping.DopingType.Vitamin:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Vitamin] || EatDrog(MainWB, ShopItems.Vitamin);
                                            break;
                                        case clsDoping.DopingType.NovajaZhizn:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.NovajaZhizn] || EatDrog(MainWB, ShopItems.NovajaZhizn);
                                            break;
                                        case clsDoping.DopingType.Barjomi:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.Barjomi] || EatDrog(MainWB, ShopItems.Barjomi);
                                            break;
                                        case clsDoping.DopingType.AquaDeminerale:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.AquaDeminerale] || EatDrog(MainWB, ShopItems.AquaDeminerale);
                                            break;
                                        case clsDoping.DopingType.WeakNPC1:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC1] || (!clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC1] && EatDrog(MainWB, ShopItems.WeakNPC1)) || Settings.NoSauceNoProblem;
                                            break;
                                        case clsDoping.DopingType.WeakNPC2:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC2] || (!clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC2] && EatDrog(MainWB, ShopItems.WeakNPC2)) || Settings.NoSauceNoProblem;
                                            break;
                                        case clsDoping.DopingType.WeakNPC3:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.WeakNPC3] || (!clsDoping.NeedToBuy[(int)clsDoping.DopingType.WeakNPC3] && EatDrog(MainWB, ShopItems.WeakNPC3)) || Settings.NoSauceNoProblem;
                                            break;
                                        #endregion
                                        #region Умная/глупая конфеты
                                        case clsDoping.DopingType.CandyExp:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyExp] || (!clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyExp] && EatDrog(MainWB, ShopItems.CandyExp)) || Settings.NoCandyNoProblem;
                                            break;
                                        case clsDoping.DopingType.CandyAntiExp:
                                            bRet &= clsDoping.AlreadyEated[(int)clsDoping.DopingType.CandyAntiExp] || (!clsDoping.NeedToBuy[(int)clsDoping.DopingType.CandyAntiExp] && EatDrog(MainWB, ShopItems.CandyAntiExp)) || Settings.NoCandyNoProblem;
                                            break;
                                        #endregion
                                    }
                                }
                                #endregion
                            }
                            else UpdateStatus("! " + DateTime.Now + " Походу нас обокрали, не нахожу средств на допинги!");
                            #endregion
                        }                                               
                    }
                    else UpdateStatus("! " + DateTime.Now + " Походу нас обокрали, не нахожу спец-допингов на складе!");
                    if (bRet) //Всё прошло успешно?
                    {
                        ArrD[Index].Done = (ArrD[Index].Event == clsDoping.DopingEvent.Allways || ArrD[Index].Event == clsDoping.DopingEvent.PVP) ? false : true; //Всегда и PVP кушаем по старту, а не по стоперу, для того чтоб всегда быть под всеми допингами!
                        #region Расчёт нового времени поедания допингов для стопперов + сброс стопперов
                        if (ArrD[Index].Done) //Кушать заново можно по таймеру или при наступлении новых суток + 30 мин на стопперы.
                        {
                            ServerDT = GetServerTime(MainWB);
                            if (ArrD[Index].Event != clsDoping.DopingEvent.Timer) ArrD[Index].StopDT = ServerDT.Date.AddDays(1); //Присет времени
                            switch (ArrD[Index].Event)
                            {
                                case clsDoping.DopingEvent.Timer:
                                    if (ServerDT < ArrD[Index].StopDT) ArrD[Index].StartDT = ArrD[Index].StartDT.AddDays(1); //У этого допинга уже есть стопер - переносим его поедание на следующий день!
                                    break;
                                case clsDoping.DopingEvent.Agent:
                                    ArrD[Index].StopDT += new TimeSpan(0, 30, 0);
                                    Me.AgentHunting.Val = 0;
                                    Me.AgentHunting.Stop = false;
                                    break;
                                case clsDoping.DopingEvent.Neft:
                                    ArrD[Index].StopDT += new TimeSpan(0, 30, 0);
                                    Me.OilHunting.Val = 0;
                                    Me.OilHunting.Stop = false;
                                    break;
                                case clsDoping.DopingEvent.Rat:
                                    ArrD[Index].StopDT += new TimeSpan(0, 30, 0);
                                    Me.Rat.Val = 0;
                                    Me.Rat.Stop = false;
                                    break;
                                case clsDoping.DopingEvent.EnemyLvl:
                                    ArrD[Index].StopDT = Me.RatHunting.RestartDT;
                                    break;
                            }
                        }
                        #endregion
                        return true;
                    }
                    else if (ArrD.Equals(Me.ArrUsualDoping)) ArrD[Index].StartDT = (ArrD[Index].Event == clsDoping.DopingEvent.Timer ? ArrD[Index].StartDT.AddDays(1) : GetServerTime(MainWB).AddHours(1)); //Переносим поедание на час, ибо чтото прошло не так! (Только в обычных допингах, иначе после пересохранения, может побежать без допа на крыс или ленинца!)
                    break;
            }
            return false;
        }
        public bool EatDrog(WebBrowser WB, ShopItems SI) //NOK ne otlichaet Neftjanye i obychnye zhujki...
        {
            BugReport("EatDrog ~" + SI);

            int i;
            object Info;
            HtmlElement HtmlEl;
            HtmlElement[] ArrHtmlEl;
            EatDopingInfo DopingInfo;

            if (frmMain.GetDocumentURL(WB).EndsWith(Settings.ServerURL + "/player/")) //Если не успел и висим на крысе может быть иная страничка!
            {
                switch (SI)
                {
                    case ShopItems.Snikers:         DopingInfo = new EatDopingInfo { BlockID = "heal-accordion", BtnID = new string[] { "inventory-snikers-btn" } }; break;
                    case ShopItems.Me100:           DopingInfo = new EatDopingInfo { BlockID = "heal-accordion", BtnID = new string[] { "inventory-mikstura-btn" } }; break;
                    case ShopItems.Me50:            DopingInfo = new EatDopingInfo { BlockID = "heal-accordion", BtnID = new string[] { "inventory-sirop-btn" } }; break;
                    case ShopItems.Pet100:          DopingInfo = new EatDopingInfo { BlockID = "pet-accordion", BtnID = new string[] { "petfood2" } }; break; //Корм для питомца +100%
                    case ShopItems.Pet50:           DopingInfo = new EatDopingInfo { BlockID = "pet-accordion", BtnID = new string[] { "petfood1" } }; break;  //Корм для питомца +50%
                    //Health, Strength, Dexterity, Endurance, Cunning, Attentiveness                    
                    //Жуйки:
                    case ShopItems.Gum1Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_health-btn" } }; break;
                    case ShopItems.Gum2Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_strength-btn" } }; break;
                    case ShopItems.Gum3Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_dexterity-btn" } }; break;
                    case ShopItems.Gum4Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_resistance-btn" } }; break;
                    case ShopItems.Gum5Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_intuition-btn" } }; break;
                    case ShopItems.Gum6Ex:          DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_attention-btn" } }; break;
                    case ShopItems.Gum1:
                    case ShopItems.Gum1Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_health2-btn" } }; break;
                    case ShopItems.Gum2:
                    case ShopItems.Gum2Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_strength2-btn" } }; break;
                    case ShopItems.Gum3:
                    case ShopItems.Gum3Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_dexterity2-btn" } }; break;
                    case ShopItems.Gum4:
                    case ShopItems.Gum4Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_resistance2-btn" } }; break;
                    case ShopItems.Gum5:
                    case ShopItems.Gum5Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_intuition2-btn" } }; break;
                    case ShopItems.Gum6:
                    case ShopItems.Gum6Adv: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-gum_attention2-btn" } }; break;
                    //Коктейли:
                    case ShopItems.Coctail1: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_health-btn" }, PicName = "health.png" }; break;
                    case ShopItems.Coctail2: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_strength-btn" }, PicName = "strength.png" }; break;
                    case ShopItems.Coctail3: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_dexterity-btn" }, PicName = "dexterity.png" }; break;
                    case ShopItems.Coctail4: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_resistance-btn" }, PicName = "resistance.png" }; break;
                    case ShopItems.Coctail5: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_intuition-btn" }, PicName = "intuition.png" }; break;
                    case ShopItems.Coctail6: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-shake_attention-btn" }, PicName = "attention.png" }; break;
                    //Шоко-Чаи
                    case ShopItems.Tea1: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_s4-btn", "inventory-chocolate_s5-btn", "inventory-chocolate_s6-btn" }, BoxID = "inventory-chocolates_13-btn" }; break;
                    case ShopItems.Shoko1: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_s1-btn", "inventory-chocolate_s2-btn", "inventory-chocolate_s3-btn" }, BoxID = "inventory-chocolates_10-btn" }; break;
                    case ShopItems.Tea4: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_m4-btn", "inventory-chocolate_m5-btn", "inventory-chocolate_m6-btn" }, BoxID = "inventory-chocolates_14-btn" }; break;
                    case ShopItems.Shoko4: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_m1-btn", "inventory-chocolate_m2-btn", "inventory-chocolate_m3-btn" }, BoxID = "inventory-chocolates_11-btn" }; break;
                    case ShopItems.Tea7: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_l4-btn", "inventory-chocolate_l5-btn", "inventory-chocolate_l6-btn" }, BoxID = "inventory-chocolates_15-btn" }; break;
                    case ShopItems.Shoko7: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_l1-btn", "inventory-chocolate_l2-btn", "inventory-chocolate_l3-btn" }, BoxID = "inventory-chocolates_12-btn" }; break;
                    case ShopItems.Tea10: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_xl4-btn", "inventory-chocolate_xl5-btn", "inventory-chocolate_xl6-btn" }, BoxID = "inventory-chocolates_16-btn" }; break;
                    case ShopItems.Shoko10: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_xl1-btn", "inventory-chocolate_xl2-btn", "inventory-chocolate_xl3-btn" }, BoxID = "inventory-chocolates_17-btn" }; break;
                    case ShopItems.Tea15: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_xxl4-btn", "inventory-chocolate_xxl5-btn", "inventory-chocolate_xxl6-btn" }, BoxID = "inventory-chocolates_18-btn" }; break;
                    case ShopItems.Shoko15: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-chocolate_xxl1-btn", "inventory-chocolate_xxl2-btn", "inventory-chocolate_xxl3-btn" }, BoxID = "inventory-chocolates_19-btn" }; break;
                    //Прочее
                    case ShopItems.Pyani: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-pyani-btn" } }; break;
                    case ShopItems.Tvorog: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-tvorozhok-btn" } }; break;
                    case ShopItems.Vitamin: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_hp50-btn" } }; break;
                    case ShopItems.NovajaZhizn: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_ny2013-btn" } }; break;
                    case ShopItems.Barjomi: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_critpower100-btn" } }; break;
                    case ShopItems.AquaDeminerale: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_critpower20-btn" } }; break;
                    case ShopItems.WeakNPC1: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_npc1-btn" } }; break;
                    case ShopItems.WeakNPC2: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_npc2-btn" } }; break;
                    case ShopItems.WeakNPC3: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-eliksir_x2-btn" } }; break;
                    case ShopItems.CandyExp: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-xpdoping-btn" }, PicName = "lamp-pack.png" }; break;
                    case ShopItems.CandyAntiExp: DopingInfo = new EatDopingInfo { BlockID = "gums-accordion", BtnID = new string[] { "inventory-xpdoping-btn" }, PicName = "anti-lamp.png" }; break;
                    //          
                    default: DopingInfo = new EatDopingInfo(); break;
                }
                
                #region Функция вскрытия коробок + мониторинг
                bool bRet = false;
                while (DopingInfo.BoxID != null && (HtmlEl = frmMain.GetDocument(WB).GetElementById(DopingInfo.BoxID)) != null) //Нашел то, что было приказано вскрыть!
                {
                    bRet = true; //Было что вскрывать!
                    for (i = 1; i < 4; i++)
                    {
                        if (WaitDrugEated(WB, HtmlEl)) break;
                        else 
                        {
                            UpdateStatus("@ " + DateTime.Now + " Попытка № " + i + ": Нееепонял, так не пойдеть! Эти коробки, что гвоздями забили?!");
                            frmMain.RefreshURL(WB, Settings.ServerURL);
                            IsWBComplete(WB);
                        }
                    }
                }
                #region Было что вскрывать! (Иначе не обновляет багажник!)
                if (bRet)
                {
                    frmMain.RefreshURL(WB, Settings.ServerURL);
                    IsWBComplete(WB);
                }
                #endregion
                #endregion

                switch (DopingInfo.BlockID)
                {
                    case "pet-accordion":
                        #region Пэты
                        Info = frmMain.GetJavaVar(MainWB, "$(\"#content .slots .slot-pet.notempty\").html()");
                        if (Info != DBNull.Value) //
                        {
                            Match match = Regex.Match((string)Info, "/pets/([0-9-])+[.]png"); //Именно этот питомец сейчас со мной (Картинка с боевым питомцем в предпоследнем элементе, последний беговой при условии показывать)
                            foreach (HtmlElement PetHtml in frmMain.GetDocument(WB).GetElementById("pet-accordion").GetElementsByTagName("img"))
                            {
                                if (PetHtml.GetAttribute("src").EndsWith(match.Value)) //Тот же самый питомец, что сейчас со мной?
                                {
                                    string PetID = PetHtml.GetAttribute("data-id");
                                    #region Переходим на страничку нужного нам питомца
                                    frmMain.NavigateURL(WB, Settings.ServerURL + "/petarena/train/" + PetID + "/");
                                    IsWBComplete(WB);
                                    #endregion
                                    #region Употребляем выбранный корм
                                    foreach (HtmlElement FoodHtml in frmMain.GetDocument(WB).GetElementById("content").GetElementsByTagName("dd")[0].GetElementsByTagName("IMG"))
                                    {
                                        if (FoodHtml.GetAttribute("src").IndexOf(DopingInfo.BtnID[0]) > 0) //Выдираю нужную еду по картинке указанной в пути.
                                        {
                                            //PRIMER: $.post('/petarena/feedpet/' + params.pet + '/' + params.food + '/',{}, function(response))
                                            string FoodID = FoodHtml.GetAttribute("data-id");
                                            frmMain.GetJavaVar(WB, "$.post(\"/petarena/feedpet/" + PetID + "/" + FoodID + "/\", {});");
                                            Wait(1500, 2000);
                                            GoToPlace(WB, Place.Player);
                                            return true; //Уже использовал, хватит, уходим!
                                        }
                                    }
                                    #endregion
                                    break;
                                }
                            }
                        }
                        break;
                        #endregion
                    case "heal-accordion":
                    case "gums-accordion":
                        #region Мои допинги + лечение
                        #region Инициализация
                        bRet = true;
                        #endregion
                        //Одинаковые элементы хоть и сгрупированны, но, всёравно, рудные и нефтяные, процентные и зефирки, имеют одинаковые ID.
                        foreach (string BtnID in DopingInfo.BtnID)
                        {
                            HtmlEl = null; //Тут будет храниться то, что необходимо покушать.                
                            for (i = 1; i < 4; i++)
                            {
                                ArrHtmlEl = frmMain.GetElementsById(WB, BtnID);
                                if (ArrHtmlEl != null)
                                {
                                    foreach (HtmlElement H in ArrHtmlEl)
                                    {
                                        switch (SI)
                                        {
                                            #region Коктейли
                                            case ShopItems.Coctail1:
                                            case ShopItems.Coctail2:
                                            case ShopItems.Coctail3:
                                            case ShopItems.Coctail4:
                                            case ShopItems.Coctail5:
                                            case ShopItems.Coctail6:
                                                if (HtmlEl == null)
                                                {
                                                    Info = frmMain.GetJavaVar(WB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                                    if (Settings.AllowCoctailAdv || Info.Equals(DopingInfo.PicName)) HtmlEl = H;
                                                }                                                                                              
                                                break;
                                            #endregion
                                            #region % -ные жвачки
                                            case ShopItems.Gum1Ex:
                                            case ShopItems.Gum2Ex:
                                            case ShopItems.Gum3Ex:
                                            case ShopItems.Gum4Ex:
                                            case ShopItems.Gum5Ex:
                                            case ShopItems.Gum6Ex:
                                                if (HtmlEl == null) 
                                                {
                                                    Info = frmMain.GetJavaVar(WB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].info.content");
                                                    Info = Regex.Match((string)Info, "(?<=[+])([0-9])+(?=%)").Value;
                                                    if ((Settings.PreferShokoZefir ? 25 : Settings.PreferZefir ? 20 : 15) >= Convert.ToInt32(Info)) HtmlEl = H; //Зефирки в багаже сами устанавливаются так, что сверху самые сильные!
                                                }                                                
                                                break;
                                            #endregion
                                            #region Моё лечение + сникерс
                                            case ShopItems.Me50:
                                            case ShopItems.Me100:
                                            case ShopItems.Snikers:
                                            #endregion
                                            #region Обычные жвачки
                                            case ShopItems.Gum1:
                                            case ShopItems.Gum2:
                                            case ShopItems.Gum3:
                                            case ShopItems.Gum4:
                                            case ShopItems.Gum5:
                                            case ShopItems.Gum6:
                                            #endregion
                                            #region Нефтяные жвачки
                                            case ShopItems.Gum1Adv:
                                            case ShopItems.Gum2Adv:
                                            case ShopItems.Gum3Adv:
                                            case ShopItems.Gum4Adv:
                                            case ShopItems.Gum5Adv:
                                            case ShopItems.Gum6Adv:
                                            #endregion
                                            #region Шоко-Чаи
                                            case ShopItems.Tea1:
                                            case ShopItems.Shoko1:
                                            case ShopItems.Tea4:
                                            case ShopItems.Shoko4:
                                            case ShopItems.Tea7:
                                            case ShopItems.Shoko7:
                                            case ShopItems.Tea10:
                                            case ShopItems.Shoko10:
                                            case ShopItems.Tea15:
                                            case ShopItems.Shoko15:
                                            #endregion
                                            #region Соусы
                                            case ShopItems.WeakNPC1:
                                            case ShopItems.WeakNPC2:
                                            case ShopItems.WeakNPC3:
                                                if (HtmlEl == null)
                                                {
                                                    HtmlEl = H;
                                                }                                                
                                                break;
                                            #endregion
                                            #region Иное (Витаминки, Баржоми, Пяни, Творог, Новая жизнь, Аквадеминерале)
                                            case ShopItems.Vitamin:
                                                if (HtmlEl == null //Если витаминок ещё небыло найдено!
                                                    || //Берём те на которых тикает таймер предпочитая ультра! (Так как в инвентаре сперва лежит ультра, то заносим её, после сверяем где тикающие, если ультра не тикает берём тикающие витаминки!)
                                                    (Regex.IsMatch((string)frmMain.GetJavaVar(WB, "m.items['" + Regex.Match(HtmlEl.Parent.InnerHtml, "(?<=data-id=\"?)([0-9])+") + "'].info.content"), "(([0-9])+ шт. до|Срок годности:) ([0-9 .:])+") ? false : Regex.IsMatch((string)frmMain.GetJavaVar(WB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=\"?)([0-9])+") + "'].info.content"), "(([0-9])+ шт. до|Срок годности:) ([0-9 .:])+"))
                                                   ) HtmlEl = H;                                               
                                                break;
                                            case ShopItems.Pyani:
                                            case ShopItems.Tvorog:
                                            case ShopItems.Barjomi:
                                            case ShopItems.NovajaZhizn:
                                            case ShopItems.AquaDeminerale:
                                                if (HtmlEl == null)
                                                {
                                                    HtmlEl = H;
                                                }                                                
                                                break;                                            
                                            #endregion
                                            #region Умные и глупые конфеты
                                            case ShopItems.CandyExp:
                                            case ShopItems.CandyAntiExp:
                                                if (HtmlEl == null)
                                                {
                                                    Info = frmMain.GetJavaVar(WB, "m.items['" + Regex.Match(H.Parent.InnerHtml, "(?<=data-id=(\")?)([0-9])+").Value + "'].obj.context.nameProp");
                                                    if (Info.Equals(DopingInfo.PicName)) HtmlEl = H;
                                                }                                                
                                                break;
                                            #endregion
                                        }                                                                  
                                    }
                                    if (HtmlEl != null)
                                    {
                                        #region Функция поедания + мониторинг оного
                                        //MessageBox.Show(HtmlEl.Parent.InnerHtml); return false;                                           
                                        if (WaitDrugEated(WB, HtmlEl))
                                        {
                                            bRet &= true;
                                            i = 4; //Уже скушал, нет смысла пробовать кушать дальше!
                                            if (SI == ShopItems.Snikers) UpdateStatus("* " + DateTime.Now + " Закинувшись сникерсом поломал 2 зуба о орешки, пойду отомщу прохожим!");
                                            break;
                                        }
                                        else
                                        {
                                            bRet = i < 3; //Покушать не удалось только на 3-ий раз!
                                            UpdateStatus("@ " + DateTime.Now + " Попытка № " + i + ": Нееепонял, так не пойдеть! Присел покушать, а зубы то я походу дома забыл!");
                                            GoToPlace(WB, Place.Player);
                                            break;
                                        }
                                        #endregion
                                    }  
                                }
                                else
                                {
                                    #region Не нашёл то, что было приказано поесть? Возможно всёже съел в прошлом цикле?!
                                    bRet = i > 1;
                                    i = 4; //Нет смысла пробовать кушать дальше!
                                    break;
                                    #endregion
                                }                            
                            }                    
                        }
                        if (bRet) return true; //Допинг был найден и съеден, нет смысла его покупать и пробовать снова!               
                        break;
                        #endregion
                    default: return false;
                }                
                if (BuyItems(WB, SI)) { GoToPlace(WB, Place.Player); return EatDrog(WB, SI); } //Не нашёл средства, пробуем купить и снова использовать!
            }
            return false;
        }
        public bool CookCoctail(CoctailAction CA, CoctailType CT = CoctailType.None)
        {
            int FruitsFound = 0;
            MatchCollection matches;
            DateTime ServerDT = GetServerTime(MainWB);

            switch (CA)
            {
                case CoctailAction.GetRecipe:
                    if (ServerDT > Me.CocktailRecipe.LastCheck.AddMinutes(10))
                    {
                        BugReport("CookCoctail ~GetRecipe");
                        #region Считывание и парсинг рецепта
                        #region Инициализация
                        int Retries = 0;
                        string Response;
                        string[] URL = { "moswarkok.ru", "moswarkok.in.ua", "moswarkok.net" };
                        Me.CocktailRecipe.Wrong = false;
                        #endregion
                    ReTry:
                        try 
                        {
                            if (Retries >= URL.Count()) return false; //Все сайты в дауне?
                            HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create("http://" + URL[Retries] + "//reporter.php?level=" + Me.Player.Level + " уровень");
                            httpRequest.KeepAlive = false;
                            httpRequest.Timeout = (Int32)(Settings.GagIE) * 1000;                            
                            httpRequest.UserAgent = (string)frmMain.GetJavaVar(MainWB, "navigator['userAgent']");
                            HttpWebResponse webResponse = (HttpWebResponse)httpRequest.GetResponse();
                            Response = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                        }
                        catch
                        {
                            Retries++;
                            UpdateStatus("@ " + DateTime.Now + " Сервер: " + URL[Retries] + " в дауне, пробую следующий!");
                            goto ReTry;
                        }                        
                        matches = Regex.Matches(Response, "alt=\"(?<Type>(\\w)+)\"((?!\"ideal\")[\\s\\S])+\"ideal\">(?<Count>([0-9](?<NotReady>-)?)+)"); //\"ideal\">([0-9])+
                        Me.CocktailRecipe.LastCheck = ServerDT;
                        Me.CocktailRecipe.Component = null;
                        for (int i = 0; i < matches.Count; i++)
                        {
                            if (!matches[i].Groups["NotReady"].Success)
                            {
                                Array.Resize<stcCoctailComponent>(ref Me.CocktailRecipe.Component, Me.CocktailRecipe.Component == null ? 1 : Me.CocktailRecipe.Component.Count<stcCoctailComponent>() + 1);
                                int x = Me.CocktailRecipe.Component.Count<stcCoctailComponent>() - 1;
                                switch (matches[i].Groups["Type"].Value)
                                {
                                    case "Апельсин":
                                        Me.CocktailRecipe.Component[x].Fruit = "orange";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Лимон":
                                        Me.CocktailRecipe.Component[x].Fruit = "lemon";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Яблоко":
                                        Me.CocktailRecipe.Component[x].Fruit = "apple";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Персик":
                                        Me.CocktailRecipe.Component[x].Fruit = "peach";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Ананас":
                                        Me.CocktailRecipe.Component[x].Fruit = "pineapple";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Банан":
                                        Me.CocktailRecipe.Component[x].Fruit = "banana";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Арбуз":
                                        Me.CocktailRecipe.Component[x].Fruit = "watermelon";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Дыня":
                                        Me.CocktailRecipe.Component[x].Fruit = "melon";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Малина":
                                        Me.CocktailRecipe.Component[x].Fruit = "raspberry";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Манго":
                                        Me.CocktailRecipe.Component[x].Fruit = "mango";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Киви":
                                        Me.CocktailRecipe.Component[x].Fruit = "kiwi";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Мандарин":
                                        Me.CocktailRecipe.Component[x].Fruit = "mandarin";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Виноград":
                                        Me.CocktailRecipe.Component[x].Fruit = "grapes";
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Лед":
                                        Me.CocktailRecipe.Component[x].Fruit = "ice"; //icecream_
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                    case "Газ":
                                        Me.CocktailRecipe.Component[x].Fruit = "gas"; //piece_
                                        Me.CocktailRecipe.Component[x].RecipeAmount = Convert.ToInt32(matches[i].Groups["Count"].Value);
                                        break;
                                }
                            }
                        }
                        #endregion
                    }                   
                    if (Me.CocktailRecipe.Component == null || Me.CocktailRecipe.Component.Count<stcCoctailComponent>() < 6 || Me.CocktailRecipe.Component[Me.CocktailRecipe.Component.Count<stcCoctailComponent>() - 1].Fruit != "gas" || Me.CocktailRecipe.Component[Me.CocktailRecipe.Component.Count<stcCoctailComponent>() - 2].Fruit != "ice") 
                    {
                        UpdateStatus("@ " + DateTime.Now + " Рецептик пока свежеват - в нём не хватает компонентов, я позже загляну!");
                        return false; //Не хватает компонентов для варения, необходимо: минимум 4 фрукта + газ + лёд
                    }
                    break;
                case CoctailAction.CheckRecipe:
                    BugReport("CookCoctail ~CheckRecipe");
                    if (!CookCoctail(CoctailAction.GetRecipe)) return false;
                    if (!frmMain.GetDocument(MainWB).GetElementById("content").GetAttribute("classname").Equals("nightclub-coctail-mixer")) GoToPlace(MainWB, Place.Nightclub, "/shakes");
                    object Info;
                    #region Проверка наличия лёдогенератора и газа
                    HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementById("comments").GetElementsByTagName("button");
                    for (int i = 0; i < frmMain.GetDocument(MainWB).GetElementById("comments").GetElementsByTagName("button").Count; i++)
                    {
			            UpdateMyInfo(MainWB);
                        if (Me.Wallet.Ore < 50)
                        {
                            UpdateStatus("@ " + DateTime.Now + " Эх я беднота, нет у меня денег на эти модные коктейльные приборы=(");
                            return false;
                        }
                        UpdateStatus("# " + DateTime.Now + " Вот денежка, - даёшь \"Льдогенератор/Газирователь\"!");
                        frmMain.InvokeMember(MainWB, frmMain.GetDocument(MainWB).GetElementById("comments").GetElementsByTagName("button")[0], "click");
                    }
                    matches = Regex.Matches(frmMain.GetDocument(MainWB).GetElementById("comments").InnerText, "([0-9])+(?= коктейл(ей|я|ь))");
                    if (matches.Count != 2) 
                    {
                        UpdateStatus("! " + DateTime.Now + " Дохтур посмотри меня! Проблемы при варении коктейлей!");
                        return false;
                    }
                    #endregion                      
                    #region Инициализация
                    Me.CocktailRecipe.SpecialComponent = new stcSpecialCoctailComponent[28];
                    Me.CocktailRecipe.RecipeTotalFruitsAmount = 0;
                    #endregion  
                    #region Проверка наличия фруктов
                    for (int i = 0; i < Me.CocktailRecipe.Component.Count(); i++) 
                    {
                        if (i < Me.CocktailRecipe.Component.Count() - 2) //Фрукты
                        {
                            Info = frmMain.GetJavaVar(MainWB, "$(\"#content .object-thumbs.fruits .action.action-get[data-code=" + Me.CocktailRecipe.Component[i].Fruit + "]\").attr(\"data-amount\")");
                            Me.CocktailRecipe.Component[i].StorageAmmount = Info == null ? 0 : Convert.ToInt32(Info);
                            Me.CocktailRecipe.Component[i].Use = (!Settings.UseMaxFruitProRecipe || Me.CocktailRecipe.Component[i].RecipeAmount <= Settings.MaxFruitProRecipe || (Settings.UseMinFruitIgnoreAmount && Me.CocktailRecipe.Component[i].StorageAmmount >= Settings.MinFruitIgnoreAmount)) && FruitsFound < 4;
                            if (Me.CocktailRecipe.Component[i].Use) { Me.CocktailRecipe.RecipeTotalFruitsAmount += Me.CocktailRecipe.Component[i].RecipeAmount; FruitsFound++; }                            
                        }
                        else //Газ и лёд 
                        {
                            Me.CocktailRecipe.Component[i].StorageAmmount = Convert.ToInt32(matches[i == Me.CocktailRecipe.Component.Count() - 2 ? 0 : 1].Value);
                            Me.CocktailRecipe.Component[i].Use = true; //Лёд и Газ нужны всегда!
                        }               
                    }
                    if (FruitsFound < 4 || Me.CocktailRecipe.RecipeTotalFruitsAmount > Settings.TotalFruitsProRecipe)
                    {
                        UpdateStatus("@ " + DateTime.Now + " Не, коктейли варить не буду дороговато ...");
                        return false; //Не хватает компонентов для варения, необходимо: минимум 4 фрукта + газ + лёд
                    } 
                    #endregion
                    #region Проверка наличия добавок
                    string[] ID = new string[2];
                    for (int i = 0; i < 28; i++)
                    {
                        switch (i / 7)
                        {
                            case 0:
                                ID[1] = "icecream";
                                break;
                            case 1:
                                ID[1] = "piece";
                                break;
                            case 2:
                                ID[1] = "straw";
                                break;
                            case 3:
                                ID[1] = "umbrella";
                                break;
                        }
                        switch (i % 7)
                        {
                            case 0:
                                continue;
                            case 1:
                                ID[0] = "ratingaccur";
                                break;
                            case 2:
                                ID[0] = "ratinganticrit";
                                break;
                            case 3:
                                ID[0] = "ratingcrit";
                                break;
                            case 4:
                                ID[0] = "ratingdamage";
                                break;
                            case 5:
                                ID[0] = "ratingdodge";
                                break;
                            case 6:
                                ID[0] = "ratingresist";
                                break;
                        }
                        
                        Info = frmMain.GetJavaVar(MainWB, "$(\"#content .object-thumbs .action.action-get[data-rating=" + ID[0] + "][data-code=" + ID[1] + "]\").attr(\"data-amount\")");
                        Me.CocktailRecipe.SpecialComponent[i].Name = ID[1] + "_" + ID[0];
                        Me.CocktailRecipe.SpecialComponent[i].StorageAmmount = Info == null ? 0 : Convert.ToInt32(Info);                        
                    }
                    if (CT != CoctailType.None)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (Me.CocktailRecipe.SpecialComponent[Settings.CookCoctailSpecials[(int)CT * 8 + i] + i * 7].Name != null && Me.CocktailRecipe.SpecialComponent[Settings.CookCoctailSpecials[(int)CT * 8 + i] + i * 7].StorageAmmount < Settings.CookCoctailSpecials[(int)CT * 8 + i + 4])
                            {
                                UpdateStatus("@ " + DateTime.Now + " Чёрт, добавок маловато..., а шеф без трубочки не пьёт!");
                                return false;
                            }
                        } 
                    }           
                    #endregion                   
                    break;
                case CoctailAction.CheckMissing:
                    BugReport("CookCoctail ~CheckMissing");
                    if (CookCoctail(CoctailAction.CheckRecipe))
                    {
                        stcCoctail[] Coctails = new stcCoctail[6];
                        while (!Me.CocktailRecipe.Wrong)
                        {
                            int Index = -1;
                            for (int i = 0; i < 6; i++)
                            {                                
                                Coctails[i].CoctailName = ((CoctailType)i).ToString().ToLower();
                                Coctails[i].MissingAmount = (int)Settings.CookCoctailType[i] - GetArrClassCount(MainWB, "$(\"#content #inventory-shake_" + Coctails[i].CoctailName + "-btn\")");
                                if (!Coctails[i].Ignore && Coctails[i].MissingAmount > 0 && (Index == -1 || Coctails[i].MissingAmount >= Coctails[Index].MissingAmount)) Index = i;
                            }
                            if (Index != -1) Coctails[Index].Ignore = !CookCoctail(CoctailAction.Cook, (CoctailType)Index);
                            else break;
                        }                        
                    }
                    if (!Me.CocktailRecipe.Wrong) Me.CocktailRecipe.LastCook = ServerDT; //Перезаписывать время последнего варения, только успешных действий.
                    break;
                case CoctailAction.Cook:
                    BugReport("CookCoctail ~Cook");
                    #region Составление рецепта.
                    string[] Component = new string[10];
                    decimal[] ComponentAmount  = new decimal[10];
                    if (!CookCoctail(CoctailAction.CheckRecipe, CT)) return false;
                    foreach (stcCoctailComponent Item in Me.CocktailRecipe.Component)
                    {
                        if (Item.Use) 
                        {
                            Component[FruitsFound] = Item.Fruit;
                            ComponentAmount[FruitsFound] = Item.RecipeAmount;
                            FruitsFound++;
                            if (FruitsFound == 4) FruitsFound = 8; //Все фрукты найдены, Лёд и Газ должны быть в конце рецепта, резервируем место под добавки!
                        }
                    }
                    for (int i = 0; i < 4; i++) 
                    {
                        Component[4 + i] = Me.CocktailRecipe.SpecialComponent[Settings.CookCoctailSpecials[(int)CT * 8 + i] + i * 7].Name;
                        ComponentAmount[4 + i] = Settings.CookCoctailSpecials[(int)CT * 8 + i + 4];
                        
                    }
                    string PostData = "";
                    int index = 0;
                    #region Сбор формулы для отправки via POST
                     for (int i = 0; i < 10; i++)
                     {
                         if (Component[i] != null)
                         {
                             PostData += (PostData == "" ? "" : "&") + "components%5B" + index + "%5D%5Bcode%5D=" + Component[i] + "&components%5B" + index + "%5D%5Bamount%5D=" + ComponentAmount[i];
                             index++;
                         }
                     }
                     PostData += "&shaker=" + CT.ToString().ToLower();
                    #endregion             
                    #endregion
                    #region Завариваем коктейль.
                    if (!frmMain.GetDocumentURL(MainWB).Contains("/shakes/"))
                    {
                        GoToPlace(MainWB, Place.Nightclub, "/shakes");
                        Wait(20000, 35000, " Симулирую выбор ингредиентов до: ");
                    }
                    frmMain.NavigateURL(MainWB, Settings.ServerURL + "/nightclub/shakes/shake/", PostData, "Referer: http://" + Settings.ServerURL + "/nightclub/shakes/");
                    IsWBComplete(MainWB, 500, 1500);
                    #region Дополнительная проверка для вспомогательных доменов
                    if (!frmMain.GetDocumentURL(MainWB).Contains(Settings.ServerURL))
                    {
                        frmMain.NavigateURL(MainWB, Settings.ServerURL);
                        IsWBComplete(MainWB);
                    }
                    #endregion
                    #endregion
                    #region Проверка перфектности коктейля!
                    if (frmMain.GetJavaVar(MainWB, "$(\"#alert-text .pont.pont5\").attr(\"class\")") == null)
                    {
                        Me.CocktailRecipe.Wrong = true;
                        Me.CocktailRecipe.LastCook = ServerDT.Date.AddMinutes(1500); //25[часов] * 60[минут] ибо обновление не ранее новых суток + 30 минут, до этого времени блокировать варение по неверному рецепту!
                        UpdateStatus("@ " + DateTime.Now + " Бармену бы руки оторвать не помешало, что за бурду он мне посоветовал?!" + (Settings.SellBadCoctail ? " Погнал продам!" : ""));
                        #region Продажа коктейля, если нужно!
                        Info = frmMain.GetJavaVar(MainWB, "$(\".alert.infoalert.coctail .actions\").html()");
                        if (Settings.SellBadCoctail && Info != DBNull.Value)
                        {
                           
                            string CoctailID = Regex.Match((string)Info, "(?<=/use/)([0-9])+(?=/shake/)").Value;
                            frmMain.NavigateURL(MainWB, Settings.ServerURL + "/shop/section/mine/");
                            IsWBComplete(MainWB);
                            Wait(3000, 6000); //Создаём иллюзию поиска ненужных цепочек в багаже перед удалением
                            object[] Args = new object[3] { CoctailID, "/shop/section/mine/", 1 };
                            frmMain.InvokeScript(MainWB, "shopSellItem", Args);  //shopSellItem('118468851', '/shop/section/mine/', 1);
                            IsWBComplete(MainWB);
                            GoToPlace(MainWB, Place.Nightclub, "/shakes");
                        }
                        #endregion
                    }
                    else UpdateStatus("# " + DateTime.Now + " Пиши Босс... : +1 коктейль!");
                    #endregion
                    break;            
            }        
            return true;
        }
        public void WearSet(WebBrowser WB, clsWearSets.stcSet[] ArrSet, int IndexName)
        {
            BugReport("WearSet");

            HtmlElement HtmlEl;
            bool[] Status = new bool[2] { false, false }; // [0] -> Я переодевался?,  [1] -> Все вещи были найдены?             

            if (ArrSet == null || (Thread.CurrentThread.Name == "MainBotThread" && Me.SetInfo.LastSetIndex == IndexName && Me.SetInfo.LastDT.AddSeconds(120) > DateTime.Now)) return; //нет сетов, просто покидаем функцию!
            foreach (clsWearSets.stcSet Set in ArrSet)
            {
                if (Set.IndexName == IndexName)
                {                    
                    IsWBComplete(WB);
                    if (!Regex.IsMatch(frmMain.GetDocumentURL(WB), "/player/$")) GoToPlace(WB, Place.Player);
                    Status[1] = true; //Как минимум нужный сет найден.
                    
                    for (int i = 0; i < Set.Item.Count<clsWearSets.stcSetItem>(); i++)
                    {
                        object Info;
                        DateTime MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                        do
                        {
                            if (WB.InvokeRequired) Thread.Sleep(50); else Application.DoEvents();
                            Info = frmMain.GetJavaVar(WB, "$(\"#main .slots .slot" + (i + 1) + "\").html()");
                        }
                        while (Info == DBNull.Value && MonitorDT < DateTime.Now);
                        if (MonitorDT < DateTime.Now) Status[1] = false;  //Что-то пошло не так?

                        string SlotItemHtml = Info == DBNull.Value ? null : (string)Info;
                        string SlotItemID = SlotItemHtml == null ? "" : Regex.Match(SlotItemHtml, "(?<=data-id=\")([0-9])+(?=\")").Value;
                        if (Set.Item[i].ID != SlotItemID) //На мне не та шмотка
                        {
                            #region Инициализация
                            HtmlEl = null;
                            if (!Status[0]) UpdateStatus("@ " + DateTime.Now + " А не заглянуть ли мне в шкаф ... ?!?"); //Только первый раз
                            Status[0] = true; //Как минимум одна вещь должна быть переодета.                            
                            #endregion 
                            #region Поиск вещи которую будем одевать/снимать
                            HtmlElement[] ArrHTmlEl = frmMain.GetElementsById(WB, Set.Item[i].ID != "" ? Set.Item[i].Btn : (string)frmMain.GetJavaVar(WB, "m.items['" + SlotItemID + "'].btn['0'].id")); //Set.Item[i].ID == "" Я должен быть голый!
                            if (ArrHTmlEl != null) //Нужная вешь вообще есть в багажнике?
                            {
                                foreach (HtmlElement H in ArrHTmlEl) //Одинаковых вещей может быть много, нужна именно сохранённая!
                                {
                                    if (H.GetAttribute("data-id") == (Set.Item[i].ID != "" ? Set.Item[i].ID : SlotItemID))
                                    {
                                        HtmlEl = H;
                                        break;
                                    }
                                }
                            }                            
                            #endregion
                            #region Переодевание
                            if (HtmlEl != null)
                            {
                                frmMain.InvokeMember(WB, HtmlEl, "click"); //Одеваем - снимаем
                                MonitorDT = DateTime.Now.AddSeconds((double)Settings.GagIE);
                                do
                                {
                                    if (WB.InvokeRequired) Thread.Sleep(50); else Application.DoEvents();
                                    Info = frmMain.GetJavaVar(WB, "$(\"#main .slots .slot" + (i + 1) + "\").html()");
                                    SlotItemHtml = Info == DBNull.Value ? "DBNull" : (string)Info + ""; //Для проходждения Match в случае null
                                }
                                while (((Set.Item[i].ID == null && SlotItemHtml != "") || (Set.Item[i].ID != null && Set.Item[i].ID != Regex.Match(SlotItemHtml, "(?<=data-id=\")([0-9])+(?=\")").Value)) && MonitorDT > DateTime.Now);
                                WB.Tag = "Ready"; //место с аякс пройденно успешно!
                                if (MonitorDT < DateTime.Now) Status[1] = false;  //Что-то пошло не так?
                                Wait(500, 1500);
                            }
                            else Status[1] = false; //Нужная, шмотка не найдена
                            #endregion
                        }                  
                    }
                    break; //Нужный сет уже был одет
                }
            }
            #region Сохранение информации о последнем успешо одето сэте!
            if (Status[1] && Thread.CurrentThread.Name == "MainBotThread")
            {
                Me.SetInfo.LastSetIndex = IndexName;
                Me.SetInfo.LastDT = DateTime.Now;
            }
            #endregion
            if (Status[0]) UpdateStatus((Status[1] ? "# " : "! ") + DateTime.Now + (Status[1] ? " Теперь я неотразим ..., меня даже в зеркале не видно!" : " Ооо Боже! Меня никак обокрали! Где мой костюм супермена?!?"));   
        }        
        public bool Bunker()
        {

            Match match = null;

            IsWBComplete(MainWB);

            foreach (HtmlElement HtmlEl in frmMain.GetDocument(MainWB).GetElementById("left-players").GetElementsByTagName("li"))
            {
                match = Regex.Match(HtmlEl.OuterHtml, "(?<Me>class=\"me alive\").*id=\"?(?<Life>fighter([0-9])+-life).*id=\"?(?<Pet>pet-([0-9])+)");
                if (match.Groups["Me"].Success) break; //Пока это условие стоит как обязательное единственный матч будет с моими данными для игроков убрать группу Ме
            }

            //Нумерация комнат начинается в ИД с 0-8, при считывании же номера комнаты в которой нахожусь используется нумерация с 1-9
            MessageBox.Show("Я в комнате N: " + Regex.Match(frmMain.GetDocument(MainWB).GetElementById("bunker-room-number").InnerText, "([0-9])+").Value
                            + "\r\nБой в бункере закончится через: " + frmMain.GetDocument(MainWB).GetElementById("game-timer").InnerText                            
                            + "\r\nВ комнате N: 1 -> " + frmMain.GetDocument(MainWB).GetElementById("room-0-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-0-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 2 -> " + frmMain.GetDocument(MainWB).GetElementById("room-1-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-1-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 3 -> " + frmMain.GetDocument(MainWB).GetElementById("room-2-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-2-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 4 -> " + frmMain.GetDocument(MainWB).GetElementById("room-3-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-3-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 5 -> " + frmMain.GetDocument(MainWB).GetElementById("room-4-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-4-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 6 -> " + frmMain.GetDocument(MainWB).GetElementById("room-5-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-5-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 7 -> " + frmMain.GetDocument(MainWB).GetElementById("room-6-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-6-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 8 -> " + frmMain.GetDocument(MainWB).GetElementById("room-7-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-7-enemies").InnerText + " Врагов"
                            + "\r\nВ комнате N: 9 -> " + frmMain.GetDocument(MainWB).GetElementById("room-8-allies").InnerText + " Союзников, " + frmMain.GetDocument(MainWB).GetElementById("room-8-enemies").InnerText + " Врагов"
                            + "\r\nМожно сходить в вверх? " + (frmMain.GetDocument(MainWB).GetElementById("arrow-up").Style == null) + " Можно сходить в низ? " + (frmMain.GetDocument(MainWB).GetElementById("arrow-down").Style == null)
                            + "\r\nМожно сходить в лево? " + (frmMain.GetDocument(MainWB).GetElementById("arrow-left").Style == null) + " Можно сходить в право? " + (frmMain.GetDocument(MainWB).GetElementById("arrow-right").Style == null)
                            + "\r\nМожно ударить через: " + frmMain.GetDocument(MainWB).GetElementById("punch-timer").InnerText + "s, Переход возможен через: " + frmMain.GetDocument(MainWB).GetElementById("move-timer").InnerText + "s"
                            + "\r\n" + frmMain.GetDocument(MainWB).GetElementById("damage-meter").InnerText
                            + "\r\nМои жизни: " + frmMain.GetDocument(MainWB).GetElementById(match.Groups["Life"].Value).InnerText + ", Питомец: " + frmMain.GetDocument(MainWB).GetElementById(match.Groups["Pet"].Value).GetAttribute("title")                                 
                            + "\r\nВ комнате есть сейф? " + (frmMain.GetDocument(MainWB).GetElementById("safe").Style == null) + ", В комнате есть сундук? " + (frmMain.GetDocument(MainWB).GetElementById("chest").Style == null)
                            + "\r\n\r\n\r\n Сундук в комнате: " + frmMain.GetJavaVar(MainWB, "bunkerChestRoom") + ", Сейф в комнате: " + frmMain.GetJavaVar(MainWB, "bunkerSafeRoom")
                          );
            return true;
        }

        public void SaveExpertSettings()
        {
            BugReport("SaveExpertSettings");

            Stream FS = new FileStream("Expert.xml", FileMode.Create);
            XmlExpertSerializer.Serialize(FS, Expert);
            FS.Close();            
        }
        public void LoadExpertSettings()
        {
            BugReport("LoadExpertSettings");

            Stream FS = new FileStream("Expert.xml", FileMode.Open);
            Expert = (stcExpert)XmlExpertSerializer.Deserialize(FS);
            FS.Close();

            if (Expert.QuestFruitNr == 0) Expert.QuestFruitNr = 63;
            if (Expert.QuestMoneyNr == 0) Expert.QuestMoneyNr = 192;
            if (Expert.RevengerPrc == 0) Expert.RevengerPrc = 150;
            if (Expert.MaxBuyFightItemAmount == 0) Expert.MaxBuyFightItemAmount = 9;
            if (Expert.MaxWebSockets == 0) Expert.MaxWebSockets = 6;
        } 
        
        public void SaveSettings()
        {
            BugReport("SaveSettings");

            Stream FS = new FileStream("BotSettings.xml", FileMode.Create);
            XmlSettingsSerializer.Serialize(FS, Settings);
            FS.Close();
            UpdateStatus("- " + DateTime.Now + " Настройки бота удачно сохранены.");
        }                    
        public void LoadSettings()
        {
            BugReport("LoadSettings");            

            Stream FS = new FileStream("BotSettings.xml", FileMode.Open);
            Settings = (stcSettings)XmlSettingsSerializer.Deserialize(FS);
            FS.Close();

            #region Инициализация времён
            if (Settings.StartDuelsDT.Equals(new DateTime())) Settings.StartDuelsDT = DateTime.Now.Date;
            if (Settings.StopDuelsDT.Equals(new DateTime())) Settings.StopDuelsDT = DateTime.Now.Date.Add(new TimeSpan(23,59,59));
            if (Settings.StartHC.Equals(new DateTime())) Settings.StartHC = DateTime.Now.Date;
            if (Settings.StopHC.Equals(new DateTime())) Settings.StopHC = DateTime.Now.Date.Add(new TimeSpan(23, 59, 59));
            #endregion
            UpdateStatus("- " + DateTime.Now + " Настройки бота удачно загружены.");
        }        

        public void RestartBotInstance(bool ForceRestart = false)
        {                                                                                 
            System.Diagnostics.Process MeProc = System.Diagnostics.Process.GetCurrentProcess();          
            if (!ForceRestart && (!Settings.UseRestartMemory || MeProc.PrivateMemorySize64 / 1048576 < Settings.maxRestartMemory)) return; //(1048576 = 1024 * 1024) Byte -> MegaByte
            UpdateStatus("! " + DateTime.Now + " Рестарт " + (ForceRestart ? "[Внутренняя ошибка]" : "[Использование оперативной памяти]"));
            Bug.Logging = false; //Останавливаем логгирование.
            clsAppRestartManager.SaveRecovery(ref Me); //Сохраняем текущие данные для дальнейшего использования
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = Application.ExecutablePath, Arguments = "-recovery" +  (MeInTray ? " -tray" : ""), WindowStyle = Settings.RestartHidden ? System.Diagnostics.ProcessWindowStyle.Hidden : System.Diagnostics.ProcessWindowStyle.Minimized }); //Все остальные параметы бот подхватит при удачной загрузке настроек
            MeProc.Kill();            
        }
        public void BugReport(string AddInfo = "", bool Save = false, string FileName = "", WebBrowser WB = null)
        {
            if (Save)
            {
                Bug.Logging = false; //Останавливаем логгирование.
                try
                {
                    StreamWriter SW = new StreamWriter("BuG-Report\\" + (FileName == "" ? "Bug" + Bug.Nr : FileName) + ".Htm");
                    IsWBComplete(WB);
                    SW.WriteLine("Info: " + Bug.Info);
                    SW.WriteLine("IE ver: " + WB.Version);
                    SW.WriteLine("URL: " + frmMain.GetDocumentURL(WB));
                    SW.WriteLine(frmMain.GetDocumentHtmlText(WB).Replace("href=\"", "href=\"http://" + Settings.ServerURL).Replace("src=\"", "src=\"http://" + Settings.ServerURL));
                    SW.Close();

                    SW = new StreamWriter("BuG-Report\\" + (FileName == "" ? "BugEx" + Bug.Nr++ : FileName + "Ex") + ".Htm");
                    SW.Write(frmMain.GetDocumentHtmlTextEx(WB));
                    SW.Close();                        

                    if (Bug.Nr >= 10) { Bug.Nr = 0; UpdateStatus("! " + DateTime.Now + " Всё, кина не будет, ибо сели баратейки!"); }
                }
                finally
                {
                    Bug.Logging = true; //Возобновляем логгирование.
                }
            }
            else
            {
                if (Bug.Logging)
                {
                    string[] sTmp;                    
                    if (Bug.Info == null) Bug.Info = new string('|', 19) + AddInfo; //Инизиализиреум массив на 20 элементов (19 + 1)
                    else
                    {
                        sTmp = Bug.Info.Split('|'); //Разбиваем, по эллементам
                        #region FIFO, где "AddInfo" новый эллемент!
                        if (sTmp[sTmp.Count<string>() - 1] != AddInfo)
                        {
                            #region Debug Mode
                            if (DebugMode) UpdateStatus(AddInfo);
                            #endregion
                            Bug.Info = null;
                            for (int i = 1; i < sTmp.Count<string>(); i++) Bug.Info += sTmp[i] + "|";
                            Bug.Info += AddInfo;
                        }
                        #endregion
                    }
                }                    
            }
        }
        public void Shutdown()
        {
            Me.Events.ShutdownRelease = false; //Всё, блокируем повторный запуск выключения.
            GetMyStats(MainWB);
            IsTimeout(MainWB, true, false);
            UpdateMyInfo(MainWB);
            #region Thimbles
            if (Me.Wallet.Money >= Settings.SDThimblesMoney & Me.Player.Level >= 5) Metro(MetroAction.Game); //Пора играть с Моней?
            #endregion
            #region MC
            if (Me.Player.Level >= 2) MC(MCAction.Work, Settings.SDWorkTime);
            #endregion
            #region ShutDown
            clsExitWindows.ShutDown();
            #endregion
        }
        public void StartBot() //OK
        {
            BugReport("StartBot");

            try
            {
                #region ChatThread
                ChatThread = new Thread(new ThreadStart(ReadChat));
                ChatThread.Name = "ChatReader";
                #endregion
                #region StartBotThread
                UpdateStatus("© " + DateTime.Now + " ОK, босс! - Беру скакалку, скакаю=)");
                WebLogin(MainWB); //                               
                Police(PoliceAction.Check);                                
                GetMyStats(MainWB);
                #region Чтение чата
                ChatThread.Start();
                #endregion
                #region Отключение режима быстрой загрузки страничек!
                if ((frmMain.GetJavaVar(MainWB, "AngryAjax.turned") ?? "0").Equals("1") && (MainWB.Version.Major < 11 || Settings.MaxIEVersion < 11)) QuickPageLoading(false);
                #endregion
                if (Me.BankDeposit.SafeTillDT == new DateTime() && Me.Player.Level >= 5) Bank(BankAction.Deposit);
                if (Me.ArrDuelsDT == null) CheckImmun(ImmunAction.Duels);
                if (Me.Thimbles.StartDT == new DateTime()) CheckImmun(ImmunAction.Mona);
                if (!Me.Major.Stop) Major(MajorAction.Check);
                if (Me.ClanWarInfo.NextDT <= GetServerTime(MainWB)) ClanWar(ClanWarAction.Check);                
                if (!Me.PigProtection.Stop) PigProtection(PigProtectionAction.Check);
                if (!Me.Safe.Stop) Safe(SafeAction.Check);
                if (Me.CarRide.Cars == null && Me.Player.Level >= 7) Automobile(AutomobileAction.Check);                
                do
                {
                    while (IsTimeout(MainWB, true, false) || Me.Police.Stop || TimeToStopAtack(NextTimeout.Atack) || (Ignore.PVPAttack && (Me.OilHunting.Stop || !Settings.GoOil) && (Me.NPCHunting.Stop || !Settings.AttackNPC))) //Таймаут между драками?
                    {                       
                        DateTime ServerDT = GetServerTime(MainWB);
                        #region Актуализация массива проведённых драк
                        while (Me.ArrDuelsDT != null && Me.ArrDuelsDT.Count<DateTime>() > 0 && ServerDT.AddDays(-1) > Me.ArrDuelsDT[Me.ArrDuelsDT.Count<DateTime>() - 1])
                        {
                            Array.Resize<DateTime>(ref Me.ArrDuelsDT, Me.ArrDuelsDT.Count<DateTime>() - 1);
                        }                      
                        #endregion
                        #region Блокировка нападений в стиле PVP, при охоте на агентов мажорами до 9 утра, слишком большом количестве драк, в запрещенное время!
                        if ((Settings.MrPlushkin && !Me.AgentHunting.Stop && Me.Major.LastDT > ServerDT && ServerDT.Hour < 8)
                             || (Settings.UseMaxDuels && Me.ArrDuelsDT != null && Settings.maxDuels <= Me.ArrDuelsDT.Count<DateTime>())
                             || (Settings.UseDuelTimes && !IsTimeInTimespan(Settings.StartDuelsDT.TimeOfDay, Settings.StopDuelsDT.TimeOfDay, ServerDT.TimeOfDay))                             
                           )
                        {
                            if (!Ignore.PVPAttack) UpdateStatus("@ " + DateTime.Now + 
                                (Settings.MrPlushkin && !Me.AgentHunting.Stop && Me.Major.LastDT > ServerDT && ServerDT.Hour < 8 ?
                                " Агенты попрятались, но Mr. Плюшкина не провести, я подожду!" :
                                " Ох и надрался же я сегодня, пора пить чай!")
                                );                            
                            Ignore.PVPAttack = true;
                        }
                        else Ignore.PVPAttack = false;
                        UpdateMessageInfo(" Согласно Вашим настройкам дуеэли приостановлены!", Ignore.PVPAttack);
                        #endregion
                        
                        if (Me.Patrol.LastDT.Date != ServerDT.Date) Me.Patrol.Stop = false;  //Обнуление!
                        if (Me.RatHunting.RestartDT < DateTime.Now.AddMinutes(Me.Major.LastDT > ServerDT ? 5 : 15)) { Me.RatHunting.Defeats = 0; Me.RatHunting.Stop = false; } //Охота обновляется каждые 24 часа, на сей раз учитываем конец прошлого обвала
                        if (Me.OilLeninHunting.RestartDT < DateTime.Now) { Me.OilLeninHunting.Defeats = 0; Me.OilLeninHunting.Stop = false; } //Охота обновляется каждые 24 часа
                        //При необходимости проводим драку в метро, и пробуем пойти в стенку или мирно покидаем таймаут ...
                        if (!Me.Trauma.Stop)
                        {                           
                            #region Беспрерывная охота на крысомах или обычный таймаут? (Перехват таймаута, допускаются только мелкие действия) + Допинг + Охота на крыс
                            TimeSpan TSTimeout = new TimeSpan();
                            HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("timeout");
                            //Таймаут на самом деле существует?
                            if (HtmlEl.Style != "display: none;") TimeSpan.TryParse(frmMain.GetDocument(MainWB).GetElementById("timeout").InnerText, out TSTimeout); //Запоминаем сколько ещё продлится таймаут
                            if (TimeToStopAtack(Settings.GoPatrol && !Me.Patrol.Stop ? NextTimeout.Patrol : (Settings.GoMetro && !Me.Rat.Stop ? NextTimeout.Metro : NextTimeout.Atack), StopTimeoutType.RatHunting))
                            {
                                if (TSTimeout < Me.RatHunting.NextDT - DateTime.Now) //Таймаут меньше интервала до новой крысы?
                                {
                                    if (Settings.HealTrauma && Me.Major.LastDT > ServerDT.AddMinutes(5)) //Мажор + лечение травм за мёд?
                                    {
                                        TSTimeout = TimeToStopAtack(NextTimeout.Atack, StopTimeoutType.GrpFight) ? new TimeSpan(0, 4, 0) : TSTimeout; //Обычно максимум за 3 минуты мы ждём бой посему накидываем минутку и разрешаем пользоваться таймаутом
                                        if (Me.RatHunting.NextDT - DateTime.Now.Add(TSTimeout) < new TimeSpan(0, 5, 0)) TSTimeout = Me.RatHunting.NextDT - DateTime.Now; //Если в итоге всёравно не будет времени на атаку, ждём сразу до упора!                             
                                    }
                                    else TSTimeout = Me.RatHunting.NextDT - DateTime.Now;
                                }
                                TSTimeout = TSTimeout.Subtract(new TimeSpan(0, 0, 20 + (Settings.UseWearSet ? 25 : 0))); //Ждать либо (до конца текущего таймаута либо до крысиного) + 20 секунд резервируем под поедание крысиных допингов и 25 секунд переодевание
                                Wait(TSTimeout, " Папка настойчиво просил сосредоточиться на крысюльках, жду до: ", TimeOutAction.NoTask);
                                #region Охота на крыс
                                Metro(MetroAction.SearchRat);
                                #endregion
                            }
                            else
                            {
                                #region Допинг
                                Dopings(ref Me.ArrUsualDoping, DopingAction.Check);
                                #endregion
                                #region ClanWar + Police + Thimbles + Major + Fitness + Automobile + Pyramid + Casino + Pet + Sovet + Factory + Quest + Patrol + Metro + Chaos
                                UseTimeOut(TimeOutAction.All);
                                #endregion
                            }
                            #endregion
                        }                        
                        Thread.Sleep((Int32)Expert.UseTimeout); //Замедляем функцию использования таймаута                         
                    }
                    if (!Me.Trauma.Stop) Me.Trauma.Stop = Trauma(TraumaAction.Check); //Не забегать сюда каждый раз когда уже поймал травму!                    
                    if (!Me.Trauma.Stop)
                    {
                        #region Допинг
                        Dopings(ref Me.ArrUsualDoping, DopingAction.Check);
                        #endregion

                        #region Охота на крыс
                        if (Me.RatHunting.RestartDT < DateTime.Now.AddMinutes(Me.Major.LastDT > GetServerTime(MainWB) ? 5 : 15)) { Me.RatHunting.Defeats = 0; Me.RatHunting.Stop = false; } //Охота обновляется каждые 24 часа, на сей раз учитываем конец прошлого обвала
                        if (Settings.SearchRat && !Me.RatHunting.Stop && Me.RatHunting.NextDT <= DateTime.Now.AddMinutes(5)) Metro(MetroAction.SearchRat);
                        #endregion

                        #region Ленинопровод
                        if (Me.OilLeninHunting.RestartDT < DateTime.Now) { Me.OilLeninHunting.Defeats = 0; Me.OilLeninHunting.Stop = false; } //Охота обновляется каждые 24 часа
                        if (Settings.GoOilLenin && !IsTimeout(MainWB, false, false) && !Me.OilLeninHunting.Stop && Me.OilLeninHunting.NextDT < DateTime.Now) Oil(OilAction.LeninFight);
                        #endregion

                        #region Проверка не настал ли следуюший день по серверному времени, ибо многое востанавливается в 00:00 по серверу!
                        DateTime ServerDT = GetServerTime(MainWB);
                        if (Me.AgentHunting.LastDT.Date != ServerDT.Date) { Me.AgentHunting.Val = 0; Me.AgentHunting.Stop = false; } //Обнуление поражений от Агентов!
                        if (Me.NPCHunting.LastDT.Date != ServerDT.Date) { Me.NPCHunting.Val = 0; Me.NPCHunting.Stop = false; } //Обнуление поражений от NPC!
                        if (Me.OilHunting.LastDT.Date != ServerDT.Date) { Me.OilHunting.Val = 0; Me.OilHunting.Stop = false; } //Обнуление поражений от Нефтянников!
                        if (Me.HCHunting.LastDT.Date != ServerDT.Date) Me.HCHunting.Stop = false;
                        #endregion

                        #region Блокировка нападений в стиле PVP, при охоте на агентов мажорами до 8 утра, слишком большом количестве драк, в запрещенное время!
                        if ((Settings.MrPlushkin && !Me.AgentHunting.Stop && Me.Major.LastDT > ServerDT && ServerDT.Hour < 8)
                             || (Settings.UseMaxDuels && Me.ArrDuelsDT != null && Settings.maxDuels <= Me.ArrDuelsDT.Count<DateTime>())
                             || (Settings.UseDuelTimes && !IsTimeInTimespan(Settings.StartDuelsDT.TimeOfDay, Settings.StopDuelsDT.TimeOfDay, ServerDT.TimeOfDay))
                           )
                        {
                            if (!Ignore.PVPAttack) UpdateStatus("@ " + DateTime.Now +
                                (Settings.MrPlushkin && !Me.AgentHunting.Stop && Me.Major.LastDT > ServerDT && ServerDT.Hour < 8 ?
                                " Агенты попрятались, но Mr. Плюшкина не провести, я подожду!" :
                                " Ох и надрался же я сегодня, пора пить чай!")
                                );
                            Ignore.PVPAttack = true;
                        }
                        else Ignore.PVPAttack = false;
                        #endregion
                       
                        if (Settings.OpenPrizeBox) CheckForPrizeBox(); //Проверка на наличие ключей и сундуков.
                        if (CheckHealthEx(99, Settings.HealMe100, Settings.HealPet50, Settings.HealPet100) ? (!Me.Trauma.Stop && !IsHPLow(MainWB, 99, false)) : false) //Пить сироп, если жизней менее 99% или микстуру по расписанию!
                        {
                            if (Settings.UseWearSet) WearSet(MainWB, ArrWearSet, 0); //Одеваем стандартный сет, на всякий случай!

                            #region Нефтекачка
                            //Проверка не настал ли следуюший день по серверному времени, ибо Нефтекачка востанавливается в 00:00 по серверу!                            
                            if (Settings.GoOil && !Me.OilHunting.Stop && (!Settings.UseWerewolf || Me.WerewolfHunting.Stop)
                                && (Ignore.PVPAttack || Settings.OilIgnoreTimeout || (Me.ClanWarInfo.WarStep == 1 && Settings.Berserker ? CheckImmun(ImmunAction.Tooth) : true))                                 
                               ) 
                            {
                                Oil(OilAction.Fight); //Дерёмся только при полном Хп.
                            }
                            #endregion
                            else
                            #region Охотничий клуб или аллея
                            {
                                if (!Me.Police.Stop)
                                {
                                    #region ОК
                                    if (Settings.GoHC && !Me.HCHunting.Stop && !Ignore.PVPAttack && IsTimeInTimespan(Settings.StartHC.TimeOfDay, Settings.StopHC.TimeOfDay, ServerDT.TimeOfDay)
                                        && (Me.ClanWarInfo.WarStep == 1 && Settings.Berserker ? CheckImmun(ImmunAction.Tooth) : true) //ClanWar
                                        && (!Settings.UseAgent || Me.AgentHunting.Stop || ServerDT.Hour < 8) //Agent разрешаем охоту ночью если не Mr. Плюшкин
                                        && (!Settings.UseWerewolf || Me.WerewolfHunting.Stop) //Werewolf
                                        && (!Settings.AttackNPC || Me.NPCHunting.Stop || (Settings.Lampofob && !Me.PerkAntiLamp.On))) //NPC
                                    {
                                        Torture(Settings.HCUseTorture);
                                        HunterClub();
                                    }
                                    #endregion
                                    else
                                    #region Аллея
                                    {
                                        if (Me.ClanWarInfo.WarStep == 1 & Settings.RemoveEnemy & !CheckImmun(ImmunAction.Tooth)) ClanWar(ClanWarAction.Tooth); //Стадия выбивания зубов, и стоит галочка?
                                        #region Использовать орудия пыток только если будем бить жертв!
                                        if (Settings.AlleyOpponent == Opponent.Victim && (Settings.UseAgent ? Me.AgentHunting.Stop : true) &&
                                            !(Me.ClanWarInfo.Now && Settings.AddClan && (Settings.FarmClan || (Me.ClanWarInfo.WarStep == 1 ? !CheckImmun(ImmunAction.Tooth) : false)))
                                            ) Torture(true);
                                        else Torture(false);
                                        #endregion
                                        Attack(Settings.AlleyOpponent, Settings.minAlleyLvl, Settings.maxAlleyLvl);
                                        Me.Player.Level = Convert.ToInt32((string)frmMain.GetJavaVar(MainWB, "player['level']")); //Возобновляем мой уровень, вдруг я был оборотнем!
                                    }
                                    #endregion
                                }
                            }
                            #endregion
                        }
                    }
                    else
                    {
                        #region MC при травме
                        UpdateStatus("# " + DateTime.Now + " Проооопустите ка инвалида! Я у вас тут в Макдаке поторчу?!?!");
                        DateTime ServerDT;
                        do
                        {
                            if (!Trauma(TraumaAction.Check)) break;
                            Me.MC.Stop = false; //Макдачу пока травма, блокируем использование Иммунитета у мони.
                            UseTimeOut(TimeOutAction.Free);                            
                            ServerDT = GetServerTime(MainWB);
                            if (Me.Trauma.LastDT > ServerDT.AddMinutes(20)) MC(clsBot.MCAction.Work, Settings.MCWorkTime); //Если уже мало времени до окончания, травмы не устраиваться в макдональдс.
                            else Wait(Me.Trauma.LastDT - ServerDT, " Болячка сама во-вот отвалится, жду до: ", TimeOutAction.Free);
                            ServerDT = GetServerTime(MainWB);
                        } while (ServerDT < Me.Trauma.LastDT);
                        Me.Trauma.Stop = false; //Была травма, но время истекло!
                        Me.MC.Stop = true; //Работы в Шаурбургесе закончены, разрешаем использование Иммунитета у мони.
                        Me.Events.SessionStartDT = DateTime.Now; //Закончились работы в Шаурбургесе, начинается новый отсчет драк.                    
                        #endregion
                    }
                } while (true);
                #endregion
            }
            catch (Exception e)
            {
                if (HCThread[0].IsAlive) HCThread[0].Abort(); 
                if (HCThread[1].IsAlive) HCThread[1].Abort();
                if (ChatThread.IsAlive) ChatThread.Abort();
                if (Thread.CurrentThread.ThreadState == ThreadState.AbortRequested) return; //Всё в порядке, просто останавливаем бота!
                #region Запись Trace
                StreamWriter SW = new StreamWriter("BuG-Report\\BugTrace" + Bug.Nr + ".txt"); //Application.StartupPath
                SW.WriteLine(e.Message);
                SW.WriteLine(e.Source);
                SW.WriteLine(e.StackTrace);
                SW.Close();
                #endregion
                BugReport(null, true, "", MainWB);
            }             
        }
        #endregion
         
        public void Test()
        {
            //http://www.moswar.ru/phone/call/#fight-phone
            //HealMePlus();
            //Bank(BankAction.Exchange);
            //GoToPlace(MainWB, Place.Oil);
            //HealMePlus();
           // Dopings(ref Me.ArrUsualDoping, DopingAction.Check);
            //MainWB.Navigate("F:\\moswarBro\\Moswar\\bin\\Debug\\Timeout.htm");
            //IsWBComplete(MainWB);

            //
            frmMain.InvokeScript(MainWB, "eval", new object[] { "Alley.Suslik.getReward()" });

//            string sH = frmMain.GetDocument(MainWB).GetElementById("neftlenin_alert_g").GetElementsByTagName("button")[0].Style;

            //REGEX вырезающий середину
            //(IsMultiFrame(WB) ? WB.Document.Window.Frames[FrameIndex].Document : WB.Document).InvokeScript("eval", new object[] { "AngryAjax.goToUrl('" + Regex.Replace(URL, "(http://)?" + Bot.Settings.ServerURL + "(?<URL>(?(?=/$)|.)+)/?", "${URL}") + "');"});
           

//            GoToPlace(MainWB, Place.Player);
 //           CheckBagFightItems((new int[] {1, 2, 4, 5, 6, 7}).ToList());
            //CheckBagFightItems(GroupFightType.Chaos);
            //TonusMePlus();
            //int i = GetArrClassCount(MainWB, "$(\"#content #slots-groupfight-place .object-thumb\").not(.icon-locked-small)");  //

            //BuyItems(MainWB, ShopItems.Tea4);

           //string[] ArrItemHTML = GetArrClassHtml(MainWB, "$(\"#dopings-accordion #" + "inventory-chocolate_m1-btn" + "\").parent()", "innerHTML");
           //Match match = Regex.Match(ArrItemHTML[0], "data-id=\"?(?<DataId>([0-9])+)([\\s\\S])+ id=(\")?(?<Id>([\\w_-])+)");
            //string[] ArrHTML = GetArrClassHtml(MainWB, "$(\"#content .slots .slot-pet.notempty\")", "innerHTML");
            //object s = frmMain.GetJavaVar(MainWB, "$(\"#content .slots .slot-pet.notempty\").html()");
            //HtmlElementCollection HC = frmMain.GetDocument(MainWB).GetElementsByTagName("tbody")[0].GetElementsByTagName("ul")[1].GetElementsByTagName("li"); //Информация о шмотках на мне + питомец
            //Match match = Regex.Match(HC[HC.Count - 2].InnerHtml, "/pets/([0-9-])+[.]png"); //Именно этот питомец сейчас со мной (Картинка с боевым питомцем в предпоследнем элементе, последний беговой при условии показывать)
            //if (!match.Success) match = Regex.Match(HC[HC.Count - 1].InnerHtml, "/pets/([0-9-])+[.]png"); //Именно этот питомец сейчас со мной (Картинка с питомцем в последнем элементе коллекции)
            //CheckBagFightItems(GroupFightType.Rat);
         //   Automobile(AutomobileAction.Check);
            //ChatThread = new Thread(new ThreadStart(ReadChat));
            //ChatThread.Start();

            //BuyItems(MainWB, ShopItems.Granade, new string[] { "!", "Кластерная граната", "Граната «Разрывная»" });
            //CheckBagFightItems((new int[] { 1, 2, 4, 4, 4, 4 }).ToList());
            
           // m.Groups["ID"].Value;
           // frmMain.GetJavaVar(MainWB, "$.ajax({url: \"/player/json/item-special/switch-weapon-group/" + BagFightItems[0].EquipmentItemId + "/\", type: \"post\", data: {\"unlocked\": 1, \"inventory\": " + BagFightItems[0].EquipmentItemId + ", \"previousItemId\": " + SlotFightItems[6].SlotItemId.Replace("_", "") + "}, dataType: \"json\"});");
    
                        
            
            
            //playerFightItemSwitch(itemId, unlocked, previousItemId)


            //bool b= Dopings(ref ArrDoping, DopingAction.Check);
            //GetMyStats(MainWB);

            //frmMain.NavigateURL(MainWB, "e:\\Mashinki-Bug.htm");
            //IsWBComplete(MainWB);
            //GetMyStats(MainWB);
            //GoToPlace(MainWB, Place.Clan, "/warstats");
            //if (frmMain.GetDocument(MainWB).GetElementById("content").GetElementsByTagName("table").Count == 1) return false;
            //$("#nexElem-id").trigger("");
            //Petarena(PetAction.TrainWarPet);           
            //MainWB.Navigate("e:\\Test.htm");
            //frmMain.NavigateURL(MainWB, "e:\\Test.htm");
            //string sa = new StreamReader(MainWB.DocumentStream).ReadToEnd(
            //IsWBComplete(MainWB);
            
            //MainWB.Document.GetElementById("Test").InvokeMember("mousemove");
            //string s = (string)frmMain.GetJavaVar(MainWB, "$(\"#content .object-thumb .padding\").find(\"img\").attr(\"title\");");
            //frmMain.GetJavaVar(MainWB, "var evt = Test.ownerDocument.createEvent('MouseEvents'); evt.initMouseEvent('mouseover',true,true, Test.ownerDocument.defaultView,0,0,0,0,0,false,false,false,false,0,null); var canceled = !Test.dispatchEvent(evt); if(canceled) alert('Event Cancelled');");
            //frmMain.GetJavaVar(MainWB, "var obj= $(\"#content .object-thumb .padding\").find(\"img\"); var evt = obj.ownerdocument.createEvent('MouseEvents'); evt.initEvent(\"mouseover\", true, false); var canceled = !obj.dispatchEvent(evt); if(canceled) alert('Event Cancelled');");
            //frmMain.GetJavaVar(MainWB, "var o= $(\"#content .object-thumb .padding\").find(\"img\"); var evt = o.ownerDocument.createEvent('MouseEvents'); evt.initMouseEvent('mouseover',true,true, o.ownerDocument.defaultView,0,0,0,0,0,false,false,false,false,0,null); var canceled = !o.dispatchEvent(evt); if(!canceled) alert('Event Cancelled');");
            //
            //
            //string sa = frmMain.GetDocument(MainWB).GetElementById("tooltipHolder").InnerHtml;

            //string strValue = frmMain.GetDocument(MainWB).GetElementById("tooltipHolder").InnerHtml;
            //EatDrog(MainWB, ShopItems.Barjomi);
            //
            //GroupFight(GroupFightAction.Fight); 

            /*
           #region Инициализация
                strValue = (string)frmMain.GetJavaVar(WB, "m.player.ore['0'].innerHTML");
                strSufix = "000";
                #endregion
                if (strValue != null)
                {
                    Me.Resource.Ore = Convert.ToInt32(Regex.Replace(strValue, "([,k])*", delegate(Match match)
                    {
                        switch (match.Value)
                        {
                            case ",": strSufix = "00"; return "";
                            case "k": return strSufix;
                            default: return match.Value;
                        }
                    }));
                }
            */

            /*
            int i;
            if (IsHPLow(MainWB, 100) ?
                (HealMePlus() ?
                true :
                CheckHealthEx(99, 49, Settings.HealPet50, Settings.HealPet100))
                : true) 
                 i=0; //Лечить в любом варианте до 100%
            */

            //Спуск на 2 уровень 00:06:08
            //HtmlElement H = frmMain.GetDocument(MainWB).GetElementById("searchNpcForm");

            //d.setTime((time - (-240) * 60) * 1000);
            //CheckHealthEx();
            //CheckHealthEx(0, 0, 0, 0);
            //GetMyStats(MainWB);
    

           // Safe(SafeAction.Check);

            //GetMyStats(MainWB);
            
             //GroupFight(GroupFightAction.Fight);

           // Bunker();
            
            //Me.Player.Level = 16;  



           
            //Извлекаем жизни пэта.


            // Petarena(PetAction.TrainWarPet);
            // Petarena(PetAction.Run);
            //AnalyseFight(HelpWB[0]);
            //
            //
            //Safe(SafeAction.Check);
            //GetMyStats(MainWB);
            // ClanWar(ClanWarAction.Check);
            
           //Automobile(AutomobileAction.Check);
           //HtmlElement HtmlEl = frmMain.GetDocument(MainWB).GetElementById("alert-title").Parent;
           //HtmlEl = frmMain.GetDocument(MainWB).GetElementById("cars-trip-choose").Parent;
           //HtmlEl = HtmlEl.GetElementsByTagName("div")[0];
            /*
                                            for (int i = 0; i < Me.Cars.Count<stcCar>(); i++)
                                 {
                                     //Нужно проверить совпадает ли последовательность машинок тут и при поездках на дачи.
                                     regex = new Regex("(?<=time=\")([0-9:])+(?=\")");
                                     Me.Cars[i].RideTime = TimeSpan.Parse("00:" + regex.Match(matches[i].Value).Value);
                                     HtmlEl = HC[i + Offset];
                                     if (HtmlEl.InnerText == null) Me.Cars[i].Timeout = DateTime.Now;
                                     else { Me.Cars[i].Timeout = DateTime.Now.Add(Convert.ToDateTime(HtmlEl.InnerText).TimeOfDay); Offset += 2; } //Сдвиг в 2 элемента при таймауте машинки.

                                     if (Settings.UseSpecialCar) //Кататься на определённой машинке!
                                     {
                                         if (Me.Cars[i].Model == Settings.SpecialCar) //Кататься на определённой машинке, и она найдена!
                                         {
                                             CarOffset = i + Offset; //сохраняем индекс нужной нам машинки!
                                             break; //Выходим, всё найдено!
                                         }
                                     }
                                     if ((TS == new TimeSpan() || TS > Me.Cars[i].RideTime) & Me.Cars[i].Timeout <= DateTime.Now) //Либо специальная машинка не выбрана, либо она попросту недоступна. (break в прошлой функции гарантирует поездку на выбраной машинке!)
                                     {
                                         TS = Me.Cars[i].RideTime; //Перезаписываем минимальное время поездки.
                                         CarOffset = i + Offset; //сохраняем индекс нужной нам машинки!
                                     }
                                 }
             */

        }
    }
}
        