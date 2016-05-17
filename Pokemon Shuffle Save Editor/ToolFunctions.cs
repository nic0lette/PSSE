﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static Pokemon_Shuffle_Save_Editor.Main;

namespace Pokemon_Shuffle_Save_Editor
{
    class ToolFunctions
    {
        public static void SetLevel(int ind, int lev = 1, int set_rml = -1, int set_exp = -1)
        {
            if (savedata != null)
            {
                lev = (lev < 2) ? 0 : ((lev > 15) ? 15 : lev);    //hardcoded 15 as the max level, change this if ever needed
                int level_ofs = 0x187 + ((((ind - 1) * 4) + 1) / 8);
                int level_shift = ((((ind - 1) * 4) + 1) % 8);
                ushort level = BitConverter.ToUInt16(savedata, level_ofs);
                level = (ushort)((level & (ushort)(~(0xF << level_shift))) | (lev << level_shift));
                Array.Copy(BitConverter.GetBytes(level), 0, savedata, level_ofs, 2);

                //lollipop patcher
                int rml_ofs = 0xA9DB + ((ind * 6) / 8);
                int rml_shift = ((ind * 6) % 8);
                ushort numRaiseMaxLevel = BitConverter.ToUInt16(savedata, rml_ofs);
                set_rml = (set_rml < 0) ? ((lev - 10 < 0) ? 0 : (lev - 10)) : set_rml;
                numRaiseMaxLevel = (ushort)((numRaiseMaxLevel & (ushort)(~(0x3F << rml_shift))) | (set_rml << rml_shift));
                Array.Copy(BitConverter.GetBytes(numRaiseMaxLevel), 0, savedata, rml_ofs, 2);

                //experience patcher
                int exp_ofs = 0x3241 + ((4 + ((ind - 1) * 24)) / 8);
                int exp_shift = ((4 + ((ind - 1) * 24)) % 8);
                int exp = BitConverter.ToInt32(savedata, exp_ofs);
                int entrylen = BitConverter.ToInt32(db.MonLevel, 0x4);
                byte[] data = db.MonLevel.Skip(0x50 + ((((lev < 2) ? 1 : lev) - 1) * entrylen)).Take(entrylen).ToArray(); //corrected level value, because if it's 0 then it means 1
                set_exp = (set_exp < 0) ? BitConverter.ToInt32(data, 0x4 * (db.Mons[ind].Item5 - 1)) : set_exp;
                exp = (exp & ~(0xFFFFFF << exp_shift)) | (set_exp << exp_shift);
                Array.Copy(BitConverter.GetBytes(exp), 0, savedata, exp_ofs, 4);
            }            
        }

        public static void GetLevel(int ind, out int lev, out int exp, out int rml)
        {
            //out level
            int level_ofs = 0x187 + ((((ind - 1) * 4) + 1) / 8);
            int level_shift = ((((ind - 1) * 4) + 1) % 8);
            lev = (BitConverter.ToUInt16(savedata, level_ofs) >> level_shift) & 0xF;
            lev = (lev == 0) ? 1 : lev;

            //out exp
            int exp_ofs = 0x3241 + ((4 + ((ind - 1) * 24)) / 8);
            int exp_shift = ((4 + ((ind - 1) * 24)) % 8);
            exp = (BitConverter.ToInt32(savedata, exp_ofs) >> exp_shift) & 0xFFFFFF;

            //out lollipop
            int rml_ofs = 0xA9DB + ((ind * 6) / 8);
            int rml_shift = ((ind * 6) % 8);
            rml = Math.Min((BitConverter.ToUInt16(savedata, rml_ofs) >> rml_shift) & 0x3F, 5); //hardcoded 5 as a maximum
        }

        public static void SetCaught(int ind, bool caught)
        {
            int caught_ofs = (((ind - 1) + 6) / 8);
            int caught_shift = (((ind - 1) + 6) % 8);
            foreach (int caught_array_start in new[] { 0xE6, 0x546, 0x5E6 })
                savedata[caught_array_start + caught_ofs] = (byte)(savedata[caught_array_start + caught_ofs] & (byte)(~(1 << caught_shift)) | ((caught ? 1 : 0) << caught_shift));
        }

        public static bool GetCaught(int ind)
        {
            int caught_ofs = 0x546 + (((ind - 1) + 6) / 8);
            int caught_shift = ((((ind - 1) + 6) % 8));
            return ((savedata[caught_ofs] >> caught_shift) & 1) == 1;
        }

        public static void SetStone(int ind, bool X = false, bool Y = false)
        {
            int mega_ofs = 0x406 + ((ind + 2) / 4);
            ushort mega_val = BitConverter.ToUInt16(savedata, mega_ofs);
            mega_val &= (ushort)(~(3 << ((5 + (ind << 1)) % 8)));
            ushort new_mega_insert = (ushort)(0 | (X ? 1 : 0) | (Y ? 2 : 0));
            mega_val |= (ushort)(new_mega_insert << ((5 + (ind << 1)) % 8));
            Array.Copy(BitConverter.GetBytes(mega_val), 0, savedata, mega_ofs, 2);
        }

