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
		
		static Menu Config, Auto;
		
		static readonly Spell.Targeted R = new Spell.Targeted(SpellSlot.R, 675, DamageType.Magical);
		
		static readonly Spell.Targeted R5 = new Spell.Targeted(SpellSlot.R, 750, DamageType.Magical);
		
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
			Config.AddGroupLabel("ParaSyndra [1.0.0.8]");
			Auto = Config.AddSubMenu("Automatic");
			Auto.AddGroupLabel("Ulti ON:");
			foreach (var enemy in EntityManager.Heroes.Enemies)
			{
				Timers.Add(enemy.NetworkId, new Timer(0f, 0f, enemy.Path));
				Auto.Add(enemy.ChampionName, new CheckBox(enemy.ChampionName));
			}
			Auto.AddSeparator();
			Auto.AddGroupLabel("AUTO Harras:");
			Auto.Add("autoq", new CheckBox("Q"));
			Auto.Add("autoei", new CheckBox("QE - Enemy In Q Range"));
			Auto.Add("autoeo", new CheckBox("QE - Enemy Out Of Q Range"));
			Auto.Add("automana", new Slider("Minimum Mana Percent", 50));
			Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
			Obj_AI_Base.OnBasicAttack += Obj_AI_Base_OnBasicAttack;
			GameObject.OnCreate += GameObject_OnCreate;
			GameObject.OnDelete += GameObject_OnDelete;
			Game.OnUpdate += Game_OnUpdate;
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
				QELogic();
				QLogic();
				if (Player.CanUseSpell(SpellSlot.E) == SpellState.Ready && Game.Time > lastq + 0.15f && Game.Time < lastq + 0.35f && Game.Time > laste + 2f && LastQCastPos.Distance(Player.Instance) < 700)
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
				if (Player.Instance.ManaPercent > Auto["automana"].Cast<Slider>().CurrentValue && Auto["autoq"].Cast<CheckBox>().CurrentValue)
				{
					QLogic();
				}
				if (Auto["autoei"].Cast<CheckBox>().CurrentValue && Player.CanUseSpell(SpellSlot.E) == SpellState.Ready && Game.Time > lastq + 0.15f && Game.Time < lastq + 0.35f && Game.Time > laste + 2f && LastQCastPos.Distance(Player.Instance) < 700)
				{
					Player.CastSpell(SpellSlot.E, LastQCastPos);
					laste = Game.Time;
				}
				if (Auto["autoeo"].Cast<CheckBox>().CurrentValue)
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
			var enemy = TargetSelector.GetTarget(1100, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			Vector2 Pred = GetPoint(enemy, 1100, 0, 0.75f);
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
		
		static void QLogic()
		{
			var enemy = TargetSelector.GetTarget(800, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			if (Player.Instance.Level > 10 && enemy.Health > Player.Instance.GetAutoAttackDamage(enemy) * 3)
			{
				Orbwalker.DisableAttacking = true;
			}
			else
			{
				Orbwalker.DisableAttacking = false;
			}
			float delay = 0.5f;
			if (Game.Time > laste + 2f && Player.CanUseSpell(SpellSlot.E) == SpellState.Ready || Player.CanUseSpell(SpellSlot.W) != SpellState.Ready)
			{
				delay = 0.9f;
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
				var enemy = TargetSelector.GetTarget(950, DamageType.Magical);
				if (!enemy.IsValidTarget())
					return;
				CastSpell(SpellSlot.W, enemy, 950, 1450, 0.5f);
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
			var enemy = TargetSelector.GetTarget(1100, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			foreach (var qobj in QObjects)
			{
				Vector2 P1 = Player.Instance.Position.To2D();
				Vector2 P2 = GetPoint(enemy, 1100, 0, 0.5f, true);
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
						Vector2 A = P2 - P1;
						Vector2 B = P2 - P3;
						double angle = Math.Abs(Math.Atan2(A.X * B.Y - A.Y * B.X, A.X * B.X + A.Y * B.Y) * 180 / Math.PI);
						if (angle < 90)
							return;
						plus = (int)(P1P3 - P1P2);
						P2 = P2 + (P2 - P1).Normalized() * plus;
					}
					else
					{
						Vector2 A = P3 - P1;
						Vector2 B = P3 - P2;
						double angle = Math.Abs(Math.Atan2(A.X * B.Y - A.Y * B.X, A.X * B.X + A.Y * B.Y) * 180 / Math.PI);
						if (angle < 90)
							return;
						plus = (int)(P1P2 - P1P3);
						P3 = P3 + (P3 - P1).Normalized() * plus;
					}
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
			if (lenght == 2)
			{
				Vector2 enemypath = path.LastOrDefault().To2D();
				float d = enemypos.Distance(enemypath);
				float t = 0f;
				if (speed == 0)
				{
					t = delay;
				}
				else
				{
					t = Quadratic_Equation(mepos, enemypos, enemypath, enemyspeed, speed, delay);
				}
				float s = enemyspeed * t;
				if (d > s)
				{
					predpos = (enemypos + ((enemypath - enemypos).Normalized() * s)).To3D();
				}
				else
				{
					predpos = (enemypos + ((enemypath - enemypos).Normalized() * d)).To3D();
				}
			}
			else if (lenght < 2)
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
			if (lenght == 2)
			{
				float d = enemypos.Distance(enemypath);
				float t = 0f;
				if (speed == 0)
				{
					t = delay;
				}
				else
				{
					t = Quadratic_Equation(mepos, enemypos, enemypath, enemyspeed, speed, delay);
				}
				float s = enemyspeed * t;
				if (d > s)
				{
					predpos = enemypos + (enemypath - enemypos).Normalized() * s;
				}
				else
				{
					predpos = enemypos + (enemypath - enemypos).Normalized() * d;
				}
			}
			else if (lenght < 2)
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
				return (float)Math.Max(t1, t2) + (Game.Ping / 1000) + delay;
			}
			if (d >= 0 && d < 0.00001)
			{
				return (-b / (2 * a)) + (Game.Ping / 1000) + delay;
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
