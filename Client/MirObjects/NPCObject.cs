﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using S = ServerPackets;

namespace Client.MirObjects
{
    class NPCObject :MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Merchant; }
        }
        public override bool Blocking
        {
            get { return true; }
        }

        public FrameSet Frames;
        public Frame Frame;
        public int BaseIndex, FrameIndex, FrameInterval, 
            EffectFrameIndex, EffectFrameInterval;

        public MirLabel TempLabel;
        public byte Image;

        private bool _CanChangeDir = true;

        public bool CanChangeDir
        {
            get { return _CanChangeDir; }
            set
            {
                _CanChangeDir = value;
                if (value == false) Direction = 0;
            }
        }

        public NPCObject(uint objectID) : base(objectID)
        {
        }

        public void Load(S.ObjectNPC info)
        {
            Name = info.Name;
            NameColour = info.NameColour;
            CurrentLocation = info.Location;
            Direction = info.Direction;
            Movement = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);

            Image = info.Image;
            if (info.Image < Libraries.NPCs.Length)
                BodyLibrary = Libraries.NPCs[info.Image];

            switch (info.Image)
            {
                #region 4 frames + direction + harvest(10 frames)
                default:
                    Frames = FrameSet.NPCs[0];
                    break;
                #endregion

                #region 4 frames + direction + harvest(20 frames)
                case 23:
                    Frames = FrameSet.NPCs[1];
                    break;
                #endregion

                #region 4 frames
                case 62:
                case 63:
                case 64:
                case 65:
                case 66:
                case 159:
                    Frames = FrameSet.NPCs[2];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 4 frames + direction
                case 52:
                case 61:
                case 68:
                case 69:
                case 70:
                case 75:
                case 83:
                case 90:
                case 91:
                case 92:
                case 93:
                case 94:
                case 95:
                case 100:
                case 101:
                case 111:
                case 112:
                case 115:
                case 116:
                case 117:
                case 118:
                case 141:
                case 142: 
                case 151:
                case 152:
                case 163:
                    Frames = FrameSet.NPCs[2];
                    break;
                #endregion

                #region 12 frames + animation(10 frames) (large tele) 
                case 33:
                case 34:
                    Frames = FrameSet.NPCs[3];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 2 frames + animation(9 frames) (small tele)
                case 79:
                case 80:
                    Frames = FrameSet.NPCs[4];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 1 frame + animation(6 frames)
                case 85:
                case 86:
                    Frames = FrameSet.NPCs[5];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 1 frame
                case 35:
                case 36:
                case 37:
                case 38:
                case 39:
                case 40:
                case 44:
                case 45:
                case 46:
                
                case 50:
                case 51:
                case 54:
                case 56:
                case 67:
                case 71:
                case 72:
                case 73:
                case 76:
                case 77:
                case 96:
                case 97:
                case 98:
                case 99:
                case 102:
                case 103:
                case 104:
                case 105:
                case 106:
                case 107:
                case 108:
                case 109:
                case 113:
                case 114:
                case 124:
                case 125:
                case 126:
                case 127:
                case 128:
                case 129:
                case 130:
                case 131:
                case 132:
                case 133:
                case 134:
                case 135:
                case 136:
                case 137:
                case 138:
                case 139:
                case 140: 
                case 144:
                case 145:
                case 146:
                case 147:
                case 148:
                case 149:
                case 150:
                case 156:
                case 157:
                    Frames = FrameSet.NPCs[6];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 10 frames
                case 53: 
                case 153:
                case 158:
                case 161:
                case 162:
                case 123:
                    Frames = FrameSet.NPCs[7];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 12 frames
                case 55:
                    Frames = FrameSet.NPCs[8];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 8 frames
                case 87:
                case 88:
                case 89:
                case 154:
                    Frames = FrameSet.NPCs[9];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 6 frames + direction
                case 110:
                case 119:
                case 122:
                case 143:
                    Frames = FrameSet.NPCs[10];
                    break;
                #endregion

                #region 1 frame + animation(8 frames)
                case 155:
                    Frames = FrameSet.NPCs[11];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 11 frames
                case 164:
                case 165:
                case 166:
                case 167:
                case 168:
                case 169:
                case 170:
                case 171:
                case 172:
                case 173:
                    Frames = FrameSet.NPCs[12];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 20 frames + animation(20 frames)
                case 59:
                    Frames = FrameSet.NPCs[13];
                    CanChangeDir = false;
                    break;
                #endregion

                #region 4 frames + direction + animation(4 frames)
                case 81:
                case 82:
                    Frames = FrameSet.NPCs[14];
                    break;
                #endregion

                #region 4 frames + harvest(6 frames)
                case 60:
                    Frames = FrameSet.NPCs[15];
                    break;
                #endregion

                #region 6 frames + animation(12 frames)
                case 48:
                    Frames = FrameSet.NPCs[16];
                    CanChangeDir = false;
                    break;
                #endregion
            }

            Light = 10;

            SetAction();
        }


        public override void Process()
        {
            bool update = CMain.Time >= NextMotion || GameScene.CanMove;

            ProcessFrames();

            if (Frame == null)
            {
                DrawFrame = 0;
                DrawWingFrame = 0;
            }
            else
            {
                DrawFrame = Frame.Start + (Frame.OffSet * (byte)Direction) + FrameIndex;
                DrawWingFrame = Frame.EffectStart + (Frame.EffectOffSet * (byte)Direction) + EffectFrameIndex;
            }

            DrawY = CurrentLocation.Y;

            DrawLocation = new Point((Movement.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (Movement.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(User.OffSetMove);
            if (BodyLibrary != null)
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));

            if (BodyLibrary != null && update)
            {
                FinalDrawLocation = DrawLocation.Add(BodyLibrary.GetOffSet(DrawFrame));
                DisplayRectangle = new Rectangle(DrawLocation, BodyLibrary.GetTrueSize(DrawFrame));
            }

            for (int i = 0; i < Effects.Count; i++)
                Effects[i].Process();

            Color colour = DrawColour;

            switch (Poison)
            {
                case PoisonType.None:
                    DrawColour = Color.White;
                    break;
                case PoisonType.Green:
                    DrawColour = Color.Green;
                    break;
                case PoisonType.Red:
                    DrawColour = Color.Red;
                    break;
                case PoisonType.Slow:
                    DrawColour = Color.Purple;
                    break;
                case PoisonType.Stun:
                    DrawColour = Color.Yellow;
                    break;
                case PoisonType.Frozen:
                    DrawColour = Color.Blue;
                    break;
                case PoisonType.Paralysis:
                    DrawColour = Color.Gray;
                    break;
            }


            if (colour != DrawColour) GameScene.Scene.MapControl.TextureValid = false;
        }
        public virtual void ProcessFrames()
        {
            if (Frame == null) return;


            switch (CurrentAction)
            {
                case MirAction.Standing:
                case MirAction.Harvest:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    if(EffectFrameInterval > 0)
                    if (CMain.Time >= NextMotion2)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame2();

                        if (UpdateFrame2() >= Frame.EffectCount)
                            EffectFrameIndex = Frame.EffectCount - 1;
                        else
                            NextMotion2 += EffectFrameInterval;
                    }
                    break;

            }

        }
        public int UpdateFrame()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--FrameIndex);

            return ++FrameIndex;
        }

        public int UpdateFrame2()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--EffectFrameIndex);

            return ++EffectFrameIndex;
        }

        public virtual void SetAction()
        {
            if (ActionFeed.Count == 0)
            {

                if (CMain.Random.Next(2) == 0 && Frames.Frames.Count > 1)
                    CurrentAction = MirAction.Harvest;  
                else
                    CurrentAction = MirAction.Standing;

                Frames.Frames.TryGetValue(CurrentAction, out Frame);
                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (MapLocation != CurrentLocation)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = CurrentLocation;
                    GameScene.Scene.MapControl.AddObject(this);
                }

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                EffectFrameInterval = Frame.EffectInterval;
            }
            else
            {
                QueuedAction action = ActionFeed[0];
                ActionFeed.RemoveAt(0);

                CurrentAction = action.Action;
                CurrentLocation = action.Location;

                if(CanChangeDir)
                    Direction = action.Direction;
                
                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                EffectFrameInterval = Frame.EffectInterval;
            }

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            GameScene.Scene.MapControl.TextureValid = false;

        }
        public override void Draw()
        {
            if (BodyLibrary == null) return;

            BodyLibrary.Draw(DrawFrame, DrawLocation, DrawColour, true);
        }

        public override bool MouseOver(Point p)
        {
            return MapControl.MapLocation == CurrentLocation || BodyLibrary != null && BodyLibrary.VisiblePixel(DrawFrame, p.Subtract(FinalDrawLocation), false);
        }

        public override void DrawEffects()
        {

            if (BodyLibrary == null) return;

            if (DrawWingFrame > 0)
                BodyLibrary.DrawBlend(DrawWingFrame, DrawLocation, Color.White, true);
        }

        public override void DrawName()
        {
            if (!Name.Contains("_"))
            {
                base.DrawName();
                return;
            }

            string[] splitName = Name.Split('_');

            for (int s = 0; s < splitName.Count(); s++)
            {
                CreateNPCLabel(splitName[s], s);

                TempLabel.Text = splitName[s];
                TempLabel.Location = new Point(DisplayRectangle.X + (48 - TempLabel.Size.Width) / 2, DisplayRectangle.Y - (32 - TempLabel.Size.Height / 2) + (Dead ? 35 : 8) - (((splitName.Count() - 1) * 10) / 2) + (s * 12));
                TempLabel.Draw();
            }
        }

        public void CreateNPCLabel(string word, int wordOrder)
        {
            TempLabel = null;

            for (int i = 0; i < LabelList.Count; i++)
            {
                if (LabelList[i].Text != word || LabelList[i].ForeColour != (wordOrder == 0 ? NameColour : Color.White)) continue;
                TempLabel = LabelList[i];
                break;
            }

            if (TempLabel != null && !TempLabel.IsDisposed) return;

            TempLabel = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = wordOrder == 0 ? NameColour : Color.White,
                OutLine = true,
                OutLineColour = Color.Black,
                Text = word,
            };

            TempLabel.Disposing += (o, e) => LabelList.Remove(TempLabel);
            LabelList.Add(TempLabel);
        }
    }
}
