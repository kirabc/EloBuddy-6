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
	class Program
	{
		static float lastq, lastw, laste, wminion;
		static Vector3 qpt, wpt;
		static Menu Config, Auto;
		static readonly Dictionary<int, GameObject> GrabableW = new Dictionary<int, GameObject>();
		static readonly Spell.Skillshot Q = new Spell.Skillshot(SpellSlot.Q, 800, EloBuddy.SDK.Enumerations.SkillShotType.Circular, 250, int.MaxValue, 150, DamageType.Magical) { MinimumHitChance = EloBuddy.SDK.Enumerations.HitChance.Medium };
		static readonly Spell.Skillshot W = new Spell.Skillshot(SpellSlot.W, 950, EloBuddy.SDK.Enumerations.SkillShotType.Circular, 250, 1450, 210, DamageType.Magical) { MinimumHitChance = EloBuddy.SDK.Enumerations.HitChance.Medium };
		static readonly Spell.Skillshot E = new Spell.Skillshot(SpellSlot.E, 700, EloBuddy.SDK.Enumerations.SkillShotType.Linear, 250, 2000, 100, DamageType.Magical) { MinimumHitChance = EloBuddy.SDK.Enumerations.HitChance.High };
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
				Auto.Add(enemy.ChampionName, new CheckBox(enemy.ChampionName));
			}
			Auto.AddSeparator();
			Auto.AddGroupLabel("AUTO Harras:");
			Auto.Add("autoq", new CheckBox("Q"));
			Auto.Add("automana", new Slider("Minimum Mana Percent", 50));
			Game.OnUpdate += Game_OnUpdate;
			GameObject.OnCreate += GameObject_OnCreate;
			GameObject.OnDelete += GameObject_OnDelete;
		}

		static void Game_OnUpdate(EventArgs args)
		{
			RLogic();
			if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
			{
				if (Q.IsReady())
				{
					Orbwalker.DisableAttacking = true;
				}
				else
				{
					Orbwalker.DisableAttacking = false;
				}
				QE1100();
				QLogic();
				if (E.IsReady() && Game.Time < lastq + 0.3f && Game.Time > laste + 2f)
				{
					Player.CastSpell(SpellSlot.E, qpt);
					laste = Game.Time;
				}
				WLogic();
				if (W.IsReady() && Game.Time < wminion + 5f && Game.Time > lastw + 2f)
				{
					if (Game.Time < wminion + 0.3f)
					{
						if (Player.Instance.HasBuff("syndrawtooltip"))
						{
							Player.CastSpell(SpellSlot.W, wpt);
							lastw = Game.Time;
						}
					}
					else if (Game.Time > wminion + 0.5f && !Player.Instance.HasBuff("syndrawtooltip"))
					{
						wminion = 0;
					}
				}
			}
			else
			{
				Orbwalker.DisableAttacking = false;
				if (Player.Instance.ManaPercent > Auto["automana"].Cast<Slider>().CurrentValue && Auto["autoq"].Cast<CheckBox>().CurrentValue)
				{
					QLogic();
				}
			}
		}
		
		static void QE1100()
		{
			if (!Q.IsReady() || !E.IsReady() || Game.Time < lastq + 1f || Game.Time < laste + 1f)
				return;
			var target = TargetSelector.GetTarget(1300, DamageType.Magical);
			if (!target.IsValidTarget())
				return;
			Vector3 pos1 = E.GetPrediction(target).CastPosition;
			Vector3 pos2 = Player.Instance.Position;
			float d = pos1.Distance(pos2);
			if (!pos1.IsZero && d < 1100 && d > 700)
			{
				Vector3 pos3 = pos2 + ((pos1 - pos2).Normalized() * 675);
				Player.CastSpell(SpellSlot.Q, pos3);
				Player.CastSpell(SpellSlot.E, pos3);
				lastq = Game.Time;
				laste = Game.Time;
			}
		}
		
		static void QLogic()
		{
			if (!Q.IsReady())
				return;
			var target = TargetSelector.GetTarget(1000, DamageType.Magical);
			if (target.IsValidTarget())
			{
				Vector3 pos = Q.GetPrediction(target).CastPosition;
				if (!pos.IsZero && pos.Distance(Player.Instance) < 800)
				{
					qpt = pos;
					if (Q.Cast(target))
						lastq = Game.Time;
				}
				else
				{
					var t = TargetSelector.GetTarget(800, DamageType.Magical);
					if (t.IsValidTarget())
					{
						qpt = Q.GetPrediction(t).CastPosition;
						if (Q.Cast(t))
							lastq = Game.Time;
					}
				}
			}
		}
		
		static void WLogic()
		{
			if (!W.IsReady() || E.IsReady() || Game.Time < laste + 0.75f)
			{
				return;
			}
			var t1 = TargetSelector.GetTarget(1050, DamageType.Magical);
			if (t1.IsValidTarget())
			{
				Vector3 pos1 = W.GetPrediction(t1).CastPosition;
				if (!pos1.IsZero && pos1.Distance(Player.Instance) < 950)
				{
					GrabMinion(pos1);
				}
				else
				{
					var t2 = TargetSelector.GetTarget(950, DamageType.Magical);
					if (t2.IsValidTarget())
					{
						Vector3 pos2 = W.GetPrediction(t2).CastPosition;
						if (!pos2.IsZero && pos2.Distance(Player.Instance) < 950)
						{
							GrabMinion(pos2);
						}
					}
				}
			}
		}
		
		static void GrabMinion(Vector3 pos)
		{
			if (Game.Time < wminion + 5)
				return;
			foreach (var syndrasq in GrabableW.Where(x=>x.Value.Position.Distance(Player.Instance)<925))
			{
				Player.CastSpell(SpellSlot.W, syndrasq.Value.Position);
				wminion = Game.Time;
				wpt = pos;
				break;
			}
			if (Game.Time < wminion + 5)
				return;
			foreach (var minion in EntityManager.MinionsAndMonsters.EnemyMinions.Where(x=>x.Position.Distance(Player.Instance)<925))
			{
				Player.CastSpell(SpellSlot.W, minion.Position);
				wminion = Game.Time;
				wpt = pos;
				break;
			}
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

		static void GameObject_OnCreate(GameObject sender, EventArgs args)
		{
			if (sender.Name == "Syndra_Base_Q_idle.troy" || sender.Name == "Syndra_Base_Q_Lv5_idle.troy")
			{
				GrabableW.Add(sender.NetworkId, sender);
			}
		}

		static void GameObject_OnDelete(GameObject sender, EventArgs args)
		{
			int id = sender.NetworkId;
			if (GrabableW.ContainsKey(id))
			{
				GrabableW.Remove(id);
			}
		}
		
		static bool CanUlt(AIHeroClient unit)
		{
			float magicresist = (unit.SpellBlock - Player.Instance.FlatMagicPenetrationMod) * Player.Instance.PercentMagicPenetrationMod;
			float damage = (1f - (magicresist / (magicresist + 100))) * (3 + GrabableW.Count()) * (new[] { 90, 135, 180 }[R.Level - 1] + (Player.Instance.TotalMagicalDamage * 0.2f));
			if (damage + 100f > unit.MagicShield + unit.Health && unit.Health / unit.MaxHealth * 100 > 10)
			{
				return true;
			}
			return false;
		}
	}
}
