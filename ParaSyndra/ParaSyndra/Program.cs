using System;
using System.Linq;
using System.Collections.Generic;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

namespace ParaSyndra
{
	public class Timer
	{
		public Timer(float pathtime, float aaendtime, Vector3[] path)
		{
			PathTime = pathtime;
			AAEndTime = aaendtime;
			Path = path;
		}
		public float PathTime{ get; set; }
		public float AAEndTime{ get; set; }
		public Vector3[] Path{ get; set; }
	}
	
	class Program
	{
		static readonly Dictionary<int, Timer> Timers = new Dictionary<int, Timer>();
		
		static readonly Dictionary<int, GameObject> QObjects = new Dictionary<int, GameObject>();
		
		static float lastq, laste, wminion;
		
		static Vector3 LastQCastPos;
		
		static Menu Config, Auto, AASettings;
		
		static readonly Spell.Targeted R = new Spell.Targeted(SpellSlot.R, 675, DamageType.Magical);
		
		static readonly Spell.Targeted R5 = new Spell.Targeted(SpellSlot.R, 750, DamageType.Magical);
		
		static int automana, disaa, minaa;
		
		static bool autoei, autoeo, autoq, readyaa;
		
		public static void Main(string[] args)
		{
			Loading.OnLoadingComplete += Loading_OnLoadingComplete;
		}
		static void Loading_OnLoadingComplete(EventArgs args)
		{
			if (Player.Instance.ChampionName != "Syndra")
			{
				return;
			}
			Config = MainMenu.AddMenu("ParaSyndra", "parasyndra");
			Config.AddGroupLabel("ParaSyndra [1.0.1.4]");
			Auto = Config.AddSubMenu("Automatic");
			Auto.AddGroupLabel("Ulti ON:");
			foreach (var enemy in EntityManager.Heroes.Enemies)
			{
				Timers.Add(enemy.NetworkId, new Timer(0f, 0f, enemy.Path));
				Auto.Add(enemy.ChampionName, new CheckBox(enemy.ChampionName));
			}
			Auto.AddSeparator();
			Auto.AddGroupLabel("AUTO Harras:");
			Auto.Add("autoq", new CheckBox("Auto Q"));
			Auto.Add("automana", new Slider("Auto Q - Minimum Mana Percent", 50));
			Auto.Add("autoei", new CheckBox("Auto QE - Enemy In Q Range", false));
			Auto.Add("autoeo", new CheckBox("Auto QE - Enemy Out Of Q Range", false));
			AASettings = Config.AddSubMenu("Attack");
			AASettings.Add("readyaa", new CheckBox("Disable if spells ready"));
			AASettings.Add("disaa", new Slider("Disable at level", 11, 1, 18));
			AASettings.Add("minaa", new Slider("Enable if killable with x aa", 3, 1, 6));
			Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
			Obj_AI_Base.OnBasicAttack += Obj_AI_Base_OnBasicAttack;
			GameObject.OnCreate += GameObject_OnCreate;
			GameObject.OnDelete += GameObject_OnDelete;
			Game.OnUpdate += Game_OnUpdate;
			
			Auto["autoq"].Cast<CheckBox>().OnValueChange += AutoQ;
			Auto["autoei"].Cast<CheckBox>().OnValueChange += AutoEI;
			Auto["autoeo"].Cast<CheckBox>().OnValueChange += AutoEO;
			Auto["automana"].Cast<Slider>().OnValueChange += AutoMana;
			AASettings["disaa"].Cast<Slider>().OnValueChange += DisAA;
			AASettings["readyaa"].Cast<CheckBox>().OnValueChange += ReadyAA;
			AASettings["minaa"].Cast<Slider>().OnValueChange += MinAA;
			
			automana = Auto["automana"].Cast<Slider>().CurrentValue;
			disaa = AASettings["disaa"].Cast<Slider>().CurrentValue;
			minaa = AASettings["minaa"].Cast<Slider>().CurrentValue;
			autoei = Auto["autoei"].Cast<CheckBox>().CurrentValue;
			autoeo = Auto["autoeo"].Cast<CheckBox>().CurrentValue;
			autoq = Auto["autoq"].Cast<CheckBox>().CurrentValue;
			readyaa = AASettings["readyaa"].Cast<CheckBox>().CurrentValue;
		}
		
		static void AutoQ(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs args)
		{
			autoq = args.NewValue;
		}
		
		static void AutoEI(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs args)
		{
			autoei = args.NewValue;
		}
		
		static void AutoEO(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs args)
		{
			autoeo = args.NewValue;
		}
		