        public static int GetStone(int ind)
        {
            int mega_ofs = 0x406 + ((ind + 2) / 4);
            int mega_shift = ((5 + (ind << 1)) % 8);
            return (savedata[mega_ofs] >> mega_shift) & 3;  //0 = 00, 1 = X0, 2 = 0Y, 3 = XY
        }

        public static void SetSpeedup(int ind, bool X = false, int suX = 0, bool Y = false, int suY = 0)
        {
            if (db.HasMega[db.Mons[ind].Item1][0] || db.HasMega[db.Mons[ind].Item1][1])
            {
                int suX_ofs = 0x2D5B + (db.MegaList.IndexOf(ind) * 7 + 3) / 8;
                int suX_shift = (db.MegaList.IndexOf(ind) * 7 + 3) % 8;
                int suY_ofs = 0x2D5B + (db.MegaList.IndexOf(ind, db.MegaList.IndexOf(ind) + 1) * 7 + 3) / 8;
                int suY_shift = (db.MegaList.IndexOf(ind, db.MegaList.IndexOf(ind) + 1) * 7 + 3) % 8 + (suY_ofs - suX_ofs) * 8; //relative to suX_ofs
                int speedUp_ValX = BitConverter.ToInt32(savedata, suX_ofs);
                int speedUp_ValY = BitConverter.ToInt32(savedata, suY_ofs);
                int newSpeedUp = db.HasMega[db.Mons[ind].Item1][1]
                    ? ((speedUp_ValX & ~(0x7F << suX_shift)) & ~(0x7F << suY_shift)) | ((X ? suX : 0) << suX_shift) | ((Y ? suY : 0) << suY_shift) //Erases both X & Y bits at the same time before updating them to make sure Y doesn't overwrite X bits
                    : (speedUp_ValX & ~(0x7F << suX_shift)) | ((X ? suX : 0) << suX_shift);
                Array.Copy(BitConverter.GetBytes(newSpeedUp), 0, savedata, suX_ofs, 4);
            }
        }

        public static int GetSpeedupX(int ind)
        {
            if (db.HasMega[db.Mons[ind].Item1][0])
            {
                int suX_ofs = 0x2D5B + (db.MegaList.IndexOf(ind) * 7 + 3) / 8;
                int suX_shift = (db.MegaList.IndexOf(ind) * 7 + 3) % 8;
                return (BitConverter.ToInt32(savedata, suX_ofs) >> suX_shift) & 0x7F;
            }
            else return 0;
        }

        public static int GetSpeedupY(int ind)
        {
            if (db.HasMega[db.Mons[ind].Item1][1])
            {
                int suY_ofs = 0x2D5B + ((db.MegaList.IndexOf(ind, db.MegaList.IndexOf(ind) + 1) * 7) + 3) / 8;
                int suY_shift = (db.MegaList.IndexOf(ind, db.MegaList.IndexOf(ind) + 1) * 7 + 3) % 8;
                return (BitConverter.ToInt32(savedata, suY_ofs) >> suY_shift) & 0x7F;
            }
            else return 0;
        }

        public static void SetStage(int ind, int type, bool completed = false)
        {
            int stage_ofs, stage_shift = (ind * 3) % 8;
            int entrylen = BitConverter.ToInt32(db.StagesMain, 0x4);
            switch (type)
            {
                case 0:
                    stage_ofs = 0x688 + ((ind * 3) / 8); //Main
                    break;
                case 1:
                    stage_ofs = 0x84A + ((ind * 3) / 8); //Expert
                    break;
                case 2:
                    stage_ofs = 0x8BA + (4 + ind * 3) / 8; //Event
                    stage_shift = (4 + ind * 3) % 8;
                    break;
                default:
                    return;
            }
            ushort stage = BitConverter.ToUInt16(savedata, stage_ofs);
            stage = (ushort)((stage & (ushort)(~(0x7 << stage_shift))) | ((completed ? 5 : 0) << stage_shift));
            Array.Copy(BitConverter.GetBytes(stage), 0, savedata, stage_ofs, 2);
        }

        public static bool GetStage(int ind, int type)
        {
            int stage_ofs, stage_shift = ind * 3 % 8;
            switch (type)
            {
                case 0:
                    stage_ofs = 0x688 + ind * 3 / 8; //Main
                    break;
                case 1:
                    stage_ofs = 0x84A + ind * 3 / 8; //Expert
                    break;
                case 2:
                    stage_ofs = 0x8BA + (4 + ind * 3) / 8; //Event
                    stage_shift = (4 + ind * 3) % 8;
                    break;
                default:
                    return false;
            }
            return ((BitConverter.ToInt16(savedata, stage_ofs) >> stage_shift) & 7) == 5;
        }

