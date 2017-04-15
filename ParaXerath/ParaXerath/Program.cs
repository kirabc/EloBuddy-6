using System;
using System.Linq;
using System.Collections.Generic;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

namespace ParaXerath
{
    class Program
    {
        static readonly Dictionary<int, float> Timers = new Dictionary<int, float>();

        static Menu menu;

        static int qMana, wMana, eMana;

        static float qCasted, wCasted, eCasted;

        static Spell.Active Q = new Spell.Active(SpellSlot.Q);
        static Spell.Active W = new Spell.Active(SpellSlot.W);
        static Spell.Skillshot E = new Spell.Skillshot(SpellSlot.E, 1050, EloBuddy.SDK.Enumerations.SkillShotType.Linear, 250, 2300, 60, DamageType.Magical)
        {
            AllowedCollisionCount = 0,
            MinimumHitChance = EloBuddy.SDK.Enumerations.HitChance.High
        };
        static Spell.Active R = new Spell.Active(SpellSlot.R);
        static bool IsCastingR
        {
            get { return Player.Instance.HasBuff("XerathR"); }
        }
        static bool IsChargingQ
        {
            get { return Player.Instance.HasBuff("XerathArcanopulseChargeUp"); }
        }
        public static void Main(string[] args)
        {
            Loading.OnLoadingComplete += Loading_OnLoadingComplete;
        }

        static void Loading_OnLoadingComplete(EventArgs args)
        {
            if (Player.Instance.ChampionName != "Xerath")
            {
                return;
            }
            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                Timers.Add(enemy.NetworkId, 0);
            }
            menu = MainMenu.AddMenu("ParaXerath", "paraxerath");
            menu.Add("combo", new KeyBind("Combo", false, KeyBind.BindTypes.HoldActive, ' '));
            Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
            Game.OnUpdate += Game_OnUpdate;
        }

        static void Obj_AI_Base_OnNewPath(Obj_AI_Base sender, GameObjectNewPathEventArgs args)
        {
            int id = sender.NetworkId;
            if (!Timers.ContainsKey(id))
                return;
            Timers[id] = Game.Time;
        }

        static void Game_OnUpdate(EventArgs args)
        {
            qMana = 70 + Q.Level * 10;
            wMana = 60 + Q.Level * 10;
            eMana = 55 + Q.Level * 5;
            if (IsCastingR)
            {
                Orbwalker.DisableAttacking = true;
                Orbwalker.DisableMovement = true;
                CastR();
            }
            else
            {
                Orbwalker.DisableMovement = false;
                Orbwalker.DisableAttacking = false;
                if (menu["combo"].Cast<KeyBind>().CurrentValue)
                {
                    CastE();
                    CastW();
                    if (!IsChargingQ)
                    {
                        var enemy = TargetSelector.GetTarget(1400, DamageType.Magical);
                        if (Game.Time > wCasted + 0.3f && Game.Time > eCasted + 0.3f && Q.IsReady() && Player.Instance.Mana > qMana && enemy.IsValidTarget() && Game.Time > lastQ + 1.5f)
                        {
                            Player.Instance.Spellbook.CastSpell(SpellSlot.Q, Game.CursorPos, true);
                            lastQ = Game.Time;
                        }
                    }
                    else
                    {
                        CastQ();
                    }
                }
            }
        }

        static float lastQ;

