﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;

namespace Server.MirObjects
{
    public sealed class PlayerObject : MapObject
    {
        public string GMPassword = Settings.GMPassword;
        public bool IsGM, GMLogin, GMNeverDie, GMGameMaster, EnableGroupRecall, EnableGuildInvite;

        public bool HasUpdatedBaseStats = true;

        public long LastRecallTime, LastRevivalTime, LastTeleportTime, LastProbeTime;

        public sbyte Looks_Armour = 0, Looks_Weapon = -1;
        public byte Looks_Wings = 0;

        public override ObjectType Race
        {
            get { return ObjectType.Player; }
        }

        public CharacterInfo Info;
        public AccountInfo Account;
        public MirConnection Connection;
        
        public override string Name
        {
            get { return Info.Name; }
            set { /*Check if Name exists.*/ }
        }

        public override int CurrentMapIndex
        {
            get { return Info.CurrentMapIndex; }
            set { Info.CurrentMapIndex = value; }
        }
        public override Point CurrentLocation
        {
            get { return Info.CurrentLocation; }
            set { Info.CurrentLocation = value; }
        }
        public override MirDirection Direction
        {
            get { return Info.Direction; }
            set { Info.Direction = value; }
        }
        public override byte Level
        {
            get { return Info.Level; }
            set { Info.Level = value; }
        }

        public override uint Health
        {
            get { return HP; }
        }
        public override uint MaxHealth
        {
            get { return MaxHP; }
        }

        public ushort HP
        {
            get { return Info.HP; }
            set { Info.HP = value; }
        }
        public ushort MP
        {
            get { return Info.MP; }
            set { Info.MP = value; }
        }

        public ushort MaxHP, MaxMP;

        public override AttackMode AMode
        {
            get { return Info.AMode; }
            set { Info.AMode = value; }
        }
        public override PetMode PMode
        {
            get { return Info.PMode; }
            set { Info.PMode = value; }
        }
        
        public long Experience
        {
            set { Info.Experience = value; }
            get { return Info.Experience; }
        }
        public long MaxExperience;
        public byte LifeOnHit;
        public byte HpDrainRate;
        public float HpDrain = 0;
        
        public byte Hair
        {
            get { return Info.Hair; }
            set { Info.Hair = value; }
        }
        public MirClass Class
        {
            get { return Info.Class; }
        }
        public MirGender Gender
        { get { return Info.Gender; } }

        public int BindMapIndex
        {
            get { return Info.BindMapIndex; }
            set { Info.BindMapIndex = value; }
        }
        public Point BindLocation
        {
            get { return Info.BindLocation; }
            set { Info.BindLocation = value; }
        }

