using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace scvision
{
    public partial class Form1 : Form
    {
        private string myPath, myName, myTempPath;
        private FileStream gamelog_file = null;
        private StreamReader gamelog_reader = null;
        private string good_log_old;
        private List<Players> playerList = new List<Players>();
        private StatisticBeforeBattle statisticBeforeBattle = new StatisticBeforeBattle();
        private StatisticAfterBattle statisticAfterBattle = new StatisticAfterBattle();
        private Dictionary<string, int> settings = new Dictionary<string, int>();
        private string showMod = null;

        public Form1()
        {
            InitializeComponent();

            // Transparent background forms
            //this.Opacity = 0.6;
            //this.BackColor = Color.LimeGreen;
            //this.TransparencyKey = Color.LimeGreen;

            // Transparent background dataGridView
            //dataGridView1.BackgroundColor = Color.LimeGreen;
            //dataGridView1.EnableHeadersVisualStyles = false;
            //dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.Transparent;
            //dataGridView1.RowHeadersDefaultCellStyle.BackColor = Color.Transparent;
            dataGridView1.AutoSize = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            //dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, Color.DarkRed);

            // Get program path, name
            string fullPath = Assembly.GetExecutingAssembly().Location;
            myTempPath = GetMyTempPath();
            myPath = fullPath.Remove(fullPath.LastIndexOf("\\") + 1);
            string myFullName = fullPath.Substring(fullPath.LastIndexOf("\\") + 1);
            myName = myFullName.Remove(myFullName.IndexOf('.'));

            // Load dll from resourse
            //assembly_Newtonsoft_Json = Assembly.Load(Properties.Resources.Newtonsoft_Json);

            // Launching yourself through a temporary folder
            if (myPath != myTempPath)
            {
                LaunchThroughTempFolder();
            }

            // Delete old logs
            string logFile = myPath + myName + ".log";
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            // Write defoult settings, if not exists
            if (File.Exists(GetSettingsFile()) == false)
            {
                settings.Add("opasity", 60);
                settings.Add("kd", 1);
                settings.Add("kda", 1);
                settings.Add("wl", 1);
                SaveSettings(settings);
            }

            // Apply settings
            ApplySettings();

            // Run an additional thread SCLogScanner
            Thread myThread3 = new Thread(SCLogScanner);
            myThread3.IsBackground = true;
            myThread3.Start();

        }

        private void LaunchThroughTempFolder()
        {
            string myOldFullName = myPath + myName + ".exe";
            string myNewFullNmae = myTempPath + myName + ".exe";
            string dllNewFullName = myTempPath + "Newtonsoft.Json.dll";
            byte[] myBytes = File.ReadAllBytes(myOldFullName);
            byte[] newtonsoftJsonBytes = Properties.Resources.Newtonsoft_Json;
            Directory.CreateDirectory(myTempPath);
            File.WriteAllBytes(myNewFullNmae, myBytes);
            File.WriteAllBytes(dllNewFullName, newtonsoftJsonBytes);
            Process.Start(myNewFullNmae);
            Environment.Exit(0);
        }
        private void SCLogScanner()
        {
            string gamelogs_route = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\My Games\StarConflict\logs";

            while (true)
            {
                // Проверка запущенной игры
                bool sc_run = IsStarConflictRun();

                // Определение актуальных логов
                List<string> dirs = new List<string>(Directory.EnumerateDirectories(gamelogs_route));
                string good_log = dirs[dirs.Count - 1] + @"\";

                // Чтение логов
                if (File.Exists(good_log + "combat.log") & sc_run)
                {
                    LogReader(good_log);
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }
        }
        private bool IsStarConflictRun()
        {
            bool sc_run = false;
            Process[] sc_process = Process.GetProcessesByName("game");
            if (sc_process.Length > 0)
            {
                sc_run = true;
            }
            return sc_run;
        }
        private void LogReader(string good_log)
        {
            if (good_log != good_log_old)
            {
                Thread.Sleep(1000);
                AddLog("Change log sub-directory: " + good_log);
                good_log_old = good_log;
                gamelog_file = new FileStream(good_log + "game.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite); //создаем файловый поток
                gamelog_reader = new StreamReader(gamelog_file); // создаем «потоковый читатель» и связываем его с файловым потоком
            }

            string inputgamelog = gamelog_reader.ReadLine();
            string gamelog_item = inputgamelog ?? "";

            if (gamelog_item.Length > 0)
            {
                LogParsing(gamelog_item);
            }
            else
            {
                Thread.Sleep(700);
            }
        }
        private void LogParsing(string inputText)
        {
            // Exit if not good string
            if (inputText.IndexOf('|') < 0) { return; }

            // Exit if time old
            string timeText = inputText.Remove(inputText.IndexOf(' '));
            var time = DateTime.Parse(timeText);
            if (time < DateTime.Now.AddSeconds(-10) || time > DateTime.Now.AddSeconds(10))
            {
                return;
            }


            string text = inputText.Substring(inputText.IndexOf('|') + 1);
            string serchText1, serchText2;
            int index;

            serchText1 = "connect to dedicated server";
            if (text.IndexOf(serchText1) > -1)
            {
                string[] arr = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                index = Array.IndexOf(arr, "session");
                string session = arr[index + 1];
                index = Array.IndexOf(arr, "addr");
                string addr_port = arr[index + 1];
                string[] buffer_arr = addr_port.Split(new char[] { '|' });
                string addr = buffer_arr[0];

                AddLog("connectin to game server " + addr);
                statisticBeforeBattle.Session = session;
                statisticBeforeBattle.Addr = addr;
            }

            serchText1 = "client: connected to";
            if (text.IndexOf(serchText1) > -1)
            {
                AddLog("game started");
                CalculatingStatisticBeforeBattle();
                ShowStatisticBeforeBattle();
            }

            serchText1 = "client: ADD_PLAYER";
            if (text.IndexOf(serchText1) > -1)
            {
                string[] arr = text.Split(new char[] { ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                string num_str = arr[2];
                int num = Convert.ToInt32(num_str);
                string nickname = arr[3];
                string buffer_str = text.Substring(text.IndexOf(',') + 1);
                string[] buffer_arr = buffer_str.Split(new char[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                string uid_str = buffer_arr[0];
                int uid = Convert.ToInt32(uid_str);
                index = Array.IndexOf(arr, "team");
                string team_str = arr[index + 1];
                int team = Convert.ToInt32(team_str);
                Players player = new Players { Num = num, Nickname = nickname, Uid = uid, Team = team };

                if (playerList.Contains(player) == false)
                {
                    AddLog("new player " + nickname);

                    playerList.Add(player);
                    CalculatingStatisticBeforeBattle();
                    ShowStatisticBeforeBattle();
                }
            }

            serchText1 = "client: player";
            serchText2 = "leave game";
            if (text.IndexOf(serchText1) > -1 && text.IndexOf(serchText2) > -1)
            {
                string[] arr = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string num_str = arr[2];
                int num = Convert.ToInt32(num_str);
                Players player = new Players { Num = num };
                playerList.Remove(player);
                CalculatingStatisticBeforeBattle();
                ShowStatisticBeforeBattle();
            }

            serchText1 = "client: connection closed";
            if (text.IndexOf(serchText1) > -1)
            {
                AddLog("game end");
                playerList = new List<Players>();
                ClearColumns();
                CalculatingStatisticAfterBattle();
                ShowStatisticAfterBattle();
            }

            serchText1 = "starting level";
            if (text.IndexOf(serchText1) > -1)
            {
                string[] arr = text.Split(new char[] { ' ', '\'' }, StringSplitOptions.RemoveEmptyEntries);
                string map = arr[3];
                string mod = arr[4];
                statisticBeforeBattle.Map = map;
                statisticBeforeBattle.Mod = mod;
            }
        }
        private void CalculatingStatisticBeforeBattle()
        {
            List<Players> players = new List<Players>(playerList);
            for (int i = 0; i < playerList.Count; i++)
            {
                players[i] = GetStatistic(playerList[i]);
            }
            statisticBeforeBattle.Players = players;
        }
        private void ShowStatisticBeforeBattle()
        {
            showMod = "BeforeBattle";

            AddColumn("Никнейм", "nickname");
            AddColumn("KD", "kd");
            AddColumn("KDA", "kda");
            AddColumn("WinRate", "winrate");
            AddColumn("WL", "wl");
            AddColumn("DPG", "dpg");
            AddColumn("HPG", "hpg");
            AddColumn("Карма", "carma");

            ClearRows();
            foreach (Players player in statisticBeforeBattle.Players)
            {
                string nickname = player.Nickname;
                if (player.ClanName != null  && player.ClanName.Length > 0)
                {
                    nickname += " [" + player.ClanTag + ']';
                }
                if (player.Signature.Contains("bot") == true)
                {
                    nickname += " (bot)";
                }
                TryAddRowInTable(dataGridView1, nickname, player.KD, player.KDA, player.WinRate, player.WL, player.DPG, player.HPG, player.Carma, player.Signature);
            }
        }
        private void CalculatingStatisticAfterBattle()
        {

        }
        private void ShowStatisticAfterBattle()
        {
            showMod = "AfterBattle";
        }
        private List<Players> GetplyersByTeam(int team)
        {
            List<Players> outputList = new List<Players>();
            foreach (Players player in playerList)
            {
                if (player.Team == team)
                {
                    outputList.Add(player);
                }
            }
            return outputList;
        }
        private Players GetStatistic(Players player)
        {
            if (player.Signature != null)
            {
                return player;
            }

            Space space = GetDataFromSpace(player.Uid);
            SC sc = GetDataFromSC(player.Nickname);
            Int64 gamePlayed, gameWin, totalAssists, totalBattleTime, totalDeath, totalDmgDone, totalHealingDone, totalKill, totalVpDmgDone;
            gamePlayed = gameWin = totalAssists = totalBattleTime = totalDeath = totalDmgDone = totalHealingDone = totalKill = totalVpDmgDone = 0;

            if (player.Uid == 0)
            {
                player.Signature = "bot";
                return player;
            }

            if (player.Team == 1)
            {
                player.Signature += "ally";
            }

            if (space.result > 0)
            {
                player.Signature += "10";
                SpaceBigdata dataFromSpace = space.bigdata[space.bigdata.Count - 1];
                gamePlayed = sc.data.pvp.gamePlayed - dataFromSpace.gamePlayed;
                gameWin = sc.data.pvp.gameWin - dataFromSpace.gameWin;
                totalAssists = sc.data.pvp.totalAssists - dataFromSpace.totalAssists;
                totalBattleTime = sc.data.pvp.totalBattleTime - dataFromSpace.totalBattleTime;
                totalDeath = sc.data.pvp.totalDeath - dataFromSpace.totalDeath;
                totalDmgDone = sc.data.pvp.totalDmgDone - dataFromSpace.totalDmgDone;
                totalHealingDone = sc.data.pvp.totalHealingDone - dataFromSpace.totalHealingDone;
                totalKill = sc.data.pvp.totalKill - dataFromSpace.totalKill;
                totalVpDmgDone = sc.data.pvp.totalVpDmgDone - dataFromSpace.totalVpDmgDone;
            }

            if (gamePlayed == 0)
            {
                gamePlayed = sc.data.pvp.gamePlayed;
                gameWin = sc.data.pvp.gameWin;
                totalAssists = sc.data.pvp.totalAssists;
                totalBattleTime = sc.data.pvp.totalBattleTime;
                totalDeath = sc.data.pvp.totalDeath;
                totalDmgDone = sc.data.pvp.totalDmgDone;
                totalHealingDone = sc.data.pvp.totalHealingDone;
                totalKill = sc.data.pvp.totalKill;
                totalVpDmgDone = sc.data.pvp.totalVpDmgDone;
            }

            if (totalDeath == 0)
            {
                totalDeath = 1;
            }
            if (gamePlayed == 0)
            {
                gamePlayed = 1;
            }

            float WL;
            float KD = totalKill / (float)totalDeath;
            float KDA = (totalKill + totalAssists) / (float)totalDeath;
            float WinRate = gameWin / (float)gamePlayed * 100;
            Int64 DPG = totalDmgDone / gamePlayed;
            Int64 HPG = totalHealingDone / gamePlayed;
            if (gamePlayed - gameWin == 0)
            {
                WL = (float)gameWin / gamePlayed;
            }
            else
            {
                WL = (float)gameWin / (gamePlayed - gameWin);
            }

            player.KD = Math.Round(KD,2);
            player.KDA = Math.Round(KDA, 2);
            player.WinRate = Math.Round(WinRate, 0);
            player.WL = Math.Round(WL, 2);
            player.DPG = DPG;
            player.HPG = HPG;
            player.Carma = sc.data.karma;
            player.Signature += "ok";
            player.ClanName = sc.data.clan.name;
            player.ClanTag = sc.data.clan.tag;

            return player;
        }
        private SC GetDataFromSC(string nickname)
        {
            WebClient client = new WebClient();
            string webform = client.DownloadString("http://gmt.star-conflict.com/pubapi/v1/userinfo.php?nickname=" + nickname);
            SC sc = JsonConvert.DeserializeObject<SC>(webform); // or SC sc = JsonToSC(webform);

            if (sc.code == 1)
            {
                return sc;
            }

            if (sc.data.clan == null)
            {
                sc.data.clan = new SCdataClan();
                sc.data.clan.name = "";
                sc.data.clan.tag = "";
            }
            if (sc.data.clan.tag == null)
            {
                sc.data.clan.tag = "-----";
            }
            if (sc.data.pvp == null || sc.data.pvp.gamePlayed == null || sc.data.pvp.gameWin == null || sc.data.pvp.totalAssists == null
                || sc.data.pvp.totalBattleTime == null || sc.data.pvp.totalDeath == null || sc.data.pvp.totalDmgDone == null
                || sc.data.pvp.totalHealingDone == null || sc.data.pvp.totalKill == null || sc.data.pvp.totalVpDmgDone == null)
            {
                sc.data.pvp = new SCdataPvp();
                sc.data.pvp.gamePlayed = 0;
                sc.data.pvp.gameWin = 0;
                sc.data.pvp.totalAssists = 0;
                sc.data.pvp.totalBattleTime = 0;
                sc.data.pvp.totalDmgDone = 0;
                sc.data.pvp.totalHealingDone = 0;
                sc.data.pvp.totalKill = 0;
                sc.data.pvp.totalVpDmgDone = 0;
            }

            return sc;
        }
        private Space GetDataFromSpace(Int64 uid)
        {
            WebClient client = new WebClient();
            string webform = client.DownloadString("http://schistory.space/api/v1/userinfo.php?limit=10&uid=" + uid);
            Space space = JsonConvert.DeserializeObject<Space>(webform); // or Space space = JsonToSpace(webform);

            return space;
        }

        private void AddColumn(string text, string name)
        {
            if (dataGridView1.Columns.Contains(name) == true)
            {
                return;
            }

            if (settings.ContainsKey(name) &&  settings[name] == 0)
            {
                return;
            }

            var column1 = new DataGridViewColumn();
            column1.HeaderText = text; //текст в шапке
            //column1.Width = 100; //ширина колонки
            column1.ReadOnly = true; //значение в этой колонке нельзя править
            column1.Name = name; //текстовое имя колонки, его можно использовать вместо обращений по индексу
            column1.Frozen = true; //флаг, что данная колонка всегда отображается на своем месте
            column1.CellTemplate = new DataGridViewTextBoxCell(); //тип нашей колонки

            //column1.DefaultCellStyle.BackColor = Color.Transparent;
            //column1.DefaultCellStyle.SelectionBackColor = Color.Transparent;
            //column1.DefaultCellStyle.BackColor = Color.FromArgb(25, Color.DarkRed);
            //column1.DefaultCellStyle.BackColor = Color.FromArgb(25, Color.DarkRed);

            try
            {
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Columns.Add(column1); }));
            }
            catch
            {
                dataGridView1.Columns.Add(column1);
            }
        }
        private void ClearColumns()
        {
            try
            {
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Columns.Clear(); }));
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Rows.Clear(); }));
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Refresh(); }));
                BeginInvoke(new MethodInvoker(delegate { Thread.Sleep(50); }));
                BeginInvoke(new MethodInvoker(delegate { this.Width = dataGridView1.Width + 4; }));
            }
            catch
            {
                dataGridView1.Columns.Clear();
                dataGridView1.Rows.Clear();
                dataGridView1.Refresh();
                Thread.Sleep(50);
                this.Width = dataGridView1.Width + 4;
            }
            
        }
        private void ClearRows()
        {
            try
            {
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Rows.Clear(); }));
                BeginInvoke(new MethodInvoker(delegate { dataGridView1.Refresh(); }));
            }
            catch
            {
                dataGridView1.Rows.Clear();
                dataGridView1.Refresh();
            }
        }
        private void TryAddRowInTable(DataGridView dataGridView, string nickname, double kd, double kda, double winrate, double wl, Int64 dpg, Int64 hpg, Int64 carma, string signature)
        {
            try
            {
                BeginInvoke(new MethodInvoker(delegate { AddRowInTable(dataGridView, nickname, kd, kda, winrate, wl, dpg, hpg, carma, signature); }));
            }
            catch
            {
                AddRowInTable(dataGridView, nickname, kd, kda, winrate, wl, dpg, hpg, carma, signature);
            }
        }
        private void AddRowInTable(DataGridView dataGridView, string nickname, double kd, double kda, double winrate, double wl, Int64 dpg, Int64 hpg, Int64 carma, string signature)
        {
            var index = dataGridView.Rows.Add();
            if (dataGridView.Columns.Contains("nickname"))
            {
                dataGridView.Rows[index].Cells["nickname"].Value = nickname;
                if (signature.Contains("ally") == true)
                {
                    dataGridView.Rows[index].Cells["nickname"].Style.BackColor = Color.Green;
                    dataGridView.Rows[index].Cells["nickname"].Style.ForeColor = Color.White;
                    dataGridView.Rows[index].Cells["nickname"].Style.SelectionBackColor = Color.Green;
                    dataGridView.Rows[index].Cells["nickname"].Style.SelectionForeColor = Color.White;
                }
            }
            if (dataGridView.Columns.Contains("kd"))
            {
                dataGridView.Rows[index].Cells["kd"].Value = kd;
            }
            if (dataGridView.Columns.Contains("kda"))
            {
                dataGridView.Rows[index].Cells["kda"].Value = kda;
            }
            if (dataGridView.Columns.Contains("winrate"))
            {
                dataGridView.Rows[index].Cells["winrate"].Value = winrate;
            }
            if (dataGridView.Columns.Contains("wl"))
            {
                dataGridView.Rows[index].Cells["wl"].Value = wl;
            }
            if (dataGridView.Columns.Contains("dpg"))
            {
                dataGridView.Rows[index].Cells["dpg"].Value = dpg;
            }
            if (dataGridView.Columns.Contains("hpg"))
            {
                dataGridView.Rows[index].Cells["hpg"].Value = hpg;
            }
            if (dataGridView.Columns.Contains("carma"))
            {
                dataGridView.Rows[index].Cells["carma"].Value = carma;
                if (carma > 0)
                {
                    dataGridView.Rows[index].Cells["carma"].Style.BackColor = Color.Green;
                    dataGridView.Rows[index].Cells["carma"].Style.ForeColor = Color.White;
                    dataGridView.Rows[index].Cells["carma"].Style.SelectionBackColor = Color.Green;
                    dataGridView.Rows[index].Cells["carma"].Style.SelectionForeColor = Color.White;
                }
                if (carma < 0)
                {
                    dataGridView.Rows[index].Cells["carma"].Style.BackColor = Color.Red;
                    dataGridView.Rows[index].Cells["carma"].Style.ForeColor = Color.White;
                    dataGridView.Rows[index].Cells["carma"].Style.SelectionBackColor = Color.Green;
                    dataGridView.Rows[index].Cells["carma"].Style.SelectionForeColor = Color.White;
                }
            }




            return;

            //int index = dataGridView.Rows.Add(values);
            if (dataGridView.Columns.Contains("carma") == true)
            {
                if ((int)dataGridView.Rows[index].Cells["carma"].Value > 0)
                {
                    dataGridView.Rows[index].Cells["carma"].Style.ForeColor = Color.Green;
                }
                if ((int)dataGridView.Rows[index].Cells["carma"].Value < 0)
                {
                    dataGridView.Rows[index].Cells["carma"].Style.ForeColor = Color.Red;
                }
            }
        }
        private bool IsStringInTable(string text, string column, DataGridView dataGridView)
        {
            bool result = false;
            try
            {
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    if (row.Cells[column].Value != null && row.Cells[column].Value.ToString() == text)
                    {
                        result = true;
                        break;
                    }
                }
            }
            catch { }
            return result;
        }

        private void ApplySettings()
        {
            string settingsFile = GetSettingsFile();
            string text = File.ReadAllText(settingsFile);
            settings = JsonConvert.DeserializeObject<Dictionary<string, int>>(text); // or settings = JsonToSettings(text);
            this.Opacity = 1.0 - settings["opasity"]/100.0;
            ClearColumns();
            if (showMod == "BeforeBattle")
            {
                ShowStatisticBeforeBattle();
            }
            if (showMod == "AfterBattle")
            {
                ShowStatisticAfterBattle();
            }

        }
        public void SaveSettings(Dictionary<string, int> settings)
        {
            string settingsFile = GetSettingsFile();
            string text = JsonConvert.SerializeObject(settings); // or string text = SettingsToJson(settings);
            File.WriteAllText(settingsFile, text);
            ApplySettings();
        }
        private string GetSettingsFile()
        {
            string settingsPath = GetMyTempPath();
            string settingsFile = settingsPath + myName + ".data";
            return settingsFile;
        }
        private string GetMyTempPath()
        {
            string tempPath = Path.GetTempPath();
            string myTempPath = tempPath + "schistory.space\\";
            Directory.CreateDirectory(myTempPath);
            return myTempPath;
        }
        private void AddLog(string inputText, bool isShowMessageBox = false)
        {
            DateTime localDate = DateTime.Now;
            string logText = localDate.ToString(" [HH:mm:ss.fff] ") + inputText;
            try
            {
                //BeginInvoke(new MethodInvoker(delegate { listBox1.Items.Add(logText); }));
                //BeginInvoke(new MethodInvoker(delegate { listBox1.SelectedIndex = listBox1.Items.Count - 1; }));
            }
            catch
            {
                //listBox1.Items.Add(logText);
                //listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    //File.AppendAllText(myPath + myName + ".log", logText + "\r\n");
                    break;
                }
                catch
                {
                    Thread.Sleep(10);
                }
            }


            if (isShowMessageBox)
            {
                MessageBox.Show(inputText);
            }
        }
        private void WriteStreamToFile(string path, Stream inputStream)
        {
            var fileStream = File.Create(path);
            inputStream.Seek(0, SeekOrigin.Begin);
            inputStream.CopyTo(fileStream);
            fileStream.Close();
        }

        private void Form1_HelpButtonClicked(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Form2 form2 = new Form2(this);
            form2.Show();
        }

    }

    public class Players : IEquatable<Players>
    {
        public string Signature { get; set; }
        public int Num { get; set; }
        public Int64 Uid { get; set; }
        public string Nickname { get; set; }
        public int Team { get; set; }
        public double KD { get; set; }
        public double KDA { get; set; }
        public double WinRate { get; set; }
        public double WL { get; set; }
        public Int64 DPG { get; set; }
        public Int64 HPG { get; set; }
        public Int64 Carma { get; set; }
        public string ClanName { get; set; }
        public string ClanTag { get; set; }

        public bool Equals(Players player)
        {
            bool result = false;
            if (player.Uid == this.Uid && player.Uid != 0)
            {
                result = true;
            }
            if (player.Num == this.Num)
            {
                result = true;
            }
            return result;
        }
    }
    public class StatisticBeforeBattle
    {
        public string Session { get; set; }
        public string Addr { get; set; }
        public string Map { get; set; }
        public string Mod { get; set; }
        public List<Players> Players { get; set; }
    }
    public class StatisticAfterBattle
    {
        public string Session { get; set; }
        public string Addr { get; set; }
        public string Map { get; set; }
        public string Mod { get; set; }
        public List<Players> Team1Players { get; set; }
        public List<Players> Team2Players { get; set; }
    }
    public class Space
    {
        public int result { get; set; }
        public string text { get; set; }
        [JsonConverter(typeof(SingleOrArrayConverter<SpaceBigdata>))]
        public List<SpaceBigdata> bigdata { get; set; }
    }
    public class SpaceBigdata : IComparable<SpaceBigdata>
    {
        public DateTime date { get; set; }
        public Int64 uid { get; set; }
        public string nickname { get; set; }
        public Int64 effRating { get; set; }
        public Int64 karma { get; set; }
        public double prestigeBonus { get; set; }
        public Int64 gamePlayed { get; set; }
        public Int64 gameWin { get; set; }
        public Int64 totalAssists { get; set; }
        public Int64 totalBattleTime { get; set; }
        public Int64 totalDeath { get; set; }
        public Int64 totalDmgDone { get; set; }
        public Int64 totalHealingDone { get; set; }
        public Int64 totalKill { get; set; }
        public Int64 totalVpDmgDone { get; set; }
        public string clanName { get; set; }
        public string clanTag { get; set; }

        public int CompareTo(SpaceBigdata p)
        {
            return this.date.CompareTo(p.date);
        }
    }
    public class SC : IEquatable<SC>, IComparable<SC>
    {
        public string result { get; set; }
        public int code { get; set; }
        public string text { get; set; }
        public SCdata data { get; set; }
        public DateTime date { get; set; }

        public bool Equals(SC sc)
        {
            if (sc.code == 2 && this.data.nickname == sc.data.nickname)
            {
                return true;
            }
            else if (this.data.nickname == sc.data.nickname && this.date.Year == sc.date.Year && this.date.Month == sc.date.Month && this.date.Day == sc.date.Day)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public int CompareTo(SC p)
        {
            return this.date.CompareTo(p.date);
        }
    }
    public class SCdata
    {
        public Int64 effRating { get; set; }
        public Int64 karma { get; set; }
        public string nickname { get; set; }
        public double prestigeBonus { get; set; }
        public Int64 uid { get; set; }
        public SCdataPvp pvp { get; set; }
        public SCdataClan clan { get; set; }
    }
    public class SCdataPvp
    {
        public Int64 gamePlayed { get; set; }
        public Int64 gameWin { get; set; }
        public Int64 totalAssists { get; set; }
        public Int64 totalBattleTime { get; set; }
        public Int64 totalDeath { get; set; }
        public Int64 totalDmgDone { get; set; }
        public Int64 totalHealingDone { get; set; }
        public Int64 totalKill { get; set; }
        public Int64 totalVpDmgDone { get; set; }
    }
    public class SCdataClan
    {
        public string name { get; set; }
        public string tag { get; set; }
    }

    class SingleOrArrayConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<T>>();
            }
            return new List<T> { token.ToObject<T>() };
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