        public static void SetRank(int ind, int type, int newRank = 0)
        {
            int rank_ofs;
            switch (type)
            {
                case 0:
                    rank_ofs = 0x987 + (7 + ind * 2) / 8; //Main
                    break;
                case 1:
                    rank_ofs = 0xAB3 + (7 + ind * 2) / 8; //Expert
                    break;
                case 2:
                    rank_ofs = 0xAFE + (7 + ind * 2) / 8; //Event
                    break;
                default:
                    return;
            }
            int rank_shift = (7 + ind * 2) % 8;
            ushort rank = BitConverter.ToUInt16(savedata, rank_ofs);
            rank = (ushort)((rank & (ushort)(~(0x3 << rank_shift))) | (newRank << rank_shift));
            Array.Copy(BitConverter.GetBytes(rank), 0, savedata, rank_ofs, 2);
        }

        public static int GetRank(int ind, int type)
        {
            int rank_ofs;
            switch (type)
            {
                case 0:
                    rank_ofs = 0x987 + (7 + ind * 2) / 8; //Main
                    break;
                case 1:
                    rank_ofs = 0xAB3 + (7 + ind * 2) / 8; //Expert
                    break;
                case 2:
                    rank_ofs = 0xAFE + (7 + ind * 2) / 8; //Event
                    break;
                default:
                    return 0;
            }
            return ((BitConverter.ToInt16(savedata, rank_ofs) >> (7 + ind * 2) % 8) & 0x3);
        }

        public static void SetScore(int ind, int type, ulong newScore = 0)
        {
            int score_ofs;
            switch (type)
            {
                case 0:
                    score_ofs = 0x4141 + 3 * ind; //Main
                    break;
                case 1:
                    score_ofs = 0x4F51 + 3 * ind; //Expert
                    break;
                case 2:
                    score_ofs = 0x52D5 + 3 * ind; //Event
                    break;
                default:
                    return;
            }
            Array.Copy(BitConverter.GetBytes((BitConverter.ToUInt64(savedata, score_ofs) & 0xFFFFFFFFF000000FL) | (newScore << 4)), 0, savedata, score_ofs, 8);
        }

        public static ulong GetScore(int ind, int type)
        {
            int score_ofs;
            switch (type)
            {
                case 0:
                    score_ofs = 0x4141 + 3 * ind; //Main
                    break;
                case 1:
                    score_ofs = 0x4F51 + 3 * ind; //Expert
                    break;
                case 2:
                    score_ofs = 0x52D5 + 3 * ind; //Event
                    break;
                default:
                    return 0;
            }
            return (BitConverter.ToUInt64(savedata, score_ofs) >> 4) & 0x00FFFFFF;
        }

        public static void PatchScore(int ind, int type)
        {
            int entrylen = BitConverter.ToInt32(db.StagesMain, 0x4);
            byte[] data = db.StagesMain.Skip(0x50 + (ind + 1) * entrylen).Take(entrylen).ToArray();
            SetScore(ind, type, Math.Max(GetScore(ind, type), (BitConverter.ToUInt64(data, 0x4) & 0xFFFFFFFF) + (ulong)Math.Min(7000, ((GetRank(ind, type) > 0) ? ((BitConverter.ToInt16(data, 0x30 + GetRank(ind, type) - 1) >> 4) & 0xFF) : 0) * 500))); //score = Max(current_highscore, hitpoints + minimum_bonus_points (a.k.a min moves left times 500, capped at 7000))
        }

        public static void SetResources(int hearts = 0, uint coins = 0, uint jewels = 0, int[] items = null, int[] enhancements = null)
        {
            if (items == null)
                items = new int[7];
            if (enhancements == null)
                enhancements = new int[9];
            Array.Copy(BitConverter.GetBytes((BitConverter.ToUInt32(savedata, 0x68) & 0xF0000007) | (coins << 3) | (jewels << 20)), 0, savedata, 0x68, 4);
            Array.Copy(BitConverter.GetBytes((BitConverter.ToUInt16(savedata, 0x2D4A) & 0xC07F) | (hearts << 7)), 0, savedata, 0x2D4A, 2);
            for (int i = 0; i < 7; i++) //Items (battle)
            {
                ushort val = BitConverter.ToUInt16(savedata, 0xd0 + i);
                val &= 0x7F;
                val |= (ushort)(items[i] << 7);
                Array.Copy(BitConverter.GetBytes(val), 0, savedata, 0xd0 + i, 2);
            }
            for (int i = 0; i < 9; i++) //Enhancements (pokemon)
                savedata[0x2D4C + i] = (byte)((((enhancements[i]) << 1) & 0xFE) | (savedata[0x2D4C + i] & 1));
        }

