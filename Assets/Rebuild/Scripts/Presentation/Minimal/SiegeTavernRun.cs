using System;
using System.Collections.Generic;
using System.Linq;
using Team5.Rebuild.Simulation;
using UnityEngine;

namespace Team5.Rebuild.Presentation
{
    // Separate prototype economy. Never touches CampaignWallet, SquadPreparation or formal rosters.
    public sealed class SiegeTavernRun
    {
        public sealed class Recruit
        {
            public string Name,Effect;public UnitRole Role;public int Tier;public float Hp,Attack,Range=2.8f,Heal;
            public Recruit(string name,UnitRole role,int tier,float hp,float attack,string effect,float heal=0){Name=name;Role=role;Tier=tier;Hp=hp;Attack=attack;Effect=effect;Heal=heal;}
        }
        public sealed class Follower {public int Id,Kind,Slot=-1,Level=1;}
        public static readonly Recruit[] Catalog={
            new Recruit("Breach Guard",UnitRole.Attack,1,7,1.8f,"Drill: +0.3 ATK per other Breach Guard."),
            new Recruit("Iron Bulwark",UnitRole.Tank,1,16,0,"Deployment: adjacent allies gain 8% armor."),
            new Recruit("Field Medic",UnitRole.Heal,1,6,0,"Heals injured allies; cannot heal herself.",.7f),
            new Recruit("Banner Captain",UnitRole.Captain,1,12,0,"Deployment: all allies gain +2 health (once)."),
            new Recruit("Longshot",UnitRole.Sniper,2,6,3,"Outer deployment slot: +1 attack range."),
            new Recruit("Vanguard",UnitRole.Vanguard,2,12,2.2f,"Deployment: +3 health when next to a Bulwark."),
            new Recruit("Siege Engineer",UnitRole.Demolisher,2,9,3,"Explosive attacks; deals bonus tower damage."),
            new Recruit("Veteran Guard",UnitRole.Attack,3,13,4,"Deployment: other attackers gain +0.5 ATK.")};
        public readonly List<Follower> Followers=new List<Follower>();
        public readonly int[] Offers={-1,-1,-1,-1};
        public int Gold=8,Round=1,Lives=3,Tier=1;public bool Frozen,Finished;public string Result="Choose your recruits and plan a 3x3 formation.";
        private int sequence=1000;private readonly System.Random random;
        public SiegeTavernRun(int seed=526){random=new System.Random(seed);Add(0);Add(1);Add(3);Roll(false);}
        private void Add(int kind){var used=Followers.Where(f=>f.Slot>=0).Select(f=>f.Slot).ToArray();Followers.Add(new Follower{Id=++sequence,Kind=kind,Slot=used.Length<6?Enumerable.Range(0,9).First(s=>!used.Contains(s)):-1});}
        public bool Roll(bool paid=true)
        {
            if(Finished||paid&&Gold<1)return false;if(paid)Gold--;var pool=Enumerable.Range(0,Catalog.Length).Where(i=>Catalog[i].Tier<=Tier).ToArray();
            for(var i=0;i<Offers.Length;i++)Offers[i]=pool[random.Next(pool.Length)];return true;
        }
        public bool Buy(int index){if(Finished||index<0||index>=Offers.Length||Offers[index]<0||Gold<3||Followers.Count>=12)return false;Gold-=3;Add(Offers[index]);Offers[index]=-1;return true;}
        public bool Upgrade(){if(Finished||Tier>=3||Gold<5)return false;Gold-=5;Tier++;return true;}
        public bool Place(int id,int slot)
        {
            var f=Followers.Find(x=>x.Id==id);if(f==null||slot<0||slot>8)return false;
            var other=Followers.Find(x=>x.Slot==slot);
            if(f.Slot<0&&other==null&&Followers.Count(x=>x.Slot>=0)>=6)return false;if(other!=null)other.Slot=f.Slot;f.Slot=slot;return true;
        }
        public bool Bench(int id){var f=Followers.Find(x=>x.Id==id);if(f==null||f.Slot<0||Followers.Count(x=>x.Slot>=0)<=1)return false;f.Slot=-1;return true;}
        public bool Sell(int id){var f=Followers.Find(x=>x.Id==id);if(f==null||Followers.Count==1||f.Slot>=0&&Followers.Count(x=>x.Slot>=0)<=1)return false;Followers.Remove(f);Gold=Math.Min(15,Gold+1);return true;}
        public bool Train(int id){var f=Followers.Find(x=>x.Id==id);if(f==null||Gold<3||f.Level>=3)return false;Gold-=3;f.Level++;return true;}
        private static bool Adjacent(int a,int b)=>Math.Abs(a%3-b%3)+Math.Abs(a/3-b/3)==1;
        public List<MemberSpawnDefinition> Deployment()
        {
            var team=Followers.Where(f=>f.Slot>=0).ToArray();var result=new List<MemberSpawnDefinition>();
            foreach(var f in team)
            {
                var c=Catalog[f.Kind];var growth=1+.3f*(f.Level-1);var hp=c.Hp*growth+(team.Any(x=>x.Kind==3)?2:0);
                if(f.Kind==5&&team.Any(x=>x.Kind==1&&Adjacent(x.Slot,f.Slot)))hp+=3;
                var damage=c.Attack*growth;if(f.Kind==0)damage+=.3f*team.Count(x=>x.Id!=f.Id&&x.Kind==0);
                if(damage>0)damage+=.5f*team.Count(x=>x.Id!=f.Id&&x.Kind==7);
                var armor=c.Role==UnitRole.Tank?.2f:0;armor=Mathf.Min(.5f,armor+.08f*team.Count(x=>x.Id!=f.Id&&x.Kind==1&&Adjacent(x.Slot,f.Slot)));
                result.Add(new MemberSpawnDefinition(f.Id,f.Slot,new UnitDefinition(hp,.8f,c.Role,damage,c.Range+(f.Kind==4&&f.Slot!=4?1:0),1,armor,c.Heal*growth,UnitRarity.Common,f.Level,f.Kind==6?1.6f:1,f.Kind==6?AttackDamageType.Explosive:AttackDamageType.Normal)));
            }
            return result;
        }
        public void Resolve(bool victory)
        {
            if(victory)Round++;else Lives--;Finished=Lives<=0||Round>5;
            Result=Finished?(Lives>0?"VICTORY — all five defenses breached!":"DEFEAT — the expedition has ended."):(victory?"Defense breached. Prepare for stronger towers.":"Assault failed. Followers recover; one expedition life lost.");
            if(Finished)return;Gold=Math.Min(15,Gold+Math.Min(8+Round,12));
            if(!Frozen)Roll(false);else for(var i=0;i<Offers.Length;i++)if(Offers[i]<0){var pool=Enumerable.Range(0,Catalog.Length).Where(k=>Catalog[k].Tier<=Tier).ToArray();Offers[i]=pool[random.Next(pool.Length)];}
            Frozen=false;
        }
    }
}