        public bool CanMove
        {
            get { return !Dead && Envir.Time >= ActionTime; }
        }
        public bool CanWalk
        {
            get { return !Dead && Envir.Time >= ActionTime && !InTrapRock; }
        }
        public bool CanAttack
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= AttackTime && CurrentPoison != PoisonType.Paralysis && CurrentPoison != PoisonType.Frozen;
            }
        }
        public bool CanRegen
        {
            get { return Envir.Time >= RegenTime && _runCounter == 0; }
        }
        private bool CanCast
        {
            get
            {
                return !Dead && Envir.Time >= ActionTime && Envir.Time >= SpellTime && CurrentPoison != PoisonType.Stun && 
                    CurrentPoison != PoisonType.Paralysis && CurrentPoison != PoisonType.Frozen;
            }
        }

        public const long TurnDelay = 350, MoveDelay = 600, HarvestDelay = 350, RegenDelay = 10000, PotDelay = 200, HealDelay = 600, DuraDelay = 10000, VampDelay = 500;
        public long ActionTime, RunTime, RegenTime, PotTime, HealTime, AttackTime, TorchTime, DuraTime, ShoutTime, SpellTime, VampTime, SearchTime, PoisonFieldTime;

        public bool MagicShield;
        public byte MagicShieldLv;
        public long MagicShieldTime;

        private int _runCounter;

        public uint NPCID;
        public NPCPage NPCPage;
        public bool NPCSuccess;

        public bool NPCGoto;
        public string NPCGotoPage;
        public NPCJumpPage NPCJumpPage;

        public NPCListener NPCListener;

        public List<KeyValuePair<string, string>> NPCVar = new List<KeyValuePair<string, string>>();

        public bool UserMatch;
        public string MatchName;
        public ItemType MatchType;
        public int PageSent;
        public List<AuctionInfo> Search = new List<AuctionInfo>();
        public List<ItemSets> ItemSets = new List<ItemSets>();
        public List<EquipmentSlot> MirSet = new List<EquipmentSlot>();

        public bool FatalSword, Slaying, TwinDrakeBlade, FlamingSword;
        public long FlamingSwordTime;
        public bool ActiveBlizzard;
        public byte Reflect;
        public bool UnlockCurse = false;
        public bool CanCreateGuild = false;
        public GuildObject MyGuild = null;
        public Rank MyGuildRank = null;
        public GuildObject PendingGuildInvite = null;
        public bool GuildNoticeChanged = true; //set to false first time client requests notice list, set to true each time someone in guild edits notice
        public bool GuildMembersChanged = true;//same as above but for members
        public bool GuildCanRequestItems = true;
        public override bool Blocking
        {
            get
            {
                return !Dead;
            }
        }
        public bool AllowGroup
        {
            get { return Info.AllowGroup; }
            set { Info.AllowGroup = value; }
        }

        public bool GameStarted { get; set; }

        public bool HasTeleportRing, HasProtectionRing, HasRevivalRing;

        public bool HasMuscleRing, HasClearRing, HasParalysisRing, HasProbeNecklace, HasSkillNecklace, NoDuraLoss;


        public PlayerObject GroupInvitation;
        
        public PlayerObject(CharacterInfo info, MirConnection connection)
        {
            if (info.Player != null)
                throw new InvalidOperationException("Player.Info not Null.");
            
            info.Player = this;
            Connection = connection;
            Info = info;
            Account = Connection.Account;

            if (Level == 255 || Account.AdminAccount)
            {
                IsGM = true;
                SMain.Enqueue(string.Format("{0} is now a GM", Name));
            }

            if (Level == 0) NewCharacter();

            RefreshStats();

            if (HP == 0)
            {
                SetHP(MaxHP);
                SetMP(MaxMP);
                CurrentLocation = BindLocation;
                CurrentMapIndex = BindMapIndex;
            }
        }
        public void StopGame()
        {
            if (Node == null) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject pet = Pets[i];
                pet.Master = null;

                if (!pet.Dead)
                {
                    Info.Pets.Add(new PetInfo(pet));
                    pet.CurrentMap.RemoveObject(pet);
                    pet.Despawn();
                }
            }

            Pets.Clear();
            if (MyGuild != null) MyGuild.PlayerLogged(this, false);
            Envir.Players.Remove(this);
            CurrentMap.RemoveObject(this);
            Despawn();

            if (GroupMembers != null)
            {
                GroupMembers.Remove(this);

                if (GroupMembers.Count > 1)
                {
                    Packet p = new S.DeleteMember { Name = Name };

                    for (int i = 0; i < GroupMembers.Count; i++)
                        GroupMembers[i].Enqueue(p);
                }
                else
                {
                    GroupMembers[0].Enqueue(new S.DeleteGroup());
                    GroupMembers[0].GroupMembers = null;
                }
                GroupMembers = null;
            }

            SMain.Enqueue(string.Format("{0} Has logged out.", Name));

            Info.LastIP = Connection.IPAddress;
            Info.LastDate = Envir.Now;

            CleanUp();
        }

        private void NewCharacter()
        {
            if (Envir.StartPoints.Count == 0) return;

            SetBind();

            Level = 1;
            Hair = 1;


            for (int i = 0; i < Envir.StartItems.Count; i++)
            {
                ItemInfo info = Envir.StartItems[i];
                if (!CorrectStartItem(info)) continue;

                AddItem(Envir.CreateFreshItem(info));
            }

        }

        public override void Process()
        {
            if (GroupInvitation != null && GroupInvitation.Node == null)
                GroupInvitation = null;

            if (MagicShield && Envir.Time > MagicShieldTime)
            {
                MagicShield = false;
                MagicShieldLv = 0;
                MagicShieldTime = 0;
                CurrentMap.Broadcast(new S.ObjectEffect {ObjectID = ObjectID, Effect = SpellEffect.MagicShieldDown}, CurrentLocation);
            }
            if (FlamingSword && Envir.Time >= FlamingSwordTime * 2)
            {
                FlamingSword = false;
                Enqueue(new S.SpellToggle { Spell = Spell.FlamingSword, CanUse = false });
            }

            if (Envir.Time > RunTime && _runCounter > 0)
            {
                RunTime = Envir.Time + 1500;
                _runCounter--;
            }

            if (HasClearRing)
                AddBuff(new Buff { Type = BuffType.Hiding, Caster = this, ExpireTime = Envir.Time + 1, Infinite = true });

            //NPC Time Recall
            if (NPCJumpPage != null && Envir.Time > NPCJumpPage.TimePeriod && NPCJumpPage.Active)
            {
                NPCJumpPage.Active = false;

                if (NPCJumpPage.PlayerMap != null)
                    Teleport(NPCJumpPage.PlayerMap, NPCJumpPage.PlayerCoords);

                if (NPCJumpPage.NPCGotoPage != null && !NPCGoto && NPCJumpPage.Interrupted == false)
                {
                    NPCGoto = true;
                    NPCGotoPage = NPCJumpPage.NPCGotoPage;
                    CallNPC(NPCJumpPage.NPCID, NPCGotoPage);
                }
            }

            for (int i = Pets.Count() - 1; i >= 0; i--)
            {
                if (Pets[i].Dead)
                    Pets.Remove(Pets[i]);
            }

            ProcessBuffs();
            ProcessRegen();
            ProcessPoison();

          /*  if (HealthChanged)
            {
                Enqueue(new S.HealthChanged { HP = HP, MP = MP });

                BroadcastHealthChange();

                HealthChanged = false;
            }*/

            UserItem item;
            if (Envir.Time > TorchTime)
            {
                TorchTime = Envir.Time + 10000;
                item = Info.Equipment[(int)EquipmentSlot.Torch];
                if (item != null)
                {
                    DamageItem(item, 5);

                    if (item.CurrentDura == 0)
                    {
                        Info.Equipment[(int)EquipmentSlot.Torch] = null;
                        Enqueue(new S.DeleteItem {UniqueID = item.UniqueID, Count = item.Count});
                        RefreshStats();
                    }
                }
            }

            if (Envir.Time > DuraTime)
            {
                DuraTime = Envir.Time + DuraDelay;

                for (int i = 0; i < Info.Equipment.Length; i++)
                {
                    item = Info.Equipment[i];
                    if (item == null || !item.DuraChanged) continue;
                    item.DuraChanged = false;
                    Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                }
            }

            base.Process();

            RefreshNameColour();

        }
        public override void SetOperateTime()
        {
            OperateTime = Envir.Time;
        }

        private void ProcessBuffs()
        {
            bool refresh = false;
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {

                Buff buff = Buffs[i];

                if (Envir.Time <= buff.ExpireTime) continue;
                //if (buff.Infinite) continue;

                Buffs.RemoveAt(i);
                Enqueue(new S.RemoveBuff {Type = buff.Type});

                switch (buff.Type)
                {
                    case BuffType.Hiding:
                        Hidden = false;
                        break;
                }

                refresh = true;
            }

            if (refresh) RefreshStats();
        }
        private void ProcessRegen()
        {
            if (Dead) return;

            int healthRegen = 0, manaRegen = 0;

            if (CanRegen)
            {
                RegenTime = Envir.Time + RegenDelay;


                if (HP < MaxHP)
                {
                    healthRegen += (int)(MaxHP * 0.03F) + 1;
                    healthRegen += (int)(healthRegen * ((double)HealthRecovery / Settings.HealthRegenWeight));
                }

                if (MP < MaxMP)
                {
                    manaRegen += (int)(MaxMP * 0.03F) + 1;
                    manaRegen += (int)(manaRegen * ((double)SpellRecovery / Settings.ManaRegenWeight));
                }
            }

            if (Envir.Time > PotTime)
            {
                //PotTime = Envir.Time + Math.Max(50,Math.Min(PotDelay, 600 - (Level * 10)));
                PotTime = Envir.Time + PotDelay;
                int PerTickRegen = 5 + (Level / 10);
                if (PotHealthAmount > PerTickRegen)
                {
                    healthRegen += PerTickRegen;
                    PotHealthAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    healthRegen += PotHealthAmount;
                    PotHealthAmount = 0;
                }

                if (PotManaAmount > PerTickRegen)
                {
                    manaRegen += PerTickRegen;
                    PotManaAmount -= (ushort)PerTickRegen;
                }
                else
                {
                    manaRegen += PotManaAmount;
                    PotManaAmount = 0;
                }
            }

            if (Envir.Time > HealTime)
            {
                HealTime = Envir.Time + HealDelay;

                if (HealAmount > 5)
                {
                    healthRegen += 5;
                    HealAmount -= 5;
                }
                else
                {
                    healthRegen += HealAmount;
                    HealAmount = 0;
                }
            }

            if (Envir.Time > VampTime)
            {
                VampTime = Envir.Time + VampDelay;

                if (VampAmount > 10)
                {
                    healthRegen += 10;
                    VampAmount -= 10;
                }
                else
                {
                    healthRegen += VampAmount;
                    VampAmount = 0;
                }
            }

            if (healthRegen > 0) ChangeHP(healthRegen);
            if (HP == MaxHP)
            {
                PotHealthAmount = 0;
                HealAmount = 0;
            }

            if (manaRegen > 0) ChangeMP(manaRegen);
            if (MP == MaxMP) PotManaAmount = 0;
        }
        private void ProcessPoison()
        {
            PoisonType type = PoisonType.None;
            PoisonRate = 1F;

            for (int i = PoisonList.Count - 1; i >= 0; i--)
            {
                Poison poison = PoisonList[i];

                if (poison.Owner != null && poison.Owner.Node == null)
                {
                    PoisonList.RemoveAt(i);
                    continue;
                }

                if (Envir.Time > poison.TickTime)
                {
                    poison.Time++;
                    poison.TickTime = Envir.Time + poison.TickSpeed;

                    if (poison.Time >= poison.Duration)
                        PoisonList.RemoveAt(i);

                    if (poison.PType == PoisonType.Green)
                    {
                        LastHitter = poison.Owner;
                        LastHitTime = Envir.Time + 10000;
                        ChangeHP(-poison.Value);
                        if (Dead) break;
                        RegenTime = Envir.Time + RegenDelay;
                    }
                }

                switch (poison.PType)
                {
                    case PoisonType.Red:
                        PoisonRate += 0.10F;
                        break;
                    case PoisonType.Stun:
                        PoisonRate += 0.2F;
                        break;
                }

                if ((int)type < (int)poison.PType)
                    type = poison.PType;
            }

            if (type == CurrentPoison) return;

            Enqueue(new S.Poisoned { Poison = type });
            Broadcast(new S.ObjectPoisoned { ObjectID = ObjectID, Poison = type });

            CurrentPoison = type;
        }


        public override void Process(DelayedAction action)
        {
            switch (action.Type)
            {
                case DelayedType.Magic:
                    CompleteMagic(action.Params);
                    break;
                case DelayedType.Damage:
                    CompleteAttack(action.Params);
                    break;
                case DelayedType.MapMovement:
                    CompleteMapMovement(action.Params);
                    break;
                case DelayedType.Mine:
                    CompleteMine(action.Params);
                    break;
            }
        }


        private void SetHP(ushort amount)
        {
            if (HP == amount) return;

            HP = amount <= MaxHP ? amount : MaxHP;
            HP = GMNeverDie ? MaxHP : HP;

            if (!Dead && HP == 0) Die();

            //HealthChanged = true;
            Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        private void SetMP(ushort amount)
        {
            if (MP == amount) return;
            //was info.MP
            MP = amount <= MaxMP ? amount : MaxMP;
            MP = GMNeverDie ? MaxMP : MP;

            // HealthChanged = true;
            Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }

        public void ChangeHP(int amount)
        {
            if (amount < 0) amount = (int)(amount * PoisonRate);

            if (HasProtectionRing && MP > 0 && amount < 0)
            {
                ChangeMP(amount);
                return;
            }

            ushort value = (ushort)Math.Max(ushort.MinValue, Math.Min(MaxHP, HP + amount));

            if (value == HP) return;

            HP = value;
            HP = GMNeverDie ? MaxHP : HP;

            if (!Dead && HP == 0) Die();

            // HealthChanged = true;
            Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        public void ChangeMP(int amount)
        {
            ushort value = (ushort)Math.Max(ushort.MinValue, Math.Min(MaxMP, MP + amount));

            if (value == MP) return;

            MP = value;
            MP = GMNeverDie ? MaxMP : MP;

            // HealthChanged = true;
            Enqueue(new S.HealthChanged { HP = HP, MP = MP });
            BroadcastHealthChange();
        }
        public override void Die()
        {
            if (HasRevivalRing && Envir.Time > LastRevivalTime)
            {
                LastRevivalTime = Envir.Time + 300000;

                for (var i = (int)EquipmentSlot.RingL; i <= (int)EquipmentSlot.RingR; i++)
                {
                    var item = Info.Equipment[i];

                    if (item == null) continue;
                    if (!(item.Info.Unique.HasFlag(SpecialItemMode.Revival)) || item.CurrentDura < 1000) continue;
                    SetHP(MaxHP);
                    item.CurrentDura = (ushort)(item.CurrentDura - 1000);
                    Enqueue(new S.DuraChanged { UniqueID = item.UniqueID, CurrentDura = item.CurrentDura });
                    RefreshStats();
                    ReceiveChat("You have been given a second chance at life", ChatType.System);
                    return;
                }
            }

            if (LastHitter != null && LastHitter.Race == ObjectType.Player && !CurrentMap.Info.Fight)
            {
                if (Envir.Time > BrownTime && PKPoints < 200)
                {
                    LastHitter.PKPoints = Math.Min(int.MaxValue, LastHitter.PKPoints + 100);
                    LastHitter.ReceiveChat(string.Format("You have murdered {0}", Name), ChatType.System);
                    ReceiveChat(string.Format("You have been murdered by {0}", LastHitter.Name), ChatType.System);
                }
            }

            for (int i = Pets.Count - 1; i >= 0; i--)
                Pets[i].Die();
            
                if (PKPoints > 200)
                    RedDeathDrop(LastHitter);
                else if (!InSafeZone)
                    DeathDrop(LastHitter);

            HP = 0;
            Dead = true;

            Enqueue(new S.Death {Direction = Direction, Location = CurrentLocation});
            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            PoisonList.Clear();
            InTrapRock = false;
        }

        private void DeathDrop(MapObject killer)
        {
            if (CurrentMap.Info.NoDropPlayer && Race == ObjectType.Player) return;

            if (killer == null || killer.Race != ObjectType.Player)
            {
                UserItem temp = Info.Equipment[(int) EquipmentSlot.Stone];
                if (temp != null)
                {
                    Info.Equipment[(int) EquipmentSlot.Stone] = null;
                    Enqueue(new S.DeleteItem {UniqueID = temp.UniqueID, Count = temp.Count});
                }


                for (int i = 0; i < Info.Equipment.Length; i++)
                {
                    temp = Info.Equipment[i];

                    if (temp == null) continue;
                    if (temp.Info.Bind.HasFlag(BindMode.DontDeathdrop)) continue;
                    if (temp.Count > 1)
                    {
                        int percent = Envir.RandomomRange(10, 8);

                        uint count = (uint)Math.Ceiling(temp.Count / 10F * percent);

                        if (count > temp.Count)
                            throw new ArgumentOutOfRangeException();

                        UserItem temp2 = Envir.CreateFreshItem(temp.Info);
                        temp2.Count = count;
                        
                        if (DropItem(temp2, Settings.DropRange))
                        {
                            if (count == temp.Count)
                                Info.Equipment[i] = null;

                            Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = count });
                            temp.Count -= count;
                        }
                    }
                    else if (Envir.Random.Next(30) == 0)
                    {
                        if (DropItem(temp, Settings.DropRange))
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                        }
                    }
                }

            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem temp = Info.Inventory[i];

                if (temp == null) continue;
                if (temp.Info.Bind.HasFlag(BindMode.DontDeathdrop)) continue;

                if (ItemSets.Any(set => set.Set == ItemSet.Spirit && !set.SetComplete))
                {
                    if (temp.Info.Set == ItemSet.Spirit)
                    {
                        Info.Equipment[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                    }
                }

                if (temp.Count > 1)
                {
                    int percent = Envir.RandomomRange(10, 8);
                    if (percent == 0) continue;
                    
                    uint count = (uint) Math.Ceiling(temp.Count/10F*percent);

                    if (count > temp.Count)
                        throw new ArgumentOutOfRangeException();

                    UserItem temp2 = Envir.CreateFreshItem(temp.Info);
                    temp2.Count = count;

                    if (DropItem(temp2, Settings.DropRange))
                    {
                        if (count == temp.Count)
                            Info.Equipment[i] = null;

                        Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = count });
                        temp.Count -= count;
                    }
                }
                else if (Envir.Random.Next(10) == 0)
                {
                    if (DropItem(temp, Settings.DropRange))
                    {
                        Info.Inventory[i] = null;
                        Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                    }
                }
            }

            RefreshStats();
        }
        private void RedDeathDrop(MapObject killer)
        {
            if (killer == null || killer.Race != ObjectType.Player)
            {
                UserItem temp = Info.Equipment[(int)EquipmentSlot.Stone];
                if (temp != null)
                {
                    Info.Equipment[(int)EquipmentSlot.Stone] = null;
                    Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                }


                for (int i = 0; i < Info.Equipment.Length; i++)
                {
                    temp = Info.Equipment[i];

                    if (temp == null) continue;
                    if (temp.Info.Bind.HasFlag(BindMode.DontDeathdrop)) continue;

                    if (temp.Count > 1)
                    {
                        int percent = Envir.RandomomRange(10, 4);

                        uint count = (uint)Math.Ceiling(temp.Count / 10F * percent);

                        if (count > temp.Count)
                            throw new ArgumentOutOfRangeException();

                        UserItem temp2 = Envir.CreateFreshItem(temp.Info);
                        temp2.Count = count;

                        if (DropItem(temp2, Settings.DropRange))
                        {
                            if (count == temp.Count)
                                Info.Equipment[i] = null;

                            Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = count });
                            temp.Count -= count;
                        }
                    }
                    else if (Envir.Random.Next(10) == 0)
                    {
                        if (DropItem(temp, Settings.DropRange))
                        {
                            Info.Equipment[i] = null;
                            Enqueue(new S.DeleteItem { UniqueID = temp.UniqueID, Count = temp.Count });
                        }
                    }
                }

            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem temp = Info.Inventory[i];

                if (temp == null) continue;
                if (temp.Info.Bind.HasFlag(BindMode.DontDeathdrop)) continue;

                if (!DropItem(temp, Settings.DropRange)) continue;

                Info.Inventory[i] = null;
                Enqueue(new S.DeleteItem {UniqueID = temp.UniqueID, Count = temp.Count});
            }

            RefreshStats();
        }


        public override void WinExp(uint amount)
        {
            amount = (uint)(amount * Settings.ExpRate);

            if (GroupMembers != null)
            {
                for (int i = 0; i < GroupMembers.Count; i++)
                {
                    PlayerObject player = GroupMembers[i];
                    if (player == this) continue;
                    if (player.CurrentMap != CurrentMap || !Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) || player.Dead) continue;

                    amount = (uint)(amount * 0.75F);
                    break;
                }

                for (int i = 0; i < GroupMembers.Count; i++)
                {
                    PlayerObject player = GroupMembers[i];
                    if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                        player.GainExp(amount);
                }
            }
            else
                GainExp(amount);

        }
        public void GainExp(uint amount)
        {
            Experience += amount;

            Enqueue(new S.GainExperience { Amount = amount });


            for (int i = 0; i < Pets.Count; i++)
            {
                MonsterObject monster = Pets[i];
                if (monster.CurrentMap == CurrentMap && Functions.InRange(monster.CurrentLocation, CurrentLocation, Globals.DataRange) && !monster.Dead)
                    monster.PetExp(amount);
            }
            
            if (MyGuild != null)
                MyGuild.GainExp(amount);

            if (Experience < MaxExperience) return;
            if (Level >= byte.MaxValue) return;

            //Calculate increased levels
            var experience = Experience;

            while (experience > MaxExperience)
            {
                Level++;
                experience -= MaxExperience;

                RefreshLevelStats();

                if (Level >= byte.MaxValue) break;
            }

            Experience = experience;

            LevelUp();
        }

        public void LevelUp()
        {
            RefreshStats();
            SetHP(MaxHP);
            SetMP(MaxMP);
            Enqueue(new S.LevelChanged { Level = Level, Experience = Experience, MaxExperience = MaxExperience });
            Broadcast(new S.ObjectLeveled { ObjectID = ObjectID });
        }


        private static int FreeSpace(IList<UserItem> array)
        {
            int count = 0;

            for (int i = 0; i < array.Count; i++)
                if (array[i] == null) count++;

            return count;
        }
        private void AddItem(UserItem item)
        {
            if (item.Info.StackSize > 1) //Stackable
            {
                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem temp = Info.Inventory[i];
                    if (temp == null || item.Info != temp.Info || temp.Count >= temp.Info.StackSize) continue;

                    if (item.Count + temp.Count <= temp.Info.StackSize)
                    {
                        temp.Count += item.Count;
                        return;
                    }
                    item.Count -= temp.Info.StackSize - temp.Count;
                    temp.Count = temp.Info.StackSize;
                }
            }

            if (item.Info.Type == ItemType.Potion || item.Info.Type == ItemType.Scroll)
            {
                for (int i = 40; i < Info.Inventory.Length; i++)
                {
                    if (Info.Inventory[i] != null) continue;
                    Info.Inventory[i] = item;
                    return;
                }
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != null) continue;
                Info.Inventory[i] = item;
                return;
            }
        }
        private bool CorrectStartItem(ItemInfo info)
        {
            switch (Class)
            {
                case MirClass.Warrior:
                    if (!info.RequiredClass.HasFlag(RequiredClass.Warrior)) return false;
                    break;
                case MirClass.Wizard:
                    if (!info.RequiredClass.HasFlag(RequiredClass.Wizard)) return false;
                    break;
                case MirClass.Taoist:
                    if (!info.RequiredClass.HasFlag(RequiredClass.Taoist)) return false;
                    break;
                case MirClass.Assassin:
                    if (!info.RequiredClass.HasFlag(RequiredClass.Assassin)) return false;
                    break;
                default:
                    return false;
            }

            switch (Gender)
            {
                case MirGender.Male:
                    if (!info.RequiredGender.HasFlag(RequiredGender.Male)) return false;
                    break;
                case MirGender.Female:
                    if (!info.RequiredGender.HasFlag(RequiredGender.Female)) return false;
                    break;
                default:
                    return false;
            }

            return true;
        }
        public void CheckItemInfo(ItemInfo info, bool DontLoop = false)
        {
            if ((DontLoop == false) && (info.ClassBased | info.LevelBased)) //send all potential data so client can display it
            {
                for (int i = 0; i < Envir.ItemInfoList.Count; i++)
                {
                    if ((Envir.ItemInfoList[i] != info) && (Envir.ItemInfoList[i].Name.StartsWith(info.Name)))
                        CheckItemInfo(Envir.ItemInfoList[i],true);
                }
            }

            if (Connection.SentItemInfo.Contains(info)) return;
            Enqueue(new S.NewItemInfo { Info = info });
            Connection.SentItemInfo.Add(info);
        }


        private void SetBind()
        {
            SafeZoneInfo szi = Envir.StartPoints[Envir.Random.Next(Envir.StartPoints.Count)];

            BindMapIndex = szi.Info.Index;
            BindLocation = szi.Location;
        }
        public void StartGame()
        {
            Map temp = Envir.GetMap(CurrentMapIndex);
            
            if (temp != null && temp.Info.NoReconnect)
            {
                Map temp1 = Envir.GetMapByNameAndInstance(temp.Info.NoReconnectMap);
                if (temp1 != null)
                {
                    temp = temp1;
                    CurrentLocation = GetRandomPoint(40, 0, temp);
                }
            }

            if (temp == null || !temp.ValidPoint(CurrentLocation))
            {
                temp = Envir.GetMap(BindMapIndex);

                if (temp == null || !temp.ValidPoint(BindLocation))
                {
                    SetBind();
                    temp = Envir.GetMap(BindMapIndex);

                    if (temp == null || !temp.ValidPoint(BindLocation))
                    {
                        StartGameFailed();
                        return;
                    }
                }
                CurrentMapIndex = BindMapIndex;
                CurrentLocation = BindLocation;
            }
            temp.AddObject(this);
            CurrentMap = temp;
            Envir.Players.Add(this);
            StartGameSuccess();
        }
        private void StartGameSuccess()
        {
            Connection.Stage = GameStage.Game;
            Enqueue(new S.StartGame { Result = 4 });
            ReceiveChat("Welcome to the Legend of Mir 2 C# Server.", ChatType.Hint);
            if (Info.GuildIndex != -1)
            {
               MyGuild = Envir.GetGuild(Info.GuildIndex);
                if (MyGuild == null)
                {
                    Info.GuildIndex = -1;
                    ReceiveChat("You have been removed from the guild.", ChatType.System);
                }
                else
                {
                    MyGuildRank = MyGuild.FindRank(Info.Name);
                    if (MyGuildRank == null)
                    {
                        MyGuild = null;
                        Info.GuildIndex = -1;
                        ReceiveChat("You have been removed from the guild.", ChatType.System);
                    }
                }
            }
            Spawned();
            GetItemInfo();
            GetMapInfo();
            GetUserInfo();
            Enqueue(new S.BaseStatsInfo { Stats = Settings.ClassBaseStats[(byte)Class] });
            GetObjectsPassive();
            Enqueue(new S.TimeOfDay { Lights = Envir.Lights });
            Enqueue(new S.ChangeAMode { Mode = AMode });
            if (Class == MirClass.Wizard || Class == MirClass.Taoist)
                Enqueue(new S.ChangePMode {Mode = PMode});
            Enqueue(new S.SwitchGroup { AllowGroup = AllowGroup });

            if (Info.Thrusting) Enqueue(new S.SpellToggle { Spell = Spell.Thrusting, CanUse = true });
            if (Info.HalfMoon) Enqueue(new S.SpellToggle { Spell = Spell.HalfMoon, CanUse = true });
            if (Info.CrossHalfMoon) Enqueue(new S.SpellToggle { Spell = Spell.CrossHalfMoon, CanUse = true });
            if (Info.DoubleSlash) Enqueue(new S.SpellToggle { Spell = Spell.DoubleSlash, CanUse = true });

            for (int i = 0; i < Info.Pets.Count; i++)
            {
                PetInfo info = Info.Pets[i];
                MonsterObject monster = MonsterObject.GetMonster(Envir.GetMonsterInfo(info.MonsterIndex));

                monster.PetLevel = info.Level;
                monster.MaxPetLevel = info.MaxPetLevel;
                monster.PetExperience = info.Experience;

                monster.Master = this;
                Pets.Add(monster);

                monster.RefreshAll();
                if (!monster.Spawn(CurrentMap, Back))
                    monster.Spawn(CurrentMap, CurrentLocation);

                monster.SetHP(info.HP);
            }
            Info.Pets.Clear();
            if (MyGuild != null)
                MyGuild.PlayerLogged(this, true);
            SMain.Enqueue(string.Format("{0} has connected.", Info.Name));
        }
        private void StartGameFailed()
        {
            Enqueue(new S.StartGame {Result = 3});
            CleanUp();
        }
        public void TownRevive()
        {
            if (!Dead) return;
            
            Map temp = Envir.GetMap(BindMapIndex);

            if (temp == null || !temp.ValidPoint(BindLocation)) return;

            Dead = false;
            SetHP(MaxHP);
            SetMP(MaxMP);
            RefreshStats();

            CurrentMap.RemoveObject(this);
            Broadcast(new S.ObjectRemove { ObjectID = ObjectID });

            CurrentMap = temp;
            CurrentLocation = BindLocation;

            CurrentMap.AddObject(this);

            Enqueue(new S.MapChanged
            {
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight
            });

            GetObjects();
            Enqueue(new S.Revived());
            Broadcast(new S.ObjectRevived {ObjectID = ObjectID, Effect = true});


            InSafeZone = true;
        }

        private void GetItemInfo()
        {
            UserItem item;
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null) continue;
                CheckItemInfo(item.Info);
            }

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                item = Info.Equipment[i];

                if (item == null) continue;
                CheckItemInfo(item.Info);
            }
        }
        private void GetUserInfo()
        {
            string guildname = MyGuild != null ? MyGuild.Name : "";
            string guildrank = MyGuild != null ? MyGuildRank.Name : "";
            S.UserInformation packet = new S.UserInformation
                {
                    ObjectID = ObjectID,
                    RealId = (uint)Info.Index,
                    Name = Name,
                    GuildName = guildname,
                    GuildRank = guildrank,
                    NameColour = NameColour,
                    Class = Class,
                    Gender = Gender,
                    Level = Level,
                    Location = CurrentLocation,
                    Direction = Direction,
                    Hair = Hair,
                    HP = HP,
                    MP = MP,

                    Experience = Experience,
                    MaxExperience = MaxExperience,
                    Inventory = new UserItem[Info.Inventory.Length],
                    Equipment = new UserItem[Info.Equipment.Length],
                    Gold = Account.Gold,
                };

            //Copy this method to prevent modification before sending packet information.
            for (int i = 0; i < Info.Magics.Count; i++)
                packet.Magics.Add(Info.Magics[i].CreateClientMagic());

            Info.Inventory.CopyTo(packet.Inventory, 0);
            Info.Equipment.CopyTo(packet.Equipment, 0);
            

            Enqueue(packet);
        }
        private void GetMapInfo()
        {
            Enqueue(new S.MapInformation
                {
                    FileName = CurrentMap.Info.FileName,
                    Title = CurrentMap.Info.Title,
                    MiniMap = CurrentMap.Info.MiniMap,
                    Lights = CurrentMap.Info.Light,
                    BigMap = CurrentMap.Info.BigMap,
                    Lightning = CurrentMap.Info.Lightning,
                    Fire = CurrentMap.Info.Fire,
                    MapDarkLight = CurrentMap.Info.MapDarkLight
                });
        }
        private void GetObjects()
        {
            for (int y = CurrentLocation.Y - Globals.DataRange; y <= CurrentLocation.Y + Globals.DataRange; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - Globals.DataRange; x <= CurrentLocation.X + Globals.DataRange; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;
                    if (x < 0 || x >= CurrentMap.Width) continue;

                    Cell cell = CurrentMap.GetCell(x, y);

                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        ob.Add(this);
                    }
                }
            }
        }
        private void GetObjectsPassive()
        {
            for (int y = CurrentLocation.Y - Globals.DataRange; y <= CurrentLocation.Y + Globals.DataRange; y++)
            {
                if (y < 0) continue;
                if (y >= CurrentMap.Height) break;

                for (int x = CurrentLocation.X - Globals.DataRange; x <= CurrentLocation.X + Globals.DataRange; x++)
                {
                    if (x < 0) continue;
                    if (x >= CurrentMap.Width) break;
                    if (x < 0 || x >= CurrentMap.Width) continue;

                    Cell cell = CurrentMap.GetCell(x, y);

                    if (!cell.Valid || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (ob == this) continue;

                        if (ob.Race == ObjectType.Player && ob.Observer) continue;

                        Enqueue(ob.GetInfo());

                        if (ob.Race == ObjectType.Player || ob.Race == ObjectType.Monster)
                            ob.SendHealth(this);
                    }
                }
            }
        }


        public void RefreshStats()
        {
            if (HasUpdatedBaseStats == false)
            {
                Enqueue(new S.BaseStatsInfo { Stats = Settings.ClassBaseStats[(byte)Class]});
                HasUpdatedBaseStats = true;
            }
            RefreshLevelStats();
            RefreshBagWeight();
            RefreshEquipmentStats();
            RefreshItemSetStats();
            RefreshMirSetStats();
            RefreshSkills();
            RefreshBuffs();
            RefreshStatCaps();
            //Location Stats ?

            if (HP > MaxHP) SetHP(MaxHP);
            if (MP > MaxMP) SetMP(MaxMP);

            AttackSpeed = 1400 - ((ASpeed * 60) + Math.Min(370,(Level * 5)));

            if (AttackSpeed < 600) AttackSpeed = 600;
        }
        private void RefreshLevelStats()
        {
            MaxExperience = Level < Settings.ExperienceList.Count ? Settings.ExperienceList[Level - 1] : 0;
            MaxHP = 0; MaxMP = 0;
            MinAC = 0; MaxAC = 0;
            MinMAC = 0; MaxMAC = 0;
            MinDC = 0; MaxDC = 0;
            MinMC = 0; MaxMC = 0;
            MinSC = 0; MaxSC = 0;

            Accuracy = Settings.ClassBaseStats[(byte)Class].StartAccuracy;
            Agility = Settings.ClassBaseStats[(byte)Class].StartAgility;
            CriticalRate = Settings.ClassBaseStats[(byte)Class].StartCriticalRate;
            CriticalDamage = Settings.ClassBaseStats[(byte)Class].StartCriticalDamage;
            //Other Stats;
            MaxBagWeight = 0;
            MaxWearWeight = 0;
            MaxHandWeight = 0;
            ASpeed = 0;
            Luck = 0;
            LifeOnHit = 0;
            HpDrainRate = 0;
            Reflect = 0;
            MagicResist = 0;
            PoisonResist = 0;
            HealthRecovery = 0;
            SpellRecovery = 0;
            PoisonRecovery = 0;
            Holy = 0;
            Freezing = 0;
            PoisonAttack = 0;

            
            MaxHP = (ushort)Math.Min(ushort.MaxValue, 14 + (Level / Settings.ClassBaseStats[(byte)Class].HpGain + Settings.ClassBaseStats[(byte)Class].HpGainRate) * Level);

            MinAC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MinAc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinAc : 0);
            MaxAC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxAc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxAc : 0);
            MinMAC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MinMac > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinMac : 0);
            MaxMAC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxMac > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxMac : 0);
            MinDC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MinDc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinDc : 0);
            MaxDC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxDc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxDc : 0);
            MinMC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MinMc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinMc : 0);
            MaxMC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxMc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxMc : 0);
            MinSC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MinSc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MinSc : 0);
            MaxSC = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].MaxSc > 0 ? Level / Settings.ClassBaseStats[(byte)Class].MaxSc : 0);
            CriticalRate = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].CritialRateGain > 0 ? CriticalRate +  (Level / Settings.ClassBaseStats[(byte)Class].CritialRateGain) : CriticalRate);
            CriticalDamage = (byte)Math.Min(byte.MaxValue, Settings.ClassBaseStats[(byte)Class].CriticalDamageGain > 0 ? CriticalDamage + (Level / Settings.ClassBaseStats[(byte)Class].CriticalDamageGain) : CriticalDamage);
            MaxBagWeight = (ushort)(50 + Level / Settings.ClassBaseStats[(byte)Class].BagWeightGain * Level);
            MaxWearWeight = (byte)Math.Min(byte.MaxValue, 15 + Level / Settings.ClassBaseStats[(byte)Class].WearWeightGain * Level);
            MaxHandWeight = (byte)Math.Min(byte.MaxValue, 12 + Level / Settings.ClassBaseStats[(byte)Class].HandWeightGain * Level);
            
            switch (Class)
            {
                case MirClass.Warrior:
                    MaxHP = (ushort)Math.Min(ushort.MaxValue, 14 + (Level / Settings.ClassBaseStats[(byte)Class].HpGain + Settings.ClassBaseStats[(byte)Class].HpGainRate + Level / 20F) * Level);
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, 11 + (Level * 3.5F) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Wizard:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, 13 + ((Level / 5F + 2F) * 2.2F * Level) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Taoist:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, (13 + Level / 8F * 2.2F * Level) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
                case MirClass.Assassin:
                    MaxMP = (ushort)Math.Min(ushort.MaxValue, (11 + Level * 5F) + (Level * Settings.ClassBaseStats[(byte)Class].MpGainRate));
                    break;
            }

        }
        private void RefreshBagWeight()
        {
            CurrentBagWeight = 0;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null)
                    CurrentBagWeight = (ushort) Math.Min(ushort.MaxValue, CurrentBagWeight + item.Weight);
            }
        }
        private void RefreshEquipmentStats()
        {
            sbyte OldLooks_Weapon = Looks_Weapon;
            sbyte OldLooks_Armour = Looks_Armour;
            byte OldLooks_Wings = Looks_Wings;
            byte OldLight = Light;
            Looks_Armour = 0;
            Looks_Weapon = -1;
            Looks_Wings = 0;
            Light = 0;
            CurrentWearWeight = 0;
            CurrentHandWeight = 0;

            HasTeleportRing = false;
            HasProtectionRing = false;
            HasRevivalRing = false;
            HasClearRing = false;
            HasMuscleRing = false;
            HasParalysisRing = false;
            HasProbeNecklace = false;
            HasSkillNecklace = false;
            NoDuraLoss = false;

            var skillsToAdd = new List<string>();
            var skillsToRemove = new List<string> {Settings.HealRing, Settings.FireRing};
            short Macrate = 0, Acrate = 0, HPrate = 0, MPrate = 0;
            ItemSets.Clear();
            MirSet.Clear();

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                UserItem temp = Info.Equipment[i];
                if (temp == null) continue;
                ItemInfo RealItem = Functions.GetRealItem(temp.Info, Info.Level, Info.Class, Envir.ItemInfoList);
                if (RealItem.Type == ItemType.Weapon || RealItem.Type == ItemType.Torch)
                    CurrentHandWeight = (byte) Math.Min(byte.MaxValue, CurrentHandWeight + temp.Weight);
                else
                    CurrentWearWeight = (byte)Math.Min(byte.MaxValue, CurrentWearWeight + temp.Weight);

                if (temp.CurrentDura == 0 && temp.Info.Durability > 0) continue;


                MinAC = (byte)Math.Min(byte.MaxValue, MinAC + RealItem.MinAC);
                MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + RealItem.MaxAC + temp.AC);
                MinMAC = (byte)Math.Min(byte.MaxValue, MinMAC + RealItem.MinMAC);
                MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + RealItem.MaxMAC + temp.MAC);

                MinDC = (byte)Math.Min(byte.MaxValue, MinDC + RealItem.MinDC);
                MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + RealItem.MaxDC + temp.DC);
                MinMC = (byte)Math.Min(byte.MaxValue, MinMC + RealItem.MinMC);
                MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + RealItem.MaxMC + temp.MC);
                MinSC = (byte)Math.Min(byte.MaxValue, MinSC + RealItem.MinSC);
                MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + RealItem.MaxSC + temp.SC);

                Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + RealItem.Accuracy + temp.Accuracy);
                Agility = (byte)Math.Min(byte.MaxValue, Agility + RealItem.Agility + temp.Agility);

                MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + RealItem.HP + temp.HP);
                MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + RealItem.MP + temp.MP);

                ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + temp.AttackSpeed + RealItem.AttackSpeed)));
                Luck = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, Luck + temp.Luck + RealItem.Luck)));

                MaxBagWeight = (ushort)Math.Max(ushort.MinValue, (Math.Min(ushort.MaxValue, MaxBagWeight + RealItem.BagWeight)));
                MaxWearWeight = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, MaxWearWeight + RealItem.WearWeight)));
                MaxHandWeight = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, MaxHandWeight + RealItem.HandWeight)));
                HPrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, HPrate + RealItem.HPrate));
                MPrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, MPrate + RealItem.MPrate));
                Acrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Acrate + RealItem.MaxAcRate));
                Macrate = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Macrate + RealItem.MaxMacRate));
                MagicResist = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, MagicResist + temp.MagicResist + RealItem.MagicResist)));
                PoisonResist = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonResist + temp.PoisonResist + RealItem.PoisonResist)));
                HealthRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, HealthRecovery + temp.HealthRecovery + RealItem.HealthRecovery)));
                SpellRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, SpellRecovery + temp.ManaRecovery + RealItem.SpellRecovery)));
                PoisonRecovery = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonRecovery + temp.PoisonRecovery + RealItem.PoisonRecovery)));
                CriticalRate = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, CriticalRate + temp.CriticalRate + RealItem.CriticalRate)));
                CriticalDamage = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, CriticalDamage + temp.CriticalDamage + RealItem.CriticalDamage)));
                Holy = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Holy + RealItem.Holy)));
                Freezing = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Freezing + temp.Freezing + RealItem.Freezing)));
                PoisonAttack = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, PoisonAttack + temp.PoisonAttack + RealItem.PoisonAttack)));
                Reflect = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, Reflect + RealItem.Reflect)));
                HpDrainRate = (byte)Math.Max(byte.MinValue, (Math.Min(byte.MaxValue, HpDrainRate + RealItem.HpDrainRate)));

                if (RealItem.Light > Light) Light = RealItem.Light;
                if (RealItem.Unique != SpecialItemMode.none)
                {
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Paralize)) HasParalysisRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Teleport)) HasTeleportRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Clearring)) HasClearRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Protection)) HasProtectionRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Revival)) HasRevivalRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Muscle)) HasMuscleRing = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Flame))
                    {
                        skillsToAdd.Add(Settings.FireRing);
                        skillsToRemove.Remove(Settings.FireRing);
                    }
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Healing))
                    {
                        skillsToAdd.Add(Settings.HealRing);
                        skillsToRemove.Remove(Settings.HealRing);
                    }
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Probe)) HasProbeNecklace = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.Skill)) HasSkillNecklace = true;
                    if (RealItem.Unique.HasFlag(SpecialItemMode.NoDuraLoss)) NoDuraLoss = true;
                }

                if (RealItem.Type == ItemType.Armour)
                {
                    Looks_Armour = RealItem.Shape;
                    Looks_Wings = RealItem.Effect;
                }

                if (RealItem.Type == ItemType.Weapon)
                    Looks_Weapon = RealItem.Shape;

                if (RealItem.Set == ItemSet.None) continue;

                //Normal Sets
                bool sameSetFound = false;
                foreach (var set in ItemSets.Where(set => set.Set == RealItem.Set && !set.Type.Contains(RealItem.Type)).TakeWhile(set => !set.SetComplete))
                {
                    set.Type.Add(RealItem.Type);
                    set.Count++;
                    sameSetFound = true;
                }

                if (!ItemSets.Any() || !sameSetFound)
                    ItemSets.Add(new ItemSets { Count = 1, Set = RealItem.Set, Type = new List<ItemType> { RealItem.Type } });

                //Mir Set
                if (RealItem.Set == ItemSet.Mir)
                {
                    if (!MirSet.Contains((EquipmentSlot) i))
                        MirSet.Add((EquipmentSlot) i);
                }
            }

            MaxHP = (ushort)Math.Min(ushort.MaxValue, (((double)HPrate / 100) + 1) * MaxHP);
            MaxMP = (ushort)Math.Min(ushort.MaxValue, (((double)MPrate / 100) + 1) * MaxMP);
            MaxAC = (byte)Math.Min(byte.MaxValue, (((double)Acrate / 100) + 1) * MaxAC);
            MaxMAC = (byte)Math.Min(byte.MaxValue, (((double)Macrate / 100) + 1) * MaxMAC);

            AddTempSkills(skillsToAdd);
            RemoveTempSkills(skillsToRemove);

            if (HasMuscleRing)
            {
                MaxBagWeight = (ushort) (MaxBagWeight*2);
                MaxWearWeight = Math.Min(byte.MaxValue, (byte)(MaxWearWeight * 2));
                MaxHandWeight = Math.Min(byte.MaxValue, (byte)(MaxHandWeight * 2));
            }
            if ((OldLooks_Armour != Looks_Armour) || (OldLooks_Weapon != Looks_Weapon) || (OldLooks_Wings != Looks_Wings) || (OldLight != Light))
            {
                Broadcast(GetUpdateInfo());
            }
        }

        private void RefreshItemSetStats()
        {
            foreach (var s in ItemSets)
            {
                if ((s.Set == ItemSet.Smash) && (s.Type.Contains(ItemType.Ring)) && (s.Type.Contains(ItemType.Bracelet)))
                    ASpeed = (sbyte)Math.Min(sbyte.MaxValue, ASpeed + 2);
                if ((s.Set == ItemSet.Purity) && (s.Type.Contains(ItemType.Ring)) && (s.Type.Contains(ItemType.Bracelet)))
                    Holy = Math.Min(byte.MaxValue, (byte)(Holy + 3));
                if ((s.Set == ItemSet.HwanDevil) && (s.Type.Contains(ItemType.Ring)) && (s.Type.Contains(ItemType.Bracelet)))
                {
                    MaxWearWeight = (byte)Math.Min(byte.MaxValue, MaxWearWeight + 5);
                    MaxBagWeight = (byte)Math.Min(byte.MaxValue, MaxBagWeight + 20);
                }

                if (!s.SetComplete) continue;
                switch (s.Set)
                {
                    case ItemSet.Mundane:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 50);
                        break;
                    case ItemSet.NokChi:
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 50);
                        break;
                    case ItemSet.TaoProtect:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 30);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 30);
                        break;
                    case ItemSet.RedOrchid:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + 2);
                        HpDrainRate = (byte)Math.Min(byte.MaxValue, HpDrainRate + 10);
                        break;
                    case ItemSet.RedFlower:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 50);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP - 50);
                        break;
                    case ItemSet.Smash:
                        MinDC = (byte)Math.Min(byte.MaxValue, MinDC + 1);
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 3);
                        break;
                    case ItemSet.HwanDevil:
                        MinMC = (byte)Math.Min(byte.MaxValue, MinMC + 1);
                        MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 2);
                        break;
                    case ItemSet.Purity:
                        MinSC = (byte)Math.Min(byte.MaxValue, MinSC + 1);
                        MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 2);
                        //holy +2;
                        break;
                    case ItemSet.FiveString:
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + (((double)MaxHP / 100) * 30));
                        MinAC = (byte)Math.Min(byte.MaxValue, MinAC + 2);
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 2);
                        break;
                    case ItemSet.Spirit:
                        MinDC = (byte)Math.Min(byte.MaxValue, MinDC + 2);
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 5);
                        ASpeed = (sbyte)Math.Min(sbyte.MaxValue, ASpeed + 2);
                        break;
                    case ItemSet.Bone:
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 2);
                        MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 1);
                        MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 1);
                        break;
                    case ItemSet.Bug:
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 1);
                        MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 1);
                        MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 1);
                        MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                        PoisonResist = (byte)Math.Min(byte.MaxValue, PoisonResist + 1);
                        break;
                    case ItemSet.WhiteGold:
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 2);
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 2);
                        break;
                    case ItemSet.WhiteGoldH:
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 3);
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 30);
                        ASpeed = (sbyte)Math.Min(int.MaxValue, ASpeed + 2);
                        break;
                    case ItemSet.RedJade:
                        MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 2);
                        MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC +2);
                        break;
                    case ItemSet.RedJadeH:
                        MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 2);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 40);
                        Agility = (byte)Math.Min(byte.MaxValue, Agility + 2);
                        break;
                    case ItemSet.Nephrite:
                        MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 2);
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 1);
                        MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                        break;
                    case ItemSet.NephriteH:
                        MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 2);
                        MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 15);
                        MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 20);
                        Holy = (byte)Math.Min(byte.MaxValue, Holy + 1);
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + 1);
                        break;
                }
            }
        }

        private void RefreshMirSetStats()
        {
            if (MirSet.Count() == 10)
            {
                MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 1);
                MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                MaxBagWeight = (byte)Math.Min(byte.MaxValue, MaxBagWeight + 70);
                Luck = (sbyte)Math.Min(sbyte.MaxValue, Luck + 2);
                ASpeed = (sbyte)Math.Min(int.MaxValue, ASpeed + 2);
                MaxHP = (ushort)Math.Min(ushort.MaxValue, MaxHP + 70);
                MaxMP = (ushort)Math.Min(ushort.MaxValue, MaxMP + 80);
                MagicResist = (byte)Math.Min(byte.MaxValue, MagicResist + 6);
                PoisonResist = (byte)Math.Min(byte.MaxValue, PoisonResist + 6);
            }
            
            if (MirSet.Contains(EquipmentSlot.RingL) && MirSet.Contains(EquipmentSlot.RingR))
            {
                MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 1);
            }
            if (MirSet.Contains(EquipmentSlot.BraceletL) && MirSet.Contains(EquipmentSlot.BraceletR))
            {
                MinAC = (byte)Math.Min(byte.MaxValue, MinAC + 1);
                MinMAC = (byte)Math.Min(byte.MaxValue, MinMAC + 1);
            }
            if ((MirSet.Contains(EquipmentSlot.RingL) | MirSet.Contains(EquipmentSlot.RingR)) && (MirSet.Contains(EquipmentSlot.BraceletL) | MirSet.Contains(EquipmentSlot.BraceletR)) && MirSet.Contains(EquipmentSlot.Necklace))
            {
                MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 1);
                MaxBagWeight = (byte)Math.Min(byte.MaxValue, MaxBagWeight + 30);
                MaxWearWeight = (byte)Math.Min(byte.MaxValue, MaxWearWeight + 17);
            }
            if (MirSet.Contains(EquipmentSlot.RingL) && MirSet.Contains(EquipmentSlot.RingR) && MirSet.Contains(EquipmentSlot.BraceletL) && MirSet.Contains(EquipmentSlot.BraceletR) && MirSet.Contains(EquipmentSlot.Necklace))
            {
                MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + 1);
                MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + 1);
                MaxBagWeight = (byte)Math.Min(byte.MaxValue, MaxBagWeight + 20);
                MaxWearWeight = (byte)Math.Min(byte.MaxValue, MaxWearWeight + 10);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Helmet) && MirSet.Contains(EquipmentSlot.Weapon))
            {
                MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 2);
                MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 1);
                MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 1);
                Agility = (byte)Math.Min(byte.MaxValue, Agility + 1);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Boots) && MirSet.Contains(EquipmentSlot.Belt))
            {
                MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 1);
                MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 1);
                MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 1);
                MaxHandWeight = (byte)Math.Min(byte.MaxValue, MaxHandWeight + 17);
            }
            if (MirSet.Contains(EquipmentSlot.Armour) && MirSet.Contains(EquipmentSlot.Boots) && MirSet.Contains(EquipmentSlot.Belt) && MirSet.Contains(EquipmentSlot.Helmet) && MirSet.Contains(EquipmentSlot.Weapon))
            {
                MinDC = (byte)Math.Min(byte.MaxValue, MinDC + 1);
                MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + 1);
                MinMC = (byte)Math.Min(byte.MaxValue, MinMC + 1);
                MaxMC = (byte)Math.Min(byte.MaxValue, MaxMC + 1);
                MinSC = (byte)Math.Min(byte.MaxValue, MinSC + 1);
                MaxSC = (byte)Math.Min(byte.MaxValue, MaxSC + 1);
                MaxHandWeight = (byte)Math.Min(byte.MaxValue, MaxHandWeight + 17);
            }
        }

        public void RefreshStatCaps()
        {
            MagicResist = Math.Min(Settings.MaxMagicResist, MagicResist);
            PoisonResist = Math.Min(Settings.MaxPoisonResist, PoisonResist);
            CriticalRate = Math.Min(Settings.MaxCriticalRate, CriticalRate);
            CriticalDamage = Math.Min(Settings.MaxCriticalDamage, CriticalDamage);
            Freezing = Math.Min(Settings.MaxFreezing, Freezing);
            PoisonAttack = Math.Min(Settings.MaxPoisonAttack, PoisonAttack);
            HealthRecovery = Math.Min(Settings.MaxHealthRegen, HealthRecovery);
            PoisonRecovery = Math.Min(Settings.MaxPoisonRecovery, PoisonRecovery);
            SpellRecovery = Math.Min(Settings.MaxManaRegen, SpellRecovery);
            HpDrainRate = Math.Min((byte)100, HpDrainRate);            
        }

        private void AddTempSkills(IEnumerable<string> skillsToAdd)
        {
            foreach (var skill in skillsToAdd)
            {
                Spell spelltype;
                bool hasSkill = false;

                if (!Enum.TryParse(skill, out spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                    if (Info.Magics[i].Spell == spelltype) hasSkill = true;

                if (hasSkill) continue;

                var magic = new UserMagic(spelltype) {IsTempSpell = true};
                Info.Magics.Add(magic);
                Enqueue(magic.GetInfo());
            }
        }

        private void RemoveTempSkills(IEnumerable<string> skillsToRemove)
        {
            foreach (var skill in skillsToRemove)
            {
                Spell spelltype;
                if (!Enum.TryParse(skill, out spelltype)) return;

                for (var i = Info.Magics.Count - 1; i >= 0; i--)
                {
                    if (!Info.Magics[i].IsTempSpell || Info.Magics[i].Spell != spelltype) continue;

                    Info.Magics.RemoveAt(i);
                    Enqueue(new S.RemoveMagic {PlaceId = i});
                }
            }
        }

        private void RefreshSkills()
        {
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                UserMagic magic = Info.Magics[i];
                switch (magic.Spell)
                {
                    case Spell.Fencing:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level * 3);
                        MaxAC = (byte) Math.Min(byte.MaxValue, MaxAC + (magic.Level + 1)*3);
                        break;
                    case Spell.FatalSword:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level);
                        break;
                    case Spell.SpiritSword:
                        Accuracy = (byte)Math.Min(byte.MaxValue, Accuracy + magic.Level);
                        MaxDC = (byte) Math.Min(byte.MaxValue, MaxDC + MaxSC*(magic.Level + 1)*0.1F);
                        break;
                }
            }
        }
        private void RefreshBuffs()
        {
            for (int i = 0; i < Buffs.Count; i++)
            {
                Buff buff = Buffs[i];

                switch (buff.Type)
                {
                    case BuffType.Haste:
                        ASpeed = (sbyte)Math.Max(sbyte.MinValue, (Math.Min(sbyte.MaxValue, ASpeed + buff.Value)));
                        break;
                    case BuffType.LightBody:
                        Agility = (byte)Math.Min(byte.MaxValue, Agility + buff.Value);
                        break;
                    case BuffType.SoulShield:
                        MaxMAC = (byte)Math.Min(byte.MaxValue, MaxMAC + buff.Value);
                        break;
                    case BuffType.BlessedArmour:
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + buff.Value);
                        break;
                    case BuffType.UltimateEnhancer:
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + buff.Value);
                        break;
                    case BuffType.ProtectionField:
                        MaxAC = (byte)Math.Min(byte.MaxValue, MaxAC + buff.Value);
                        break;
                    case BuffType.Rage:
                        MaxDC = (byte)Math.Min(byte.MaxValue, MaxDC + buff.Value);
                        break;
                }

            }
        }

        public void RefreshNameColour()
        {
            Color colour = Color.White;

            if (PKPoints >= 200)
                colour = Color.Red;
            else if (Envir.Time < BrownTime)
                colour = Color.SaddleBrown;
            else if (PKPoints >= 100)
                colour = Color.Yellow;


            if (colour == NameColour) return;

            NameColour = colour;
            Enqueue(new S.ColourChanged { NameColour = NameColour });
            Broadcast(new S.ObjectColourChanged { ObjectID = ObjectID, NameColour = NameColour });
        }
        
        public void Chat(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            SMain.EnqueueChat(string.Format("{0}: {1}", Name, message));


            if (GMLogin)
            {
                if (message == GMPassword)
                {
                    IsGM = true;
                    SMain.Enqueue(string.Format("{0} is now a GM", Name));
                    ReceiveChat("You have been made a GM", ChatType.System);
                }
                else
                {
                    SMain.Enqueue(string.Format("{0} attempted a GM login", Name));
                    ReceiveChat("Incorrect login password", ChatType.System);
                }
                GMLogin = false;
                return;
            }


            string[] parts;

            message = message.Replace("$pos", Functions.PointToString(CurrentLocation));


            Packet p;
            if (message.StartsWith("/"))
            {
                //Private Message
                message = message.Remove(0, 1);
                parts = message.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) return;

                PlayerObject player = Envir.GetPlayer(parts[0]);

                if (player == null)
                {
                    ReceiveChat(string.Format("Could not find {0}.", parts[0]), ChatType.System);
                    return;
                }

                ReceiveChat(string.Format("/{0}", message), ChatType.WhisperOut);
                player.ReceiveChat(string.Format("{0}=>{1}", Name, message.Remove(0, parts[0].Length)), ChatType.WhisperIn);
            }
            else if (message.StartsWith("!!"))
            { 
                if (GroupMembers == null) return;
                 //Group
                 message = String.Format("{0}:{1}", Name, message.Remove(0, 2));

                 p = new S.ObjectChat { ObjectID = ObjectID, Text = message, Type = ChatType.Group };

                 for (int i = 0; i < GroupMembers.Count; i++)
                     GroupMembers[i].Enqueue(p);
            }
            else if (message.StartsWith("!~"))
            {
                if (MyGuild == null) return;

                //Guild
                message = message.Remove(0, 2);
                MyGuild.SendMessage(String.Format("{0}: {1}", Name, message));

            }
            else if (message.StartsWith("!"))
            {
                //Shout

                if (Envir.Time < ShoutTime)
                {
                    ReceiveChat(string.Format("You cannot shout for another {0} seconds.", Math.Ceiling((ShoutTime - Envir.Time) / 1000D)), ChatType.System);
                    return;
                }
                if (Level < 2)
                {
                    ReceiveChat("You need to be level 2 before you can shout.", ChatType.System);
                    return;
                }

                ShoutTime = Envir.Time + 10000;
                message = String.Format("(!){0}:{1}", Name, message.Remove(0, 1));

                p = new S.Chat {Message = message, Type = ChatType.Shout};

                Envir.Broadcast(p);
                 //for (int i = 0; i < CurrentMap.Players.Count; i++)
                //     CurrentMap.Players[i].Enqueue(p);

            }
            else if (message.StartsWith("@!"))
            {
                if (!IsGM) return;

                message = String.Format("(*){0}:{1}", Name, message.Remove(0, 2));

                p = new S.Chat {Message = message, Type = ChatType.Announcement};

                Envir.Broadcast(p);
            }

            else if (message.StartsWith("#"))  // GM COMMANDS
            {
                message = message.Remove(0, 1);
                parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) return;

                PlayerObject player;
                CharacterInfo data;
                String hintstring;
                UserItem item;

                switch (parts[0].ToUpper())
                {
                    case "GMKILL":
                        if (!IsGM) return;

                        if (parts.Length >= 2)
                        {
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(string.Format("Could not find {0}", parts[0]), ChatType.System);
                                return;
                            }
                            if (!player.GMNeverDie) player.Die();
                        }
                        else
                        {
                            if (!CurrentMap.ValidPoint(Front)) return;

                            Cell cell = CurrentMap.GetCell(Front);

                            if (cell == null || cell.Objects == null) return;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];

                                switch (ob.Race)
                                {
                                    case ObjectType.Player:
                                    case ObjectType.Monster:
                                        if (ob.Dead) continue;
                                        ob.EXPOwner = this;
                                        ob.ExpireTime = Envir.Time + MonsterObject.EXPOwnerDelay;
                                        ob.Die();
                                        break;
                                    default:
                                        continue;
                                }
                            }
                        }
                        return;


                    case "LOGIN":
                        GMLogin = true;
                        ReceiveChat("Please type the GM Password", ChatType.Hint);
                        return;



                    case "GMRESTORE":
                        if (!IsGM || parts.Length < 2) return;

                        data = Envir.GetCharacterInfo(parts[1]);

                        if (data == null)
                        {
                            ReceiveChat(string.Format("Player {0} was not found", parts[1]), ChatType.System);
                            return;
                        }

                        if (!data.Deleted) return;
                        data.Deleted = false;

                        ReceiveChat(string.Format("Player {0} has been restored by", data.Name), ChatType.System);
                        SMain.Enqueue(string.Format("Player {0} has been restored by {1}", data.Name, Name));

                        break;

                    case "GMCHANGEGENDER":
                        if (!IsGM) return;

                        data = parts.Length < 2 ? Info : Envir.GetCharacterInfo(parts[1]);

                        if (data == null) return;

                        switch (data.Gender)
                        {
                            case MirGender.Male:
                                data.Gender = MirGender.Female;
                                break;
                            case MirGender.Female:
                                data.Gender = MirGender.Male;
                                break;
                        }

                        ReceiveChat(string.Format("Player {0} has been changed to {1}", data.Name, data.Gender), ChatType.System);
                        SMain.Enqueue(string.Format("Player {0} has been changed to {1} by {2}", data.Name, data.Gender, Name));

                        if (data.Player != null)
                            data.Player.Connection.LogOut();

                        break;

                    case "GMLEVEL":
                        if (!IsGM || parts.Length < 2) return;

                        byte level;
                        byte old;
                        if (parts.Length >= 3)
                        {
                            if (byte.TryParse(parts[2], out level))
                            {
                                if (level == 0) return;
                                player = Envir.GetPlayer(parts[1]);
                                if (player == null) return;
                                old = player.Level;
                                player.Level = level;
                                player.LevelUp();

                                ReceiveChat(string.Format("Player {0} has been Leveled {1} -> {2}.", player.Name, old, player.Level), ChatType.System);
                                SMain.Enqueue(string.Format("Player {0} has been Leveled {1} -> {2} by {3}", player.Name, old, player.Level, Name));
                                return;
                            }
                        }
                        else
                        {
                            if (byte.TryParse(parts[1], out level))
                            {
                                if (level == 0) return;
                                old = Level;
                                Level = level;
                                LevelUp();

                                ReceiveChat(string.Format("Leveled {0} -> {1}.", old, Level), ChatType.System);
                                SMain.Enqueue(string.Format("Player {0} has been Leveled {1} -> {2} by {3}", Name, old, Level, Name));
                                return;
                            }
                        }

                        ReceiveChat("Could not level player", ChatType.System);
                        return;

                    case "GMMAKE":
                        if (!IsGM || parts.Length < 2) return;

                        ItemInfo iInfo = Envir.GetItemInfo(parts[1]);
                        if (iInfo == null) return;

                        uint count = 1;
                        if (parts.Length >= 3 && !uint.TryParse(parts[2], out count))
                            count = 1;

                        var tempCount = count;

                        while (count > 0)
                        {
                            if (iInfo.StackSize >= count)
                            {
                                item = Envir.CreateDropItem(iInfo);
                                item.Count = count;

                                if (CanGainItem(item, false)) GainItem(item);

                                return;
                            }
                            item = Envir.CreateDropItem(iInfo);
                            item.Count = iInfo.StackSize;
                            count -= iInfo.StackSize;

                            if (!CanGainItem(item, false)) return;
                            GainItem(item);
                        }

                        ReceiveChat(string.Format("{0} x{1} has been created.", iInfo.Name, tempCount), ChatType.System);
                        SMain.Enqueue(string.Format("Player {0} has attempted to Create {1} x{2}", Name, iInfo.Name, tempCount));
                        break;

                    case "GMCLEARBAG":
                        if (!IsGM) return;
                        player = this;

                        if (parts.Length >= 2)
                            player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;
                        for (int i = 0; i < player.Info.Inventory.Length; i++)
                        {
                            item = player.Info.Inventory[i];
                            if (item == null) continue;

                            player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                            player.Info.Inventory[i] = null;
                        }
                        break;
                    case "SUPER":
                    case "SUPERMAN":
                        if (!IsGM) return;

                        GMNeverDie = !GMNeverDie;

                        hintstring = GMNeverDie ? "Invincible Mode." : "Normal Mode.";
                        ReceiveChat(hintstring, ChatType.Hint);
                        break;
                    case "GM":
                    case "GAMEMASTER":
                        if (!IsGM) return;

                        GMGameMaster = !GMGameMaster;

                        hintstring = GMGameMaster ? "GameMaster Mode." : "Normal Mode.";
                        ReceiveChat(hintstring, ChatType.Hint);
                        break;
                    case "OBS":
                    case "OBSERVER":
                        if (!IsGM) return;
                        Observer = !Observer;

                        hintstring = Observer ? "Observer Mode." : "Normal Mode.";
                        ReceiveChat(hintstring, ChatType.Hint);
                        break;


                    case "GMRECALL":
                        if (!IsGM) return;

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;

                        player.Teleport(CurrentMap, Front);
                        break;


                    case "GMGO":
                        if (!IsGM) return;

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null) return;

                        Teleport(player.CurrentMap, player.CurrentLocation);
                        break;

                    case "GMMOB":
                        if (!IsGM) return;
                        if (parts.Length < 2)
                        {
                            ReceiveChat("Not enough parameters to spawn monster", ChatType.System);
                            return;
                        }

                        MonsterInfo mInfo = Envir.GetMonsterInfo(parts[1]);
                        if (mInfo == null)
                        {
                            ReceiveChat((string.Format("Monster {0} does not exist", parts[1])), ChatType.System);
                            return;
                        }

                        count = 1;
                        if (parts.Length >= 3)
                            if (!uint.TryParse(parts[2], out count) || count > 50) count = 1;

                        for (int i = 0; i < count; i++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(mInfo);
                            if (monster == null) return;
                            monster.Spawn(CurrentMap, Front);
                        }

                        ReceiveChat((string.Format("Monster {0} x{1} has been spawned.", mInfo.Name, count)), ChatType.System);
                        break;

                    case "GMRECALLMOB":
                        if (!IsGM) return;
                        if (parts.Length < 2) return;

                        MonsterInfo mInfo2 = Envir.GetMonsterInfo(parts[1]);
                        if (mInfo2 == null) return;

                        count = 1;
                        byte petlevel = 0;

                        if (parts.Length > 2)
                            if (!uint.TryParse(parts[2], out count) || count > 50) count = 1;

                        if (parts.Length > 3)
                            if (!byte.TryParse(parts[3], out petlevel) || petlevel > 7) petlevel = 0;

                        for (int i = 0; i < count; i++)
                        {
                            MonsterObject monster = MonsterObject.GetMonster(mInfo2);
                            if (monster == null) return;
                            monster.PetLevel = petlevel;
                            monster.Master = this;
                            monster.MaxPetLevel = 7;
                            monster.Direction = Direction;
                            monster.ActionTime = Envir.Time + 1000;
                            monster.Spawn(CurrentMap, Front);
                            Pets.Add(monster);
                        }

                        ReceiveChat((string.Format("Pet {0} x{1} has been recalled.", mInfo2.Name, count)), ChatType.System);
                        break;

                    case "GMRELOADDROPS":
                        if (!IsGM) return;
                        foreach (var t in Envir.MonsterInfoList)
                            t.LoadDrops();
                        ReceiveChat("Drops Reloaded.", ChatType.Hint);
                        break;

                    case "GMGIVEGOLD":
                        if (!IsGM) return;
                        if (parts.Length < 2) return;

                        player = this;

                        if (parts.Length > 2)
                        {
                            if (!uint.TryParse(parts[2], out count)) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null)
                            {
                                ReceiveChat(string.Format("Player {0} was not found.", parts[1]), ChatType.System);
                                return;
                            }
                        }

                        else if (!uint.TryParse(parts[1], out count)) return;

                        if (count + player.Account.Gold >= uint.MaxValue)
                            count = uint.MaxValue - player.Account.Gold;

                        player.GainGold(count);
                        SMain.Enqueue(string.Format("Player {0} has been given {1} gold", player.Name, count));
                        break;


                    case "GMKICK":
                        {

                        }
                        break;

                }



            }
            else if (message.StartsWith("@"))
            {   //Command
                message = message.Remove(0, 1);
                parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0) return;

                PlayerObject player;
        //        CharacterInfo data;
                String hintstring;
            //    UserItem item;

                switch (parts[0].ToUpper())
                {

                    case "MAPMOVE":
                        if (!IsGM) return;
                        if (parts.Length < 2) return;
                        var instanceID = 1;int x = 0; int y = 0;

                        if (parts.Length == 3 || parts.Length == 5)
                            int.TryParse(parts[2], out instanceID);

                        if (instanceID < 1) instanceID = 1;

                        var map = SMain.Envir.GetMapByNameAndInstance(parts[1], instanceID);
                        if (map == null)
                        {
                            ReceiveChat((string.Format("Map {0}:[{1}] could not be found", parts[1], instanceID)), ChatType.System);
                            return;
                        }

                        if (parts.Length == 4 || parts.Length == 5)
                        {
                            int.TryParse(parts[parts.Length - 2], out x);
                            int.TryParse(parts[parts.Length - 1], out y);
                        }

                        switch (parts.Length)
                        {
                            case 2:
                                ReceiveChat(TeleportRandom(200, 0, map) ? (string.Format("Moved to Map {0}", map.Info.FileName)) :
                                    (string.Format("Failed movement to Map {0}", map.Info.FileName)), ChatType.System);   
                                break;
                            case 3:
                                ReceiveChat(TeleportRandom(200, 0, map) ? (string.Format("Moved to Map {0}:[{1}]", map.Info.FileName, instanceID)) :
                                    (string.Format("Failed movement to Map {0}:[{1}]", map.Info.FileName, instanceID)), ChatType.System); 
                                break;
                            case 4:
                                ReceiveChat(Teleport(map, new Point(x, y)) ? (string.Format("Moved to Map {0} at {1}:{2}", map.Info.FileName, x, y)): 
                                    (string.Format("Failed movement to Map {0} at {1}:{2}", map.Info.FileName, x, y)), ChatType.System);
                                break;
                            case 5:
                                ReceiveChat(Teleport(map, new Point(x, y)) ? (string.Format("Moved to Map {0}:[{1}] at {2}:{3}", map.Info.FileName, instanceID, x, y)) :
                                    (string.Format("Failed movement to Map {0}:[{1}] at {2}:{3}", map.Info.FileName, instanceID, x, y)), ChatType.System);
                                break;
                        }
                        break;

                    case "ALLOWGUILD":
                        EnableGuildInvite = !EnableGuildInvite;
                        hintstring = EnableGuildInvite ? "Guild invites enabled." : "Guild invites disabled.";
                        ReceiveChat(hintstring, ChatType.Hint);
                        break;
                  
                    case "ENABLEGROUPRECALL":
                        EnableGroupRecall = !EnableGroupRecall;
                        hintstring = EnableGroupRecall ? "Group Recall Enabled." : "Group Recall Disabled.";
                        ReceiveChat(hintstring, ChatType.Hint);
                        break;

                    case "GROUPRECALL":
                        if (GroupMembers == null || GroupMembers[0] != this || Dead)
                            return;

                        if (CurrentMap.Info.NoRecall)
                        {
                            ReceiveChat("You cannot recall people on this map", ChatType.System);
                            return;
                        }

                        if (Envir.Time < LastRecallTime)
                        {
                            ReceiveChat(string.Format("You cannot recall for another {0} seconds", (LastRecallTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                        if (ItemSets.Any(set => set.Set == ItemSet.Recall && set.SetComplete))
                        {
                            LastRecallTime = Envir.Time + 180000;
                            for (var i = 1; i < GroupMembers.Count(); i++)
                            {
                                if (GroupMembers[i].EnableGroupRecall)
                                    GroupMembers[i].Teleport(CurrentMap, CurrentLocation);
                                else
                                    GroupMembers[i].ReceiveChat("A recall was attempted without your permission",
                                        ChatType.System);
                            }
                        }
                        break;
                    case "RECALLMEMBER":
                        if (GroupMembers == null || GroupMembers[0] != this)
                        {
                            ReceiveChat("You are not a group leader.", ChatType.System);
                            return;
                        }

                        if (Dead)
                        {
                            ReceiveChat("You cannot recall when you are dead.", ChatType.System);
                            return;
                        }

                        if (Envir.Time < LastRecallTime)
                        {
                            ReceiveChat(string.Format("You cannot recall for another {0} seconds", (LastRecallTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }
                        if (ItemSets.Any(set => set.Set == ItemSet.Recall && set.SetComplete))
                        {
                            if (parts.Length < 2) return;
                            player = Envir.GetPlayer(parts[1]);

                            if (player == null || !IsMember(player) || this == player)
                            {
                                ReceiveChat((string.Format("Player {0} could not be found", parts[1])), ChatType.System);
                                return;
                            }
                            if (!player.EnableGroupRecall)
                            {
                                player.ReceiveChat("A recall was attempted without your permission",
                                        ChatType.System);
                                ReceiveChat((string.Format("{0} is blocking grouprecall", player.Name)), ChatType.System);
                                return;
                            }
                            LastRecallTime = Envir.Time + 60000;

                            if (!player.Teleport(CurrentMap, Front))
                                player.Teleport(CurrentMap, CurrentLocation);
                        }
                        else
                        {
                            ReceiveChat("You cannot recall without a recallset.", ChatType.System);
                            return;
                        }
                        break;

                    case "DATE":
                        ReceiveChat(string.Format("The time is : {0}", DateTime.Now.ToString("hh:mm tt")), ChatType.System);
                        break;

                    case "MAP":
                        var mapName = CurrentMap.Info.FileName;
                        var mapTitle = CurrentMap.Info.Title;
                        ReceiveChat((string.Format("You are currently in {0}. Map ID: {1}", mapTitle, mapName)), ChatType.System);
                        break;

                    case "MOVE":
                        if (!IsGM && !HasTeleportRing) return;
                        if (!IsGM && CurrentMap.Info.NoPosition)
                        {
                            ReceiveChat(("You cannot position move on this map"), ChatType.System);
                            return;
                        }
                        if (Envir.Time < LastTeleportTime)
                        {
                            ReceiveChat(string.Format("You cannot teleport for another {0} seconds", (LastTeleportTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                     //   int f = 0, g;

                        if (parts.Length <= 2 || !int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y))
                        {
                            if (!IsGM)
                                LastTeleportTime = Envir.Time + 180000;
                            TeleportRandom(200, 0);
                            return;
                        }
                        if (!IsGM)
                            LastTeleportTime = Envir.Time + 180000;
                        Teleport(CurrentMap, new Point(x, y));
                        break;

                    
                    case "FIND":
                        if (!IsGM && !HasProbeNecklace) return;

                        if (Envir.Time < LastProbeTime)
                        {
                            ReceiveChat(string.Format("You cannot search for another {0} seconds", (LastProbeTime - Envir.Time) / 1000), ChatType.System);
                            return;
                        }

                        if (parts.Length < 2) return;
                        player = Envir.GetPlayer(parts[1]);

                        if (player == null)
                        {
                            ReceiveChat(parts[1] + " is not online", ChatType.System);
                            return;
                        }
                        if (player.CurrentMap == null) return;
                        if (!IsGM)
                            LastProbeTime = Envir.Time + 180000;
                        ReceiveChat((string.Format("{0} is located at {1} ({2},{3})", player.Name, player.CurrentMap.Info.Title, player.CurrentLocation.X, player.CurrentLocation.Y)), ChatType.System);
                        break;
                    case "LEAVEGUILD":
                        if (MyGuild == null) return;
                        if (MyGuildRank == null) return;
                        MyGuild.DeleteMember(this, Name);
                        break;
                    case "CREATEGUILD":
                        if (!IsGM) return;
                        if (parts.Length < 3) return;
                        player = Envir.GetPlayer(parts[1]);
                        if (player == null)
                        {
                            ReceiveChat(string.Format("Player {0} was not found.", parts[1]), ChatType.System);
                            return;
                        }
                        if (player.MyGuild != null)
                        {
                            ReceiveChat(string.Format("Player {0} is already in a guild.", parts[1]), ChatType.System);
                            return;
                        }
                        if ((parts[2].Length < 3) || (parts[2].Length > 20))
                        {
                            ReceiveChat("Guildname is restricted to 3-20 characters.", ChatType.System);
                            return;
                        }
                        GuildObject guild = Envir.GetGuild(parts[2]);
                        if (guild != null)
                        {
                            ReceiveChat(string.Format("Guild {0} already exists.", parts[2]), ChatType.System);
                            return;
                        }
                        player.CanCreateGuild = true;
                        if (player.CreateGuild(parts[2]))
                            ReceiveChat(string.Format("Succesfully created guild {0}", parts[2]), ChatType.System);
                        else
                            ReceiveChat("Failed to create guild", ChatType.System);
                        player.CanCreateGuild = false;
                        break;
                }
            }
            else
            {
                //FAR - NPC Listener code
                //if (NPCListener != null && NPCListener.Active)
                //{
                //    if (NPCID == NPCListener.NPCID)
                //    {
                //        NPCPage.AddVariable(this, NPCListener.NPCVariable, message);
                //        NPCListener.Active = false;

                //        NPCJumpPage = new NPCJumpPage
                //        {
                //            NPCID = NPCListener.NPCID,
                //            TimePeriod = 0,
                //            NPCGotoPage = "[" + NPCListener.NPCGotoPage + "]"
                //        };
                //    }
                //    else
                //    {
                //        NPCListener.Active = false;
                //    }
                //}

                message = String.Format("{0}:{1}", CurrentMap.Info.NoNames ? "?????" : Name, message);

                p = new S.ObjectChat { ObjectID = ObjectID, Text = message, Type = ChatType.Normal };

                Enqueue(p);
                Broadcast(p);
            }
        }


        public void Turn(MirDirection dir)
        {
            if (CanMove)
            {
                ActionTime = Envir.Time + TurnDelay;

                Direction = dir;
                if (CheckMovement(CurrentLocation)) return;

                SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

                if (szi != null)
                {
                    BindLocation = szi.Location;
                    BindMapIndex = CurrentMapIndex;
                    InSafeZone = true;
                }
                else
                    InSafeZone = false;

                Cell cell = CurrentMap.GetCell(CurrentLocation);

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    ob.ProcessSpell(this);
                    break;
                }

                Broadcast(new S.ObjectTurn {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});
            }

            Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
        }
        public void Harvest(MirDirection dir)
        {

            if (!CanMove)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            ActionTime = Envir.Time + HarvestDelay;

            Direction = dir;

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectHarvest { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            Point front = Front;
            bool send = false;
            for (int d = 0; d <= 1; d++)
            {
                for (int y = front.Y - d; y <= front.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = front.X - d; x <= front.X + d; x += Math.Abs(y - front.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;
                        if (!CurrentMap.ValidPoint(x, y)) continue;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (cell.Objects == null) continue;

                        for (int i = 0; i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Monster || !ob.Dead || ob.Harvested) continue;

                            if (ob.EXPOwner != null && ob.EXPOwner != this && !IsMember(ob))
                            {
                                send = true;
                                continue;
                            }

                            if (ob.Harvest(this)) return;
                        }
                    }
                }
            }

            if (send)
                ReceiveChat("You do not own any nearby carcasses.", ChatType.System);
        }

        public void Walk(MirDirection dir)
        {

            if (!CanMove || !CanWalk)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            Point location = Functions.PointMove(CurrentLocation, dir, 1);

            if (!CurrentMap.ValidPoint(location))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            Cell cell = CurrentMap.GetCell(location);
            if (cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!ob.Blocking || ob.CellTime >= Envir.Time) continue;

                    Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
                    return;
                }

            if (Hidden && !HasClearRing)
            {
                Hidden = false;

                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Hiding) continue;

                    Buffs[i].ExpireTime = 0;
                    break;
                }
            }

            Direction = dir;
            if (CheckMovement(location)) return;

            CurrentMap.GetCell(CurrentLocation).Remove(this);
            RemoveObjects(dir, 1);

            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 1);



            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                BindLocation = szi.Location;
                BindMapIndex = CurrentMapIndex;
                InSafeZone = true;
            }
            else
                InSafeZone = false;


            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + MoveDelay;

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectWalk { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});


            cell = CurrentMap.GetCell(CurrentLocation);

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                if (cell.Objects[i].Race != ObjectType.Spell) continue;
                SpellObject ob = (SpellObject)cell.Objects[i];

                ob.ProcessSpell(this);
                break;
            }

        }
        public void Run(MirDirection dir)
        {
            if (!CanMove || !CanWalk)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            Point location = Functions.PointMove(CurrentLocation, dir, 1);

            if (!CurrentMap.ValidPoint(location))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            Cell cell = CurrentMap.GetCell(location);

            if (cell.Objects != null)
            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (!ob.Blocking || ob.CellTime >= Envir.Time) continue;

                Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
                return;
            }
            location = Functions.PointMove(CurrentLocation, dir, 2);

            if (!CurrentMap.ValidPoint(location))
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            cell = CurrentMap.GetCell(location);

            if (cell.Objects != null)
            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (!ob.Blocking || ob.CellTime >= Envir.Time) continue;

                Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
                return;
            }

            if (Hidden && !HasClearRing)
            {
                Hidden = false;

                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Hiding) continue;

                    Buffs[i].ExpireTime = 0;
                    break;
                }
            }

            Direction = dir;
            if (CheckMovement(location)) return;

            CurrentMap.GetCell(CurrentLocation).Remove(this);
            RemoveObjects(dir, 2);

            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 2);


            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                BindLocation = szi.Location;
                BindMapIndex = CurrentMapIndex;
                InSafeZone = true;
            }
            else
                InSafeZone = false;


            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + MoveDelay;

            _runCounter++;
            if (_runCounter > 10)
            {
                _runCounter -= 8;
                ChangeHP(-1);
            }

            Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
            Broadcast(new S.ObjectRun {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});


            cell = CurrentMap.GetCell(CurrentLocation);

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                if (cell.Objects[i].Race != ObjectType.Spell) continue;
                SpellObject ob = (SpellObject)cell.Objects[i];

                ob.ProcessSpell(this);
                break;
            }


        }
        public override int Pushed(MapObject pusher, MirDirection dir, int distance)
        {
            int result = 0;
            MirDirection reverse = Functions.ReverseDirection(dir);
            Cell cell;
            for (int i = 0; i < distance; i++)
            {
                Point location = Functions.PointMove(CurrentLocation, dir, 1);

                if (!CurrentMap.ValidPoint(location)) return result;

                cell = CurrentMap.GetCell(location);

                bool stop = false;
                if (cell.Objects != null)
                    for (int c = 0; c < cell.Objects.Count; c++)
                    {
                        MapObject ob = cell.Objects[c];
                        if (!ob.Blocking) continue;
                        stop = true;
                    }
                if (stop) break;
                
                CurrentMap.GetCell(CurrentLocation).Remove(this);

                Direction = reverse;
                RemoveObjects(dir, 1);
                CurrentLocation = location;
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(dir, 1);

                Enqueue(new S.Pushed { Direction = Direction, Location = CurrentLocation });
                Broadcast(new S.ObjectPushed { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
                
                result++;
            }

            if (result > 0)
            {
                cell = CurrentMap.GetCell(CurrentLocation);

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    if (cell.Objects[i].Race != ObjectType.Spell) continue;
                    SpellObject ob = (SpellObject)cell.Objects[i];

                    ob.ProcessSpell(this);
                    break;
                }
            }

            ActionTime = Envir.Time + 500;
            return result;
        }

        public void Attack(MirDirection dir, Spell spell)
        {
            bool Mined = false;
            if (!CanAttack)
            {
                switch (spell)
                {
                    case Spell.Slaying:
                        Slaying = false;
                        break;
                }

                Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
                return;
            }

            byte level = 0;
            UserMagic magic;
            switch (spell)
            {
                case Spell.Slaying:
                    if (!Slaying)
                        spell = Spell.None;
                    else
                    {
                        magic = GetMagic(Spell.Slaying);
                        level = magic.Level;
                    }

                    Slaying = false;
                    break;
                case Spell.DoubleSlash:
                    magic = GetMagic(spell);
                    if (magic == null || magic.Info.BaseCost + (magic.Level*magic.Info.LevelCost) > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level*magic.Info.LevelCost));
                    break;
                case Spell.Thrusting:
                case Spell.FlamingSword:
                    magic = GetMagic(spell);
                    if (magic == null)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    break;
                case Spell.HalfMoon:
                case Spell.CrossHalfMoon:
                    magic = GetMagic(spell);
                    if (magic == null || magic.Info.BaseCost + (magic.Level*magic.Info.LevelCost) > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level*magic.Info.LevelCost));
                    break;
                case Spell.TwinDrakeBlade:
                    magic = GetMagic(spell);
                    if (!TwinDrakeBlade || magic == null || magic.Info.BaseCost + magic.Level*magic.Info.LevelCost > MP)
                    {
                        spell = Spell.None;
                        break;
                    }
                    level = magic.Level;
                    ChangeMP(-(magic.Info.BaseCost + magic.Level*magic.Info.LevelCost));
                    break;
                default:
                    spell = Spell.None;
                    break;
            }


            if (!Slaying)
            {
                magic = GetMagic(Spell.Slaying);

                if (magic != null && Envir.Random.Next(12) <= magic.Level)
                {
                    Slaying = true;
                    Enqueue(new S.SpellToggle {Spell = Spell.Slaying, CanUse = Slaying});
                }
            }

            Direction = dir;


            Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});
            Broadcast(new S.ObjectAttack {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = spell, Level = level});

            AttackTime = Envir.Time + AttackSpeed;
            ActionTime = Envir.Time + 550;
            RegenTime = Envir.Time + RegenDelay;

            Point target = Functions.PointMove(CurrentLocation, dir, 1);

            int damage = GetAttackPower(MinDC, MaxDC);
            if (!CurrentMap.ValidPoint(target))
            {
                switch (spell)
                {
                    case Spell.Thrusting:
                        goto Thrusting;
                    case Spell.HalfMoon:
                        goto HalfMoon;
                    case Spell.CrossHalfMoon:
                        goto CrossHalfMoon;
                    case Spell.None:
                        Mined = true;
                        goto Mining;
                }
                return;
            }

            Cell cell = CurrentMap.GetCell(target);

            if (cell.Objects == null)
            {
                switch (spell)
                {
                    case Spell.Thrusting:
                        goto Thrusting;
                    case Spell.HalfMoon:
                        goto HalfMoon;
                    case Spell.CrossHalfMoon:
                        goto CrossHalfMoon;
                }
                return;
            }


            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                if (!ob.IsAttackTarget(this)) continue;

                //Only targets
                if (ob.Undead)
                    damage = Math.Min(int.MaxValue, damage + Holy);
                magic = GetMagic(Spell.FatalSword);

                DefenceType defence = DefenceType.ACAgility;

                if (magic != null)
                {
                    if (FatalSword)
                    {
                        damage += (magic.Level + 1)*5;
                        LevelMagic(magic);
                        S.ObjectEffect p = new S.ObjectEffect {ObjectID = ob.ObjectID, Effect = SpellEffect.FatalSword};

                        defence = DefenceType.Agility;
                        CurrentMap.Broadcast(p, ob.CurrentLocation);

                        FatalSword = false;
                    }

                    if (!FatalSword && Envir.Random.Next(10) == 0)
                        FatalSword = true;
                }

                DelayedAction action;
                switch (spell)
                {
                    case Spell.Slaying:
                        magic = GetMagic(Spell.Slaying);
                        damage += (magic.Level + 1)*8;
                        LevelMagic(magic);
                        break;
                    case Spell.DoubleSlash:
                        magic = GetMagic(Spell.DoubleSlash);
                        damage = damage*(magic.Level + 8)/10; // 110% Damage level 3

                        if (defence == DefenceType.ACAgility) defence = DefenceType.MACAgility;

                        action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, ob, damage, DefenceType.Agility, true);
                        ActionList.Add(action);
                        LevelMagic(magic);
                        break;
                    case Spell.Thrusting:
                        magic = GetMagic(Spell.Thrusting);
                        LevelMagic(magic);
                        break;
                    case Spell.HalfMoon:
                        magic = GetMagic(Spell.HalfMoon);
                        LevelMagic(magic);
                        break;
                    case Spell.CrossHalfMoon:
                        magic = GetMagic(Spell.CrossHalfMoon);
                        LevelMagic(magic);
                        break;
                    case Spell.TwinDrakeBlade:
                        magic = GetMagic(Spell.TwinDrakeBlade);
                        damage = damage*(magic.Level + 8)/10; // 110% Damage level 3
                        TwinDrakeBlade = false;
                        action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, ob, damage, DefenceType.Agility, true);
                        ActionList.Add(action);
                        LevelMagic(magic);

                        if ((((ob.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonAttackWeight) >= ob.PoisonResist)) && (ob.Level < Level + 10 && Envir.Random.Next(ob.Race == ObjectType.Player ? 40 : 20) <= magic.Level + 1))
                        {
                            ob.ApplyPoison(new Poison {PType = PoisonType.Stun, Duration = ob.Race == ObjectType.Player ? 2 : 2 + magic.Level, TickSpeed = 1000}, this);
                            ob.Broadcast(new S.ObjectEffect {ObjectID = ob.ObjectID, Effect = SpellEffect.TwinDrakeBlade});
                        }

                        break;
                    case Spell.FlamingSword:
                        magic = GetMagic(Spell.FlamingSword);
                        damage = damage + (damage / 100 * ((4 + magic.Level * 4) * 10));
                        FlamingSword = false;
                        action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, ob, damage, DefenceType.Agility, true);
                        ActionList.Add(action);
                        LevelMagic(magic);

                        break;
                }

                if (ob.Attacked(this, damage, defence) <= 0) break;


                //Level Fencing / SpiritSword
                for (int m = 0; m < Info.Magics.Count; m++)
                {
                    magic = Info.Magics[m];
                    switch (magic.Spell)
                    {
                        case Spell.Fencing:
                        case Spell.SpiritSword:
                            LevelMagic(magic);
                            break;
                    }
                }
                break;
            }

            Thrusting:
            if (spell == Spell.Thrusting)
            {
                target = Functions.PointMove(target, dir, 1);

                if (!CurrentMap.ValidPoint(target)) return;

                cell = CurrentMap.GetCell(target);

                if (cell.Objects == null) return;

                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                    if (!ob.IsAttackTarget(this)) continue;

                    magic = GetMagic(spell);
                    damage = damage*(magic.Level + 1)/4;

                    ob.Attacked(this, damage, DefenceType.Agility);
                    break;
                }


            }
            HalfMoon:
            if (spell == Spell.HalfMoon)
            {
                dir = Functions.PreviousDir(dir);
                magic = GetMagic(spell);
                damage = damage*(magic.Level + 3)/10;
                for (int i = 0; i < 4; i++)
                {
                    target = Functions.PointMove(CurrentLocation, dir, 1);
                    dir = Functions.NextDir(dir);
                    if (target == Front) continue;

                    if (!CurrentMap.ValidPoint(target)) continue;

                    cell = CurrentMap.GetCell(target);

                    if (cell.Objects == null) continue;

                    for (int o = 0; o < cell.Objects.Count; o++)
                    {
                        MapObject ob = cell.Objects[o];
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                        if (!ob.IsAttackTarget(this)) continue;

                        ob.Attacked(this, damage, DefenceType.Agility);
                        break;
                    }
                }
            }

            CrossHalfMoon:
            if (spell == Spell.CrossHalfMoon)
            {
                magic = GetMagic(spell);
                damage = damage*(magic.Level + 4)/10;
                for (int i = 0; i < 8; i++)
                {
                    target = Functions.PointMove(CurrentLocation, dir, 1);
                    dir = Functions.NextDir(dir);
                    if (target == Front) continue;

                    if (!CurrentMap.ValidPoint(target)) continue;

                    cell = CurrentMap.GetCell(target);

                    if (cell.Objects == null) continue;

                    for (int o = 0; o < cell.Objects.Count; o++)
                    {
                        MapObject ob = cell.Objects[o];
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster) continue;
                        if (!ob.IsAttackTarget(this)) continue;

                        ob.Attacked(this, damage, DefenceType.Agility);
                        break;
                    }
                }
            }
            Mining:
            if (Mined)
            {
                if(Info.Equipment[(int)EquipmentSlot.Weapon] == null) return;
                if(!Info.Equipment[(int)EquipmentSlot.Weapon].Info.CanMine) return;
                if (CurrentMap.Mine == null) return;
                MineSpot Mine = CurrentMap.Mine[target.X, target.Y];
                if ((Mine == null) || (Mine.Mine == null)) return;
                if (Mine.StonesLeft > 0)
                {
                    Mine.StonesLeft--;
                    if (Envir.Random.Next(100) <= (Mine.Mine.HitRate + (Info.Equipment[(int)EquipmentSlot.Weapon].Info.Accuracy + Info.Equipment[(int)EquipmentSlot.Weapon].Accuracy)*10))
                    {
                        //create some rubble on the floor (or increase whats there)
                        SpellObject Rubble = null;
                        Cell minecell = CurrentMap.GetCell(CurrentLocation);
                        for (int i = 0; i < minecell.Objects.Count; i++)
                        {
                            if (minecell.Objects[i].Race != ObjectType.Spell) continue;
                            SpellObject ob = (SpellObject)minecell.Objects[i];

                            if (ob.Spell != Spell.Rubble) continue;
                            Rubble = ob;
                            Rubble.ExpireTime = Envir.Time + (5 * 60 * 1000);
                            break;
                        }
                        if (Rubble == null)
                        {
                            Rubble = new SpellObject
                            {
                                Spell = Spell.Rubble,
                                Value = 1,
                                ExpireTime = Envir.Time + (5 * 60 * 1000),
                                TickSpeed = 2000,
                                Caster = null,
                                CurrentLocation = CurrentLocation,
                                CurrentMap = this.CurrentMap,
                                Direction = MirDirection.Up
                            };
                            CurrentMap.AddObject(Rubble);
                            Rubble.Spawned();
                        }
                        if (Rubble != null)
                            ActionList.Add(new DelayedAction(DelayedType.Mine, Envir.Time + 400,Rubble));
                        //check if we get a payout
                        if (Envir.Random.Next(100) <= Mine.Mine.DropRate)
                        {
                            GetMinePayout(Mine.Mine);
                        }
                        DamageItem(Info.Equipment[(int)EquipmentSlot.Weapon], 5 + Envir.Random.Next(15));
                    }
                }
                else
                {
                    if (Envir.Time > Mine.LastRegenTick)
                    {
                        Mine.LastRegenTick = Envir.Time + Mine.Mine.SpotRegenRate * 60 * 1000;
                        Mine.StonesLeft = (byte)Envir.Random.Next(Mine.Mine.MaxStones);
                    }
                }
            }
        }

        public void GetMinePayout(MineSet Mine)
        {
            if ((Mine.Drops == null) || (Mine.Drops.Count == 0)) return;
            if (FreeSpace(Info.Inventory) == 0) return;
            byte Slot = (byte)Envir.Random.Next(Mine.TotalSlots);
            for (int i = 0; i < Mine.Drops.Count; i++)
            {
                MineDrop Drop = Mine.Drops[i];
                if ((Drop.MinSlot <= Slot) && (Drop.MaxSlot >= Slot) && (Drop.Item != null))
                {
                    UserItem item = Envir.CreateDropItem(Drop.Item);
                    if (item.Info.Type == ItemType.Ore)
                    {
                        item.CurrentDura = (ushort)Math.Min(ushort.MaxValue,(Drop.MinDura + Envir.Random.Next(Math.Max(0,Drop.MaxDura - Drop.MinDura))) * 1000);
                        if ((Drop.BonusChance > 0) && (Envir.Random.Next(100) <= Drop.BonusChance))
                            item.CurrentDura = (ushort)Math.Min(ushort.MaxValue,item.CurrentDura + (Envir.Random.Next(Drop.MaxBonusDura) * 1000));
                    }
                    if (CanGainItem(item, false)) GainItem(item);
                    return;
                }
            }
            
        }

        public void Magic(Spell spell, MirDirection dir, uint targetID, Point location)
        {

            if (!CanCast)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            AttackTime = Envir.Time + MoveDelay;
            SpellTime = Envir.Time + 1800; //Spell Delay
            ActionTime = Envir.Time + MoveDelay;

            UserMagic magic = GetMagic(spell);

            if (magic == null)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            int cost = magic.Info.BaseCost + magic.Info.LevelCost*magic.Level;

            if (spell == Spell.Teleport)
                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Teleport) continue;
                    cost += (int) (MaxMP*0.3F);
                    break;
                }

            if (cost > MP)
            {
                Enqueue(new S.UserLocation { Direction = Direction, Location = CurrentLocation });
                return;
            }

            RegenTime = Envir.Time + RegenDelay;
            ChangeMP(-cost);

            Direction = dir;
            if (spell != Spell.ShoulderDash)
                Enqueue(new S.UserLocation {Direction = Direction, Location = CurrentLocation});

            MapObject target = null;

            if (targetID == ObjectID)
                target = this;
            else if (targetID > 0)
                target = FindObject(targetID, 10);

            bool cast = true;
            byte level = magic.Level;
            switch (spell)
            {
                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.FrostCrunch:
                    if (!Fireball(target, magic)) targetID = 0;
                    break;
                case Spell.Healing:
                    if (target == null)
                    {
                        target = this;
                        targetID = ObjectID;
                    }
                    Healing(target, magic);
                    break;
                case Spell.Repulsion:
                case Spell.EnergyRepulsor:
                case Spell.FireBurst:
                    Repulsion(magic);
                    break;
                case Spell.ElectricShock:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target as MonsterObject));
                    break;
                case Spell.Poisoning:
                    if (!Poisoning(target, magic)) cast = false;
                    break;
                case Spell.HellFire:
                    HellFire(magic);
                    break;
                case Spell.ThunderBolt:
                    ThunderBolt(target, magic);
                    break;
                case Spell.SoulFireBall:
                    if (!SoulFireball(target, magic, out cast)) targetID = 0;
                    break;
                case Spell.SummonSkeleton:
                    SummonSkeleton(magic);
                    break;
                case  Spell.Teleport:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 200, magic, location));
                    break;
                case Spell.Hiding:
                    Hiding(magic);
                    break;
                case Spell.Haste:
                case Spell.LightBody:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic));
                    break;
                case Spell.FireBang:
                case Spell.IceStorm:
                    FireBang(magic, target == null ? location : target.CurrentLocation);
                    break;
                case Spell.MassHiding:
                    MassHiding(magic, target == null ? location : target.CurrentLocation, out cast);
                    break;
                case Spell.SoulShield:
                case Spell.BlessedArmour:
                    SoulShield(magic, target == null ? location : target.CurrentLocation, out cast);
                    break;
                case Spell.FireWall:
                    FireWall(magic, target == null ? location : target.CurrentLocation);
                    break;
                case Spell.Lightning:
                    Lightning(magic);
                    break;
                case Spell.HeavenlySword:
                    HeavenlySword(magic);
                    break;
                case Spell.MassHealing:
                    MassHealing(magic, target == null ? location : target.CurrentLocation);
                    break;
                case Spell.ShoulderDash:
                    ShoulderDash(magic);
                    return;
                case Spell.ThunderStorm:
                case Spell.FlameField:
                    ThunderStorm(magic);
                    if (spell == Spell.FlameField)
                        SpellTime = Envir.Time + 2500; //Spell Delay
                    break;
                case Spell.MagicShield:
                    ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, magic.GetPower(GetAttackPower(MinMC, MaxMC) + 15)));
                    break;
                case Spell.FlameDisruptor:
                    FlameDisruptor(target, magic);
                    break;
                case Spell.TurnUndead:
                    TurnUndead(target, magic);
                    break;
                case Spell.Vampirism:
                    Vampirism(target, magic);
                    break;
                case Spell.SummonShinsu:
                    SummonShinsu(magic);
                    break;
                case Spell.Purification:
                    if (target == null)
                    {
                        target = this;
                        targetID = ObjectID;
                    }
                    Purification(target, magic);
                    break;
                case Spell.LionRoar:
                    CurrentMap.ActionList.Add(new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, CurrentLocation));
                    break;
                case Spell.Revelation:
                    Revelation(target, magic);
                    break;
                case Spell.PoisonField:
                    PoisonField(magic, target == null ? location : target.CurrentLocation, out cast);
                    break;
                case Spell.Entrapment:
                    Entrapment(target, magic);
                    break;
                case Spell.BladeAvalanche:
                    BladeAvalanche(magic);
                    break;
                case Spell.ProtectionField:
                    ProtectionField(magic);
                    break;
                case Spell.Rage:
                    Rage(magic);
                    break;
                case Spell.Mirroring:
                    Mirroring(magic);
                    break;
                case Spell.Blizzard:
                    Blizzard(magic, target == null ? location : target.CurrentLocation, out cast);
                    break;
                case Spell.MeteorStrike:
                    MeteorStrike(magic, target == null ? location : target.CurrentLocation, out cast);
                    break;
                default :
                    cast = false;
                    break;
            }

            Enqueue(new S.Magic { Spell = spell, TargetID = targetID, Target = location, Cast = cast, Level = level });
            Broadcast(new S.ObjectMagic { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = spell, TargetID = targetID, Target = location, Cast = cast, Level = level });
        }


        private void ShoulderDash(UserMagic magic)
        {
            if (InTrapRock) return;
            if (!CanWalk) return;

            int dist = Envir.Random.Next(2) + magic.Level + 2;
            int travel = 0;
            bool wall = true;
            Point location = CurrentLocation;
            MapObject target = null;
            for (int i = 0; i < dist; i++)
            {
                location = Functions.PointMove(location, Direction, 1);

                if (!CurrentMap.ValidPoint(location)) break;
                

                Cell cell = CurrentMap.GetCell(location);
                
                bool blocking = false;
                if (cell.Objects != null)
                {
                    for (int c = cell.Objects.Count - 1; c >= 0; c--)
                    {
                        MapObject ob = cell.Objects[c];
                        if (!ob.Blocking) continue;
                        wall = false;
                        if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                        {
                            blocking = true;
                            break;
                        }

                        if (target == null && ob.Race == ObjectType.Player)
                            target = ob;

                        if (Envir.Random.Next(20) >= 6 + magic.Level*3 + Level - ob.Level || !ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                        {
                            if (target == ob)
                                target = null;
                            blocking = true;
                            break;
                        }

                        if (cell.Objects == null) break;

                    }
                }

                if (blocking)
                {
                    if (magic.Level != 3) break;

                    Point location2 = Functions.PointMove(location, Direction, 1);

                    if (!CurrentMap.ValidPoint(location2)) break;

                    cell = CurrentMap.GetCell(location2);

                    blocking = false;
                    if (cell.Objects != null)
                    {
                        for (int c = cell.Objects.Count - 1; c >= 0; c--)
                        {
                            MapObject ob = cell.Objects[c];
                            if (!ob.Blocking) continue;
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                            {
                                blocking = true;
                                break;
                            }

                            if (!ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                            {
                                blocking = true;
                                break;
                            }

                            if (cell.Objects == null) break;
                        }
                    }

                    if (blocking) break;

                    cell = CurrentMap.GetCell(location);
                    
                    if (cell.Objects != null)
                    {
                        for (int c = cell.Objects.Count - 1; c >= 0; c--)
                        {
                            MapObject ob = cell.Objects[c];
                            if (!ob.Blocking) continue;
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player)
                            {
                                blocking = true;
                                break;
                            }

                            if (Envir.Random.Next(20) >= 6 + magic.Level * 3 + Level - ob.Level || !ob.IsAttackTarget(this) || ob.Level >= Level || ob.Pushed(this, Direction, 1) == 0)
                            {
                                blocking = true;
                                break;
                            }

                            if (cell.Objects == null) break;
                        }
                    }

                    if (blocking) break;
                }

                travel++;
                CurrentMap.GetCell(CurrentLocation).Remove(this);
                RemoveObjects(Direction, 1);
                
                CurrentLocation = location;

                Enqueue(new S.UserDash { Direction = Direction, Location = location });
                Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = location });
                
                CurrentMap.GetCell(CurrentLocation).Add(this);
                AddObjects(Direction, 1);
            }

            if (travel > 0 && !wall)
            {
                if (target != null) target.Attacked(this, magic.Level + 1, DefenceType.None, false);
                LevelMagic(magic);
            }

            if (travel > 0)
            {

                Cell cell = CurrentMap.GetCell(CurrentLocation);
                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        if (cell.Objects[i].Race != ObjectType.Spell) continue;
                        SpellObject ob = (SpellObject) cell.Objects[i];

                        if (ob.Spell != Spell.FireWall || !IsAttackTarget(ob.Caster)) continue;
                        Attacked(ob.Caster, ob.Value, DefenceType.MAC, false);
                        break;
                    }
            }

            if (travel == 0 || wall && dist != travel)
            {
                if (travel > 0)
                {
                    Enqueue(new S.UserDash { Direction = Direction, Location = Front });
                    Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = Front });
                }
                else
                    Broadcast(new S.ObjectDash { ObjectID = ObjectID, Direction = Direction, Location = Front });

                Enqueue(new S.UserDashFail {Direction = Direction, Location = CurrentLocation});
                Broadcast(new S.ObjectDashFail {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});
                ReceiveChat("Not enough pushing Power.", ChatType.System);
            }

            CellTime = Envir.Time + 500;
        }
        
        private bool Fireball(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);
            
            ActionList.Add(action);

            return true;
        }
        private void Repulsion(UserMagic magic)
        {
            bool result = false;
            for (int d = 0; d <= 1; d++)
            {
                for (int y = CurrentLocation.Y - d; y <= CurrentLocation.Y + d; y++)
                {
                    if (y < 0) continue;
                    if (y >= CurrentMap.Height) break;

                    for (int x = CurrentLocation.X - d; x <= CurrentLocation.X + d; x += Math.Abs(y - CurrentLocation.Y) == d ? 1 : d * 2)
                    {
                        if (x < 0) continue;
                        if (x >= CurrentMap.Width) break;

                        Cell cell = CurrentMap.GetCell(x, y);
                        if (!cell.Valid || cell.Objects == null) continue;

                        for (int i = 0; cell.Objects != null && i < cell.Objects.Count; i++)
                        {
                            MapObject ob = cell.Objects[i];
                            if (ob.Race != ObjectType.Monster && ob.Race != ObjectType.Player) continue;

                            if (!ob.IsAttackTarget(this) || ob.Level >= Level) continue;

                            if (Envir.Random.Next(20) >= 6 + magic.Level*3 + Level - ob.Level) continue;

                            int distance = 1 + Math.Max(0, magic.Level - 1) + Envir.Random.Next(2);
                            MirDirection dir = Functions.DirectionFromPoint(CurrentLocation, ob.CurrentLocation);

                            if (ob.Pushed(this, dir, distance) == 0) continue;

                            if (ob.Race == ObjectType.Player)
                                ob.Attacked(this, magic.Level + 1, DefenceType.None, false);
                            result = true;
                        }
                    }
                }
            }

            if (result) LevelMagic(magic);
        }
        private void ElectricShock(MonsterObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            if (Envir.Random.Next(4 - magic.Level) > 0)
            {
                if (Envir.Random.Next(2) == 0) LevelMagic(magic);
                return;
            }

            LevelMagic(magic);

            if (target.Master == this)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (Envir.Random.Next(2) > 0)
            {
                target.ShockTime = Envir.Time + (magic.Level * 5 + 10) * 1000;
                target.Target = null;
                return;
            }

            if (target.Level > Level + 2 || !target.Info.CanTame) return;

            if (Envir.Random.Next(Level + 20 + magic.Level * 5) <= target.Level + 10)
            {
                if (Envir.Random.Next(5) > 0 && target.Master == null)
                {
                    target.RageTime = Envir.Time + (Envir.Random.Next(20) + 10) * 1000;
                    target.Target = null;
                }
                return;
            }

            if (Pets.Count(t => !t.Dead) >= magic.Level + 2) return;
            int rate = (int)(target.MaxHP / 100);
            if (rate <= 2) rate = 2;
            else rate *= 2;

            if (Envir.Random.Next(rate) != 0) return;
            //else if (Envir.Random.Next(20) == 0) target.Die();

            if (target.Master != null)
            {
                target.SetHP(target.MaxHP / 10);
                target.Master.Pets.Remove(target);
            }
            else if (target.Respawn != null)
            {
                target.Respawn.Count--;
                Envir.MonsterCount--;
                CurrentMap.MonsterCount--;
                target.Respawn = null;
            }

            target.Master = this;
            //target.HealthChanged = true;
            target.BroadcastHealthChange();
            Pets.Add(target);
            target.Target = null;
            target.RageTime = 0;
            target.ShockTime = 0;
            target.OperateTime = 0;
            target.MaxPetLevel = (byte)(1 + magic.Level * 2);

            target.Broadcast(new S.ObjectName { ObjectID = target.ObjectID, Name = target.Name });
        }
        private void HellFire(UserMagic magic)
        {
            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction, 4);
            CurrentMap.ActionList.Add(action);

            if (magic.Level != 3) return;

            MirDirection dir = (MirDirection) (((int) Direction + 1)%8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);

            dir = (MirDirection) (((int) Direction - 1 + 8)%8);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, dir, 4);
            CurrentMap.ActionList.Add(action);
        }
        private void ThunderBolt(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            if (target.Undead) damage = (int) (damage*1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void Vampirism(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();
            
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void FireBang(UserMagic magic, Point location)
        {
            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void FireWall(UserMagic magic, Point location)
        {
            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);
            CurrentMap.ActionList.Add(action);
        }
        private void Lightning(UserMagic magic)
        {
            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        
        private void Healing(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;

            int health = GetAttackPower(MinSC, MaxSC) * 2 + magic.GetPower() + Level;
            
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, health, target);

            ActionList.Add(action);
        }
        private bool Poisoning(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return false;

            UserItem item = GetPoison(1);
            if (item == null) return false;

            int power = GetAttackPower(MinSC, MaxSC);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, power, target, item);
            ActionList.Add(action);
            ConsumeItem(item, 1);
            return true;
        }
        private bool SoulFireball(MapObject target, UserMagic magic, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return false;
            cast = true;

            if (target == null || !target.IsAttackTarget(this) || !CanFly(target.CurrentLocation)) return false;

            int damage = GetAttackPower(MinSC, MaxSC) + magic.GetPower();

            int delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, magic, damage, target);

            ActionList.Add(action);
            ConsumeItem(item, 1);

            return true;
        }
        private void SummonSkeleton(UserMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.SkeletonName && monster.Info.Name != Settings.ShinsuName &&
                    monster.Info.Name != Settings.AngelName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            UserItem item = GetAmulet(1);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.SkeletonName);
            if (info == null) return;


            LevelMagic(magic);
            ConsumeItem(item, 1);

            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Purification(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsFriendlyTarget(this)) return;
            
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);

            ActionList.Add(action);
        }

        private void SummonShinsu(UserMagic magic)
        {
            MonsterObject monster;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.SkeletonName && monster.Info.Name != Settings.ShinsuName &&
                    monster.Info.Name != Settings.AngelName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                monster.ActionList.Add(new DelayedAction(DelayedType.Recall, Envir.Time + 500));
                return;
            }

            UserItem item = GetAmulet(5);
            if (item == null) return;

            MonsterInfo info = Envir.GetMonsterInfo(Settings.ShinsuName);
            if (info == null) return;


            LevelMagic(magic);
            ConsumeItem(item, 5);


            monster = MonsterObject.GetMonster(info);
            monster.PetLevel = magic.Level;
            monster.Master = this;
            monster.MaxPetLevel = (byte)(1 + magic.Level * 2);
            monster.Direction = Direction;
            monster.ActionTime = Envir.Time + 1000;

            Pets.Add(monster);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front);
            CurrentMap.ActionList.Add(action);
        }
        private void Hiding(UserMagic magic)
        {
            UserItem item = GetAmulet(1);
            if (item == null) return;
            
            ConsumeItem(item, 1);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, GetAttackPower(MinSC, MaxSC) + (magic.Level + 1) * 5);
            ActionList.Add(action);

        }
        private void MassHiding(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(MinSC, MaxSC) / 2 + (magic.Level + 1) * 2, location);
            CurrentMap.ActionList.Add(action);
        }
        private void SoulShield(UserMagic magic, Point location, out bool cast)
        {
            cast = false;
            UserItem item = GetAmulet(1);
            if (item == null) return;
            cast = true;

            int delay = Functions.MaxDistance(CurrentLocation, location) * 50 + 500; //50 MS per Step

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + delay, this, magic, GetAttackPower(MinSC, MaxSC) * 2 + (magic.Level + 1) * 10, location);
            CurrentMap.ActionList.Add(action);
        }
        private void MassHealing(UserMagic magic, Point location)
        {
            int value = GetAttackPower(MinSC, MaxSC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, value, location);
            CurrentMap.ActionList.Add(action);
        }
        private void TurnUndead(MapObject target, UserMagic magic)
        {
            if (target == null || !target.Undead || !target.IsAttackTarget(this)) return;
            if (Envir.Random.Next(2) + Level - 1 <= target.Level)
            {
                if (target.Race == ObjectType.Monster) target.Target = this;
                return;
            }

            int dif = Level - target.Level + 15;

            if (Envir.Random.Next(100) >= (magic.Level + 1 << 3) + dif)
            {
                if (target.Race == ObjectType.Monster) target.Target = this;
                return;
            }

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, target);
            ActionList.Add(action);

        }
        private void FlameDisruptor(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            if (!target.Undead) damage = (int)(damage * 1.5F);

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void Revelation(MapObject target, UserMagic magic)
        {
            if (target == null) return;

            int value = GetAttackPower(MinSC, MaxSC) + magic.GetPower();
            
            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, value, target);

            ActionList.Add(action);
        }

        private void HeavenlySword(UserMagic magic)
        {
            int damage = GetAttackPower(MinDC, MaxDC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction);
            CurrentMap.ActionList.Add(action);
        }
        private void ThunderStorm(UserMagic magic)
        {
            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation);
            CurrentMap.ActionList.Add(action);
        }
        private void PoisonField(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            if(Envir.Time < PoisonFieldTime) return;

            UserItem amuelt = GetAmulet(5);
            if (amuelt == null) return;


            UserItem poison = GetPoison(5, 1);
            if (poison == null) return;
            

            int damage = GetAttackPower(MinSC, MaxSC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location, Envir.Random.Next(PoisonAttack));

            ConsumeItem(amuelt, 5);
            ConsumeItem(poison, 5);

            PoisonFieldTime = Envir.Time + (18 - magic.Level*2)*1000;

            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private void Entrapment(MapObject target, UserMagic magic)
        {
            if (target == null || !target.IsAttackTarget(this)) return;

            int damage = 0;

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, magic, damage, target);

            ActionList.Add(action);
        }
        private void BladeAvalanche(UserMagic magic)
        {
            int damage = GetAttackPower(MinDC, MaxDC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, CurrentLocation, Direction, 3);
            CurrentMap.ActionList.Add(action);

            Point location = Functions.PointMove(CurrentLocation, MirDirection.Right, 1);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location, Direction, 3);
            CurrentMap.ActionList.Add(action);

            location = Functions.PointMove(CurrentLocation, MirDirection.Left, 1);
            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location, Direction, 3);
            CurrentMap.ActionList.Add(action);
        }
        private void ProtectionField(UserMagic magic)
        {
            int count = Buffs.Where(x => x.Type == BuffType.ProtectionField).ToList().Count();
            if (count > 0) return;

            int duration = 45 + (15 * magic.Level);
            int value = (int)Math.Round(MaxAC * (0.2 + (0.03 * magic.Level)));

            AddBuff(new Buff { Type = BuffType.ProtectionField, Caster = this, ExpireTime = Envir.Time + duration * 1000, Value = value });
            OperateTime = 0;
            LevelMagic(magic);
        }
        private void Rage(UserMagic magic)
        {
            int count = Buffs.Where(x => x.Type == BuffType.Rage).ToList().Count();
            if (count > 0) return;

            int duration = 48 + (6 * magic.Level);
            int value = (int)Math.Round(MaxDC * (0.12 + (0.03 * magic.Level)));

            AddBuff(new Buff { Type = BuffType.Rage, Caster = this, ExpireTime = Envir.Time + duration * 1000, Value = value });
            OperateTime = 0;
            LevelMagic(magic);
        }
        private void Mirroring(UserMagic magic)
        {
            MonsterObject monster;
            DelayedAction action;
            for (int i = 0; i < Pets.Count; i++)
            {
                monster = Pets[i];
                if ((monster.Info.Name != Settings.CloneName) || monster.Dead) continue;
                if (monster.Node == null) continue;
                action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, true);
                CurrentMap.ActionList.Add(action);
                return;
            }

            MonsterInfo info = Envir.GetMonsterInfo(Settings.CloneName);
            if (info == null) return;


            LevelMagic(magic);

            monster = MonsterObject.GetMonster(info);
            monster.Master = this;
            monster.ActionTime = Envir.Time + 1000;
            monster.RefreshNameColour(false);

            Pets.Add(monster);

            action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, monster, Front, false);
            CurrentMap.ActionList.Add(action);
        }
        private void Blizzard(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }
        private void MeteorStrike(UserMagic magic, Point location, out bool cast)
        {
            cast = false;

            int damage = GetAttackPower(MinMC, MaxMC) + magic.GetPower();

            DelayedAction action = new DelayedAction(DelayedType.Magic, Envir.Time + 500, this, magic, damage, location);

            ActiveBlizzard = true;
            CurrentMap.ActionList.Add(action);
            cast = true;
        }

        private void CompleteMagic(IList<object> data)
        {
            UserMagic magic = (UserMagic) data[0];
            int value;
            MapObject target;

            MonsterObject monster;
            switch (magic.Spell)
            {
                    #region FireBall, GreatFireBall, ThunderBolt, SoulFireBall, FlameDisruptor

                case Spell.FireBall:
                case Spell.GreatFireBall:
                case Spell.ThunderBolt:
                case Spell.SoulFireBall:
                case Spell.FlameDisruptor:
                    value = (int) data[1];
                    target = (MapObject) data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0) LevelMagic(magic);
                    break;

                    #endregion

                    #region FrostCrunch
                case Spell.FrostCrunch:
                    value = (int) data[1];
                    target = (MapObject) data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Attacked(this, value, DefenceType.MAC, false) > 0)
                    {
                        if (Level + (target.Race == ObjectType.Player ? 2 : 10) >= target.Level && Envir.Random.Next(target.Race == ObjectType.Player ? 100 : 20) <= magic.Level)
                        {
                            target.ApplyPoison(new Poison
                                {
                                    Owner = this,
                                    Duration = target.Race == ObjectType.Player ? 2 : 5 + Envir.Random.Next(Freezing),
                                    PType = PoisonType.Frozen,
                                    TickSpeed = 1000,
                                }, this);
                            target.OperateTime = 0;
                        }
                        
                        LevelMagic(magic);
                    }
                    break;

                    #endregion

                    #region Vampirism

                case Spell.Vampirism:
                    value = (int) data[1];
                    target = (MapObject) data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    value = target.Attacked(this, value, DefenceType.MAC, false);
                    if (value == 0) return;
                    LevelMagic(magic);
                    if (VampAmount == 0) VampTime = Envir.Time + 1000;
                    VampAmount += (ushort)(value*(magic.Level + 1)*0.25F);
                    break;

                    #endregion

                    #region Healing

                case Spell.Healing:
                    value = (int) data[1];
                    target = (MapObject) data[2];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (target.Health >= target.MaxHealth) return;
                    target.HealAmount = (ushort) Math.Min(ushort.MaxValue, target.HealAmount + value);
                    target.OperateTime = 0;
                    LevelMagic(magic);
                    break;

                    #endregion

                    #region ElectricShock

                case Spell.ElectricShock:
                    monster = (MonsterObject) data[1];
                    if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;
                    ElectricShock(monster, magic);
                    break;

                    #endregion

                    #region Poisoning

                case Spell.Poisoning:
                    value = (int) data[1];
                    target = (MapObject) data[2];
                    UserItem item = (UserItem) data[3];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

                    switch (item.Info.Shape)
                    {
                        case 1:
                            target.ApplyPoison(new Poison
                                {
                                    Duration = (value*2) + (magic.Level + 1)*7,
                                    Owner = this,
                                    PType = PoisonType.Green,
                                    TickSpeed = 2000,
                                    Value = value / 15 + magic.Level + 1 + Envir.Random.Next(PoisonAttack)
                                }, this);
                            break;
                        case 2:
                            target.ApplyPoison(new Poison
                                {
                                    Duration = (value*2) + (magic.Level + 1)*7,
                                    Owner = this,
                                    PType = PoisonType.Red,
                                    TickSpeed = 2000,
                                }, this);
                            break;
                    }
                    target.OperateTime = 0;

                    LevelMagic(magic);
                    break;

                    #endregion

                    #region Teleport

                case Spell.Teleport:
                    Point location = (Point) data[1];
                    if (CurrentMap.Info.NoTeleport)
                    {
                        ReceiveChat(("You cannot teleport on this map"), ChatType.System);
                        return;
                    }
                    if (!CurrentMap.ValidPoint(location) || Envir.Random.Next(4) >= magic.Level + 1 || !Teleport(CurrentMap, location, false)) return;                   
                    CurrentMap.Broadcast(new S.ObjectEffect {ObjectID = ObjectID, Effect = SpellEffect.Teleport}, CurrentLocation);
                    LevelMagic(magic);
                    AddBuff(new Buff {Type = BuffType.Teleport, Caster = this, ExpireTime = Envir.Time + 30000});
                    break;

                    #endregion

                    #region Hiding

                case Spell.Hiding:
                    for (int i = 0; i < Buffs.Count; i++)
                        if (Buffs[i].Type == BuffType.Hiding) return;

                    value = (int) data[1];
                    AddBuff(new Buff {Type = BuffType.Hiding, Caster = this, ExpireTime = Envir.Time + value*1000});
                    LevelMagic(magic);
                    break;

                    #endregion

                    #region Haste

                case Spell.Haste:
                    AddBuff(new Buff { Type = BuffType.Haste, Caster = this, ExpireTime = Envir.Time + (magic.Level + 1) * 30000, Value = (magic.Level + 1) * 2 });
                    LevelMagic(magic);
                    break;

                #endregion

                    #region LightBody

                case Spell.LightBody:
                    AddBuff(new Buff { Type = BuffType.LightBody, Caster = this, ExpireTime = Envir.Time + (magic.Level + 1) * 30000, Value = (magic.Level + 1) * 2 });
                    LevelMagic(magic);
                    break;

                #endregion

                    #region MagicShield

                case Spell.MagicShield:
                    if (MagicShield) return;
                    MagicShield = true;
                    MagicShieldLv = magic.Level;
                    MagicShieldTime = Envir.Time + (int) data[1]*1000;
                    CurrentMap.Broadcast(new S.ObjectEffect {ObjectID = ObjectID, Effect = SpellEffect.MagicShieldUp}, CurrentLocation);
                    LevelMagic(magic);
                    break;

                #endregion

                    #region TurnUndead

                case Spell.TurnUndead:
                    monster = (MonsterObject)data[1];
                    if (monster == null || !monster.IsAttackTarget(this) || monster.CurrentMap != CurrentMap || monster.Node == null) return;
                    monster.LastHitter = this;
                    monster.LastHitTime = Envir.Time + 5000;
                    monster.EXPOwner = this;
                    monster.EXPOwnerTime = Envir.Time + 5000;
                    monster.Die();
                    LevelMagic(magic);
                    break;

                #endregion

                    #region Purification

                case Spell.Purification:
                    target = (MapObject)data[1];

                    if (target == null || !target.IsFriendlyTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if (Envir.Random.Next(4) > magic.Level || target.PoisonList.Count == 0) return;

                    target.PoisonList.Clear();
                    target.OperateTime = 0;

                    LevelMagic(magic);
                    break;

                #endregion

                    #region Revelation

                case Spell.Revelation:
                    value = (int) data[1];
                    target = (MapObject) data[2];
                    if (target == null || target.CurrentMap != CurrentMap || target.Node == null) return;
                    if(target.Race != ObjectType.Player && target.Race != ObjectType.Monster) return;
                    if (Envir.Random.Next(4) > magic.Level || Envir.Time < target.RevTime) return;

                    target.RevTime = Envir.Time + value*1000;
                    target.PoisonList.Clear();
                    target.OperateTime = 0;
                    target.BroadcastHealthChange();

                    LevelMagic(magic);
                    break;

                #endregion

                    #region Entrapment

                case Spell.Entrapment:
                    value = (int)data[1];
                    target = (MapObject)data[2];

                    if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null ||
                        Functions.MaxDistance(CurrentLocation, target.CurrentLocation) > 7 || target.Level >= Level + 5 + Envir.Random.Next(8)) return;

                    MirDirection pulldirection = (MirDirection)((byte)(Direction - 4) % 8);
                    int pulldistance = 0;
                    if ((byte)pulldirection % 2 > 0)
                        pulldistance = Math.Max(0, Math.Min(Math.Abs(CurrentLocation.X - target.CurrentLocation.X), Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y)));
                    else
                        pulldistance = pulldirection == MirDirection.Up || pulldirection == MirDirection.Down ? Math.Abs(CurrentLocation.Y - target.CurrentLocation.Y) - 2 : Math.Abs(CurrentLocation.X - target.CurrentLocation.X) - 2;
                    
                    int levelgap = target.Race == ObjectType.Player ? Level - target.Level + 4 : Level - target.Level + 9;
                    if (Envir.Random.Next(30) >= ((magic.Level + 1) * 3) + levelgap) return;

                    int duration = target.Race == ObjectType.Player ? (int)Math.Round((magic.Level + 1) * 1.6) : (int)Math.Round((magic.Level + 1) * 0.8);
                    if (duration > 0) target.ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = duration, TickSpeed = 1000 }, this);
                    CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.Entrapment }, target.CurrentLocation);
                    if (target.Pushed(this, pulldirection, pulldistance) > 0) LevelMagic(magic);
                    break;

                #endregion
            }


        }
        private void CompleteMine(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            if (target == null) return;
            target.Broadcast(new S.MapEffect{ Effect = SpellEffect.Mine, Location = target.CurrentLocation, Value = (byte)Direction});
            //target.Broadcast(new S.ObjectEffect { ObjectID = target.ObjectID, Effect = SpellEffect.Mine });
            if ((byte)target.Direction < 6)
                target.Direction++;
            target.Broadcast(target.GetInfo());
        }
        private void CompleteAttack(IList<object> data)
        {
            MapObject target = (MapObject) data[0];
            int damage = (int) data[1];
            DefenceType defence = (DefenceType) data[2];
            bool damageWeapon = (bool) data[3];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            target.Attacked(this, damage, defence, damageWeapon);
        }
        

        private UserItem GetAmulet(int count)
        {
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null && item.Info.Type == ItemType.Amulet && item.Info.Shape == 0 && item.Count >= count)
                    return item;
            }

            return null;
        }
        private UserItem GetPoison(int count, byte shape = 0)
        {

            UserItem item = Info.Equipment[(int)EquipmentSlot.Amulet];
            if (item == null || item.Info.Type != ItemType.Amulet || item.Count < count) return null;

            if (shape == 0)
            {
                if (item.Info.Shape == 1 || item.Info.Shape == 2)
                    return item;
            }
            else if (item.Info.Shape == shape)
                return item;

            return null;
        }
        private UserMagic GetMagic(Spell spell)
        {
            for (int i = 0; i < Info.Magics.Count; i++)
            {
                UserMagic magic = Info.Magics[i];
                if (magic.Spell != spell) continue;
                return magic;
            }


            return null;
        }

        public void LevelMagic(UserMagic magic)
        {
            byte exp = (byte)(Envir.Random.Next(3) + 1);
            if (HasSkillNecklace) exp *= 3;
            if (Level == 255) exp = byte.MaxValue;

            switch (magic.Level)
            {
                case 0:
                    if (Level < magic.Info.Level1)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need1)
                    {
                        magic.Level++;
                        magic.Experience = (ushort) (magic.Experience - magic.Info.Need1);
                        RefreshStats();
                    }
                    break;
                case 1:
                    if (Level < magic.Info.Level2)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need2)
                    {
                        magic.Level++;
                        magic.Experience = (ushort) (magic.Experience - magic.Info.Need2);
                        RefreshStats();
                    }
                    break;
                case 2:
                    if (Level < magic.Info.Level3)
                        return;

                    magic.Experience += exp;
                    if (magic.Experience >= magic.Info.Need3)
                    {
                        magic.Level++;
                        magic.Experience = 0;
                        RefreshStats();
                    }
                    break;
                default:
                    return;
            }

            Enqueue(new S.MagicLeveled { Spell = magic.Spell, Level = magic.Level, Experience = magic.Experience });

        }

        public bool CheckMovement(Point location)
        {
            for (int i = 0; i < CurrentMap.Info.Movements.Count; i++)
            {
                MovementInfo info = CurrentMap.Info.Movements[i];

                if (info.Source != location) continue;

                if (info.NeedHole)
                {
                    Cell cell = CurrentMap.GetCell(location);

                    if (cell.Objects == null ||
                        cell.Objects.Where(ob => ob.Race == ObjectType.Spell).All(ob => ((SpellObject) ob).Spell != Spell.DigOutZombie))
                        continue;
                }

                Map temp = Envir.GetMap(info.MapIndex);

                if (temp == null || !temp.ValidPoint(info.Destination)) continue;

                CurrentMap.RemoveObject(this);
                Broadcast(new S.ObjectRemove {ObjectID = ObjectID});

                ActionList.Add(new DelayedAction(DelayedType.MapMovement, Envir.Time + 500, temp, info.Destination, CurrentMap, CurrentLocation));

                return true;
            }

            return false;
        }
        private void CompleteMapMovement(IList<object> data)
        {
            if (this == null) return;
            Map temp = (Map)data[0];
            Point destination = (Point)data[1];
            Map checkmap = (Map)data[2];
            Point checklocation = (Point)data[3];

            if (CurrentMap != checkmap || CurrentLocation != checklocation) return;

            CurrentMap = temp;
            CurrentLocation = destination;

            CurrentMap.AddObject(this);

            Enqueue(new S.MapChanged
            {
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight
            });

            GetObjects();

            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                BindLocation = szi.Location;
                BindMapIndex = CurrentMapIndex;
                InSafeZone = true;
            }
            else
                InSafeZone = false;
        }

        public override bool Teleport(Map temp, Point location, bool effects = true, byte effectnumber = 0)
        {
            if (!base.Teleport(temp, location, effects)) return false;
            
            Enqueue(new S.MapChanged
            {
                FileName = CurrentMap.Info.FileName,
                Title = CurrentMap.Info.Title,
                MiniMap = CurrentMap.Info.MiniMap,
                BigMap = CurrentMap.Info.BigMap,
                Lights = CurrentMap.Info.Light,
                Location = CurrentLocation,
                Direction = Direction,
                MapDarkLight = CurrentMap.Info.MapDarkLight
            });

            if (effects) Enqueue(new S.TeleportIn());

            GetObjectsPassive();

            SafeZoneInfo szi = CurrentMap.GetSafeZone(CurrentLocation);

            if (szi != null)
            {
                BindLocation = szi.Location;
                BindMapIndex = CurrentMapIndex;
                InSafeZone = true;
            }
            else
                InSafeZone = false;

            return true;
        }


        public bool TeleportEscape(int attempts)
        {
            Map temp = Envir.GetMap(BindMapIndex);
            
            for (int i = 0; i < attempts; i++)
            {
                Point location = new Point(BindLocation.X + Envir.Random.Next(-100, 100),
                                           BindLocation.Y + Envir.Random.Next(-100, 100));

                if (Teleport(temp, location)) return true;
            }

            return false;
        }


        private Packet GetUpdateInfo()
        {
            return new S.PlayerUpdate
                {
                    ObjectID = ObjectID,
                    Weapon = Looks_Weapon,
                    Armour = Looks_Armour,
                    Light = Light,
                    WingEffect = Looks_Wings
                };


        }
        public override Packet GetInfo()
        {
            return new S.ObjectPlayer
            {
                ObjectID = ObjectID,
                Name = CurrentMap.Info.NoNames ? "?????" : Name,
                NameColour = NameColour,
                GuildName = CurrentMap.Info.NoNames ? "?????": MyGuild != null? MyGuild.Name: "",
                GuildRankName = CurrentMap.Info.NoNames ? "?????": MyGuildRank != null? MyGuildRank.Name: "",
                Class = Class,
                Gender = Gender,
                Location = CurrentLocation,
                Direction = Direction,
                Hair = Hair,
                Weapon = Looks_Weapon,
                Armour = Looks_Armour,
                Light = Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = MagicShield ? SpellEffect.MagicShieldUp : SpellEffect.None,
                WingEffect = Looks_Wings
            };
        }
        

        public override bool IsAttackTarget(PlayerObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || InSafeZone || attacker.InSafeZone || attacker == this || GMGameMaster) return false;

            switch (attacker.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return GroupMembers == null || !GroupMembers.Contains(attacker);
                case AttackMode.Guild:
                    return true;
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;
        }
        public override bool IsAttackTarget(MonsterObject attacker)
        {
            if (attacker == null || attacker.Node == null) return false;
            if (Dead || attacker.Master == this || GMGameMaster) return false;
            if (attacker.Info.AI == 6) return PKPoints >= 200;
            if (attacker.Master == null) return true;
            if (InSafeZone || attacker.Master.InSafeZone) return false;
            if (LastHitter != attacker.Master && attacker.Master.LastHitter != this)
            {
                bool target = false;

                for (int i = 0; i < attacker.Pets.Count; i++)
                {
                    if (attacker.Pets[i].EXPOwner != this) continue;

                    target = true;
                    break;
                }

                if (!target)
                    return false;
            }

            switch (attacker.Master.AMode)
            {
                case AttackMode.All:
                    return true;
                case AttackMode.Group:
                    return GroupMembers == null || !GroupMembers.Contains(attacker.Master);
                case AttackMode.Guild:
                    return true;
                case AttackMode.Peace:
                    return false;
                case AttackMode.RedBrown:
                    return PKPoints >= 200 || Envir.Time < BrownTime;
            }

            return true;

        }
        public override bool IsFriendlyTarget(PlayerObject ally)
        {
            if (ally == this) return true;

            switch (ally.AMode)
            {
                case AttackMode.Group:
                    return GroupMembers != null && GroupMembers.Contains(ally);
                case AttackMode.RedBrown:
                    return PKPoints < 200 & Envir.Time > BrownTime;
                case AttackMode.Guild:
                    return false;
            }
            return true;
        }
        public override bool IsFriendlyTarget(MonsterObject ally)
        {
            if (ally.Race != ObjectType.Monster) return false;
            if (ally.Master == null) return false;

            switch (ally.Master.Race)
            {
                case ObjectType.Player:
                    if (!ally.Master.IsFriendlyTarget(this)) return false;
                    break;
                case ObjectType.Monster:
                    return false;
            }

            return true;
        }
        public override int Attacked(PlayerObject attacker, int damage, DefenceType type = DefenceType.ACAgility, bool damageWeapon = true)
        {
            int armour = 0;

            switch (type)
            {
                case DefenceType.ACAgility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist)) return 0;
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    armour = GetAttackPower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    if ((Settings.PvpCanResistMagic) && (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist)) return 0;
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.Agility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    break;
            }

            if (damageWeapon)
                attacker.DamageWeapon();

            if ((attacker.CriticalRate * Settings.CriticalRateWeight) > Envir.Random.Next(100))
            {   
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Critical },CurrentLocation);
                damage = Math.Min(int.MaxValue, damage + (int)Math.Floor(damage * (((double)attacker.CriticalDamage / (double)Settings.CriticalDamageWeight) * 10)));
            }

            if (Envir.Random.Next(100) < Reflect)
            {
                attacker.Attacked(this, damage, type, false);
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect },CurrentLocation);
                return 0;
            }



            if (MagicShield)
                damage -= damage*(MagicShieldLv + 2)/10;

            if (armour >= damage) return 0;

            MagicShieldTime -= (damage - armour) * 60;

            if (attacker.LifeOnHit > 0)
                attacker.ChangeHP(attacker.LifeOnHit);

            if (attacker.HpDrainRate > 0)
            {
                attacker.HpDrain += Math.Max(0, ((float)(damage - armour) / 100) * attacker.HpDrainRate);
                if (attacker.HpDrain > 2)
                {
                    int HpGain = (int)Math.Floor(attacker.HpDrain);
                    attacker.ChangeHP(HpGain);
                    attacker.HpDrain -= HpGain;

                }
            }

            LastHitter = attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;

            if (Envir.Time > BrownTime && PKPoints < 200 && !CurrentMap.Info.Fight)
                attacker.BrownTime = Envir.Time + Settings.Minute;

            if (attacker.HasParalysisRing && 1 == Envir.Random.Next(1, 15))
                ApplyPoison(new Poison { PType = PoisonType.Paralysis, Duration = 5, TickSpeed = 1000 }, attacker);
            byte LevelOffset = (byte)(Level > attacker.Level ? 0 : Math.Min(10,attacker.Level - Level));
            if ((attacker.Freezing > 0) && (Settings.PvpCanFreeze))
                if ((Envir.Random.Next(Settings.FreezingAttackWeight)  <  attacker.Freezing) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Slow, Duration = Math.Min(10,(3 + Envir.Random.Next(attacker.Freezing))), TickSpeed = 1000}, attacker);
            if (attacker.PoisonAttack > 0)
                if ((Envir.Random.Next(Settings.PoisonAttackWeight) < attacker.PoisonAttack) && (Envir.Random.Next(LevelOffset) == 0))
                    ApplyPoison(new Poison { PType = PoisonType.Green, Duration = 5, TickSpeed = 1000 , Value = Math.Min(10, 3 + Envir.Random.Next(attacker.PoisonAttack))}, attacker);
            
            DamageDura();
            ActiveBlizzard = false;

            Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });

            ChangeHP(armour - damage);
            return damage - armour;
        }
        public override int Attacked(MonsterObject attacker, int damage, DefenceType type = DefenceType.ACAgility)
        {
            int armour = 0;
            switch (type)
            {
                case DefenceType.ACAgility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.AC:
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.MACAgility:
                    if (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist) return 0;
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    armour = GetAttackPower(MinMAC, MaxMAC);
                    break;
                case DefenceType.MAC:
                    if (Envir.Random.Next(Settings.MagicResistWeight) < MagicResist) return 0;
                    armour = GetAttackPower(MinAC, MaxAC);
                    break;
                case DefenceType.Agility:
                    if (Envir.Random.Next(Agility + 1) > attacker.Accuracy) return 0;
                    break;
            }

            if (Envir.Random.Next(100) < Reflect)
            {
                attacker.Attacked(this, damage, type, false);
                CurrentMap.Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = SpellEffect.Reflect },CurrentLocation);
                return 0;
            }

            if (MagicShield)
                damage -= damage * (MagicShieldLv + 2) / 10;

            if (armour >= damage) return 0;

            MagicShieldTime -= (damage - armour) * 60;


            LastHitter = attacker.Master ?? attacker;
            LastHitTime = Envir.Time + 10000;
            RegenTime = Envir.Time + RegenDelay;
            
            DamageDura();
            ActiveBlizzard = false;

            Enqueue(new S.Struck { AttackerID = attacker.ObjectID });
            Broadcast(new S.ObjectStruck { ObjectID = ObjectID, AttackerID = attacker.ObjectID, Direction = Direction, Location = CurrentLocation });