        public static rsItem GetRessources()
        {
            int hearts = (BitConverter.ToUInt16(savedata, 0x2D4A) >> 7) & 0x7F;
            int coins = (BitConverter.ToInt32(savedata, 0x68) >> 3) & 0x1FFFF;
            int jewels = (BitConverter.ToInt32(savedata, 0x68) >> 20) & 0xFF;
            int[] items = new int[7], enhancements = new int[9];
            for (int i = 0; i < items.Length; i++) 
                items[i] = (BitConverter.ToUInt16(savedata, 0xD0 + i) >> 7) & 0x7F;
            for (int i = 0; i < enhancements.Length; i++)
                enhancements[i] = (savedata[0x2D4C + i] >> 1) & 0x7F;
            return new rsItem { Hearts = hearts, Coins = coins, Jewels = jewels, Items = items, Enhancements = enhancements};
        }

        public static void SetExcalationStep(int step = 0)
        {
            if (step < 0)
                step = 0;
            if (step > 999)
                step = 999;
            int data = BitConverter.ToUInt16(savedata, 0x2D59);
            data = (data & (~(0x3FF << 2))) | (step << 2);
            Array.Copy(BitConverter.GetBytes(data), 0, savedata, 0x2D59, 2); //Will only update 1 escalation battle. Update offsets if there ever are more than 1 at once
        }

        public static Bitmap GetCaughtImage(int ind, bool caught = false)
        {
            Bitmap bmp = GetMonImage(ind);
            GetBlackImage(bmp, caught);
            return bmp;
        }

        public static Bitmap GetMonImage(int ind)
        {
            string imgname = string.Empty;
            int mon_num = db.Mons[ind].Item1, form = db.Mons[ind].Item2;
            bool mega = db.Mons[ind].Item3;
            if (mega)
            {
                form -= db.HasMega[mon_num][1] ? 1 : 2; //Differenciate Rayquaza/Gyarados from Charizard/Mewtwo, otherwise either stage 300 is Shiny M-Ray or stage 150 is M-mewtwo X
                imgname += "mega_";
            }
            imgname += "pokemon_" + mon_num.ToString("000");
            if (form > 0 && mon_num > 0)
                imgname += "_" + form.ToString("00");
            if (mega)
                imgname += "_lo";
            return new Bitmap((Image)Properties.Resources.ResourceManager.GetObject(imgname));
        }

        public static Bitmap GetStageImage(int ind, int type, bool completed = true, bool overridePB = false)
        {
            int mon_num = db.Mons[ind].Item1, form = db.Mons[ind].Item2;
            bool mega = db.Mons[ind].Item3;
            Bitmap bmp = new Bitmap(64, 80);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                if (mega && !(type == 2))
                    g.DrawImage(Properties.Resources.PlateMega, new Point(0, 16));
                else
                    g.DrawImage(Properties.Resources.Plate, new Point(0, 16));
                g.DrawImage(ResizeImage(GetMonImage(ind), 48, 48), new Point(8, 7));
                GetBlackImage(bmp, (type == 0) ? completed : true);
            }

            return bmp;
        }

        public static Bitmap GetBlackImage(Bitmap bmp, bool caught = true)
        {
            if (!caught)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        bmp.SetPixel(x, y, Color.FromArgb(c.A, 0, 0, 0));
                    }
                }
            }
            return bmp;
        }

        public static void GetRankImage(Label label, int rank = default(int), bool completed = false) //Plain text until a way is found to extract Rank sprites from game's folders.
        {                                                                                       //These are in several files in "Layout Archives", #127 for example,
            if (completed)                                                                      //but I can't get a proper png without it being cropped or its colours distorted.
            {
                switch (rank)
                {
                    case 0:
                        label.Text = "C";
                        //label.ForeColor = Color.Orchid;
                        break;
                    case 1:
                        label.Text = "B";
                        //label.ForeColor = Color.ForestGreen;
                        break;
                    case 2:
                        label.Text = "A";
                        //label.ForeColor = Color.RoyalBlue;
                        break;
                    case 3:
                        label.Text = "S";
                        //label.ForeColor = Color.Goldenrod;
                        break;
                    default:
                        label.Text = "-";
                        //label.ForeColor = Color.Black;
                        break;
                }
            }
            else
            {
                label.Text = "-";
                //label.ForeColor = Color.Black;
            }
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            if (image.HorizontalResolution > 0 && image.VerticalResolution > 0)
                destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }

    public class cbItem
    {
        public string Text { get; set; }
        public int Value { get; set; }
    }

    public class rsItem
    {
        public int Hearts { get; set; }
        public int Coins { get; set; }
        public int Jewels { get; set; }
        public int[] Items { get; set; }
        public int[] Enhancements { get; set; }
    }
}