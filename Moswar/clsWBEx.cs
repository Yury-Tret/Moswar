using System;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.IO;
using Microsoft.Win32;

namespace Moswar
{
    public class clsWBEx
    {                
        public const int OLECMDERR_E_NOTSUPPORTED = unchecked((int)0x80040100);
        public const int OLECMDERR_E_DISABLED = unchecked((int)0x80040101);
        public const int OLECMDERR_E_UNKNOWNGROUP = unchecked((int)0x80040104);
        public const int S_OK = 0;
        public const int S_FALSE = 1;
        public const int E_NOTIMPL = unchecked((int)0x80004001);
        public const int E_NOINTERFACE = unchecked((int)0x80004002);

        public string Login;
        public string Password;

        private struct INTERNET_PROXY_INFO
        {
            public int dwAccessType;
            public IntPtr proxy;
            public IntPtr proxyBypass;
        }; 

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int lpdwBufferLength);

        [DllImport("ole32.dll", CharSet = CharSet.Auto)]
        public static extern int CreateStreamOnHGlobal(IntPtr hGlobal, bool fDeleteOnRelease, [MarshalAs(UnmanagedType.Interface)] out IStream ppstm);
        
        #region COM Interfaces
        [ComImport, Guid("00000112-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IOleObject
        {
            void SetClientSite(IOleClientSite pClientSite);
            void GetClientSite(ref IOleClientSite ppClientSite);
            void SetHostNames(object szContainerApp, object szContainerObj);
            void Close(uint dwSaveOption);
            void SetMoniker(uint dwWhichMoniker, object pmk);
            void GetMoniker(uint dwAssign, uint dwWhichMoniker, object ppmk);
            void InitFromData(System.Runtime.InteropServices.ComTypes.IDataObject pDataObject, bool fCreation, uint dwReserved);
            void GetClipboardData(uint dwReserved, ref System.Runtime.InteropServices.ComTypes.IDataObject ppDataObject);
            void DoVerb(uint iVerb, uint lpmsg, object pActiveSite, uint lindex, uint hwndParent, uint lprcPosRect);
            void EnumVerbs(ref object ppEnumOleVerb);
            void Update();
            void IsUpToDate();
            void GetUserClassID(uint pClsid);
            void GetUserType(uint dwFormOfType, uint pszUserType);
            void SetExtent(uint dwDrawAspect, uint psizel);
            void GetExtent(uint dwDrawAspect, uint psizel);
            void Advise(object pAdvSink, uint pdwConnection);
            void Unadvise(uint dwConnection);
            void EnumAdvise(ref object ppenumAdvise);
            void GetMiscStatus(uint dwAspect, uint pdwStatus);
            void SetColorScheme(object pLogpal);
        }
 
        [ComImport, Guid("00000118-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IOleClientSite
        {
            [PreserveSig]
            int SaveObject();
            [PreserveSig]
            int GetMoniker([In, MarshalAs(UnmanagedType.U4)] int dwAssign, [In, MarshalAs(UnmanagedType.U4)] int dwWhichMoniker, [MarshalAs(UnmanagedType.Interface)] out object moniker);
            [PreserveSig]
            int GetContainer(out object container);
            [PreserveSig]
            int ShowObject();
            [PreserveSig]
            int OnShowWindow(int fShow);
            [PreserveSig]
            int RequestNewObjectLayout();
        }

        #region IDocHostUIHandler
        public enum DOCHOSTUIDBLCLICK
        {
            DEFAULT = 0x0,
            SHOWPROPERTIES = 0x1,
            SHOWCODE = 0x2
        }

        public enum DOCHOSTUIFLAG
        {
            DOCHOSTUIFLAG_DIALOG = 0x00000001,
            DOCHOSTUIFLAG_DISABLE_HELP_MENU = 0x00000002,
            DOCHOSTUIFLAG_NO3DBORDER = 0x00000004,
            DOCHOSTUIFLAG_SCROLL_NO = 0x00000008,
            DOCHOSTUIFLAG_DISABLE_SCRIPT_INACTIVE = 0x00000010,
            DOCHOSTUIFLAG_OPENNEWWIN = 0x00000020,
            DOCHOSTUIFLAG_DISABLE_OFFSCREEN = 0x00000040,
            DOCHOSTUIFLAG_FLAT_SCROLLBAR = 0x00000080,
            DOCHOSTUIFLAG_DIV_BLOCKDEFAULT = 0x00000100,
            DOCHOSTUIFLAG_ACTIVATE_CLIENTHIT_ONLY = 0x00000200,
            DOCHOSTUIFLAG_OVERRIDEBEHAVIORFACTORY = 0x00000400,
            DOCHOSTUIFLAG_CODEPAGELINKEDFONTS = 0x00000800,
            DOCHOSTUIFLAG_URL_ENCODING_DISABLE_UTF8 = 0x00001000,
            DOCHOSTUIFLAG_URL_ENCODING_ENABLE_UTF8 = 0x00002000,
            DOCHOSTUIFLAG_ENABLE_FORMS_AUTOCOMPLETE = 0x00004000,
            DOCHOSTUIFLAG_ENABLE_INPLACE_NAVIGATION = 0x00010000,
            DOCHOSTUIFLAG_IME_ENABLE_RECONVERSION = 0x00020000,
            DOCHOSTUIFLAG_THEME = 0x00040000,
            DOCHOSTUIFLAG_NOTHEME = 0x00080000,
            DOCHOSTUIFLAG_NOPICS = 0x00100000,
            DOCHOSTUIFLAG_NO3DOUTERBORDER = 0x00200000,
            DOCHOSTUIFLAG_DELEGATESIDOFDISPATCH = 0x00400000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOCHOSTUIINFO
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cbSize;
            [MarshalAs(UnmanagedType.I4)]
            public int dwFlags;
            [MarshalAs(UnmanagedType.I4)]
            public int dwDoubleClick;
            [MarshalAs(UnmanagedType.I4)]
            public int dwReserved1;
            [MarshalAs(UnmanagedType.I4)]
            public int dwReserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMRECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public int message;
            public IntPtr wParam;
            public IntPtr lParam;
            public int time;
            POINT pt;
        }

        [ComImport, Guid("BD3F23C0-D43E-11CF-893B-00AA00BDCE1A"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDocHostUIHandler
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int ShowContextMenu([In, MarshalAs(UnmanagedType.U4)] int dwID, [In] ref POINT pt, [In] IntPtr pcmdtReserved, [In] IntPtr pdispReserved);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int GetHostInfo([In, Out] ref DOCHOSTUIINFO info);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int ShowUI([In, MarshalAs(UnmanagedType.I4)] int dwID, [In] IntPtr activeObject, [In] IntPtr commandTarget, [In] IntPtr frame, [In] IntPtr doc);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int HideUI();

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int UpdateUI();

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int EnableModeless([In, MarshalAs(UnmanagedType.Bool)] bool fEnable);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int OnDocWindowActivate([In, MarshalAs(UnmanagedType.Bool)] bool fActivate);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int OnFrameWindowActivate([In, MarshalAs(UnmanagedType.Bool)] bool fActivate);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int ResizeBorder([In] ref COMRECT rect, [In] IntPtr doc, bool fFrameWindow);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int TranslateAccelerator([In] ref MSG msg, [In] ref Guid group, [In, MarshalAs(UnmanagedType.I4)] int nCmdID);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int GetOptionKeyPath([Out, MarshalAs(UnmanagedType.LPArray)] String[] pbstrKey, [In, MarshalAs(UnmanagedType.U4)] int dw);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int GetDropTarget([In] IntPtr pDropTarget, [Out] out IntPtr ppDropTarget);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int GetExternal([Out, MarshalAs(UnmanagedType.IDispatch)] out object ppDispatch);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int TranslateUrl([In, MarshalAs(UnmanagedType.U4)] int dwTranslate, [In, MarshalAs(UnmanagedType.LPWStr)] string strURLIn, [Out, MarshalAs(UnmanagedType.LPWStr)] out string pstrURLOut);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int FilterDataObject([In] IntPtr pDO, [Out] out IntPtr ppDORet);
        }
        #endregion

        #region IOleControl
        public const int DISPID_AMBIENT_DLCONTROL = -5512;
        [ComImport, Guid("B196B288-BAB4-101A-B69C-00AA00341D07"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IOleControl
        {
            [PreserveSig]
            int GetControlInfo([Out] object pCI);
            [PreserveSig]
            int OnMnemonic([In] ref MSG pMsg);
            [PreserveSig]
            int OnAmbientPropertyChange(int dispID);
            [PreserveSig]
            int FreezeEvents(int bFreeze);
        }
        [Flags]
        public enum AmbientProperty : uint
        {
            DLCTL_DLIMAGES = 0x00000010,
            DLCTL_VIDEOS = 0x00000020,
            DLCTL_BGSOUNDS = 0x00000040,
            DLCTL_NO_SCRIPTS = 0x00000080,
            DLCTL_NO_JAVA = 0x00000100,
            DLCTL_NO_RUNACTIVEXCTLS = 0x00000200,
            DLCTL_NO_DLACTIVEXCTLS = 0x00000400,
            DLCTL_DOWNLOADONLY = 0x00000800,
            DLCTL_NO_FRAMEDOWNLOAD = 0x00001000,
            DLCTL_RESYNCHRONIZE = 0x00002000,
            DLCTL_PRAGMA_NO_CACHE = 0x00004000,
            DLCTL_NO_BEHAVIORS = 0x00008000,
            DLCTL_NO_METACHARSET = 0x00010000,
            DLCTL_URL_ENCODING_DISABLE_UTF8 = 0x00020000,
            DLCTL_URL_ENCODING_ENABLE_UTF8 = 0x00040000,
            DLCTL_NOFRAMES = 0x00080000,
            DLCTL_FORCEOFFLINE = 0x10000000,
            DLCTL_NO_CLIENTPULL = 0x20000000,
            DLCTL_SILENT = 0x40000000,
            DLCTL_OFFLINEIFNOTCONNECTED = 0x80000000,
            DLCTL_OFFLINE = 0x80000000
        }
        #endregion

        #region IOleCommandTarget
        /// <summary>
        /// http://msdn.microsoft.com/en-us/library/windows/desktop/ms691264(v=vs.85).aspx
        /// </summary>
        public enum OLECMDID
        {
            OLECMDID_OPEN = 1,
            OLECMDID_NEW = 2,
            OLECMDID_SAVE = 3,
            OLECMDID_SAVEAS = 4,
            OLECMDID_SAVECOPYAS = 5,
            OLECMDID_PRINT = 6,
            OLECMDID_PRINTPREVIEW = 7,
            OLECMDID_PAGESETUP = 8,
            OLECMDID_SPELL = 9,
            OLECMDID_PROPERTIES = 10,
            OLECMDID_CUT = 11,
            OLECMDID_COPY = 12,
            OLECMDID_PASTE = 13,
            OLECMDID_PASTESPECIAL = 14,
            OLECMDID_UNDO = 15,
            OLECMDID_REDO = 16,
            OLECMDID_SELECTALL = 17,
            OLECMDID_CLEARSELECTION = 18,
            OLECMDID_ZOOM = 19,
            OLECMDID_GETZOOMRANGE = 20,
            OLECMDID_UPDATECOMMANDS = 21,
            OLECMDID_REFRESH = 22,
            OLECMDID_STOP = 23,
            OLECMDID_HIDETOOLBARS = 24,
            OLECMDID_SETPROGRESSMAX = 25,
            OLECMDID_SETPROGRESSPOS = 26,
            OLECMDID_SETPROGRESSTEXT = 27,
            OLECMDID_SETTITLE = 28,
            OLECMDID_SETDOWNLOADSTATE = 29,
            OLECMDID_STOPDOWNLOAD = 30,
            OLECMDID_ONTOOLBARACTIVATED = 31,
            OLECMDID_FIND = 32,
            OLECMDID_DELETE = 33,
            OLECMDID_HTTPEQUIV = 34,
            OLECMDID_HTTPEQUIV_DONE = 35,
            OLECMDID_ENABLE_INTERACTION = 36,
            OLECMDID_ONUNLOAD = 37,
            OLECMDID_PROPERTYBAG2 = 38,
            OLECMDID_PREREFRESH = 39,
            OLECMDID_SHOWSCRIPTERROR = 40,
            OLECMDID_SHOWMESSAGE = 41,
            OLECMDID_SHOWFIND = 42,
            OLECMDID_SHOWPAGESETUP = 43,
            OLECMDID_SHOWPRINT = 44,
            OLECMDID_CLOSE = 45,
            OLECMDID_ALLOWUILESSSAVEAS = 46,
            OLECMDID_DONTDOWNLOADCSS = 47,
            OLECMDID_UPDATEPAGESTATUS = 48,
            OLECMDID_PRINT2 = 49,
            OLECMDID_PRINTPREVIEW2 = 50,
            OLECMDID_SETPRINTTEMPLATE = 51,
            OLECMDID_GETPRINTTEMPLATE = 52,
            OLECMDID_PAGEACTIONBLOCKED = 55,
            OLECMDID_PAGEACTIONUIQUERY = 56,
            OLECMDID_FOCUSVIEWCONTROLS = 57,
            OLECMDID_FOCUSVIEWCONTROLSQUERY = 58,
            OLECMDID_SHOWPAGEACTIONMENU = 59,
            OLECMDID_ADDTRAVELENTRY = 60,
            OLECMDID_UPDATETRAVELENTRY = 61,
            OLECMDID_UPDATEBACKFORWARDSTATE = 62,
            OLECMDID_OPTICAL_ZOOM = 63,
            OLECMDID_OPTICAL_GETZOOMRANGE = 64,
            OLECMDID_WINDOWSTATECHANGED = 65,
            OLECMDID_ACTIVEXINSTALLSCOPE = 66,
            OLECMDID_UPDATETRAVELENTRY_DATARECOVERY = 67
        }

        [StructLayout(LayoutKind.Sequential)]
        public class OLECMD
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cmdID;
            [MarshalAs(UnmanagedType.U4)]
            public int cmdf;
        }

        [ComImport, Guid("B722BCCB-4E68-101B-A2BC-00AA00404770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IOleCommandTarget
        {

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int QueryStatus(
                [In] IntPtr pguidCmdGroup,
                [In, MarshalAs(UnmanagedType.U4)] uint cCmds,
                [In, Out, MarshalAs(UnmanagedType.Struct)] ref OLECMD prgCmds,
                //This parameter must be IntPtr, as it can be null
                [In, Out] IntPtr pCmdText);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int Exec(
                //[In] ref Guid pguidCmdGroup,
                //have to be IntPtr, since null values are unacceptable
                //and null is used as default group!
                [In] IntPtr pguidCmdGroup,
                [In, MarshalAs(UnmanagedType.U4)] uint nCmdID,
                [In, MarshalAs(UnmanagedType.U4)] uint nCmdexecopt,
                [In] IntPtr pvaIn,
                [In, Out] IntPtr pvaOut);
        }
        #endregion

        #region IDocHostShowUI
        public const uint MB_ICONINFORMATION = (uint)0x00000040L;
        public const uint MB_ICONWARNING = (uint)0x00000030L;
        public const uint MB_ICONQUESTION = (uint)0x00000020L;
        public const uint MB_ICONERROR = (uint)0x00000030L;

        [ComImport, ComVisible(true)]
        [Guid("C4D244B0-D43E-11CF-893B-00AA00BDCE1A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDocHostShowUI
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int ShowMessage(
                IntPtr hwnd,
                [MarshalAs(UnmanagedType.LPWStr)] string lpstrText,
                [MarshalAs(UnmanagedType.LPWStr)] string lpstrCaption,
                [MarshalAs(UnmanagedType.U4)] uint dwType,
                [MarshalAs(UnmanagedType.LPWStr)] string lpstrHelpFile,
                [MarshalAs(UnmanagedType.U4)] uint dwHelpContext,
                [In, Out] ref int lpResult);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int ShowHelp(
                IntPtr hwnd,
                [MarshalAs(UnmanagedType.LPWStr)] string pszHelpFile,
                [MarshalAs(UnmanagedType.U4)] uint uCommand,
                [MarshalAs(UnmanagedType.U4)] uint dwData,
                [In, MarshalAs(UnmanagedType.Struct)] POINT ptMouse,
                [Out, MarshalAs(UnmanagedType.IDispatch)] object pDispatchObjectHit);
        }
        #endregion

        #region IServiceProvider
        [ComImport, GuidAttribute("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        [ComImport, GuidAttribute("79EAC9D0-BAF9-11CE-8C82-00AA004BA90B"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IAuthenticate
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int Authenticate(ref IntPtr phwnd, ref IntPtr pszUsername, ref IntPtr pszPassword);
        }

        [ComImport, Guid("D81F90A3-8156-44F7-AD28-5ABB87003274"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IProtectFocus
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int AllowFocusChange([In, Out] ref bool pfAllow);
        }
        #endregion

        #region IPersistStreamInit
        public enum tagSTREAM_SEEK
        {
            STREAM_SEEK_SET = 0,
            STREAM_SEEK_CUR = 1,
            STREAM_SEEK_END = 2
        }

        public enum tagSTATFLAG
        {
            STATFLAG_DEFAULT = 0,
            STATFLAG_NONAME = 1,
            STATFLAG_NOOPEN = 2
        }

        [ComImport, Guid("7FD52380-4E07-101B-AE2D-08002B2EC713"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), ComVisible(true)]
        public interface IPersistStreamInit
        {
            void GetClassID([In, Out] ref Guid pClassID);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int IsDirty();

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int Load([In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IStream pstm);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int Save([In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IStream pstm, [In, MarshalAs(UnmanagedType.Bool)] bool fClearDirty);

            void GetSizeMax([Out, MarshalAs(UnmanagedType.LPArray)] long pcbSize);

            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int InitNew();
        }
        #endregion
      
        #endregion

        #region IE REGEDIT TWIKS
        //http://msdn.microsoft.com/ru-ru/library/ee330720(v=vs.85).aspx
        //Для ИЕ8 в cmd-> regsvr32 actxprxy.dll
        /// <summary>
        /// Эмуляция режима работы ИЕ
        /// <param name="WB"></param> WebBrowserControl для определения версии установленной в системе
        /// <param name="MaxIEVersion"></param> Номер эмулируемой версии
        /// </summary>
        static public void EmulateIEMode(WebBrowser WB, int MaxIEVersion = 9)
        {
            if (WB.Version.Major >= 8)
            {
                int EmulateIEVersion = (WB.Version.Major > MaxIEVersion ? MaxIEVersion : WB.Version.Major) * 1000;
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_BROWSER_EMULATION").SetValue(Path.GetFileName(Application.ExecutablePath), EmulateIEVersion);
            }
        }
        /// <summary>
        /// Установка максимального колличества HTTP-сокетов для загрузки страниц
        /// <param name="WB"></param> WebBrowserControl для определения версии установленной в системе
        /// <param name="Max"></param> Количество максимально разрешённых параллельных соединений [2-128]
        /// </summary>
        static public void SetMaxIEConnections(WebBrowser WB, int Max = 10)
        {
            if (WB.Version.Major >= 8)
            {
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_MAXCONNECTIONSPER1_0SERVER").SetValue(Path.GetFileName(Application.ExecutablePath), Max); //HTTP 1.0
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_MAXCONNECTIONSPERSERVER").SetValue(Path.GetFileName(Application.ExecutablePath), Max); //HTTP 1.1
            }
            else
            {
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\MaxConnectionsPer1_0Server").SetValue(Path.GetFileName(Application.ExecutablePath), Max); //HTTP 1.0
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\MaxConnectionsPerServer").SetValue(Path.GetFileName(Application.ExecutablePath), Max); //HTTP 1.1
            }
        }
        /// <summary>
        /// Отключение/Включение звуков при навигации
        /// <param name="WB"></param> WebBrowserControl для определения версии установленной в системе
        /// <param name="NoSound"></param> Отключить звук при навигации
        /// </summary>
        static public void ShutDownIENavigationSound(WebBrowser WB, bool NoSound = true)
        {
            if (WB.Version.Major >= 8)
            {
                Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\FEATURE_DISABLE_NAVIGATION_SOUNDS").SetValue(Path.GetFileName(Application.ExecutablePath), NoSound ? 1 : 0);
            }
        }
        #endregion

        public void SetProxyServer(string strProxy)
        {
            const int INTERNET_OPTION_PROXY = 38;
            const int INTERNET_OPEN_TYPE_PROXY = 3;
            const int INTERNET_OPEN_TYPE_DIRECT = 1;

            INTERNET_PROXY_INFO ProxyInfo = new INTERNET_PROXY_INFO();            
            // Filling in structure 
            if (strProxy == null)
            {
                ProxyInfo.dwAccessType = INTERNET_OPEN_TYPE_DIRECT;
            }
            else 
            {
                strProxy = Regex.Replace(strProxy, "( |^0|(?<=[.])0{1,2})|(?<=:)0{1,4}", ""); //На всякий случай обрабатываю IP убираю 0 в начале каждого сегмента
                ProxyInfo.dwAccessType = INTERNET_OPEN_TYPE_PROXY;
                ProxyInfo.proxy = Marshal.StringToHGlobalAnsi(strProxy);
                ProxyInfo.proxyBypass = Marshal.StringToHGlobalAnsi("local");
            }            
            // Allocating memory 
            IntPtr ProxyInfoPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(ProxyInfo));
            // Converting structure to IntPtr
            Marshal.StructureToPtr(ProxyInfo, ProxyInfoPtr, true);
            bool iReturn = InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PROXY, ProxyInfoPtr, Marshal.SizeOf(ProxyInfo));
        }
        public string GetHTMLSource(HtmlDocument Document, Encoding Encode)
        {
            if (Document == null) return string.Empty;

            //Declare vars
            int HRESULT;
            IStream pStream = null;
            IPersistStreamInit pPersistStreamInit = null;

            // Query for IPersistStreamInit.
            pPersistStreamInit = Document.DomDocument as IPersistStreamInit;
            if (pPersistStreamInit == null) return string.Empty;

            //Create stream, delete on release
            HRESULT = CreateStreamOnHGlobal(IntPtr.Zero, true, out pStream);
            if ((pStream == null) || (HRESULT != S_OK)) return string.Empty;

            //Save
            HRESULT = pPersistStreamInit.Save(pStream, false);
            if (HRESULT != S_OK) return string.Empty;

            //Now read from stream....

            //First get the size
            long ulSizeRequired = (long)0;
            ////LARGE_INTEGER
            //long liBeggining = (long)0;
            System.Runtime.InteropServices.ComTypes.STATSTG statstg = new System.Runtime.InteropServices.ComTypes.STATSTG();
            pStream.Seek(0, (int)tagSTREAM_SEEK.STREAM_SEEK_SET, IntPtr.Zero);
            pStream.Stat(out statstg, (int)tagSTATFLAG.STATFLAG_NONAME);

            //Size
            ulSizeRequired = statstg.cbSize;
            if (ulSizeRequired == (long)0) return string.Empty;

            //Allocate buffer + read
            byte[] pSource = new byte[ulSizeRequired];
            pStream.Read(pSource, (int)ulSizeRequired, IntPtr.Zero);

            #region Auto-Encoding (Работает не всегда)
            ////UTF-8: EF BB BF
            ////UTF-16 big endian byte order: FE FF
            ////UTF-16 little endian byte order: FF FE
            ////UTF-32 big endian byte order: 00 00 FE FF
            ////UTF-32 little endian byte order: FF FE 00 00
            //Encoding enc = null;
            //if (pSource.Length > 8)
            //{
            //    // Check byte order mark
            //    if ((pSource[0] == 0xFF) && (pSource[1] == 0xFE)) // UTF16LE
            //        enc = Encoding.Unicode;

            //    if ((pSource[0] == 0xFE) && (pSource[1] == 0xFF)) // UTF16BE
            //        enc = Encoding.BigEndianUnicode;

            //    if ((pSource[0] == 0xEF) && (pSource[1] == 0xBB) && (pSource[2] == 0xBF)) //UTF8
            //        enc = Encoding.UTF8;

            //    if (enc == null)
            //    {
            //        // Check for alternating zero bytes which might indicate Unicode
            //        if ((pSource[1] == 0) && (pSource[3] == 0) && (pSource[5] == 0) && (pSource[7] == 0))
            //            enc = Encoding.Unicode;
            //    }
            //}

            //if (enc == null) enc = Encoding.UTF8;

            //int bomLength = enc.GetPreamble().Length;

            //return enc.GetString(pSource, bomLength, pSource.Length - bomLength);
            #endregion            
            
            return Encode.GetString(pSource);
        }        
    }    
}