//
            ChangeHP(armour - damage);
            return damage - armour;
        }


        public override void ApplyPoison(Poison p, MapObject Caster = null, bool NoResist = false)
        {
            if ((Caster != null) || (!NoResist))
                if (((Caster.Race != ObjectType.Player) || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonResistWeight) < PoisonResist))
                    return;
            if (p.Owner != null && p.Owner.Race == ObjectType.Player && Envir.Time > BrownTime && PKPoints < 200)
                p.Owner.BrownTime = Envir.Time + Settings.Minute;
            if ((p.PType == PoisonType.Green) || (p.PType == PoisonType.Red)) p.Duration = Math.Max(0, p.Duration - PoisonRecovery);
            if (p.Duration == 0) return;
            
            for (int i = 0; i < PoisonList.Count; i++)
            {
                if (PoisonList[i].PType != p.PType) continue;
                if ((PoisonList[i].PType == PoisonType.Green) && (PoisonList[i].Value > p.Value)) return;//cant cast weak poison to cancel out strong poison
                if ((PoisonList[i].PType != PoisonType.Green) && ((PoisonList[i].Duration - PoisonList[i].Time) > p.Duration)) return;//cant cast 1 second poison to make a 1minute poison go away!
                ReceiveChat("You have been poisoned.", ChatType.System);
                PoisonList[i] = p;
                return;
            }
            ReceiveChat("You have been poisoned.", ChatType.System);
            PoisonList.Add(p);
        }
        public override void AddBuff(Buff b)
        {
            base.AddBuff(b);
            Enqueue(new S.AddBuff { Type = b.Type, Caster = b.Caster.Name, Expire = b.ExpireTime - Envir.Time, Value = b.Value, Infinite = b.Infinite});
            RefreshStats();
        }

        public void RemoveItem(MirGridType grid, ulong id, int to)
        {
            S.RemoveItem p = new S.RemoveItem { Grid = grid, UniqueID = id, To = to, Success = false };
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (to < 0 || to >= array.Length) return;

            UserItem temp = null;
            int index = -1;

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                temp = Info.Equipment[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (temp == null || index == -1)
            {
                Enqueue(p);
                return;
            }
            if (temp.Cursed && !UnlockCurse)
            {
                Enqueue(p);
                return;
            }

            if (!CanRemoveItem(grid, temp)) return;
            Info.Equipment[index] = null;
            if (temp.Cursed)
                UnlockCurse = false;
            if (array[to] == null)
            {
                array[to] = temp;
                p.Success = true;
                Enqueue(p);
                RefreshStats();
                Broadcast(GetUpdateInfo());
                return;
            }

            Enqueue(p);
        }
        public void MoveItem(MirGridType grid, int from, int to)
        {
            S.MoveItem p = new S.MoveItem { Grid = grid, From = from, To = to, Success = false };
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            if (from >= 0 && to >= 0 && from < array.Length && to < array.Length)
            {
                UserItem i = array[to];
                array[to] = array[from];
                array[from] = i;

                p.Success = true;
                Enqueue(p);
                return;
            }

            Enqueue(p);
        }
        public void StoreItem(int from, int to)
        {
            S.StoreItem p = new S.StoreItem { From = from, To = to, Success = false };

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(p);
                return;
            }
            NPCObject ob = null;
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                ob = CurrentMap.NPCs[i];
                break;
            }

            if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
            {
                Enqueue(p);
                return;
            }

            
            if (from < 0 || from >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Account.Storage.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Info.Inventory[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.DontStore))
            {
                Enqueue(p);
                return;
            }

            if (Account.Storage[to] == null)
            {
                Account.Storage[to] = temp;
                Info.Inventory[from] = null;
                RefreshBagWeight();

                p.Success = true;
                Enqueue(p);
                return;
            }
            Enqueue(p);
        }
        public void TakeBackItem(int from, int to)
        {
            S.TakeBackItem p = new S.TakeBackItem { From = from, To = to, Success = false };

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(p);
                return;
            }
            NPCObject ob = null;
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                ob = CurrentMap.NPCs[i];
                break;
            }

            if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
            {
                Enqueue(p);
                return;
            }

            
            if (from < 0 || from >= Account.Storage.Length)
            {
                Enqueue(p);
                return;
            }

            if (to < 0 || to >= Info.Inventory.Length)
            {
                Enqueue(p);
                return;
            }

            UserItem temp = Account.Storage[from];

            if (temp == null)
            {
                Enqueue(p);
                return;
            }

            if (temp.Weight + CurrentBagWeight > MaxBagWeight)
            {
                ReceiveChat("Too heavy to get back.", ChatType.System);
                Enqueue(p);
                return;
            }

            if (Info.Inventory[to] == null)
            {
                Info.Inventory[to] = temp;
                Account.Storage[from] = null;

                p.Success = true;
                RefreshBagWeight();
                Enqueue(p);
                return;
            }
            Enqueue(p);
        }
        public void EquipItem(MirGridType grid, ulong id, int to)
        {
            S.EquipItem p = new S.EquipItem { Grid = grid, UniqueID = id, To = to, Success = false };
            if (to < 0 || to >= Info.Equipment.Length)
            {
                Enqueue(p);
                return;
            }
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }


            int index = -1;
            UserItem temp = null;

            for (int i = 0; i < array.Length; i++)
            {
                temp = array[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }


            if (temp == null || index == -1)
            {
                Enqueue(p);
                return;
            }
            if ((Info.Equipment[to] != null) && (Info.Equipment[to].Cursed) && (!UnlockCurse))
            {
                Enqueue(p);
                return;
            }

            if ((temp.SoulBoundId != -1) && (temp.SoulBoundId != Info.Index))
            {
                Enqueue(p);
                return;
            }

            if (CanEquipItem(temp, to))
            {
                if (temp.Info.NeedIdentify && !temp.Identified)
                {
                    temp.Identified = true;
                    Enqueue(new S.RefreshItem { Item = temp });
                }
                if ((temp.Info.BindOnEquip) && (temp.SoulBoundId == -1))
                {
                    temp.SoulBoundId = Info.Index;
                    Enqueue(new S.RefreshItem { Item = temp });   
                }
                if (UnlockCurse && Info.Equipment[to].Cursed)
                    UnlockCurse = false;
                array[index] = Info.Equipment[to];
                Info.Equipment[to] = temp;
                p.Success = true;
                Enqueue(p);
                RefreshStats();
                //Broadcast(GetUpdateInfo());
                return;
            }
            Enqueue(p);
        }
        public void UseItem(ulong id)
        {
            S.UseItem p = new S.UseItem { UniqueID = id, Success = false };
            if (Dead)
            {
                Enqueue(p);
                return;
            }

            UserItem item = null;
            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                item = Info.Inventory[i];
                if (item == null || item.UniqueID != id) continue;
                index = i;
                break;
            }

            if (item == null || index == -1 || !CanUseItem(item))
            {
                Enqueue(p);
                return;
            }

            switch (item.Info.Type)
            {
                case ItemType.Potion:
                    switch (item.Info.Shape)
                    {
                        case 0:
                            PotHealthAmount = (ushort)Math.Min(ushort.MaxValue,PotHealthAmount + item.Info.HP);
                            PotManaAmount = (ushort)Math.Min(ushort.MaxValue, PotManaAmount + item.Info.MP);
                            break;
                        case 1: //Sun Potion
                            ChangeHP(item.Info.HP);
                            ChangeMP(item.Info.MP);
                            break;
                        case 2:
                            if (UnlockCurse)
                            {
                                ReceiveChat("You can already unequip a cursed item.", ChatType.Hint);
                                Enqueue(p);
                                return;
                            }
                            ReceiveChat("You can now unequip a cursed item.", ChatType.Hint);
                            UnlockCurse = true;
                            break;
                    }
                    break;
                case ItemType.Scroll:
                    UserItem temp;
                    switch (item.Info.Shape)
                    {
                        case 0: //DE
                            if (!TeleportEscape(20))
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 1: //TT
                            if (!Teleport(Envir.GetMap(BindMapIndex), BindLocation))
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 2: //RT
                            if (!TeleportRandom(200, item.Info.Durability))
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 3:
                            if (!TryLuckWeapon())
                            {
                                Enqueue(p);
                                return;
                            }
                            break;
                        case 4:
                            temp = Info.Equipment[(int)EquipmentSlot.Weapon];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair))
                            {
                                Enqueue(p);
                                return;
                            }
                            temp.MaxDura = (ushort)Math.Max(0, temp.MaxDura - Math.Min(5000, temp.MaxDura - temp.CurrentDura) / 8);

                            temp.CurrentDura = (ushort)Math.Min(temp.MaxDura, temp.CurrentDura + 5000);
                            temp.DuraChanged = false;

                            ReceiveChat("Your weapon has been partially repaired", ChatType.Hint);
                            Enqueue(new S.ItemRepaired {UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura});
                            break;
                        case 5:
                            temp = Info.Equipment[(int)EquipmentSlot.Weapon];
                            if (temp == null || temp.MaxDura == temp.CurrentDura)
                            {
                                Enqueue(p);
                                return;
                            }
                            if (temp.Info.Bind.HasFlag(BindMode.DontRepair) || temp.Info.BindNoSRepair)
                            {
                                Enqueue(p);
                                return;
                            }
                            temp.CurrentDura = temp.MaxDura;
                            temp.DuraChanged = false;

                            ReceiveChat("Your weapon has been completely repaired", ChatType.Hint);
                            Enqueue(new S.ItemRepaired { UniqueID = temp.UniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                            break;
                    }
                    break;
                case ItemType.Book:
                    UserMagic magic = new UserMagic((Spell)item.Info.Shape);

                    if (magic.Info == null)
                    {
                        Enqueue(p);
                        return;
                    }

                    Info.Magics.Add(magic);
                    Enqueue(magic.GetInfo());
                    break;
                default:
                    return;
            }

            if (item.Count > 1) item.Count--;
            else Info.Inventory[index] = null;
            RefreshBagWeight();

            p.Success = true;
            Enqueue(p);
        }
        public void SplitItem(MirGridType grid, ulong id, uint count)
        {
            S.SplitItem1 p = new S.SplitItem1 { Grid = grid, UniqueID = id, Count = count, Success = false };
            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    array = Account.Storage;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            UserItem temp = null;


            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == null || array[i].UniqueID != id) continue;
                temp = array[i];
                break;
            }
            
            if (temp == null || count >= temp.Count || FreeSpace(array) == 0)
            {
                Enqueue(p);
                return;
            }

            temp.Count -= count;

            temp = Envir.CreateFreshItem(temp.Info);
            temp.Count = count;


            p.Success = true;
            Enqueue(p);
            Enqueue(new S.SplitItem { Item = temp, Grid = grid });

            if (grid == MirGridType.Inventory && (temp.Info.Type == ItemType.Potion || temp.Info.Type == ItemType.Scroll))
            {
                for (int i = 40; i < array.Length; i++)
                {
                    if (array[i] != null) continue;
                    array[i] = temp;
                    RefreshBagWeight();
                    return;
                }
            }

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null) continue;
                array[i] = temp;
                RefreshBagWeight();
                return;
            }
        }
        public void MergeItem(MirGridType gridFrom, MirGridType gridTo, ulong fromID, ulong toID)
        {
            S.MergeItem p = new S.MergeItem { GridFrom = gridFrom, GridTo = gridTo, IDFrom = fromID, IDTo = toID, Success = false };

            UserItem[] arrayFrom;
            switch (gridFrom)
            {
                case MirGridType.Inventory:
                    arrayFrom = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayFrom = Account.Storage;
                    break;
                case MirGridType.Equipment:
                    arrayFrom = Info.Equipment;
                    break;
                default:
                    Enqueue(p);
                    return;
            }


            UserItem[] arrayTo;
            switch (gridTo)
            {
                case MirGridType.Inventory:
                    arrayTo = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.StorageKey, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Enqueue(p);
                        return;
                    }
                    NPCObject ob = null;
                    for (int i = 0; i < CurrentMap.NPCs.Count; i++)
                    {
                        if (CurrentMap.NPCs[i].ObjectID != NPCID) continue;
                        ob = CurrentMap.NPCs[i];
                        break;
                    }

                    if (ob == null || !Functions.InRange(ob.CurrentLocation, CurrentLocation, Globals.DataRange))
                    {
                        Enqueue(p);
                        return;
                    }
                    arrayTo = Account.Storage;
                    break;
                case MirGridType.Equipment:
                    arrayTo = Info.Equipment;
                    break;
                default:
                    Enqueue(p);
                    return;
            }

            UserItem tempFrom = null;
            int index = -1;

            for (int i = 0; i < arrayFrom.Length; i++)
            {
                if (arrayFrom[i] == null || arrayFrom[i].UniqueID != fromID) continue;
                index = i;
                tempFrom = arrayFrom[i];
                break;
            }

            if (tempFrom == null || tempFrom.Info.StackSize == 1 || index == -1)
            {
                Enqueue(p);
                return;
            }


            UserItem tempTo = null;

            for (int i = 0; i < arrayTo.Length; i++)
            {
                if (arrayTo[i] == null || arrayTo[i].UniqueID != toID) continue;
                tempTo = arrayTo[i];
                break;
            }

            if (tempTo == null || tempTo.Info != tempFrom.Info || tempTo.Count == tempTo.Info.StackSize ||
                (tempTo.Info.Type != ItemType.Amulet && (gridFrom == MirGridType.Equipment || gridTo == MirGridType.Equipment)))
            {
                Enqueue(p);
                return;
            }

            if (tempFrom.Count <= tempTo.Info.StackSize - tempTo.Count)
            {
                tempTo.Count += tempFrom.Count;
                arrayFrom[index] = null;
            }
            else
            {
                tempFrom.Count -= tempTo.Info.StackSize - tempTo.Count;
                tempTo.Count = tempTo.Info.StackSize;
            }

            p.Success = true;
            Enqueue(p);
            RefreshStats();
        }
        public void DropItem(ulong id, uint count)
        {
            S.DropItem p = new S.DropItem { UniqueID = id, Count = count, Success = false };
            if (Dead)
            {
                Enqueue(p);
                return;
            }

            if (CurrentMap.Info.NoThrowItem)
            {
                ReceiveChat("You cannot drop items on this map", ChatType.System);
                Enqueue(p);
                return;            
            }

            UserItem temp = null;
            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                temp = Info.Inventory[i];
                if (temp == null || temp.UniqueID != id) continue;
                index = i;
                break;
            }

            if (temp == null || index == -1 || count > temp.Count)
            {
                Enqueue(p);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.DontDrop))
            {
                Enqueue(p);
                return;
            }

            if (temp.Count == count)
            {
                if (!temp.Info.Bind .HasFlag(BindMode.DestroyOnDrop))
                    if (!DropItem(temp))
                    {
                        Enqueue(p);
                        return;
                    }
                Info.Inventory[index] = null;
            }
            else
            {
                UserItem temp2 = Envir.CreateFreshItem(temp.Info);
                temp2.Count = count;
                if (!temp.Info.Bind.HasFlag(BindMode.DestroyOnDrop))
                    if (!DropItem(temp2))
                    {
                        Enqueue(p);
                        return;
                    }
                temp.Count -= count;
            }
            p.Success = true;
            Enqueue(p);
            RefreshBagWeight();
        }
        public void DropGold(uint gold)
        {
            if (Account.Gold < gold) return;

            ItemObject ob = new ItemObject(this, gold);

            if (!ob.Drop(5)) return;
            Account.Gold -= gold;
            Enqueue(new S.LoseGold { Gold = gold });
        }
        public void PickUp()
        {
            if (Dead)
            {
                //Send Fail
                return;
            }

            Cell cell = CurrentMap.GetCell(CurrentLocation);

            bool sendFail = false;

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Item) continue;

                if (ob.Owner != null && ob.Owner != this && !IsGroupMember(ob.Owner)) //Or Group member.
                {
                    sendFail = true;
                    continue;
                }
                ItemObject item = (ItemObject) ob;

                if (item.Item != null)
                {
                    if (!CanGainItem(item.Item)) continue;
                    if (item.Item.Info.ShowGroupPickup)
                        for (int j = 0; j < GroupMembers.Count; j++)
                            GroupMembers[j].ReceiveChat(Name + " Picked up: {" + item.Item.Name + "}", ChatType.System);
                    GainItem(item.Item);
                    CurrentMap.RemoveObject(ob);
                    ob.Despawn();
                    return;
                }

                if (!CanGainGold(item.Gold)) continue;

                GainGold(item.Gold);
                CurrentMap.RemoveObject(ob);
                ob.Despawn();
                return;
            }

            if (sendFail)
                ReceiveChat("Can not pick up, You do not own this item.", ChatType.System);

        }

        private bool IsGroupMember(MapObject player)
        {
            if (player.Race != ObjectType.Player) return false;
            return GroupMembers != null && GroupMembers.Contains(player);
        }

        public override bool CanGainGold(uint gold)
        {
            return (UInt64)gold + Account.Gold <= uint.MaxValue;
        }
        public override void WinGold(uint gold)
        {
            if (GroupMembers == null)
            {
                GainGold(gold);
                return;
            }

            uint count = 0;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject player = GroupMembers[i];
                if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    count++;
            }

            if (count == 0 || count > gold)
            {
                GainGold(gold);
                return;
            }
            gold = gold / count;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject player = GroupMembers[i];
                if (player.CurrentMap == CurrentMap && Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) && !player.Dead)
                    player.GainGold(gold);
            }
        }
        public void GainGold(uint gold)
        {
            if (((UInt64)Account.Gold + gold) > uint.MaxValue)
                gold = uint.MaxValue - Account.Gold;
            
            Account.Gold += gold;

            Enqueue(new S.GainedGold { Gold = gold });
        }

        public void GainItem(UserItem item)
        {
            CheckItemInfo(item.Info);

            Enqueue(new S.GainedItem { Item = item.Clone() }); //Cloned because we are probably going to change the amount.

            AddItem(item);
            RefreshBagWeight();
        }
        public bool CanGainItem(UserItem item, bool useWeight = true)
        {
            if (item.Info.Type == ItemType.Amulet)
            {
                if (FreeSpace(Info.Inventory) > 0 && (CurrentBagWeight + item.Weight <= MaxBagWeight || !useWeight)) return true;

                uint count = item.Count;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem bagItem = Info.Inventory[i];

                    if (bagItem == null || bagItem.Info != item.Info) continue;

                    if (bagItem.Count + count <= bagItem.Info.StackSize) return true;

                    count -= bagItem.Info.StackSize - bagItem.Count;
                }

                return false;
            }

            if (useWeight && CurrentBagWeight + (item.Weight) > MaxBagWeight) return false;

            if (FreeSpace(Info.Inventory) > 0) return true;

            if (item.Info.StackSize > 1)
            {
                uint count = item.Count;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem bagItem = Info.Inventory[i];

                    if (bagItem.Info != item.Info) continue;

                    if (bagItem.Count + count <= bagItem.Info.StackSize) return true;

                    count -= bagItem.Info.StackSize - bagItem.Count;
                }
            }

            return false;
        }
        private bool DropItem(UserItem item, int range = 1)
        {
            ItemObject ob = new ItemObject(this, item);

            if (!ob.Drop(range)) return false;

            if (item.Info.Type == ItemType.Meat)
                item.CurrentDura = (ushort) Math.Max(0, item.CurrentDura - 2000);

            return true;
        }
        private bool CanUseItem(UserItem item)
        {
            if (item == null) return false;

            switch (Gender)
            {
                case MirGender.Male:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.Male))
                    {
                        ReceiveChat("You are not Female.", ChatType.System);
                        return false;
                    }
                    break;
                case MirGender.Female:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.Female))
                    {
                        ReceiveChat("You are not Male.", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (Class)
            {
                case MirClass.Warrior:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Warrior))
                    {
                        ReceiveChat("Warriors cannot use this item.", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.Wizard:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Wizard))
                    {
                        ReceiveChat("Wizards cannot use this item.", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.Taoist:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Taoist))
                    {
                        ReceiveChat("Taoists cannot use this item.", ChatType.System);
                        return false;
                    }
                    break;
                case MirClass.Assassin:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Assassin))
                    {
                        ReceiveChat("Assassins cannot use this item.", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (item.Info.RequiredType)
            {
                case RequiredType.Level:
                    if (Level < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You are not a high enough level.", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.AC:
                    if (MaxAC < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You do not have enough AC.", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MAC:
                    if (MaxMAC < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You do not have enough MAC.", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.DC:
                    if (MaxDC < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You do not have enough DC.", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.MC:
                    if (MaxMC < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You do not have enough MC.", ChatType.System);
                        return false;
                    }
                    break;
                case RequiredType.SC:
                    if (MaxSC < item.Info.RequiredAmount)
                    {
                        ReceiveChat("You do not have enough SC.", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (item.Info.Shape)
            {
                case 0:
                    if (CurrentMap.Info.NoEscape)
                    {
                        ReceiveChat("You cannot use Dungeon Escapes here", ChatType.System);
                        return false;
                    }
                    break;
                case 2:
                    if (CurrentMap.Info.NoRandom)
                    {
                        ReceiveChat("You cannot use Random Teleports here", ChatType.System);
                        return false;
                    }
                    break;
            }

            switch (item.Info.Type)
            {
                case ItemType.Potion:
                    if (CurrentMap.Info.NoDrug)
                    {
                        ReceiveChat("You cannot use Potions here", ChatType.System);
                        return false;
                    }
                    break;

                case ItemType.Book:
                    if (Info.Magics.Any(t => t.Spell == (Spell)item.Info.Shape))
                    {
                        return false;
                    }
                    break;
            }

            //if (item.Info.Type == ItemType.Book)
            //    for (int i = 0; i < Info.Magics.Count; i++)
            //        if (Info.Magics[i].Spell == (Spell)item.Info.Shape) return false;

            return true;
        }
        private bool CanEquipItem(UserItem item, int slot)
        {

            switch ((EquipmentSlot)slot)
            {
                case EquipmentSlot.Weapon:
                    if (item.Info.Type != ItemType.Weapon)
                        return false;
                    break;
                case EquipmentSlot.Armour:
                    if (item.Info.Type != ItemType.Armour)
                        return false;
                    break;
                case EquipmentSlot.Helmet:
                    if (item.Info.Type != ItemType.Helmet)
                        return false;
                    break;
                case EquipmentSlot.Torch:
                    if (item.Info.Type != ItemType.Torch)
                        return false;
                    break;
                case EquipmentSlot.Necklace:
                    if (item.Info.Type != ItemType.Necklace)
                        return false;
                    break;
                case EquipmentSlot.BraceletL:
                    if (item.Info.Type != ItemType.Bracelet)
                        return false;
                    break;
                case EquipmentSlot.BraceletR:
                    if (item.Info.Type != ItemType.Bracelet)
                        return false;
                    break;
                case EquipmentSlot.RingL:
                case EquipmentSlot.RingR:
                    if (item.Info.Type != ItemType.Ring)
                        return false;
                    break;
                case EquipmentSlot.Amulet:
                    if (item.Info.Type != ItemType.Amulet || item.Info.Shape == 0)
                        return false;
                    break;
                case EquipmentSlot.Boots:
                    if (item.Info.Type != ItemType.Boots)
                        return false;
                    break;
                case EquipmentSlot.Belt:
                    if (item.Info.Type != ItemType.Belt)
                        return false;
                    break;
                case EquipmentSlot.Stone:
                    if (item.Info.Type != ItemType.Stone)
                        return false;
                    break;
                default:
                    return false;
            }


            switch (Gender)
            {
                case MirGender.Male:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.Male))
                        return false;
                    break;
                case MirGender.Female:
                    if (!item.Info.RequiredGender.HasFlag(RequiredGender.Female))
                        return false;
                    break;
            }


            switch (Class)
            {
                case MirClass.Warrior:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Warrior))
                        return false;
                    break;
                case MirClass.Wizard:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Wizard))
                        return false;
                    break;
                case MirClass.Taoist:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Taoist))
                        return false;
                    break;
                case MirClass.Assassin:
                    if (!item.Info.RequiredClass.HasFlag(RequiredClass.Assassin))
                        return false;
                    break;
            }

            switch (item.Info.RequiredType)
            {
                case RequiredType.Level:
                    if (Level < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.AC:
                    if (MaxAC < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MAC:
                    if (MaxMAC < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.DC:
                    if (MaxDC < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.MC:
                    if (MaxMC < item.Info.RequiredAmount)
                        return false;
                    break;
                case RequiredType.SC:
                    if (MaxSC < item.Info.RequiredAmount)
                        return false;
                    break;
            }

            if (item.Info.Type == ItemType.Weapon || item.Info.Type == ItemType.Torch)
            {
                if (item.Weight - (Info.Equipment[slot] != null ? Info.Equipment[slot].Weight : 0) + CurrentHandWeight > MaxHandWeight)
                    return false;
            }
            else
                if (item.Weight - (Info.Equipment[slot] != null ? Info.Equipment[slot].Weight : 0) + CurrentWearWeight > MaxWearWeight)
                    return false;

            return true;
        }
        private bool CanRemoveItem(MirGridType grid, UserItem item)
        {
            //Item  Stuck


            UserItem[] array;
            switch (grid)
            {
                case MirGridType.Inventory:
                    array = Info.Inventory;
                    break;
                case MirGridType.Storage:
                    array = Account.Storage;
                    break;
                default:
                    return false;
            }

            return FreeSpace(array) > 0;
        }
        private void DamageDura()
        {
            if (!NoDuraLoss)
                for (int i = 0; i < Info.Equipment.Length; i++) DamageItem(Info.Equipment[i], Envir.Random.Next(1) + 1);          
        }
        public void DamageWeapon()
        {
            if (!NoDuraLoss)
                DamageItem(Info.Equipment[(int)EquipmentSlot.Weapon], Envir.Random.Next(4) + 1);
        }
        private void DamageItem(UserItem item, int amount)
        {
            if (item == null || item.CurrentDura == 0 || item.Info.Type == ItemType.Amulet) return;
            if (item.Info.Strong > 0) amount = Math.Max(1, amount - item.Info.Strong);
            item.CurrentDura = (ushort) Math.Max(ushort.MinValue, item.CurrentDura - amount);
            item.DuraChanged = true;

            if (item.CurrentDura > 0) return;
            Enqueue(new S.DuraChanged {UniqueID = item.UniqueID, CurrentDura = item.CurrentDura});
            item.DuraChanged = false;
            RefreshStats();
        }
        private void ConsumeItem(UserItem item, uint cost)
        {
            item.Count -= cost;
            Enqueue(new S.DeleteItem {UniqueID = item.UniqueID, Count = cost});


            if (item.Count != 0) return;

            for (int i = 0; i < Info.Equipment.Length; i++)
            {
                if (Info.Equipment[i] != item) continue;
                Info.Equipment[i] = null;
                return;
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != item) continue;
                Info.Inventory[i] = null;
                return;
            }
            //Item not found
        }

        public bool TryLuckWeapon()
        {
            UserItem item = Info.Equipment[(int)EquipmentSlot.Weapon];

            if (item == null || item.Luck >= 7) return false;

            if (item.Luck > -10 && Envir.Random.Next(20) == 0)
            {
                Luck--;
                item.Luck--;
                Enqueue(new S.RefreshItem { Item = item });
                ReceiveChat("Curse dwells within your weapon.", ChatType.System);
            }
            else if (item.Luck <= 0 || Envir.Random.Next(10*item.Luck) == 0)
            {
                Luck++;
                item.Luck++;
                Enqueue(new S.RefreshItem { Item = item });
                ReceiveChat("Luck dwells within your weapon.", ChatType.Hint);
            }
            else
            {
                ReceiveChat("No effect.", ChatType.Hint);
            }

            return true;
        }


        public void CallNPC(uint objectID, string key)
        {
            if (NPCJumpPage != null) NPCJumpPage.Interrupted = true;

            if (Dead) return;
            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != objectID) continue;
                ob.Call(this, NPCGoto ? NPCGotoPage.ToUpper() : key.ToUpper());

                if(NPCGoto) i--;
                else break;
            }
        }

        public void BuyItem(int index, uint count)
        {
            if (Dead) return;

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.BuyKey, StringComparison.CurrentCultureIgnoreCase)) return;

            for (int i = 0; i < CurrentMap.NPCs.Count; i++)
            {
                NPCObject ob = CurrentMap.NPCs[i];
                if (ob.ObjectID != NPCID) continue;
                ob.Buy(this, index, count);
            }
        }
        public void SellItem(ulong uniqueID, uint count)
        {
            S.SellItem p = new S.SellItem {UniqueID = uniqueID, Count = count};
            if (Dead || count == 0)
            {
                Enqueue(p);
                return;
            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.SellKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(p);
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1 || count > temp.Count)
                {
                    Enqueue(p);
                    return;
                }

                if (temp.Info.Bind.HasFlag(BindMode.DontSell))
                {
                    Enqueue(p);
                    return;
                }

                if (ob.Types.Count != 0 && !ob.Types.Contains(temp.Info.Type))
                {
                    ReceiveChat("You cannot sell this item here.", ChatType.System);
                    Enqueue(p);
                    return;
                }

                if (temp.Info.StackSize > 1 && count != temp.Count)
                {
                    UserItem item = Envir.CreateFreshItem(temp.Info);
                    item.Count = count;

                    if (item.Price() / 2 + Account.Gold > uint.MaxValue)
                    {
                        Enqueue(p);
                        return;
                    }

                    temp.Count -= count;
                    temp = item;
                }
                else Info.Inventory[index] = null;

                ob.Sell(temp);
                p.Success = true;
                Enqueue(p);
                GainGold(temp.Price() / 2);
                RefreshBagWeight();
                return;
            }



            Enqueue(p);
        }
        public void RepairItem(ulong uniqueID, bool special = false)
        {
            Enqueue(new S.RepairItem { UniqueID = uniqueID });

            if (Dead) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCObject.RepairKey, StringComparison.CurrentCultureIgnoreCase) && !special) || (!String.Equals(NPCPage.Key, NPCObject.SRepairKey, StringComparison.CurrentCultureIgnoreCase) && special)) return;

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1) return;

                if ((temp.Info.Bind.HasFlag(BindMode.DontRepair)) || (temp.Info.BindNoSRepair && special))
                {
                    ReceiveChat("You cannot Repair this item.", ChatType.System);
                    return;
                }

                if (ob.Types.Count != 0 && !ob.Types.Contains(temp.Info.Type))
                {
                    ReceiveChat("You cannot Repair this item here.", ChatType.System);
                    return;
                }

                uint cost = (uint) (temp.RepairPrice() * ob.Info.PriceRate);

                if (cost > Account.Gold || cost == 0) return;

                Account.Gold -= cost;
                Enqueue(new S.LoseGold { Gold = cost });

                if (!special) temp.MaxDura = (ushort) Math.Max(0, temp.MaxDura - (temp.MaxDura - temp.CurrentDura)/8);

                temp.CurrentDura = temp.MaxDura;
                temp.DuraChanged = false;

                Enqueue(new S.ItemRepaired { UniqueID = uniqueID, MaxDura = temp.MaxDura, CurrentDura = temp.CurrentDura });
                return;
            }
        }
        public void SendStorage()
        {
            if (Connection.StorageSent) return;
            Connection.StorageSent = true;

            for (int i = 0; i < Account.Storage.Length; i++)
            {
                UserItem item = Account.Storage[i];
                if (item == null) continue;
                CheckItemInfo(item.Info);
            }

            Enqueue(new S.UserStorage { Storage = Account.Storage }); // Should be no alter before being sent.
        }


        public void ConsignItem(ulong uniqueID, uint price)
        {
            S.ConsignItem p = new S.ConsignItem { UniqueID = uniqueID };
            if (price < Globals.MinConsignment || price > Globals.MaxConsignment || Dead)
            {
                Enqueue(p);
                return;
            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.ConsignKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(p);
                return;
            }

            if (Account.Gold < Globals.ConsignmentCost)
            {
                Enqueue(p);
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;
                
                UserItem temp = null;
                int index = -1;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    temp = Info.Inventory[i];
                    if (temp == null || temp.UniqueID != uniqueID) continue;
                    index = i;
                    break;
                }

                if (temp == null || index == -1)
                {
                    Enqueue(p);
                    return;
                }

                if (temp.Info.Bind.HasFlag(BindMode.DontSell))
                {
                    Enqueue(p);
                    return;
                }

                //Check Max Consignment.

                AuctionInfo auction = new AuctionInfo
                    {
                        AuctionID = ++Envir.NextAuctionID,
                        CharacterIndex = Info.Index,
                        CharacterInfo = Info,
                        ConsignmentDate = Envir.Now,
                        Item = temp,
                        Price = price
                    };

                Account.Auctions.AddLast(auction);
                Envir.Auctions.AddFirst(auction);

                p.Success = true;
                Enqueue(p);

                Info.Inventory[index] = null;
                Account.Gold -= Globals.ConsignmentCost;
                Enqueue(new S.LoseGold {Gold = Globals.ConsignmentCost});
                RefreshBagWeight();

            }

            Enqueue(p);
        }
        public bool Match(AuctionInfo info)
        {
            if (Envir.Now >= info.ConsignmentDate.AddDays(Globals.ConsignmentLength) && !info.Sold)
                info.Expired = true;

            return (UserMatch || !info.Expired && !info.Sold) && ((MatchType == ItemType.Nothing || info.Item.Info.Type == MatchType) &&
                (string.IsNullOrWhiteSpace(MatchName) || info.Item.Info.Name.Replace(" ", "").IndexOf(MatchName, StringComparison.OrdinalIgnoreCase) >= 0));
        }
        public void MarketPage(int page)
        {
            if (Dead || Envir.Time < SearchTime) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCObject.MarketKey, StringComparison.CurrentCultureIgnoreCase) && !String.Equals(NPCPage.Key, NPCObject.ConsignmentsKey, StringComparison.CurrentCultureIgnoreCase)) || page <= PageSent) return;

            SearchTime = Envir.Time + Globals.SearchDelay;

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                List<ClientAuction> listings = new List<ClientAuction>();

                for (int i = 0; i < 10; i++)
                {
                    if (i + page*10 >= Search.Count) break;
                    listings.Add(Search[i + page * 10].CreateClientAuction(UserMatch));
                }

                for (int i = 0; i < listings.Count; i++)
                    CheckItemInfo(listings[i].Item.Info);

                PageSent = page;
                Enqueue(new S.NPCMarketPage {Listings = listings});
            }
        }
        public void GetMarket(string name, ItemType type)
        {
            Search.Clear();
            MatchName = name.Replace(" ", "");
            MatchType = type;
            PageSent = 0;

            long start = Envir._stopwatch.ElapsedMilliseconds;

            LinkedListNode<AuctionInfo> current = UserMatch ? Account.Auctions.First : Envir.Auctions.First;

            while (current != null)
            {
                if (Match(current.Value)) Search.Add(current.Value);
                current = current.Next;
            }

            List<ClientAuction> listings = new List<ClientAuction>();

            for (int i = 0; i < 10; i++)
            {
                if (i >= Search.Count) break;
                listings.Add(Search[i].CreateClientAuction(UserMatch));
            }

            for (int i = 0; i < listings.Count; i++)
                CheckItemInfo(listings[i].Item.Info);

            Enqueue(new S.NPCMarket {Listings = listings, Pages = (Search.Count - 1)/10 + 1, UserMode = UserMatch});

            SMain.EnqueueDebugging(string.Format("{0}ms to match {1} items", Envir._stopwatch.ElapsedMilliseconds - start, UserMatch ? Account.Auctions.Count : Envir.Auctions.Count));
        }
        public void MarketSearch(string match)
        {
            if (Dead || Envir.Time < SearchTime) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCObject.MarketKey, StringComparison.CurrentCultureIgnoreCase) && !String.Equals(NPCPage.Key, NPCObject.ConsignmentsKey, StringComparison.CurrentCultureIgnoreCase))) return;

            SearchTime = Envir.Time + Globals.SearchDelay;

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                GetMarket(match, ItemType.Nothing);
            }
        }
        public void MarketRefresh()
        {
            if (Dead || Envir.Time < SearchTime) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCObject.MarketKey, StringComparison.CurrentCultureIgnoreCase) && !String.Equals(NPCPage.Key, NPCObject.ConsignmentsKey, StringComparison.CurrentCultureIgnoreCase))) return;

            SearchTime = Envir.Time + Globals.SearchDelay;

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;
                
                GetMarket(string.Empty, MatchType);
            }
        }
        public void MarketBuy(ulong auctionID)
        {
            if (Dead)
            {
                Enqueue(new S.MarketFail {Reason = 0});
                return;
                
            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.MarketKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(new S.MarketFail {Reason = 1});
                return;
            }
            
            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                foreach (AuctionInfo auction in Search)
                {
                    if (auction.AuctionID != auctionID) continue;


                    if (auction.Sold)
                    {
                        Enqueue(new S.MarketFail { Reason = 2 });
                        return;
                    }

                    if (auction.Expired)
                    {
                        Enqueue(new S.MarketFail { Reason = 3 });
                        return;
                    }

                    if (auction.Price > Account.Gold)
                    {
                        Enqueue(new S.MarketFail { Reason = 4 });
                        return;
                    }

                    if (!CanGainItem(auction.Item))
                    {
                        Enqueue(new S.MarketFail { Reason = 5 });
                        return;
                    }

                    if (Account.Auctions.Contains(auction))
                    {
                        Enqueue(new S.MarketFail { Reason = 6 });
                        return;
                    }
                    
                    auction.Sold = true;
                    Account.Gold -= auction.Price;
                    Enqueue(new S.LoseGold {Gold = auction.Price});
                    GainItem(auction.Item);
                    Envir.MessageAccount(auction.CharacterInfo.AccountInfo, string.Format("You Sold {0} for {1:#,##0} Gold", auction.Item.Name, auction.Price), ChatType.Hint);
                    Enqueue(new S.MarketSuccess { Message = string.Format("You brought {0} for {1:#,##0} Gold", auction.Item.Name, auction.Price) });
                    MarketSearch(MatchName);
                    return;
                }
            }

            Enqueue(new S.MarketFail { Reason = 7 });
        }
        public void MarketGetBack(ulong auctionID)
        {
            if (Dead)
            {
                Enqueue(new S.MarketFail { Reason = 0 });
                return;

            }

            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCObject.ConsignmentsKey, StringComparison.CurrentCultureIgnoreCase))
            {
                Enqueue(new S.MarketFail { Reason = 1 });
                return;
            }

            for (int n = 0; n < CurrentMap.NPCs.Count; n++)
            {
                NPCObject ob = CurrentMap.NPCs[n];
                if (ob.ObjectID != NPCID) continue;

                foreach (AuctionInfo auction in Account.Auctions)
                {
                    if (auction.AuctionID != auctionID) continue;

                    if (auction.Sold && auction.Expired)
                    {
                        SMain.Enqueue(string.Format("Auction both sold and Expired {0}", Account.AccountID));
                        return;
                    }


                    if (!auction.Sold || auction.Expired)
                    {
                        if (!CanGainItem(auction.Item))
                        {
                            Enqueue(new S.MarketFail { Reason = 5 });
                            return;
                        }

                        Account.Auctions.Remove(auction);
                        Envir.Auctions.Remove(auction);
                        GainItem(auction.Item);
                        MarketSearch(MatchName);
                        return;
                    }

                    uint gold = (uint) Math.Max(0, auction.Price - auction.Price*Globals.Commission);
                    if (!CanGainGold(gold))
                    {
                        Enqueue(new S.MarketFail { Reason = 8 });
                        return;
                    }

                    Account.Auctions.Remove(auction);
                    Envir.Auctions.Remove(auction);
                    GainGold(gold);
                    Enqueue(new S.MarketSuccess { Message = string.Format("You Sold {0} for {1:#,##0} Gold. \nEarnings: {2:#,##0} Gold.\nCommision: {3:#,##0} Gold.‎", auction.Item.Name, auction.Price, gold, auction.Price - gold) });
                    MarketSearch(MatchName);
                    return;
                }

            }

            Enqueue(new S.MarketFail { Reason = 7 });
        }
        public void RequestUserName(uint id)
        {
            CharacterInfo Character = Envir.GetCharacterInfo((int)id);
            if (Character != null)
                Enqueue(new S.UserName { Id = (uint)Character.Index, Name = Character.Name });

            /*
            for (int i = 0; i < Envir.Players.Count; i++)
            {
                if (Envir.Players[i].Info.Index == id)
                {
                    Enqueue(new S.UserName { Id = (uint)Envir.Players[i].Info.Index, Name = Envir.Players[i].Name });
                    return;
                }
            }
            */
        }

        public void RequestChatItem(ulong id)
        {
            //Enqueue(new S.ChatItemStats { ChatItemId = id, Stats = whatever });
        }
        public void Inspect(uint id)
        {
            if (ObjectID == id) return;

            
            PlayerObject player = CurrentMap.Players.SingleOrDefault(x => x.ObjectID == id || x.Pets.Count(y => y.ObjectID == id && y is Monsters.HumanWizard) > 0);

            if (player == null) return;

            for (int i = 0; i < player.Info.Equipment.Length; i++)
            {
                UserItem u = player.Info.Equipment[i];
                if (u == null) continue;

                CheckItemInfo(u.Info);
            }
            string guildname = "";
            string guildrank = "";
            if (player.MyGuild != null)
            {
                guildname = player.MyGuild.Name;
                guildrank = player.MyGuildRank.Name;
            }
            Enqueue(new S.PlayerInspect
            {
                Name = player.Name,
                Equipment = player.Info.Equipment,
                GuildName = guildname,
                GuildRank = guildrank,
                Hair = player.Hair,
                Gender = player.Gender,
                Class = player.Class,
                Level = player.Level
            });
        }
        public void RemoveObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Remove(this);
                            }
                        }
                    }
                    break;
            }
        }
        public void AddObjects(MirDirection dir, int count)
        {
            switch (dir)
            {
                case MirDirection.Up:
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpRight:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Right:
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownRight:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Right Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X + Globals.DataRange - b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Down:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.DownLeft:
                    //Bottom Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y + Globals.DataRange - a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange - count; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.Left:
                    //Left Block
                    for (int a = -Globals.DataRange; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
                case MirDirection.UpLeft:
                    //Top Block
                    for (int a = 0; a < count; a++)
                    {
                        int y = CurrentLocation.Y - Globals.DataRange + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = -Globals.DataRange; b <= Globals.DataRange; b++)
                        {
                            int x = CurrentLocation.X + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }

                    //Left Block
                    for (int a = -Globals.DataRange + count; a <= Globals.DataRange; a++)
                    {
                        int y = CurrentLocation.Y + a;
                        if (y < 0 || y >= CurrentMap.Height) continue;

                        for (int b = 0; b < count; b++)
                        {
                            int x = CurrentLocation.X - Globals.DataRange + b;
                            if (x < 0 || x >= CurrentMap.Width) continue;

                            Cell cell = CurrentMap.GetCell(x, y);

                            if (!cell.Valid || cell.Objects == null) continue;

                            for (int i = 0; i < cell.Objects.Count; i++)
                            {
                                MapObject ob = cell.Objects[i];
                                ob.Add(this);
                            }
                        }
                    }
                    break;
            }
        }
        public override void Remove(PlayerObject player)
        {
            if (player == this) return;

            base.Remove(player);
            Enqueue(new S.ObjectRemove {ObjectID = player.ObjectID});
        }
        public override void Add(PlayerObject player)
        {
            if (player == this) return;

            base.Add(player);
            Enqueue(player.GetInfo());

            player.SendHealth(this);
            SendHealth(player);
        }
        public override void Remove(MonsterObject monster)
        {
            Enqueue(new S.ObjectRemove { ObjectID = monster.ObjectID });
        }
        public override void Add(MonsterObject monster)
        {
            Enqueue(monster.GetInfo());

            monster.SendHealth(this);
        }
        public override void SendHealth(PlayerObject player)
        {
            if (!player.IsMember(this) && Envir.Time > RevTime) return;
            byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));
            player.Enqueue(new S.ObjectHealth { ObjectID = ObjectID, Percent = PercentHealth, Expire = time });
        }
        
        public override void ReceiveChat(string text, ChatType type)
        {
            Enqueue(new S.Chat { Message = text, Type = type });
        }

        private void CleanUp()
        {
            Connection.Player = null;
            Info.Player = null;
            Connection = null;
            Account = null;
            Info = null;
        }

        public void SwitchGroup(bool allow)
        {
            Enqueue(new S.SwitchGroup { AllowGroup = allow });

            if (AllowGroup == allow) return;
            AllowGroup = allow;

            if (AllowGroup || GroupMembers == null) return;
            GroupMembers.Remove(this);
            Enqueue(new S.DeleteGroup());

            if (GroupMembers.Count > 1)
            {
                Packet p = new S.DeleteMember { Name = Name };

                for (int i = 0; i < GroupMembers.Count; i++)
                    GroupMembers[i].Enqueue(p);
            }
            else
            {
                GroupMembers[0].Enqueue(new S.DeleteGroup());
                GroupMembers[0].GroupMembers = null;
            }
            GroupMembers = null;
        }
        public void AddMember(string name)
        {
            if (GroupMembers != null && GroupMembers[0] != this)
            {
                ReceiveChat("You are not the group leader.", ChatType.System);
                return;
            }

            if (GroupMembers != null && GroupMembers.Count >= Globals.MaxGroup)
            {
                ReceiveChat("Your group already has the maximum number of members.", ChatType.System);
                return;
            }

            PlayerObject player = Envir.GetPlayer(name);

            if (player == null)
            {
                ReceiveChat(name + " could not be found.", ChatType.System);
                return;
            }
            if (player == this)
            {
                ReceiveChat("You cannot group yourself.", ChatType.System);
                return;
            }

            if (!player.AllowGroup)
            {
                ReceiveChat(name + " is not allowing group.", ChatType.System);
                return;
            }

            if (player.GroupMembers != null)
            {
                ReceiveChat(name + " is already in another group.", ChatType.System);
                return;
            }

            if (player.GroupInvitation != null)
            {
                ReceiveChat(name + " is already receiving an invite from another player.", ChatType.System);
                return;
            }

            SwitchGroup(true);
            player.Enqueue(new S.GroupInvite { Name = Name });
            player.GroupInvitation = this;

        }
        public void DelMember(string name)
        {
            if (GroupMembers == null)
            {
                ReceiveChat("You are not in a group.", ChatType.System);
                return;
            }
            if (GroupMembers[0] != this)
            {
                ReceiveChat("You are not the group leader.", ChatType.System);
                return;
            }

            PlayerObject player = null;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                if (String.Compare(GroupMembers[i].Name, name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                player = GroupMembers[i];
                break;
            }


            if (player == null)
            {
                ReceiveChat(name + " is not in your group.", ChatType.System);
                return;
            }

            GroupMembers.Remove(player);
            player.Enqueue(new S.DeleteGroup());

            if (GroupMembers.Count > 1)
            {
                Packet p = new S.DeleteMember { Name = player.Name };

                for (int i = 0; i < GroupMembers.Count; i++)
                    GroupMembers[i].Enqueue(p);
            }
            else
            {
                GroupMembers[0].Enqueue(new S.DeleteGroup());
                GroupMembers[0].GroupMembers = null;
            }
            player.GroupMembers = null;
        }
        public void GroupInvite(bool accept)
        {
            if (GroupInvitation == null)
            {
                ReceiveChat("You have not been invited to a group.", ChatType.System);
                return;
            }

            if (!accept)
            {
                GroupInvitation.ReceiveChat(Name + " has declined your group invite.", ChatType.System);
                GroupInvitation = null;
                return;
            }


            if (GroupInvitation.GroupMembers != null && GroupInvitation.GroupMembers[0] != GroupInvitation)
            {
                ReceiveChat(GroupInvitation.Name + " is no longer the group leader.", ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.GroupMembers != null && GroupInvitation.GroupMembers.Count >= Globals.MaxGroup)
            {
                ReceiveChat(GroupInvitation.Name + "'s group already has the maximum number of members.", ChatType.System);
                GroupInvitation = null;
                return;
            }
            if (!GroupInvitation.AllowGroup)
            {
                ReceiveChat(GroupInvitation.Name + " is not on allow group.", ChatType.System);
                GroupInvitation = null;
                return;
            }
            if (GroupInvitation.Node == null)
            {
                ReceiveChat(GroupInvitation.Name + " no longer online.", ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.GroupMembers == null)
            {
                GroupInvitation.GroupMembers = new List<PlayerObject> { GroupInvitation };
                GroupInvitation.Enqueue(new S.AddMember { Name = GroupInvitation.Name });
            }

            Packet p = new S.AddMember { Name = Name };
            GroupMembers = GroupInvitation.GroupMembers;
            GroupInvitation = null;
            

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject member = GroupMembers[i];
                member.Enqueue(p);
                Enqueue(new S.AddMember { Name = member.Name });

                if (CurrentMap != member.CurrentMap || !Functions.InRange(CurrentLocation, member.CurrentLocation, Globals.DataRange)) continue;
                
                byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));
                member.Enqueue(new S.ObjectHealth { ObjectID = ObjectID, Percent = member.PercentHealth, Expire = time });
                Enqueue(new S.ObjectHealth { ObjectID = member.ObjectID, Percent = member.PercentHealth, Expire = time });
            }

            GroupMembers.Add(this);

            Enqueue(p);
        }

        public void GuildInvite(bool accept)
        {
            if (PendingGuildInvite == null)
            {
                ReceiveChat("You have not been invited to a guild.", ChatType.System);
                return;
            }
            if (!accept) return;
            if (!PendingGuildInvite.HasRoom())
            {
                ReceiveChat(String.Format("{0} is full.",PendingGuildInvite.Name), ChatType.System);
                return;
            }
            PendingGuildInvite.NewMember(this);
            Info.GuildIndex = PendingGuildInvite.Guildindex;
            MyGuild = PendingGuildInvite;
            MyGuildRank = PendingGuildInvite.FindRank(Name);
            GuildMembersChanged = true;
            GuildNoticeChanged = true;
            //tell us we now have a guild
            Broadcast(GetInfo());
            MyGuild.SendGuildStatus(this);
            PendingGuildInvite = null;
            EnableGuildInvite = false;
            GuildCanRequestItems = true;
        }

        public void RequestGuildInfo(byte Type)
        {
            if (MyGuild == null) return;
            if (MyGuildRank == null) return;
            switch (Type)
            {
                case 0://notice
                    if (GuildNoticeChanged)
                        Enqueue(new S.GuildNoticeChange() { notice = MyGuild.Notice });
                    GuildNoticeChanged = false;
                    break;
                case 1://memberlist
                    if (GuildMembersChanged)
                        Enqueue(new S.GuildMemberChange() { Status = 255, Ranks = MyGuild.Ranks });
                    break;
            }
        }

        public void GuildNameReturn(string Name)
        {
            if (Name == "") CanCreateGuild = false;
            if (!CanCreateGuild) return;            
            if ((Name.Length < 3) || (Name.Length > 20))
            {
                ReceiveChat("Guild name to long.", ChatType.System);
                CanCreateGuild = false;
                return;
            }
            if (Name.Contains('\\'))
            {
                CanCreateGuild = false;
                return;
            }
            if (MyGuild != null)
            {
                ReceiveChat("You are already part of a guild.", ChatType.System);
                CanCreateGuild = false;
                return;
            }
            GuildObject guild = Envir.GetGuild(Name);
            if (guild != null)
            {
                ReceiveChat(string.Format("Guild {0} already exists.", Name), ChatType.System);
                CanCreateGuild = false;
                return;
            }


            CreateGuild(Name);
            CanCreateGuild = false;
        }

        public void GuildStorageGoldChange(Byte Type, uint Amount)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat("You are not part of a guild.", ChatType.System);
                return;
            }
            if (Type == 0)//donate
            {
                if (Account.Gold < Amount)
                {
                    ReceiveChat("Insufficient gold.", ChatType.System);
                    return;
                }
                if ((MyGuild.Gold + (UInt64)Amount) > uint.MaxValue)
                {
                    ReceiveChat("Guild gold limit reached.", ChatType.System);
                    return;
                }
                Account.Gold -= Amount;
                MyGuild.Gold += Amount;
                Enqueue(new S.LoseGold { Gold = Amount });
                MyGuild.SendServerPacket(new S.GuildStorageGoldChange() { Type = 0, Name = Info.Name, Amount = Amount });
                MyGuild.NeedSave = true;
            }
            else
            {
                if (MyGuild.Gold < Amount)
                {
                    ReceiveChat("Insufficient gold.", ChatType.System);
                    return;
                }
                if (!CanGainGold(Amount))
                {
                    ReceiveChat("Gold limit reached.", ChatType.System);
                    return;
                }
                if (MyGuildRank.Index != 0)
                {
                    ReceiveChat("Insufficient rank.", ChatType.System);
                    return;
                }
                MyGuild.Gold -= Amount;
                GainGold(Amount);
                MyGuild.SendServerPacket(new S.GuildStorageGoldChange() { Type = 1, Name = Info.Name, Amount = Amount });
                MyGuild.NeedSave = true;
            }
        }

        public void GuildStorageItemChange(Byte Type, int from, int to)
        {
            S.GuildStorageItemChange p = new S.GuildStorageItemChange { Type = (byte)(3 + Type), From = from, To = to };
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                Enqueue(p);
                ReceiveChat("You are not part of a guild.", ChatType.System);
                return;
            }

            switch (Type)
            {
                case 0://store
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanStoreItem))
                    {
                        Enqueue(p);
                        ReceiveChat("You do not have permission to store items in guild storage.", ChatType.System);
                        return;
                    }
                    if (from < 0 || from >= Info.Inventory.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (to < 0 || to >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[from] == null)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[from].Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[to] != null)
                    {
                        ReceiveChat("Target slot not empty.", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    MyGuild.StoredItems[to] = new GuildStorageItem() { Item = Info.Inventory[from], UserId = Info.Index };
                    Info.Inventory[from] = null;
                    RefreshBagWeight();
                    MyGuild.SendItemInfo(MyGuild.StoredItems[to].Item.Info);
                    MyGuild.SendServerPacket(new S.GuildStorageItemChange() { Type = 0, User = Info.Index, Item = MyGuild.StoredItems[to], To = to, From = from });
                    MyGuild.NeedSave = true;
                    break;
                case 1://retrieve
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanRetrieveItem))
                    {

                        ReceiveChat("You do not have permission to retrieve items from guild storage.", ChatType.System);
                        return;
                    }
                    if (from < 0 || from >= MyGuild.StoredItems.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (to < 0 || to >= Info.Inventory.Length)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (Info.Inventory[to] != null)
                    {
                        ReceiveChat("Target slot not empty.", ChatType.System);
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from] == null)
                    {
                        Enqueue(p);
                        return;
                    }
                    if (MyGuild.StoredItems[from].Item.Info.Bind.HasFlag(BindMode.DontStore))
                    {
                        Enqueue(p);
                        return;
                    }
                    Info.Inventory[to] = MyGuild.StoredItems[from].Item;
                    MyGuild.StoredItems[from] = null;
                    MyGuild.SendServerPacket(new S.GuildStorageItemChange() { Type = 1, User = Info.Index, To = to, From = from });
                    RefreshBagWeight();
                    MyGuild.NeedSave = true;
                    break;
                case 2://request list
                    if (!GuildCanRequestItems) return;
                    GuildCanRequestItems = false;
                    for (int i = 0; i < MyGuild.StoredItems.Length; i++)
                    {
                        if (MyGuild.StoredItems[i] == null) continue;
                        UserItem item = MyGuild.StoredItems[i].Item;
                        if (item == null) continue;
                        CheckItemInfo(item.Info);
                    }
                    Enqueue(new S.GuildStorageList() { Items = MyGuild.StoredItems });
                    break;
            }

        }

        public void Enqueue(Packet p)
        {
            if (Connection == null) return;
            Connection.Enqueue(p);
        }

        public void SpellToggle(Spell spell, bool use)
        {
            UserMagic magic;
            int cost;
            switch (spell)
            {
                case Spell.Thrusting:
                    Info.Thrusting = use;
                    break;
                case Spell.HalfMoon:
                    Info.HalfMoon = use;
                    break;
                case Spell.CrossHalfMoon:
                    Info.CrossHalfMoon = use;
                    break;
                case Spell.DoubleSlash:
                    Info.DoubleSlash = use;
                    break;
                case Spell.TwinDrakeBlade:
                    if (TwinDrakeBlade) return;
                    magic = GetMagic(spell);
                    if (magic == null) return;
                    cost = magic.Info.BaseCost + magic.Level*magic.Info.LevelCost;
                    if (cost >= MP) return;

                    TwinDrakeBlade = true;
                    ChangeMP(-cost);
                    break;
                case Spell.FlamingSword:
                    if (FlamingSword || Envir.Time < FlamingSwordTime) return;
                    magic = GetMagic(spell);
                    if (magic == null) return;
                    cost = magic.Info.BaseCost + magic.Level * magic.Info.LevelCost;
                    if (cost >= MP) return;

                    FlamingSword = true;
                    FlamingSwordTime = Envir.Time + 10000;
                    Enqueue(new S.SpellToggle { Spell = Spell.FlamingSword, CanUse = true });
                    ChangeMP(-cost);
                    break;
            }
        }

        public bool CreateGuild(string GuildName)
        {
            if ((MyGuild != null) || (Info.GuildIndex != -1)) return false;
            if (Envir.GetGuild(GuildName) != null) return false;
            if (Info.Level < Settings.Guild_RequiredLevel)
            {
                ReceiveChat(String.Format("Your level is not high enough to create a guild, required: {0}", Settings.Guild_RequiredLevel), ChatType.System);
                return false;
            }
            //check if we have the required items
            for (int i = 0; i < Settings.Guild_CreationCostList.Count; i++)
            {
                ItemVolume Required = Settings.Guild_CreationCostList[i];
                if (Required.Item == null)
                {
                    if (Info.AccountInfo.Gold < Required.Amount)
                    {
                        ReceiveChat(String.Format("Insufficient gold. Creating a guild requires {0} gold.", Required.Amount), ChatType.System);
                        return false;
                    }
                }
                else
                {
                    uint count = Required.Amount;
                    foreach (var item in Info.Inventory.Where(item => item != null && item.Info == Required.Item))
                    {
                        if ((Required.Item.Type == ItemType.Ore) && (item.CurrentDura / 1000 > Required.Amount))
                        {
                            count = 0;
                            break;
                        }
                        if (item.Count > count)
                            count = 0;
                        else
                            count = count - item.Count;
                        if (count == 0) break;
                    }
                    if (count != 0)
                    {
                        if (Required.Amount == 1)
                            ReceiveChat(String.Format("{0} is required to create a guild.", Required.Item.Name), ChatType.System);
                        else
                        {
                            if (Required.Item.Type == ItemType.Ore)
                                ReceiveChat(string.Format("{0} with purity {1} is recuired to create a guild.", Required.Item.Name, Required.Amount / 1000), ChatType.System);
                            else
                                ReceiveChat(string.Format("Insufficient {0}, you need {1} to create a guild.", Required.Item.Name, Required.Amount), ChatType.System);
                        }
                        return false;
                    }
                }
            }
            //take the required items
            for (int i = 0; i < Settings.Guild_CreationCostList.Count; i++)
            {
                ItemVolume Required = Settings.Guild_CreationCostList[i];
                if (Required.Item == null)
                {
                    if (Info.AccountInfo.Gold >= Required.Amount)
                    {
                        Info.AccountInfo.Gold -= Required.Amount;
                        Enqueue(new S.LoseGold { Gold = Required.Amount });
                    }
                }
                else
                {
                    uint count = Required.Amount;
                    for (int o = 0; o < Info.Inventory.Length; o++)
                    {
                        UserItem item = Info.Inventory[o];
                        if (item == null) continue;
                        if (item.Info != Required.Item) continue;
                            
                        if ((Required.Item.Type == ItemType.Ore) && (item.CurrentDura / 1000 > Required.Amount))
                        {
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                            Info.Inventory[o] = null;
                            break;
                        }
                        if (count > item.Count)
                        {
                            Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                            Info.Inventory[o] = null;
                            count -= item.Count;
                            continue;
                        }
                        
                        Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = count });
                        if (count == item.Count)
                            Info.Inventory[o] = null;
                        else
                            item.Count -= count;
                        break;
                    }
                }
            }
            RefreshStats();
            //make the guild
            GuildObject guild = new GuildObject(this, GuildName) { Guildindex = ++Envir.NextGuildID };
            Envir.GuildList.Add(guild);
            Info.GuildIndex = guild.Guildindex;
            MyGuild = guild;
            MyGuildRank = guild.FindRank(Name);
            GuildMembersChanged = true;
            GuildNoticeChanged = true;
            GuildCanRequestItems = true;
            //tell us we now have a guild
            Broadcast(GetInfo());
            MyGuild.SendGuildStatus(this);
            return true;
        }

        public void EditGuildMember(string Name, string RankName, byte RankIndex, byte ChangeType)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat("You are not in a guild!",ChatType.System);
                return;
            }
            switch (ChangeType)
            {
                case 0: //add member
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanRecruit))
                    {
                        ReceiveChat("You are not allowed to recruit new members!",ChatType.System);
                        return;
                    }
                    if (Name == "") return;
                    PlayerObject player = Envir.GetPlayer(Name);
                    if (player == null)
                    {
                        ReceiveChat(String.Format("{0} is not online!",Name), ChatType.System);
                        return;
                    }
                    if ((player.MyGuild != null) || (player.MyGuildRank != null) || (player.Info.GuildIndex != -1))
                    {
                        ReceiveChat(String.Format("{0} is already in a guild!", Name), ChatType.System);
                        return;
                    }
                    if (!player.EnableGuildInvite)
                    {
                        ReceiveChat(String.Format("{0} is disabling guild invites!", Name), ChatType.System);
                        return;
                    }
                    if (player.PendingGuildInvite != null)
                    {
                        ReceiveChat(string.Format("{0} already has a guild invite pending.", Name), ChatType.System);
                        return;
                    }
                    player.Enqueue(new S.GuildInvite { Name = MyGuild.Name });
                    player.PendingGuildInvite = MyGuild;
                    break;
                case 1: //delete member
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanKick))
                    {
                        ReceiveChat("You are not allowed to remove members!",ChatType.System);
                        return;
                    }
                    if (Name == "") return;
                    
                    if (!MyGuild.DeleteMember(this,Name))
                    {
                        return;
                    }
                    break;
                case 2: //promote member (and it'll auto create a new rank at bottom if the index > total ranks!)
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanChangeRank))
                    {
                        ReceiveChat("You are not allowed to change other members rank!",ChatType.System);
                        return;
                    }
                    if (Name == "") return;
                    MyGuild.ChangeRank(this, Name, RankIndex, RankName);
                    break;
                case 3: //change rank name
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanChangeRank))
                    {
                        ReceiveChat("You are not allowed to change ranks!",ChatType.System);
                        return;
                    }
                    if ((RankName == "") || (RankName.Length < 3))
                    {
                        ReceiveChat("Rank name to short!", ChatType.System);
                        return;
                    }
                    if (RankName.Contains("\\") ||RankName.Length > 20)
                    {
                        return;
                    }
                    if (!MyGuild.ChangeRankName(this, RankName, RankIndex))
                        return;
                    break;
                case 4: //new rank
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanChangeRank))
                    {
                        ReceiveChat("You are not allowed to change ranks!", ChatType.System);
                        return;
                    }
                    if (MyGuild.Ranks.Count > 254)
                    {
                        ReceiveChat("No more rank slots available.", ChatType.System);
                        return;
                    }
                    MyGuild.NewRank(this);
                    break;
                case 5: //change rank setting
                    if (!MyGuildRank.Options.HasFlag(RankOptions.CanChangeRank))
                    {
                        ReceiveChat("You are not allowed to change ranks!", ChatType.System);
                        return;
                    }
                    int temp;

                    if (!int.TryParse(RankName, out temp))
                    {
                        return;
                    }
                    MyGuild.ChangeRankOption(this, RankIndex, temp,Name);
                    break;
            }
        }
        public void EditGuildNotice(List<string> notice)
        {
            if ((MyGuild == null) || (MyGuildRank == null))
            {
                ReceiveChat("You are not in a guild!",ChatType.System);
                return;
            }
            if (!MyGuildRank.Options.HasFlag(RankOptions.CanChangeNotice))
            {
                
                ReceiveChat("You are not allowed to change the guild notice!",ChatType.System);
                return;
            }
            if (notice.Count > 200)
            {
                ReceiveChat("Guild notice can not be longer then 200 lines!", ChatType.System);
                return;
            }
            MyGuild.NewNotice(notice);
        }
    }
}

public class ItemSets
{
    public ItemSet Set;
    public List<ItemType> Type;
    private byte Amount
    {
        get
        {
            switch (Set)
            {
                case ItemSet.Mundane:
                case ItemSet.NokChi:
                case ItemSet.TaoProtect:
                    return 2;
                case ItemSet.RedOrchid:
                case ItemSet.RedFlower:
                case ItemSet.Smash:
                case ItemSet.HwanDevil:
                case ItemSet.Purity:
                case ItemSet.FiveString:
                case ItemSet.Bone:
                case ItemSet.Bug:
                    return 3;
                case ItemSet.Recall:
                    return 4;
                case ItemSet.Spirit:
                case ItemSet.WhiteGold:
                case ItemSet.WhiteGoldH:
                case ItemSet.RedJade:
                case ItemSet.RedJadeH:
                case ItemSet.Nephrite:
                case ItemSet.NephriteH:
                    return 5;
                default:
                    return 0;
            }
        }
    }
    public byte Count;
    public bool SetComplete
    {
        get
        {
            return Count == Amount;
        }
    }
}