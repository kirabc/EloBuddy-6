using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;

namespace ParaTristana
{
	class Program
	{
		static Menu menu;
        
		static float lastaa, lastmove, aacastdelay, aadelay;
		
		static readonly Spell.Active Q = new Spell.Active(SpellSlot.Q, 669);
        
		static readonly Spell.Targeted E = new Spell.Targeted(SpellSlot.E, 669);
        
		public static void Main(string[] args)
		{
			Loading.OnLoadingComplete += Loading_OnLoadingComplete;
		}
        
		static void Loading_OnLoadingComplete(EventArgs args)
		{
			if (!Player.Instance.ChampionName.ToLower().Contains("tristana"))
				return;
			menu = MainMenu.AddMenu("ParaTristana", "paratristana");
			menu.Add("combo", new KeyBind("Combo", false, KeyBind.BindTypes.HoldActive, ' '));
			Game.OnUpdate += Game_OnTick;
			Obj_AI_Base.OnBasicAttack += Obj_AI_Base_OnBasicAttack;
		}
		
		static void Game_OnTick(EventArgs args)
		{
			if (menu["combo"].Cast<KeyBind>().CurrentValue)
			{
				Orbwalker.DisableMovement = true;
				Orbwalker.DisableAttacking = true;
				Orb();
			}
			else
			{
				Orbwalker.DisableMovement = false;
				Orbwalker.DisableAttacking = false;
			}
		}
		
		static void Orb()
		{
			var target = GetAATarget(Player.Instance.AttackRange + Player.Instance.BoundingRadius);
			if (target == null)
			{
				if (Game.Time > lastaa + aacastdelay + 0.025f && Game.Time > lastmove + 0.150f)
				{
					Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
					lastmove = Game.Time;
				}
				return;
			}
			if (E.IsReady())
			{
				AIHeroClient t = target as AIHeroClient;
				E.Cast(t);
				Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
				return;
			}
			if (Q.IsReady())
			{
				Q.Cast();
				Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
				return;
			}
			if (Game.Time > lastaa + aadelay)
			{
				Player.IssueOrder(GameObjectOrder.AttackUnit, target);
				return;
			}
			if (Game.Time > lastaa + aacastdelay + 0.025f && Game.Time > lastmove + 0.150f)
			{
				Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
				lastmove = Game.Time;
				return;
			}
		}
		
		static AttackableUnit GetAATarget(float range)
		{
			AttackableUnit t = null;
			float num = 10000;
			foreach (var enemy in EntityManager.Heroes.Enemies)
			{
				float hp = enemy.Health;
				if (enemy.IsValidTarget(range + enemy.BoundingRadius) && hp < num)
				{
					num = hp;
					t = enemy;
				}
			}
			return t;
		}
		
		static void Obj_AI_Base_OnBasicAttack(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
		{
			if (sender.IsMe)
			{
				aacastdelay = sender.AttackCastDelay;
				aadelay = sender.AttackDelay;
				lastaa = Game.Time;
			}
		}
	}
}