		static void AutoMana(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
		{
			automana = args.NewValue;
		}

		static void DisAA(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
		{
			disaa = args.NewValue;
		}

		static void ReadyAA(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs args)
		{
			readyaa = args.NewValue;
		}

		static void MinAA(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
		{
			minaa = args.NewValue;
		}
		
		static void Obj_AI_Base_OnNewPath(Obj_AI_Base sender, GameObjectNewPathEventArgs args)
		{
			int id = sender.NetworkId;
			if (!Timers.ContainsKey(id))
				return;
			Timers[id].Path = args.Path;
			Timers[id].PathTime = Game.Time;
		}
		
		static void Obj_AI_Base_OnBasicAttack(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
		{
			int id = sender.NetworkId;
			if (!Timers.ContainsKey(id))
				return;
			Timers[id].AAEndTime = Game.Time + sender.AttackCastDelay;
		}
		
		static void GameObject_OnCreate(GameObject sender, EventArgs args)
		{
			if (sender.Name == "Syndra_Base_Q_idle.troy" || sender.Name == "Syndra_Base_Q_Lv5_idle.troy")
			{
				QObjects.Add(sender.NetworkId, sender);
			}
		}

		static void GameObject_OnDelete(GameObject sender, EventArgs args)
		{
			int id = sender.NetworkId;
			if (QObjects.ContainsKey(id))
			{
				QObjects.Remove(id);
			}
		}
		
		static void Game_OnUpdate(EventArgs args)
		{
			RLogic();
			if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
			{
				DisableAA();
				QELogic();
				QLogic();
				if (Player.CanUseSpell(SpellSlot.E) == SpellState.Ready && Game.Time > lastq + 0.25f && Game.Time < lastq + 0.4f && Game.Time > laste + 2f && LastQCastPos.Distance(Player.Instance) < 700)
				{
					Player.CastSpell(SpellSlot.E, LastQCastPos);
					laste = Game.Time;
				}
				ELogic();
				WLogic();
			}
			else
			{
				Orbwalker.DisableAttacking = false;
				if (Player.Instance.ManaPercent > automana && autoq)
				{
					QLogic();
				}
				if (autoei && Player.CanUseSpell(SpellSlot.E) == SpellState.Ready && Game.Time > lastq + 0.25f && Game.Time < lastq + 0.4f && Game.Time > laste + 2f && LastQCastPos.Distance(Player.Instance) < 700)
				{
					Player.CastSpell(SpellSlot.E, LastQCastPos);
					laste = Game.Time;
				}
				if (autoeo)
				{
					QELogic();
					ELogic();
				}
			}
		}
		
		static void QELogic()
		{
			if (Player.CanUseSpell(SpellSlot.Q) != SpellState.Ready || Player.CanUseSpell(SpellSlot.E) != SpellState.Ready || Game.Time < lastq + 1f || Game.Time < laste + 1f)
				return;
			var enemy = TargetSelector.GetTarget(1200, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			Vector2 Pred = GetPoint(enemy, 1100, 2500, 0.25f);
			if (Pred.IsZero)
				return;
			Vector2 mepos = Player.Instance.Position.To2D();
			float dist = Pred.Distance(mepos);
			if (dist > 675)
			{
				Pred = mepos + (Pred - mepos).Normalized() * 675;
			}
			Vector3 Pred2 = Pred.To3D();
			Player.CastSpell(SpellSlot.Q, Pred2);
			Core.DelayAction(() => Player.CastSpell(SpellSlot.E, Pred2), 150);
			lastq = Game.Time;
			laste = Game.Time;
		}
		
		static void DisableAA()
		{
			var enemy = TargetSelector.GetTarget(Player.Instance.AttackRange + Player.Instance.BoundingRadius + 150, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			if ((Player.Instance.Level >= disaa && enemy.Health > Player.Instance.GetAutoAttackDamage(enemy) * minaa) ||
			    (readyaa && (Player.CanUseSpell(SpellSlot.Q) == SpellState.Ready || (Player.CanUseSpell(SpellSlot.W) == SpellState.Ready && Player.CanUseSpell(SpellSlot.E) != SpellState.Ready))))
			{
				Orbwalker.DisableAttacking = true;
			}
			else
			{
				Orbwalker.DisableAttacking = false;
			}
		}
		
		static void QLogic()
		{
			var enemy = TargetSelector.GetTarget(900, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			float delay = 0.4f;
			if (Game.Time > laste + 2f && Player.CanUseSpell(SpellSlot.E) == SpellState.Ready)
			{
				delay = 0.8f;
			}
			CastSpell(SpellSlot.Q, enemy, 800, 0, delay);
		}
		
		static void WLogic()
		{
			if (Player.CanUseSpell(SpellSlot.E) == SpellState.Ready || Game.Time < laste + 0.75f || Player.CanUseSpell(SpellSlot.W) != SpellState.Ready)
				return;
			
			if (Game.Time > wminion + 0.5f && Game.Time < wminion + 5 && !Player.Instance.HasBuff("syndrawtooltip"))
				wminion = 0;
			
			if (Player.Instance.HasBuff("syndrawtooltip"))
			{
				var enemy = TargetSelector.GetTarget(1050, DamageType.Magical);
				if (!enemy.IsValidTarget())
					return;
				CastSpell(SpellSlot.W, enemy, 950, 1450, 0f);
				return;
			}
			
			if (Game.Time < wminion + 5 || Player.Instance.HasBuff("syndrawtooltip"))
				return;
			
			int count = EntityManager.Heroes.Enemies.Count(x => x.IsValidTarget(900));
			
			if (count < 1)
				return;
			
			foreach (var syndrasq in QObjects.Where(x=>x.Value.Position.Distance(Player.Instance)<925))
			{
				Player.CastSpell(SpellSlot.W, syndrasq.Value.Position);
				wminion = Game.Time;
				break;
			}
			if (Game.Time < wminion + 5)
				return;
			
			foreach (var minion in EntityManager.MinionsAndMonsters.EnemyMinions.Where(x=>x.Position.Distance(Player.Instance)<925))
			{
				Player.CastSpell(SpellSlot.W, minion.Position);
				wminion = Game.Time;
				break;
			}
		}
		
		static void ELogic()
		{
			if (Game.Time < laste + 2f || Player.CanUseSpell(SpellSlot.E) != SpellState.Ready)
				return;
			var enemy = TargetSelector.GetTarget(1200, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			foreach (var qobj in QObjects)
			{
				Vector2 P1 = Player.Instance.Position.To2D();
				Vector2 P2 = GetPoint(enemy, 1100, 2500, 0.25f);
				if (P2.IsZero)
					return;
				Vector2 P3 = qobj.Value.Position.To2D();
				float P1P3 = P3.Distance(P1);
				float P1P2 = P2.Distance(P1);
				if (P1P3 < 700 && P1P3 > 150)
				{
					int plus = 0;
					if (P1P3 > P1P2)
					{
						plus = (int)(P1P3 - P1P2 + 25);
						P2 = P2 + (P2 - P1).Normalized() * plus;
					}
					else
					{
						plus = (int)(P1P2 - P1P3 - 25);
						P3 = P3 + (P3 - P1).Normalized() * plus;
					}
					Vector2 A = P1 - P2;
					Vector2 B = P1 - P3;
					double angle = Math.Abs(Math.Atan2(A.X * B.Y - A.Y * B.X, A.X * B.X + A.Y * B.Y) * 180 / Math.PI);
					if (angle > 90)
						return;
					double dist = Dist_Point_Line_Segment(P1, P2, P3);
					if (dist < 75f)
					{
						Player.CastSpell(SpellSlot.E, P3.To3D());
						laste = Game.Time;
						break;
					}
				}
			}
		}
		
		static bool CanUlt(AIHeroClient unit)
		{
			float magicresist = (unit.SpellBlock - Player.Instance.FlatMagicPenetrationMod) * Player.Instance.PercentMagicPenetrationMod;
			float damage = (1f - (magicresist / (magicresist + 100))) * (3 + QObjects.Count()) * (new[] { 90, 135, 180 }[R.Level - 1] + (Player.Instance.TotalMagicalDamage * 0.2f));
			if (damage + 100f > unit.MagicShield + unit.Health && unit.Health / unit.MaxHealth * 100 > 10)
			{
				return true;
			}
			return false;
		}
		
		static void RLogic()
		{
			if (R.IsReady())
			{
				float extra = 0f;
				int level = R.Level;
				if (level == 3)
				{
					extra = level * 25;
				}
				var target = TargetSelector.GetTarget(675f + extra, DamageType.Magical);
				if (target.IsValidTarget() && CanUlt(target) && Auto[target.ChampionName].Cast<CheckBox>().CurrentValue)
				{
					R.Cast(target);
				}
			}
		}
		
		static bool CastSpell(SpellSlot slot, AIHeroClient enemy, int range, int speed, float delay)
		{
			if (Player.CanUseSpell(slot) != SpellState.Ready)
				return false;
			int enemyid = enemy.NetworkId;
			bool aatrue = Game.Time < Timers[enemyid].AAEndTime;
			Vector2 mepos = Player.Instance.Position.To2D();
			Vector2 enemypos = enemy.Position.To2D();
			float dist = mepos.Distance(enemypos);
			if (aatrue)
			{
				if (dist > range)
					return false;
				Player.CastSpell(slot, enemy.Position);
				if (slot == SpellSlot.Q)
				{
					LastQCastPos = enemy.Position;
					lastq = Game.Time;
				}
				return true;
			}
			float enemyspeed = enemy.MoveSpeed;
			Vector3[] path = Timers[enemyid].Path;
			int lenght = path.Length;
			Vector3 predpos = Vector3.Zero;
			if (lenght > 1)
			{
				float s_in_time = enemyspeed * (Game.Time - Timers[enemyid].PathTime + (Game.Ping * 0.001f));
				float d = 0f;
				for (int i = 0; i < lenght - 1; i++)
				{
					Vector2 vi = path[i].To2D();
					Vector2 vi1 = path[i + 1].To2D();
					d += vi.Distance(vi1);
					if (d >= s_in_time)
					{
						float dd = enemypos.Distance(vi1);
						float t = 0f;
						if (speed == 0)
						{
							t = delay;
						}
						else
						{
							t = Quadratic_Equation(mepos, enemypos, vi1, enemyspeed, speed, delay);
						}
						float ss = enemyspeed * t;
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
							if (speed == 0)
							{
								ss -= dd;
							}
							else
							{
								t = Quadratic_Equation(mepos, vj, vj1, enemyspeed, speed, delay);
								ss = (enemyspeed * t) - dd;
							}
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
			if (predpos.IsZero || predpos.Distance(mepos) > range || (int)path.LastOrDefault().X != (int)enemy.Path.LastOrDefault().X)
				return false;
			Player.CastSpell(slot, predpos);
			if (slot == SpellSlot.Q)
			{
				LastQCastPos = enemy.Position;
				lastq = Game.Time;
			}
			return true;
		}
		
		static Vector2 GetPoint(AIHeroClient enemy, int range, int speed, float delay, bool checktime = false)
		{
			int enemyid = enemy.NetworkId;
			Vector2 predpos = Vector2.Zero;
			Vector2 mepos = Player.Instance.Position.To2D();
			Vector2 enemypos = enemy.Position.To2D();
			float dist = mepos.Distance(enemypos);
			if (Game.Time < Timers[enemyid].AAEndTime)
			{
				if (dist > range)
				{
					return predpos;
				}
				return enemypos;
			}
			if (checktime && Game.Time > Timers[enemyid].PathTime + 0.2f)
				return predpos;
			float enemyspeed = enemy.MoveSpeed;
			Vector3[] path = Timers[enemyid].Path;
			int lenght = path.Length;
			Vector2 enemypath = path.LastOrDefault().To2D();
			if (lenght > 1)
			{
				float s_in_time = enemyspeed * (Game.Time - Timers[enemyid].PathTime + (Game.Ping * 0.001f));
				float d = 0f;
				for (int i = 0; i < lenght - 1; i++)
				{
					Vector2 vi = path[i].To2D();
					Vector2 vi1 = path[i + 1].To2D();
					d += vi.Distance(vi1);
					if (d >= s_in_time)
					{
						float dd = enemypos.Distance(vi1);
						float t = 0f;
						if (speed == 0)
						{
							t = delay;
						}
						else
						{
							t = Quadratic_Equation(mepos, enemypos, vi1, enemyspeed, speed, delay);
						}
						float ss = enemyspeed * t;
						if (dd >= ss)
						{
							predpos = enemypos + ((vi1 - enemypos).Normalized() * ss);
							break;
						}
						if (i + 1 == lenght - 1)
						{
							predpos = enemypos + ((vi1 - enemypos).Normalized() * enemypos.Distance(vi1));
							break;
						}
						for (int j = i + 1; j < lenght - 1; j++)
						{
							Vector2 vj = path[j].To2D();
							Vector2 vj1 = path[j + 1].To2D();
							if (speed == 0)
							{
								ss -= dd;
							}
							else
							{
								t = Quadratic_Equation(mepos, vj, vj1, enemyspeed, speed, delay);
								ss = (enemyspeed * t) - dd;
							}
							dd = vj.Distance(vj1);
							if (dd >= ss)
							{
								predpos = vj + ((vj1 - vj).Normalized() * ss);
								break;
							}
							if (j + 1 == lenght - 1)
							{
								predpos = vj + ((vj1 - vj).Normalized() * dd);
								break;
							}
						}
						break;
					}
					if (i + 1 == lenght - 1)
					{
						predpos = vi + ((vi1 - vi).Normalized() * vi.Distance(vi1));
						break;
					}
				}
			}
			else
			{
				predpos = enemypos;
			}
			if (predpos.IsZero || predpos.Distance(mepos) > range || (int)enemypath.X != (int)enemy.Path.LastOrDefault().X)
			{
				return Vector2.Zero;
			}
			return predpos;
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
