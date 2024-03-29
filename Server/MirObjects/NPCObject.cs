﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects
{
    public sealed class NPCObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Merchant; }
        }

        public const string
            MainKey = "[@MAIN]",
            BuyKey = "[@BUY]",
            SellKey = "[@SELL]",
            RepairKey = "[@REPAIR]",
            SRepairKey = "[@SREPAIR]",
            BuyBackKey = "[@BUYBACK]",
            StorageKey = "[@STORAGE]",
            ConsignKey = "[@CONSIGN]",
            MarketKey = "[@MARKET]",
            ConsignmentsKey = "[@CONSIGNMENT]",
            TradeKey = "[TRADE]",
            TypeKey = "[TYPES]",
            GuildCreateKey = "[@CREATEGUILD]";


        //public static Regex Regex = new Regex(@"[^\{\}]<.*?/(.*?)>");
        public static Regex Regex = new Regex(@"<.*?/(\@.*?)>");
        public NPCInfo Info;
        private const long TurnDelay = 10000;
        public long TurnTime;

        public List<ItemInfo> Goods = new List<ItemInfo>();
        public List<int> GoodsIndex = new List<int>();
        public List<ItemType> Types = new List<ItemType>();
        public List<NPCPage> NPCSections = new List<NPCPage>();

        public override string Name
        {
            get { return Info.Name; }
            set { throw new NotSupportedException(); }
        }

        public override int CurrentMapIndex { get; set; }

        public override Point CurrentLocation
        {
            get { return Info.Location; }
            set { throw new NotSupportedException(); }
        }

        public override MirDirection Direction { get; set; }

        public override byte Level
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override uint Health
        {
            get { throw new NotSupportedException(); }
        }

        public override uint MaxHealth
        {
            get { throw new NotSupportedException(); }
        }


        public NPCObject(NPCInfo info)
        {
            Info = info;
            NameColour = Color.Lime;

            Direction = (MirDirection) Envir.Random.Next(3);
            TurnTime = Envir.Time + Envir.Random.Next(100);

            Spawned();

            if (!Directory.Exists(Settings.NPCPath)) return;

            string fileName = Path.Combine(Settings.NPCPath, info.FileName + ".txt");
            if (File.Exists(fileName))
                ParseScript(File.ReadAllLines(fileName));
            else
                SMain.Enqueue(string.Format("File Not Found: {0}, NPC: {1}", info.FileName, info.Name));
        }

        private void ParseScript(IList<string> lines)
        {
            List<string> buttons = ParseSection(lines, MainKey);

            for (int i = 0; i < buttons.Count; i++)
            {
                string section = buttons[i].ToUpper();

                bool match = false;
                for (int a = 0; a < NPCSections.Count; a++)
                {
                    if (NPCSections[a].Key != section) continue;
                    match = true;
                    break;

                }

                if (match) continue;

                buttons.AddRange(ParseSection(lines, section));
            }

            ParseGoods(lines);
            ParseTypes(lines);

            for (int i = 0; i < Goods.Count; i++)
                GoodsIndex.Add(Goods[i].Index);
        }

        private List<string> ParseSection(IEnumerable<string> scriptLines, string sectionName)
        {
            List<string>
                checks = new List<string>(),
                acts = new List<string>(),
                say = new List<string>(),
                buttons = new List<string>(),
                elseSay = new List<string>(),
                elseActs = new List<string>(),
                elseButtons = new List<string>(),
                gotoButtons = new List<string>();

            List<string> lines = scriptLines.ToList();
            List<string> currentSay = say, currentButtons = buttons;

            //Used to fake page name
            string tempSectionName = SectionArgumentParse(sectionName).ToUpper();

            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].ToUpper().StartsWith(tempSectionName)) continue;

                for (int x = i + 1; x < lines.Count; x++)
                {
                    if (string.IsNullOrEmpty(lines[x])) continue;

                    if (lines[x].StartsWith("#"))
                    {
                        string[] action = lines[x].Remove(0, 1).ToUpper().Trim().Split(' ');
                        switch (action[0])
                        {
                            case "IF":
                                currentSay = checks;
                                currentButtons = null;
                                continue;
                            case "SAY":
                                currentSay = say;
                                currentButtons = buttons;
                                continue;
                            case "ACT":
                                currentSay = acts;
                                currentButtons = gotoButtons;
                                continue;
                            case "ELSESAY":
                                currentSay = elseSay;
                                currentButtons = elseButtons;
                                continue;
                            case "ELSEACT":
                                currentSay = elseActs;
                                currentButtons = gotoButtons;
                                continue;
                            case "INCLUDE":
                                lines.InsertRange(x + 1, ParseInclude(lines[x]));
                                continue;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    if (lines[x].StartsWith("[") && lines[x].EndsWith("]")) break;

                    if (currentButtons != null)
                    {
                        Match match = Regex.Match(lines[x]);
                        while (match.Success)
                        {
                            currentButtons.Add(string.Format("[{0}]", match.Groups[1].Captures[0].Value));//ToUpper()
                            match = match.NextMatch();
                        }

                        //Check if line has a goto command
                        var parts = lines[x].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Count() > 1)
                        switch (parts[0].ToUpper())
                        {
                            case "GOTO":
                                gotoButtons.Add(string.Format("[{0}]", parts[1].ToUpper()));
                                break;
                            case "TIMERECALLPAGE":
                                gotoButtons.Add(string.Format("[{0}]", parts[1].ToUpper()));
                                break;
                            case "TIMERECALLGROUPPAGE":
                                gotoButtons.Add(string.Format("[{0}]", parts[1].ToUpper()));
                                break;
                            case "DELAYGOTO":
                                gotoButtons.Add(string.Format("[{0}]", parts[2].ToUpper()));
                                break;
                            case "LISTEN":
                                gotoButtons.Add(string.Format("[{0}]", parts[2].ToUpper()));
                                break;
                        }
                    }

                    currentSay.Add(lines[x].TrimEnd());
                }

                break;
            }


            NPCPage page = new NPCPage(sectionName.ToUpper(), Info.Name, say, buttons, elseSay, elseButtons, gotoButtons);

            for (int i = 0; i < checks.Count; i++)
                page.ParseCheck(checks[i]);

            for (int i = 0; i < acts.Count; i++)
                page.ParseAct(page.ActList, acts[i]);

            for (int i = 0; i < elseActs.Count; i++)
                page.ParseAct(page.ElseActList, elseActs[i]);

            NPCSections.Add(page);
            currentButtons = new List<string>();
            currentButtons.AddRange(buttons);
            currentButtons.AddRange(elseButtons);
            currentButtons.AddRange(gotoButtons);

            return currentButtons;
        }

        public static List<string> Args = new List<string>();

        public string SectionArgumentParse(string key)
        {
            Regex r = new Regex(@"\((.*?)\)");

            Match match = r.Match(key);
            if (!match.Success) return key;

            key = Regex.Replace(key, r.ToString(), "()");

            string strValues = match.Groups[1].Value;
            string[] arrValues = strValues.Split(',');

            Args = new List<string>();

            foreach (var t in arrValues)
                Args.Add(t);

            return key;
        }

        

        private IEnumerable<string> ParseInclude(string line)
        {
            string[] split = line.Split(' ');

            string path = Path.Combine(Settings.EnvirPath, split[1].Substring(1, split[1].Length - 2));
            string page = ("[" + split[2] + "]").ToUpper();

            bool start = false, finish = false;

            var parsedLines = new List<string>();

            if (!File.Exists(path)) return parsedLines;
            IList<string> lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].ToUpper().StartsWith(page)) continue;

                for (int x = i + 1; x < lines.Count; x++)
                {
                    if (lines[x].Trim() == ("{"))
                    {
                        start = true;
                        continue;
                    }

                    if (lines[x].Trim() == ("}"))
                    {
                        finish = true;
                        break;
                    }

                    parsedLines.Add(lines[x]);
                }
            }

            if (start && finish)
                return parsedLines;

            return new List<string>();
        } 

        private void ParseTypes(IList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].ToUpper().StartsWith(TypeKey)) continue;

                while (++i < lines.Count)
                {
                    if (String.IsNullOrEmpty(lines[i])) continue;

                    int index;
                    if (!int.TryParse(lines[i], out index)) return;
                    Types.Add((ItemType) index);
                }
            }
        }

        private void ParseGoods(IList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (!lines[i].ToUpper().StartsWith(TradeKey)) continue;

                while (++i < lines.Count)
                {
                    if (String.IsNullOrEmpty(lines[i])) continue;

                    ItemInfo info = SMain.Envir.GetItemInfo(lines[i]);
                    if (info == null || Goods.Contains(info))
                    {
                        SMain.Enqueue(string.Format("Could not find Item: {0}, File: {1}", lines[i], Info.FileName));
                        continue;
                    }

                    Goods.Add(info);
                }
            }
        }

        public override void Process(DelayedAction action)
        {
            throw new NotSupportedException();
        }

        public override bool IsAttackTarget(PlayerObject attacker)
        {
            throw new NotSupportedException();
        }
        public override bool IsFriendlyTarget(PlayerObject ally)
        {
            throw new NotSupportedException();
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            throw new NotSupportedException();
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            throw new NotSupportedException();
        }

        public override int Attacked(PlayerObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            throw new NotSupportedException();
        }

        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            throw new NotSupportedException();
        }

        public override void SendHealth(PlayerObject player)
        {
            throw new NotSupportedException();
        }

        public override void Die()
        {
            throw new NotSupportedException();
        }

        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            throw new NotSupportedException();
        }

        public override void ReceiveChat(string text, ChatType type)
        {
            throw new NotSupportedException();
        }


        public override void Process()
        {
            base.Process();

            if (Envir.Time < TurnTime) return;

            TurnTime = Envir.Time + TurnDelay;
            Turn((MirDirection) Envir.Random.Next(3));
        }

        public override void SetOperateTime()
        {
            long time = Envir.Time + 2000;

            if (TurnTime < time && TurnTime > Envir.Time)
                time = TurnTime;

            if (OwnerTime < time && OwnerTime > Envir.Time)
                time = OwnerTime;

            if (ExpireTime < time && ExpireTime > Envir.Time)
                time = ExpireTime;

            if (PKPointTime < time && PKPointTime > Envir.Time)
                time = PKPointTime;

            if (LastHitTime < time && LastHitTime > Envir.Time)
                time = LastHitTime;

            if (EXPOwnerTime < time && EXPOwnerTime > Envir.Time)
                time = EXPOwnerTime;

            if (BrownTime < time && BrownTime > Envir.Time)
                time = BrownTime;

            for (int i = 0; i < ActionList.Count; i++)
            {
                if (ActionList[i].Time >= time && ActionList[i].Time > Envir.Time) continue;
                time = ActionList[i].Time;
            }

            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].TickTime >= time && PoisonList[i].TickTime > Envir.Time) continue;
                time = PoisonList[i].TickTime;
            }

            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].ExpireTime >= time && Buffs[i].ExpireTime > Envir.Time) continue;
                time = Buffs[i].ExpireTime;
            }


            if (OperateTime <= Envir.Time || time < OperateTime)
                OperateTime = time;
        }

        public void Turn(MirDirection dir)
        {
            Direction = dir;

            Broadcast(new S.ObjectTurn {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});
        }

        public override Packet GetInfo()
        {
            return new S.ObjectNPC
                {
                    ObjectID = ObjectID,
                    Name = Name,
                    NameColour = NameColour,
                    Image = Info.Image,
                    Location = CurrentLocation,
                    Direction = Direction,
                };
        }

        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false)
        {
            throw new NotSupportedException();
        }

        public override void AddBuff(Buff b)
        {
            throw new NotSupportedException();
        }

        public void Call(PlayerObject player, string key)
        {
            bool found = false;

            key = key.ToUpper();
            if (key != MainKey)
            {
                if (player.NPCID != ObjectID) return;

                if (!player.NPCGoto)
                {
                    if (player.NPCSuccess)
                    {
                       if (!player.NPCPage.Buttons.Any(c => c.ToUpper().Contains(key))) return;
                    }
                    else
                    {
                        if (!player.NPCPage.ElseButtons.Any(c => c.ToUpper().Contains(key))) return;
                    }
                }

                player.NPCGoto = false;
            }

            for (int i = 0; i < NPCSections.Count; i++)
            {

                NPCPage page = NPCSections[i];
                if (!String.Equals(page.Key, key, StringComparison.CurrentCultureIgnoreCase)) continue;
                ProcessPage(player, page);
            }
        }

        public void Buy(PlayerObject player, int index, uint count)
        {
            ItemInfo info = null;

            for (int i = 0; i < Goods.Count; i++)
            {
                if (Goods[i].Index != index) continue;
                info = Goods[i];
                break;
            }

            if (count == 0 || info == null || count > info.StackSize) return;

            uint cost = info.Price*count;
            cost = (uint) (cost*Info.PriceRate);

            if (cost > player.Account.Gold) return;

            UserItem item = Envir.CreateFreshItem(info);
            item.Count = count;

            if (!player.CanGainItem(item)) return;

            player.Account.Gold -= cost;
            player.Enqueue(new S.LoseGold {Gold = cost});
            player.GainItem(item);
        }
        public void Sell(UserItem item)
        {
            /* Handle Item Sale */


        }

        private void ProcessPage(PlayerObject player, NPCPage page)
        {
            player.NPCID = ObjectID;
            player.NPCSuccess = page.Check(player);
            player.NPCPage = page;

            switch (page.Key.ToUpper())
            {
                case BuyKey:
                    for (int i = 0; i < Goods.Count; i++)
                        player.CheckItemInfo(Goods[i]);

                    player.Enqueue(new S.NPCGoods {List = GoodsIndex, Rate = Info.PriceRate});
                    break;
                case SellKey:
                    player.Enqueue(new S.NPCSell());
                    break;
                case RepairKey:
                    player.Enqueue(new S.NPCRepair { Rate = Info.PriceRate });
                    break;
                case SRepairKey:
                    player.Enqueue(new S.NPCSRepair { Rate = Info.PriceRate });
                    break;
                case StorageKey:
                    player.SendStorage();
                    player.Enqueue(new S.NPCStorage());
                    break;
                case BuyBackKey:
                    break;
                case ConsignKey:
                    player.Enqueue(new S.NPCConsign());
                    break;
                case MarketKey:
                    player.UserMatch = false;
                    player.GetMarket(string.Empty, ItemType.Nothing);
                    break;
                case ConsignmentsKey:
                    player.UserMatch = true;
                    player.GetMarket(string.Empty, ItemType.Nothing);
                    break;
                case GuildCreateKey:
                    if (player.Info.Level < Settings.Guild_RequiredLevel)
                    {
                        player.ReceiveChat(String.Format("You have to be at least level {0} to create a guild.", Settings.Guild_RequiredLevel), ChatType.System);
                    }
                    if (player.MyGuild == null)
                    {
                        player.CanCreateGuild = true;
                        player.Enqueue(new S.GuildNameRequest());
                    }
                    else
                        player.ReceiveChat("You are already part of a guild.", ChatType.System);
                    break;
            }

        }
    }

    public class NPCPage
    {
        public readonly string Key;
        public List<NPCChecks> CheckList = new List<NPCChecks>();

        public List<NPCActions> ActList = new List<NPCActions>(), ElseActList = new List<NPCActions>();
        public List<string> Say, ElseSay, Buttons, ElseButtons, GotoButtons;

        public string Param1;
        public int Param1Instance, Param2, Param3;

        public string NPCName;

        public NPCPage(string key, string npcName, List<string> say, List<string> buttons, List<string> elseSay, List<string> elseButtons, List<string> gotoButtons)
        {
            Key = key;
            NPCName = npcName;

            Say = say;
            Buttons = buttons;

            ElseSay = elseSay;
            ElseButtons = elseButtons;

            GotoButtons = gotoButtons;
        }

        private bool _sayCommandFound;
        private string _sayCommandValue;
        public string SayCommandCheck
        {
            get { return _sayCommandValue; }
            set { _sayCommandValue = value; _sayCommandFound = true; }
        }

        public string[] ParseArguments(string[] words)
        {
            Regex r = new Regex(@"\%ARG\((\d+)\)$");

            for (int i = 0; i < words.Length; i++)
            {
                Match match = r.Match(words[i].ToUpper());

                if (!match.Success) continue;

                int sequence = Convert.ToInt32(match.Groups[1].Value);

                if (NPCObject.Args.Count >= (sequence + 1)) words[i] = NPCObject.Args[sequence];
            }

            return words;
        }

        public void AddVariable(PlayerObject player, string key, string value)
        {
            Regex regex = new Regex(@"[A-Za-z][0-9]");

            if (!regex.Match(key).Success) return;

            for (int i = 0; i < player.NPCVar.Count; i++)
            {
                if (!String.Equals(player.NPCVar[i].Key, key, StringComparison.CurrentCultureIgnoreCase)) continue;
                player.NPCVar[i] = new KeyValuePair<string, string>(player.NPCVar[i].Key, value);
                return;
            }

            player.NPCVar.Add(new KeyValuePair<string, string>(key, value));
        }

        public string FindVariable(PlayerObject player, string key)
        {
            Regex regex = new Regex(@"\%[A-Za-z][0-9]");

            if (!regex.Match(key).Success) return key;

            string tempKey = key.Substring(1);

            foreach (KeyValuePair<string, string> t in player.NPCVar)
            {
                if (String.Equals(t.Key, tempKey, StringComparison.CurrentCultureIgnoreCase)) return t.Value;
            }

            return key;
        }

        public void ParseCheck(string line)
        {
            var parts = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            parts = ParseArguments(parts);

            if (parts.Length == 0) return;

            string tempString, tempString2;

            var regexFlag = new Regex(@"\[(.*?)\]");

            switch (parts[0].ToUpper())
            {
                case "LEVEL":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.Level, parts[1], parts[2]));
                    break;

                case "CHECKGOLD":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckGold, parts[1], parts[2]));
                    break;

                case "CHECKITEM":
                    if (parts.Length < 2) return;

                    tempString = parts.Length < 3 ? "1" : parts[2];
                    tempString2 = parts.Length > 3 ? parts[3] : "";

                    CheckList.Add(new NPCChecks(CheckType.CheckItem, parts[1], tempString, tempString2));
                    break;

                case "CHECKGENDER":
                    if (parts.Length < 2) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckGender, parts[1]));
                    break;

                case "CHECKCLASS":
                    if (parts.Length < 2) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckClass, parts[1]));
                    break;

                case "DAYOFWEEK":
                    if (parts.Length < 2) return;
                    CheckList.Add(new NPCChecks(CheckType.CheckDay, parts[1]));
                    break;

                case "HOUR":
                    if (parts.Length < 2) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckHour, parts[1]));
                    break;

                case "MIN":
                    if (parts.Length < 2) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckMinute, parts[1]));
                    break;

                //cant use stored var
                case "CHECKNAMELIST":
                    if (parts.Length < 2) return;

                    var fileName = Path.Combine(Settings.NameListPath, parts[1] + ".txt");
                    
                    CheckList.Add(new NPCChecks(CheckType.CheckNameList, fileName));
                    break;

                case "ISADMIN":
                    CheckList.Add(new NPCChecks(CheckType.IsAdmin));
                    break;

                case "CHECKPKPOINT":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckPkPoint, parts[1], parts[2]));
                    break;

                case "CHECKRANGE":
                    if (parts.Length < 4) return;

                    CheckList.Add(new NPCChecks(CheckType.CheckRange, parts[1], parts[2], parts[3]));
                    break;

                //cant use stored var
                case "CHECK":
                    if (parts.Length < 3) return;
                    var match = regexFlag.Match(parts[1]);
                    if (match.Success)
                    {
                        string flagIndex = match.Groups[1].Captures[0].Value;
                        CheckList.Add(new NPCChecks(CheckType.Check, flagIndex, parts[2]));
                    }
                    break;

                case "CHECKHUM":
                    if (parts.Length < 3) return;

                    tempString = parts.Length < 4 ? "1" : parts[3];
                    CheckList.Add(new NPCChecks(CheckType.CheckHum, parts[1], parts[2], tempString));
                    break;

                case "CHECKMON":
                    if (parts.Length < 3) return;

                    tempString = parts.Length < 4 ? "1" : parts[3];
                    CheckList.Add(new NPCChecks(CheckType.CheckMon, parts[1], parts[2], tempString));
                    break;

                case "RANDOM":
                    if (parts.Length < 2) return;

                    CheckList.Add(new NPCChecks(CheckType.Random, parts[1]));
                    break;

                case "GROUPLEADER":
                    CheckList.Add(new NPCChecks(CheckType.Groupleader));
                    break;

                case "GROUPCOUNT":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.GroupCount, parts[1], parts[2]));
                    break;

                case "PETCOUNT":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.PetCount, parts[1], parts[2]));
                    break;

                case "PETLEVEL":
                    if (parts.Length < 3) return;

                    CheckList.Add(new NPCChecks(CheckType.PetLevel, parts[1], parts[2]));
                    break;

                case "CHECKCALC":
                    if (parts.Length < 4) return;
                    CheckList.Add(new NPCChecks(CheckType.CheckCalc, parts[1], parts[2], parts[3]));
                    break;
            }

        }
        public void ParseAct(List<NPCActions> acts, string line)
        {
            var parts = line.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            parts = ParseArguments(parts);

            if (parts.Length == 0) return;

            string fileName;
            var regexMessage = new Regex("\"([^\"]*)\"");
            var regexFlag = new Regex(@"\[(.*?)\]");

            switch (parts[0].ToUpper())
            {
                case "MOVE":
                    if (parts.Length < 2) return;

                    string tempx = parts.Length > 3 ? parts[2] : "0";
                    string tempy = parts.Length > 3 ? parts[3] : "0";

                    acts.Add(new NPCActions(ActionType.Teleport, parts[1], tempx, tempy));
                    break;

                case "INSTANCEMOVE":
                    if (parts.Length < 5) return;

                    acts.Add(new NPCActions(ActionType.InstanceTeleport, parts[1], parts[2], parts[3], parts[4]));
                    break;

                case "GIVEGOLD":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.GiveGold, parts[1]));
                    break;

                case "TAKEGOLD":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.TakeGold, parts[1]));
                    break;

                case "GIVEITEM":
                    if (parts.Length < 2) return;

                    string count = parts.Length < 3 ? string.Empty : parts[2];
                    acts.Add(new NPCActions(ActionType.GiveItem, parts[1], count));
                    break;

                case "TAKEITEM":
                    if (parts.Length < 3) return;

                    count = parts.Length < 3 ? string.Empty : parts[2];
                    string dura = parts.Length > 3 ? parts[3] : "";

                    acts.Add(new NPCActions(ActionType.TakeItem, parts[1], count, dura));
                    break;

                case "GIVEEXP":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.GiveExp, parts[1]));
                    break;

                case "GIVEPET":
                    if (parts.Length < 2) return;

                    string petcount = parts.Length > 2 ? parts[2] : "1";
                    string petlevel = parts.Length > 2 ? parts[2] : "0";

                    acts.Add(new NPCActions(ActionType.GivePet, parts[1], petcount, petlevel));
                    break;

                case "GOTO":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.Goto, parts[1]));
                    break;

                //cant use stored var
                case "ADDNAMELIST":
                    if (parts.Length < 2) return;

                    fileName = Path.Combine(Settings.NameListPath, parts[1] + ".txt");
                    if (!File.Exists(fileName))
                        File.Create(fileName);

                        acts.Add(new NPCActions(ActionType.AddNameList, fileName));
                    break;

                //cant use stored var
                case "DELNAMELIST":
                    if (parts.Length < 2) return;

                    fileName = Path.Combine(Settings.NameListPath, parts[1] + ".txt");
                    if (File.Exists(fileName))
                        acts.Add(new NPCActions(ActionType.DelNameList, fileName));
                    break;

                //cant use stored var
                case "CLEARNAMELIST":
                    if (parts.Length < 2) return;

                    fileName = Path.Combine(Settings.NameListPath, parts[1] + ".txt");
                    if (File.Exists(fileName))
                        acts.Add(new NPCActions(ActionType.ClearNameList, fileName));
                    break;

                case "GIVEHP":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.GiveHP, parts[1]));
                    break;

                case "GIVEMP":
                    if (parts.Length < 2) return;
                    acts.Add(new NPCActions(ActionType.GiveMP, parts[1]));
                    break;

                case "CHANGELEVEL":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.ChangeLevel, parts[1]));
                    break;

                case "SETPKPOINT":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.SetPkPoint, parts[1]));
                    break;

                case "CHANGEGENDER":
                    acts.Add(new NPCActions(ActionType.ChangeGender));
                    break;

                case "CHANGECLASS":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.ChangeClass, parts[1]));
                    break;

                //cant use stored var
                case "LINEMESSAGE":
                    var match = regexMessage.Match(line);
                    if (match.Success)
                    {
                        var message = match.Groups[1].Captures[0].Value;

                        var last = parts.Count() - 1;
                        acts.Add(new NPCActions(ActionType.LineMessage, message, parts[last]));
                    }
                    break;

                case "GIVESKILL":
                    if (parts.Length < 3) return;

                    string spelllevel = parts.Length > 2 ? parts[2] : "0";
                    acts.Add(new NPCActions(ActionType.GiveSkill, parts[1], spelllevel));
                    break;

                //cant use stored var
                case "SET":
                    if (parts.Length < 3) return;
                    match = regexFlag.Match(parts[1]);
                    if (match.Success)
                    {
                        string flagIndex = match.Groups[1].Captures[0].Value;
                        acts.Add(new NPCActions(ActionType.Set, flagIndex, parts[2]));
                    }   
                    break;

                case "PARAM1":
                    if (parts.Length < 2) return;

                    string instanceId = parts.Length < 3 ? "1" : parts[2];
                    acts.Add(new NPCActions(ActionType.Param1, parts[1], instanceId));
                    break;

                case "PARAM2":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.Param2, parts[1]));
                    break;

                case "PARAM3":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.Param3, parts[1]));
                    break;

                case "MONGEN":
                    if (parts.Length < 2) return;

                    count = parts.Length < 3 ? "1" : parts[2];
                    acts.Add(new NPCActions(ActionType.Mongen, parts[1], count));
                    break;

                case "TIMERECALL":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.TimeRecall, parts[1]));
                    break;

                case "TIMERECALLGROUP":
                    if (parts.Length < 2) return;

                    acts.Add(new NPCActions(ActionType.TimeRecallGroup, parts[1]));
                    break;

                case "TIMERECALLPAGE":
                    if (parts.Length < 2) return;
                    string page = "[" + parts[1] + "]";
                    acts.Add(new NPCActions(ActionType.TimeRecallPage, page));
                    break;

                case "TIMERECALLGROUPPAGE":
                    if (parts.Length < 2) return;
                    page = "[" + parts[1] + "]";
                    acts.Add(new NPCActions(ActionType.TimeRecallGroupPage, page));
                    break;

                case "BREAKTIMERECALL":
                    acts.Add(new NPCActions(ActionType.BreakTimeRecall));
                    break;

                case "DELAYGOTO":
                    if (parts.Length < 3) return;

                    acts.Add(new NPCActions(ActionType.DelayGoto, parts[1], parts[2]));
                    break;

                case "MONCLEAR":
                    if (parts.Length < 2) return;

                    instanceId = parts.Length < 3 ? "1" : parts[2];
                    acts.Add(new NPCActions(ActionType.MonClear, parts[1], instanceId));
                    break;

                case "GROUPRECALL":
                    acts.Add(new NPCActions(ActionType.GroupRecall));
                    break;

                case "GROUPTELEPORT":
                    if (parts.Length < 2) return;
                    string x;
                    string y;

                    if (parts.Length == 4)
                    {
                        instanceId = "1";
                        x = parts[2];
                        y = parts[3];
                    }
                    else
                    {
                        instanceId = parts.Length < 3 ? "1" : parts[2];
                        x = parts.Length < 4 ? "0" : parts[3];
                        y = parts.Length < 5 ? "0" : parts[4];
                    }

                    acts.Add(new NPCActions(ActionType.GroupTeleport, parts[1], instanceId, x, y));
                    break;

                case "MOV":
                    if (parts.Length < 3) return;
                    match = Regex.Match(parts[1], @"[A-Z][0-9]", RegexOptions.IgnoreCase);
                    Match msgMatch = regexMessage.Match(line);

                    string valueToStore = parts[2];

                    if (msgMatch.Success)
                        valueToStore = msgMatch.Groups[1].Captures[0].Value;

                    if (match.Success)
                        acts.Add(new NPCActions(ActionType.Mov, parts[1], valueToStore));

                    break;
                case "CALC":
                    if (parts.Length < 4) return;

                    match = Regex.Match(parts[1], @"[A-Z][0-9]", RegexOptions.IgnoreCase);

                    msgMatch = regexMessage.Match(line);

                    valueToStore = parts[3];

                    if (msgMatch.Success)
                        valueToStore = msgMatch.Groups[1].Captures[0].Value;

                    if (match.Success)
                        acts.Add(new NPCActions(ActionType.Calc, "%" + parts[1], parts[2], valueToStore, parts[1].Insert(1, "-")));

                    break;

                case "LISTEN":
                    if (parts.Length < 3) return;

                    match = Regex.Match(parts[1], @"[A-Z][0-9]", RegexOptions.IgnoreCase);

                    if (match.Success)
                        acts.Add(new NPCActions(ActionType.Listen, parts[1], parts[2]));

                    break;
            }

        }
        public List<string> ParseSay(PlayerObject player, List<string> speech)
        {
            for (var i = 0; i < speech.Count; i++)
            {
                var parts = speech[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) continue;

                var regex = new Regex(@"\<\$(.*?)\>");
                var varRegex = new Regex(@"(.*?)\(([A-Z][0-9])\)");

                foreach (var part in parts)
                {
                    var match = regex.Match(part);

                    if (!match.Success) continue;
                    
                    string innerMatch = match.Groups[1].Captures[0].Value.ToUpper();  
 
                    Match varMatch = varRegex.Match(innerMatch);

                    if (varRegex.Match(innerMatch).Success)
                        innerMatch = innerMatch.Replace(varMatch.Groups[2].Captures[0].Value.ToUpper(), "");

                    switch (innerMatch)
                    {
                        case "OUTPUT()":
                            SayCommandCheck = FindVariable(player, "%" + varMatch.Groups[2].Captures[0].Value.ToUpper());
                            break;
                        case "NPCNAME":
                            SayCommandCheck = NPCName.Replace("_"," ");
                            break;
                        case "USERNAME":
                            SayCommandCheck = player.Name;
                            break;
                        case "LEVEL":
                            SayCommandCheck = player.Level.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "HP":
                            SayCommandCheck = player.HP.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "MAXHP":
                            SayCommandCheck = player.MaxHP.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "MP":
                            SayCommandCheck = player.MP.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "MAXMP":
                            SayCommandCheck = player.MaxMP.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "GAMEGOLD":
                            SayCommandCheck = player.Account.Gold.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "ARMOUR":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Armour] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Armour].Info.Name : "No Armour";
                            break;
                        case "WEAPON":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Weapon] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Weapon].Info.Name : "No Weapon";
                            break;
                        case "RING_L":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.RingL] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.RingL].Info.Name : "No Ring";
                            break;
                        case "RING_R":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.RingR] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.RingR].Info.Name : "No Ring";
                            break;
                        case "BRACELET_L":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.BraceletL] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.BraceletL].Info.Name : "No Bracelet";
                            break;
                        case "BRACELET_R":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.BraceletR] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.BraceletR].Info.Name : "No Bracelet";
                            break;
                        case "NECKLACE":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Necklace] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Necklace].Info.Name : "No Necklace";
                            break;
                        case "BELT":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Belt] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Belt].Info.Name : "No Belt";
                            break;
                        case "BOOTS":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Boots] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Boots].Info.Name : "No Boots";
                            break;
                        case "HELMET":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Helmet] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Helmet].Info.Name : "No Helmet";
                            break;
                        case "AMULET":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Amulet] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Amulet].Info.Name : "No Amulet";
                            break;
                        case "STONE":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Stone] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Stone].Info.Name : "No Stone";
                            break;
                        case "TORCH":
                            SayCommandCheck = player.Info.Equipment[(int)EquipmentSlot.Torch] != null ?
                                player.Info.Equipment[(int)EquipmentSlot.Torch].Info.Name : "No Torch";
                            break;

                        case "DATE":
                            SayCommandCheck = DateTime.Now.ToShortDateString();
                            break;
                        case "USERCOUNT":
                            SayCommandCheck = SMain.Envir.PlayerCount.ToString(CultureInfo.InvariantCulture);
                            break;
                        case "PKPOINT":
                            SayCommandCheck = player.PKPoints.ToString();
                            break;

                        default:
                            SayCommandCheck = string.Empty;
                            break;
                    }

                    if (!_sayCommandFound) continue;

                    _sayCommandFound = false;
                    speech[i] = speech[i].Replace(match.ToString(), _sayCommandValue);
                }
            }
            return speech;
        }

        public bool Check(PlayerObject player)
        {
            var failed = false;

            for(int i = 0 ; i < CheckList.Count ; i++)
            {
                NPCChecks check = CheckList[i];
                List<string> param = check.Params.Select(t => FindVariable(player, t)).ToList();

                byte tempByte;
                uint tempUint;
                int tempInt;
                int tempInt2;
                switch (check.Type)
                {
                    case CheckType.Level:
                        if (!byte.TryParse(param[1], out tempByte))
                        {
                            failed = true;
                            break;
                        }

                        try
                        {
                            failed = !Compare(param[0], player.Level, tempByte);
                        }
                        catch (ArgumentException)
                        {
                            SMain.Enqueue(string.Format("Incorrect operator: {0}, Page: {1}", param[0], Key));
                            return true;
                        }
                        break;

                    case CheckType.CheckGold:
                        if (!uint.TryParse(param[1], out tempUint))
                        {
                            failed = true;
                            break;
                        }

                        try
                        {
                            failed = !Compare(param[0], player.Account.Gold, tempUint);
                        }
                        catch (ArgumentException)
                        {
                            SMain.Enqueue(string.Format("Incorrect operator: {0}, Page: {1}", param[0], Key));
                            return true;
                        }
                        break;

                    case CheckType.CheckItem:
                        uint count;
                        ushort dura;

                        if (!uint.TryParse(param[1], out count))
                        {
                            failed = true;
                            break;
                        }

                        bool checkDura = ushort.TryParse(param[2], out dura);

                        var info = SMain.Envir.GetItemInfo(param[0]);

                        foreach (var item in player.Info.Inventory.Where(item => item != null && item.Info == info))
                        {
                            if(checkDura)
                                if (item.CurrentDura < dura * 1000) continue;

                            if (count > item.Count)
                            {
                                count -= item.Count;
                                continue;
                            }

                            if (count > item.Count) continue;
                            count = 0;
                            break;
                        }
                        if (count > 0)
                            failed = true;
                        break;

                    case CheckType.CheckGender:
                        if (!Enum.IsDefined(typeof(MirGender), param[0]))
                        {
                            failed = true;
                            break;
                        }

                        tempByte = (byte)Enum.Parse(typeof(MirGender), param[0]);

                        failed = (byte)player.Gender != tempByte;
                        break;

                    case CheckType.CheckClass:
                        if (!Enum.IsDefined(typeof(MirClass), param[0]))
                        {
                            failed = true;
                            break;
                        }

                        tempByte = (byte)Enum.Parse(typeof(MirClass), param[0]);

                        failed = (byte)player.Class != tempByte;
                        break;

                    case CheckType.CheckDay:
                        var day = DateTime.Now.DayOfWeek.ToString().ToUpper();
                        var dayToCheck = param[0].ToUpper();

                        failed = day != dayToCheck;
                        break;

                    case CheckType.CheckHour:
                        if (!uint.TryParse(param[0], out tempUint))
                        {
                            failed = true;
                            break;
                        }

                        var hour = DateTime.Now.Hour;
                        var hourToCheck = tempUint;

                        failed = hour != hourToCheck;
                        break;

                    case CheckType.CheckMinute:
                        if (!uint.TryParse(param[0], out tempUint))
                        {
                            failed = true;
                            break;
                        }

                        var minute = DateTime.Now.Minute;
                        var minuteToCheck = tempUint;

                        failed = minute != minuteToCheck;
                        break;

                    case CheckType.CheckNameList:
                        if (!File.Exists(param[0])) return true;

                        var read = File.ReadAllLines(param[0]);
                        failed = !read.Contains(player.Name);
                        break;

                    case CheckType.IsAdmin:
                        failed = !player.IsGM;
                        break;

                    case CheckType.CheckPkPoint:
                        if (!int.TryParse(param[1], out tempInt))
                        {
                            failed = true;
                            break;
                        }

                        try
                        {
                            failed = !Compare(param[0], player.PKPoints, tempInt);
                        }
                        catch (ArgumentException)
                        {
                            SMain.Enqueue(string.Format("Incorrect operator: {0}, Page: {1}", param[0], Key));
                            return true;
                        }
  
                        break;

                    case CheckType.CheckRange:
                        int x, y, range;
                        if (!int.TryParse(param[0], out x) || !int.TryParse(param[1], out y) || !int.TryParse(param[2], out range))
                        {
                            failed = true;
                            break;
                        }

                        var target = new Point {X = x, Y = y};

                        failed = !Functions.InRange(player.CurrentLocation, target, range);
                        break;

                    case CheckType.Check:
                        uint onCheck;

                        if (!uint.TryParse(param[0], out tempUint) || !uint.TryParse(param[1], out onCheck) || tempUint > Globals.FlagIndexCount)
                        {
                            failed = true;
                            break;
                        }

                        bool tempBool = Convert.ToBoolean(onCheck);

                        bool flag = player.Info.Flags[tempUint];

                        failed = flag != tempBool;
                        break;
                        
                    case CheckType.CheckHum:
                        if (!int.TryParse(param[0], out tempInt) || !int.TryParse(param[2], out tempInt2))
                        {
                            failed = true;
                            break;
                        }

                        var map = SMain.Envir.GetMapByNameAndInstance(param[1], tempInt2);
                        if (map == null)
                        {
                            failed = true;
                            break;
                        }

                        failed = map.Players.Count() < tempInt;
                        break;

                    case CheckType.CheckMon:
                        if (!int.TryParse(param[0], out tempInt) || !int.TryParse(param[2], out tempInt2))
                        {
                            failed = true;
                            break;
                        }

                        map = SMain.Envir.GetMapByNameAndInstance(param[1], tempInt2);
                        if (map == null)
                        {
                            failed = true;
                            break;
                        }

                        failed = map.MonsterCount < tempInt;
                        break;

                    case CheckType.Random:
                        if (!int.TryParse(param[0], out tempInt))
                        {
                            failed = true;
                            break;
                        }

                        failed = 0 != SMain.Envir.Random.Next(0, tempInt);
                        break;

                    case CheckType.Groupleader:
                        failed = (player.GroupMembers == null || player.GroupMembers[0] != player);
                        break;

                    case CheckType.GroupCount:
                        if (!int.TryParse(param[1], out tempInt))
                        {
                            failed = true;
                            break;
                        }

                        failed = (player.GroupMembers == null || !Compare(param[0], player.GroupMembers.Count, tempInt));
                        break;

                    case CheckType.PetCount:
                        if (!int.TryParse(param[1], out tempInt))
                        {
                            failed = true;
                            break;
                        }

                        failed = !Compare(param[0], player.Pets.Count(), tempInt);
                        break;

                    case CheckType.PetLevel:
                        if (!int.TryParse(param[1], out tempInt))
                        {
                            failed = true;
                            break;
                        }

                        for (int p = 0; p < player.Pets.Count(); p++)
                        {
                            failed = !Compare(param[0], player.Pets[p].PetLevel, tempInt);
                        }
                        break;

                    case CheckType.CheckCalc:
                        int left;
                        int right;

                        if (!int.TryParse(param[0], out left) || !int.TryParse(param[2], out right))
                        {
                            failed = true;
                            break;
                        }

                        try
                        {
                            failed = !Compare(param[1], left, right);
                        }
                        catch (ArgumentException)
                        {
                            SMain.Enqueue(string.Format("Incorrect operator: {0}, Page: {1}", param[1], Key));
                            return true;
                        }
                        break;
                }

                if (!failed) continue;

                Failed(player);
                return false;
            }

            Success(player);
            return true;

        }

        private void Act(IList<NPCActions> acts, PlayerObject player)
        {
            for (var i = 0; i < acts.Count; i++)
            {
                uint gold;
                uint count;
                string path;
                int x, y;
                int tempInt;
                byte tempByte;
                long tempLong;
                ItemInfo info;
                MonsterInfo monInfo;

                NPCActions act = acts[i];
                List<string> param = act.Params.Select(t => FindVariable(player, t)).ToList();

                switch (act.Type)
                {
                    case ActionType.Teleport:
                        Map map = SMain.Envir.GetMapByNameAndInstance(param[0]);
                        if (map == null) return;

                        if (!int.TryParse(param[1], out x)) return;
                        if (!int.TryParse(param[2], out y)) return;

                        var coords = new Point(x, y);

                        if (coords.X > 0 && coords.Y > 0) player.Teleport(map, coords);
                        else player.TeleportRandom(200, 0, map);
                        break;

                    case ActionType.InstanceTeleport:
                        int instanceId;
                        if (!int.TryParse(param[1], out instanceId)) return;
                        if (!int.TryParse(param[2], out x)) return;
                        if (!int.TryParse(param[3], out y)) return;

                        map = SMain.Envir.GetMapByNameAndInstance(param[0], instanceId);
                        if (map == null) return;
                        player.Teleport(map, new Point(x, y));
                        break;

                    case ActionType.GiveGold:
                        if (!uint.TryParse(param[0], out gold)) return;

                        if (gold + player.Account.Gold >= uint.MaxValue)
                            gold = uint.MaxValue - player.Account.Gold;

                        player.GainGold(gold);
                        break;

                    case ActionType.TakeGold:
                        if (!uint.TryParse(param[0], out gold)) return;

                        if (gold >= player.Account.Gold) gold = player.Account.Gold;

                        player.Account.Gold -= gold;
                        player.Enqueue(new S.LoseGold { Gold = gold });
                        break;

                    case ActionType.GiveItem:
                        if (param.Count < 2 || !uint.TryParse(param[1], out count)) count = 1;

                        info = SMain.Envir.GetItemInfo(param[0]);

                        if (info == null)
                        {
                            SMain.Enqueue(string.Format("Failed to get ItemInfo: {0}, Page: {1}", param[0], Key));
                            break;
                        }

                        while (count > 0)
                        {
                            UserItem item = SMain.Envir.CreateFreshItem(info);

                            if (item == null)
                            {
                                SMain.Enqueue(string.Format("Failed to create UserItem: {0}, Page: {1}", param[0], Key));
                                return;
                            }

                            if (item.Info.StackSize > count)
                            {
                                item.Count = count;
                                count = 0;
                            }
                            else
                            {
                                count -= item.Info.StackSize;
                                item.Count = item.Info.StackSize;
                            }

                            if (player.CanGainItem(item, false))
                                player.GainItem(item);
                        }
                        break;

                    case ActionType.TakeItem:
                        if (param.Count < 2 || !uint.TryParse(param[1], out count)) count = 1;
                        info = SMain.Envir.GetItemInfo(param[0]);

                        ushort dura;
                        bool checkDura = ushort.TryParse(param[2], out dura);

                        if (info == null)
                        {
                            SMain.Enqueue(string.Format("Failed to get ItemInfo: {0}, Page: {1}", param[0], Key));
                            break;
                        }

                        for (int o = 0; o < player.Info.Inventory.Length; o++)
                        {
                            UserItem item = player.Info.Inventory[o];
                            if (item == null) continue;
                            if (item.Info != info) continue;

                            if(checkDura)
                                if (item.CurrentDura < dura) continue;

                            if (count > item.Count)
                            {
                                player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                                player.Info.Inventory[o] = null;

                                count -= item.Count;
                                continue;
                            }

                            player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = count });
                            if (count == item.Count)
                                player.Info.Inventory[o] = null;
                            else
                                item.Count -= count;
                            break;
                        }
                        player.RefreshStats();
                        break;

                    case ActionType.GiveExp:
                        uint tempUint;
                        if (!uint.TryParse(param[0], out tempUint)) return;
                        player.GainExp(tempUint);
                        break;

                    case ActionType.GivePet:
                        byte petcount = 0;
                        byte petlevel = 0;

                        monInfo = SMain.Envir.GetMonsterInfo(param[0]);
                        if (monInfo == null) return;

                        if (param.Count > 1)
                            petcount = byte.TryParse(param[1], out petcount) ? Math.Min((byte)5, petcount) : (byte)1;

                        if (param.Count > 2)
                            petlevel = byte.TryParse(param[2], out petlevel) ? Math.Min((byte)7, petlevel) : (byte)0;

                        for (var c = 0; c < petcount; c++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(monInfo);
                            if (monster == null) return;
                            monster.PetLevel = petlevel;
                            monster.Master = player;
                            monster.MaxPetLevel = 7;
                            monster.Direction = player.Direction;
                            monster.ActionTime = SMain.Envir.Time + 1000;
                            monster.Spawn(player.CurrentMap, player.CurrentLocation);
                            player.Pets.Add(monster);
                        }
                        break;

                    case ActionType.AddNameList:
                        path = param[0];
                        if (File.ReadAllLines(path).All(t => player.Name != t))
                            {
                                using (var line = File.AppendText(path))
                                {
                                    line.WriteLine(player.Name);
                                }
                            }
                        break;

                    case ActionType.DelNameList:
                        path = param[0];
                        File.WriteAllLines(path, File.ReadLines(path).Where(l => l != player.Name).ToList());
                        break;

                    case ActionType.ClearNameList:
                        path = param[0];
                        File.WriteAllLines(path, new string[] { });
                        break;

                    case ActionType.GiveHP:
                        if (!int.TryParse(param[0], out tempInt)) return;
                        player.ChangeHP(tempInt);
                        break;

                    case ActionType.GiveMP:
                        if (!int.TryParse(param[0], out tempInt)) return;
                        player.ChangeMP(tempInt);
                        break;

                    case ActionType.ChangeLevel:
                        if (!byte.TryParse(param[0], out tempByte)) return;
                        tempByte = Math.Min(byte.MaxValue, tempByte);

                        player.Level = tempByte;
                        player.LevelUp();
                        break;

                    case ActionType.SetPkPoint:
                        if (!int.TryParse(param[0], out tempInt)) return;
                        player.PKPoints = tempInt;
                        break;

                    case ActionType.ChangeGender:
                        switch (player.Info.Gender)
                        {
                            case MirGender.Male:
                                player.Info.Gender = MirGender.Female;
                                break;
                            case MirGender.Female:
                                player.Info.Gender = MirGender.Male;
                                break;
                        }
                        break;

                    case ActionType.ChangeClass:
                        if (!Enum.IsDefined(typeof(MirClass), param[0])) return;
                        var data = (MirClass)((byte)Enum.Parse(typeof(MirClass), param[0]));

                        switch (data)
                        {
                            case MirClass.Warrior:
                                player.Info.Class = MirClass.Warrior;
                                break;
                            case MirClass.Taoist:
                                player.Info.Class = MirClass.Taoist;
                                break;
                            case MirClass.Wizard:
                                player.Info.Class = MirClass.Wizard;
                                break;
                            case MirClass.Assassin:
                                player.Info.Class = MirClass.Assassin;
                                break;
                        }
                        break;

                    case ActionType.LineMessage:
	                    if (!Enum.IsDefined(typeof(ChatType), param[1])) return;

                        ChatType chatType = (ChatType)((byte)Enum.Parse(typeof(ChatType), param[1]));

                        player.ReceiveChat(param[0], chatType);
                        break;

                    case ActionType.GiveSkill:
                        byte spellLevel = 0;
                        if (!Enum.IsDefined(typeof(Spell), param[0])) return;

                        if (param.Count > 1)
                            spellLevel = byte.TryParse(param[1], out spellLevel) ? Math.Min((byte)3, spellLevel) : (byte)0;

                        var magic = new UserMagic((Spell)(byte)Enum.Parse(typeof(Spell), param[0])) { Level = spellLevel };

                        player.Info.Magics.Add(magic);
                        player.Enqueue(magic.GetInfo());
                        break;

                    case ActionType.Goto:
                        player.NPCGoto = true;
                        player.NPCGotoPage = "[" + param[0] + "]";
                        break;

                    case ActionType.Set:
                        uint flagIndex;
                        uint onCheck;
                        if (!uint.TryParse(param[0], out flagIndex)) return;
                        if (!uint.TryParse(param[1], out onCheck)) return;

                        if (flagIndex > Globals.FlagIndexCount) return;
                        var flagIsOn = Convert.ToBoolean(onCheck);

                        player.Info.Flags[flagIndex] = flagIsOn;
                        break;

                    case ActionType.Param1:
                        if (!int.TryParse(param[1], out tempInt)) return;

                        Param1 = param[0];
                        Param1Instance = tempInt;
                        break;

                    case ActionType.Param2:
                        if (!int.TryParse(param[0], out tempInt)) return;

                        Param2 = tempInt;
                        break;

                    case ActionType.Param3:
                        if (!int.TryParse(param[0], out tempInt)) return;

                        Param3 = tempInt;
                        break;

                    case ActionType.Mongen:
                        if (Param1 == null || Param2 == 0 || Param3 == 0) return;
                        if (!byte.TryParse(param[1], out tempByte)) return;

                        map = SMain.Envir.GetMapByNameAndInstance(Param1, Param1Instance);
                        if (map == null) return;

                        monInfo = SMain.Envir.GetMonsterInfo(param[0]);
                        if (monInfo == null) return;

                        for (var c = 0; c < tempByte; c++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(monInfo);
                            if (monster == null) return;
                            monster.Direction = 0;
                            monster.ActionTime = SMain.Envir.Time + 1000;
                            monster.Spawn(map, new Point(Param2, Param3));
                        }
                        break;

                    case ActionType.TimeRecall:
                        if (!long.TryParse(param[0], out tempLong)) return;

                        player.NPCJumpPage = new NPCJumpPage
                        {
                            PlayerMap = player.CurrentMap,
                            PlayerCoords = player.CurrentLocation,
                            TimePeriod = tempLong
                        };
                        break;

                    case ActionType.TimeRecallGroup:
                        if (player.GroupMembers == null) return;
                        if (!long.TryParse(param[0], out tempLong)) return;

                        for (i = 0; i < player.GroupMembers.Count(); i++)
                        {
                            player.GroupMembers[i].NPCJumpPage = new NPCJumpPage
                            {
                                PlayerMap = player.CurrentMap,
                                PlayerCoords = player.CurrentLocation,
                                TimePeriod = tempLong
                            };
                        }
                        break;

                    case ActionType.TimeRecallPage:
                        if (player.NPCJumpPage == null) return;

                        player.NPCJumpPage.NPCID = player.NPCID;
                        player.NPCJumpPage.NPCGotoPage = param[0];
                        break;

                    case ActionType.TimeRecallGroupPage:
                        if (player.NPCJumpPage == null) return;
                        if (player.GroupMembers == null) return;
                        for (i = 0; i < player.GroupMembers.Count(); i++)
                        {
                            if (player.GroupMembers[i].NPCJumpPage == null) continue;
                            player.GroupMembers[i].NPCJumpPage.NPCID = player.NPCID;
                            player.GroupMembers[i].NPCJumpPage.NPCGotoPage = param[0];
                        }
                        break;

                    case ActionType.BreakTimeRecall:
                        if (player.NPCJumpPage == null) return;
                        player.NPCJumpPage = null;
                        break;

                    case ActionType.DelayGoto:
                        if (!long.TryParse(param[0], out tempLong)) return;

                        player.NPCJumpPage = new NPCJumpPage
                        {
                            NPCID = player.NPCID,
                            TimePeriod = tempLong,
                            NPCGotoPage = "[" + param[1] + "]"
                        };
                        break;

                    case ActionType.MonClear:
                        if (!int.TryParse(param[1], out tempInt)) return;

                        map = SMain.Envir.GetMapByNameAndInstance(param[0], tempInt);
                        if (map == null) return;

                        foreach (var cell in map.Cells)
                        {
                            if (cell == null || cell.Objects == null) continue;

                            for (i = 0 ; i < cell.Objects.Count() ; i++)
                            {
                                MapObject ob = cell.Objects[i];

                                if (ob.Race != ObjectType.Monster) continue;
                                if (ob.Dead) continue;
                                ob.Die();
                            }
                        }
                        break;
                    case ActionType.GroupRecall:
                        if (player.GroupMembers == null) return;

                        for (i = 0; i < player.GroupMembers.Count(); i++)
                        {
                            player.GroupMembers[i].Teleport(player.CurrentMap, player.CurrentLocation);
                        }
                        break;

                    case ActionType.GroupTeleport:
                        if (player.GroupMembers == null) return;
                        if (!int.TryParse(param[1], out tempInt)) return;
                        if (!int.TryParse(param[2], out x)) return;
                        if (!int.TryParse(param[3], out y)) return;

                        map = SMain.Envir.GetMapByNameAndInstance(param[0], tempInt);
                        if (map == null) return;

                        for (i = 0; i < player.GroupMembers.Count(); i++)
                        {
                            if (x == 0 || y == 0)
                            {
                                player.GroupMembers[i].TeleportRandom(200, 0, map);
                            }
                            else
                            {
                                player.GroupMembers[i].Teleport(map, new Point(x, y));
                            }
                        }
                        break;

                    case ActionType.Mov:
                        string value = param[0];
                        AddVariable(player, value, param[1]);
                        break;

                    case ActionType.Calc:
                        int left;
                        int right;

                        bool resultLeft = int.TryParse(param[0], out left);
                        bool resultRight = int.TryParse(param[2], out right);

                        if (resultLeft && resultRight)
                        {
                            try
                            {
                                int result = Calculate(param[1], left, right);
                                AddVariable(player, param[3].Replace("-", ""), result.ToString());
                            }
                            catch (ArgumentException)
                            {
                                SMain.Enqueue(string.Format("Incorrect operator: {0}, Page: {1}", param[1], Key));
                            }
                        }
                        else
                        {
                            AddVariable(player, param[3].Replace("-", ""), param[0] + param[2]);
                        }
                        break;

                    case ActionType.Listen:
                        player.NPCListener = new NPCListener
                        {
                            Active = true,
                            NPCGotoPage = param[1],
                            NPCVariable = param[0],
                            NPCID = player.NPCID
                        };
                        break;
                }
            }
        }

        private void Success(PlayerObject player)
        {
            Act(ActList, player);

            var parseSay = new List<String>(Say);
            parseSay = ParseSay(player, parseSay);

            player.Enqueue(new S.NPCResponse { Page = parseSay });
        }
        private void Failed(PlayerObject player)
        {
            Act(ElseActList, player);

            var parseElseSay = new List<String>(ElseSay);
            parseElseSay = ParseSay(player, parseElseSay);

            player.Enqueue(new S.NPCResponse { Page = parseElseSay });
        }

        public static bool Compare<T>(string op, T left, T right) where T : IComparable<T>
        {
            switch (op)
            {
                case "<": return left.CompareTo(right) < 0;
                case ">": return left.CompareTo(right) > 0;
                case "<=": return left.CompareTo(right) <= 0;
                case ">=": return left.CompareTo(right) >= 0;
                case "==": return left.Equals(right);
                case "!=": return !left.Equals(right);
                default: throw new ArgumentException("Invalid comparison operator: {0}", op);
            }
        }

        public static int Calculate(string op, int left, int right)
        {
            switch (op)
            {
                case "+": return left + right;
                case "-": return left - right;
                case "*": return left * right;
                case "/": return left / right;
                default: throw new ArgumentException("Invalid sum operator: {0}", op);
            }
        }
    }

    public class NPCChecks
    {
        public CheckType Type;
        public List<string> Params = new List<string>();

        public NPCChecks(CheckType check, params string[] p)
        {
            Type = check;

            for (int i = 0; i < p.Length; i++)
                Params.Add(p[i]);
        }
    }
    public class NPCActions
    {
        public ActionType Type;
        public List<string> Params = new List<string>();

        public NPCActions(ActionType action, params string[] p)
        {
            Type = action;

            Params.AddRange(p);
        }
    }

    public enum ActionType
    {
        Teleport,
        InstanceTeleport,
        GiveGold,
        TakeGold,
        GiveItem,
        TakeItem,
        GiveExp,
        GivePet,
        AddNameList,
        DelNameList,
        ClearNameList,
        GiveHP,
        GiveMP,
        ChangeLevel,
        SetPkPoint,
        ChangeGender,
        ChangeClass,
        LineMessage,
        Goto,
        GiveSkill,
        Set,
        Param1,
        Param2,
        Param3,
        Mongen,
        TimeRecall,
        TimeRecallGroup,
        TimeRecallPage,
        TimeRecallGroupPage,
        BreakTimeRecall,
        MonClear,
        GroupRecall,
        GroupTeleport,
        DelayGoto,
        Mov,
        Calc,
        Listen,
    }
    public enum CheckType
    {
        IsAdmin,
        Level,
        CheckItem,
        CheckGold,
        CheckGender,
        CheckClass,
        CheckDay,
        CheckHour,
        CheckMinute,
        CheckNameList,
        CheckPkPoint,
        CheckRange,
        Check,
        CheckHum,
        CheckMon,
        Random,
        Groupleader,
        GroupCount,
        PetLevel,
        PetCount,
        CheckCalc,
    }

    public class NPCJumpPage
    {
        public Map PlayerMap;
        public Point PlayerCoords;
        public uint NPCID;
        public string NPCGotoPage;
        public string NPCPage;
        public bool Active;
        public bool Interrupted;

        private long _timePeriod;
        public long TimePeriod
        {
            get
            {
                return _timePeriod;
            }
            set
            {
                _timePeriod = SMain.Envir.Time + (value * 1000);
                Active = true;
            }
        }
    }

    public class NPCListener
    {
        public uint NPCID;
        public string NPCGotoPage;
        public bool Active;
        public string NPCVariable;
    }
}