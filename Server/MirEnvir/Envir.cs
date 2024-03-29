﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.MirEnvir
{
    public class Envir
    {
        public static object AccountLock = new object();
        public static object LoadLock = new object();

        public const int Version = 29;
        public const string DatabasePath = @".\Server.MirDB";
        public const string AccountPath = @".\Server.MirADB";
        public const string BackUpPath = @".\Back Up\";
        public const string GuildPath = @".\guilds\";
        private static readonly Regex AccountIDReg, PasswordReg, EMailReg, CharacterReg;

        public static int LoadVersion;

        private readonly DateTime _startTime = DateTime.Now;
        public readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public long Time { get; private set; }

        public DateTime Now
        {
            get { return _startTime.AddMilliseconds(Time); }
        }

        public bool Running { get; private set; }


        private static uint _objectID;
        public uint ObjectID
        {
            get { return ++_objectID; }
        }

        public static int _playerCount;
        public int PlayerCount
        {
            get { return Players.Count; }
        }


        public Random Random = new Random();
        private Thread _thread;
        private TcpListener _listener;
        private int _sessionID;
        public List<MirConnection> Connections = new List<MirConnection>();


        //Server DB
        public int MapIndex, ItemIndex,  MonsterIndex;
        public List<MapInfo> MapInfoList = new List<MapInfo>();
        public List<ItemInfo> ItemInfoList = new List<ItemInfo>();
        public List<MonsterInfo> MonsterInfoList = new List<MonsterInfo>();
        public DragonInfo DragonInfo = new DragonInfo();

        //User DB
        public int NextAccountID, NextCharacterID;
        public ulong NextUserItemID, NextAuctionID;
        public List<AccountInfo> AccountList = new List<AccountInfo>();
        public List<CharacterInfo> CharacterList = new List<CharacterInfo>(); 
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public int GuildCount, NextGuildID;
        public List<GuildObject> GuildList = new List<GuildObject>();

        //Live Info
        public List<Map> MapList = new List<Map>();
        public List<SafeZoneInfo> StartPoints = new List<SafeZoneInfo>(); 
        public List<ItemInfo> StartItems = new List<ItemInfo>(); 
        public List<PlayerObject> Players = new List<PlayerObject>();
        public bool Saving = false;
        public LightSetting Lights;
        public LinkedList<MapObject> Objects = new LinkedList<MapObject>();
        public Dragon DragonSystem;

        static Envir()
        {
            AccountIDReg =
                new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");
            PasswordReg =
                new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");
            EMailReg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
            CharacterReg =
                new Regex(@"^[A-Za-z0-9]{" + Globals.MinCharacterNameLength + "," + Globals.MaxCharacterNameLength +
                          "}$");
        }

        public static int LastCount = 0, LastRealCount = 0;
        public int MonsterCount;

        private void WorkLoop()
        {
            Time = _stopwatch.ElapsedMilliseconds;
            long conTime = Time;
            long saveTime = Time + Settings.SaveDelay * Settings.Minute;
            long userTime = Time + Settings.Minute * 5;
            long proceesTime = Time + 1000;
            int processCount = 0;
            int processRealCount = 0;

            LinkedListNode<MapObject> current = null;

            StartEnvir();
            StartNetwork();

            try
            {

                while (Running)
                {
                    Time = _stopwatch.ElapsedMilliseconds;

                    if (Time >= proceesTime)
                    {
                        LastCount = processCount;
                        LastRealCount = processRealCount;
                        processCount = 0;
                        processRealCount = 0;
                        proceesTime = Time + 1000;
                    }

                    if (conTime != Time)
                    {
                        conTime = Time;

                        AdjustLights();

                        lock (Connections)
                        {
                            for (int i = Connections.Count - 1; i >= 0; i--)
                                Connections[i].Process();
                        }
                    }

                    if (current == null)
                        current = Objects.First;

                    for (int i = 0; i < 100; i++)
                    {
                        if (current == null) break;

                        LinkedListNode<MapObject> next = current.Next;

                        if (Time > current.Value.OperateTime)
                        {
                            processRealCount++;
                            current.Value.Process();
                            current.Value.SetOperateTime();
                        }
                        processCount++;
                        current = next;
                    }


                    for (int i = 0; i < MapList.Count; i++)
                        MapList[i].Process();

                    if (DragonSystem != null) DragonSystem.Process();

                    if (Time >= saveTime)
                    {
                        saveTime = Time + Settings.SaveDelay*Settings.Minute;
                        BeginSaveAccounts();
                        SaveGuilds();
                    }

                    if (Time >= userTime)
                    {
                        userTime = Time + Settings.Minute*5;
                        Broadcast(new S.Chat
                            {
                                Message = string.Format("Online Players: {0}", Players.Count),
                                Type = ChatType.Hint
                            });
                    }

                    //   if (Players.Count == 0) Thread.Sleep(1);
                    //   GC.Collect();
                }
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);

                lock (Connections)
                {
                    for (int i = Connections.Count - 1; i >= 0; i--)
                        Connections[i].SendDisconnect(3);
                }
            }

            StopNetwork();
            StopEnvir(); 
            SaveAccounts();
            SaveGuilds(true);

            _thread = null;
        }


        private void AdjustLights()
        {
            LightSetting oldLights = Lights;

            int hours = (Now.Hour * 2) % 24;
            if (hours == 6 || hours == 7)
                Lights = LightSetting.Dawn;
            else if (hours >= 8 && hours <= 15)
                Lights = LightSetting.Day;
            else if (hours == 16 || hours == 17)
                Lights = LightSetting.Evening;
            else
                Lights = LightSetting.Night;

            if (oldLights == Lights) return;

            Broadcast(new S.TimeOfDay { Lights = Lights });
        }

        public void Broadcast(Packet p)
        {
            for (int i = 0; i < Players.Count; i++) Players[i].Enqueue(p);
        }

        public void RequiresBaseStatUpdate()
        {
            for (int i = 0; i < Players.Count; i++) Players[i].HasUpdatedBaseStats = false;
        }

        public void SaveDB()
        {
            using (FileStream stream = File.Create(DatabasePath))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(MapIndex);
                writer.Write(ItemIndex);
                writer.Write(MonsterIndex);

                writer.Write(MapInfoList.Count);
                for (int i = 0; i < MapInfoList.Count; i++)
                    MapInfoList[i].Save(writer);

                writer.Write(ItemInfoList.Count);
                for (int i = 0; i < ItemInfoList.Count; i++)
                    ItemInfoList[i].Save(writer);

                writer.Write(MonsterInfoList.Count);
                for (int i = 0; i < MonsterInfoList.Count; i++)
                    MonsterInfoList[i].Save(writer);

                DragonInfo.Save(writer);
            }
        }
        public void SaveAccounts()
        {
            while (Saving)
                Thread.Sleep(1);

            try
            {
                using (FileStream stream = File.Create(AccountPath))
                    SaveAccounts(stream);
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);
            }
        }

        private void SaveAccounts(Stream stream)
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(NextAccountID);
                writer.Write(NextCharacterID);
                writer.Write(NextUserItemID);
                writer.Write(GuildList.Count);
                writer.Write(NextGuildID);
                writer.Write(AccountList.Count);
                for (int i = 0; i < AccountList.Count; i++)
                    AccountList[i].Save(writer);

                writer.Write(NextAuctionID);
                writer.Write(Auctions.Count);
                foreach (AuctionInfo auction in Auctions)
                    auction.Save(writer);
            }
        }

        private void SaveGuilds(bool forced = false)
        {
            if (!Directory.Exists(GuildPath)) Directory.CreateDirectory(GuildPath);
            for (int i = 0; i < GuildList.Count; i++)
            {
                if (GuildList[i].NeedSave)
                {
                    GuildList[i].NeedSave = false;
                    MemoryStream mStream = new MemoryStream();
                    BinaryWriter writer = new BinaryWriter(mStream);
                    GuildList[i].Save(writer); //mir guild data :p
                    FileStream fStream = new FileStream(GuildPath + i.ToString() + ".mgd", FileMode.Create);
                    byte[] data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGuilds, fStream);
                }
            }
        }
        private void EndSaveGuilds(IAsyncResult result)
        {
            FileStream fStream = result.AsyncState as FileStream;
            if (fStream != null)
            {
                fStream.EndWrite(result);
                fStream.Dispose();
            }

        }
        private void BeginSaveAccounts()
        {
            if (Saving) return;

            Saving = true;
            

            using (MemoryStream mStream = new MemoryStream())
            {
                if (File.Exists(AccountPath))
                {
                    if (!Directory.Exists(BackUpPath)) Directory.CreateDirectory(BackUpPath);
                    string fileName = string.Format("Accounts {0:0000}-{1:00}-{2:00} {3:00}-{4:00}-{5:00}.bak", Now.Year, Now.Month, Now.Day, Now.Hour, Now.Minute, Now.Second);
                    if (File.Exists(Path.Combine(BackUpPath, fileName))) File.Delete(Path.Combine(BackUpPath, fileName));
                    File.Move(AccountPath, Path.Combine(BackUpPath, fileName));
                }

                SaveAccounts(mStream);
                FileStream fStream = new FileStream(AccountPath, FileMode.Create);

                byte[] data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveAccounts, fStream);
            }

        }
        private void EndSaveAccounts(IAsyncResult result)
        {
            FileStream fStream = result.AsyncState as FileStream;

            if (fStream != null)
            {
                fStream.EndWrite(result);
                fStream.Dispose();
            }

            Saving = false;
        }

        public void LoadDB()
        {
            lock (LoadLock)
            {
                if (!File.Exists(DatabasePath))
                    SaveDB();

                using (FileStream stream = File.OpenRead(DatabasePath))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    MapIndex = reader.ReadInt32();
                    ItemIndex = reader.ReadInt32();
                    MonsterIndex = reader.ReadInt32();

                    int count = reader.ReadInt32();
                    MapInfoList.Clear();
                    for (int i = 0; i < count; i++)
                        MapInfoList.Add(new MapInfo(reader));

                    count = reader.ReadInt32();
                    ItemInfoList.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        ItemInfoList.Add(new ItemInfo(reader, LoadVersion));
                        if ((ItemInfoList[i] != null) && (ItemInfoList[i].RandomStatsId < Settings.RandomItemStatsList.Count))
                        {
                            ItemInfoList[i].RandomStats = Settings.RandomItemStatsList[ItemInfoList[i].RandomStatsId];
                        }
                    }
                    count = reader.ReadInt32();
                    MonsterInfoList.Clear();
                    for (int i = 0; i < count; i++)
                        MonsterInfoList.Add(new MonsterInfo(reader));

                    if (LoadVersion >= 11) DragonInfo = new DragonInfo(reader);
                    else DragonInfo = new DragonInfo();
                }
                Settings.LinkGuildCreationItems(ItemInfoList);
            }

        }

        public void LoadAccounts()
        {
            lock (LoadLock)
            {
                if (!File.Exists(AccountPath))
                    SaveAccounts();

                using (FileStream stream = File.OpenRead(AccountPath))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    NextAccountID = reader.ReadInt32();
                    NextCharacterID = reader.ReadInt32();
                    NextUserItemID = reader.ReadUInt64();

                    if (LoadVersion > 27)
                    {
                        GuildCount = reader.ReadInt32();
                        NextGuildID = reader.ReadInt32();
                    }

                    int count = reader.ReadInt32();
                    AccountList.Clear();
                    CharacterList.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        AccountList.Add(new AccountInfo(reader));
                        CharacterList.AddRange(AccountList[i].Characters);
                    }

                    if (LoadVersion < 7) return;

                    foreach (AuctionInfo auction in Auctions)
                        auction.CharacterInfo.AccountInfo.Auctions.Remove(auction);
                    Auctions.Clear();

                    if (LoadVersion >= 8)
                        NextAuctionID = reader.ReadUInt64();

                    count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        AuctionInfo auction = new AuctionInfo(reader);

                        if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;

                        Auctions.AddLast(auction);
                        auction.CharacterInfo.AccountInfo.Auctions.AddLast(auction);
                    }

                    if (LoadVersion == 7)
                    {
                        foreach (AuctionInfo auction in Auctions)
                        {
                            if (auction.Sold && auction.Expired) auction.Expired = false;

                            auction.AuctionID = ++NextAuctionID;
                        }
                    }
                }
            }
        }

        public void LoadGuilds()
        {
            lock (LoadLock)
            {
                for (int i = 0; i < GuildCount; i++)
                {
                    GuildObject newGuild;
                    if (!File.Exists(GuildPath + i.ToString() + ".mgd"))
                        newGuild = new GuildObject();
                    else
                    {
                        using (FileStream stream = File.OpenRead(GuildPath + i.ToString() + ".mgd"))
                        using (BinaryReader reader = new BinaryReader(stream))
                            newGuild = new GuildObject(reader);
                    }
                    GuildList.Add(newGuild);
                }
            }
        }

        private bool BindCharacter(AuctionInfo auction)
        {
            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (CharacterList[i].Index != auction.CharacterIndex) continue;

                auction.CharacterInfo = CharacterList[i];
                return true;
            }
            return false;

        }

        public void Start()
        {
            if (Running || _thread != null) return;

            Running = true;

            _thread = new Thread(WorkLoop) {IsBackground = true};
            _thread.Start();
        }
        public void Stop()
        {
            Running = false;

            while (_thread != null)
                Thread.Sleep(1);
        }
        
        private void StartEnvir()
        {
            Players.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            MapList.Clear();

            LoadDB();


            for (int i = 0; i < MapInfoList.Count; i++)
                MapInfoList[i].CreateMap();

            for (int i = 0; i < ItemInfoList.Count; i++)
                if (ItemInfoList[i].StartItem)
                    StartItems.Add(ItemInfoList[i]);

            for (int i = 0; i < MonsterInfoList.Count; i++)
                MonsterInfoList[i].LoadDrops();

            if (DragonInfo.Enabled)
            {
                DragonSystem = new Dragon(DragonInfo);
                if (DragonSystem != null)
                {
                    if (DragonSystem.Load()) DragonSystem.Info.LoadDrops();
                }
            }
            SMain.Enqueue("Envir Started.");
        }
        private void StartNetwork()
        {
            Connections.Clear();
            LoadAccounts();
            LoadGuilds();
            _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(Connection, null);
            SMain.Enqueue("Network Started.");
        }

        private void StopEnvir()
        {
            MapList.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            Objects.Clear();
            Players.Clear();
            GC.Collect();

            SMain.Enqueue("Envir Stopped.");
        }
        private void StopNetwork()
        {
            _listener.Stop();

            lock (Connections)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].SendDisconnect(0);
            }

            long expire = Time + 5000;

            while (Connections.Count != 0 && _stopwatch.ElapsedMilliseconds < expire)
            {
                Time = _stopwatch.ElapsedMilliseconds;

                for (int i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].Process();

                Thread.Sleep(1);
            }
            

            Connections.Clear();
            SMain.Enqueue("Network Stopped.");
        }

        private void Connection(IAsyncResult result)
        {
            if (!Running || !_listener.Server.IsBound) return;

            try
            {
                TcpClient tempTcpClient = _listener.EndAcceptTcpClient(result);
                lock (Connections)
                    Connections.Add(new MirConnection(++_sessionID, tempTcpClient));
            }
            catch (Exception ex)
            {
                SMain.Enqueue(ex);
            }
            finally
            {
                while (Connections.Count >= Settings.MaxUser)
                    Thread.Sleep(1);

                if (Running && _listener.Server.IsBound)
                    _listener.BeginAcceptTcpClient(Connection, null);
            }
        }

        
        public void NewAccount(ClientPackets.NewAccount p, MirConnection c)
        {
            if (!Settings.AllowNewAccount)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 0});
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 1});
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 2});
                return;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 3});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 4});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 5});
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount {Result = 6});
                return;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    c.Enqueue(new ServerPackets.NewAccount {Result = 7});
                    return;
                }

                AccountList.Add(new AccountInfo(p) {Index = ++NextAccountID, CreationIP = c.IPAddress});


                c.Enqueue(new ServerPackets.NewAccount {Result = 8});
            }
        }
        public void ChangePassword(ClientPackets.ChangePassword p, MirConnection c)
        {
            if (!Settings.AllowChangePassword)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 0});
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 1});
                return;
            }

            if (!PasswordReg.IsMatch(p.CurrentPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 2});
                return;
            }

            if (!PasswordReg.IsMatch(p.NewPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 3});
                return;
            }

            AccountInfo account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 4});
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.ChangePasswordBanned {Reason = account.BanReason, ExpiryDate = account.ExpiryDate});
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            if (String.CompareOrdinal(account.Password, p.CurrentPassword) != 0)
            {
                c.Enqueue(new ServerPackets.ChangePassword {Result = 5});
                return;
            }

            account.Password = p.NewPassword;
            c.Enqueue(new ServerPackets.ChangePassword {Result = 6});
        }
        public void Login(ClientPackets.Login p, MirConnection c)
        {
            if (!Settings.AllowLogin)
            {
                c.Enqueue(new ServerPackets.Login { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.Login { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.Login { Result = 2 });
                return;
            }
            AccountInfo account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.Login { Result = 3 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > DateTime.Now)
                {
                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }
                account.Banned = false;
            }
                account.BanReason = string.Empty;
                account.ExpiryDate = DateTime.MinValue;


            if (String.CompareOrdinal(account.Password, p.Password) != 0)
            {
                c.Enqueue(new ServerPackets.Login { Result = 4 });
                return;
            }

            lock (AccountLock)
            {
                if (account.Connection != null)
                    account.Connection.SendDisconnect(1);

                account.Connection = c;
            }

            c.Account = account;
            c.Stage = GameStage.Select;

            account.LastDate = Now;
            account.LastIP = c.IPAddress;
            
            c.Enqueue(new ServerPackets.LoginSuccess { Characters = account.GetSelectInfo() });
        }
        public void NewCharacter(ClientPackets.NewCharacter p, MirConnection c)
        {
            if (!Settings.AllowNewCharacter)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 0});
                return;
            }

            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 1});
                return;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 2});
                return;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist &&
                p.Class != MirClass.Assassin)
            {
                c.Enqueue(new ServerPackets.NewCharacter {Result = 3});
                return;
            }

            int count = 0;

            for (int i = 0; i < c.Account.Characters.Count; i++)
            {
                if (c.Account.Characters[i].Deleted) continue;

                if (++count >= Globals.MaxCharacterCount)
                {
                    c.Enqueue(new ServerPackets.NewCharacter {Result = 4});
                    return;
                }
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new ServerPackets.NewCharacter {Result = 5});
                    return;
                }

                CharacterInfo info = new CharacterInfo(p, c) {Index = ++NextCharacterID};

                c.Account.Characters.Add(info);
                CharacterList.Add(info);

                c.Enqueue(new ServerPackets.NewCharacterSuccess {CharInfo = info.ToSelectInfo()});
            }
        }

        public bool AccountExists(string accountID)
        {
                for (int i = 0; i < AccountList.Count; i++)
                    if (String.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                        return true;

                return false;
        }
        public bool CharacterExists(string name)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (String.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        private AccountInfo GetAccount(string accountID)
        {
                for (int i = 0; i < AccountList.Count; i++)
                    if (String.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                        return AccountList[i];

                return null;
        }
        public List<AccountInfo> MatchAccounts(string accountID)
        {
                if (string.IsNullOrEmpty(accountID)) return new List<AccountInfo>(AccountList);

                List<AccountInfo> list = new List<AccountInfo>();

                for (int i = 0; i < AccountList.Count; i++)
                    if (AccountList[i].AccountID.IndexOf(accountID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);

            return list;
        }

        public void CreateAccountInfo()
        {
            AccountList.Add(new AccountInfo {Index = ++NextAccountID});
        }

        public void CreateMapInfo()
        {
            MapInfoList.Add(new MapInfo {Index = ++MapIndex});
        }

        public void CreateItemInfo(ItemType type = ItemType.Nothing)
        {
            ItemInfoList.Add(new ItemInfo { Index = ++ItemIndex, Type = type, RandomStatsId = 255});
        }

        public void CreateMonsterInfo()
        {
            MonsterInfoList.Add(new MonsterInfo {Index = ++MonsterIndex});
        }

        public void Remove(MapInfo info)
        {
            MapInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(ItemInfo info)
        {
            ItemInfoList.Remove(info);
        }
        public void Remove(MonsterInfo info)
        {
            MonsterInfoList.Remove(info);
            //Desync all objects\
        }


        public UserItem CreateFreshItem(ItemInfo info)
        {
            return new UserItem(info)
                {
                    UniqueID = ++NextUserItemID,
                    CurrentDura = info.Durability,
                    MaxDura = info.Durability,
                };
        }
        public UserItem CreateDropItem(int index)
        {
            return CreateDropItem(GetItemInfo(index));
        }
        public UserItem CreateDropItem(ItemInfo info)
        {
            if (info == null) return null;

            UserItem item = new UserItem(info)
                {
                    UniqueID = ++NextUserItemID,
                    MaxDura = info.Durability,
                    CurrentDura = (ushort) Math.Min(info.Durability, Random.Next(info.Durability) + 1000)
                };
            UpgradeItem(item);
            if (!info.NeedIdentify) item.Identified = true;
            return item;
        }

        public void UpgradeItem(UserItem item)
        {
            if (item.Info.RandomStats == null) return;
            RandomItemStat stat = item.Info.RandomStats;
            if ((stat.MaxDuraChance > 0) && (Random.Next(stat.MaxDuraChance) == 0))
            {
                int dura = RandomomRange(stat.MaxDuraMaxStat, stat.MaxDuraStatChance);
                item.MaxDura = (ushort)Math.Min(ushort.MaxValue, item.MaxDura + dura * 1000);
                item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + dura * 1000);
            }

            if ((stat.MaxAcChance > 0) && (Random.Next(stat.MaxAcChance) == 0)) item.AC = (byte)(RandomomRange(stat.MaxAcMaxStat-1, stat.MaxAcStatChance)+1);
            if ((stat.MaxMacChance > 0) && (Random.Next(stat.MaxMacChance) == 0)) item.MAC = (byte)(RandomomRange(stat.MaxMacMaxStat-1, stat.MaxMacStatChance)+1);
            if ((stat.MaxDcChance > 0) && (Random.Next(stat.MaxDcChance) == 0)) item.DC = (byte)(RandomomRange(stat.MaxDcMaxStat-1, stat.MaxDcStatChance)+1);
            if ((stat.MaxMcChance > 0) && (Random.Next(stat.MaxScChance) == 0)) item.MC = (byte)(RandomomRange(stat.MaxMcMaxStat-1, stat.MaxMcStatChance)+1);
            if ((stat.MaxScChance > 0) && (Random.Next(stat.MaxMcChance) == 0)) item.SC = (byte)(RandomomRange(stat.MaxScMaxStat-1, stat.MaxScStatChance)+1);
            if ((stat.AccuracyChance > 0) && (Random.Next(stat.AccuracyChance) == 0)) item.Accuracy = (byte)(RandomomRange(stat.AccuracyMaxStat-1, stat.AccuracyStatChance)+1);
            if ((stat.AgilityChance > 0) && (Random.Next(stat.AgilityChance) == 0)) item.Agility = (byte)(RandomomRange(stat.AgilityMaxStat-1, stat.AgilityStatChance)+1);
            if ((stat.HpChance > 0) && (Random.Next(stat.HpChance) == 0)) item.HP = (byte)(RandomomRange(stat.HpMaxStat-1, stat.HpStatChance)+1);
            if ((stat.MpChance > 0) && (Random.Next(stat.MpChance) == 0)) item.MP = (byte)(RandomomRange(stat.MpMaxStat-1, stat.MpStatChance)+1);
            if ((stat.StrongChance > 0) && (Random.Next(stat.StrongChance) == 0)) item.Strong = (byte)(RandomomRange(stat.StrongMaxStat-1, stat.StrongStatChance)+1);
            if ((stat.MagicResistChance > 0) && (Random.Next(stat.MagicResistChance) == 0)) item.MagicResist = (byte)(RandomomRange(stat.MagicResistMaxStat-1, stat.MagicResistStatChance)+1);
            if ((stat.PoisonResistChance > 0) && (Random.Next(stat.PoisonResistChance) == 0)) item.PoisonResist = (byte)(RandomomRange(stat.PoisonResistMaxStat-1, stat.PoisonResistStatChance)+1);
            if ((stat.HpRecovChance > 0) && (Random.Next(stat.HpRecovChance) == 0)) item.HealthRecovery = (byte)(RandomomRange(stat.HpRecovMaxStat-1, stat.HpRecovStatChance)+1);
            if ((stat.MpRecovChance > 0) && (Random.Next(stat.MpRecovChance) == 0)) item.ManaRecovery = (byte)(RandomomRange(stat.MpRecovMaxStat-1, stat.MpRecovStatChance)+1);
            if ((stat.PoisonRecovChance > 0) && (Random.Next(stat.PoisonRecovChance) == 0)) item.PoisonRecovery = (byte)(RandomomRange(stat.PoisonRecovMaxStat-1, stat.PoisonRecovStatChance)+1);
            if ((stat.CriticalRateChance > 0) && (Random.Next(stat.CriticalRateChance) == 0)) item.CriticalRate = (byte)(RandomomRange(stat.CriticalRateMaxStat-1, stat.CriticalRateStatChance)+1);
            if ((stat.CriticalDamageChance > 0) && (Random.Next(stat.CriticalDamageChance) == 0)) item.CriticalDamage = (byte)(RandomomRange(stat.CriticalDamageMaxStat-1, stat.CriticalDamageStatChance)+1);
            if ((stat.FreezeChance > 0) && (Random.Next(stat.FreezeChance) == 0)) item.Freezing = (byte)(RandomomRange(stat.FreezeMaxStat-1, stat.FreezeStatChance)+1);
            if ((stat.PoisonAttackChance > 0) && (Random.Next(stat.PoisonAttackChance) == 0)) item.PoisonAttack = (byte)(RandomomRange(stat.PoisonAttackMaxStat-1, stat.PoisonAttackStatChance)+1);
            if ((stat.AttackSpeedChance > 0) && (Random.Next(stat.AttackSpeedChance) == 0)) item.AttackSpeed = (sbyte)(RandomomRange(stat.AttackSpeedMaxStat-1, stat.AttackSpeedStatChance)+1);
            if ((stat.LuckChance > 0) && (Random.Next(stat.LuckChance) == 0)) item.Luck = (sbyte)(RandomomRange(stat.LuckMaxStat-1, stat.LuckStatChance)+1);
            if ((stat.CurseChance > 0) && (Random.Next(100) <= stat.CurseChance)) item.Cursed = true;
        }

        public int RandomomRange(int count, int rate)
        {
            int x = 0;
            for (int i = 0; i < count; i++) if (Random.Next(rate) == 0) x++;
            return x;
        }
        public bool BindItem(UserItem item)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;
                return true;
            }
            return false;
        }


        public Map GetMap(int index)
        {
            for (int i = 0; i < MapList.Count; i++)
                if (MapList[i].Info.Index == index) return MapList[i];

            return null;
        }

        //public Map GetMapByName(string name)
        //{
        //    return MapList.FirstOrDefault(t => String.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase));
        //}

        public Map GetMapByNameAndInstance(string name, int instanceValue = 0)
        {
            if (instanceValue < 0) instanceValue = 0;
            if (instanceValue > 0) instanceValue--;

            var instanceMapList = MapList.Where(t => String.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase)).ToList();
            return instanceValue < instanceMapList.Count() ? instanceMapList[instanceValue] : null;
        }

        public MonsterInfo GetMonsterInfo(int index)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].Index == index) return MonsterInfoList[i];

            return null;
        }
        public MonsterInfo GetMonsterInfo(string name)
        {
            for (int i = 0; i < MonsterInfoList.Count; i++)
            {
                MonsterInfo info = MonsterInfoList[i];
                if (info.Name != name && !info.Name.Replace(" ", "").StartsWith(name, StringComparison.OrdinalIgnoreCase)) continue;
                return info;
            }
            return null;
        }
        public PlayerObject GetPlayer(string name)
        {
            for (int i = 0; i < Players.Count; i++)
                if (String.Compare(Players[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Players[i];

            return null;
        }
        public PlayerObject GetPlayer(uint PlayerId)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].Info.Index == PlayerId)
                    return Players[i];

            return null;
        }
        public CharacterInfo GetCharacterInfo(string name)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (String.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return CharacterList[i];

            return null;
        }

        public CharacterInfo GetCharacterInfo(int Index)
        {
            for (int i = 0; i < CharacterList.Count; i++)
                if (CharacterList[i].Index == Index)
                    return CharacterList[i];

            return null;
        }

        public ItemInfo GetItemInfo(int index)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }
            return null;
        }
        public ItemInfo GetItemInfo(string name)
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                ItemInfo info = ItemInfoList[i];
                if (String.Compare(info.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }

        public void MessageAccount(AccountInfo account, string message, ChatType type)
        {
            if (account == null) return;
            if (account.Characters == null) return;

            for (int i = 0; i < account.Characters.Count; i++)
            {
                if (account.Characters[i].Player == null) continue;
                account.Characters[i].Player.ReceiveChat(message, type);
                return;
            }
        }
        public GuildObject GetGuild(string Name)
        {
            for (int i = 0; i < GuildList.Count; i++)
            {
                if (String.Compare(GuildList[i].Name.Replace(" ", ""), Name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return GuildList[i];
            }
            return null;
        }
        public GuildObject GetGuild(int index)
        {
            for (int i = 0; i < GuildList.Count; i++)
                if (GuildList[i].Guildindex == index)
                    return GuildList[i];
            return null;
        }
    }
}