        static bool CastQ()
        {
            int range = 750 + (int)((Game.Time - lastQ) * 430);
            if (range > 1600)
            {
                range = 1600;
            }
            var enemy = TargetSelector.GetTarget(1600, DamageType.Magical);
            if (!enemy.IsValidTarget())
                return false;
            int enemyid = enemy.NetworkId;
            Vector2 enemypos = enemy.Position.To2D();
            float enemyspeed = enemy.MoveSpeed;
            Vector3[] path = enemy.Path;
            int lenght = path.Length;
            Vector3 predpos = Vector3.Zero;
            if (lenght > 1)
            {
                float s_in_time = enemyspeed * (Game.Time - Timers[enemyid] + (Game.Ping * 0.001f));
                float d = 0f;
                for (int i = 0; i < lenght - 1; i++)
                {
                    Vector2 vi = path[i].To2D();
                    Vector2 vi1 = path[i + 1].To2D();
                    d += vi.Distance(vi1);
                    if (d >= s_in_time)
                    {
                        float dd = enemypos.Distance(vi1);
                        float ss = enemyspeed * 0.5f;
                        if (dd >= ss)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * ss)).To3D();
                            break;
                        }
                        if (i + 1 == lenght - 1)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * enemypos.Distance(vi1))).To3D();
                            break;
                        }
                        for (int j = i + 1; j < lenght - 1; j++)
                        {
                            Vector2 vj = path[j].To2D();
                            Vector2 vj1 = path[j + 1].To2D();
                            ss -= dd;
                            dd = vj.Distance(vj1);
                            if (dd >= ss)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * ss)).To3D();
                                break;
                            }
                            if (j + 1 == lenght - 1)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * dd)).To3D();
                                break;
                            }
                        }
                        break;
                    }
                    if (i + 1 == lenght - 1)
                    {
                        predpos = (vi + ((vi1 - vi).Normalized() * vi.Distance(vi1))).To3D();
                        break;
                    }
                }
            }
            else
            {
                predpos = enemy.Position;
            }
            if (predpos.IsZero || predpos.Distance(Player.Instance.Position) > range || (int)path.LastOrDefault().X != (int)enemy.Path.LastOrDefault().X)
                return false;
            Player.Instance.Spellbook.UpdateChargeableSpell(SpellSlot.Q, predpos, true);
            Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            qCasted = Game.Time;
            return true;
        }

        static bool CastW()
        {
            if (!W.IsReady() || Game.Time < qCasted + 0.3f || Player.Instance.Mana < wMana || Game.Time < eCasted + 0.3f)
                return false;
            var enemy = TargetSelector.GetTarget(1100, DamageType.Magical);
            if (!enemy.IsValidTarget())
                return false;
            int enemyid = enemy.NetworkId;
            Vector2 enemypos = enemy.Position.To2D();
            float enemyspeed = enemy.MoveSpeed;
            Vector3[] path = enemy.Path;
            int lenght = path.Length;
            Vector3 predpos = Vector3.Zero;
            if (lenght > 1)
            {
                float s_in_time = enemyspeed * (Game.Time - Timers[enemyid] + (Game.Ping * 0.001f));
                float d = 0f;
                for (int i = 0; i < lenght - 1; i++)
                {
                    Vector2 vi = path[i].To2D();
                    Vector2 vi1 = path[i + 1].To2D();
                    d += vi.Distance(vi1);
                    if (d >= s_in_time)
                    {
                        float dd = enemypos.Distance(vi1);
                        float ss = enemyspeed * 0.5f;
                        if (dd >= ss)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * ss)).To3D();
                            break;
                        }
                        if (i + 1 == lenght - 1)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * enemypos.Distance(vi1))).To3D();
                            break;
                        }
                        for (int j = i + 1; j < lenght - 1; j++)
                        {
                            Vector2 vj = path[j].To2D();
                            Vector2 vj1 = path[j + 1].To2D();
                            ss -= dd;
                            dd = vj.Distance(vj1);
                            if (dd >= ss)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * ss)).To3D();
                                break;
                            }
                            if (j + 1 == lenght - 1)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * dd)).To3D();
                                break;
                            }
                        }
                        break;
                    }
                    if (i + 1 == lenght - 1)
                    {
                        predpos = (vi + ((vi1 - vi).Normalized() * vi.Distance(vi1))).To3D();
                        break;
                    }
                }
            }
            else
            {
                predpos = enemy.Position;
            }
            if (predpos.IsZero || predpos.Distance(Player.Instance.Position) > 1100 || (int)path.LastOrDefault().X != (int)enemy.Path.LastOrDefault().X)
                return false;
            Player.Instance.Spellbook.CastSpell(SpellSlot.W, predpos);
            Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            wCasted = Game.Time;
            return true;
        }

        static bool CastE()
        {
            if (!E.IsReady() || Game.Time < qCasted + 0.3f || Player.Instance.Mana < eMana || Game.Time < wCasted + 0.3f)
                return false;
            var enemy = TargetSelector.GetTarget(1050, DamageType.Magical);
            if (!enemy.IsValidTarget())
                return false;
            if (!E.Cast(enemy))
                return false;
            eCasted = Game.Time;
            Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            return true;
        }

        static float lastR;

        static bool CastR()
        {
            if (!R.IsReady() || Game.Time < lastR + 0.8f)
                return false;
            int range = 2200 + (R.Level * 1320);
            var enemy = TargetSelector.GetTarget(range, DamageType.Magical);
            if (!enemy.IsValidTarget())
                return false;
            int enemyid = enemy.NetworkId;
            Vector2 enemypos = enemy.Position.To2D();
            float enemyspeed = enemy.MoveSpeed;
            Vector3[] path = enemy.Path;
            int lenght = path.Length;
            Vector3 predpos = Vector3.Zero;
            if (lenght > 1)
            {
                float s_in_time = enemyspeed * (Game.Time - Timers[enemyid] + (Game.Ping * 0.001f));
                float d = 0f;
                for (int i = 0; i < lenght - 1; i++)
                {
                    Vector2 vi = path[i].To2D();
                    Vector2 vi1 = path[i + 1].To2D();
                    d += vi.Distance(vi1);
                    if (d >= s_in_time)
                    {
                        float dd = enemypos.Distance(vi1);
                        float ss = enemyspeed * 0.5f;
                        if (dd >= ss)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * ss)).To3D();
                            break;
                        }
                        if (i + 1 == lenght - 1)
                        {
                            predpos = (enemypos + ((vi1 - enemypos).Normalized() * enemypos.Distance(vi1))).To3D();
                            break;
                        }
                        for (int j = i + 1; j < lenght - 1; j++)
                        {
                            Vector2 vj = path[j].To2D();
                            Vector2 vj1 = path[j + 1].To2D();
                            ss -= dd;
                            dd = vj.Distance(vj1);
                            if (dd >= ss)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * ss)).To3D();
                                break;
                            }
                            if (j + 1 == lenght - 1)
                            {
                                predpos = (vj + ((vj1 - vj).Normalized() * dd)).To3D();
                                break;
                            }
                        }
                        break;
                    }
                    if (i + 1 == lenght - 1)
                    {
                        predpos = (vi + ((vi1 - vi).Normalized() * vi.Distance(vi1))).To3D();
                        break;
                    }
                }
            }
            else
            {
                predpos = enemy.Position;
            }
            if (predpos.IsZero || predpos.Distance(Player.Instance.Position) > range || (int)path.LastOrDefault().X != (int)enemy.Path.LastOrDefault().X)
                return false;
            Player.Instance.Spellbook.CastSpell(SpellSlot.R, predpos);
            lastR = Game.Time;
            return true;
        }

        static float Quadratic_Equation(Vector2 source, Vector2 startP, Vector2 endP, float unitspeed, int spellspeed, float delay)
        {
            float sx = source.X;
            float sy = source.Y;
            float ux = startP.X;
            float uy = startP.Y;
            float dx = endP.X - ux;
            float dy = endP.Y - uy;
            float magnitude = (float)Math.Sqrt(dx * dx + dy * dy);
            dx = (dx / magnitude) * unitspeed;
            dy = (dy / magnitude) * unitspeed;
            float a = (dx * dx) + (dy * dy) - (spellspeed * spellspeed);
            float b = 2 * ((ux * dx) + (uy * dy) - (sx * dx) - (sy * dy));
            float c = (ux * ux) + (uy * uy) + (sx * sx) + (sy * sy) - (2 * sx * ux) - (2 * sy * uy);
            float d = (b * b) - (4 * a * c);
            if (d > 0)
            {
                double t1 = (-b + Math.Sqrt(d)) / (2 * a);
                double t2 = (-b - Math.Sqrt(d)) / (2 * a);
                return (float)Math.Max(t1, t2) + delay;
            }
            if (d >= 0 && d < 0.00001)
            {
                return (-b / (2 * a)) + delay;
            }
            return 0.0001f;
        }

        static float Dist_Point_Line_Segment(Vector2 a, Vector2 b, Vector2 c)
        {
            float ax = a.X;
            float ay = a.Y;
            float bx = b.X;
            float by = b.Y;
            float cx = c.X;
            float cy = c.Y;
            float dx = bx - ax;
            float dy = by - ay;
            float t = ((cx - ax) * dx + (cy - ay) * dy) / (dx * dx + dy * dy);
            if (t < 0)
            {
                dx = cx - ax;
                dy = cy - ay;
            }
            else if (t > 1)
            {
                dx = cx - bx;
                dy = cy - by;
            }
            else
            {
                dx = cx - (ax + (t * dx));
                dy = cy - (ay + (t * dy));
            }
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
