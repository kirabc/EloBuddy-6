using System;
using System.Linq;
using System.Collections.Generic;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Enumerations;
using SharpDX;
namespace ParaSyndra
{
	class Program
	{
		static Menu Config, Auto;
		static readonly Dictionary<int, GameObject> GrabableW = new Dictionary<int, GameObject>();
		static readonly Spell.Skillshot Q = new Spell.Skillshot(SpellSlot.Q, 800, SkillShotType.Circular, 275, int.MaxValue, 225, DamageType.Magical) { MinimumHitChance = HitChance.High };
		static readonly Spell.Skillshot W = new Spell.Skillshot(SpellSlot.W, 950, SkillShotType.Circular, 275, 2500, 225, DamageType.Magical) { MinimumHitChance = HitChance.High };
		static readonly Spell.Skillshot E = new Spell.Skillshot(SpellSlot.E, 700, SkillShotType.Linear, 250, 2500, 55, DamageType.Magical) { MinimumHitChance = HitChance.High };
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
			Config.AddGroupLabel("Para Syndra [1.0.0.6]");
			
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
				if (Game.Time > laste + 0.5f)
				{
					QLogic();
				}
				if (E.IsReady() && Game.Time > lastq + 0.2f && Game.Time < lastq + 0.4f && Game.Time > laste + 2)
				{
					qe++;
					Chat.Print("qe: "+qe);
					Player.CastSpell(SpellSlot.E, qpt);
					laste = Game.Time;
				}
				if (Game.Time > laste + 0.5f)
				{
					WLogic();
					if (Game.Time < lastwobj + 0.25f && Game.Time > lastw + 2)
					{
						w++;
						Chat.Print("w: "+w);
						lastw = Game.Time;
						Player.CastSpell(SpellSlot.W, wpt);
					}
					else if (Game.Time > lastwobj + 0.25f && Game.Time < lastwobj + 5 && Player.Instance.HasBuff("syndrawtooltip"))
					{
						var target = TargetSelector.GetTarget(1050, DamageType.Magical);
						if (target.IsValidTarget())
						{
							Vector3 pos1 = W.GetPrediction(target).CastPosition;
							if (pos1.Distance(Player.Instance) < 950)
							{
								lastw = Game.Time;
								Player.CastSpell(SpellSlot.W, pos1);
							}
							else
							{
								var t = TargetSelector.GetTarget(950, DamageType.Magical);
								if (t.IsValidTarget())
								{
									Vector3 pos2 = W.GetPrediction(t).CastPosition;
									if (pos2.Distance(Player.Instance) < 950)
									{
										lastw = Game.Time;
										Player.CastSpell(SpellSlot.W, pos2);
									}
								}
							}
						}
					}
				}
				if (E.IsReady() && Game.Time > lastw + 0.2f && Game.Time < lastw + 0.5f && !Player.Instance.HasBuff("syndrawtooltip") && Game.Time > laste + 2 && qball && wpt.Distance(Player.Instance)<675)
				{
					we++;
					Chat.Print("we: "+we);
					Player.CastSpell(SpellSlot.E, wpt);
					qball = false;
					laste = Game.Time;
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
		
		static int q, w, qe, we, gm, go;
		static float lastq, lastwobj, lastw, laste;
		static Vector3 qpt, wpt;
		static bool qball;
		static void QLogic()
		{
			if (!Q.IsReady() || Game.Time < lastq + 2)
				return;
			var target = TargetSelector.GetTarget(1000, DamageType.Magical);
			if (target.IsValidTarget())
			{
				Vector3 pos = Q.GetPrediction(target).CastPosition;
				if (pos.Distance(Player.Instance) < 800)
				{
					if (Player.CastSpell(SpellSlot.Q, pos))
					{
						q++;
						Chat.Print("q: "+q);
						qpt = pos;
						lastq = Game.Time;
						return;
					}
				}
				else
				{
					var t = TargetSelector.GetTarget(800, DamageType.Magical);
					if (t.IsValidTarget())
					{
						Vector3 pos2 = Q.GetPrediction(target).CastPosition;
						if (pos2.Distance(Player.Instance) < 800)
						{
							if (Player.CastSpell(SpellSlot.Q, pos2))
							{
								q++;
								Chat.Print("q: "+q);
								qpt = pos2;
								lastq = Game.Time;
								return;
							}
						}
					}
				}
			}
		}
		
		static void WLogic()
		{
			if (!W.IsReady()) return;
			if (Game.Time < lastwobj + 5) // E.IsReady() || Game.Time < laste + 0.5f ||
			{
				return;
			}
			bool pos = false;
			Vector3 wobj = new Vector3();
			foreach (var syndrasq in GrabableW.Where(x=>x.Value.Position.Distance(Player.Instance)<925))
			{
				qball = true;
				pos = true;
				wobj = syndrasq.Value.Position;
				break;
			}
			if (!pos)
			{
				foreach (var minion in EntityManager.MinionsAndMonsters.EnemyMinions.Where(x=>x.Position.Distance(Player.Instance)<925))
				{
					pos = true;
					wobj = minion.Position;
					break;
				}
			}
			if (pos)
			{
				var target = TargetSelector.GetTarget(1050, DamageType.Magical);
				if (target.IsValidTarget())
				{
					Vector3 pos1 = W.GetPrediction(target).CastPosition;
					if (pos1.Distance(Player.Instance) < 950)
					{
						if (qball)
						{
							go++;
							Chat.Print("grab qball: "+go);
						}
						else
						{
							gm++;
							Chat.Print("grab minion: "+gm);
						}
						wpt = pos1;
						lastwobj = Game.Time;
						Player.CastSpell(SpellSlot.W, wobj);
						return;
					}
					else
					{
						var t = TargetSelector.GetTarget(950, DamageType.Magical);
						if (t.IsValidTarget())
						{
							Vector3 pos2 = W.GetPrediction(t).CastPosition;
							if (pos2.Distance(Player.Instance) < 950)
							{
								if (qball)
								{
									go++;
									Chat.Print("grab qball: "+go);
								}
								else
								{
									gm++;
									Chat.Print("grab minion: "+gm);
								}
								wpt = pos2;
								lastwobj = Game.Time;
								Player.CastSpell(SpellSlot.W, wobj);
							}
						}
					}
				}
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
			if (damage + 200f > unit.MagicShield + unit.Health && unit.Health / unit.MaxHealth * 100 > 10)
			{
				return true;
			}
			return false;
		}
	}
}
