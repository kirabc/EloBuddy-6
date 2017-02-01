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
		
		static float lastq, lastw, laste;
		
		static bool wobj;
		
		static Vector3 LastQCastPos;
		
		static Menu Config, Auto, AASettings;
		
		static readonly Spell.Targeted R = new Spell.Targeted(SpellSlot.R, 675, DamageType.Magical);
		
		static readonly Spell.Targeted R5 = new Spell.Targeted(SpellSlot.R, 750, DamageType.Magical);
		
		public static void Main(string[] args)
		{
			Loading.OnLoadingComplete += Loading_OnLoadingComplete;
		}
		
		static int automana, disaa, minaa;
		
		static bool autoei, autoeo, autoq, readyaa;
		
		static void Loading_OnLoadingComplete(EventArgs args)
		{
			if (Player.Instance.ChampionName != "Syndra")
				return;
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
			Auto.Add("autoq", new CheckBox("Auto Q"));
			Auto.Add("automana", new Slider("Auto Q - Minimum Mana Percent", 50));
			Auto.Add("autoei", new CheckBox("Auto QE - Enemy In Q Range", false));
			Auto.Add("autoeo", new CheckBox("Auto QE - Enemy Out Of Q Range", false));
			AASettings = Config.AddSubMenu("Attack");
			AASettings.Add("readyaa", new CheckBox("Disable If Q Or W Ready"));
			AASettings.Add("disaa", new Slider("Disable At Level", 11, 1, 18));
			AASettings.Add("minaa", new Slider("Enable If Killable With x AA", 3, 1, 6));
			
			Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
			Obj_AI_Base.OnBasicAttack += Obj_AI_Base_OnBasicAttack;
			GameObject.OnCreate += GameObject_OnCreate;
			GameObject.OnDelete += GameObject_OnDelete;
			Game.OnUpdate += Game_OnUpdate;
			Obj_AI_Base.OnBuffGain += Obj_AI_Base_OnBuffGain;
			Obj_AI_Base.OnBuffLose += Obj_AI_Base_OnBuffLose;
			
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
			Timers[id].PathTime = Game.Time;
			Timers[id].Path = args.Path;
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

		static void Obj_AI_Base_OnBuffGain(Obj_AI_Base sender, Obj_AI_BaseBuffGainEventArgs args)
		{
			if (sender.IsMe && args.Buff.Name == "syndrawtooltip")
			{
				wobj = true;
			}
		}

		static void Obj_AI_Base_OnBuffLose(Obj_AI_Base sender, Obj_AI_BaseBuffLoseEventArgs args)
		{
			if (sender.IsMe && args.Buff.Name == "syndrawtooltip")
			{
				wobj = false;
			}
		}

		static void QLogic()
		{
			var enemy = TargetSelector.GetTarget(800, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			float delay = 0.4f;
			if (Game.Time > laste + 2f && Player.CanUseSpell(SpellSlot.E) == SpellState.Ready)
			{
				delay += 0.4f;
			}
			CastSpell(SpellSlot.Q, enemy, 800, 0, delay);
		}
		
		static void WLogic()
		{
			if (Player.CanUseSpell(SpellSlot.E) == SpellState.Ready || Game.Time < laste + 0.75f)
				return;
			
			if (wobj && Game.Time > lastw + 0.25f)
			{
				var enemy = TargetSelector.GetTarget(950, DamageType.Magical);
				if (enemy.IsValidTarget())
				{
					CastSpell(SpellSlot.W, enemy, 950, 1450, 0.2f);
				}
			}
			
			if (wobj || Game.Time < lastw + 5f || Player.CanUseSpell(SpellSlot.W) != SpellState.Ready)
				return;
			
			var check = TargetSelector.GetTarget(900, DamageType.Magical);
			if (!check.IsValidTarget())
				return;
			
			foreach (var qobj in QObjects)
			{
				Vector3 pos = qobj.Value.Position;
				if (pos.Distance(Player.Instance.Position) < 925)
				{
					Player.CastSpell(SpellSlot.W, pos);
					wobj = true;
					lastw = Game.Time;
					break;
				}
			}
			
			if (wobj || Game.Time < lastw + 5f)
				return;
			
			foreach (var m in EntityManager.MinionsAndMonsters.EnemyMinions)
			{
				if (m.IsEnemy)
				{
					Vector3 pos = m.Position;
					if (pos.Distance(Player.Instance.Position) < 925)
					{
						Player.CastSpell(SpellSlot.W, pos);
						wobj = true;
						lastw = Game.Time;
						break;
					}
				}
			}
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
				return;
			}
			Orbwalker.DisableAttacking = false;
		}
		
		static void QELogic()
		{
			if (Player.CanUseSpell(SpellSlot.Q) != SpellState.Ready || Player.CanUseSpell(SpellSlot.E) != SpellState.Ready || Game.Time < lastq + 1f || Game.Time < laste + 1f)
				return;
			var enemy = TargetSelector.GetTarget(1100, DamageType.Magical);
			if (!enemy.IsValidTarget())
				return;
			Vector2 Pred = GetPrediction(enemy, 1100, 0, 0.75f);
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
				Vector2 P2 = GetPrediction(enemy, 1100, 0, 0.5f);
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
		
		static bool CastSpell(SpellSlot Slot, AIHeroClient Enemy, int SpellRange, int SpellSpeed, float SpellDelay, bool CheckTime = false, int CheckTimer = 1)
		{
			if (Player.CanUseSpell(Slot) != SpellState.Ready)
				return false;
			
			int EnemyID = Enemy.NetworkId;
			Vector2 SourcePos = Player.Instance.Position.To2D();
			Vector2 EnemyPos = Enemy.Position.To2D();
			float Dist = SourcePos.Distance(EnemyPos);
			
			if (Game.Time < Timers[EnemyID].AAEndTime)
			{
				if (Dist > SpellRange)
					return false;
				Player.CastSpell(Slot, EnemyPos.To3D());
				if (Slot == SpellSlot.Q)
				{
					LastQCastPos = EnemyPos.To3D();
					lastq = Game.Time;
				}
				return true;
			}
			
			Vector3[] Path = Timers[EnemyID].Path;
			float PathTime = Timers[EnemyID].PathTime;
			float EnemySpeed = Enemy.MoveSpeed;
			
			Vector2 PredPos = GetPoint(Path, SourcePos, EnemyPos, PathTime, EnemySpeed, SpellSpeed, SpellDelay + (Game.Ping * 0.001f));
			if (PredPos.IsZero || PredPos.Distance(SourcePos) > SpellRange || (int)Path.LastOrDefault().X != (int)Enemy.Path.LastOrDefault().X || (int)EnemySpeed != (int)Enemy.MoveSpeed || (CheckTime && Game.Time > PathTime + (CheckTimer / 1000) && Game.Time > Timers[EnemyID].PathTime + (CheckTimer / 1000)))
			{
				return false;
			}
			
			Player.CastSpell(Slot, PredPos.To3D());
			if (Slot == SpellSlot.Q)
			{
				LastQCastPos = PredPos.To3D();
				lastq = Game.Time;
			}
			return true;
		}
		
		static Vector2 GetPrediction(AIHeroClient Enemy, int SpellRange, int SpellSpeed, float SpellDelay, bool CheckTime = false, int CheckTimer = 1)
		{
			int EnemyID = Enemy.NetworkId;
			Vector2 SourcePos = Player.Instance.Position.To2D();
			Vector2 EnemyPos = Enemy.Position.To2D();
			float Dist = SourcePos.Distance(EnemyPos);
			
			if (Game.Time < Timers[EnemyID].AAEndTime)
			{
				if (Dist > SpellRange)
					return Vector2.Zero;
				return EnemyPos;
			}
			
			Vector3[] Path = Timers[EnemyID].Path;
			float PathTime = Timers[EnemyID].PathTime;
			float EnemySpeed = Enemy.MoveSpeed;
			
			Vector2 PredPos = GetPoint(Path, SourcePos, EnemyPos, PathTime, EnemySpeed, SpellSpeed, SpellDelay + (Game.Ping * 0.001f));
			if (PredPos.IsZero || PredPos.Distance(SourcePos) > SpellRange || (int)Path.LastOrDefault().X != (int)Enemy.Path.LastOrDefault().X || (int)EnemySpeed != (int)Enemy.MoveSpeed || (CheckTime && Game.Time > PathTime + (CheckTimer / 1000) && Game.Time > Timers[EnemyID].PathTime + (CheckTimer / 1000)))
			{
				return Vector2.Zero;
			}
			
			return PredPos;
		}
		
		static Vector2 GetPoint(Vector3[] Path, Vector2 SourcePos, Vector2 EnemyPos, float PathTime, float EnemySpeed, int SpellSpeed, float SpellDelay)
		{
			float Dist = SourcePos.Distance(EnemyPos);
			int Lenght = Path.Length;
			if (Lenght > 1)
			{
				float s_in_time = EnemySpeed * (Game.Time - PathTime);
				float d = 0f;
				for (int i = 0; i < Lenght - 1; i++)
				{
					Vector2 vi = Path[i].To2D();
					Vector2 vi1 = Path[i + 1].To2D();
					d += vi.Distance(vi1);
					if (d >= s_in_time)
					{
						float dd = EnemyPos.Distance(vi1);
						float t = 0f;
						if (SpellSpeed == 0)
						{
							t = SpellDelay;
						}
						else
						{
							t = Quadratic_Equation(SourcePos, EnemyPos, vi1, EnemySpeed, SpellSpeed) + SpellDelay;
						}
						float ss = EnemySpeed * t;
						if (dd >= ss)
						{
							return EnemyPos + ((vi1 - EnemyPos).Normalized() * ss);
						}
						if (i + 1 == Lenght - 1)
						{
							return EnemyPos + ((vi1 - EnemyPos).Normalized() * EnemyPos.Distance(vi1));
						}
						for (int j = i + 1; j < Lenght - 1; j++)
						{
							Vector2 vj = Path[j].To2D();
							Vector2 vj1 = Path[j + 1].To2D();
							if (SpellSpeed == 0)
							{
								ss -= dd;
							}
							else
							{
								t = Quadratic_Equation(SourcePos, vj, vj1, EnemySpeed, SpellSpeed) + SpellDelay;
								ss = (EnemySpeed * t) - dd;
							}
							dd = vj.Distance(vj1);
							if (dd >= ss)
							{
								return vj + ((vj1 - vj).Normalized() * ss);
							}
							if (j + 1 == Lenght - 1)
							{
								return vj + ((vj1 - vj).Normalized() * dd);
							}
						}
					}
					else if (i + 1 == Lenght - 1)
					{
						return vi + ((vi1 - vi).Normalized() * vi.Distance(vi1));
					}
				}
			}
			return EnemyPos;
		}
		
		static float Quadratic_Equation(Vector2 source, Vector2 startP, Vector2 endP, float unitspeed, int spellspeed)
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
				return (float)Math.Max(t1, t2);
			}
			if (d >= 0 && d < 0.00001)
			{
				return (-b / (2 * a));
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
