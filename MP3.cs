//*Project:       MP3Lib
//*Author:        Michael D. Tighe
//*Created:       February 24, 2003 
//*Last Revision: October 24, 2003
//*Language:	  Visual C# (.NET Framework v1.1)
//*File:          MP3.cs
//*Description:   a library of classes which can return ID3 & ID3v2 tags from MP3 files, 
//*				  as well as manipulate Winamp v2.x
//******************************************************************************************	

using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MP3Lib {
    #region Library Enumerations
    /// <summary>
    /// Enumeration for displaying track time in Winamp.
    /// </summary>
    public enum WinampTimeMode {
        /// <summary>
        /// Shows time since track started.
        /// </summary>
        Elapsed = 40037,
        /// <summary>
        /// Shows time remaining in the track.
        /// </summary>
        Remaining
    };

    /// <summary>
    /// Enumeration for telling the class what to return when getting playlist info.
    /// </summary>
    public enum WinampPlaylistReturn {
        /// <summary>
        /// Returns the full path and filename for the songs in the playlist.
        /// </summary>
        Filenames = 211,
        /// <summary>
        /// Returns the song titles as they are displayed in the playlist window.
        /// </summary>
        SongTitles
    };
    #endregion

    /// <summary>
    /// Internal class for the Windows 32 API.
    /// </summary>
    internal class Win32API {
        /// <summary>
        /// Win32API Call: FindWindow
        /// </summary>
        [DllImport("user32.dll")]
        public static extern uint FindWindow(
            string lpClassName,
            string lpWindowName
        );
        /// <summary>
        /// Win32API Call: SendMessage
        /// </summary>
        [DllImport("user32.dll")]
        public static extern Int32 SendMessage(
            uint hWnd,   //Handle of Window to send msgs to
            uint wMsg,   //Type of message to send
            int wParam,  //1st message parameter
            int lParam   //2nd message parameter
        );
        /// <summary>
        /// Win32API Call: GetWindowThreadProcessId
        /// </summary>
        [DllImport("user32.dll")]
        public static extern Int32 GetWindowThreadProcessId(
            uint hWnd,
            ref uint ProcessID
        );
        /// <summary>
        /// Win32API Call: ReleaseCapture
        /// </summary>
        [DllImport("user32.dll")]
        public static extern Int32 ReleaseCapture();

        /// <summary>
        /// Win32API Call: OpenProcess
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern Int32 OpenProcess(
            uint DesiredAccess,
            bool InheritHandle,
            uint ProcessID
        );
        /// <summary>
        /// Win32API Call: ReadProcessMemory
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern Int32 ReadProcessMemory(
            int hProcess,
            int BaseAddress,
            ref byte Buffer,
            int Size,
            ref int BytesWritten
        );
        /// <summary>
        /// Win32API Call: CloseHandle
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern Int32 CloseHandle(int hObject);
    }

    /// <summary>
    /// The Winamp class, for controlling or obtaining info from Winamp 2.x.
    /// </summary>
    public class Winamp {

        /// <summary>
        /// class containing info on the current song
        /// </summary>
        public class WinampSong {
            #region Variables
            private int l_Len;
            private long l_Pos;
            private uint hWndWinamp;
            private Test.ID3.ID3v2 _ID3v2;
            private string fname;
            #endregion

            #region Constructor
            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="WinampHandle"></param>
            public WinampSong(uint WinampHandle) {
                hWndWinamp = WinampHandle;
                l_Len = -1;
                l_Pos = -1;
                Filename = GetWinampMP3();
                _ID3v2 = new Test.ID3.ID3v2(Filename);
            }
            #endregion

            #region Properties / Methods
            /// <summary>
            /// Length of the current song in seconds.
            /// </summary>
            public int Length {
                get {
                    if(hWndWinamp != 0) {
                        l_Len = Win32API.SendMessage(hWndWinamp, WM_USER, 1, WINAMP_TRACK_STAT);
                    } else {
                        l_Len = -1;
                    }
                    return l_Len;
                }
            }
            /// <summary>
            /// position in the current song in milliseconds
            /// </summary>
            public long Position {
                get {
                    if(hWndWinamp != 0) {
                        l_Pos = Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_TRACK_STAT);
                    } else {
                        l_Pos = -1;
                    }
                    return l_Pos;
                }
            }
            /// <summary>
            /// Sets the current position of the song to pos
            /// </summary>
            /// <param name="pos">Place to jump to in the song (in milliseconds)</param>
            public void Seek(int pos) {
                if(hWndWinamp != 0) {
                    Win32API.SendMessage(hWndWinamp, WM_USER, pos, WINAMP_TRACK_SEEK);
                }
            }

            /// <summary>
            /// Returns a string indicating the playback status of the current song.
            /// </summary>
            public string Status {
                get {
                    int state = Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_STATUS);
                    switch(state) {
                        case 1:
                            return "Playing";
                        case 3:
                            return "Paused";
                        default:
                            return "Stopped";
                    }
                }
            }
            /// <summary>
            /// Returns the sample rate of the current song.
            /// </summary>
            public int SampleRate {
                get {
                    if(hWndWinamp != 0) {
                        return Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_SONG_INFO);
                    } else {
                        return -1;
                    }
                }
            }
            /// <summary>
            /// Returns the number of channels in the current song.
            /// </summary>
            public int Channels {
                get {
                    if(hWndWinamp != 0) {
                        return Win32API.SendMessage(hWndWinamp, WM_USER, 2, WINAMP_SONG_INFO);
                    } else {
                        return -1;
                    }
                }
            }
            /// <summary>
            /// Returns the bit rate of the current song.
            /// </summary>
            public int BitRate {
                get {
                    if(hWndWinamp != 0) {
                        return Win32API.SendMessage(hWndWinamp, WM_USER, 1, WINAMP_SONG_INFO);
                    } else {
                        return -1;
                    }
                }
            }

            /// <summary>
            /// Returns the position of the current song in the playlist.
            /// </summary>
            public int PlaylistLocation {
                get {
                    if(hWndWinamp != 0) {
                        return Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_PLAYLIST_POS);
                    } else {
                        return -1;
                    }
                }
            }
            /*
            /// <summary>
            /// Returns the ID3 tag of the current song.
            /// </summary>
            /// <returns></returns>
            public ID3 ID3
            {
                get
                {
                    _ID3 = new ID3(GetWinampMP3());
                    return _ID3;
                }
            }
            */
            /// <summary>
            /// Returns the ID3v2 tag of the current song.
            /// </summary>
            /// <returns></returns>
            public Test.ID3.ID3v2 ID3v2 {
                get {
                    if(Filename != GetWinampMP3())
                        _ID3v2 = new Test.ID3.ID3v2(Filename = GetWinampMP3());
                    return _ID3v2;
                }
            }
            private string GetWinampMP3() {
                int Pointer;
                uint ProcessID = 0;
                int BytesRead = 0;
                int WinampProcessHandle;
                byte[] Data = new byte[MAX_LEN];

                if(hWndWinamp != 0) {
                    int CurSong = Win32API.SendMessage(hWndWinamp, WM_USER, 0, 125);

                    Pointer = Win32API.SendMessage(hWndWinamp, WM_USER, CurSong, 211);
                    Win32API.GetWindowThreadProcessId(hWndWinamp, ref ProcessID);
                    WinampProcessHandle = Win32API.OpenProcess(PROCESS_VM_READ, false, ProcessID);
                    Win32API.ReadProcessMemory(WinampProcessHandle, Pointer, ref Data[0],
                        Data.GetUpperBound(0), ref BytesRead);
                    Win32API.CloseHandle(WinampProcessHandle);

                    int nullpos = Encoding.UTF8.GetString(Data).IndexOf((char)0);
                    int strlen = Encoding.UTF8.GetString(Data).Length;
                    return Encoding.UTF8.GetString(Data).Remove(nullpos, (strlen - nullpos));
                } else {
                    return null;
                }
            }
            public string Filename {
                get;
                set;
            }
            #endregion
        }

        #region Class Variables
        uint hWndWinamp = new uint();
        WinampSong _CurrentSong;
        #endregion

        #region Class Constants
        const uint PROCESS_VM_READ = 0x0010;
        const uint WM_USER = 0x0400;
        const uint WM_COMMAND = 0x0111;
        const int MAX_LEN = 1024;

        const int WINAMP_VERSION = 0;
        const int WINAMP_OPTIONS_EQ = 40036;
        const int WINAMP_OPTIONS_PLEDIT = 40040;
        const int WINAMP_VOLUME_UP = 40058;
        const int WINAMP_VOLUME_DOWN = 40059;
        const int WINAMP_PREVIOUS = 40044;
        const int WINAMP_PLAY = 40045;
        const int WINAMP_PAUSE = 40046;
        const int WINAMP_STOP = 40047;
        const int WINAMP_FADING_STOP = 40147;
        const int WINAMP_NEXT = 40048;
        const int WINAMP_REWIND = 40144;
        const int WINAMP_FORWARD = 40148;
        const int WINAMP_PLAYLIST_START = 40154;
        const int WINAMP_OPEN_URL = 40155;
        const int WINAMP_SACT = 40157;
        const int WINAMP_PLAYLIST_END = 40158;
        const int WINAMP_FILE_INFO = 40188;
        const int WINAMP_LOAD_FILE = 40029;
        const int WINAMP_OPTIONS_PREFS = 40012;
        const int WINAMP_OPTIONS_AOT = 40019;
        const int WINAMP_HELP_ABOUT = 40041;
        const int WINAMP_CLOSE = 40001;
        const int WINAMP_TOGGLE_ME = 40258;
        const int WINAMP_TOGGLE_SHUFFLE = 40023;
        const int WINAMP_TOGGLE_REPEAT = 40022;
        const int WINAMP_VIZ_CONFIG = 40221;
        const int WINAMP_VIZ_PLUGIN = 40191;
        const int WINAMP_START_VIZ = 40192;
        const int WINAMP_SKIN_SELECT = 40219;
        //Constants for the CurrentSong Struct
        const int WINAMP_STATUS = 104;
        const int WINAMP_TRACK_STAT = 105;
        const int WINAMP_TRACK_SEEK = 106;
        const int WINAMP_PLAYLIST_JUMP = 121;
        const int WINAMP_VOLUME_SET = 122;
        const int WINAMP_PANNING_SET = 123;
        const int WINAMP_PLAYLIST_LEN = 124;
        const int WINAMP_PLAYLIST_POS = 125;
        const int WINAMP_SONG_INFO = 126;
        #endregion

        #region Constructors
        /// <summary>
        /// Default Constructor
        /// </summary>
        public Winamp() {
            hWndWinamp = Win32API.FindWindow("Winamp v1.x", null);
            if(hWndWinamp == 0) {
                System.Diagnostics.Process WAProc = new System.Diagnostics.Process();
                RegistryKey Root, Key, Value;
                string WinampPath = "";

                Root = Registry.CurrentUser;
                Key = Root.OpenSubKey("Software");
                Value = Key.OpenSubKey("Winamp");

                if(Value == null) {
                    throw new System.Exception("You do not have Winamp installed" +
                        ", or you do not have the user rights" +
                        " to run it.");
                }

                WinampPath = (string)Value.GetValue("") + @"\Winamp.exe";
                Root = Key = Value = null;

                WAProc.StartInfo.FileName = WinampPath;
                WAProc.Start();
                hWndWinamp = (uint)WAProc.MainWindowHandle.ToInt32();
                WAProc = null;
            }
            _CurrentSong = new WinampSong(hWndWinamp);
        }
        #endregion

        #region Class Delegates / Events
        /// <summary>
        /// WinampEventHandler is the delegate for all events.
        /// </summary>
        public delegate void WinampEventHandler(object sender, WinampEventArgs e);
        /// <summary>
        /// Fired when the play command is sent to Winamp.
        /// </summary>
        public event WinampEventHandler OnPlay;
        /// <summary>
        /// Fired when playback is stopped.
        /// </summary>
        public event WinampEventHandler OnStop;
        /// <summary>
        /// Fired when playback is paused.
        /// </summary>
        public event WinampEventHandler OnPause;
        /// <summary>
        /// Fired when the the Next command is sent to Winamp.
        /// </summary>
        public event WinampEventHandler OnNext;
        /// <summary>
        /// Fired when the Previous command is sent to Winamp.
        /// </summary>
        public event WinampEventHandler OnPrevious;
        /// <summary>
        /// Fired when the Close method is called.
        /// </summary>
        public event WinampEventHandler OnClose;
        /// <summary>
        /// Fired when the position of the playlist changes.
        /// </summary>
        public event WinampEventHandler OnPlaylistMove;
        #endregion

        #region Class Properties / Methods
        /// <summary>
        /// Returns a string array of all the songs currently in 
        /// Winamp's playlist.
        /// </summary>
        /// <returns></returns>
        public string[] Playlist(WinampPlaylistReturn Mode) {
            int Pointer;
            uint ProcessID = 0;
            int BytesRead = 0;
            string[] PlaylistEntry = new string[0];
            int WinampProcessHandle;
            byte[] Data = new byte[MAX_LEN];
            int SongCount = new int();

            if(hWndWinamp != 0) {
                SongCount = Win32API.SendMessage(hWndWinamp, WM_USER, 0, 124);
                PlaylistEntry = new string[SongCount];
                string tempString = "";

                for(int x = 0; x <= SongCount - 1; x++) {
                    Pointer = Win32API.SendMessage(hWndWinamp, WM_USER, x, (int)Mode);
                    Win32API.GetWindowThreadProcessId(hWndWinamp, ref ProcessID);
                    WinampProcessHandle = Win32API.OpenProcess(PROCESS_VM_READ, false, ProcessID);
                    Win32API.ReadProcessMemory(WinampProcessHandle, Pointer, ref Data[0],
                        Data.GetUpperBound(0), ref BytesRead);
                    Win32API.CloseHandle(WinampProcessHandle);

                    tempString = Encoding.UTF8.GetString(Data);
                    PlaylistEntry[x] = tempString.Substring(0, tempString.IndexOf('\0'));

                    Data = new byte[MAX_LEN];
                }
            }
            return PlaylistEntry;
        }
        /// <summary>
        /// Sends the Play command to Winamp.
        /// </summary>
        public void Play() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_PLAY, 0);
                //OnPlay(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Sends the Stop command to Winamp
        /// </summary>
        public void Stop() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_STOP, 0);
                OnStop(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Stops the current song while fading it out.
        /// </summary>
        public void FadingStop() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_FADING_STOP, 0);
                OnStop(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Skips to the next track in Winamp
        /// </summary>
        public void Next() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_NEXT, 0);
                OnNext(this, new WinampEventArgs(this.CurrentSong));
                OnPlaylistMove(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Goes back to the previous track in the playlist.
        /// </summary>
        public void Previous() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_PREVIOUS, 0);
                OnPrevious(this, new WinampEventArgs(this.CurrentSong));
                OnPlaylistMove(this, new WinampEventArgs(this.CurrentSong));
            }
        }

        /// <summary>
        /// Sends the Pause command to Winamp.
        /// </summary>
        public void Pause() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_PAUSE, 0);
                OnPause(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Rewinds to the last 5 seconds in the current track.
        /// </summary>
        public void Rewind() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_REWIND, 0);
            }
        }
        /// <summary>
        /// Skips ahead 5 seconds in the current track.
        /// </summary>
        public void Forward() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_FORWARD, 0);
            }
        }
        /// <summary>
        /// Returns to the beginning of the playlist.
        /// </summary>
        public void PlaylistTop() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_PLAYLIST_START, 0);
                OnPlaylistMove(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Jumps to the bottom of the playlist.
        /// </summary>
        public void PlaylistBottom() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_PLAYLIST_END, 0);
                OnPlaylistMove(this, new WinampEventArgs(this.CurrentSong));
            }
        }

        /// <summary>
        /// Property which returns the length of the playlist in tracks.
        /// </summary>
        public int PlaylistLength {
            get {
                if(hWndWinamp != 0) {
                    return Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_PLAYLIST_LEN);
                } else {
                    return -1;
                }
            }
        }
        /// <summary>
        /// Opens the URL dialog box.
        /// </summary>
        public void OpenURL() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_OPEN_URL, 0);
            }
        }

        /// <summary>
        /// Stops playback after the current track has finished.
        /// </summary>
        public void StopAfterCurrentTrack() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_SACT, 0);
                OnStop(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Toggles the Winamp Equalizer window.
        /// </summary>
        public void ToggleEQ() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_OPTIONS_EQ, 0);
            }
        }
        /// <summary>
        /// Toggles the Winamp Playlist window.
        /// </summary>
        public void TogglePlaylist() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_OPTIONS_PLEDIT, 0);
            }
        }
        /// <summary>
        /// Toggles the Always On Top option.
        /// </summary>
        public void ToggleAlwaysOnTop() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_OPTIONS_AOT, 0);
            }
        }
        /// <summary>
        /// Toggles the main Winamp window.
        /// </summary>
        public void ToggleMainWindow() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_TOGGLE_ME, 0);
            }
        }
        /// <summary>
        /// Toggles the Repeat function On/Off
        /// </summary>
        public void ToggleRepeat() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_TOGGLE_REPEAT, 0);
            }
        }
        /// <summary>
        /// Toggles the Shuffle function
        /// </summary>
        public void ToggleShuffle() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_TOGGLE_SHUFFLE, 0);
            }
        }
        /// <summary>
        /// Raises the Winamp volume by 1%
        /// </summary>
        public void VolumeUp() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_VOLUME_UP, 0);
            }
        }
        /// <summary>
        /// Lowers the Winamp volume by 1%
        /// </summary>
        public void VolumeDown() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_VOLUME_DOWN, 0);
            }
        }

        /// <summary>
        /// Sets the volume of Winamp to the value specified in level
        /// </summary>
        /// <param name="level">a value between 0-255 (0 being no sound)</param>
        public void VolumeSet(int level) {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_USER, level, WINAMP_VOLUME_SET);
            }
        }
        /// <summary>
        /// Shows the "Open File" dialog.
        /// </summary>
        public void OpenFile() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_LOAD_FILE, 0);
            }
        }

        /// <summary>
        /// Shows the Preferences dialog
        /// </summary>
        public void Preferences() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_OPTIONS_PREFS, 0);
            }
        }
        /// <summary>
        /// Shows the about dialog.
        /// </summary>
        public void About() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_HELP_ABOUT, 0);
            }
        }
        /// <summary>
        /// Shows the file information dialog.
        /// </summary>
        public void ShowInfo() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_FILE_INFO, 0);
            }
        }
        /// <summary>
        /// Sets the time display for Winamp based on the mode.
        /// </summary>
        /// <param name="Mode">enumeration: Elapsed or Remaining.</param>
        public void TimeMode(WinampTimeMode Mode) {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, (int)Mode, 0);
            }
        }

        /// <summary>
        /// Closes Winamp.
        /// </summary>
        public void Close() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_CLOSE, 0);
                OnClose(this, new WinampEventArgs(this.CurrentSong));
            }
        }
        /// <summary>
        /// Opens the configuration dialog for the active visual plug-in.
        /// </summary>
        public void VisualPlugInConfig() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_VIZ_CONFIG, 0);
            }
        }
        /// <summary>
        /// Opens the visualization plug-in dialog.
        /// </summary>
        public void VisualPlugIns() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_VIZ_PLUGIN, 0);
            }
        }
        /// <summary>
        /// Executes the selected visualization plugin.
        /// </summary>
        public void StartVisualPlugin() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_START_VIZ, 0);
            }
        }
        /// <summary>
        /// Opens the skin selection dialog box.
        /// </summary>
        public void SkinSelector() {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_COMMAND, WINAMP_SKIN_SELECT, 0);
            }
        }

        /// <summary>
        /// Sets the panning to the value specified in level.
        /// </summary>
        /// <param name="level">a value between 0-255 (0 = all left, 255 = all right)</param>
        public void PanningSet(int level) {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_USER, level, WINAMP_PANNING_SET);
            }
        }
        /// <summary>
        /// Class which returns information about the current Winamp song.
        /// </summary>
        public WinampSong CurrentSong {
            get {
                return _CurrentSong;
            }
        }
        /// <summary>
        /// Returns the current Winamp version.
        /// </summary>
        public string Version {
            get {
                if(hWndWinamp != 0) {
                    string ver = Convert.ToString(Win32API.SendMessage(hWndWinamp, WM_USER, 0, WINAMP_VERSION), 16);
                    ver = ver.Replace("2", "2.");
                    ver = ver.Replace("01", "1");
                    return "Winamp v" + ver;
                } else {
                    return null;
                }
            }
        }
        /// <summary>
        /// Jumps to the spot in the current playlist spcified in position. (zero-based)
        /// </summary>
        /// <param name="position">number of the position in the playlist to jump to</param>
        public void PlaylistGoto(int position) {
            if(hWndWinamp != 0) {
                Win32API.SendMessage(hWndWinamp, WM_USER, position, WINAMP_PLAYLIST_JUMP);
                //OnPlaylistMove(this, new WinampEventArgs(this.CurrentSong));
                Play();
            }
        }
        #endregion
    }

    /// <summary>
    /// WinampEventArgs Class, for returning winamp info when events are fired.
    /// </summary>
    public class WinampEventArgs : System.EventArgs {
        /// <summary>
        /// Returns info on the current song.
        /// </summary>
        public readonly Winamp.WinampSong CurrentSong;
        /// <summary>
        /// WinampEventArgs Constructor
        /// </summary>
        public WinampEventArgs(Winamp.WinampSong curSong) {
            CurrentSong = curSong;
        }
    }
}